# Модель «Влияния» поверх ch_model/ch_smart: как начисляется, на что тратит бот, и проверка баланса.
# Те же правила переносятся в C# (GameInfluence.cs, SmartBot).
import sys, random, collections
from ch_model import *
from ch_smart import SmartBot, Params

class InfCfg(Cfg):
    def __init__(s, **kw):
        # Начисление
        s.inf_contract=1; s.inf_complex=2; s.inf_district=1; s.inf_goal=1; s.inf_catchup=1; s.catchup_gap=6; s.grant_inf=3
        # Траты (раз в ход, не тратит ход)
        s.cost_refresh=3; s.cost_plus=1; s.cost_permit=1; s.cost_cancel=3
        super().__init__(**kw)

def complex_(c): return len(c)>4 and c[4] in ("transit","hub")

class InfGame(Game):
    def __init__(s, m, n, cfg=None, seed=0, chars=None):
        s.inf_ready=False
        super().__init__(m, n, cfg or InfCfg(), seed, chars)
        for p in s.players: p.inf=0; p.inf_earned=collections.Counter(); p.inf_spent=collections.Counter(); p.dists=set(); p.rewarded=set()
        s.inf_used=False; s.permit=False; s.inf_ready=True
    # ---------------- начисление
    def earn(s,p,n,why):
        if n<=0: return
        p.inf+=n; p.inf_earned[why]+=n
    def place(s,p,r):
        # «Новый район» — только когда сеть расширяется: первая дорога компании бонуса не даёт.
        new=({s.m.dist(r[1]),s.m.dist(r[2])}-p.dists) if p.dists else set()
        p.dists|={s.m.dist(r[1]),s.m.dist(r[2])}
        super().place(p,r)
        if new: s.earn(p,s.cfg.inf_district,"district")
        # Контракты: выполненный впервые (у любого игрока — чужая дорога могла замкнуть путь) приносит влияние.
        for q in s.players:
            for c in q.contracts:
                if c[0] in q.rewarded: continue
                if check_contract(s.m,c,q.i,s.owner,s.cfg.maxforeign,s.toll_for)[0]:
                    q.rewarded.add(c[0]); s.earn(q,s.cfg.inf_complex if complex_(c) else s.cfg.inf_contract,"contract")
    def check_goals(s,p):
        before=len(s.claimed); super().check_goals(p)
        if len(s.claimed)>before: s.earn(p,s.cfg.inf_goal*(len(s.claimed)-before),"goal")
    def apply_luck(s,p,k):
        if k=="grant" and not (p.char=="insurer" and False):
            s.earn(p,s.cfg.grant_inf,"grant"); return
        super().apply_luck(p,k)
    def luck_value(s,p,k):
        if k=="grant": return 1.0*s.cfg.grant_inf
        return super().luck_value(p,k)
    def start_round(s):
        if getattr(s,"inf_ready",False) and s.turn>1:
            vis=[p.pts+p.goal_pts+p.luck_pts for p in s.players]
            lo=min(vis)
            # Догоняющий бонус — только заметно отстающим (на catchup_gap очков и больше от лидера).
            if max(vis)-lo>=s.cfg.catchup_gap:
                for p,v in zip(s.players,vis):
                    if v==lo: s.earn(p,s.cfg.inf_catchup,"catchup")
        super().start_round()
    # ---------------- траты
    def can_spend(s,cost): return s.phase=="await" and not s.inf_used and s.players[s.cur].inf>=cost
    def spend(s,cost,why):
        p=s.players[s.cur]; p.inf-=cost; p.inf_spent[why]+=cost; s.inf_used=True
    def refresh(s):
        if not s.can_spend(s.cfg.cost_refresh): return False
        s.spend(s.cfg.cost_refresh,"refresh")
        for i,c in enumerate(s.face):
            if c is not None: s.disc.append(c); s.face[i]=None
        s.refill(); return True
    def draw_contracts_plus(s):
        if not s.can_spend(s.cfg.cost_plus) or not s.cdeck: return False
        s.spend(s.cfg.cost_plus,"plus")
        p=s.players[s.cur]; k=s.cfg.draw+1; p.pending=s.cdeck[:k]; s.cdeck=s.cdeck[k:]; s.phase="choose"; return True
    def buy_permit(s):
        if not s.can_spend(s.cfg.cost_permit) or not s.event or s.event["kind"] not in("parade","snow"): return False
        s.spend(s.cfg.cost_permit,"permit"); s.permit=True; return True
    def cancel_event(s):
        if not s.can_spend(s.cfg.cost_cancel) or not s.event or s.event["kind"]=="district": return False
        s.spend(s.cfg.cost_cancel,"cancel"); s.event=None; s.repair=set(); return True
    def available(s,p,r):
        why=super().available(p,r)
        if why=="parade" and s.permit and p.i==s.cur:
            return "pieces" if p.pieces<r[3] else None
        return why
    def cost(s,r,p=None):
        L=super().cost(r,p)
        if s.permit and r[4]==GR and s.event and s.event["kind"]=="snow" and (p is None or p.i==s.cur): L-=s.cfg.snow
        return L
    def end_turn(s,passed=False):
        s.inf_used=False; s.permit=False
        super().end_turn(passed)

class InfBot(SmartBot):
    def __init__(s, seed=0, prm=None, spend=True): super().__init__(seed,prm); s.spend=spend
    def turn(s,g):
        if s.spend and g.phase=="await":
            a=s.analyze(g); me=a["me"]
            # Разрешение: нужная дорога закрыта парадом или подорожала из-за снега, а с разрешением её можно построить.
            if g.event and g.event["kind"] in("parade","snow") and g.can_spend(g.cfg.cost_permit):
                blocked=[r for r in a["desired"] if not g.options(r[0])]
                if blocked:
                    g.permit=True
                    ok=any(g.options(r[0]) for r in blocked)
                    g.permit=False
                    if ok: g.buy_permit()
            # Обновить рынок: ресурсы нужны, а в открытом ряду нет ни одной полезной карты.
            elif g.can_spend(g.cfg.cost_refresh) and me.inf>=g.cfg.cost_refresh+1:
                need=a["need"]; want=[x for x in BUILDING if need[x]>me.hand[x]]
                if want and not any(c in want for c in g.face if c is not None) and not [r for r in a["desired"] if g.options(r[0])]:
                    g.refresh()
        return super().turn(g)
    def act(s,g):
        r=super().act(g)
        return r
    # Брать контракты с запасом выбора, если влияния хватает.
    def maybe_plus(s,g):
        return s.spend and g.can_spend(g.cfg.cost_plus) and g.players[g.cur].inf>=2

# Подмена draw_contracts для бота: при возможности — с лоббированием (+1 контракт на выбор).
_orig_draw=InfGame.draw_contracts
def _draw(s):
    bot=s.bots[s.cur] if getattr(s,"bots",None) else None
    if bot is not None and isinstance(bot,InfBot) and bot.maybe_plus(s): return s.draw_contracts_plus()
    return _orig_draw(s)
InfGame.draw_contracts=_draw

def play(m, n, seed, spenders, cfg=None):
    g=InfGame(m,n,cfg or InfCfg(),seed)
    bots=[InfBot(seed*10+i, spend=(i in spenders)) for i in range(n)]
    g.bots=bots; g.responders=bots
    steps=0
    while g.phase!="over" and steps<5000:
        steps+=1
        a=g.acting()
        if not bots[a].act(g): g.pas() or g.finish()
    return g

def main(games=300, n=4):
    m=load_moscow(sys.argv[1] if len(sys.argv)>1 else "../Resources/CityHighways/Maps/moscow.json")
    earned=collections.Counter(); spent=collections.Counter(); left=[]; total_inf=[]
    wins_spender=0; wins_base=0; score_sp=[]; score_no=[]; uses=collections.Counter()
    for k in range(games):
        sp_seat=k%n
        g=play(m,n,1000+k,{sp_seat})
        best=max(x["total"] for x in g.result)
        for p,x in zip(g.players,g.result):
            if p.i==sp_seat: score_sp.append(x["total"]); wins_spender+= x["total"]==best
            else: score_no.append(x["total"])
            earned.update(p.inf_earned); spent.update(p.inf_spent); left.append(p.inf); total_inf.append(sum(p.inf_earned.values()))
    P=games*n
    print(f"Партий: {games}, игроков: {n}")
    print("Влияние за партию на игрока: заработано %.1f (контракты %.1f, районы %.1f, цели %.1f, грант %.1f, догоняющий %.1f), остаток %.1f"%(
        sum(total_inf)/P, earned["contract"]/P, earned["district"]/P, earned["goal"]/P, earned["grant"]/P, earned["catchup"]/P, sum(left)/P))
    print("Траты (у тратящего бота за партию): " + ", ".join(f"{k} {v/games:.2f}" for k,v in spent.items()))
    print(f"Победы тратящего бота: {wins_spender/games*100:.0f}% (ровно {100/n:.0f}% — без преимущества); средний счёт {sum(score_sp)/len(score_sp):.1f} против {sum(score_no)/len(score_no):.1f}")

if __name__=="__main__":
    main(int(sys.argv[2]) if len(sys.argv)>2 else 200, int(sys.argv[3]) if len(sys.argv)>3 else 4)
