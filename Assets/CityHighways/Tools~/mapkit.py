"""
Общий сборщик карт City Highways (Амстердам, Чунцин; Москва собирается своим build_moscow.py).

Карта описывается словарём spec:
  name, districts [(id, name)], expansions {id}, locations [(id, name, district, x, y)],
  roads [(a, b, resource|None, length|None)], rivers [[(x, y), ...], ...],
  center (id места — центр «лупы»), spread_k, spread_s,
  transit [(a, b, c)], hub [(a, b, c)], goals [...], contract_counts (short, mid, long),
  expansion_contracts [(a, b)].

Что делает:
  * «лупа» — растягивает плотный центр (как в build_moscow.py);
  * длина дороги по умолчанию — по расстоянию на поле 1400×780;
  * мост = сталь: дорога, пересекающая любую реку, обязана быть стальной, остальные — нет.
    Ресурс None — подобрать автоматически (мост — сталь, окраина — щебень, иначе асфальт/бетон, изредка любой);
  * проверки: связность, нет дублей, места не теснятся, подписи не наезжают, дороги не пересекаются друг с другом;
  * контракты по кратчайшим путям (как в Москве), сложные — транзит и узел;
  * JSON в формате CityMapDto: первая река — в "river" (для старых версий), все — в "rivers".
"""
import json, heapq, itertools, collections, math, random

FIELD_W, FIELD_H = 1400.0, 780.0
A, C, S, G, ANY = "asphalt", "concrete", "steel", "gravel", "any"


def auto_length(px):
    if px < 95: return 1
    if px < 150: return 2
    if px < 210: return 3
    if px < 300: return 4
    return 5


def _cr(a, b, c): return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])


def _inter(p1, p2, p3, p4):
    return _cr(p3, p4, p1) * _cr(p3, p4, p2) < 0 and _cr(p1, p2, p3) * _cr(p1, p2, p4) < 0


def _seg_dist(p, a, b):
    ax, ay = a; bx, by = b; px, py = p
    dx, dy = bx - ax, by - ay
    t = max(0.0, min(1.0, ((px - ax) * dx + (py - ay) * dy) / max(1e-9, dx * dx + dy * dy)))
    return math.hypot(px - (ax + t * dx), py - (ay + t * dy))


def build(spec, out_path, verbose=True):
    districts = spec["districts"]
    expansions = set(spec.get("expansions", ()))
    locations = list(spec["locations"])
    rivers = [list(r) for r in spec["rivers"]]
    errors = []

    # ------------------------------------------------------------ «лупа» для центра
    k, s = spec.get("spread_k", 0.5), spec.get("spread_s", 300.0)
    cx0, cy0 = next(l for l in locations if l[0] == spec["center"])[3:5]
    cx, cy = cx0 * FIELD_W, cy0 * FIELD_H

    def spread(x, y):
        px, py = x * FIELD_W - cx, y * FIELD_H - cy
        f = 1 + k * math.exp(-(math.hypot(px, py) / s) ** 2)
        nx, ny = (cx + px * f) / FIELD_W, (cy + py * f) / FIELD_H
        return round(min(max(nx, 0.015), 0.985), 3), round(min(max(ny, 0.015), 0.985), 3)

    locations = [(i, n, d) + spread(x, y) for i, n, d, x, y in locations]
    rivers = [[spread(x, y) for x, y in r] for r in rivers]
    loc = {l[0]: l for l in locations}
    pos = {l[0]: (l[3], l[4]) for l in locations}
    px = {i: (x * FIELD_W, y * FIELD_H) for i, (x, y) in pos.items()}
    dist_names = {i for i, _ in districts}
    for l in locations:
        if l[2] not in dist_names: errors.append(f"место {l[0]}: нет района {l[2]}")
    ids = [l[0] for l in locations]
    if len(ids) != len(set(ids)): errors.append("повторяются id мест")

    def crosses_river(a, b):
        return any(_inter(pos[a], pos[b], r[i], r[i + 1]) for r in rivers for i in range(len(r) - 1))

    # ------------------------------------------------------------ дороги: длина и ресурс
    center_xy = (cx0, cy0)
    roads = []
    counter = 0
    for entry in spec["roads"]:
        a, b = entry[0], entry[1]
        res = entry[2] if len(entry) > 2 else None
        length = entry[3] if len(entry) > 3 else None
        if a not in loc or b not in loc:
            errors.append(f"дорога {a}-{b}: нет такого места"); continue
        d = math.dist(px[a], px[b])
        if length is None: length = auto_length(d)
        bridge = crosses_river(a, b)
        if res is None:
            counter += 1
            far = max(math.dist(pos[a], center_xy), math.dist(pos[b], center_xy))
            outer = loc[a][2] in expansions or loc[b][2] in expansions or far > 0.36
            if bridge: res = S
            elif counter % 7 == 0 and length <= 2: res = ANY
            elif outer: res = G if counter % 3 else A
            else: res = A if counter % 2 else C
        if bridge and res != S: errors.append(f"дорога {a}-{b} пересекает реку — должна быть мостом (steel)")
        if not bridge and res == S: errors.append(f"дорога {a}-{b}: мост не пересекает реку")
        roads.append((a, b, length, res))

    pairs = [tuple(sorted((a, b))) for a, b, _, _ in roads]
    dup = [p for p, c in collections.Counter(pairs).items() if c > 1]
    if dup: errors.append(f"повторяются дороги: {dup}")

    # Дороги не должны пересекаться друг с другом (общий конец — можно) и проходить вплотную к чужому месту.
    for (a1, b1, _, _), (a2, b2, _, _) in itertools.combinations(roads, 2):
        if len({a1, b1, a2, b2}) < 4: continue
        if _inter(px[a1], px[b1], px[a2], px[b2]):
            errors.append(f"дороги пересекаются: {a1}-{b1} и {a2}-{b2}")
    for a, b, _, _ in roads:
        for l in locations:
            if l[0] in (a, b): continue
            if _seg_dist(px[l[0]], px[a], px[b]) < 16:
                errors.append(f"дорога {a}-{b} проходит прямо через место {l[0]}")

    # Места не на реке.
    for l in locations:
        for r in rivers:
            for i in range(len(r) - 1):
                pa = (r[i][0] * FIELD_W, r[i][1] * FIELD_H); pb = (r[i + 1][0] * FIELD_W, r[i + 1][1] * FIELD_H)
                if _seg_dist(px[l[0]], pa, pb) < 18: errors.append(f"место {l[0]} стоит на реке")

    adj = collections.defaultdict(list)
    for a, b, n, r in roads:
        adj[a].append((b, n)); adj[b].append((a, n))
    seen = {locations[0][0]}; st = [locations[0][0]]
    while st:
        u = st.pop()
        for v, _ in adj[u]:
            if v not in seen: seen.add(v); st.append(v)
    if len(seen) != len(locations): errors.append(f"нет связи с местами: {set(loc) - seen}")

    # Проверки отрисовки.
    mind = min(math.dist(px[a], px[b]) for a, b in itertools.combinations(px, 2))
    if mind < 60:
        close = min(itertools.combinations(px, 2), key=lambda p: math.dist(px[p[0]], px[p[1]]))
        errors.append(f"места слишком близко: {close} {mind:.0f}px")

    def box(l):
        x, y = px[l[0]]; w = len(l[1]) * 7.2 + 10
        return (x - w / 2, y + 13, x + w / 2, y + 31)
    for l1, l2 in itertools.combinations(locations, 2):
        A_, B_ = box(l1), box(l2)
        if A_[0] < B_[2] and B_[0] < A_[2] and A_[1] < B_[3] and B_[1] < A_[3]:
            errors.append(f"подписи наезжают: {l1[1]} и {l2[1]}")
    # Подпись не должна лежать на дороге, проходящей под местом.
    for l in locations:
        x0, y0, x1, y1 = box(l)
        for a, b, _, _ in roads:
            if l[0] in (a, b): continue
            if any(_inter(px[a], px[b], p, q) for p, q in (((x0, y0), (x1, y0)), ((x0, y1), (x1, y1)), ((x0, y0), (x0, y1)), ((x1, y0), (x1, y1)))):
                errors.append(f"подпись «{l[1]}» лежит на дороге {a}-{b}")

    if errors:
        print("ОШИБКИ КАРТЫ " + spec["name"] + ":")
        for e in errors: print("  -", e)
        raise SystemExit(1)

    # ------------------------------------------------------------ контракты
    def dist_from(srcs, skip=frozenset()):
        d = {x: 0 for x in srcs}; pq = [(0, x) for x in srcs]
        while pq:
            du, u = heapq.heappop(pq)
            if du > d[u]: continue
            for v, w in adj[u]:
                if v in skip: continue
                if du + w < d.get(v, 1e9): d[v] = du + w; heapq.heappush(pq, (du + w, v))
        return d

    members = collections.defaultdict(list)
    for l in locations: members[l[2]].append(l[0])
    closed = frozenset(l[0] for l in locations if l[2] in expansions)
    D = {}
    for a in members:
        d = dist_from(members[a], frozenset() if a in expansions else closed)
        for b in members:
            if a != b: D[(a, b)] = min(d.get(x, 1e9) for x in members[b])

    def pts(d): return max(4, d + 1 + (2 if d >= 9 else 0) + (3 if d >= 13 else 0))

    short_n, mid_n, long_n = spec.get("contract_counts", (12, 14, 10))
    cand = [(a, b) for a in members for b in members
            if a < b and D[(a, b)] >= 4 and a not in expansions and b not in expansions]
    cand.sort(key=lambda p: (D[p], p))
    short = [p for p in cand if D[p] <= 6]; mid = [p for p in cand if 7 <= D[p] <= 10]; long_ = [p for p in cand if D[p] >= 11]
    chosen = []
    for lst, n in ((short, short_n), (mid, mid_n), (long_, long_n)):
        random.Random(len(lst) + 7).shuffle(lst); chosen += lst[:n]
    chosen += list(spec.get("expansion_contracts", ()))
    contracts = []
    for i, (a, b) in enumerate(chosen, 1):
        contracts.append({"id": f"c{i:02d}", "from": a, "to": b, "points": pts(D[(a, b)])})
    bad = []
    for kind, lst in (("transit", spec.get("transit", ())), ("hub", spec.get("hub", ()))):
        for a, b, c in lst:
            if kind == "transit":
                da, db = dist_from(members[a], closed), dist_from(members[b], closed)
                d = min(da.get(x, 1e9) + db.get(x, 1e9) for x in members[c])
            else:
                d = D[(a, b)] + D[(a, c)]
            if not (6 <= d < 1e8):
                bad.append(f"{kind} {a}-{b}-{c}: длина {d}"); continue
            contracts.append({"id": f"c{len(contracts) + 1:02d}", "from": a, "to": b,
                              "points": pts(d) + (2 if kind == "transit" else 1), "kind": kind, "third": c})

    if bad:
        print("Сложные контракты не подходят (нужна длина 6+):", *bad, sep="\n  ")
        print("Длины между районами (для подбора):")
        for a in members:
            print("  ", a, {b: D[(a, b)] for b in members if b != a and a not in expansions and b not in expansions})
        raise SystemExit(1)

    data = {
        "name": spec["name"],
        "districts": [dict({"id": i, "name": n}, **({"expansion": True} if i in expansions else {})) for i, n in districts],
        "locations": [{"id": i, "name": n, "district": dd, "x": x, "y": y} for i, n, dd, x, y in locations],
        "roads": [{"id": f"r{k:02d}", "from": a, "to": b, "length": n, "resource": r} for k, (a, b, n, r) in enumerate(roads, 1)],
        "contracts": contracts,
        "river": [{"x": x, "y": y} for x, y in rivers[0]],
        "rivers": [{"name": nm, "points": [{"x": x, "y": y} for x, y in r]} for nm, r in zip(spec.get("river_names", [""] * len(rivers)), rivers)],
        "goals": spec["goals"],
        "piecesScale": spec.get("pieces_scale", 1.0),
        **({"ring": spec["ring"]} if spec.get("ring") else {}),
    }
    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=1)

    if verbose:
        tot = collections.Counter(); cnt = collections.Counter()
        for a, b, n, r in roads: tot[r] += n; cnt[r] += 1
        print(f"{spec['name']}: мест {len(locations)}, дорог {len(roads)}, суммарная длина {sum(tot.values())}, "
              f"минимум между местами {mind:.0f}px")
        print("  длина по ресурсам", dict(tot)); print("  дорог по ресурсам", dict(cnt))
        print("  длины", dict(sorted(collections.Counter(n for _, _, n, _ in roads).items())))
        print("  контрактов", len(contracts), "очки", sorted(c["points"] for c in contracts))
    return data
