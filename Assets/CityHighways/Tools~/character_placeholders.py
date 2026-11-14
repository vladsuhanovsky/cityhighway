# Заглушки одежды персонажа (плоский мульт-стиль) + catalog.json + шаблон для художника.
# Все слои — холст 512×768 с прозрачным фоном, фигура стоит по центру, ноги внизу.
# Замена: положите свою PNG того же размера с тем же именем файла — игра подхватит её без правки кода.
import json, os
from PIL import Image, ImageDraw
W,H=512,768
OUT=os.path.join(os.path.dirname(os.path.abspath(__file__)),'..','Resources','CityHighways','Character')
INK=(30,28,36,255); O=5   # контур
def canvas(): return Image.new('RGBA',(W,H),(0,0,0,0))
def save(img, path):
    full=os.path.join(OUT,path+'.png'); os.makedirs(os.path.dirname(full),exist_ok=True); img.save(full,optimize=True)
def poly(d,pts,fill): d.polygon(pts,fill=fill,outline=INK,width=O)
def ell(d,box,fill): d.ellipse(box,fill=fill,outline=INK,width=O)
def rect(d,box,fill,r=18): d.rounded_rectangle(box,radius=r,fill=fill,outline=INK,width=O)
def shade(c,k): return tuple(max(0,min(255,int(v*k))) for v in c[:3])+(255,)

# Геометрия фигуры (общая для всех слоёв)
HEAD=(186,90,326,240); NECK=(236,228,276,268)
TORSO=[(170,262),(342,262),(352,470),(160,470)]
ARML=[(170,270),(128,300),(112,440),(146,446),(162,330)]; ARMR=[(342,270),(384,300),(400,440),(366,446),(350,330)]
HANDL=(104,430,154,476); HANDR=(358,430,408,476)
LEGL=(170,462,252,700); LEGR=(260,462,342,700)
FOOTL=(146,684,254,734); FOOTR=(258,684,366,734)

items=[]
def item(id,slot,name,price,draw):
    img=canvas(); d=ImageDraw.Draw(img); draw(d); save(img,f'{slot}/{id}'); items.append(dict(id=id,slot=slot,name=name,price=price,file=f'{slot}/{id}'))

# --- Тело (кожа + лицо): бесплатно
def body(skin):
    def f(d):
        for a in (ARML,ARMR): poly(d,a,skin)
        ell(d,HANDL,skin); ell(d,HANDR,skin)
        rect(d,(172,462,250,700),skin,30); rect(d,(262,462,340,700),skin,30)
        poly(d,TORSO,shade(skin,0.95)); rect(d,NECK,skin,8)
        ell(d,HEAD,skin)
        ell(d,(176,150,200,185),skin); ell(d,(312,150,336,185),skin)   # уши
        d.ellipse((222,152,240,172),fill=INK); d.ellipse((272,152,290,172),fill=INK)   # глаза
        d.arc((228,170,284,212),20,160,fill=INK,width=5)   # улыбка
        d.ellipse((208,176,228,190),fill=(240,140,140,120)); d.ellipse((284,176,304,190),fill=(240,140,140,120))
    return f
item('body_light','body','Светлая кожа',0,body((246,214,186)))
item('body_tan','body','Смуглая кожа',0,body((212,160,118)))
item('body_dark','body','Тёмная кожа',0,body((140,94,64)))

# --- Причёски
def hair(kind,color):
    def f(d):
        if kind=='short':
            d.chord((180,80,332,210),180,360,fill=color,outline=INK,width=O)
            poly(d,[(186,150),(200,112),(230,128)],color)
        elif kind=='long':
            rect(d,(176,110,206,300),color,14); rect(d,(306,110,336,300),color,14)
            d.chord((176,76,336,214),180,360,fill=color,outline=INK,width=O)
        elif kind=='bun':
            ell(d,(226,40,286,100),color)
            d.chord((182,82,330,206),180,360,fill=color,outline=INK,width=O)
        elif kind=='beard':
            d.chord((180,80,332,210),180,360,fill=color,outline=INK,width=O)
            d.chord((196,150,316,260),0,180,fill=color,outline=INK,width=O)
            d.arc((228,176,284,212),20,160,fill=INK,width=5)
    return f
item('hair_short','hair','Короткая стрижка',0,hair('short',(72,48,34)))
item('hair_long','hair','Длинные волосы',0,hair('long',(120,76,40)))
item('hair_bun','hair','Пучок',10,hair('bun',(40,32,30)))
item('hair_beard','hair','Борода прораба',15,hair('beard',(96,60,38)))

# --- Верх
def top(kind):
    def f(d):
        if kind=='tshirt':
            c=(70,110,170,255); poly(d,[(164,262),(348,262),(392,330),(358,348),(346,320),(350,470),(162,470),(166,320),(154,348),(120,330)],c)
        elif kind=='vest':
            c=(70,110,170,255); poly(d,[(164,262),(348,262),(392,330),(358,348),(346,320),(350,470),(162,470),(166,320),(154,348),(120,330)],c)
            o=(255,140,30,255); poly(d,[(170,266),(236,266),(246,470),(162,470)],o); poly(d,[(276,266),(342,266),(350,470),(266,470)],o)
            for y in (360,420): d.rectangle((164,y,248,y+16),fill=(225,225,215,255)); d.rectangle((264,y,348,y+16),fill=(225,225,215,255))
        elif kind=='shirt':
            c=(236,238,242,255); poly(d,[(164,262),(348,262),(396,300),(404,440),(366,446),(350,330),(350,470),(162,470),(162,330),(146,446),(108,440),(116,300)],c)
            poly(d,[(226,262),(256,300),(286,262)],(250,250,250,255)); d.line((256,300,256,470),fill=INK,width=3)
            for y in (330,370,410,450): d.ellipse((251,y,261,y+10),fill=INK)
        elif kind=='sweater':
            c=(150,40,50,255); poly(d,[(164,262),(348,262),(396,300),(404,440),(366,446),(350,330),(350,470),(162,470),(162,330),(146,446),(108,440),(116,300)],c)
            for y in range(300,460,26): d.line((172,y,340,y),fill=shade(c,0.8),width=4)
            rect(d,(226,254,286,280),shade(c,0.85),10)
        elif kind=='jacket':
            c=(52,58,74,255); poly(d,[(162,262),(350,262),(398,300),(406,444),(366,450),(350,330),(352,476),(160,476),(162,330),(146,450),(106,444),(114,300)],c)
            poly(d,[(226,262),(256,330),(286,262)],(236,238,242,255))
            poly(d,[(206,262),(256,380),(226,262)],shade(c,1.25)); poly(d,[(306,262),(256,380),(286,262)],shade(c,1.25))
            for y in (400,440): d.ellipse((250,y,262,y+12),fill=(200,170,80,255))
    return f
item('top_tshirt','top','Футболка',0,top('tshirt'))
item('top_vest','top','Сигнальный жилет',10,top('vest'))
item('top_shirt','top','Рубашка',15,top('shirt'))
item('top_sweater','top','Свитер',15,top('sweater'))
item('top_jacket','top','Пиджак',30,top('jacket'))

# --- Низ
def bottom(kind):
    def f(d):
        c={'trousers':(70,70,82,255),'jeans':(60,90,150,255),'overalls':(220,120,30,255),'classic':(36,38,48,255)}[kind]
        poly(d,[(160,462),(352,462),(346,700),(262,700),(256,520),(250,700),(166,700)],c)
        if kind=='jeans':
            d.line((210,480,210,690),fill=shade(c,1.3),width=3); d.line((302,480,302,690),fill=shade(c,1.3),width=3)
        if kind=='overalls':
            rect(d,(196,330,316,470),c,10); d.line((200,270,210,340),fill=INK,width=12); d.line((312,270,302,340),fill=INK,width=12)
            rect(d,(230,360,282,400),shade(c,0.85),6)
        if kind=='classic': d.line((208,480,208,696),fill=shade(c,1.6),width=2); d.line((304,480,304,696),fill=shade(c,1.6),width=2)
    return f
item('bottom_trousers','bottom','Рабочие брюки',0,bottom('trousers'))
item('bottom_jeans','bottom','Джинсы',10,bottom('jeans'))
item('bottom_overalls','bottom','Комбинезон',20,bottom('overalls'))
item('bottom_classic','bottom','Классические брюки',15,bottom('classic'))

# --- Обувь
def shoes(kind):
    def f(d):
        c={'boots':(110,72,40,255),'sneakers':(240,240,240,255),'rubber':(40,120,60,255),'oxford':(30,24,22,255)}[kind]
        if kind=='rubber': rect(d,(166,600,252,734),c,16); rect(d,(260,600,346,734),c,16)
        else: rect(d,FOOTL,c,20); rect(d,FOOTR,c,20)
        if kind=='sneakers':
            d.line((150,718,252,718),fill=(220,60,60,255),width=8); d.line((260,718,362,718),fill=(220,60,60,255),width=8)
        if kind=='oxford': d.line((176,700,236,700),fill=shade(c,3),width=2); d.line((276,700,336,700),fill=shade(c,3),width=2)
    return f
item('shoes_boots','shoes','Рабочие ботинки',0,shoes('boots'))
item('shoes_sneakers','shoes','Кроссовки',10,shoes('sneakers'))
item('shoes_rubber','shoes','Резиновые сапоги',15,shoes('rubber'))
item('shoes_oxford','shoes','Туфли',20,shoes('oxford'))

# --- Головные уборы
def hat(kind):
    def f(d):
        if kind=='hardhat':
            c=(250,200,30,255); d.chord((172,46,340,206),180,360,fill=c,outline=INK,width=O); rect(d,(160,118,352,140),c,10)
            d.line((256,50,256,124),fill=shade(c,0.8),width=8)
        elif kind=='cap':
            c=(200,40,40,255); d.chord((180,60,332,196),180,360,fill=c,outline=INK,width=O); poly(d,[(250,120),(340,118),(352,136),(250,138)],shade(c,0.85))
        elif kind=='fedora':
            c=(90,70,54,255); ell(d,(150,108,362,150),c); rect(d,(196,40,316,132),c,26); d.rectangle((198,104,314,120),fill=(30,30,30,255))
        elif kind=='tophat':
            c=(26,26,32,255); ell(d,(160,112,352,146),c); rect(d,(200,0,312,128),c,10); d.rectangle((202,92,310,110),fill=(170,30,40,255))
    return f
item('hat_hardhat','hat','Строительная каска',10,hat('hardhat'))
item('hat_cap','hat','Кепка',10,hat('cap'))
item('hat_fedora','hat','Шляпа',20,hat('fedora'))
item('hat_tophat','hat','Цилиндр',35,hat('tophat'))

# --- Аксессуары
def acc(kind):
    def f(d):
        if kind=='glasses':
            d.ellipse((210,144,250,180),outline=INK,width=6); d.ellipse((262,144,302,180),outline=INK,width=6); d.line((250,160,262,160),fill=INK,width=5)
        elif kind=='tie':
            poly(d,[(246,268),(266,268),(272,290),(264,410),(256,428),(248,410),(240,290)],(170,30,40,255))
        elif kind=='radio':
            rect(d,(368,380,412,470),(40,44,52,255),8); d.line((402,382,410,330),fill=INK,width=6); d.rectangle((376,392,404,416),fill=(120,200,120,255))
        elif kind=='blueprint':
            rect(d,(86,380,128,500),(70,130,210,255),18); d.line((96,400,118,400),fill=(230,240,255,255),width=3); d.line((96,420,118,420),fill=(230,240,255,255),width=3)
        elif kind=='chain':
            for i in range(9):
                x=212+i*11; y=272+int(28*(1-((i-4)/4)**2))
                d.ellipse((x-6,y-6,x+6,y+6),outline=(240,200,60,255),width=4)
    return f
item('acc_glasses','accessory','Очки',10,acc('glasses'))
item('acc_tie','accessory','Галстук',15,acc('tie'))
item('acc_radio','accessory','Рация',20,acc('radio'))
item('acc_blueprint','accessory','Свиток чертежей',20,acc('blueprint'))
item('acc_chain','accessory','Золотая цепь',40,acc('chain'))

catalog=dict(
    canvas=[W,H],
    note="Все PNG — 512×768, прозрачный фон, фигура как в template.png. Слои рисуются по порядку order. price 0 — есть у всех.",
    order=['body','bottom','shoes','top','hair','accessory','hat'],
    defaults=dict(body='body_light',hair='hair_short',top='top_tshirt',bottom='bottom_trousers',shoes='shoes_boots',hat='',accessory=''),
    items=items)
json.dump(catalog,open(os.path.join(OUT,'catalog.json'),'w',encoding='utf-8'),ensure_ascii=False,indent=1)

# Шаблон для художника: силуэт + зоны слоёв.
t=Image.new('RGBA',(W,H),(255,255,255,255)); d=ImageDraw.Draw(t)
for f in (body((235,235,235)),): f(d)
zones=[('hat (головной убор)',(150,0,362,150),(250,200,30)),('hair (причёска)',(176,40,336,300),(120,76,40)),
       ('top (верх)',(104,254,408,480),(70,110,170)),('bottom (низ)',(156,456,356,704),(70,70,82)),('shoes (обувь)',(140,600,372,740),(110,72,40))]
for name,box,c in zones:
    d.rectangle(box,outline=c+(255,),width=3); d.text((box[0]+6,box[1]+4),name,fill=c+(255,))
d.text((10,748),"512x768, ноги стоят на y=734, центр по x=256",fill=(0,0,0,255))
t.save(os.path.join(os.path.dirname(os.path.abspath(__file__)),'character_template.png'))
print(len(items),'items')
