# Python-модель умного бота City Highways. C#-версия переносится отсюда один в один.
import heapq, itertools, random
from ch_model import *

class Params:
    def __init__(s, **kw):
        s.lam=0.7; s.foreign_cost=2.5; s.reserve=2; s.endgame=5; s.comfort=0.75; s.overflow_penalty=1.5
        s.luck=True  # брать карту удачи вместо карты вслепую из колоды
        s.luck_deficit=2; s.luck_hand=8
        s.trade=True; s.upgrade=True
        s.__dict__.update(kw)

class Plan:
    def __init__(s): s.network=set(); s.to_build=[]; s.pieces=0; s.tolls=0; s.completed=[]; s.reachable=[]; s.unreachable=[]

class Planner:
    def __init__(s, g, me, prm): s.g=g; s.me=me; s.prm=prm
    def shortest(s, a, b, via, planned):
        g=s.g; maxf=g.cfg.maxforeign; cnt=itertools.count()
        dist={}; prev={}; pq=[]
        for l in g.m.inside[a]:
            st=(l,0,False,via is None,None); dist[st]=0.0; heapq.heappush(pq,(0.0,next(cnt),st))
        target=None
        while pq:
            d,_,st=heapq.heappop(pq)
            if d>dist.get(st,1e18): continue
            l,f,own,v,last=st
            if own and v and g.m.dist(l)==b: target=st; break
            for r in g.m.at[l]:
                if r[0]==last: continue
                o=g.owner.get(r[0])
                if o==s.me or r[0] in planned: w=0.0; nf=f; no=True
                elif o is None:
                    if not g.road_open(r): continue
                    w=float(r[3]); nf=f; no=True
                else:
                    if f+1>maxf: continue
                    w=s.prm.foreign_cost*g.toll_for(r[0])/g.cfg.toll; nf=f+1; no=own
                nl=other(r,l); ns=(nl,nf,no,v or g.m.dist(nl)==via,r[0]); nd=d+w
                if nd<dist.get(ns,1e18): dist[ns]=nd; prev[ns]=(st,r); heapq.heappush(pq,(nd,next(cnt),ns))
        if target is None: return None
        path=[]; cur=target
        while cur in prev: st,r=prev[cur]; path.append(r); cur=st
        path.reverse(); return path
    def build_plan(s, contracts):
        g=s.g; p=Plan()
        for c in sorted(contracts,key=lambda c:-c[3]):
            ok,_=check_contract(g.m,c,s.me,g.owner,g.cfg.maxforeign,g.toll_for)
            if ok: p.completed.append(c); continue
            # Плечи прокладываются по очереди: второе пользуется дорогами, запланированными под первое.
            net=set(p.network); new=[]; tolls={}; reachable=True
            for a,b,via in legs(c):
                path=s.shortest(a,b,via,net)
                if path is None: reachable=False; break
                for r in path:
                    o=g.owner.get(r[0])
                    if o is None and r[0] not in net: net.add(r[0]); new.append(r)
                    elif o is not None and o!=s.me: tolls[r[0]]=g.toll_for(r[0])
            if not reachable: p.unreachable.append(c); continue
            p.reachable.append(c)
            for r in new: p.network.add(r[0]); p.to_build.append(r); p.pieces+=r[3]
            p.tolls+=sum(tolls.values())
        return p

class SmartBot:
    def __init__(s, seed=0, prm=None): s.rng=random.Random(seed); s.prm=prm or Params(); s.last_offer=-99
    def act(s,g):
        if g.phase=="setup":
            p=g.players[g.acting()]; return g.keep_initial(p.i, s.choose(g,p,g.cfg.init_keep))
        if g.phase=="choose":
            p=g.players[g.cur]; return g.keep_contracts(s.choose(g,p,g.cfg.keep))
        if g.phase=="second": return s.draw_best(g, s.analyze(g), True)
        return s.turn(g)
    def choose(s,g,p,minkeep):
        pending=list(p.pending); req=min(minkeep,len(pending)); pl=Planner(g,p.i,s.prm)
        base=pl.build_plan(p.contracts); budget=p.pieces-s.prm.reserve
        best=None; bests=-1e18; fb=None; fbk=1e18
        for mask in range(1,1<<len(pending)):
            sub=[pending[i] for i in range(len(pending)) if mask>>i&1]
            if len(sub)<req: continue
            plan=pl.build_plan(p.contracts+sub)
            reach=all(c not in plan.unreachable for c in sub)
            extra=plan.pieces-base.pieces; extra_toll=plan.tolls-base.tolls
            key=extra+(0 if reach else 1000)+len(sub)
            if key<fbk: fbk=key; fb=sub
            if not reach or plan.pieces>budget: continue
            score=sum(c[3] for c in sub)-s.prm.lam*extra-extra_toll
            comfort=p.pieces*s.prm.comfort
            if plan.pieces>comfort: score-=(plan.pieces-comfort)*s.prm.overflow_penalty
            if score>bests: bests=score; best=sub
        chosen=best or fb or pending[:req]
        return [c[0] for c in chosen]
    def analyze(s,g,who=None):
        me=g.players[g.cur if who is None else who]; plan=Planner(g,me.i,s.prm).build_plan(me.contracts)
        desired=[r for r in plan.to_build if r[3]<=me.pieces]; goals=bool(desired)
        if not goals:
            mine=set()
            for r in me.roads: mine|={r[1],r[2]}
            opens=[r for r in g.m.roads.values() if r[0] not in g.owner and r[3]<=me.pieces and g.road_open(r)]
            # Сначала дороги, которые продвигают открытую цель мэрии, затем — примыкающие к своей сети.
            live=[x for x in g.goals if x["id"] not in g.claimed]
            base={x["id"]:goal_progress(g.m,x,me.roads) for x in live}
            def helps(r): return sum(1 for x in live if goal_progress(g.m,x,me.roads+[r])>base[x["id"]])
            opens.sort(key=lambda r:(-helps(r), -(r[1] in mine or r[2] in mine), -r[3], r[0]))
            desired=opens[:3]
        need=[0]*5; gray=0
        for r in desired:
            if r[4] is not None: need[r[4]]+=r[3]
            else: gray=max(gray,r[3])
        return dict(me=me,desired=desired,goals=goals,need=need,gray=gray)
    # ---------------- сделки
    @staticmethod
    def surplus_of(a,x):
        me=a["me"]
        if x==MA: return me.hand[MA]-3          # технику бережём: она джокер
        return me.hand[x]-a["need"][x]-(a["gray"] if a["gray"]>0 else 0)
    def propose(s,g,a):
        """Раз в ход: предложить излишек в обмен на нужный ресурс сопернику по кругу."""
        me=a["me"]; need=a["need"]
        # Предлагаем редко: когда до нужной дороги не хватает 1–2 карт, и не чаще раза в 3 своих хода.
        deficit=sum(max(0,need[x]-me.hand[x]) for x in BUILDING)
        if not a["goals"] or deficit==0 or deficit>2 or g.turn-s.last_offer<3*len(g.players): return
        want=[x for x in BUILDING if need[x]>me.hand[x]]
        spare=[x for x in BUILDING if s.surplus_of(a,x)>=1 and x not in want]
        if not want or not spare: return
        x=max(want,key=lambda c:(need[c]-me.hand[c],-c)); y=max(spare,key=lambda c:(s.surplus_of(a,c),-c))
        others=[p.i for p in g.players if p.i!=me.i]
        to=others[g.turn%len(others)]; s.last_offer=g.turn
        give=2 if s.surplus_of(a,y)>=2 else 1
        g.propose_trade(to,y,give,x,1,lambda gg,t,o: gg.responders[t].respond(gg,t,o) if getattr(gg,"responders",None) else False)
    def respond(s,g,who,offer):
        """Принять сделку, если отдаём лишнее, а получаем нужное (или больше, чем отдаём)."""
        if not s.prm.trade: return False
        a=s.analyze(g,who); me=a["me"]
        give_res,give_n=offer["get_res"],offer["get_n"]     # что отдаю я
        take_res,take_n=offer["give_res"],offer["give_n"]   # что получаю
        if me.hand[give_res]<give_n or s.surplus_of(a,give_res)<give_n: return False
        useful=a["need"][take_res]>me.hand[take_res] or (a["gray"]>0 and take_res!=MA)
        return useful or take_n>give_n
    def turn(s,g):
        a=s.analyze(g); me=a["me"]
        if s.prm.trade and g.can_trade():
            s.propose(g,a); a=s.analyze(g); me=a["me"]
        endgame=g.final>0 or any(p.pieces<=s.prm.endgame for p in g.players)
        # Прораб тратит бесплатную стройку на нужную дорогу полной длины (или на любую нужную в конце партии).
        if me.char=="foreman" and me.free>0:
            free=[r for r in a["desired"] if g.can_build_free(r[0])]
            free=[r for r in free if r[3]==g.cfg.foreman_len or endgame]
            if free: return g.build_free(max(free,key=lambda r:(r[3],r[0]))[0])
        aff=[(r,g.options(r[0])) for r in a["desired"]]; aff=[x for x in aff if x[1]]
        if a["goals"] and aff: return s.build(g,a,max(aff,key=lambda x:(x[0][3],x[0][0])))
        if endgame:
            allaff=[(r,g.options(r[0])) for r in g.m.roads.values()]; allaff=[x for x in allaff if x[1]]
            if allaff: return s.build(g,a,max(allaff,key=lambda x:(x[0][3],x[0][0])))
        # Улучшение: в конце партии, когда строить нечего, или с переполненной рукой —
        # самую «проезжую» свою дорогу (больше всего соседних дорог) делаем магистралью.
        if s.prm.upgrade and endgame:
            ups=[(r,g.upgrade_options(r[0])) for r in me.roads]; ups=[x for x in ups if x[1]]
            if ups:
                def busy(r): return len(g.m.at[r[1]])+len(g.m.at[r[2]])
                r,opts=max(ups,key=lambda x:(busy(x[0]),x[0][3],x[0][0]))
                o=sorted(opts,key=lambda o:(1 if o[0]==MA else 0,o[1]))[0]
                return g.upgrade(r[0],o[0],o[1])
        if not a["goals"]:
            calm=not any(p.pieces<=12 for p in g.players)
            if g.cdeck and me.pieces>=14 and calm and g.final<=0: return g.draw_contracts()
            if aff:
                lg=max(aff,key=lambda x:(x[0][3],x[0][0]))
                if lg[0][3]>=3 or sum(me.hand)>=10: return s.build(g,a,lg)
        if g.can_draw_any(): return s.draw_best(g,a,False)
        allaff=[(r,g.options(r[0])) for r in g.m.roads.values()]; allaff=[x for x in allaff if x[1]]
        if allaff: return s.build(g,a,max(allaff,key=lambda x:(x[0][3],x[0][0])))
        if g.cdeck: return g.draw_contracts()
        return g.pas()
    def build(s,g,a,choice):
        r,opts=choice; me=a["me"]
        def surplus(o):
            res,mach=o
            if res==MA: return 0
            spent=r[3]-mach; need=a["need"][res]-(r[3] if r[4]==res else 0)
            return me.hand[res]-spent-need
        o=sorted(opts,key=lambda o:(1 if o[0]==MA else 0, o[1], -surplus(o)))[0]
        return g.build(r[0],o[0],o[1])
    def draw_best(s,g,a,second):
        me=a["me"]; need=a["need"]
        deficit=sum(max(0,need[x]-me.hand[x]) for x in BUILDING)
        surplus=lambda x: me.hand[x]-need[x]
        grayc=max(BUILDING,key=lambda x:(surplus(x),-x))
        grayd=max(0,a["gray"]-max(0,surplus(grayc))) if a["gray"]>0 else 0
        deficit+=grayd
        def val(c):
            if c==MA: return 2.5 if deficit>=3 else 0.5
            if need[c]>me.hand[c]: return 3.0
            if grayd>0 and c==grayc: return 2.5
            if grayd>0 and surplus(c)>=0: return 1.5
            return 0.0
        best=-1; bs=-1e18
        for i,c in enumerate(g.face):
            if c is None or (second and c==MA): continue
            v=val(c)+s.rng.random()*0.01
            if v>bs: bs=v; best=i
        deck=g.avail()>0
        if best>=0 and (bs>=1.5 or not deck): return g.draw_face(best)
        # Удача — риск: берём её, только когда ресурсы не горят (дефицит мал или рука полная).
        bold=me.char in g.cfg.bold_luck  # кому удача выгоднее — рискуют всегда
        if s.prm.luck and g.can_luck() and (bold or deficit<=s.prm.luck_deficit or sum(me.hand)>=s.prm.luck_hand):
            return g.draw_luck()
        if deck: return g.draw_deck()
        return False
