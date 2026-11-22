import math
def frac(v): return v-math.floor(v)
def H(x,y):
    s=math.sin(x*127.1+y*311.7)*43758.5453
    return s-math.floor(s)
def bruit(x,y):
    xi,yi=math.floor(x),math.floor(y); xf,yf=x-xi,y-yi
    u=xf*xf*(3-2*xf); v=yf*yf*(3-2*yf)
    a,b,c,d=H(xi,yi),H(xi+1,yi),H(xi,yi+1),H(xi+1,yi+1)
    return a+(b-a)*u+(c-a)*v+(a-b-c+d)*u*v
def fbm(x,y):
    s=0;a=.5
    for i in range(4): s+=a*bruit(x,y); x*=2.03; y*=2.03; a*=.5
    return s
def lisse(a,b,x):
    t=min(1,max(0,(x-a)/(b-a))); return t*t*(3-2*t)
def R(z): return 14*math.sin(z/55)+6*math.sin(z/23+1)
V=(55,10); D=(118,-80); B=13
def hauteur(x,z):
    d=abs(x-R(z)); y=-3.6+lisse(0,B,d)*4.8
    if d>B: y=1.2+(d-B)*.11+(max(0,d-70)/40)**2*7
    y+=(fbm(x*.02,z*.02)-.5)*11*lisse(B,B+30,d)
    r=math.hypot(x,z); crete=1-abs(fbm(x*.01+3,z*.01)*2-1)
    y+=max(0,r-185)**1.3*.11*(.45+.9*crete)
    y+=15*math.exp(-((x-D[0])**2+(z-D[1])**2)/(2*34*34))
    k=lisse(46,24,math.hypot(x-V[0],z-V[1]))
    y=y+(3.3+(fbm(x*.05,z*.05)-.5)*.8-y)*k
    return y
if __name__=='__main__':
    for z in [10,14,18,22,26,30,34,40]:
        print(z, round(R(z),2), ' '.join(f"{d}:{hauteur(R(z)+d,z):.2f}" for d in [0,3,5,6.5,8,9,10,11,12,13,15]))
