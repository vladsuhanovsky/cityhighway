"""Подрезать «грязный» край одежды там, где она граничит с кожей.

У рукавов, ворота и низа брюк правка базы оставляла полоску смешанного цвета (ткань + светлая кожа базы); на
смуглой и тёмной коже она видна как серо-зелёная кайма. Здесь край вещи, лежащий на коже тела, плавно уходит в
прозрачность на 2–3 px: под ним видна кожа из слоя тела, вещь не «отрывается» от фигуры.

python3 wardrobe_trim_edges.py <body.png> <item.png> [<item.png> ...]
"""
import sys
import numpy as np
from PIL import Image
from scipy import ndimage


def skin_mask(img):
    hsv = np.asarray(Image.fromarray(img.astype(np.uint8)).convert("HSV")).astype(np.float32)
    h, s, v = hsv[..., 0] * 360.0 / 255.0, hsv[..., 1] / 255.0, hsv[..., 2] / 255.0
    return ((h < 50) | (h > 330)) & (s > 0.14) & (v > 0.22)


def trim(body_path, item_path):
    body = np.asarray(Image.open(body_path).convert("RGBA")).astype(np.float32)
    skin = skin_mask(body[..., :3]) & (body[..., 3] > 200)
    skin = ndimage.binary_dilation(skin, iterations=2)
    im = np.asarray(Image.open(item_path).convert("RGBA")).astype(np.float32)
    a = im[..., 3]
    inside = a >= 128
    dist = ndimage.distance_transform_edt(inside)
    ramp = np.clip((dist - 0.5) / 2.5, 0.0, 1.0)
    # Край, который граничит именно с кожей: прозрачная сторона границы — на коже тела.
    near_skin = ndimage.binary_dilation(~inside & skin, iterations=4)
    zone = inside & near_skin & (dist <= 3.0)
    a2 = a.copy()
    a2[zone] = a[zone] * ramp[zone]
    soft = (a > 0) & ~inside & near_skin
    a2[soft] = a[soft] * 0.35
    im[..., 3] = a2
    im[im[..., 3] < 1, :3] = 0
    Image.fromarray(np.clip(im, 0, 255).astype(np.uint8), "RGBA").save(item_path, optimize=True)
    return int(zone.sum())


if __name__ == "__main__":
    for p in sys.argv[2:]:
        print(p, trim(sys.argv[1], p))
