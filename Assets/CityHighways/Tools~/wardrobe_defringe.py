"""Убрать розовую кайму (остаток хромакея) по краям слоёв гардероба.

1. Полупрозрачный край получает цветность ближайшего сплошного пикселя (яркость своя), альфа не меняется: сглаживание
   остаётся, маджента фона — нет.
2. В полосе 4 px у края (и в просветах причёсок) розовый оттенок гасится: R и B опускаются до уровня G.
   У вещей, которые сами фиолетовые (жакет), второй шаг пропускается.

python3 wardrobe_defringe.py <png> [<png> ...]
"""
import os
import sys
import numpy as np
from PIL import Image
from scipy import ndimage

PURPLE = ("top_f_blazer",)


def defringe(path):
    im = np.asarray(Image.open(path).convert("RGBA")).astype(np.float32)
    rgb, a = im[..., :3].copy(), im[..., 3]
    solid = a >= 250
    if not solid.any():
        return 0
    soft = (a > 0) & ~solid
    name = os.path.basename(path)

    def despill(c, mask):
        r, g, b = c[..., 0], c[..., 1], c[..., 2]
        k = np.clip(np.minimum(r, b) - g - 4.0, 0, None) * mask
        c[..., 0] -= k
        c[..., 2] -= k
        return int((k > 0).sum())

    count = 0
    if not any(name.startswith(p) for p in PURPLE):
        near = ndimage.binary_dilation(~solid, iterations=4) & (a > 0)
        if name.startswith(("hair_", "hat_f_sunhat")):
            near = a > 0                       # в просветах кудрей и соломы маджента бывает и внутри
        count += despill(rgb, near)
    if name.startswith("hair_"):
        # Бордовые пряди (маджента, смешанная с каштаном): оттенок — как у основной массы волос, насыщенность ниже.
        hsv = np.asarray(Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8)).convert("HSV")).astype(np.float32)
        hue = hsv[..., 0] * 360.0 / 255.0
        body = solid & (hsv[..., 1] > 40)
        if body.any():
            main = np.median(hsv[..., 0][body & ((hue > 5) & (hue < 60))]) if (body & (hue > 5) & (hue < 60)).any() else 15.0
            bad = (a > 0) & ((hue > 270) | (hue < 4)) & (hsv[..., 1] > 30)
            hsv[..., 0][bad] = main
            hsv[..., 1][bad] *= 0.75
            fixed = np.asarray(Image.fromarray(hsv.astype(np.uint8), "HSV").convert("RGB")).astype(np.float32)
            rgb[bad] = fixed[bad]
            count += int(bad.sum())
    # Полупрозрачный край: цветность от ближайшего сплошного пикселя.
    _, (iy, ix) = ndimage.distance_transform_edt(~solid, return_indices=True)
    src = rgb[iy, ix]
    lum = lambda c: c @ np.array([0.3, 0.59, 0.11], np.float32)
    scale = np.clip(lum(rgb) / (lum(src) + 1e-3), 0.25, 1.3)[..., None]
    rgb[soft] = (src * scale)[soft]
    count += int(soft.sum())
    rgb[a == 0] = 0
    res = np.dstack([np.clip(rgb, 0, 255), a]).astype(np.uint8)
    Image.fromarray(res, "RGBA").save(path, optimize=True)
    return count


if __name__ == "__main__":
    for p in sys.argv[1:]:
        print(p, defringe(p))
