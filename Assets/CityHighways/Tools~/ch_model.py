# Построчная Python-модель CityHighways.Core для проверки логики без компилятора C#.
import json, random, collections
AS,CO,ST,GR,MA = range(5); BUILDING=[AS,CO,ST,GR]
RES={"asphalt":AS,"concrete":CO,"steel":ST,"gravel":GR}
class Cfg:
    def __init__(s, **kw):
        s.pieces={2:35,3:32,4:28,5:25}; s.override=0; s.hand=4; s.cards=[26,24,10,18]; s.mach=14
        s.faceup=5; s.maxmach=2; s.init_deal=3; s.init_keep=2; s.draw=3; s.keep=1; s.final=2
        s.toll=2; s.maxforeign=2; s.bonus=10; s.points=[0,1,2,4,7,10,15]
        s.events=True; s.rush_mult=2; s.repair_roads=2; s.subsidy=1
        # parade — дороги, касающиеся Центра, закрыты для стройки на круг; snow — щебёночные дороги на 1 карту дороже;
        # investor — каждая дорога этого круга приносит +investor очков.
        s.event_counts={'rush':3,'repair':3,'subsidy':3,'parade':2,'snow':2,'investor':2}
        s.snow=1; s.investor=2; s.parade_district="center"; s.event_chance=0.5
        # Улучшение дороги: своя дорога становится магистралью (проезд ×rush_mult) за ceil(L/2) карт её ресурса,
        # действие хода, +upgrade_points очков.
        s.upgrade=True; s.upgrade_points=1
        # Сделки: раз в ход до основного действия предложить обмен 1–3 карт на 1–3 карты одному сопернику.
        s.trades=True; s.trade_max=3
        # Цели мэрии и колода удачи
        s.goals_dealt=3; s.luck=True
        s.luck_counts={'grant':4,'supply':4,'premium':3,'fine':3,'theft':2,'recall':2}
        s.grant=5; s.supply=3; s.premium_per=3; s.premium_max=9; s.fine=3; s.theft=1
        # Персонажи: lucky — открывает 2 карты удачи и берёт одну; foreman — foreman_free раз за партию
        # строит дорогу длиной до foreman_len без карт; insurer — дебаф удачи заменяется на −insurer_fine;
        # tollman — проезд по его дорогам toll_tollman; official — +official_bonus за каждый выполненный контракт;
        # bridger — мосты на bridger_discount карт дешевле (не меньше 1) и +bridger_points за каждый свой мост.
        s.foreman_free=1; s.foreman_len=2; s.insurer_fine=1; s.toll_tollman=4;  s.bridger_discount=1; s.bridger_points=1; s.official_bonus=2; s.bold_luck=("insurer",)
        s.__dict__.update(kw)
    def total(s): return sum(s.cards)+s.mach
class Map:
    def __init__(s, districts, locations, roads, contracts, expansions=(), goals=()):
        s.goals=list(goals); s.districts=districts; s.expansions=set(expansions); s.loc={l[0]:l for l in locations}; s.roads={r[0]:r for r in roads}; s.contracts={c[0]:c for c in contracts}
        s.at=collections.defaultdict(list); s.inside=collections.defaultdict(list)
        for r in roads: s.at[r[1]].append(r); s.at[r[2]].append(r)
        for l in locations: s.inside[l[1]].append(l[0])
    def dist(s,l): return s.loc[l][1]
def load_moscow(p):
    d=json.load(open(p))
    return Map([x["id"] for x in d["districts"]],[(l["id"],l["district"]) for l in d["locations"]],
               [(r["id"],r["from"],r["to"],r["length"],RES.get(r["resource"])) for r in d["roads"]],
               [(c["id"],c["from"],c["to"],c["points"],c.get("kind","link"),c.get("third")) for c in d["contracts"]],
               [x["id"] for x in d["districts"] if x.get("expansion")], d.get("goals",[]))
def other(r,l): return r[2] if l==r[1] else r[1]
def legs(c):
    """Плечи контракта (откуда, куда, через): link A–B; transit — один путь A–B через район C; hub — A–B и A–C."""
    kind=c[4] if len(c)>4 else "link"
    if kind=="transit": return [(c[1],c[2],c[5])]
    if kind=="hub": return [(c[1],c[2],None),(c[1],c[5],None)]
    return [(c[1],c[2],None)]
def districts(c): return [d for d in (c[1],c[2],c[5] if len(c)>5 else None) if d]
def check_contract(m, c, p, owner, maxf, toll_of=lambda rid: 2):
    """Контракт выполнен, если выполнено каждое плечо. Чужой участок, общий для плеч, оплачивается один раз."""
    foreign={}
    for a,b,via in legs(c):
        ok,f=check_leg(m,a,b,via,p,owner,maxf,toll_of)
        if not ok: return False,[]
        for r in f: foreign.setdefault(r[0],r)
    return True,list(foreign.values())
def check_leg(m, a, b, via, p, owner, maxf, toll_of=lambda rid: 2):
    """Лучший путь: минимум платы за проезд (при равенстве — меньше чужих участков), не больше maxf чужих, есть своя дорога."""
    import heapq, itertools
    cnt=itertools.count(); dist={}; prev={}; pq=[]
    for l in m.inside[a]:
        st=(l,False,0,via is None or a==via,None); dist[st]=(0,0); heapq.heappush(pq,((0,0),next(cnt),st))
    target=None
    while pq:
        dkey,_,st=heapq.heappop(pq)
        if dkey>dist.get(st,(1e9,1e9)): continue
        l,own,k,v,last=st
        if own and v and m.dist(l)==b: target=st; break
        for r in m.at[l]:
            if r[0]==last: continue  # без разворота по той же дороге
            o=owner.get(r[0])
            if o is None: continue
            mine=o==p
            nk=k+(0 if mine else 1)
            if nk>maxf: continue
            nl=other(r,l)
            ns=(nl, own or mine, nk, v or m.dist(nl)==via, r[0])
            nd=(dkey[0]+(0 if mine else toll_of(r[0])), nk)
            if nd<dist.get(ns,(1e9,1e9)): dist[ns]=nd; prev[ns]=(st,r); heapq.heappush(pq,(nd,next(cnt),ns))
    if target is None: return False,[]
    foreign=[]; cur=target
    while cur in prev:
        st,r=prev[cur]
        if owner.get(r[0])!=p: foreign.append(r)
        cur=st
    return True,foreign
def goal_progress(m, goal, roads):
    """Сколько уже сделано для цели мэрии (цель выполнена, когда progress >= goal["count"])."""
    k=goal["kind"]
    if k=="locations":
        touched=set()
        for r in roads: touched|={r[1],r[2]}
        return sum(1 for l in m.inside[goal["district"]] if l in touched)
    if k=="bridges": return sum(1 for r in roads if r[4]==ST)
    if k=="network": return network(m,roads)
    if k=="resource_pieces": return sum(r[3] for r in roads if r[4]==RES[goal["resource"]])
    if k=="long_roads": return sum(1 for r in roads if r[3]>=goal["length"])
    raise ValueError(k)
def goal_done(m, goal, roads): return goal_progress(m,goal,roads)>=goal["count"]
def network(m, roads):
    parent={}
    def f(x):
        parent.setdefault(x,x)
        while parent[x]!=x: parent[x]=parent[parent[x]]; x=parent[x]
        return x
    locs=set()
    for r in roads: parent[f(r[1])]=f(r[2]); locs|={r[1],r[2]}
    g=collections.defaultdict(set)
    for l in locs: g[f(l)].add(m.dist(l))
    return max((len(v) for v in g.values()), default=0)
class Player:
    def __init__(s,i,pieces,char=None): s.i=i; s.char=char; s.free=0; s.late=set(); s.pieces=pieces; s.pts=0; s.hand=[0]*5; s.roads=[]; s.contracts=[]; s.pending=[]; s.goal_pts=0; s.luck_pts=0
class Game:
    def __init__(s, m, n, cfg=None, seed=0, chars=None):
        s.m=m; s.cfg=cfg or Cfg(); s.rng=random.Random(seed); s.chars=list(chars) if chars else [None]*n
        s.draw=[c for r,k in zip(BUILDING,s.cfg.cards) for c in [r]*k]+[MA]*s.cfg.mach; s.rng.shuffle(s.draw); s.disc=[]
        s.open=set(d for d in m.districts if d not in m.expansions)
        s.cdeck=[c for c in m.contracts.values() if all(d in s.open for d in districts(c))]; s.rng.shuffle(s.cdeck)
        s.event=None; s.edeck=[]; s.repair=set(); s.double=set(); s.events_seen=[]
        s.face=[None]*s.cfg.faceup; s.owner={}; s.final=-1; s.passes=0; s.result=None
        s.trade_used=False; s.trade_offers=0; s.trades_done=0; s.upgrades=0
        # Цели мэрии: открыты для всех, награду получает первый выполнивший.
        pool=list(m.goals); s.rng.shuffle(pool); s.goals=pool[:s.cfg.goals_dealt]; s.claimed={}
        # Колода удачи: берётся вместо одной из двух карт ресурсов, не больше одной за ход.
        s.ldeck=[]; s.ldisc=[]; s.luck_taken=False; s.last_luck=None; s.luck_log=collections.Counter()
        if s.cfg.luck:
            for kind,count in s.cfg.luck_counts.items(): s.ldeck+=[kind]*count
            s.rng.shuffle(s.ldeck)
        pieces=s.cfg.override or s.cfg.pieces[n]
        s.players=[Player(i,pieces,s.chars[i]) for i in range(n)]
        for p in s.players:
            if p.char=="foreman": p.free=s.cfg.foreman_free
        for p in s.players:
            for _ in range(s.cfg.hand):
                c=s.trydraw()
                if c is not None: p.hand[c]+=1
            p.pending=s.cdeck[:s.cfg.init_deal]; s.cdeck=s.cdeck[s.cfg.init_deal:]
        s.refill(); s.phase="setup"; s.cur=0; s.turn=0; s.setup_done()
    def trydraw(s):
        if not s.draw and s.disc: s.draw=s.disc; s.disc=[]; s.rng.shuffle(s.draw)
        return s.draw.pop() if s.draw else None
    def avail(s): return len(s.draw)+len(s.disc)
    def acting(s):
        if s.phase=="over": return -1
        if s.phase=="setup":
            for p in s.players:
                if p.pending: return p.i
            return -1
        return s.cur
    def can_draw_any(s): return s.avail()>0 or any(c is not None for c in s.face)
    def can_second(s): return s.avail()>0 or any(c is not None and c!=MA for c in s.face)
    def fill(s):
        for i in range(len(s.face)):
            if s.face[i] is None:
                c=s.trydraw()
                if c is not None: s.face[i]=c
    def fix(s):
        for _ in range(10):
            if sum(1 for c in s.face if c==MA)<=s.cfg.maxmach: return
            if s.avail()<len(s.face): return
            for i,c in enumerate(s.face):
                if c is not None: s.disc.append(c); s.face[i]=None
            s.fill()
    def refill(s): s.fill(); s.fix()
    def setup_done(s):
        if s.phase!="setup" or any(p.pending for p in s.players): return
        s.phase="await"; s.cur=0; s.turn=1; s.start_round()
    # ---------------- события
    def closed_expansions(s): return sorted(d for d in s.m.expansions if d not in s.open)
    def road_open(s,r): return s.m.dist(r[1]) in s.open and s.m.dist(r[2]) in s.open
    def toll_for(s,rid):
        o=s.owner.get(rid)
        base=s.cfg.toll_tollman if o is not None and s.players[o].char=="tollman" else s.cfg.toll
        return base*(s.cfg.rush_mult if rid in s.double else 1)
    def cost(s,r,p=None):
        p=p or s.players[s.cur]; L=r[3]
        if r[4]==GR and s.event and s.event["kind"]=="snow": L+=s.cfg.snow
        if r[4]==ST:
            if s.event and s.event["kind"]=="subsidy": L-=s.cfg.subsidy
            if p.char=="bridger": L-=s.cfg.bridger_discount
            L=max(1,L)
        return L
    def build_event_deck(s):
        deck=[]
        for k,n in s.cfg.event_counts.items(): deck+=[k]*n
        deck+=["district"]*len(s.closed_expansions())
        s.rng.shuffle(deck); return deck
    def start_round(s):
        s.repair=set(); s.event=None
        if not s.cfg.events: return
        if s.cfg.event_chance<1 and s.rng.random()>=s.cfg.event_chance: return
        for _ in range(50):
            if not s.edeck: s.edeck=s.build_event_deck()
            if not s.edeck: return
            k=s.edeck.pop()
            if k=="district" and not s.closed_expansions(): continue
            break
        ev={"kind":k}
        if k=="repair":
            cand=sorted(r[0] for r in s.m.roads.values() if r[0] not in s.owner and s.road_open(r))
            s.rng.shuffle(cand); s.repair=set(cand[:s.cfg.repair_roads]); ev["roads"]=sorted(s.repair)
        if k=="district":
            d=s.rng.choice(s.closed_expansions()); s.open.add(d); ev["district"]=d
            new=[c for c in s.m.contracts.values() if d in districts(c) and all(x in s.open for x in districts(c))]
            s.rng.shuffle(new); s.cdeck=new+s.cdeck
        s.event=ev; s.events_seen.append(k)
    def available(s,p,r):
        if r[0] in s.owner: return "owned"
        if not s.road_open(r): return "closed district"
        if r[0] in s.repair: return "repair"
        if s.event and s.event["kind"]=="parade" and s.cfg.parade_district in (s.m.dist(r[1]),s.m.dist(r[2])): return "parade"
        if p.pieces<r[3]: return "pieces"
        return None
    def options(s,rid):
        if s.phase!="await": return []
        r=s.m.roads[rid]; p=s.players[s.cur]
        if s.available(p,r): return []
        mach=p.hand[MA]; res=[r[4]] if r[4] is not None else BUILDING; out=[]; L=s.cost(r)
        for x in res:
            have=p.hand[x]
            if have==0: continue
            need=L-min(have,L)
            if need<=mach: out.append((x,need))
        if mach>=L: out.append((MA,L))
        return out
    def any_legal(s):
        if s.phase!="await": return True
        if s.can_draw_any() or s.cdeck: return True
        return any(s.options(r) for r in s.m.roads)
    def keep(s,pi,ids,minkeep):
        p=s.players[pi]; req=min(minkeep,len(p.pending)); kept=[]
        for i in ids:
            c=[x for x in p.pending if x[0]==i]
            if not c: return False
            if c[0] in kept: return False
            kept.append(c[0])
        if len(kept)<req: return False
        p.contracts+=kept; s.cdeck+= [c for c in p.pending if c not in kept]; p.pending=[]
        return True
    def keep_initial(s,pi,ids):
        if s.phase!="setup" or not s.players[pi].pending: return False
        if not s.keep(pi,ids,s.cfg.init_keep): return False
        s.setup_done(); return True
    def draw_face(s,slot):
        if s.phase not in("await","second"): return False
        if s.face[slot] is None: return False
        c=s.face[slot]; second=s.phase=="second"
        if second and c==MA: return False
        p=s.players[s.cur]; p.hand[c]+=1; s.face[slot]=s.trydraw(); s.fix()
        if second or c==MA or not s.can_second(): s.end_turn()
        else: s.phase="second"
        return True
    def draw_deck(s):
        if s.phase not in("await","second"): return False
        c=s.trydraw()
        if c is None: return False
        s.players[s.cur].hand[c]+=1
        if s.phase=="second" or not s.can_second(): s.end_turn()
        else: s.phase="second"
        return True
    # ---------------- удача
    def can_luck(s):
        # Удача — только вместо карты ресурса, поэтому без доступных карт ресурсов её не взять.
        return (s.cfg.luck and s.phase in("await","second") and not s.luck_taken and bool(s.ldeck or s.ldisc)
                and s.can_draw_any())
    def draw_luck(s):
        if not s.can_luck(): return False
        p=s.players[s.cur]
        if not s.ldeck: s.ldeck=s.ldisc; s.ldisc=[]; s.rng.shuffle(s.ldeck)
        k=s.ldeck.pop(); s.ldisc.append(k)
        if p.char=="lucky":
            # Везунчик открывает вторую карту (если в колоде она есть) и оставляет лучшую; обе уходят в сброс.
            if s.ldeck:
                k2=s.ldeck.pop(); s.ldisc.append(k2)
                if s.luck_value(p,k2)>s.luck_value(p,k): k=k2
        s.luck_taken=True; s.last_luck=k; s.luck_log[k]+=1
        s.apply_luck(p,k)
        if s.phase=="second" or not s.can_second(): s.end_turn()
        else: s.phase="second"
        return True
    def luck_value(s,p,k):
        """Оценка карты удачи для выбора Везунчика (тот же расчёт у бота C#)."""
        c=s.cfg; debuff=k in("fine","theft","recall")
        if debuff and p.char=="insurer": return -c.insurer_fine
        if k=="grant": return c.grant
        if k=="supply": return 1.5*c.supply
        if k=="premium":
            done=sum(1 for ct in p.contracts if check_contract(s.m,ct,p.i,s.owner,c.maxforeign,s.toll_for)[0])
            return min(c.premium_max,c.premium_per*done)
        if k=="fine": return -c.fine
        if k=="theft": return -4.0*min(c.theft,sum(p.hand))
        open_=[ct for ct in p.contracts if not check_contract(s.m,ct,p.i,s.owner,c.maxforeign,s.toll_for)[0]]
        return -0.6*min(x[3] for x in open_) if open_ else -c.fine
    def apply_luck(s,p,k):
        c=s.cfg
        if k in("fine","theft","recall") and p.char=="insurer": p.luck_pts-=c.insurer_fine; return
        if k=="grant": p.luck_pts+=c.grant
        elif k=="supply":
            for _ in range(c.supply):
                x=s.trydraw()
                if x is not None: p.hand[x]+=1
        elif k=="premium":
            done=sum(1 for ct in p.contracts if check_contract(s.m,ct,p.i,s.owner,c.maxforeign,s.toll_for)[0])
            p.luck_pts+=min(c.premium_max,c.premium_per*done)
        elif k=="fine": p.luck_pts-=c.fine
        elif k=="theft":
            for _ in range(c.theft):
                cards=[x for x in range(5) for _ in range(p.hand[x])]
                if not cards: break
                x=s.rng.choice(cards); p.hand[x]-=1; s.disc.append(x)
        elif k=="recall":
            open_=[ct for ct in p.contracts if not check_contract(s.m,ct,p.i,s.owner,c.maxforeign,s.toll_for)[0]]
            # Самый дешёвый невыполненный контракт уходит обратно под колоду, без штрафа.
            # Нет невыполненных — штраф как у «Проверки».
            if not open_: p.luck_pts-=c.fine
            else:
                ct=min(open_,key=lambda x:(x[3],x[0])); p.contracts.remove(ct); s.cdeck.append(ct)
        else: raise ValueError(k)
    def check_goals(s,p):
        for g in s.goals:
            if g["id"] in s.claimed: continue
            if goal_done(s.m,g,p.roads): s.claimed[g["id"]]=p.i; p.goal_pts+=g["points"]
    def build(s,rid,res,mach):
        if s.phase!="await": return False
        r=s.m.roads[rid]; p=s.players[s.cur]
        if s.available(p,r): return False
        cards=s.cost(r)-mach
        if res==MA and cards!=0: return False
        if res!=MA and cards>0 and r[4] is not None and r[4]!=res: return False
        if p.hand[MA]<mach or (cards>0 and p.hand[res]<cards): return False
        if cards>0: p.hand[res]-=cards; s.disc+=[res]*cards
        if mach>0: p.hand[MA]-=mach; s.disc+=[MA]*mach
        s.place(p,r)
        s.end_turn(); return True
    def place(s,p,r):
        p.pieces-=r[3]; p.roads.append(r); s.owner[r[0]]=p.i; p.pts+=s.cfg.points[r[3]]
        if s.event and s.event["kind"]=="rush": s.double.add(r[0])
        if s.event and s.event["kind"]=="investor": p.pts+=s.cfg.investor
        s.check_goals(p)
    # ---------------- улучшение дорог
    def upgrade_cost(s,r): return (r[3]+1)//2
    def upgrade_options(s,rid):
        if not s.cfg.upgrade or s.phase!="await": return []
        r=s.m.roads[rid]; p=s.players[s.cur]
        if s.owner.get(rid)!=p.i or rid in s.double: return []
        L=s.upgrade_cost(r); mach=p.hand[MA]; out=[]
        for x in ([r[4]] if r[4] is not None else BUILDING):
            have=p.hand[x]
            if have==0: continue
            need=L-min(have,L)
            if need<=mach: out.append((x,need))
        if mach>=L: out.append((MA,L))
        return out
    def upgrade(s,rid,res,mach):
        if (res,mach) not in s.upgrade_options(rid): return False
        r=s.m.roads[rid]; p=s.players[s.cur]; cards=s.upgrade_cost(r)-mach
        if cards>0: p.hand[res]-=cards; s.disc+=[res]*cards
        if mach>0: p.hand[MA]-=mach; s.disc+=[MA]*mach
        s.double.add(rid); p.pts+=s.cfg.upgrade_points; s.upgrades+=1
        s.end_turn(); return True
    # ---------------- сделки
    def can_trade(s):
        return s.cfg.trades and s.phase=="await" and not s.trade_used
    def propose_trade(s,to,give_res,give_n,get_res,get_n,respond):
        """Текущий игрок отдаёт give_n карт give_res и получает get_n карт get_res от игрока to,
        если тот согласится (respond(game, to, offer) -> bool) и у обоих есть карты. Раз в ход, ход не тратит."""
        if not s.can_trade() or to==s.cur or not (0<=to<len(s.players)): return None
        if not (1<=give_n<=s.cfg.trade_max and 1<=get_n<=s.cfg.trade_max): return None
        p=s.players[s.cur]; q=s.players[to]
        if p.hand[give_res]<give_n: return None
        s.trade_used=True; s.trade_offers+=1
        offer=dict(frm=s.cur,to=to,give_res=give_res,give_n=give_n,get_res=get_res,get_n=get_n)
        if q.hand[get_res]<get_n or not respond(s,to,offer): return False
        p.hand[give_res]-=give_n; q.hand[give_res]+=give_n
        q.hand[get_res]-=get_n; p.hand[get_res]+=get_n
        s.trades_done+=1; return True
    def can_build_free(s,rid):
        if s.phase!="await": return False
        r=s.m.roads[rid]; p=s.players[s.cur]
        return p.char=="foreman" and p.free>0 and r[3]<=s.cfg.foreman_len and not s.available(p,r)
    def build_free(s,rid):
        """Прораб: дорога длиной до foreman_len без карт (очки — как обычно)."""
        if not s.can_build_free(rid): return False
        r=s.m.roads[rid]; p=s.players[s.cur]; p.free-=1
        s.place(p,r)
        s.end_turn(); return True
    def draw_contracts(s):
        if s.phase!="await" or not s.cdeck: return False
        p=s.players[s.cur]; p.pending=s.cdeck[:s.cfg.draw]; s.cdeck=s.cdeck[s.cfg.draw:]; s.phase="choose"; return True
    def keep_contracts(s,ids):
        if s.phase!="choose": return False
        if not s.keep(s.cur,ids,s.cfg.keep): return False
        s.end_turn(); return True
    def pas(s):
        if s.phase!="await" or s.any_legal(): return False
        s.passes+=1
        if s.passes>=len(s.players): s.finish(); return True
        s.end_turn(True); return True
    def end_turn(s,passed=False):
        if not passed: s.passes=0
        p=s.players[s.cur]
        if s.final>0:
            s.final-=1
            if s.final==0: s.finish(); return
        elif s.final<0 and p.pieces<=s.cfg.final: s.final=len(s.players)
        s.refill(); s.cur=(s.cur+1)%len(s.players); s.turn+=1; s.phase="await"; s.luck_taken=False; s.trade_used=False
        if s.cur==0: s.start_round()
    def finish(s):
        s.phase="over"; sc=[dict(i=p.i,road=p.pts,cp=0,ok=0,fail=0,paid=0,earned=0,net=network(s.m,p.roads),bonus=0,char=0) for p in s.players]
        for p in s.players:
            for c in p.contracts:
                ok,f=check_contract(s.m,c,p.i,s.owner,s.cfg.maxforeign,s.toll_for)
                if not ok: sc[p.i]["cp"]-=c[3]; sc[p.i]["fail"]+=1; continue
                sc[p.i]["cp"]+=c[3]; sc[p.i]["ok"]+=1
                if p.char=="official": sc[p.i]["char"]+=s.cfg.official_bonus  # связи в мэрии
                for r in f: t=s.toll_for(r[0]); sc[p.i]["paid"]+=t; sc[s.owner[r[0]]]["earned"]+=t
        best=max(x["net"] for x in sc)
        if best>0:
            for x in sc:
                if x["net"]==best: x["bonus"]=s.cfg.bonus
        for p,x in zip(s.players,sc): x["goals"]=p.goal_pts; x["luck"]=p.luck_pts
        for p,x in zip(s.players,sc):
            if p.char=="bridger": x["char"]+=s.cfg.bridger_points*sum(1 for r in p.roads if r[4]==ST)
        for x in sc: x["total"]=x["road"]+x["cp"]-x["paid"]+x["earned"]+x["bonus"]+x["goals"]+x["luck"]+x["char"]
        s.result=sc
    def count_cards(s): return sum(sum(p.hand) for p in s.players)+sum(1 for c in s.face if c is not None)+s.avail()
class RandomBot:
    def __init__(s,seed): s.rng=random.Random(seed)
    def act(s,g):
        if g.phase=="setup":
            p=g.players[g.acting()]; return g.keep_initial(p.i,[c[0] for c in p.pending])
        if g.phase=="choose": return g.keep_contracts([g.players[g.cur].pending[0][0]])
        if g.phase=="second": return s.drawc(g,True)
        builds=[(r,o) for r in g.m.roads for o in g.options(r)]
        cd=g.can_draw_any(); ct=bool(g.cdeck); roll=s.rng.random()
        if builds and (roll<0.5 or not cd): r,o=s.rng.choice(builds); return g.build(r,o[0],o[1])
        if cd and (roll<0.95 or not ct): return s.drawc(g,False)
        if ct: return g.draw_contracts()
        if builds: r,o=s.rng.choice(builds); return g.build(r,o[0],o[1])
        return g.pas()
    def drawc(s,g,second):
        ch=([-1] if g.avail()>0 else [])+[i for i,c in enumerate(g.face) if c is not None and not(second and c==MA)]
        if not ch: return False
        c=s.rng.choice(ch); return g.draw_deck() if c<0 else g.draw_face(c)
