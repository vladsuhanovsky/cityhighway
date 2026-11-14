# Модель аукциона мэрии, взяток и обвинений поверх ch_influence (правила GameAuction.cs / GameBribes.cs,
# поведение — как у SmartBot.cs). Проверяет, нет ли беспроигрышной стратегии: «всегда давать взятку»,
# «никогда не обвинять», «перебивать всех на аукционе» и т. п.
# Запуск: python3 ch_politics.py [карта] [партий] [игроков]
import sys, math, collections
from ch_model import *
from ch_influence import InfGame, InfCfg, InfBot

class PolCfg(InfCfg):
    def __init__(s, **kw):
        s.auction=True; s.auction_share=0.34; s.auction_candidates=5; s.auction_bonus=5
        s.bribes=True; s.bribe_discount=2; s.bribe_extra=2; s.whistle=2; s.false_fine=3
        # Штраф по числу игроков (подобран этой моделью); bribe_fine>0 — один для всех. Вдвоём взяток нет.
        s.bribe_fine_by={3:6,4:4,5:4}; s.bribe_fine=0; s.bribes_min=3
        # Тайный шёпот (этап 21): о взятках объявляют в начале следующего круга числом, без имён (в дуэли — никак);
        # раскрыть взятку можно до конца круга после неё. Улика «дешевле» — дорога дешевле своей длины.
        s.secret=True
        s.bribe_power=1   # скидка в картах / сколько карт даёт «Поставка»
        super().__init__(**kw)
        # Обновление рынка: 4, каждое следующее у того же игрока +2 (этап 21).
        s.cost_refresh=kw.get("cost_refresh",4); s.refresh_step=kw.get("refresh_step",2)

class PolGame(InfGame):
    def __init__(s, m, n, cfg=None, seed=0, chars=None):
        s.pol_ready=False
        super().__init__(m, n, cfg or PolCfg(), seed, chars)
        for p in s.players: p.pen=0; p.mayor=None; p.bribes_given=0; p.bribes_exposed=0; p.accusations=0; p.false_acc=0
        s.auction_held=False; s.auction_log=[]
        s.bribes_active=[]; s.bribed=False; s.accused=False; s.discount=False
        s.hand_start=None; s.paid_less=False; s.suspicious=[False]*n
        s.round=0; s.announced=0
        s.pol_ready=True
    # ---------------- аукцион
    def visible(s,p): return p.pts+p.goal_pts+p.luck_pts+p.pen
    def start_round(s):
        super().start_round()
        if getattr(s,"pol_ready",False):
            s.round+=1
            if s.cfg.secret:
                # Взятки прошлого круга объявляются числом; более старые уже не раскрыть.
                s.bribes_active=[b for b in s.bribes_active if b[2]>=s.round-1]
                s.announced=sum(1 for b in s.bribes_active if b[2]==s.round-1) if len(s.players)>=3 else 0
            s.try_auction()
    def try_auction(s):
        c=s.cfg
        if not c.auction or s.auction_held or s.turn<=1 or s.phase!="await": return
        start=c.override or c.pieces[len(s.players)]
        thr=math.ceil(start*c.auction_share)
        if not any(start-p.pieces>=thr for p in s.players): return
        cand=[x for x in s.cdeck[:c.auction_candidates] if all(d in s.open for d in districts(x))]
        if not cand: return
        contract=max(cand,key=lambda x:(x[3],[-ord(ch) for ch in x[0]]))
        s.cdeck.remove(contract); s.auction_held=True
        bots=getattr(s,"bots",None)
        bids=[(bots[p.i].bid(s,p,contract) if bots and p.inf>0 else 0) for p in s.players]
        bids=[max(0,min(b,p.inf)) for b,p in zip(bids,s.players)]
        best=max(bids); winner=-1
        if best>0:
            tied=[i for i in range(len(s.players)) if bids[i]==best]
            winner=min(tied,key=lambda i:(s.visible(s.players[i]),(i-s.cur)%len(s.players)))
            p=s.players[winner]; p.inf-=best; p.inf_spent["auction"]+=best
            p.contracts.append(contract); p.mayor=contract[0]
        else: s.cdeck.append(contract)
        s.auction_log.append(dict(contract=contract,bids=bids,winner=winner,price=best))
    # ---------------- взятки
    def can_bribe(s,kind):
        c=s.cfg; p=s.players[s.cur]
        if not c.bribes or len(s.players)<c.bribes_min or s.phase!="await" or s.bribed: return False
        cost=c.bribe_discount if kind=="discount" else c.bribe_extra
        if p.inf<cost: return False
        if kind=="extra" and s.avail()==0: return False
        return True
    def bribe(s,kind):
        if not s.can_bribe(kind): return False
        c=s.cfg; p=s.players[s.cur]; cost=c.bribe_discount if kind=="discount" else c.bribe_extra
        p.inf-=cost; p.inf_spent["bribe"]+=cost; p.bribes_given+=1; s.bribed=True
        s.bribes_active.append((p.i,kind,s.round))
        if kind=="discount": s.discount=True
        else:
            for _ in range(s.cfg.bribe_power):
                x=s.trydraw()
                if x is not None: p.hand[x]+=1
        return True
    def can_accuse(s,who):
        return s.cfg.bribes and len(s.players)>=s.cfg.bribes_min and s.phase=="await" and not s.accused and who!=s.cur and 0<=who<len(s.players)
    def accuse(s,who):
        if not s.can_accuse(who): return False
        s.accused=True; me=s.players[s.cur]; q=s.players[who]; me.accusations+=1
        exposed=sum(1 for b in s.bribes_active if b[0]==who)
        s.bribes_active=[b for b in s.bribes_active if b[0]!=who]
        if exposed:
            fine=s.cfg.bribe_fine or s.cfg.bribe_fine_by.get(len(s.players),4)
            q.pen-=fine*exposed; q.bribes_exposed+=exposed; s.earn(me,s.cfg.whistle,"whistle")
        else:
            me.pen-=s.cfg.false_fine; me.false_acc+=1
        return True
    def base_cost(s,r,p=None): return super().cost(r,p)
    def cost(s,r,p=None):
        L=super().cost(r,p)
        if s.discount and (p is None or p.i==s.cur): L=max(1,L-s.cfg.bribe_power)
        return L
    def build(s,rid,res,mach):
        r=s.m.roads.get(rid); p=s.players[s.cur]
        # Улика «дорога обошлась дешевле» должна быть известна до конца хода (его закрывает build).
        if s.cfg.secret: s.paid_less_last=r is not None and s.cost(r)<r[3]   # дешевле длины: взятка, субсидия или Мостовик
        else: s.paid_less_last=r is not None and s.discount and s.cost(r)<s.base_cost(r)
        ok=super().build(rid,res,mach)
        if not ok: s.paid_less_last=False
        return ok
    def end_turn(s,passed=False):
        p=s.players[s.cur]
        if s.hand_start is None: s.hand_start=sum(p.hand)
        s.suspicious[p.i]=sum(p.hand)-s.hand_start>=3 or getattr(s,"paid_less_last",False)
        s.paid_less_last=False
        s.bribed=False; s.accused=False; s.discount=False
        super().end_turn(passed)
        if s.phase=="over": return
        q=s.players[s.cur]
        if not s.cfg.secret: s.bribes_active=[b for b in s.bribes_active if b[0]!=q.i]
        s.hand_start=sum(q.hand)
    def setup_done(s):
        super().setup_done()
        if getattr(s,"pol_ready",False) and s.phase=="await": s.hand_start=sum(s.players[s.cur].hand)
    # ---------------- итог
    def finish(s):
        super().finish()
        for p,x in zip(s.players,s.result):
            x["pen"]=p.pen; x["mayor"]=0
            if p.mayor:
                c=next((ct for ct in p.contracts if ct[0]==p.mayor),None)  # «Отзыв» мог вернуть его в колоду
                if c and check_contract(s.m,c,p.i,s.owner,s.cfg.maxforeign,s.toll_for)[0]: x["mayor"]=s.cfg.auction_bonus
            x["total"]+=x["pen"]+x["mayor"]

class PolBot(InfBot):
    """bribe: normal / never / always; accuse: normal / never / always; bid: normal / zero / high."""
    def __init__(s, seed=0, prm=None, spend=True, bribe="normal", accuse="normal", bid="normal"):
        super().__init__(seed,prm,spend); s.bribe_mode=bribe; s.accuse_mode=accuse; s.bid_mode=bid; s.accuse_rate=0.6
    def bid(s,g,me,contract):
        if s.bid_mode=="zero" or me.inf<=0: return 0
        from ch_smart import Planner
        pl=Planner(g,me.i,s.prm); base=pl.build_plan(me.contracts); plan=pl.build_plan(me.contracts+[contract])
        if contract in plan.unreachable or plan.pieces>me.pieces-s.prm.reserve: return 0
        extra=plan.pieces-base.pieces; extra_toll=plan.tolls-base.tolls
        value=contract[3]+g.cfg.auction_bonus-s.prm.lam*extra-extra_toll
        if value<=0: return 0
        if s.bid_mode=="high": return min(me.inf, round(value/1.5))
        return max(0,min(round(value/2.0), me.inf*3//4))  # этап 21: была треть ценности и 2/3 запаса — слишком робко
    def turn(s,g):
        if g.phase=="await":
            s.try_accuse(g)
            if s.bribe_mode!="never": s.try_bribe(g)
        return super().turn(g)
    def try_accuse(s,g):
        if s.accuse_mode=="never" or not g.cfg.bribes or g.accused: return
        if g.cfg.secret:
            duel=len(g.players)==2
            if not duel and g.announced==0: return
            sus=[p.i for p in g.players if p.i!=g.cur and g.suspicious[p.i]]
            if s.accuse_mode=="evidence":   # внимательный человек: улика есть — обвиняет всегда
                if sus: g.accuse(s.rng.choice(sus))
                return
            if not sus or s.rng.random()>s.accuse_rate: return
            g.accuse(s.rng.choice(sus)); return
        if not g.bribes_active: return
        if s.accuse_mode=="whisper":
            # Внимательный человек: шёпот был в чей-то ход — значит, взятку дал он.
            who=[b[0] for b in g.bribes_active if b[0]!=g.cur]
            if who: g.accuse(who[-1])
            return
        sus=[p.i for p in g.players if p.i!=g.cur and g.suspicious[p.i]]
        if s.accuse_mode=="always" and not sus: sus=[p.i for p in g.players if p.i!=g.cur]
        if not sus or (s.accuse_mode=="normal" and s.rng.random()>0.6): return
        g.accuse(s.rng.choice(sus))
    def try_bribe(s,g):
        a=s.analyze(g); me=a["me"]; c=g.cfg
        reserve=0 if s.bribe_mode=="always" else 2
        if me.inf>=c.bribe_discount+reserve and g.can_bribe("discount"):
            for r in a["desired"]:
                if g.options(r[0]) or r[0] in g.owner or me.pieces<r[3]: continue
                if g.available(me,r): continue
                L=g.cost(r,me)
                if L>1 and s.can_pay(me,r,max(1,L-c.bribe_power)): g.bribe("discount"); return
        if me.inf>=c.bribe_extra+reserve and g.can_bribe("extra") and (s.bribe_mode=="always" or s.rng.random()<0.3):
            need=a["need"]; deficit=sum(max(0,need[x]-me.hand[x]) for x in BUILDING)
            if a["goals"] and (1<=deficit<=1+c.bribe_power or (s.bribe_mode=="always" and deficit>=1)): g.bribe("extra")
    @staticmethod
    def can_pay(me,r,L):
        mach=me.hand[MA]
        for x in ([r[4]] if r[4] is not None else BUILDING):
            if me.hand[x]>0 and L-min(me.hand[x],L)<=mach: return True
        return mach>=L

def play(m, n, seed, cfg=None, modes=None):
    g=PolGame(m,n,cfg or PolCfg(),seed)
    modes=modes or {}
    bots=[PolBot(seed*10+i, **modes.get(i,{})) for i in range(n)]
    g.bots=bots; g.responders=bots
    steps=0
    while g.phase!="over" and steps<5000:
        steps+=1
        a=g.acting()
        if not bots[a].act(g): g.pas() or g.finish()
    return g

def summary(m, n, games, seed0=1000, cfg_kw=None):
    st=collections.Counter(); wins_mayor=0
    for k in range(games):
        g=play(m,n,seed0+k,PolCfg(**(cfg_kw or {})))
        best=max(x["total"] for x in g.result)
        for log in g.auction_log:
            st["auctions"]+=1; st["price"]+=log["price"]; st["nobid"]+=log["winner"]<0
            if log["winner"]>=0:
                x=g.result[log["winner"]]; st["mayor_done"]+=x["mayor"]>0; st["mayor_won"]+=x["total"]==best
        for p,x in zip(g.players,g.result):
            st["bribes"]+=p.bribes_given; st["exposed"]+=p.bribes_exposed; st["acc"]+=p.accusations; st["false"]+=p.false_acc
            st["pen"]+=x["pen"]
    A=max(1,st["auctions"]-st["nobid"])
    print(f"n={n}, партий {games}: аукцион в {st['auctions']/games*100:.0f}% партий, без ставок {st['nobid']}, "
          f"цена {st['price']/A:.1f} влияния, контракт мэрии выполнен {st['mayor_done']/A*100:.0f}%, его владелец победил {st['mayor_won']/A*100:.0f}% (база {100/n:.0f}%)")
    print(f"  взяток за партию {st['bribes']/games:.1f}, раскрыто {st['exposed']/max(1,st['bribes'])*100:.0f}%, "
          f"обвинений {st['acc']/games:.1f} (ложных {st['false']/max(1,st['acc'])*100:.0f}%), штрафов {st['pen']/games:.1f} очк. за партию")

def strategy(m, n, games, mode, seed0=1000, cfg_kw=None):
    wins=0; diff=[]
    for k in range(games):
        seat=k%n
        g=play(m,n,seed0+k,PolCfg(**(cfg_kw or {})),{seat:mode})
        best=max(x["total"] for x in g.result)
        wins+=g.result[seat]["total"]==best
        others=[x["total"] for x in g.result if x["i"]!=seat]
        diff.append(g.result[seat]["total"]-sum(others)/len(others))
    return wins/games, sum(diff)/len(diff)

STRATEGIES=[("всегда взятка",dict(bribe="always")),("без взяток",dict(bribe="never")),
            ("обвиняет по уликам",dict(accuse="evidence")),("не обвиняет",dict(accuse="never")),
            ("не ставит на аукционе",dict(bid="zero")),("ставит много",dict(bid="high"))]

def main():
    m=load_moscow(sys.argv[1] if len(sys.argv)>1 else "../Resources/CityHighways/Maps/moscow.json")
    games=int(sys.argv[2]) if len(sys.argv)>2 else 300; n=int(sys.argv[3]) if len(sys.argv)>3 else 4
    which=sys.argv[4] if len(sys.argv)>4 else "all"
    cfg_kw=eval("dict("+sys.argv[5]+")") if len(sys.argv)>5 else {}
    if which in ("all","summary"): summary(m,n,games,cfg_kw=cfg_kw)
    for k,(label,mode) in enumerate(STRATEGIES):
        if which not in ("all",str(k)): continue
        w,d=strategy(m,n,games,mode,cfg_kw=cfg_kw)
        print(f"  n={n} {label:22s}: побед {w*100:.0f}% (база {100/n:.0f}%), разница {d:+.1f}")

if __name__=="__main__":
    main()
