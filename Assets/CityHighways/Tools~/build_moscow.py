import json, heapq, itertools, collections, sys
OUT = sys.argv[1] if len(sys.argv) > 1 else "../Resources/CityHighways/Maps/moscow.json"

districts = [
 ("center","Центр"),("zamoskvorechye","Замоскворечье"),("vokzaly","Вокзалы"),
 ("north","Север"),("northeast","Северо-Восток"),("northwest","Северо-Запад"),("city","Москва-Сити"),("west","Запад"),
 ("southwest","Юго-Запад"),("south","Юг"),("east","Восток"),("southeast","Юго-Восток"),("newmoscow","Новая Москва"),("airports","Аэропорты"),
 ("zelenograd","Зеленоград"),("nekrasovka","Некрасовка"),("troitsk","Троицк"),
]
# id, name, district, x, y   (x: запад→восток, y: север→юг, 0..1)
locations = [
 ("kremlin","Кремль","center",0.50,0.48),("lubyanka","Лубянка","center",0.54,0.44),
 ("arbat","Арбат","center",0.45,0.48),("pushkinskaya","Пушкинская","center",0.48,0.42),
 ("tretyakovka","Третьяковка","zamoskvorechye",0.52,0.54),("oktyabrskaya","Октябрьская","zamoskvorechye",0.48,0.58),
 ("paveletsky","Павелецкий","zamoskvorechye",0.56,0.58),
 ("komsomolskaya","Комсомольская","vokzaly",0.57,0.37),("kursky","Курский","vokzaly",0.60,0.45),
 ("savyolovsky","Савёловский","north",0.47,0.32),("ostankino","Останкино","north",0.52,0.24),("vdnh","ВДНХ","north",0.57,0.22),
 ("sokol","Сокол","northwest",0.40,0.30),("rechnoy","Речной вокзал","northwest",0.37,0.18),("shchukinskaya","Щукинская","northwest",0.31,0.33),
 ("city","Сити","city",0.37,0.45),("presnya","Пресня","city",0.41,0.42),
 ("kievsky","Киевский","west",0.38,0.53),("kutuzovsky","Кутузовский","west",0.31,0.50),
 ("luzhniki","Лужники","southwest",0.42,0.60),("mgu","МГУ","southwest",0.40,0.70),
 ("vernadskogo","Вернадского","southwest",0.32,0.70),("teply_stan","Тёплый Стан","southwest",0.32,0.83),
 ("tulskaya","Тульская","south",0.52,0.64),("nagatino","Нагатино","south",0.56,0.70),
 ("kolomenskoe","Коломенское","south",0.62,0.735),("chertanovo","Чертаново","south",0.48,0.82),
 ("baumanskaya","Бауманская","east",0.64,0.41),("sokolniki","Сокольники","east",0.63,0.32),
 ("izmaylovo","Измайлово","east",0.75,0.38),("perovo","Перово","east",0.77,0.48),
 ("taganka","Таганка","southeast",0.60,0.52),("volgogradsky","Волгоградский","southeast",0.67,0.58),
 ("kuzminki","Кузьминки","southeast",0.74,0.62),("maryino","Марьино","southeast",0.69,0.76),
 ("timiryazevskaya","Тимирязевская","north",0.44,0.22),
 ("otradnoe","Отрадное","northeast",0.52,0.13),("medvedkovo","Медведково","northeast",0.63,0.13),("losiny","Лосиный остров","northeast",0.71,0.22),
 ("rizhskaya","Рижский","vokzaly",0.54,0.31),("belorussky","Белорусский","vokzaly",0.44,0.37),
 ("tushino","Тушино","northwest",0.26,0.24),("strogino","Строгино","northwest",0.19,0.36),
 ("fili","Фили","west",0.28,0.42),("krylatskoe","Крылатское","west",0.18,0.49),
 ("profsoyuznaya","Профсоюзная","southwest",0.45,0.76),("yasenevo","Ясенево","southwest",0.41,0.88),("troparevo","Тропарёво","southwest",0.24,0.77),
 ("solntsevo","Солнцево","newmoscow",0.15,0.70),("kommunarka","Коммунарка","newmoscow",0.29,0.94),
 ("tsaritsyno","Царицыно","south",0.57,0.83),("biryulevo","Бирюлёво","south",0.51,0.91),
 ("lyublino","Люблино","southeast",0.66,0.66),("kapotnya","Капотня","southeast",0.81,0.69),
 ("lefortovo","Лефортово","east",0.69,0.48),("novogireevo","Новогиреево","east",0.85,0.48),("golyanovo","Гольяново","east",0.86,0.35),
 ("zelenograd","Зеленоград","zelenograd",0.08,0.17),("kryukovo","Крюково","zelenograd",0.04,0.29),
 ("kosino","Косино","nekrasovka",0.93,0.55),("nekrasovka","Некрасовка","nekrasovka",0.95,0.65),
 ("troitsk","Троицк","troitsk",0.06,0.97),("vatutinki","Ватутинки","troitsk",0.20,0.975),
 ("sheremetyevo","Шереметьево","airports",0.34,0.05),("vnukovo","Внуково","airports",0.14,0.88),
 ("domodedovo","Домодедово","airports",0.63,0.96),
]
A,C,S,G,ANY="asphalt","concrete","steel","gravel","any"
roads = [
 # центр
 ("kremlin","lubyanka",1,A),("kremlin","arbat",1,A),("kremlin","pushkinskaya",1,C),
 ("lubyanka","pushkinskaya",1,ANY),("arbat","pushkinskaya",1,ANY),
 # мосты из центра
 ("kremlin","tretyakovka",1,S),("arbat","kievsky",2,S),
 # Замоскворечье
 ("tretyakovka","oktyabrskaya",1,A),("tretyakovka","paveletsky",1,A),("oktyabrskaya","paveletsky",2,C),
 # вокзалы
 ("lubyanka","komsomolskaya",2,C),("lubyanka","kursky",2,A),("komsomolskaya","kursky",2,C),
 ("kursky","taganka",2,C),("taganka","paveletsky",2,S),("kremlin","taganka",2,A),
 # север
 ("pushkinskaya","savyolovsky",3,C),("savyolovsky","ostankino",2,A),("ostankino","vdnh",1,ANY),
 ("komsomolskaya","sokolniki",2,A),("sokolniki","vdnh",3,G),("komsomolskaya","vdnh",3,C),
 ("savyolovsky","komsomolskaya",2,ANY),
 # северо-запад
 ("sokol","rechnoy",3,C),("rechnoy","sheremetyevo",4,A),
 ("sokol","shchukinskaya",2,A),("savyolovsky","sokol",2,ANY),("vdnh","sheremetyevo",5,G),
 # Сити
 ("city","presnya",1,ANY),("presnya","pushkinskaya",2,A),("presnya","arbat",2,C),
 ("shchukinskaya","city",3,A),("city","kutuzovsky",2,S),
 # запад
 ("kievsky","kutuzovsky",2,C),("kievsky","luzhniki",2,S),
 # юго-запад
 ("arbat","luzhniki",3,A),("luzhniki","mgu",2,S),("luzhniki","oktyabrskaya",3,S),
 ("oktyabrskaya","mgu",3,C),("mgu","vernadskogo",2,C),("vernadskogo","teply_stan",3,C),
 ("teply_stan","vnukovo",4,A),
 # юг
 ("paveletsky","tulskaya",2,A),("oktyabrskaya","tulskaya",2,ANY),("tulskaya","nagatino",2,A),
 ("tulskaya","chertanovo",4,C),("nagatino","kolomenskoe",1,ANY),("kolomenskoe","maryino",3,S),
 ("chertanovo","teply_stan",3,G),
 # восток
 ("kursky","baumanskaya",2,A),("baumanskaya","sokolniki",2,ANY),("baumanskaya","izmaylovo",3,C),
 ("izmaylovo","perovo",2,G),("sokolniki","izmaylovo",3,G),
 # юго-восток
 ("taganka","volgogradsky",3,C),("volgogradsky","kuzminki",2,C),("kuzminki","maryino",3,G),
 ("perovo","kuzminki",3,A),
]
roads += [
 # север и северо-восток
 ("timiryazevskaya","savyolovsky",2,C),("timiryazevskaya","sokol",2,A),("timiryazevskaya","ostankino",2,ANY),
 ("otradnoe","timiryazevskaya",2,A),("otradnoe","medvedkovo",2,A),("otradnoe","ostankino",2,C),
 ("medvedkovo","vdnh",3,C),("medvedkovo","losiny",2,G),("losiny","sokolniki",3,G),("losiny","golyanovo",3,G),
 # вокзалы
 ("rizhskaya","savyolovsky",2,A),("rizhskaya","komsomolskaya",1,C),("rizhskaya","ostankino",2,C),("rizhskaya","sokolniki",2,A),
 ("belorussky","pushkinskaya",1,C),("belorussky","presnya",1,ANY),("belorussky","savyolovsky",2,A),("belorussky","sokol",3,C),
 # северо-запад и запад
 ("tushino","shchukinskaya",2,A),("tushino","rechnoy",3,A),("tushino","strogino",3,G),
 ("strogino","shchukinskaya",2,C),("strogino","krylatskoe",2,S),
 ("krylatskoe","kutuzovsky",3,C),("fili","kutuzovsky",2,S),("fili","city",2,A),("fili","shchukinskaya",3,G),
 # юго-запад и Новая Москва
 ("mgu","profsoyuznaya",2,A),("profsoyuznaya","vernadskogo",2,ANY),("profsoyuznaya","yasenevo",3,C),
 ("yasenevo","teply_stan",2,A),("yasenevo","chertanovo",3,G),
 ("troparevo","vernadskogo",2,A),("troparevo","solntsevo",2,G),("troparevo","teply_stan",2,C),
 ("kutuzovsky","solntsevo",4,C),("krylatskoe","solntsevo",4,G),("solntsevo","vnukovo",2,A),
 ("teply_stan","kommunarka",3,A),("yasenevo","kommunarka",3,G),("kommunarka","vnukovo",4,G),
 # юг
 ("chertanovo","tsaritsyno",2,A),("nagatino","tsaritsyno",3,C),("tsaritsyno","biryulevo",2,G),
 ("biryulevo","chertanovo",2,ANY),("tsaritsyno","domodedovo",4,A),("biryulevo","domodedovo",3,G),
 # юго-восток и восток
 ("volgogradsky","lyublino",2,A),("lyublino","kuzminki",2,ANY),("lyublino","maryino",3,A),("nagatino","lyublino",2,S),
 ("kuzminki","kapotnya",2,G),("kapotnya","maryino",3,G),
 ("kursky","lefortovo",2,A),("baumanskaya","lefortovo",2,ANY),("lefortovo","volgogradsky",2,C),("lefortovo","perovo",3,A),
 ("perovo","novogireevo",2,A),("novogireevo","kuzminki",3,G),("novogireevo","golyanovo",3,ANY),("izmaylovo","golyanovo",2,C),
]
# Районы-расширения: закрыты в начале партии, открываются событием «Новый район».
EXPANSIONS = {"zelenograd", "nekrasovka", "troitsk"}
roads += [
 ("zelenograd","kryukovo",1,ANY),("zelenograd","sheremetyevo",4,A),("zelenograd","tushino",4,G),("kryukovo","strogino",4,G),
 ("kosino","nekrasovka",2,ANY),("kosino","novogireevo",2,A),("nekrasovka","kapotnya",3,G),("nekrasovka","kuzminki",4,A),
 ("troitsk","vatutinki",2,ANY),("vatutinki","kommunarka",3,A),("troitsk","vnukovo",4,G),("vatutinki","solntsevo",4,G),
]
river = [(0.06,0.42),(0.18,0.43),(0.25,0.45),(0.30,0.465),(0.34,0.475),(0.37,0.49),(0.41,0.53),(0.405,0.58),
         (0.415,0.625),(0.445,0.63),(0.465,0.59),(0.475,0.55),(0.49,0.515),(0.52,0.51),(0.555,0.52),(0.585,0.555),
         (0.60,0.60),(0.61,0.66),(0.635,0.70),(0.645,0.75),(0.67,0.82),(0.76,0.83),(0.86,0.80),(0.97,0.83)]

loc = {l[0]:l for l in locations}
# Номера дорог закрепляются до удаления — у оставшихся дорог они не меняются (сохранения и история команд
# ссылаются на номера). Удалённые дороги просто пропускаются.
ROAD_ID={(a,b):f"r{k:02d}" for k,(a,b,n,r) in enumerate(roads,1)}
REMOVED={("profsoyuznaya","vernadskogo"),("sokolniki","izmaylovo"),("rizhskaya","komsomolskaya")}
assert REMOVED <= set(ROAD_ID), "удаляемой дороги нет в списке"
roads=[x for x in roads if (x[0],x[1]) not in REMOVED]
for a,b,n,r in roads: assert a in loc and b in loc, (a,b)
pairs=[tuple(sorted((a,b))) for a,b,_,_ in roads]; assert len(pairs)==len(set(pairs)), "dup road"
adj=collections.defaultdict(list)
for a,b,n,r in roads: adj[a].append((b,n)); adj[b].append((a,n))
# connectivity
seen={locations[0][0]}; st=[locations[0][0]]
while st:
    u=st.pop()
    for v,_ in adj[u]:
        if v not in seen: seen.add(v); st.append(v)
assert len(seen)==len(locations), set(loc)-seen
def dist_from(srcs, skip=frozenset()):
    """Кратчайшие расстояния; места из skip (закрытые районы) не проходятся."""
    d={s:0 for s in srcs}; pq=[(0,s) for s in srcs]
    while pq:
        du,u=heapq.heappop(pq)
        if du>d[u]: continue
        for v,w in adj[u]:
            if v in skip: continue
            if du+w<d.get(v,1e9): d[v]=du+w; heapq.heappush(pq,(du+w,v))
    return d
members=collections.defaultdict(list)
for l in locations: members[l[2]].append(l[0])
# Очки обычных контрактов — по карте без районов-расширений (в начале партии их нет).
_closed=frozenset(l[0] for l in locations if l[2] in EXPANSIONS)
D={}
for a in members:
    d=dist_from(members[a], frozenset() if a in EXPANSIONS else _closed)
    for b in members:
        if a!=b: D[(a,b)]=min(d.get(x,1e9) for x in members[b])
# contracts: choose a spread of pairs, points = distance + 1 (+2 if far, airports harder)
import random as _r
_pairs=[(a,b) for a in members for b in members if a<b and D[(a,b)]>=4 and a not in EXPANSIONS and b not in EXPANSIONS]
_r.Random(7).shuffle(_pairs)
# Берём пары равномерно по длине: короткие, средние и длинные.
_pairs.sort(key=lambda p: D[p])
_buckets=[_pairs[i::3] for i in range(3)]
chosen=[]
for p in _pairs:
    pass
_short=[p for p in _pairs if D[p]<=6]; _mid=[p for p in _pairs if 7<=D[p]<=10]; _long=[p for p in _pairs if D[p]>=11]
for lst,k in ((_short,12),(_mid,16),(_long,12)):
    _r.Random(len(lst)).shuffle(lst); chosen+=lst[:k]
assert len(set(tuple(sorted(p)) for p in chosen))==len(chosen)
chosen += [("zelenograd","center"),("zelenograd","vokzaly"),("zelenograd","west"),
           ("nekrasovka","center"),("nekrasovka","city"),("nekrasovka","south"),
           ("troitsk","center"),("troitsk","north"),("troitsk","southwest")]
contracts=[]
for i,(a,b) in enumerate(chosen,1):
    d=D[(a,b)]
    pts=max(4, d + 1 + (2 if d>=9 else 0) + (3 if d>=13 else 0))
    contracts.append({"id":f"c{i:02d}","from":a,"to":b,"points":pts})
# Сложные контракты. «Транзит» — один путь из A в B, проходящий через район C.
# «Узел» — район A связать и с B, и с C (два плеча, каждое проверяется как обычный контракт).
# Длина: для транзита — кратчайший путь A→C→B, для узла — сумма плеч A–B и A–C.
# Очки = обычная формула от длины + надбавка за сложность (транзит +2, узел +1),
# выходит ~1,4 очка за участок против ~1,25 у обычных контрактов.
TRANSIT=[("northwest","southeast","center"),("west","east","center"),("north","south","zamoskvorechye"),
         ("city","east","vokzaly"),("northeast","southwest","center"),("northwest","newmoscow","west"),
         ("airports","city","west"),("southeast","north","east")]
HUB=[("vokzaly","airports","northeast"),("city","southwest","north"),("center","airports","newmoscow"),
     ("south","west","southeast"),("east","northeast","zamoskvorechye"),("west","northwest","south")]
def _pts(d): return max(4, d + 1 + (2 if d>=9 else 0) + (3 if d>=13 else 0))
for kind,lst in (("transit",TRANSIT),("hub",HUB)):
    for a,b,c in lst:
        if kind=="transit":
            da,db=dist_from(members[a],_closed),dist_from(members[b],_closed)
            d=min(da.get(x,1e9)+db.get(x,1e9) for x in members[c])
        else: d=D[(a,b)]+D[(a,c)]
        assert d>=6, f"сложный контракт {a}-{b}-{c} слишком короткий: {d}"
        contracts.append({"id":f"c{len(contracts)+1:02d}","from":a,"to":b,"points":_pts(d)+(2 if kind=="transit" else 1),"kind":kind,"third":c})
# Общие цели мэрии. В партии открываются 3 случайные; награду получает первый, кто выполнит цель.
# kind: locations — своя дорога в count местах района; bridges — построить count мостов;
# network — связная сеть на count районов; resource_pieces — count участков дорог ресурса resource;
# long_roads — count дорог длиной не меньше length.
GOALS=[
 {"id":"g1","name":"Воздушные ворота","kind":"locations","district":"airports","count":2,"points":8},
 {"id":"g2","name":"Мостостроитель","kind":"bridges","count":3,"points":7},
 {"id":"g3","name":"Городская сеть","kind":"network","count":8,"points":8},
 {"id":"g4","name":"Загородные трассы","kind":"resource_pieces","resource":"gravel","count":10,"points":6},
 {"id":"g5","name":"Дальнобой","kind":"long_roads","length":4,"count":3,"points":7},
 {"id":"g6","name":"Сердце Москвы","kind":"locations","district":"center","count":4,"points":9},
 {"id":"g7","name":"Вокзальная площадь","kind":"locations","district":"vokzaly","count":3,"points":6},
]
# ---------------------------------------------------------------- «Лупа» для центра
# Центр Москвы плотный, поэтому, как на схемах метро, растягиваем его: смещение от Кремля
# умножается на 1 + K·exp(−(r/S)²), где r — расстояние в пикселях поля 1400×780 (экран 1080p).
# Порядок мест сохраняется, окраины почти не двигаются. Реку растягиваем так же.
import math
SPREAD_K, SPREAD_S, FIELD_W, FIELD_H = 0.6, 320.0, 1400.0, 780.0
_cx = next(l for l in locations if l[0]=="kremlin")[3]*FIELD_W
_cy = next(l for l in locations if l[0]=="kremlin")[4]*FIELD_H
def spread(x, y):
    px, py = x*FIELD_W-_cx, y*FIELD_H-_cy
    f = 1 + SPREAD_K*math.exp(-(math.hypot(px, py)/SPREAD_S)**2)
    nx, ny = (_cx+px*f)/FIELD_W, (_cy+py*f)/FIELD_H
    return round(min(max(nx,0.015),0.985),3), round(min(max(ny,0.015),0.985),3)
locations = [(i,n,dd)+spread(x,y) for i,n,dd,x,y in locations]
river = [spread(x,y) for x,y in river]

# Проверки отрисовки: мосты пересекают реку, остальные дороги — нет; места не теснятся; подписи не наезжают.
_pos = {l[0]:(l[3],l[4]) for l in locations}
def _cr(a,b,c): return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
def _inter(p1,p2,p3,p4): return _cr(p3,p4,p1)*_cr(p3,p4,p2)<0 and _cr(p1,p2,p3)*_cr(p1,p2,p4)<0
for a_,b_,n_,r_ in roads:
    crosses = any(_inter(_pos[a_],_pos[b_],river[i],river[i+1]) for i in range(len(river)-1))
    assert crosses == (r_==S), f"дорога {a_}-{b_}: {'пересекает реку, но не мост' if crosses else 'мост не пересекает реку'}"
_px = {i:(x*FIELD_W,y*FIELD_H) for i,(x,y) in _pos.items()}
_min = min(math.dist(_px[a_],_px[b_]) for a_,b_ in itertools.combinations(_px,2))
assert _min >= 60, f"места слишком близко: {_min:.0f}px"
def _box(l):
    x,y=_px[l[0]]; w=len(l[1])*7.2+10; return (x-w/2,y+13,x+w/2,y+31)
for l1,l2 in itertools.combinations(locations,2):
    A_,B_=_box(l1),_box(l2)
    assert not (A_[0]<B_[2] and B_[0]<A_[2] and A_[1]<B_[3] and B_[1]<A_[3]), f"подписи наезжают: {l1[1]} и {l2[1]}"
print(f"проверки отрисовки пройдены, минимальное расстояние между местами {_min:.0f}px")

data={"name":"Москва","districts":[dict({"id":i,"name":n}, **({"expansion":True} if i in EXPANSIONS else {})) for i,n in districts],
 "locations":[{"id":i,"name":n,"district":dd,"x":x,"y":y} for i,n,dd,x,y in locations],
 "roads":[{"id":ROAD_ID[(a,b)],"from":a,"to":b,"length":n,"resource":r} for (a,b,n,r) in roads],
 "contracts":contracts,
 "river":[{"x":x,"y":y} for x,y in river],
 "goals":GOALS}
json.dump(data,open(OUT,"w"),ensure_ascii=False,indent=1)
tot=collections.Counter(); cnt=collections.Counter()
for a,b,n,r in roads: tot[r]+=n; cnt[r]+=1
print("locations",len(locations),"roads",len(roads),"total length",sum(tot.values()))
print("length by resource",dict(tot)); print("roads by resource",dict(cnt))
print("length histogram",collections.Counter(n for _,_,n,_ in roads))
for c in contracts: print(c["id"],c.get("kind","link"),c["from"],c["to"],c.get("third",""),"direct",D[(c["from"],c["to"])],"pts",c["points"])
