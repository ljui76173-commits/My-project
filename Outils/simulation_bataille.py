import math, sys
sys.path.insert(0, __import__('os').path.dirname(__import__('os').path.abspath(__file__)))
from terrain import R, hauteur
def v(a,b): return (a[0]+b[0],a[1]+b[1])
qB=(R(14)+3.8,14); qR=(R(14)+7.6,14); plage=(R(26)+8,26); ligne=(33,5)
Vd=(55-33,10-5); n=math.hypot(*Vd); front=(ligne[0]-Vd[0]/n*1.3, ligne[1]-Vd[1]/n*1.3)
na1=v(qR,(2.6,-1.4)); m1=(26.5,10.5); J=(29.5,8.5); nb=(23.8,22.5); m2=(25.2,18.8); c1=(26.8,15.2); s2=(31,41)
A=[qB,qR,na1,m1,J,front]; Bp=[plage,nb,m2,c1,J,front]
fuite=[plage,(25,33),s2,(38,56),(44,80)]; fuiteQ=[qB,qR,(24,24),(25,33),s2,(38,56),(44,80)]
def surquai(p): return qB[0]-.6<p[0]<qR[0]+.6 and abs(p[1]-14)<1.4
def nage(p): return not surquai(p) and hauteur(*p)<-.75
def run(traps, chain=True, dt=.05):
    T={k:dict(pos=p,typ=t,cap=0,tombe=False,det=False,pris=[]) for k,(t,p) in traps.items()}
    C=[dict(p=None,st='pont',ch=None,e=0,att=0,des=(i==11)) for i in range(12)]
    t=0; sched=[]; fiole=[False]; esc=0; log=[]
    def plonger(c,att):
        c['p']=(R(40)+2.1, 36+C.index(c)*.6); c['st']='nage'; c['ch']=A; c['e']=0; c['att']=att
    if chain:
        for k in range(3): plonger(C[k],k*.25)
        # bras de fer ~15s, then rive
        sched.append((15,lambda:[plonger(C[k],(k-3)*.9) for k in range(3,6)]))
        def barque():
            for k in range(6,12):
                c=C[k]; c['p']=(plage[0]-1+(k%2)*.6, plage[1]+(k-6)*.3); c['att']=(k-6)*.55; c['e']=1
                if c['des']: c['st']='fuite'; c['ch']=fuite
                else: c['st']='marche'; c['ch']=Bp
        # barque travel ~ dist / 2.2
        d=math.dist((R(40)-2.8+0,39),(plage[0]-2.6,26)); sched.append((16.5+d/2.2,barque))
    else:
        for k in range(12):
            def deb(k=k):
                c=C[k]; c['p']=(qB[0]+.3,14); c['e']=1
                if c['des']: c['st']='fuite'; c['ch']=fuiteQ
                else: c['st']='marche'; c['ch']=A
            sched.append((k*.6,deb))
    def prendre(c,tr): c['st']='pris'; 
    while t<120:
        t+=dt
        for s in [s for s in sched if s[0]<=t]: sched.remove(s); s[1]()
        for c in C:
            if c['st'] in ('nage','marche','fuite'):
                if c['att']>0: c['att']-=dt; continue
                if c['e']>=len(c['ch']):
                    if c['st']=='fuite': c['st']='enfui'; log.append((round(t,1),'enfui'))
                    else: c['st']='ligne'; esc+=1; log.append((round(t,1),'ligne'))
                    continue
                b=c['ch'][c['e']]; p=c['p']; d=math.dist(p,b)
                if d<.6: c['e']+=1; continue
                sp=1.35 if nage(p) else (3.4 if c['st']=='fuite' else 2.5)
                step=min(sp*dt,d); c['p']=(p[0]+(b[0]-p[0])/d*step, p[1]+(b[1]-p[1])/d*step)
                if c['st']=='nage' and not nage(c['p']): c['st']='marche'
                if c['st']=='marche':
                    for k,tr in T.items():
                        dd=math.dist(c['p'],tr['pos'])
                        if tr['det']: continue
                        if tr['typ']=='fosse' and dd<2 and tr['cap']<2: c['st']='pris'; tr['cap']+=1; tr['pris'].append(c); log.append((round(t,1),k)); break
                        if tr['typ']=='filet' and not tr['tombe'] and dd<1.6:
                            tr['tombe']=True
                            for o in C:
                                if o['st']=='marche' and tr['cap']<3 and math.dist(o['p'],tr['pos'])<3.6: o['st']='pris'; tr['cap']+=1; tr['pris'].append(o)
                            log.append((round(t,1),k,tr['cap']))
                            if tr['cap']>=3 and not fiole[0]:
                                fiole[0]=True
                                def fio(tr=tr,k=k):
                                    lib=list(tr['pris']); tr['pris'].clear(); tr['cap']=0; tr['det']=True
                                    for o in lib: o['st']='marche'; o['att']=.8
                                    log.append((round(t,1),'fiole',k,len(lib)))
                                    if lib: sched.append((t+2,lambda o=lib[0]: o.__setitem__('st','pris') if o['st']=='marche' else None))
                                sched.append((t+1.3,fio))
                            break
                        if tr['typ']=='trape' and dd<2.2 and tr['cap']<2: c['st']='pris'; tr['cap']+=1; log.append((round(t,1),k)); break
                elif c['st']=='fuite' and 's2' in T and math.dist(c['p'],s2)<3.5: c['st']='rattrape'; log.append((round(t,1),'rattrape'))
        if all(c['st'] in ('pris','ligne','enfui','rattrape') for c in C) and not sched: break
    return esc, round(t,1), log
parfait={'na1':('filet',na1),'nb':('filet',nb),'J':('filet',J),'m1':('fosse',m1),'m2':('fosse',m2),'c1':('trape',c1),'s2':('cloche',s2)}
for nom,tr,ch in [('parfait',parfait,True),('sans chaîne',{k:v for k,v in parfait.items()},False),('rien',{},True),('sans filet du carrefour',{k:v for k,v in parfait.items() if k!='J'},True),('chaîne seule',{},True)]:
    e,t,log=run(tr,ch); print(f"{nom:28s} passés à la ligne: {e}  fin à {t}s"); print('   ',log)
