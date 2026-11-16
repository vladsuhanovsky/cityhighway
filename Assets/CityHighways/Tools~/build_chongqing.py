"""Карта «Чунцин»: город мостов и гор. Полуостров Юйчжун зажат между Янцзы и Цзялинцзян, реки сливаются у Чаотяньмэнь.
Запуск: python3 build_chongqing.py [путь к json]."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mapkit import build, A, C, S, G, ANY

OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                                       "../Resources/CityHighways/Maps/chongqing.json")

districts = [
    ("center", "Юйчжун"), ("jiangbei", "Цзянбэй"), ("yubei", "Юбэй"), ("airports", "Аэропорт"),
    ("liangjiang", "Лянцзян"), ("shapingba", "Шапинба"), ("huxi", "Хуси"), ("jiulongpo", "Цзюлунпо"),
    ("dadukou", "Дадукоу"), ("nanan", "Наньань"), ("nanshan", "Гора Наньшань"),
    ("banan", "Банань"), ("beibei", "Бэйбэй"), ("changshou", "Чаншоу"),
]
expansions = {"banan", "beibei", "changshou"}

locations = [
    # Юйчжун — полуостров между реками
    ("jiefangbei", "Цзефанбэй", "center", 0.53, 0.50), ("chaotianmen", "Чаотяньмэнь", "center", 0.595, 0.49),
    ("hongyadong", "Хунъядун", "center", 0.475, 0.465), ("liziba", "Лицзыба", "center", 0.41, 0.53),
    ("daping", "Дапин", "center", 0.36, 0.585),
    # Цзянбэй — северный берег Цзялинцзян
    ("jiangbeizui", "Цзянбэйцзуй", "jiangbei", 0.625, 0.38), ("guanyinqiao", "Гуаньиньцяо", "jiangbei", 0.53, 0.33),
    ("hongensi", "Хунъэньсы", "jiangbei", 0.465, 0.265), ("cuntan", "Цуньтань", "jiangbei", 0.75, 0.25),
    # Юбэй и аэропорт
    ("yuelai", "Юэлай", "yubei", 0.44, 0.165), ("lianglu", "Лянлу", "yubei", 0.64, 0.17),
    ("dazhulin", "Дачжулинь", "yubei", 0.355, 0.235),
    ("airport", "Аэропорт Цзянбэй", "airports", 0.565, 0.06), ("t3", "Терминал Т3", "airports", 0.715, 0.075),
    # Лянцзян — новый район на северо-востоке
    ("liangjiang", "Лянцзян", "liangjiang", 0.87, 0.175), ("guoyuan", "Порт Гоюань", "liangjiang", 0.935, 0.235),
    ("shuangfengqiao", "Шуанфэнцяо", "liangjiang", 0.79, 0.12),
    # Шапинба и Хуси — запад, за Цзялинцзян
    ("shapingba", "Шапинба", "shapingba", 0.24, 0.385), ("ciqikou", "Цыцикоу", "shapingba", 0.315, 0.38),
    ("shuangbei", "Шуанбэй", "shapingba", 0.18, 0.30), ("geleshan", "Гэлэшань", "shapingba", 0.14, 0.395),
    ("huxi", "Хуси", "huxi", 0.065, 0.455), ("xiyong", "Сиюн", "huxi", 0.085, 0.565),
    # Цзюлунпо и Дадукоу — северный берег Янцзы
    ("yangjiaping", "Янцзяпин", "jiulongpo", 0.325, 0.50), ("erlang", "Эрлан", "jiulongpo", 0.225, 0.55),
    ("huayan", "Хуаянь", "jiulongpo", 0.15, 0.625),
    ("dadukou", "Дадукоу", "dadukou", 0.265, 0.68), ("baishiyi", "Байшии", "dadukou", 0.12, 0.73),
    # Наньань и гора Наньшань — южный берег Янцзы
    ("nanping", "Наньпин", "nanan", 0.55, 0.68), ("longmenhao", "Лунмыньхао", "nanan", 0.655, 0.575),
    ("danzishi", "Даньцзыши", "nanan", 0.74, 0.475),
    ("nanshan", "Наньшань", "nanshan", 0.81, 0.60), ("chayuan", "Чаюань", "nanshan", 0.73, 0.72),
    ("tushan", "Тушань", "nanshan", 0.615, 0.80),
    # районы-расширения
    ("banan", "Банань", "banan", 0.575, 0.865), ("yudong", "Юйдун", "banan", 0.43, 0.875),
    ("beibei", "Бэйбэй", "beibei", 0.22, 0.06), ("jinyunshan", "Цзиньюньшань", "beibei", 0.33, 0.11),
    ("changshou", "Чаншоу", "changshou", 0.95, 0.07), ("yanjia", "Яньцзя", "changshou", 0.86, 0.04),
]

roads = [
    # Юйчжун
    ("jiefangbei", "chaotianmen", ANY), ("jiefangbei", "hongyadong", A), ("jiefangbei", "liziba", C),
    ("hongyadong", "liziba", A), ("liziba", "daping", ANY), ("daping", "yangjiaping", C), ("liziba", "yangjiaping", A),
    # мосты через Цзялинцзян и Янцзы из центра
    ("hongyadong", "guanyinqiao"), ("chaotianmen", "jiangbeizui"), ("jiefangbei", "nanping"),
    ("chaotianmen", "longmenhao"), ("daping", "nanping"),
    # Цзянбэй и Юбэй
    ("jiangbeizui", "guanyinqiao", A), ("guanyinqiao", "hongensi", ANY), ("hongensi", "yuelai", C),
    ("hongensi", "dazhulin", A), ("jiangbeizui", "cuntan", C), ("cuntan", "lianglu", A),
    ("guanyinqiao", "lianglu", C), ("yuelai", "dazhulin", G), ("yuelai", "lianglu", A),
    ("yuelai", "airport", C), ("lianglu", "airport", A), ("lianglu", "t3", C), ("airport", "t3", ANY),
    ("lianglu", "shuangfengqiao", G), ("t3", "shuangfengqiao", A), ("shuangfengqiao", "liangjiang", C),
    ("liangjiang", "guoyuan", ANY), ("cuntan", "liangjiang", G), ("cuntan", "shuangfengqiao", A),
    # Шапинба и Хуси
    ("shapingba", "ciqikou", A), ("shapingba", "shuangbei", C), ("shapingba", "geleshan", G),
    ("ciqikou", "dazhulin"), ("ciqikou", "liziba", C), ("ciqikou", "yangjiaping", ANY),
    ("huxi", "geleshan", G), ("huxi", "xiyong", ANY), ("geleshan", "shuangbei", G), ("huxi", "shuangbei", G),
    # Цзюлунпо и Дадукоу
    ("yangjiaping", "erlang", A), ("yangjiaping", "shapingba", C), ("erlang", "huayan", ANY),
    ("erlang", "xiyong", G), ("xiyong", "huayan", A), ("dadukou", "daping", A), ("dadukou", "erlang", C),
    ("dadukou", "baishiyi", G), ("baishiyi", "huayan", A), ("dadukou", "yangjiaping", G),
    # Наньань и гора Наньшань
    ("nanping", "longmenhao", A), ("longmenhao", "danzishi", C), ("danzishi", "jiangbeizui"),
    ("danzishi", "cuntan"), ("danzishi", "nanshan", G), ("longmenhao", "nanshan", A),
    ("nanshan", "chayuan", G), ("chayuan", "tushan", G), ("nanping", "tushan", C), ("nanping", "chayuan", A),
    # районы-расширения
    ("banan", "yudong", ANY), ("tushan", "banan", G), ("nanping", "yudong", A), ("dadukou", "yudong"),
    ("beibei", "jinyunshan", ANY), ("jinyunshan", "yuelai", G), ("jinyunshan", "dazhulin", G), ("beibei", "shuangbei"),
    ("changshou", "yanjia", ANY), ("yanjia", "shuangfengqiao", G), ("changshou", "guoyuan", A), ("yanjia", "t3", G),
]

# Янцзы — с юго-запада, огибает Юйчжун и уходит на северо-восток; Цзялинцзян — с северо-запада до слияния у Чаотяньмэнь.
yangtze = [(0.0, 0.86), (0.10, 0.83), (0.20, 0.79), (0.30, 0.73), (0.38, 0.665), (0.45, 0.615), (0.52, 0.59),
           (0.585, 0.57), (0.625, 0.525), (0.645, 0.475), (0.68, 0.41), (0.74, 0.35), (0.82, 0.31),
           (0.91, 0.29), (1.0, 0.265)]
jialing = [(0.06, 0.0), (0.13, 0.10), (0.21, 0.19), (0.28, 0.275), (0.345, 0.345), (0.415, 0.40),
           (0.50, 0.42), (0.575, 0.44), (0.645, 0.475)]

goals = [
    {"id": "g1", "name": "Воздушная гавань", "kind": "locations", "district": "airports", "count": 2, "points": 8},
    {"id": "g2", "name": "Город мостов", "kind": "bridges", "count": 3, "points": 8},
    {"id": "g3", "name": "Слияние рек", "kind": "locations", "district": "center", "count": 3, "points": 8},
    {"id": "g4", "name": "Горные серпантины", "kind": "resource_pieces", "resource": "gravel", "count": 9, "points": 6},
    {"id": "g5", "name": "Скоростные трассы", "kind": "long_roads", "length": 3, "count": 3, "points": 7},
    {"id": "g6", "name": "Два берега", "kind": "network", "count": 6, "points": 8},
    {"id": "g7", "name": "Южный берег", "kind": "locations", "district": "nanan", "count": 3, "points": 6},
]

spec = {
    "name": "Чунцин", "districts": districts, "expansions": expansions, "locations": locations,
    "roads": roads, "rivers": [yangtze, jialing], "river_names": ["Янцзы", "Цзялинцзян"],
    "center": "jiefangbei", "spread_k": 0.5, "spread_s": 300.0,
    "contract_counts": (10, 12, 8), "pieces_scale": 0.8,
    "expansion_contracts": [("banan", "center"), ("banan", "jiangbei"), ("beibei", "center"), ("beibei", "nanan"),
                            ("changshou", "center"), ("changshou", "jiulongpo")],
    "transit": [("shapingba", "nanan", "center"), ("airports", "dadukou", "center"), ("huxi", "liangjiang", "jiangbei"),
                ("yubei", "nanshan", "nanan"), ("jiulongpo", "airports", "shapingba")],
    "hub": [("center", "airports", "nanshan"), ("jiangbei", "huxi", "dadukou"), ("nanan", "yubei", "jiulongpo"),
            ("shapingba", "liangjiang", "nanan")],
    "goals": goals,
}

if __name__ == "__main__":
    build(spec, OUT)
