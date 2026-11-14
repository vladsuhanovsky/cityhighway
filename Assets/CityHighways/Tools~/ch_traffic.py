# Модель «оплаты при выполнении» и пробок поверх ch_influence: маршрут контракта фиксируется в момент выполнения,
# проезд платится сразу; дорога (не магистраль), по которой уже едут jam чужих контрактов, — в пробке:
# каждый следующий проезд платит ещё jam_fee очков городу. Те же правила — в C# (GameTraffic.cs).
# Запуск: python3 ch_traffic.py [карта] [партий] [игроков]
import sys, collections
from ch_model import *
from ch_influence import InfGame, InfCfg, InfBot

class TrafficGame(InfGame):
    def __init__(s, m, n, cfg=None, seed=0, chars=None, jam=3, jam_fee=1, fixed=True):
        s.jam=jam; s.jam_fee=jam_fee; s.fixed=fixed
        s.traffic=collections.Counter(); s.paid={}; s.jam_paid=collections.Counter(); s.peak=collections.Counter()
        super().__init__(m, n, cfg, seed, chars)
    def jammed(s, rid): return s.jam>0 and rid not in s.double and s.traffic[rid]>=s.jam
    def route_cost(s, rid): return s.toll_for(rid)+(s.jam_fee if s.jammed(rid) else 0)
    def check(s, c, p): return check_contract(s.m,c,p,s.owner,s.cfg.maxforeign,s.route_cost if s.fixed else s.toll_for)
    def settle(s):
        if not s.fixed: return
        for q in s.players:
            for c in q.contracts:
                if c[0] in s.paid: continue
                ok,f=s.check(c,q.i)
                if not ok: continue
                rec=[]
                for r in f:
                    rid=r[0]; jam=s.jammed(rid)
                    rec.append((rid,s.owner[rid],s.toll_for(rid),s.jam_fee if jam else 0))
                    if jam: s.jam_paid[q.i]+=s.jam_fee
                for rid,_,_,_ in rec:
                    s.traffic[rid]+=1; s.peak[rid]=max(s.peak[rid],s.traffic[rid])
                s.paid[c[0]]=rec
    def place(s, p, r):
        super().place(p, r); s.settle()
    def finish(s):
        if not s.fixed: return super().finish()
        s.settle()
        sc=[dict(i=p.i,road=p.pts,cp=0,ok=0,fail=0,paid=0,earned=0,jam=0,net=network(s.m,p.roads),bonus=0,char=0) for p in s.players]
        for p in s.players:
            for c in p.contracts:
                rec=s.paid.get(c[0])
                if rec is None: sc[p.i]["cp"]-=c[3]; sc[p.i]["fail"]+=1; continue
                sc[p.i]["cp"]+=c[3]; sc[p.i]["ok"]+=1
                if p.char=="official": sc[p.i]["char"]+=s.cfg.official_bonus
                for rid,o,t,j in rec: sc[p.i]["paid"]+=t; sc[o]["earned"]+=t; sc[p.i]["jam"]+=j
        best=max(x["net"] for x in sc)
        for x in sc:
            if best>0 and x["net"]==best: x["bonus"]=s.cfg.bonus
        for p,x in zip(s.players,sc): x["goals"]=p.goal_pts; x["luck"]=p.luck_pts
        for p,x in zip(s.players,sc):
            if p.char=="bridger": x["char"]+=s.cfg.bridger_points*sum(1 for r in p.roads if r[4]==ST)
        for x in sc: x["total"]=x["road"]+x["cp"]-x["paid"]-x["jam"]+x["earned"]+x["bonus"]+x["goals"]+x["luck"]+x["char"]
        s.result=sc; s.phase="over"

def play(m, n, seed, **kw):
    g=TrafficGame(m,n,InfCfg(),seed,**kw)
    bots=[InfBot(seed*10+i, spend=False) for i in range(n)]
    g.bots=bots; g.responders=bots
    steps=0
    while g.phase!="over" and steps<5000:
        steps+=1
        a=g.acting()
        if not bots[a].act(g): g.pas() or g.finish()
    return g

def main(games=200, n=4):
    m=load_moscow(sys.argv[1] if len(sys.argv)>1 else "../Resources/CityHighways/Maps/moscow.json")
    for label,kw in [("проезд в конце (было)",dict(fixed=False,jam=0)),("оплата при выполнении",dict(jam=0)),
                     ("+ пробка с 2, сбор 2",dict(jam=2,jam_fee=2)),("+ пробка с 3, сбор 1",dict(jam=3,jam_fee=1))]:
        foreign=0; tolls=0; jams=0; jam_fee=0; peak=collections.Counter(); totals=[]
        for k in range(games):
            g=play(m,n,1000+k,**kw)
            for x in g.result: tolls+=x["paid"]; jam_fee+=x.get("jam",0); totals.append(x["total"])
            if kw.get("fixed",True):
                for v in g.peak.values(): peak[min(v,4)]+=1
                foreign+=sum(len(r) for r in g.paid.values())
                jams+=sum(1 for rid,v in g.traffic.items() if g.jammed(rid))
        P=games
        line=f"{label:24s} проезд {tolls/P:5.1f} за партию, пробочных сборов {jam_fee/P:4.1f}, дорог в пробке к концу {jams/P:4.2f}, средний счёт {sum(totals)/len(totals):5.1f}"
        if peak: line+="; дорог с чужим трафиком 1/2/3/4+: "+"/".join(f"{peak[i]/P:.2f}" for i in (1,2,3,4))
        print(line)

if __name__=="__main__":
    main(int(sys.argv[2]) if len(sys.argv)>2 else 200, int(sys.argv[3]) if len(sys.argv)>3 else 4)
