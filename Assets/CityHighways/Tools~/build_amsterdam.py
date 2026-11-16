"""Карта «Амстердам»: город каналов, две водные преграды — залив Эй (север) и река Амстел (с юга до центра).
Запуск: python3 build_amsterdam.py [путь к json]."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mapkit import build, A, C, S, G, ANY

OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                                       "../Resources/CityHighways/Maps/amsterdam.json")

districts = [
    ("center", "Центр"), ("canals", "Каналы"), ("west", "Запад"), ("museum", "Музейный квартал"),
    ("pijp", "Де Пейп"), ("zuid", "Зёйдас"), ("east", "Восток"), ("watergraafsmeer", "Ватерграфсмер"),
    ("docks", "Восточные доки"), ("noord", "Норд"), ("newwest", "Новый Запад"), ("port", "Порт"),
    ("zuidoost", "Зёйдост"), ("airports", "Схипхол"),
    ("zaandam", "Заандам"), ("ijburg", "Эйбург"), ("amstelveen", "Амстелвен"),
]
expansions = {"zaandam", "ijburg", "amstelveen"}

# id, название, район, x (запад→восток), y (север→юг), 0..1 — до «лупы».
locations = [
    ("dam", "Дам", "center", 0.48, 0.45), ("centraal", "Вокзал", "center", 0.51, 0.40),
    ("nieuwmarkt", "Ньюмаркт", "center", 0.555, 0.445),
    ("jordaan", "Йордан", "west", 0.42, 0.425), ("westerpark", "Вестерпарк", "west", 0.355, 0.40),
    ("bosenlommer", "Бос-эн-Ломмер", "west", 0.30, 0.47), ("oudwest", "Оуд-Вест", "west", 0.335, 0.54),
    ("leidseplein", "Лейдсеплейн", "canals", 0.43, 0.515), ("rembrandtplein", "Рембрандтплейн", "canals", 0.50, 0.515),
    ("rijksmuseum", "Рейксмюсеум", "museum", 0.46, 0.585), ("vondelpark", "Вондел-парк", "museum", 0.39, 0.615),
    ("albertcuyp", "Альберт-Кёйп", "pijp", 0.525, 0.615), ("rai", "RAI", "pijp", 0.525, 0.71),
    ("zuidas", "Зёйдас", "zuid", 0.43, 0.725), ("buitenveldert", "Бёйтенвелдерт", "zuid", 0.47, 0.82),
    ("waterlooplein", "Ватерлоплейн", "east", 0.575, 0.505), ("artis", "Артис", "east", 0.625, 0.46),
    ("oosterpark", "Остерпарк", "east", 0.645, 0.545), ("indischebuurt", "Индише-бюрт", "east", 0.735, 0.485),
    ("watergraafsmeer", "Ватерграфсмер", "watergraafsmeer", 0.72, 0.60),
    ("amstelstation", "Станция Амстел", "watergraafsmeer", 0.625, 0.655), ("diemen", "Димен", "watergraafsmeer", 0.82, 0.63),
    ("pta", "Терминал", "docks", 0.585, 0.40), ("javaeiland", "Ява-эйланд", "docks", 0.665, 0.405),
    ("knsm", "КНСМ", "docks", 0.75, 0.40),
    ("ndsm", "NDSM", "noord", 0.39, 0.29), ("eye", "Музей EYE", "noord", 0.50, 0.325),
    ("noorderpark", "Нордерпарк", "noord", 0.45, 0.205), ("buikslotermeer", "Бёйкслотермер", "noord", 0.57, 0.20),
    ("nieuwendam", "Ньювендам", "noord", 0.68, 0.27),
    ("sloterdijk", "Слотердейк", "newwest", 0.265, 0.385), ("slotermeer", "Слотермер", "newwest", 0.19, 0.495),
    ("osdorp", "Осдорп", "newwest", 0.105, 0.575), ("slotervaart", "Слотерварт", "newwest", 0.25, 0.61),
    ("westpoort", "Вестпорт", "port", 0.06, 0.43), ("houthavens", "Хаутхавенс", "port", 0.165, 0.40),
    ("arena", "Арена", "zuidoost", 0.72, 0.80), ("bijlmer", "Бейлмер", "zuidoost", 0.79, 0.90),
    ("gaasperdam", "Гаспердам", "zuidoost", 0.89, 0.81),
    ("schiphol", "Схипхол", "airports", 0.15, 0.82), ("cargo", "Схипхол-Карго", "airports", 0.06, 0.70),
    ("zaandam", "Заандам", "zaandam", 0.21, 0.14), ("zaanseschans", "Заансе-Сханс", "zaandam", 0.29, 0.05),
    ("ijburg", "Эйбург", "ijburg", 0.87, 0.47), ("blijburg", "Блейбург", "ijburg", 0.95, 0.55),
    ("amstelveen", "Амстелвен", "amstelveen", 0.40, 0.91), ("amsterdamsebos", "Амстердамсе-Бос", "amstelveen", 0.27, 0.88),
]

# Дороги: (a, b[, ресурс[, длина]]). Ресурс не задан — подберётся (мост через Эй или Амстел — всегда сталь).
roads = [
    # центр и каналы
    ("dam", "centraal", A), ("dam", "nieuwmarkt", ANY), ("centraal", "nieuwmarkt", C), ("dam", "jordaan", A),
    ("dam", "leidseplein", C), ("dam", "rembrandtplein", A), ("nieuwmarkt", "waterlooplein", A),
    ("centraal", "pta", ANY), ("nieuwmarkt", "pta", C), ("leidseplein", "rembrandtplein", ANY),
    ("leidseplein", "rijksmuseum", A), ("leidseplein", "jordaan", C), ("leidseplein", "oudwest", A),
    ("rembrandtplein", "albertcuyp", C), ("rembrandtplein", "waterlooplein"),
    # музейный квартал, Де Пейп, Зёйдас
    ("rijksmuseum", "albertcuyp", A), ("rijksmuseum", "vondelpark", ANY), ("rijksmuseum", "zuidas", C),
    ("vondelpark", "oudwest", A), ("vondelpark", "slotervaart", C),
    ("albertcuyp", "rai", A), ("rai", "zuidas", C), ("rai", "buitenveldert", A), ("rai", "amstelstation"),
    ("albertcuyp", "oosterpark"),
    ("zuidas", "buitenveldert", ANY), ("zuidas", "schiphol", C), ("zuidas", "slotervaart", A),
    ("buitenveldert", "arena"),
    # восток и Ватерграфсмер
    ("waterlooplein", "artis", A), ("artis", "oosterpark", C), ("artis", "pta", A), ("artis", "javaeiland", C),
    ("artis", "indischebuurt", A), ("oosterpark", "indischebuurt", ANY), ("oosterpark", "watergraafsmeer", C),
    ("oosterpark", "amstelstation", A), ("watergraafsmeer", "amstelstation", ANY), ("watergraafsmeer", "diemen", G),
    ("watergraafsmeer", "indischebuurt", C), ("amstelstation", "arena", C), ("diemen", "arena", G),
    ("diemen", "gaasperdam", G),
    # Восточные доки
    ("pta", "javaeiland", A), ("javaeiland", "knsm", ANY), ("knsm", "indischebuurt", C), ("knsm", "nieuwendam"),
    # Норд
    ("centraal", "eye"), ("eye", "ndsm", A), ("eye", "noorderpark", C), ("eye", "buikslotermeer", A),
    ("noorderpark", "buikslotermeer", ANY), ("buikslotermeer", "nieuwendam", C), ("ndsm", "noorderpark", G),
    ("eye", "nieuwendam", A),
    # запад и Новый Запад
    ("jordaan", "westerpark", A), ("westerpark", "bosenlommer", C), ("bosenlommer", "oudwest", ANY),
    ("westerpark", "ndsm"), ("westerpark", "sloterdijk", A), ("jordaan", "centraal", C),
    ("sloterdijk", "houthavens", C), ("sloterdijk", "bosenlommer", A), ("bosenlommer", "slotermeer", G),
    ("slotermeer", "osdorp", G), ("osdorp", "slotervaart", A), ("slotervaart", "oudwest", C),
    ("slotermeer", "slotervaart", A),
    # порт и Схипхол
    ("westpoort", "houthavens", G), ("westpoort", "slotermeer", G), ("houthavens", "slotermeer", ANY),
    ("schiphol", "osdorp", A), ("schiphol", "slotervaart", G), ("schiphol", "cargo", ANY), ("cargo", "osdorp", G),
    ("cargo", "westpoort", G),
    # Зёйдост
    ("arena", "bijlmer", A), ("bijlmer", "gaasperdam", ANY), ("arena", "gaasperdam", C),
    # районы-расширения
    ("zaandam", "zaanseschans", ANY), ("zaandam", "houthavens"), ("zaandam", "ndsm", G), ("zaanseschans", "noorderpark", G),
    ("ijburg", "blijburg", ANY), ("ijburg", "indischebuurt", A), ("ijburg", "knsm", C), ("blijburg", "diemen", G),
    ("amstelveen", "amsterdamsebos", ANY), ("amstelveen", "buitenveldert", A), ("amsterdamsebos", "schiphol", G),
    ("amsterdamsebos", "zuidas", G), ("amstelveen", "bijlmer"),
]

# Залив Эй: с запада (Нордзеканал) на восток (Эймер). Амстел: с юга до центра (дальше — каналы).
rivers = [
    [(0.0, 0.355), (0.10, 0.36), (0.20, 0.365), (0.30, 0.355), (0.40, 0.37), (0.47, 0.375), (0.53, 0.37),
     (0.62, 0.36), (0.72, 0.345), (0.81, 0.37), (0.90, 0.41), (1.0, 0.44)],
    [(0.665, 1.0), (0.65, 0.89), (0.625, 0.78), (0.598, 0.68), (0.57, 0.60), (0.548, 0.54), (0.535, 0.50)],
]

goals = [
    {"id": "g1", "name": "Ворота Схипхола", "kind": "locations", "district": "airports", "count": 2, "points": 8},
    {"id": "g2", "name": "Город мостов", "kind": "bridges", "count": 3, "points": 8},
    {"id": "g3", "name": "Вся агломерация", "kind": "network", "count": 7, "points": 8},
    {"id": "g4", "name": "Польдерные дороги", "kind": "resource_pieces", "resource": "gravel", "count": 9, "points": 6},
    {"id": "g5", "name": "Кольцевая A10", "kind": "long_roads", "length": 3, "count": 3, "points": 7},
    {"id": "g6", "name": "Сердце Амстердама", "kind": "locations", "district": "center", "count": 3, "points": 8},
    {"id": "g7", "name": "Портовый город", "kind": "locations", "district": "docks", "count": 3, "points": 6},
]

spec = {
    "name": "Амстердам", "districts": districts, "expansions": expansions, "locations": locations,
    "roads": roads, "rivers": rivers, "river_names": ["Эй", "Амстел"],
    "center": "dam", "spread_k": 0.45, "spread_s": 300.0,
    "contract_counts": (12, 14, 8), "pieces_scale": 0.85,
    # Кольцевая A10 — вокруг центра (после «лупы»), с юга по Зёйдасу, с севера через Норд.
    "ring": {"name": "A10", "cx": 0.47, "cy": 0.475, "rx": 0.215, "ry": 0.285},
    "expansion_contracts": [("zaandam", "center"), ("zaandam", "zuid"), ("ijburg", "center"), ("ijburg", "west"),
                            ("amstelveen", "center"), ("amstelveen", "noord")],
    "transit": [("noord", "zuid", "center"), ("newwest", "east", "canals"), ("newwest", "docks", "center"),
                ("airports", "east", "museum"), ("port", "zuidoost", "pijp"), ("noord", "airports", "west")],
    "hub": [("center", "airports", "zuidoost"), ("canals", "noord", "watergraafsmeer"), ("zuid", "docks", "port"),
            ("east", "west", "noord")],
    "goals": goals,
}

if __name__ == "__main__":
    build(spec, OUT)
