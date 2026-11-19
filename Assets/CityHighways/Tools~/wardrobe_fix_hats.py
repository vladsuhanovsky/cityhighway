"""Чистка слоёв головных уборов (после wardrobe_extract.py).

Правка базы иногда задевала лицо: в слой шляпы попадали брови, глаза, полоска лба и контуры плеч (у цилиндра
даже глаза светлого тона). Здесь из слоя убирается всё, что совпадает с лысой базой (лицо, кожа), и мелкие
обрывки вдали от самой шляпы. У широкополой шляпки рваные дыры в полях (солому приняли за кожу) заделываются:
поля закрываются по контуру, пустоты дорисовываются соломой (inpaint), лицо остаётся открытым.

python3 wardrobe_fix_hats.py <base.jpg> <hat.png> [--brim] [--keep-skin]   # --keep-skin: шляпа сама цвета кожи (фетровая)
"""
import sys
import numpy as np
import cv2
from PIL import Image
from scipy import ndimage

W, H = 512, 768


def base_small(path):
    im = Image.open(path).convert("RGB").resize((W, H), Image.LANCZOS)
    return np.asarray(im).astype(np.float32)


def skin_mask(img):
    hsv = np.asarray(Image.fromarray(img.astype(np.uint8)).convert("HSV")).astype(np.float32)
    h, s, v = hsv[..., 0] * 360.0 / 255.0, hsv[..., 1] / 255.0, hsv[..., 2] / 255.0
    return ((h < 50) | (h > 330)) & (s > 0.14) & (v > 0.22)


def fix(base_path, hat_path, brim=False, keep_skin=False):
    base = base_small(base_path)
    im = np.asarray(Image.open(hat_path).convert("RGBA")).astype(np.float32)
    rgb, a = im[..., :3], im[..., 3].copy()
    # Голова базы: кожа и всё, что внутри её контура (брови, глаза, рот), в верхней трети кадра.
    skin = skin_mask(base)
    skin[int(H * 0.34):] = False
    head = ndimage.binary_fill_holes(ndimage.binary_closing(skin, iterations=6))
    # Лицо — нижняя часть головы (ниже макушки): по нему и чистим.
    blur = lambda x: ndimage.uniform_filter(x, size=(3, 3, 1))
    diff = np.abs(blur(rgb) - blur(base)).max(axis=2)
    same = head & (diff < 42.0)
    a[same] = 0
    hsv = np.asarray(Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8)).convert("HSV")).astype(np.float32) / 255.0
    lum = rgb.mean(axis=2)
    # Сдвинутые правкой брови: тёмный коричневый контур вокруг вырезанного выше.
    brows = ndimage.binary_dilation(same, iterations=3) & head & (lum < 110) & (hsv[..., 1] > 0.25)
    a[brows] = 0
    if not brim and not keep_skin:
        # Лоб в тени под козырьком: оранжевый оттенок кожи (красный — вне диапазона, жёлтые каски насыщеннее).
        h, sat, v = hsv[..., 0] * 360.0, hsv[..., 1], hsv[..., 2]
        a[head & (h > 10) & (h < 40) & (sat > 0.15) & (sat < 0.78) & (v > 0.25)] = 0
        # Полоска лба под козырьком (кожа в тени): по цветности кожи базы.
        core = skin & (base.mean(axis=2) > 60)
        mean = base[core].mean(axis=0)
        chroma = rgb / (rgb.sum(axis=2, keepdims=True) + 1e-3)
        ref = mean / mean.sum()
        skinlike = np.sqrt(((chroma - ref) ** 2).sum(axis=2)) < 0.05
        a[head & skinlike & (lum > 50)] = 0
    # Контуры плеч и прочие обрывки — всё, что ниже головы.
    a[int(H * 0.36):] = 0
    solid = a > 96
    labels, n = ndimage.label(solid)
    if n:
        sizes = ndimage.sum(solid, labels, range(1, n + 1))
        keep = np.zeros(n + 1, bool)
        keep[1:] = sizes >= sizes.max() * 0.12
        body = ndimage.binary_dilation(keep[labels], iterations=2)
        a[~body] = 0
    if brim:
        # Поля шляпки: внешний край — эллипс по точкам контура полей, внутри открыто только лицо (голова базы ниже
        # ленты). Пустоты в соломе дорисовываются по соседней соломе.
        solid = a > 128
        # Низ тульи — нижний край чёрной ленты у средней линии.
        cols = np.nonzero(solid.any(axis=0))[0]
        mid = int((cols.min() + cols.max()) / 2)
        dark = solid & (rgb.mean(axis=2) < 70)
        dark[:, :mid - 90] = False
        dark[:, mid + 90:] = False
        dl, dn = ndimage.label(dark)
        ribbon = dl == (1 + int(np.argmax(ndimage.sum(dark, dl, range(1, dn + 1)))))   # лента — самый крупный тёмный кусок
        band_y = int(np.nonzero(ribbon[:, mid - 6:mid + 7].any(axis=1))[0].max()) + 2
        low = solid.copy()
        low[:band_y] = False
        ly, lx = np.nonzero(low)
        x0, x1 = lx.min(), lx.max()
        # Центр по высоте — где поля шире всего (крайние точки слева и справа), низ — самая низкая точка полей.
        cy = (ly[lx <= x0 + 2].mean() + ly[lx >= x1 - 2].mean()) / 2.0
        ry = max(8.0, ly.max() - cy)
        ellipse = (((x0 + x1) / 2.0, cy), (float(x1 - x0), 2.0 * ry), 0.0)
        brim_mask = np.zeros((H, W), np.uint8)
        cv2.ellipse(brim_mask, ellipse, 255, -1)
        brim_mask = brim_mask > 0
        crown_top = np.zeros_like(solid)
        crown_top[:band_y - 4] = True
        crown_top |= ribbon
        # Лицо открыто ниже ленты; лента изгибается, поэтому граница своя в каждом столбце.
        face = head.copy()
        rows = np.arange(H)[:, None]
        has = ribbon.any(axis=0)
        low_row = np.where(has, H - 1 - np.argmax(ribbon[::-1], axis=0) + 2, band_y)
        face &= rows >= np.maximum(low_row, band_y)[None, :]
        crown = solid & (rows < np.maximum(low_row, band_y)[None, :])
        target = (brim_mask & ~face) | crown
        target &= ~(head & ~crown_top & skin_mask(rgb) & (rgb.mean(axis=2) > 60))   # лоб под лентой — не шляпа
        # Обрывки бровей под лентой: коричневое на голове, не солома (светлая) и не лента (чёрная).
        lum2 = rgb.mean(axis=2)
        target &= ~(head & ~crown_top & (lum2 > 60) & (lum2 < 150) & (hsv[..., 1] > 0.15))
        target = ndimage.binary_opening(target, iterations=1)
        holes = target & (a < 200)
        # Поля почти симметричны: дыру сначала заполняем зеркальной соломой с другой стороны (не лентой и не бантом).
        straw = (a >= 200) & (rgb.mean(axis=2) > 120)
        yy, xx = np.nonzero(holes)
        mx = np.clip(2 * mid - xx, 0, W - 1)
        ok = straw[yy, mx]
        rgb[yy[ok], xx[ok]] = rgb[yy[ok], mx[ok]]
        holes[yy[ok], xx[ok]] = False
        rgb8 = np.clip(rgb, 0, 255).astype(np.uint8)
        painted = cv2.inpaint(cv2.cvtColor(rgb8, cv2.COLOR_RGB2BGR), (holes * 255).astype(np.uint8), 7, cv2.INPAINT_TELEA)
        painted = cv2.cvtColor(painted, cv2.COLOR_BGR2RGB).astype(np.float32)
        rgb = np.where(holes[..., None], painted, rgb)
        # Мелкие островки внутри (обрывки на лбу) — убрать: оставить связную шляпу.
        labels, n = ndimage.label(target)
        if n > 1:
            sizes = ndimage.sum(target, labels, range(1, n + 1))
            target = np.isin(labels, 1 + np.nonzero(sizes >= sizes.max() * 0.05)[0])
        a = ndimage.gaussian_filter(target.astype(np.float32), 0.8) * 255.0
    # Мягкий край там, где срезали.
    a = np.minimum(a, ndimage.gaussian_filter((a > 0).astype(np.float32), 0.6) * 255.0 + 0.0 * a)
    out = np.dstack([np.clip(rgb, 0, 255), np.clip(a, 0, 255)]).astype(np.uint8)
    out[out[..., 3] == 0, :3] = 0
    Image.fromarray(out, "RGBA").save(hat_path, optimize=True)


if __name__ == "__main__":
    fix(sys.argv[1], sys.argv[2], "--brim" in sys.argv, "--keep-skin" in sys.argv)
