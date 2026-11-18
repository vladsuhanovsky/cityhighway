"""Нарезка слоёв гардероба из картинок Scenario.

Каждая вещь сгенерирована правкой базовой фигуры (та же поза и кадр, фон — чистая маджента). Слой вещи — пиксели,
которые отличаются от базы; фон убирается хромакеем, кожа базы в слой не попадает. Выход — PNG 512×768 с альфой.

python3 wardrobe_extract.py <base.jpg> <item.jpg> <slot> <out.png>
python3 wardrobe_extract.py --body <base.jpg> <tone> <out.png>     # tone: light | tan | dark
"""
import sys
import numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage

W, H = 512, 768

# Порог «кожа почти не изменилась» по слотам: у одежды бывают телесные цвета (хаки, беж), их не путать с кожей.
SKIN_DIFF = {"top": 16.0, "bottom": 16.0, "shoes": 16.0}

# Где на кадре может быть вещь (доли высоты): отсекает случайный шум далеко от места вещи.
SLOT_BAND = {
    "hat": (0.0, 0.30),
    "hair": (0.0, 0.62),
    "top": (0.12, 0.80),
    "bottom": (0.40, 0.97),
    "shoes": (0.78, 1.0),
    "accessory": (0.05, 0.75),
}


def load(path):
    return np.asarray(Image.open(path).convert("RGB")).astype(np.float32)


def background_alpha(img):
    """Непрозрачность по хромакею: маджента (R и B высокие, G низкий) — прозрачно."""
    r, g, b = img[..., 0], img[..., 1], img[..., 2]
    key = np.minimum(r, b) - g          # у чистой мадженты ~255
    alpha = 1.0 - np.clip((key - 70.0) / (190.0 - 70.0), 0.0, 1.0)
    return alpha


def despill(img, alpha):
    """Убрать розовый ореол по краям: ограничить R и B зелёным плюс запас там, где альфа неполная."""
    out = img.copy()
    edge = alpha < 0.999
    g = out[..., 1]
    limit = g + 40.0
    for c in (0, 2):
        ch = out[..., c]
        ch[edge] = np.minimum(ch[edge], limit[edge])
    return out


def skin_mask(img):
    """Кожа: тёплый оттенок, средняя насыщенность, не тёмная (брови, ресницы — не кожа)."""
    hsv = np.asarray(Image.fromarray(img.astype(np.uint8)).convert("HSV")).astype(np.float32)
    h, s, v = hsv[..., 0] * 360.0 / 255.0, hsv[..., 1] / 255.0, hsv[..., 2] / 255.0
    return ((h < 50) | (h > 330)) & (s > 0.14) & (v > 0.22)


def to_png(rgb, alpha, out):
    rgba = np.dstack([np.clip(rgb, 0, 255), np.clip(alpha * 255.0, 0, 255)]).astype(np.uint8)
    im = Image.fromarray(rgba, "RGBA")
    # Уменьшение с премультипликацией, чтобы у краёв не было тёмной/розовой каймы.
    pre = rgba.astype(np.float32)
    pre[..., :3] *= pre[..., 3:4] / 255.0
    small = Image.fromarray(pre.astype(np.uint8), "RGBA").resize((W, H), Image.LANCZOS)
    s = np.asarray(small).astype(np.float32)
    a = s[..., 3:4]
    s[..., :3] = np.where(a > 0, s[..., :3] * 255.0 / np.maximum(a, 1), 0)
    Image.fromarray(np.clip(s, 0, 255).astype(np.uint8), "RGBA").save(out, optimize=True)


def register(base, item, lo, hi):
    """Если правка сдвинула или чуть масштабировала фигуру, подогнать картинку вещи к базе.
    Подбор масштаба и сдвига — по уменьшенным копиям, по строкам вне места вещи."""
    def small(a):
        return np.asarray(Image.fromarray(a.astype(np.uint8)).resize((256, 384), Image.BILINEAR)).astype(np.float32)
    bs, is_ = small(base), small(item)
    rows = np.ones(384, bool)
    rows[int(lo * 384):int(hi * 384)] = False
    def cost(img):
        return np.abs(bs - img).max(axis=2)[rows].mean()
    best = (cost(is_), 1.0, 0, 0)
    if best[0] < 6.0:
        return item
    im = Image.fromarray(item.astype(np.uint8))
    H0, W0 = base.shape[:2]
    for sc in np.arange(0.94, 1.0601, 0.01):
        w, h = int(round(W0 * sc)), int(round(H0 * sc))
        scaled = im.resize((w, h), Image.BILINEAR)
        for dy in range(-48, 49, 4):
            for dx in range(-16, 17, 4):
                canvas = Image.new("RGB", (W0, H0), (255, 0, 255))
                canvas.paste(scaled, ((W0 - w) // 2 + dx, (H0 - h) // 2 + dy))
                c = cost(small(np.asarray(canvas)))
                if c < best[0]:
                    best = (c, sc, dx, dy)
    c, sc, dx, dy = best
    w, h = int(round(W0 * sc)), int(round(H0 * sc))
    canvas = Image.new("RGB", (W0, H0), (255, 0, 255))
    canvas.paste(im.resize((w, h), Image.LANCZOS), ((W0 - w) // 2 + dx, (H0 - h) // 2 + dy))
    print(f"registered scale={sc:.2f} dx={dx} dy={dy} cost={c:.1f}", file=sys.stderr)
    return np.asarray(canvas).astype(np.float32)


def extract_item(base_path, item_path, slot, out, band_override=None):
    base, item = load(base_path), load(item_path)
    hgt = base.shape[0]
    lo0, hi0 = band_override or SLOT_BAND.get(slot, (0.0, 1.0))
    item = register(base, item, lo0, hi0)
    bb = ndimage.uniform_filter(base, size=(3, 3, 1))
    ib = ndimage.uniform_filter(item, size=(3, 3, 1))
    diff = np.abs(bb - ib).max(axis=2)
    mask = diff > 28.0

    lo, hi = band_override or SLOT_BAND.get(slot, (0.0, 1.0))
    band = np.zeros_like(mask)
    band[int(lo * hgt):int(hi * hgt)] = True
    mask &= band

    # Кожа базы, которую правка лишь чуть перекрасила, — не вещь (руки у рации, края причёски).
    base_skin, item_skin = skin_mask(base), skin_mask(item)
    skin_diff = SKIN_DIFF.get(slot, 40.0)
    mask &= ~(base_skin & item_skin & (diff < skin_diff))

    # Шляпа, причёска, аксессуар никогда не бывают цвета кожи: если правка чуть изменила лицо, это не вещь.
    if slot in ("hat", "hair", "accessory"):
        mask &= ~skin_like(item, base_skin_mean(base))

    mask = ndimage.binary_opening(mask, iterations=1)
    labels, n = ndimage.label(mask)
    if n:
        sizes = ndimage.sum(mask, labels, range(1, n + 1))
        keep = np.zeros(n + 1, bool)
        # У шляпы оставляем только крупные куски: брови и мелочи лица, которые правка перерисовала, — отдельные пятна.
        share = 0.15 if slot == "hat" else 0.02
        keep[1:] = sizes >= max(120.0, sizes.max() * share)
        mask = keep[labels]
    mask = ndimage.binary_closing(mask, iterations=4)
    mask = ndimage.binary_fill_holes(mask)
    core = mask
    # Наружу (на фон и одежду базы) слой расширяется на 2 px — чтобы захватить сглаженный край вещи. Там, где вещь
    # граничит с кожей базы, край, наоборот, на 1 px внутрь: иначе на тёмной коже виден светлый контур у ворота.
    base_skin_wide = ndimage.binary_dilation(base_skin, iterations=1)
    mask = (ndimage.binary_dilation(core, iterations=2) & ~base_skin_wide) | ndimage.binary_erosion(core, iterations=1)
    skin_both = base_skin & item_skin
    mask &= ~(skin_both & (diff < skin_diff))

    soft = np.asarray(Image.fromarray((mask * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2))).astype(np.float32) / 255.0
    alpha = np.minimum(soft, background_alpha(item))
    rgb = despill(item, alpha)
    to_png(rgb, alpha, out)
    return float(mask.mean())


TONES = {"tan": (196.0, 136.0, 96.0), "dark": (118.0, 76.0, 52.0)}


def base_skin_mean(base):
    core = skin_mask(base) & (background_alpha(base) > 0.5)
    return base[core].mean(axis=0)


def skin_like(img, mean):
    """Пиксели цвета кожи базы (по цветности, без учёта яркости): их перекрашивают под тон тела."""
    total = img.sum(axis=2, keepdims=True) + 1e-3
    chroma = img / total
    ref = mean / mean.sum()
    dist = np.sqrt(((chroma - ref) ** 2).sum(axis=2))
    lum = img.mean(axis=2)
    return (dist < 0.05) & (lum > 125.0)


def tint_layer(path, mean, out_prefix):
    """Варианты слоя под смуглую и тёмную кожу: перекрашены только пиксели цвета кожи. Нет кожи — нет вариантов."""
    im = np.asarray(Image.open(path).convert("RGBA")).astype(np.float32)
    rgb, a = im[..., :3], im[..., 3]
    skin = skin_like(rgb, mean) & (a > 10)
    if skin.sum() < 60:
        return []
    soft = ndimage.gaussian_filter(skin.astype(np.float32), 0.6)[..., None]
    made = []
    for tone, target in TONES.items():
        tinted = rgb * (np.array(target, np.float32) / mean)
        out = rgb * (1 - soft) + tinted * soft
        res = np.dstack([np.clip(out, 0, 255), a]).astype(np.uint8)
        Image.fromarray(res, "RGBA").save(f"{out_prefix}_{tone}.png", optimize=True)
        made.append(tone)
    return made


def extract_body(base_path, tone, out):
    base = load(base_path)
    alpha = background_alpha(base)
    rgb = base.copy()
    if tone in TONES:
        core = skin_mask(base) & (alpha > 0.5)
        mean = base[core].mean(axis=0)
        # Край фигуры (полупрозрачные пиксели) и мелкие пропуски — тоже кожа, если вокруг кожа.
        skin = ndimage.binary_closing(core, iterations=3) | (ndimage.binary_dilation(core, iterations=3) & (alpha < 0.98))
        soft = ndimage.gaussian_filter(skin.astype(np.float32), 0.8)[..., None]
        target = np.array(TONES[tone], np.float32)
        tinted = base * (target / mean)
        rgb = base * (1 - soft) + tinted * soft
    rgb = despill(rgb, alpha)
    to_png(rgb, alpha, out)


if __name__ == "__main__":
    if sys.argv[1] == "--tint":
        mean = base_skin_mean(load(sys.argv[2]))
        print(tint_layer(sys.argv[3], mean, sys.argv[4]))
    elif sys.argv[1] == "--body":
        extract_body(sys.argv[2], sys.argv[3], sys.argv[4])
    else:
        band = tuple(float(v) for v in sys.argv[5].split(",")) if len(sys.argv) > 5 else None
        print(extract_item(sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4], band))
