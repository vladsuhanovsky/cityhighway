from PIL import Image, ImageDraw, ImageFont, ImageFilter
import math, random
W,H=300,408; S=3
img=Image.new('RGB',(W*S,H*S)); d=ImageDraw.Draw(img)
# фон: вертикальный градиент как у карт «Асфальт»/«Бетон»
for y in range(H*S):
    t=y/(H*S); c=(int(46-10*t),int(50-10*t),int(62-10*t)); d.line([(0,y),(W*S,y)],fill=c)
# диагональные светлые полосы
stripes=Image.new('RGBA',img.size,(0,0,0,0)); sd=ImageDraw.Draw(stripes)
for k,(off,w,a) in enumerate([(120,26,22),(190,14,16),(520,30,18),(600,12,14)]):
    x0=off*S; sd.polygon([(x0,H*S),(x0+w*S,H*S),(x0+w*S+H*S*0.75,0),(x0+H*S*0.75,0)],fill=(150,170,200,a))
img=Image.alpha_composite(img.convert('RGBA'),stripes)
d=ImageDraw.Draw(img)
# лёгкий шум
rng=random.Random(5)
for _ in range(9000):
    x,y=rng.randrange(W*S),rng.randrange(H*S); v=rng.randint(-12,12)
    p=img.getpixel((x,y)); d.point((x,y),fill=(max(0,p[0]+v),max(0,p[1]+v),max(0,p[2]+v),255))
# голубая кромка сверху (как цветной верх у остальных карт)
top=Image.new('RGBA',img.size,(0,0,0,0)); td=ImageDraw.Draw(top)
for y in range(16*S):
    a=int(255*(1-y/(16*S))**1.5); td.line([(0,y),(W*S,y)],fill=(107,173,255,a))
img=Image.alpha_composite(img,top); d=ImageDraw.Draw(img)
# заголовок
font=ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",40*S)
t="Сталь"; tw=d.textlength(t,font=font)
d.text(((W*S-tw)/2+2*S,44*S+3*S),t,font=font,fill=(0,0,0,140))
d.text(((W*S-tw)/2,44*S),t,font=font,fill=(245,247,250,255))
# двутавровая балка в лёгкой изометрии: торец (I-профиль) + длинная боковая грань
def beam(ox,oy,L,sc):
    # профиль I: ширина полки f, высота h, толщины
    f,h,tf,tw_=46*sc,52*sc,10*sc,10*sc
    dx,dy=L*0.86,-L*0.30   # направление вдоль балки (вправо-вверх)
    def P(x,y): return (ox+x,oy+y)
    prof=[(0,0),(f,0),(f,tf),((f+tw_)/2,tf),((f+tw_)/2,h-tf),(f,h-tf),(f,h),(0,h),(0,h-tf),((f-tw_)/2,h-tf),((f-tw_)/2,tf),(0,tf)]
    # верхняя грань верхней полки
    d.polygon([P(0,0),P(f,0),P(f+dx,dy),P(dx,dy)],fill=(196,205,218,255))
    # боковая грань полок и стенки (видна правая сторона)
    d.polygon([P(f,0),P(f+dx,dy),P(f+dx,dy+tf),P(f,tf)],fill=(120,132,150,255))
    d.polygon([P((f+tw_)/2,tf),P((f+tw_)/2+dx,dy+tf),P((f+tw_)/2+dx,dy+h-tf),P((f+tw_)/2,h-tf)],fill=(92,104,122,255))
    d.polygon([P(f,h-tf),P(f+dx,dy+h-tf),P(f+dx,dy+h),P(f,h)],fill=(108,120,138,255))
    # верх нижней полки (свет)
    d.polygon([P((f+tw_)/2,h-tf),P((f+tw_)/2+dx,dy+h-tf),P(f+dx,dy+h-tf),P(f,h-tf)],fill=(170,180,196,255))
    # торец
    d.polygon([P(x,y) for x,y in prof],fill=(150,162,180,255),outline=(60,68,82,255))
    # блик на торце
    d.line([P(2*sc,2*sc),P(f-2*sc,2*sc)],fill=(235,240,248,255),width=int(2*sc))
    # заклёпки/отверстия на стенке сбоку
    for k in range(1,4):
        cx,cy=P((f+tw_)/2+dx*k/4,dy*k/4+h/2)
        d.ellipse([cx-3*sc,cy-3*sc,cx+3*sc,cy+3*sc],fill=(55,62,75,255))
sc=S*1.4
# тень под стопкой
sh=Image.new('RGBA',img.size,(0,0,0,0)); shd=ImageDraw.Draw(sh)
shd.ellipse([30*S,318*S,285*S,372*S],fill=(0,0,0,150)); sh=sh.filter(ImageFilter.GaussianBlur(10*S))
img=Image.alpha_composite(img,sh); d=ImageDraw.Draw(img)
beam(22*S,262*S,165*S,sc); beam(100*S,232*S,165*S,sc)   # нижний ряд (дальняя рисуется раньше)
beam(62*S,178*S,165*S,sc)                                # верхняя балка
img=img.resize((W,H),Image.LANCZOS).convert('RGB'); img.save('steel.png'); print('ok')
