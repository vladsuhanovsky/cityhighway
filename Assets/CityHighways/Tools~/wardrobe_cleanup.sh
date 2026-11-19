#!/bin/sh
# Полная чистка слоёв гардероба после нарезки (wardrobe_extract.py): шляпы, край одежды у кожи, розовая кайма,
# предметы в руке. Запускать из папки Assets/CityHighways на свежих слоях (повторный запуск по уже чищенным —
# шляпы чистить не нужно, остальное безвредно).
set -e
C=Resources/CityHighways/Character
B=Tools~/wardrobe_bases
T=Tools~
for f in hat_cap hat_hardhat; do python3 $T/wardrobe_fix_hats.py $B/base_m.jpg $C/hat/$f.png; done
python3 $T/wardrobe_fix_hats.py $B/base_m.jpg $C/hat/hat_tophat.png --keep-skin
for f in hat_fedora hat_fedora_tan hat_fedora_dark; do python3 $T/wardrobe_fix_hats.py $B/base_m.jpg $C/hat/$f.png --keep-skin; done
for f in hat_f_cap hat_f_hardhat hat_f_beret; do python3 $T/wardrobe_fix_hats.py $B/base_f.jpg $C/hat/$f.png; done
python3 $T/wardrobe_fix_hats.py $B/base_f.jpg $C/hat/hat_f_sunhat.png --brim
python3 $T/wardrobe_trim_edges.py $C/body/body_light.png $(ls $C/top/top_[!f]*.png $C/bottom/bottom_[!f]*.png $C/shoes/shoes_[!f]*.png) > /dev/null
python3 $T/wardrobe_trim_edges.py $C/body/body_f_light.png $C/top/top_f_*.png $C/bottom/bottom_f_*.png $C/shoes/shoes_f_*.png > /dev/null
python3 $T/wardrobe_defringe.py $C/*/*.png > /dev/null
python3 $T/wardrobe_handheld.py $C
