"""
Данные слоёв гардероба для CharacterLayers.cs — дописываются в catalog.json (скрипт можно запускать повторно):
  bounds  — рамка непрозрачной части вещи на холсте 512×768 [x, y, ширина, высота] (сверху вниз). По ней
            анимация надевания крутится вокруг самой вещи, а «призрак» при перетаскивании показывает вещь крупно;
  overTop — низ рисуется поверх верха (комбинезон: нагрудник и лямки поверх футболки);
  outer   — верхняя одежда (жилет, пиджак, кардиган, жакет): её всё равно рисуют поверх комбинезона.
Заодно чистит шляпу-федору: в вырезе под лицо оставались обрывки бровей и переносицы («двойные брови»).
Запуск: python3 Tools~/wardrobe_layers.py
"""
import json, os
import numpy as np
from PIL import Image

ROOT = os.path.join(os.path.dirname(__file__), '..', 'Resources', 'CityHighways', 'Character')
OVER_TOP = {'bottom_overalls', 'bottom_f_overalls'}
OUTER = {'top_vest', 'top_jacket', 'top_f_vest', 'top_f_cardigan', 'top_f_blazer'}


def fix_fedora():
    for name in ('hat_fedora', 'hat_fedora_tan', 'hat_fedora_dark'):
        path = os.path.join(ROOT, 'hat', name + '.png')
        if not os.path.exists(path):
            continue
        im = np.array(Image.open(path).convert('RGBA'))
        im[55:90, 236:286, 3] = 0  # центр выреза под лентой: там должно быть лицо, а не обрывки
        Image.fromarray(im).save(path)


def bounds(file):
    a = np.array(Image.open(os.path.join(ROOT, file + '.png')).convert('RGBA'))[:, :, 3] > 16
    ys, xs = np.where(a)
    if len(xs) == 0:
        return [0, 0, 512, 768]
    return [int(xs.min()), int(ys.min()), int(xs.max() - xs.min() + 1), int(ys.max() - ys.min() + 1)]


def main():
    fix_fedora()
    path = os.path.join(ROOT, 'catalog.json')
    cat = json.load(open(path, encoding='utf-8'))
    for item in cat['items']:
        item['bounds'] = bounds(item['file'])
        item.pop('overTop', None)
        item.pop('outer', None)
        if item['id'] in OVER_TOP:
            item['overTop'] = True
        if item['id'] in OUTER:
            item['outer'] = True
    json.dump(cat, open(path, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('items:', len(cat['items']))


if __name__ == '__main__':
    main()
