# Порт TerrainTexture.cs (те же правила, тот же хеш-шум) + дневные дороги/места — только для превью.
import json, math, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter
ROOT='/home/claude/RailGame/Assets/CityHighways/'
m=json.load(open(ROOT+'Resources/CityHighways/Maps/moscow.json'))
W,H,PAD=1500,920,50
MARGIN=0.12   # текстура покрывает нормированные координаты [-MARGIN, 1+MARGIN]
def ts(x,y): return (PAD+x*(W-2*PAD), PAD+y*(H-2*PAD))
open_=[d['id'] for d in m['districts'] if not d.get('expansion')]
locs={l['id']:l for l in m['locations'] if l['district'] in open_}
PARKS={"sokolniki":0.045,"izmaylovo":0.055,"kolomenskoe":0.045,"mgu":0.04,"vdnh":0.04,"kuzminki":0.045,"luzhniki":0.03,
       "losiny":0.065,"tsaritsyno":0.045,"fili":0.03,"troparevo":0.04}

# ------------------------------------------------------------------ хеш и шум (как в C#)
def hsh(x,y,salt):
    h=((x*73856093)^(y*19349663)^(salt*83492791))&0xffffffff
    h^=h>>13; h=(h*0x5bd1e995)&0xffffffff; h^=h>>15
    return (h&0xFFFFFF)/0x1000000
def hgrid(ix,iy,salt):
    ix=ix.astype(np.int64); iy=iy.astype(np.int64)
    h=((ix*73856093)^(iy*19349663)^(salt*83492791))&0xffffffff
    h^=h>>13; h=(h*0x5bd1e995)&0xffffffff; h^=h>>15
    return (h&0xFFFFFF)/0x1000000
def vnoise(X,Y,cell,salt):
    gx=X/cell; gy=Y/cell; ix=np.floor(gx); iy=np.floor(gy); fx=gx-ix; fy=gy-iy
    fx=fx*fx*(3-2*fx); fy=fy*fy*(3-2*fy)
    a=hgrid(ix,iy,salt); b=hgrid(ix+1,iy,salt); c=hgrid(ix,iy+1,salt); d=hgrid(ix+1,iy+1,salt)
    return (a+(b-a)*fx)+((c+(d-c)*fx)-(a+(b-a)*fx))*fy
def fbm(X,Y,salt): return vnoise(X,Y,90,salt)*0.5+vnoise(X,Y,34,salt+1)*0.3+vnoise(X,Y,11,salt+2)*0.2

def bake(TW,TH):
    """Текстура TW×TH пикселей на область [-M,1+M]² в координатах карты; px — пиксели текстуры."""
    span=1+2*MARGIN
    def tp(x,y): return ((x+MARGIN)/span*TW,(y+MARGIN)/span*TH)
    sx=TW/span; sy=TH/span                      # пикселей на единицу карты
    Y,X=np.mgrid[0:TH,0:TW].astype(np.float64)
    nx=X/TW*span-MARGIN; ny=Y/TH*span-MARGIN
    # «Городскость»: 1 в центре, 0 за МКАД (эллипс 0.40×0.49 вокруг 0.5,0.5).
    e=np.sqrt(((nx-0.5)/0.40)**2+((ny-0.5)/0.49)**2)
    urban=np.clip((1.08-e)/0.55,0,1)
    n=fbm(X,Y,11)
    grassD=np.array([0.29,0.40,0.17]); grassL=np.array([0.45,0.55,0.25]); field=np.array([0.62,0.60,0.34])
    pave=np.array([0.50,0.50,0.46])
    t=n[...,None]
    img=grassD+(grassL-grassD)*t
    # Поля за городом: светлые пятна там, где шум высок.
    f=np.clip((vnoise(X,Y,70,21)-0.62)/0.08,0,1)*(1-urban)
    img=img+(field-img)*f[...,None]*0.8
    # Город: земля сереет к центру пятнами.
    g=np.clip(urban*0.85+(vnoise(X,Y,40,31)-0.5)*0.5,0,1)*0.75
    img=img+(pave-img)*g[...,None]
    img=img+(hgrid(X,Y,5)[...,None]-0.5)*0.04   # зерно
    im=Image.fromarray((np.clip(img,0,1)*255).astype(np.uint8),'RGB').convert('RGBA')
    d=ImageDraw.Draw(im,'RGBA')

    P={k:tp(l['x'],l['y']) for k,l in locs.items()}
    segs=[(P[r['from']],P[r['to']]) for r in m['roads'] if r['from'] in P and r['to'] in P]
    river=[tp(p['x'],p['y']) for p in m['river']]
    def dseg(px,py,a,b):
        ax,ay=a; bx,by=b; dx,dy=bx-ax,by-ay; t=max(0,min(1,((px-ax)*dx+(py-ay)*dy)/max(1e-4,dx*dx+dy*dy)))
        return math.hypot(px-(ax+dx*t),py-(ay+dy*t))
    def droad(x,y): return min(dseg(x,y,a,b) for a,b in segs)
    def driver(x,y): return min(dseg(x,y,river[i],river[i+1]) for i in range(len(river)-1))
    def dloc(x,y): return min(math.hypot(x-a,y-b) for a,b in P.values())
    def ering(x,y):
        mx=(x/TW*span-MARGIN-0.5)/0.40; my=(y/TH*span-MARGIN-0.5)/0.49; return math.hypot(mx,my)
    k=sy/ (H-2*PAD)   # пикселей текстуры на пиксель экрана (для размеров в «экранных» единицах)
    parks=[]
    for pid,rad in PARKS.items():
        if pid in P:
            cx,cy=P[pid]; r=rad*H*k   # радиус в долях высоты поля, как в MapView
            parks.append([(cx+r*.35,cy-r*.2,r),(cx-r*.4,cy+r*.25,r*.8),(cx+r*.1,cy+r*.45,r*.65)])
    def inpark(x,y,pad=0): return any(math.hypot(x-a,y-b)<rr+pad for blob in parks for a,b,rr in blob)

    # ---- Дома: крыши сверху, как плитки зданий в MegaMek.
    cell=13*k; roofs=[(0.60,0.60,0.58),(0.52,0.33,0.27),(0.46,0.40,0.33),(0.40,0.44,0.48),(0.66,0.62,0.52)]
    for iy in range(int(TH/cell)+1):
        for ix in range(int(TW/cell)+1):
            cx=(ix+.5+(hsh(ix,iy,4)-.5)*.15)*cell; cy=(iy+.5+(hsh(ix,iy,5)-.5)*.15)*cell
            e2=ering(cx,cy); u=max(0,min(1,(1.08-e2)/0.55))
            dens=min(1,u*1.1)+(0.05 if e2>1.08 else 0)
            if u>0.15 and (ix%4==0 or iy%3==0): continue      # улицы между кварталами
            if hsh(ix,iy,1)>dens: continue
            if e2>0.97 and e2<1.05: continue
            if droad(cx,cy)<13*k or dloc(cx,cy)<24*k or driver(cx,cy)<22*k or inpark(cx,cy,4*k): continue
            w=cell*(0.55+hsh(ix,iy,6)*0.42); h=cell*(0.55+hsh(ix,iy,7)*0.42)
            c=roofs[int(hsh(ix,iy,8)*len(roofs))]; v=0.9+hsh(ix,iy,9)*0.2; c=tuple(min(1,x*v) for x in c)
            x0,y0,x1,y1=cx-w/2,cy-h/2,cx+w/2,cy+h/2
            d.rectangle([x0+2*k,y0+2*k,x1+2*k,y1+2*k],fill=(10,15,5,95))
            d.rectangle([x0,y0,x1,y1],fill=tuple(int(x*255) for x in c)+(255,))
            d.line([(x0,y0),(x1,y0)],fill=tuple(int(min(1,x*1.25)*255) for x in c)+(255,),width=max(1,int(k)))
            d.line([(x0,y0),(x0,y1)],fill=tuple(int(min(1,x*1.18)*255) for x in c)+(255,),width=max(1,int(k)))
            d.line([(x0,y1),(x1,y1)],fill=tuple(int(x*0.7*255) for x in c)+(255,),width=max(1,int(k)))
            d.line([(x1,y0),(x1,y1)],fill=tuple(int(x*0.75*255) for x in c)+(255,),width=max(1,int(k)))
            if hsh(ix,iy,10)<0.45:   # конёк крыши
                if w>h: d.line([(x0+2*k,cy),(x1-2*k,cy)],fill=tuple(int(x*0.82*255) for x in c)+(255,),width=max(1,int(k)))
                else: d.line([(cx,y0+2*k),(cx,y1-2*k)],fill=tuple(int(x*0.82*255) for x in c)+(255,),width=max(1,int(k)))

    # ---- Леса: кроны деревьев с тенью и бликом (MegaMek woods).
    def tree(x,y,r,salt):
        d.ellipse([x-r+r*.35,y-r+r*.45,x+r+r*.35,y+r+r*.45],fill=(12,22,6,110))
        tone=0.85+hsh(int(x),int(y),salt)*0.3
        d.ellipse([x-r,y-r,x+r,y+r],fill=(int(40*tone),int(78*tone),int(30*tone),255))
        d.ellipse([x-r*.75,y-r*.8,x+r*.35,y+r*.3],fill=(int(62*tone),int(105*tone),int(40*tone),255))
        d.ellipse([x-r*.5,y-r*.55,x-r*.05,y-r*.1],fill=(int(88*tone),int(130*tone),int(56*tone),200))
    for pi,blob in enumerate(parks):
        for a,b,rr in blob: d.ellipse([a-rr,b-rr,a+rr,b+rr],fill=(38,60,22,170))
        pts=[]
        for j in range(1400):
            x=min(a-rr for a,b,rr in blob)+hsh(pi,j,40)*(max(a+rr for a,b,rr in blob)-min(a-rr for a,b,rr in blob))
            y=min(b-rr for a,b,rr in blob)+hsh(pi,j,41)*(max(b+rr for a,b,rr in blob)-min(b-rr for a,b,rr in blob))
            if not inpark(x,y,-3*k) or droad(x,y)<10*k or dloc(x,y)<16*k or driver(x,y)<16*k: continue
            pts.append((y,x,j))
        for y,x,j in sorted(pts): tree(x,y,(3.2+hsh(pi,j,42)*2.6)*k,43)
    # Лесополосы за МКАД.
    tc=11*k
    for iy in range(int(TH/tc)+1):
        for ix in range(int(TW/tc)+1):
            x=(ix+.5+(hsh(ix,iy,50)-.5)*.8)*tc; y=(iy+.5+(hsh(ix,iy,51)-.5)*.8)*tc
            e2=ering(x,y)
            if e2<1.06: continue
            wn=vnoise(np.array([x]),np.array([y]),60*k,52)[0]
            if wn<0.55 or hsh(ix,iy,53)>0.8: continue
            if droad(x,y)<12*k or dloc(x,y)<18*k or driver(x,y)<16*k: continue
            tree(x,y,(3+hsh(ix,iy,54)*2.5)*k,55)
    return im

# ------------------------------------------------------------------ сцена
S=2
span=1+2*MARGIN
TW=int((W-2*PAD)*span*S); TH=int((H-2*PAD)*span*S)
ter=bake(TW,TH)
x0,y0=ts(-MARGIN,-MARGIN)
img=Image.new('RGBA',(W*S,H*S),(74,98,44,255))
img.paste(ter,(int(x0*S),int(y0*S)))
d=ImageDraw.Draw(img,'RGBA')
pos={k:(ts(l['x'],l['y'])[0]*S,ts(l['x'],l['y'])[1]*S) for k,l in locs.items()}
def C(r,g,b,a=1): return (int(r*255),int(g*255),int(b*255),int(a*255))
pal=[(0.95,0.55,0.35),(0.45,0.75,0.95),(0.65,0.85,0.45),(0.95,0.80,0.40),(0.80,0.55,0.90),(0.40,0.85,0.80),(0.95,0.60,0.70)]
didx={dd['id']:i for i,dd in enumerate(m['districts'])}
R=max(32,H*0.045)*S
zl=Image.new('RGBA',img.size,(0,0,0,0)); zd=ImageDraw.Draw(zl)
for k,l in locs.items():
    c=pal[didx[l['district']]%len(pal)]; x,y=pos[k]
    zd.ellipse([x-R,y-R,x+R,y+R],fill=C(*c,0.16))
img=Image.alpha_composite(img,zl); zl=Image.new('RGBA',img.size,(0,0,0,0)); zd=ImageDraw.Draw(zl)
for k,l in locs.items():
    c=pal[didx[l['district']]%len(pal)]; x,y=pos[k]
    zd.ellipse([x-R,y-R,x+R,y+R],outline=C(*c,0.7),width=2*S)
img=Image.alpha_composite(img,zl); d=ImageDraw.Draw(img,'RGBA')
# река: гранитная набережная + вода MegaMek-синяя
river=[(ts(p['x'],p['y'])[0]*S,ts(p['x'],p['y'])[1]*S) for p in m['river']]
for wdt,col in((30,C(0.48,0.47,0.43)),(27,C(0.62,0.60,0.55)),(24,C(0.16,0.36,0.60)),(14,C(0.22,0.44,0.70))):
    d.line(river,fill=col,width=wdt*S,joint='curve')
# МКАД днём: асфальтовая лента с белыми кромками и пунктиром
cx,cy=ts(.5,.5); ex,ey=ts(.9,.99); cx*=S;cy*=S;ex*=S;ey*=S; rx,ry=ex-cx,ey-cy
ring=[(cx+math.cos(a/320*2*math.pi)*rx,cy+math.sin(a/320*2*math.pi)*ry) for a in range(321)]
d.line(ring,fill=C(0.85,0.85,0.82),width=11*S); d.line(ring,fill=C(0.27,0.28,0.30),width=9*S)
for i in range(0,320,4): d.line([ring[i],ring[i+2]],fill=C(0.95,0.85,0.4,0.9),width=1*S)
# дороги: текстуры покрытий (цвета близки к RoadTextures)
RC={'asphalt':(0.22,0.22,0.24),'concrete':(0.64,0.64,0.62),'steel':(0.32,0.38,0.46),'gravel':(0.50,0.42,0.32),None:(0.72,0.72,0.70)}
PL=[(0.91,0.26,0.24),(0.16,0.66,0.36),(0.56,0.32,0.82)]
roads=[r for r in m['roads'] if r['from'] in locs and r['to'] in locs]
T=11*S; GAP=3*S; LR=9*S
def quad(cx,cy,ux,uy,hl,ht):
    nx,ny=-uy,ux
    return [(cx-ux*hl-nx*ht,cy-uy*hl-ny*ht),(cx+ux*hl-nx*ht,cy+uy*hl-ny*ht),(cx+ux*hl+nx*ht,cy+uy*hl+ny*ht),(cx-ux*hl+nx*ht,cy-uy*hl+ny*ht)]
for i,r in enumerate(roads):
    (ax,ay),(bx,by)=pos[r['from']],pos[r['to']]
    L=math.hypot(bx-ax,by-ay); ux,uy=(bx-ax)/L,(by-ay)/L
    sx,sy=ax+ux*(LR+3*S),ay+uy*(LR+3*S); tl=L-2*(LR+3*S); n=r['length']; sl=(tl-GAP*(n-1))/n
    own=(i//5)%3 if i%5==0 else None
    for kk in range(n):
        c0=kk*(sl+GAP)+sl/2; qx,qy=sx+ux*c0,sy+uy*c0
        bc=RC.get(r.get('resource'),RC[None])
        d.polygon(quad(qx+1.5*S,qy+1.5*S,ux,uy,sl/2,T/2),fill=(0,0,0,70))
        d.polygon(quad(qx,qy,ux,uy,sl/2,T/2),fill=C(*bc),outline=C(*(own is not None and PL[own] or (0.15,0.15,0.15))),width=(3 if own is not None else 1)*S)
        if r.get('resource')=='asphalt' or r.get('resource') is None:
            d.line([(qx-ux*sl*.25,qy-uy*sl*.25),(qx+ux*sl*.25,qy+uy*sl*.25)],fill=C(1,1,1,.8),width=1*S)
try: font=ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",12*S)
except: font=ImageFont.load_default()
for k,l in locs.items():
    x,y=pos[k]; c=pal[didx[l['district']]%len(pal)]
    d.ellipse([x-13*S,y-13*S+2*S,x+13*S,y+13*S+2*S],fill=(0,0,0,90))
    d.ellipse([x-11.5*S,y-11.5*S,x+11.5*S,y+11.5*S],fill=C(.97,.97,.95))
    d.ellipse([x-9*S,y-9*S,x+9*S,y+9*S],fill=C(.16,.17,.20))
    d.ellipse([x-4*S,y-4*S,x+4*S,y+4*S],fill=C(*c))
    tw=d.textlength(l['name'],font=font); ty=y+13*S
    d.rounded_rectangle([x-tw/2-5*S,ty,x+tw/2+5*S,ty+17*S],radius=4*S,fill=C(0.08,0.10,0.08,0.80))
    d.text((x-tw/2,ty+1*S),l['name'],font=font,fill=C(.96,.97,.95))
img=img.resize((W,H),Image.LANCZOS).convert('RGB'); img.save('/home/claude/preview/terrain_preview.png')
img.crop((420,250,1020,620)).resize((1200,740),Image.LANCZOS).save('/home/claude/preview/terrain_closeup.png'); print('ok',TW,TH)
