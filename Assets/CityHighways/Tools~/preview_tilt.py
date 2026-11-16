# Превью наклона поля (2.5D): та же перспектива, что в BoardLayout.ToScreen, — проверка, что карта
# помещается в окно, доска и торец на месте, а зоны и тени ложатся на землю.
# Запуск из Tools~: python3 preview_tilt.py [tilt 0..1] [час] [out.png]
import json, math, os, sys
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
m = json.load(open(os.path.join(HERE, "../Resources/CityHighways/Maps/moscow.json"), encoding="utf-8"))
TILT = float(sys.argv[1]) if len(sys.argv) > 1 else 1.0
HOUR = float(sys.argv[2]) if len(sys.argv) > 2 else 14.0
OUT = sys.argv[3] if len(sys.argv) > 3 else "/tmp/tilt.png"

W, H, PAD = 1520, 880, 50
FAR_SHRINK, FULL_SQUASH, SLAB, INSET, MARGIN = 0.14, 0.72, 16.0, 6.0, 0.12
L, R, T, B = PAD, W - PAD, PAD, H - PAD
w0 = 1 - FAR_SHRINK * TILT
K = 1 / w0 - 1
flat = TILT < 0.001

def depth(y): return 1.0 if flat else 1 / (1 + K * (1 - y))

def ts(x, y):
    if flat: return (L + x * (R - L), T + y * (B - T))
    w = depth(y); ny = (w - w0) / (1 - w0)
    return ((L + R) / 2 + (x - 0.5) * (R - L) * w, T + ny * (B - T))

def map_y(sy):
    ny = (sy - T) / (B - T)
    if flat: return ny
    w = max(0.05, w0 + ny * (1 - w0))
    return 1 - (1 / w - 1) / K

def scale_at(sy):
    if flat: return 1.0
    ny = (sy - T) / (B - T)
    return min(1.15, max(0.6, w0 + ny * (1 - w0)))

squash = 1 + (FULL_SQUASH - 1) * TILT
inset = INSET * TILT
y0 = max(-MARGIN, map_y(inset)); y1 = min(1 + MARGIN, map_y(H - inset - SLAB * TILT))
half = (W / 2 - inset) / ((R - L) * depth(y1))
x0 = max(-MARGIN, 0.5 - half); x1 = min(1 + MARGIN, 0.5 + half)

img = Image.new("RGB", (W, H), (18, 19, 23))
d = ImageDraw.Draw(img, "RGBA")
tl, tr, br, bl = ts(x0, y0), ts(x1, y0), ts(x1, y1), ts(x0, y1)
depth_px = SLAB * TILT
d.polygon([tl, tr, br, bl], fill=(52, 84, 46))
if TILT > 0.01:
    d.polygon([bl, br, (br[0], br[1] + depth_px * 0.45), (bl[0], bl[1] + depth_px * 0.45)], fill=(84, 64, 41))
    d.polygon([(bl[0], bl[1] + depth_px * 0.45), (br[0], br[1] + depth_px * 0.45), (br[0], br[1] + depth_px), (bl[0], bl[1] + depth_px)], fill=(54, 38, 26))
    d.line([tl, tr, br, bl, tl], fill=(0, 0, 0, 128), width=2)

closed = {dd["id"] for dd in m["districts"] if dd.get("expansion")}
locs = {l["id"]: l for l in m["locations"] if l["district"] not in closed}
pos = {k: ts(l["x"], l["y"]) for k, l in locs.items()}

# МКАД
ring = [ts(0.5 + math.cos(a) * 0.40, 0.5 + math.sin(a) * 0.49) for a in [i * 2 * math.pi / 200 for i in range(201)]]
d.line(ring, fill=(70, 72, 77), width=9)
# река
river = [ts(p["x"], p["y"]) for p in m["river"]]
d.line(river, fill=(41, 92, 153), width=24)
# зоны районов — эллипсы на земле
rad = max(32, H * 0.045)
for k, l in locs.items():
    x, y = pos[k]; rx = rad * scale_at(y); ry = rx * squash
    d.ellipse([x - rx, y - ry, x + rx, y + ry], fill=(255, 255, 255, 22), outline=(255, 255, 255, 90))
# дороги
for r in m["roads"]:
    if r["from"] in pos and r["to"] in pos:
        d.line([pos[r["from"]], pos[r["to"]]], fill=(140, 150, 170), width=9)
# солнце и тени
alt = math.sin(math.pi * (HOUR - 5) / 16); sunx = math.cos(math.pi * (HOUR - 5) / 16)
ln = 2.4 + (0.8 - 2.4) * max(0, min(1, alt))
sdx, sdy = -sunx, 0.45; n = math.hypot(sdx, sdy); sdx, sdy = sdx / n * ln, sdy / n * ln
for k in pos:
    x, y = pos[k]; th = 3 * TILT; rr = 11.5
    d.ellipse([x - rr + sdx * (th + 2), y + th - rr * squash + sdy * (th + 2), x + rr + sdx * (th + 2), y + th + rr * squash + sdy * (th + 2)], fill=(0, 0, 0, 70))
    d.rectangle([x - rr, y, x + rr, y + th], fill=(77, 82, 92))
    d.ellipse([x - rr, y + th - rr, x + rr, y + th + rr], fill=(77, 82, 92))
    d.ellipse([x - rr, y - rr, x + rr, y + rr], fill=(235, 240, 250))
    d.ellipse([x - 9, y - 9, x + 9, y + 9], fill=(18, 20, 31))
img.save(OUT)
xs = [p[0] for p in pos.values()]; ys = [p[1] for p in pos.values()]
print(f"tilt={TILT} board x[{x0:.3f},{x1:.3f}] y[{y0:.3f},{y1:.3f}]  места x {min(xs):.0f}..{max(xs):.0f} y {min(ys):.0f}..{max(ys):.0f} из {W}x{H}")
