# Порт генераторов из RoadTextures.cs (те же правила) — только для превью.
import math, random
from PIL import Image
def mk(w,h,fill,seed):
    px=[[(0,0,0,1)]*w for _ in range(h)]; rng=random.Random(seed); fill(px,w,h,rng)
    img=Image.new('RGBA',(w,h))
    img.putdata([tuple(int(max(0,min(1,c))*255) for c in px[y][x]) for y in range(h) for x in range(w)]); return img
def N(rng,a): return (rng.random()*2-1)*a
def blend(px,w,h,x,y,c,a):
    x%=w; y%=h; o=px[y][x]; px[y][x]=tuple(o[i]+(c[i]-o[i])*a for i in range(3))+(1,)
def disc(px,w,h,cx,cy,r,c,al=1):
    for y in range(math.floor(cy-r),math.ceil(cy+r)+1):
        for x in range(math.floor(cx-r),math.ceil(cx+r)+1):
            d=math.hypot(x-cx,y-cy)
            if d<=r: blend(px,w,h,x,y,c,al*max(0,min(1,r-d+.5)))
def asphalt(px,w,h,rng):
    for y in range(h):
        for x in range(w):
            tr=-0.025 if (abs(y-h*.3)<5 or abs(y-h*.7)<5) else 0; v=.2+tr+N(rng,.035); px[y][x]=(v,v+.005,v+.02,1)
    for _ in range(90):
        g=.32+rng.random()*.18; blend(px,w,h,rng.randrange(w),rng.randrange(h),(g,g,g),.8)
    cy=rng.randrange(h); d=N(rng,.6)
    for x in range(w): blend(px,w,h,x,round(cy),(.08,.08,.09),.5); cy+=d+N(rng,.5)
def concrete(px,w,h,rng):
    for y in range(h):
        for x in range(w): v=.62+N(rng,.03); px[y][x]=(v,v,v-.02,1)
    for _ in range(5): disc(px,w,h,rng.randrange(w),rng.randrange(h),4+rng.random()*6,(.5,.5,.48),.18)
    for y in range(h): blend(px,w,h,0,y,(.32,.32,.31),.9); blend(px,w,h,1,y,(.4,.4,.39),.6); blend(px,w,h,w-1,y,(.78,.78,.76),.5)
    for x in range(w): blend(px,w,h,x,h//2,(.45,.45,.44),.35)
def gravel(px,w,h,rng):
    for y in range(h):
        for x in range(w): v=N(rng,.04); px[y][x]=(.36+v,.29+v,.21+v,1)
    for _ in range(260):
        r=.9+rng.random()*1.9; t=rng.random(); k=.8+.35*rng.random()
        c=tuple(((.55+(.66-.55)*t),(.47+(.64-.47)*t),(.36+(.6-.36)*t)))
        c=tuple(min(1,v*k) for v in c); x,y=rng.randrange(w),rng.randrange(h)
        disc(px,w,h,x+.6,y+.8,r,(.15,.12,.09),.5); disc(px,w,h,x,y,r,c); blend(px,w,h,round(x-r*.4),round(y-r*.4),(1,1,1),.35)
def steel(px,w,h,rng):
    for y in range(h):
        for x in range(w): v=N(rng,.02); px[y][x]=(.3+v,.36+v,.44+v,1)
    for gy in range(4,h,8):
        for gx in range(4,w,8):
            fl=((gx//8)+(gy//8))%2==0
            for k in range(-2,3):
                x,y=gx+k,gy+(k if fl else -k); blend(px,w,h,x,y,(.62,.7,.8),.7); blend(px,w,h,x+1,y+1,(.14,.17,.22),.5)
    for x in range(w): blend(px,w,h,x,0,(.12,.15,.2),.9); blend(px,w,h,x,h-1,(.12,.15,.2),.9)
    for x in range(6,w,12): disc(px,w,h,x,3.5,1.4,(.75,.8,.88)); disc(px,w,h,x,h-4.5,1.4,(.75,.8,.88))
    for y in range(h): blend(px,w,h,0,y,(.12,.15,.2),.8)
def cobble(px,w,h,rng):
    for y in range(h):
        row=y//11; sh=0 if row%2==0 else 8
        for x in range(w):
            sx,sy=(x+sh)%16,y%11; seam=sx==0 or sy==0; st=(x+sh)//16+row*13
            t=.7+((st*37)%11)/11*.12+N(rng,.02); c=(.3,.3,.32) if seam else (t,t,t-.03)
            if not seam and (sx==1 or sy==1): c=tuple(min(1,v*1.08) for v in c)
            px[y][x]=c+(1,)
def water(px,w,h,rng):
    for y in range(h):
        for x in range(w):
            ac=abs(y/(h-1)-.5)*2; wave=math.sin(x/w*math.pi*4+math.sin(y*.35)*1.5)*.5+.5; rip=math.sin(x/w*math.pi*10+y*.9)*.5+.5
            deep=(.04,.14,.27); bank=(.03,.08,.15); c=[deep[i]+(bank[i]-deep[i])*ac*ac for i in range(3)]
            add1=(.05,.09,.13); add2=(.03,.05,.07); n=N(rng,.008)
            c=[c[i]+add1[i]*wave**6*(1-ac)+add2[i]*rip**10+n for i in range(3)]; px[y][x]=tuple(c)+(1,)
    for _ in range(26):
        x,y=rng.randrange(w),rng.randrange(h//5,h-h//5); L=3+rng.randrange(6)
        for k in range(L): blend(px,w,h,x+k,y,(.65,.78,.95),.35*(1-abs(k-L/2)/L*2))
TEX={k:mk(64,64,f,i) for i,(k,f) in enumerate([('asphalt',asphalt),('concrete',concrete),('gravel',gravel),('steel',steel),('any',cobble)])}
TEX['water']=mk(128,64,water,9)
