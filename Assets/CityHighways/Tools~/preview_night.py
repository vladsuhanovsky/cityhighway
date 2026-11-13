import json, math, sys
from PIL import Image, ImageDraw, ImageFont, ImageFilter
m=json.load(open('/home/claude/RailGame/Assets/CityHighways/Resources/CityHighways/Maps/moscow.json'))
W,H,PAD=1500,920,50; S=2  # суперсэмплинг
def ts(x,y): return ((PAD+x*(W-2*PAD))*S,(PAD+y*(H-2*PAD))*S)
open_=[d['id'] for d in m['districts'] if not d.get('expansion')]
locs={l['id']:l for l in m['locations'] if l['district'] in open_}
pos={k:ts(l['x'],l['y']) for k,l in locs.items()}
def C(r,g,b,a=1): return (int(r*255),int(g*255),int(b*255),int(a*255))
img=Image.new('RGBA',(W*S,H*S),C(0.055,0.065,0.10))
def hsh(x,y,salt):
    h=((x*73856093)^(y*19349663)^(salt*83492791))&0xffffffff
    h^=h>>13; h=(h*0x5bd1e995)&0xffffffff; h^=h>>15
    return (h&0xFFFFFF)/0x1000000
pal=[(0.95,0.55,0.35),(0.45,0.75,0.95),(0.65,0.85,0.45),(0.95,0.80,0.40),(0.80,0.55,0.90),(0.40,0.85,0.80),(0.95,0.60,0.70)]
didx={d['id']:i for i,d in enumerate(m['districts'])}
lay=Image.new('RGBA',img.size,(0,0,0,0)); d=ImageDraw.Draw(lay)
R=max(32,(H)*0.045)*S
for k,l in locs.items():
    c=pal[didx[l['district']]%len(pal)]; x,y=pos[k]
    d.ellipse([x-R,y-R,x+R,y+R],fill=C(*c,0.10))
img=Image.alpha_composite(img,lay)
d=ImageDraw.Draw(img)
cell=46; reach=max(150,H*0.2)
pts=[(p[0]/S,p[1]/S) for p in pos.values()]
roads_all=[r for r in m['roads'] if r['from'] in locs and r['to'] in locs]
tracks=[((pos[r['from']][0]/S,pos[r['from']][1]/S),(pos[r['to']][0]/S,pos[r['to']][1]/S)) for r in roads_all]
def dseg(px,py,a,b):
    ax,ay=a; bx,by=b; dx,dy=bx-ax,by-ay; t=max(0,min(1,((px-ax)*dx+(py-ay)*dy)/max(1e-4,dx*dx+dy*dy)))
    return math.hypot(px-(ax+dx*t),py-(ay+dy*t))
def house(x,y,w,h,lit):
    d.rectangle([(x-w/2)*S,(y-h)*S,(x+w/2)*S,y*S],fill=C(0.10,0.115,0.16))
    d.polygon([((x-w/2-1.2)*S,(y-h)*S),(x*S,(y-h-h*.55-1)*S),((x+w/2+1.2)*S,(y-h)*S)],fill=C(0.14,0.16,0.22))
    if lit: d.rectangle([(x-1.2)*S,(y-h+h*.3)*S,(x+1.2)*S,(y-h+h*.3+2.4)*S],fill=C(0.6,0.5,0.32))
for ix in range(int(W/cell)+1):
  for iy in range(int(H/cell)+1):
    cx=(ix+.5)*cell+(hsh(ix,iy,4)-.5)*cell*.5; cy=(iy+.5)*cell+(hsh(ix,iy,5)-.5)*cell*.5
    near=min(math.hypot(px-cx,py-cy) for px,py in pts)
    under=any(abs(cx-px)<62 and py<cy<py+40 for px,py in pts)
    if under or near<30: continue
    dens=max(0,min(1,1-near/reach))*0.55+0.05
    if hsh(ix,iy,1)>dens: continue
    if any(dseg(cx,cy,a,b)<16 for a,b in tracks): continue
    n=1+int(hsh(ix,iy,2)*3)
    for k in range(n):
        x=cx+(k-(n-1)/2)*11; y=cy+(hsh(ix+k,iy,3)-.5)*4
        house(x,y,7+hsh(ix,iy+k,6)*4,5+hsh(ix+k,iy+k,7)*4,hsh(ix*3+k,iy*5,8)<.45)
river=[ts(p['x'],p['y']) for p in m['river']]
for wdt,col in((30,C(0.20,0.30,0.45)),(24,C(0.05,0.13,0.24)),(10,C(0.10,0.24,0.40))):
    d.line(river,fill=col,width=wdt*S,joint='curve')
cx,cy=ts(.5,.5); ex,ey=ts(.9,.99); rx,ry=ex-cx,ey-cy
ring=[(cx+math.cos(a/160*2*math.pi)*rx,cy+math.sin(a/160*2*math.pi)*ry) for a in range(161)]
d.line(ring,fill=C(0.16,0.17,0.22),width=7*S)
for i in range(0,160,2):
    x,y=ring[i]; d.ellipse([x-4.5*S,y-4.5*S,x+4.5*S,y+4.5*S],fill=C(0.18,0.16,0.14)); d.ellipse([x-1.4*S,y-1.4*S,x+1.4*S,y+1.4*S],fill=C(1,0.86,0.55))
RC={'asphalt':(0.52,0.58,0.70),'concrete':(0.84,0.84,0.80),'steel':(0.42,0.68,1),'gravel':(0.90,0.64,0.34),None:(0.92,0.92,0.90)}
PL=[(0.91,0.26,0.24),(0.16,0.66,0.36),(0.56,0.32,0.82)]
owned={}
roads=[r for r in m['roads'] if r['from'] in locs and r['to'] in locs]
for i,r in enumerate(roads):
    if i%5==0: owned[r['id']]=(i//5)%3
glow=Image.new('RGBA',img.size,(0,0,0,0)); g=ImageDraw.Draw(glow)
T=11*S; GAP=3*S; LR=9*S
segs=[]
for r in roads:
    (ax,ay),(bx,by)=pos[r['from']],pos[r['to']]
    L=math.hypot(bx-ax,by-ay); ux,uy=(bx-ax)/L,(by-ay)/L
    sx,sy=ax+ux*(LR+3*S),ay+uy*(LR+3*S); tl=L-2*(LR+3*S); n=r['length']; sl=(tl-GAP*(n-1))/n
    for k in range(n):
        c0=k*(sl+GAP)+sl/2; segs.append((r, sx+ux*c0, sy+uy*c0, ux,uy, sl))
def quad(cx,cy,ux,uy,hl,ht):
    nx,ny=-uy,ux
    return [(cx-ux*hl-nx*ht,cy-uy*hl-ny*ht),(cx+ux*hl-nx*ht,cy+uy*hl-ny*ht),(cx+ux*hl+nx*ht,cy+uy*hl+ny*ht),(cx-ux*hl+nx*ht,cy-uy*hl+ny*ht)]
for r,cx,cy,ux,uy,sl in segs:
    if r['id'] in owned:
        pc=PL[owned[r['id']]]; g.polygon(quad(cx,cy,ux,uy,sl/2+4*S,T/2+4*S),fill=C(*pc,0.35))
glow=glow.filter(ImageFilter.GaussianBlur(4*S)); img=Image.alpha_composite(img,glow); d=ImageDraw.Draw(img)
for r,cx,cy,ux,uy,sl in segs:
    res=r.get('resource'); bc=RC.get(res,RC[None])
    q=quad(cx,cy,ux,uy,sl/2,T/2)
    if r['id'] in owned:
        pc=PL[owned[r['id']]]
        d.polygon(q,fill=C(*pc),outline=C(*[c*.55+.45 for c in pc]))
        d.line([(cx-ux*sl*.22,cy-uy*sl*.22),(cx+ux*sl*.22,cy+uy*sl*.22)],fill=C(1,1,1,.85),width=2*S)
        nx,ny=-uy,ux
        for sgn in(-1,1):
            lx,ly=cx+nx*(T/2+3.5*S)*sgn-ux*sl*.25*sgn,cy+ny*(T/2+3.5*S)*sgn-uy*sl*.25*sgn
            d.ellipse([lx-1.4*S,ly-1.4*S,lx+1.4*S,ly+1.4*S],fill=C(1,0.86,0.55))
    else:
        f=[0.055*0.78+bc[0]*0.22,0.065*0.78+bc[1]*0.22,0.10*0.78+bc[2]*0.22]
        d.polygon(q,fill=C(*f),outline=C(*bc,0.85),width=2*S)
    if res=='steel': d.polygon(quad(cx,cy,ux,uy,sl/2+2.5*S,T/2+2.5*S),outline=C(0.62,0.8,1,0.8),width=2*S)
try: font=ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",12*S); big=ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",14*S)
except: font=big=ImageFont.load_default()
for k,l in locs.items():
    x,y=pos[k]; c=pal[didx[l['district']]%len(pal)]
    d.ellipse([x-14*S,y-14*S,x+14*S,y+14*S],fill=C(c[0]*.18+.045,c[1]*.18+.053,c[2]*.18+.082))
    sq=l['district'] in('airports','vokzaly')
    shp=(d.rectangle if sq else d.ellipse)
    k1=11.5*S if not sq else 10.6*S; shp([x-k1,y-k1,x+k1,y+k1],fill=C(.92,.94,.98))
    k2=9*S if not sq else 8.3*S; shp([x-k2,y-k2,x+k2,y+k2],fill=C(.07,.08,.12))
    if l['name']=='Кремль':
        st=[(x+math.cos(-math.pi/2+i*math.pi/5)*(6.5 if i%2==0 else 2.7)*S,y+math.sin(-math.pi/2+i*math.pi/5)*(6.5 if i%2==0 else 2.7)*S) for i in range(10)]
        d.polygon(st,fill=C(*c))
    elif l['district']=='airports':
        d.line([(x,y-5.4*S),(x,y+5.4*S)],fill=C(*c),width=2*S); d.line([(x-5.4*S,y),(x+5.4*S,y)],fill=C(*c),width=2*S)
    elif l['district']=='vokzaly':
        for dx in(-2.7,2.7): d.line([(x+dx*S,y-5.4*S),(x+dx*S,y+5.4*S)],fill=C(*c),width=2*S)
    else: d.ellipse([x-4*S,y-4*S,x+4*S,y+4*S],fill=C(*c))
    tw=d.textlength(l['name'],font=font); ty=y+13*S
    d.rounded_rectangle([x-tw/2-5*S,ty,x+tw/2+5*S,ty+17*S],radius=4*S,fill=C(0.04,0.05,0.08,0.78))
    d.text((x-tw/2,ty+1*S),l['name'],font=font,fill=C(.9,.92,.96))
# виньетка
v=Image.new('RGBA',img.size,(0,0,0,0)); vd=ImageDraw.Draw(v)
for i in range(6): vd.rectangle([i*14*S,i*14*S,img.size[0]-i*14*S,img.size[1]-i*14*S],outline=(0,0,0,26),width=28*S)
img=Image.alpha_composite(img,v)
img=img.resize((W,H),Image.LANCZOS).convert('RGB'); img.save('night_map_preview.png'); print("saved")
