import json, os, shutil
OUT = '/home/claude/cityhighway/Assets/CityHighways/Resources/CityHighways/Character'
M = [
 ('body_light','body','Светлая кожа',0,'light'),('body_tan','body','Смуглая кожа',0,'tan'),('body_dark','body','Тёмная кожа',0,'dark'),
 ('hair_short','hair','Короткая стрижка',0,None),('hair_long','hair','Длинные волосы',0,None),('hair_bun','hair','Пучок',10,None),('hair_beard','hair','Борода прораба',15,None),
 ('top_tshirt','top','Футболка',0,None),('top_vest','top','Сигнальный жилет',10,None),('top_shirt','top','Рубашка',15,None),('top_sweater','top','Свитер',15,None),('top_jacket','top','Пиджак',30,None),
 ('bottom_trousers','bottom','Рабочие брюки',0,None),('bottom_jeans','bottom','Джинсы',10,None),('bottom_classic','bottom','Классические брюки',15,None),('bottom_overalls','bottom','Комбинезон',20,None),
 ('shoes_boots','shoes','Рабочие ботинки',0,None),('shoes_sneakers','shoes','Кроссовки',10,None),('shoes_rubber','shoes','Резиновые сапоги',15,None),('shoes_oxford','shoes','Туфли',20,None),
 ('hat_hardhat','hat','Строительная каска',10,None),('hat_cap','hat','Кепка',10,None),('hat_fedora','hat','Шляпа',20,None),('hat_tophat','hat','Цилиндр',35,None),
 ('acc_glasses','accessory','Очки',10,None),('acc_tie','accessory','Галстук',15,None),('acc_radio','accessory','Рация',20,None),('acc_blueprint','accessory','Свиток чертежей',20,None),('acc_chain','accessory','Золотая цепь',40,None),
]
F = [
 ('body_f_light','body','Светлая кожа',0,'light'),('body_f_tan','body','Смуглая кожа',0,'tan'),('body_f_dark','body','Тёмная кожа',0,'dark'),
 ('hair_f_bob','hair','Каре',0,None),('hair_f_long','hair','Длинные волосы',0,None),('hair_f_ponytail','hair','Высокий хвост',10,None),('hair_f_curly','hair','Кудри',15,None),
 ('top_f_tshirt','top','Футболка',0,None),('top_f_vest','top','Сигнальный жилет',10,None),('top_f_blouse','top','Блузка',15,None),('top_f_cardigan','top','Кардиган',15,None),('top_f_blazer','top','Жакет',30,None),
 ('bottom_f_trousers','bottom','Рабочие брюки',0,None),('bottom_f_jeans','bottom','Джинсы',10,None),('bottom_f_skirt','bottom','Юбка',15,None),('bottom_f_overalls','bottom','Комбинезон',20,None),
 ('shoes_f_boots','shoes','Рабочие ботинки',0,None),('shoes_f_sneakers','shoes','Кроссовки',10,None),('shoes_f_rubber','shoes','Резиновые сапоги',15,None),('shoes_f_heels','shoes','Туфли на каблуке',20,None),
 ('hat_f_hardhat','hat','Строительная каска',10,None),('hat_f_cap','hat','Кепка',10,None),('hat_f_beret','hat','Берет',20,None),('hat_f_sunhat','hat','Шляпка',35,None),
 ('acc_f_glasses','accessory','Очки',10,None),('acc_f_scarf','accessory','Шейный платок',15,None),('acc_f_radio','accessory','Рация',20,None),('acc_f_tablet','accessory','Планшет',20,None),('acc_f_pearls','accessory','Жемчужное ожерелье',40,None),
]
items=[]; copied=0
for gender, table in (('m',M),('f',F)):
    for id_, slot, name, price, tone in table:
        it={'id':id_,'slot':slot,'name':name,'price':price,'file':f'{slot}/{id_}','gender':gender}
        if tone: it['tone']=tone
        items.append(it)
        os.makedirs(f'{OUT}/{slot}', exist_ok=True)
        for suffix in ('','_tan','_dark'):
            src=f'out/{id_}{suffix}.png'
            if os.path.exists(src):
                shutil.copy(src, f'{OUT}/{slot}/{id_}{suffix}.png'); copied+=1
            elif suffix=='' : print('MISSING', src)
cat={
 'canvas':[512,768],
 'note':'Все PNG — 512×768, прозрачный фон, фигура как у базы своего пола. Слои рисуются по порядку order. price 0 — есть у всех. gender: m — мужское, f — женское. У внешности tone — тон кожи; у вещей рядом лежат варианты <файл>_tan.png и <файл>_dark.png. Нарезка — Tools~/wardrobe_extract.py.',
 'order':['body','bottom','shoes','top','hair','accessory','hat'],
 'defaults':{'body':'body_light','hair':'hair_short','top':'top_tshirt','bottom':'bottom_trousers','shoes':'shoes_boots','hat':'','accessory':''},
 'defaultsFemale':{'body':'body_f_light','hair':'hair_f_bob','top':'top_f_tshirt','bottom':'bottom_f_trousers','shoes':'shoes_f_boots','hat':'','accessory':''},
 'items':items}
json.dump(cat, open(f'{OUT}/catalog.json','w',encoding='utf-8'), ensure_ascii=False, indent=1)
print('items',len(items),'pngs',copied)
