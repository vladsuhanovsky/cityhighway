# Карта обучения: центр Москвы (Центр, Замоскворечье, Вокзалы, Москва-Сити, Запад) из moscow.json,
# растянутая на всё поле, со своими простыми контрактами. Запуск из Tools~: python3 make_tutorial_map.py
import json, os
HERE=os.path.dirname(os.path.abspath(__file__))
SRC=os.path.join(HERE,"../Resources/CityHighways/Maps/moscow.json")
DST=os.path.join(HERE,"../Resources/CityHighways/Maps/tutorial.json")
d=json.load(open(SRC,encoding="utf-8"))
KEEP=["center","zamoskvorechye","vokzaly","city","west"]
loc={l["id"]:l for l in d["locations"]}
# Рижская связана только с Севером — без него она была бы островом.
DROP={"rizhskaya"}
locs=[dict(l) for l in d["locations"] if l["district"] in KEEP and l["id"] not in DROP]
xs=[l["x"] for l in locs]; ys=[l["y"] for l in locs]
x0,x1,y0,y1=min(xs),max(xs),min(ys),max(ys)
def tx(x): return round(0.06+0.88*(x-x0)/(x1-x0),3)
def ty(y): return round(0.06+0.88*(y-y0)/(y1-y0),3)
for l in locs: l["x"]=tx(l["x"]); l["y"]=ty(l["y"])
ids={l["id"] for l in locs}
roads=[r for r in d["roads"] if r["from"] in ids and r["to"] in ids]
river=[{"x":tx(p["x"]),"y":ty(p["y"])} for p in d["river"]]
river=[p for p in river if -0.25<=p["x"]<=1.25 and -0.25<=p["y"]<=1.25]
# Простые контракты «связать два района»: очки — по тому, насколько районы далеко друг от друга.
pairs=[("center","zamoskvorechye",3),("center","vokzaly",3),("city","center",3),("center","west",4),
       ("city","west",3),("vokzaly","zamoskvorechye",5),("city","zamoskvorechye",5),("city","vokzaly",6),
       ("west","zamoskvorechye",6),("vokzaly","west",7)]
contracts=[{"id":"t%02d"%(i+1),"from":a,"to":b,"points":p} for i,(a,b,p) in enumerate(pairs)]
out={"name":"Центр Москвы (обучение)",
     "districts":[x for x in d["districts"] if x["id"] in KEEP],
     "locations":locs,"roads":roads,"contracts":contracts,"river":river,"goals":[]}
json.dump(out,open(DST,"w",encoding="utf-8"),ensure_ascii=False,indent=1)
print(len(locs),"мест,",len(roads),"дорог,",len(contracts),"контрактов,",len(river),"точек реки")
