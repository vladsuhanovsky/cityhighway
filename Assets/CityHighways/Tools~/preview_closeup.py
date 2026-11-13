import json, math
from PIL import Image, ImageDraw, ImageFont
from textures import TEX
m=json.load(open('/home/claude/RailGame/Assets/CityHighways/Resources/CityHighways/Maps/moscow.json'))
W,H,PAD=1500,920,50; Z=3.0
CX,CY=750,470; VW,VH=560,340   # окно вокруг центра (в пикселях игры)
def ts(x,y): 
    gx,gy=PAD+x*(W-2*PAD),PAD+y*(H-2*PAD); return ((gx-(CX-VW/2))*Z,(gy-(CY-VH/2))*Z)
img=Image.new('RGBA',(int(VW*Z),int(VH*Z)),(14,17,26,255)); d=ImageDraw.Draw(img)
locs={l['id']:l for l in m['locations']}; pos={k:ts(l['x'],l['y']) for k,l in locs.items()}
# река: кромка + текстурная лента (плитки воды, повернутые вдоль русла)
river=[ts(p['x'],p['y']) for p in m['river']]
d.line(river,fill=(92,97,107,255),width=int(27*Z),joint='curve')
wt=TEX['water'].resize((int(48*Z),int(24*Z)))
for i in range(len(river)-1):
    (ax,ay),(bx,by)=river[i],river[i+1]; L=math.hypot(bx-ax,by-ay); ang=math.degrees(math.atan2(by-ay,bx-ax))
    n=max(1,int(L/(48*Z))+1)
    for k in range(n):
        t=(k+.5)/n; cx,cy=ax+(bx-ax)*t,ay+(by-ay)*t
        tile=wt.resize((int(L/n)+4,int(24*Z))).rotate(-ang,expand=True,resample=Image.BICUBIC)
        img.alpha_composite(tile,(int(cx-tile.width/2),int(cy-tile.height/2)))
T=11*Z; GAP=3*Z; LR=9*Z
owned={'r':0}
roads=m['roads']
PL=[(232,66,61),(41,168,92),(143,82,209)]
own={}
for i,r in enumerate(roads):
    if i%3==0: own[r['id']]=(i//3)%3
RC={'asphalt':(133,148,179),'concrete':(214,214,204),'steel':(107,173,255),'gravel':(230,163,87),'any':(235,235,230)}
glow=Image.new('RGBA',img.size,(0,0,0,0)); g=ImageDraw.Draw(glow)
def quad(cx,cy,ux,uy,hl,ht):
    nx,ny=-uy,ux
    return [(cx-ux*hl-nx*ht,cy-uy*hl-ny*ht),(cx+ux*hl-nx*ht,cy+uy*hl-ny*ht),(cx+ux*hl+nx*ht,cy+uy*hl+ny*ht),(cx-ux*hl+nx*ht,cy-uy*hl+ny*ht)]
segs=[]
for r in roads:
    if r['from'] not in pos or r['to'] not in pos: continue
    (ax,ay),(bx,by)=pos[r['from']],pos[r['to']]
    L=math.hypot(bx-ax,by-ay); ux,uy=(bx-ax)/L,(by-ay)/L
    sx,sy=ax+ux*(LR+3*Z),ay+uy*(LR+3*Z); tl=L-2*(LR+3*Z); n=r['length']; sl=(tl-GAP*(n-1))/n
    for k in range(n): c0=k*(sl+GAP)+sl/2; segs.append((r,sx+ux*c0,sy+uy*c0,ux,uy,sl))
for r,cx,cy,ux,uy,sl in segs:
    if r['id'] in own: g.polygon(quad(cx,cy,ux,uy,sl/2+3*Z,T/2+3*Z),fill=PL[own[r['id']]]+(80,))
img.alpha_composite(glow)
d=ImageDraw.Draw(img)
for r,cx,cy,ux,uy,sl in segs:
    res=r.get('resource') or 'any'; tex=TEX[res]; built=r['id'] in own
    ang=math.degrees(math.atan2(uy,ux)); n=max(1,round(sl/T)); tlen=sl/n
    for k in range(n):
        t=(k+.5)/n-.5; px_,py_=cx+ux*sl*t,cy+uy*sl*t
        tile=tex.resize((max(2,int(tlen)),int(T)),Image.BICUBIC)
        if not built:
            a=tile.split()[3].point(lambda v:int(v*.22)); tile.putalpha(a)
        tile=tile.rotate(-ang,expand=True,resample=Image.BICUBIC)
        img.alpha_composite(tile,(int(px_-tile.width/2),int(py_-tile.height/2)))
    nx,ny=-uy,ux; a=(cx-ux*sl/2,cy-uy*sl/2); b=(cx+ux*sl/2,cy+uy*sl/2); h=T/2
    if built:
        c=PL[own[r['id']]]
        for s in(1,-1): d.line([(a[0]+nx*h*s,a[1]+ny*h*s),(b[0]+nx*h*s,b[1]+ny*h*s)],fill=c,width=int(2.4*Z))
        if res in('asphalt','concrete'):
            col=(255,255,255) if res=='asphalt' else (255,209,64)
            d.line([(cx-ux*sl*.22,cy-uy*sl*.22),(cx+ux*sl*.22,cy+uy*sl*.22)],fill=col,width=int(1.8*Z))
        for s in(-1,1):
            lx,ly=cx+nx*(h+3.5*Z)*s-ux*sl*.25*s,cy+ny*(h+3.5*Z)*s-uy*sl*.25*s
            d.ellipse([lx-1.2*Z,ly-1.2*Z,lx+1.2*Z,ly+1.2*Z],fill=(255,219,140))
    else:
        d.polygon(quad(cx,cy,ux,uy,sl/2,h),outline=RC[res],width=int(1.6*Z))
    if res=='steel':
        rail=(168,209,255)
        for s in(1,-1): d.line([(a[0]+nx*(h+2.5*Z)*s,a[1]+ny*(h+2.5*Z)*s),(b[0]+nx*(h+2.5*Z)*s,b[1]+ny*(h+2.5*Z)*s)],fill=rail,width=int(1.8*Z))
font=ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",int(12*Z))
for k,l in locs.items():
    x,y=pos[k]
    if not(-50<x<img.width+50 and -50<y<img.height+50): continue
    d.ellipse([x-11.5*Z,y-11.5*Z,x+11.5*Z,y+11.5*Z],fill=(235,240,250)); d.ellipse([x-9*Z,y-9*Z,x+9*Z,y+9*Z],fill=(18,20,31)); d.ellipse([x-4*Z,y-4*Z,x+4*Z,y+4*Z],fill=(240,180,90))
    tw=d.textlength(l['name'],font=font); ty=y+13*Z
    d.rounded_rectangle([x-tw/2-5*Z,ty,x+tw/2+5*Z,ty+17*Z],radius=4*Z,fill=(10,13,20,200)); d.text((x-tw/2,ty+1*Z),l['name'],font=font,fill=(230,235,245))
img.convert('RGB').save('textures_closeup.png'); print('saved',img.size)
