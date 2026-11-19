"""Предметы в руке: свиток чертежей (мужчина) и планшет (женщина).

Раньше они были сгенерированы отдельно и просто лежали поверх прямой руки. Теперь предмет рисуется здесь, по
кисти базовой фигуры, и рука «держит» его:
- свиток лежит наискосок в кулаке: середину закрывает кисть с большим пальцем, концы торчат с обеих сторон;
- планшет висит под кистью, его верхний край зажат: большой палец лежит поверх экрана, остальные пальцы — за ним.
Кисть (или большой палец) копируется в слой из слоя тела, поэтому на каждый тон кожи — свой вариант слоя
(<имя>_tan.png, <имя>_dark.png), их подхватывает CharacterCatalog.Texture.

python3 wardrobe_handheld.py <папка Character>
"""
import math
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

W, H = 512, 768
SS = 4  # суперсэмплинг


def lerp(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(len(a)))


def tube_sprite(length, radius):
    """Свиток в своей системе: вертикальный цилиндр length×2r (px при SS), сверху — торец рулона."""
    w, h = int(2 * radius), int(length)
    im = Image.new("RGBA", (w, h + int(radius)), (0, 0, 0, 0))
    px = im.load()
    base, light, dark = (46, 108, 196), (150, 196, 245), (18, 50, 110)
    for x in range(w):
        u = (x + 0.5) / w * 2 - 1                    # −1..1 поперёк
        shade = math.sqrt(max(0.0, 1 - u * u))
        hl = math.exp(-((u + 0.35) ** 2) / 0.04)     # блик слева от центра
        c = lerp(dark, base, shade)
        c = lerp(c, light, hl * 0.7)
        for y in range(int(radius * 0.5), h):
            px[x, y] = c + (255,)
    d = ImageDraw.Draw(im)
    # Линии чертежа: тонкие белёсые полосы поперёк (изгибаются по цилиндру) и одна вдоль.
    for k in range(6):
        y0 = int(radius * 1.4 + k * (h - radius * 2) / 6)
        d.arc([0, y0 - radius * 0.35, w - 1, y0 + radius * 0.35], 0, 180, fill=(200, 225, 255, 150), width=max(1, SS // 2))
    d.line([(w * 0.62, radius), (w * 0.62, h - 2)], fill=(200, 225, 255, 120), width=max(1, SS // 2))
    # Нижний край — полуэллипс (рулон снизу).
    d.ellipse([0, h - radius * 0.5, w - 1, h + radius * 0.5], fill=lerp(dark, base, 0.6) + (255,))
    # Торец сверху: светлая бумага со спиралью.
    d.ellipse([0, 0, w - 1, radius], fill=(225, 238, 252, 255), outline=(30, 70, 140, 255), width=SS)
    for k in range(1, 4):
        f = k / 4.0
        d.ellipse([w / 2 - w / 2 * f, radius / 2 - radius / 2 * f, w / 2 + w / 2 * f, radius / 2 + radius / 2 * f],
                  outline=(120, 160, 210, 255), width=max(1, SS // 2))
    return im


def tablet_sprite(w, h):
    """Планшет: тёмная рамка, светящийся экран с плиткой интерфейса (px при SS)."""
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    r = int(w * 0.09)
    d.rounded_rectangle([0, 0, w - 1, h - 1], r, fill=(38, 42, 52, 255))
    d.rounded_rectangle([SS, SS, w - 1 - SS, h - 1 - SS], r, outline=(90, 96, 110, 255), width=SS)
    m = int(w * 0.09)
    sx0, sy0, sx1, sy1 = m, m, w - m, h - m
    # Экран: вертикальный градиент.
    for y in range(sy0, sy1):
        t = (y - sy0) / max(1, sy1 - sy0)
        d.line([(sx0, y), (sx1, y)], fill=lerp((70, 170, 230), (30, 80, 160), t) + (255,))
    # Интерфейс: шапка, карта-сетка, строки.
    d.rectangle([sx0, sy0, sx1, sy0 + (sy1 - sy0) * 0.12], fill=(240, 170, 60, 255))
    gy0, gy1 = sy0 + (sy1 - sy0) * 0.18, sy0 + (sy1 - sy0) * 0.58
    d.rectangle([sx0 + SS * 2, gy0, sx1 - SS * 2, gy1], fill=(200, 230, 250, 255))
    for k in range(1, 4):
        x = sx0 + (sx1 - sx0) * k / 4
        d.line([(x, gy0), (x, gy1)], fill=(120, 170, 210, 255), width=SS // 2)
        y = gy0 + (gy1 - gy0) * k / 4
        d.line([(sx0 + SS * 2, y), (sx1 - SS * 2, y)], fill=(120, 170, 210, 255), width=SS // 2)
    d.line([(sx0 + (sx1 - sx0) * 0.15, gy1 - (gy1 - gy0) * 0.2), (sx0 + (sx1 - sx0) * 0.5, gy0 + (gy1 - gy0) * 0.35),
            (sx1 - (sx1 - sx0) * 0.15, gy0 + (gy1 - gy0) * 0.2)], fill=(230, 80, 60, 255), width=SS)
    for k in range(3):
        y = gy1 + (sy1 - gy1) * (0.2 + k * 0.27)
        d.rounded_rectangle([sx0 + SS * 2, y, sx0 + (sx1 - sx0) * (0.85 - k * 0.2), y + SS * 2], SS, fill=(220, 240, 255, 220))
    # Блик по стеклу.
    gloss = Image.new("L", (w, h), 0)
    ImageDraw.Draw(gloss).polygon([(sx0, sy0), (sx0 + (sx1 - sx0) * 0.45, sy0), (sx0, sy0 + (sy1 - sy0) * 0.4)], fill=40)
    white = Image.new("RGBA", (w, h), (255, 255, 255, 255))
    im = Image.composite(Image.alpha_composite(im, Image.merge("RGBA", (*white.split()[:3], gloss))), im, im.split()[3])
    return im


def place(sprite, center, angle_deg):
    """Повернуть спрайт (угол против часовой) и положить центром в center (координаты слоя 512×768)."""
    rot = sprite.rotate(angle_deg, resample=Image.BICUBIC, expand=True)
    big = Image.new("RGBA", (W * SS, H * SS), (0, 0, 0, 0))
    big.alpha_composite(rot, (int(center[0] * SS - rot.width / 2), int(center[1] * SS - rot.height / 2)))
    return big.resize((W, H), Image.LANCZOS)


def with_hand(item, body_path, region):
    """Поверх предмета — пиксели кисти из слоя тела в прямоугольнике region (x0, y0, x1, y1)."""
    body = Image.open(body_path).convert("RGBA")
    mask = Image.new("L", (W, H), 0)
    ImageDraw.Draw(mask).rectangle(region, fill=255)
    hand = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    hand.paste(body, (0, 0), Image.composite(body.split()[3], Image.new("L", (W, H), 0), mask))
    # Тень от пальцев на предмете: пиксели предмета у самой кисти темнее.
    from scipy import ndimage
    ia = np.asarray(item).astype(np.float32)
    ha = np.asarray(hand)[..., 3] > 128
    dist = ndimage.distance_transform_edt(~ha)
    shade = np.where(ha, 1.0, np.clip(0.6 + 0.4 * (dist - 1.0) / 3.0, 0.6, 1.0))
    ia[..., :3] *= shade[..., None]
    out = Image.fromarray(np.clip(ia, 0, 255).astype(np.uint8), "RGBA")
    out.alpha_composite(hand)
    return out


def save(im, path):
    a = np.asarray(im).copy()
    a[a[..., 3] == 0, :3] = 0
    Image.fromarray(a, "RGBA").save(path, optimize=True)


def build(root):
    # Мужчина: свиток наискосок сквозь кулак правой руки (слева на кадре): верхний конец с торцом торчит у
    # большого пальца, нижний — из-под мизинца, середину закрывает кисть.
    tube = tube_sprite(126 * SS, 11 * SS)
    blueprint = place(tube, (57, 440), -43)
    for tone, suffix in (("light", ""), ("tan", "_tan"), ("dark", "_dark")):
        body = f"{root}/body/body_{tone}.png"
        save(with_hand(blueprint, body, (0, 398, 120, 480)), f"{root}/accessory/acc_blueprint{suffix}.png")
    # Женщина: планшет висит под кистью, большой палец (x 94–106, y 414–444) — поверх верхнего края.
    tablet = tablet_sprite(54 * SS, 74 * SS)
    tab = place(tablet, (92, 469), 9)
    for tone, suffix in (("light", ""), ("tan", "_tan"), ("dark", "_dark")):
        body = f"{root}/body/body_f_{tone}.png"
        save(with_hand(tab, body, (93, 412, 110, 446)), f"{root}/accessory/acc_f_tablet{suffix}.png")


if __name__ == "__main__":
    build(sys.argv[1])
