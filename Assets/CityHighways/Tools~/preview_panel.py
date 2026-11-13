from PIL import Image, ImageDraw, ImageFont
W,H=400,990; S=2
img=Image.new('RGB',(W*S,H*S),(30,33,41)); d=ImageDraw.Draw(img)
F="/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"; FB="/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
def f(sz,b=False): return ImageFont.truetype(FB if b else F,int(sz*S))
def T(x,y,t,sz=12,c=(225,228,235),b=False): d.text((x*S,y*S),t,font=f(sz,b),fill=c)
def R(x,y,w,h,fill,rad=8,outline=None,width=1): d.rounded_rectangle([x*S,y*S,(x+w)*S,(y+h)*S],radius=rad*S,fill=fill,outline=outline,width=width*S)
dim=(140,146,160); panel=(41,45,56); acc=(240,184,72)
x0=16; y=16
T(x0,y,"Ход 7",20,b=True); R(318,y-2,64,30,None,8,(70,75,90)); T(332,y+5,"Меню",13); y+=40
R(x0,y,368,46,panel); d.ellipse([(x0+10)*S,(y+10)*S,(x0+20)*S,(y+20)*S],fill=(204,128,255)); T(x0+28,y+6,"Парад",14,(204,128,255),True)
T(x0+10,y+26,"Район «Центр» перекрыт: нельзя строить дороги, которые его касаются.",10.5,dim); y+=56
players=[("Поля · Везунчик · бот","4 очк · 21 уч · 0 карт",(232,66,61)),("Борис · Прораб · бот","0 очк · 25 уч · 4 карт",(41,168,92)),("Аня · Чиновник · бот","0 очк · 25 уч · 6 карт",(143,82,209)),("Вера · Страховщик · бот","0 очк · 25 уч · 4 карт",(40,170,200)),("Глеб · Сборщик пошлин · бот","1 очк · 24 уч · 3 карт",(220,90,140))]
for i,(n,st,c) in enumerate(players):
    active=i==1; h=46 if active else 30
    R(x0,y,368,h,(52,57,70) if active else panel,8,c if active else (60,64,78),2 if active else 1)
    d.ellipse([(x0+8)*S,(y+4)*S,(x0+30)*S,(y+26)*S],outline=c,width=2*S,fill=(25,27,36))
    T(x0+38,y+7,n,13,b=True); tw=d.textlength(st,font=f(11.5))/S; T(x0+360-tw,y+8,st,11.5,dim)
    if active: T(x0+38,y+27,"Раз за партию строит дорогу длиной до 2 без карт. Осталось: 1.",10.5,dim)
    y+=h+4
y+=6
cards=[("Техника",(196,150,40)),("Асфальт",(58,62,72)),("Щебень",(150,112,70)),("Щебень",(150,112,70)),("Асфальт",(58,62,72))]
for i,(n,c) in enumerate(cards):
    R(x0+i*75,y,68,86,c,8); T(x0+i*75+6,y+6,n,10.5,(240,240,240),True)
y+=94
for i,t in enumerate(["Колода ×2","Удача","Контракты","Сделка"]):
    R(x0+i*93,y,87,34,(58,63,78),8); tw=d.textlength(t,font=f(12.5))/S; T(x0+i*93+43.5-tw/2,y+9,t,12.5)
y+=40; T(x0,y,"В колоде 65 · контрактов 31 · удача 18: шанс бафа 61%",10.5,dim); y+=26
tabs=["Контракты · 4","Цели · 3","Журнал"]
for i,t in enumerate(tabs):
    tw=d.textlength(t,font=f(13))/S; cx=x0+i*123+61
    T(cx-tw/2,y,t,13,acc if i==0 else dim,i==0)
    if i==0: d.rectangle([(x0+i*123+10)*S,(y+22)*S,(x0+i*123+112)*S,(y+24)*S],fill=acc)
d.line([(x0*S,(y+24)*S),((x0+368)*S,(y+24)*S)],fill=(60,64,78),width=S); y+=34
cons=[("Аэропорты","Центр","",13),("Северо-Запад","Юго-Восток","",8),("Москва-Сити","Замоскворечье","",5),("Москва-Сити","Юго-Запад","Север",10)]
for a,b,c3,p in cons:
    h=44 if c3 else 32
    R(x0,y,368,h,panel,8)
    d.ellipse([(x0+10)*S,(y+(h-14)/2)*S,(x0+24)*S,(y+(h+14)/2)*S],outline=dim,width=2*S)
    yy=y+8
    if c3: T(x0+34,y+4,"УЗЕЛ",9.5,(184,122,255),True); yy=y+20
    xx=x0+34
    for name,col,sep in ((a,(240,200,60)," — "),(b,(240,60,70)," и " if c3 else ""),(c3,(184,122,255),"")):
        if not name: continue
        d.ellipse([xx*S,(yy+4)*S,(xx+8)*S,(yy+12)*S],fill=col); xx+=11; T(xx,yy,name,12.5); xx+=d.textlength(name,font=f(12.5))/S
        if sep: T(xx,yy,sep,12.5,dim); xx+=d.textlength(sep,font=f(12.5))/S
    T(x0+345,y+(h-16)/2,str(p),14,acc,True); y+=h+5
img=img.resize((W,H),Image.LANCZOS); img.save('panel_mockup.png'); print('ok')
