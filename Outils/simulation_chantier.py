# Simulation de l'économie du chantier du pont (mêmes règles que Chantier.cs / Poste.cs / Ouvrier.cs).
# Usage : python3 Outils/simulation_chantier.py "{'etapes':[(8,4,0,3),(0,0,16,3),(0,0,20,3)],'four_jp':1.5,'four_rate':4,'rend':{'foret':('bois',10,4,None),'riviere':('pierre',6,2,31)}}"
import math, sys
# Reproduction simplifiée de la logique du jeu (Chantier.cs, Poste.cs, Ouvrier.cs)
JOUR=180.0
POS={'chantier':(22.2,-13),'riviere':(15.1,7),'carriere':(95,-108),'foret':(40,74),'talus':(70,-37),'four':(80.8,-39.8)}
def dist(a,b): return math.dist(POS[a],POS[b])
CAP={'pierre':3,'chaux':3,'bois':4,'calcaire':3}
REND={'riviere':('pierre',4,2,31),'carriere':('pierre',2,4,None),'foret':('bois',5,4,None),'talus':('calcaire',4,2,40)}
def simuler(plan, mana=True, P=None, verbose=False):
    P=P or {}
    REND.update(P.get('rend',{})); CAP.update(P.get('cap',{}))
    FOUR_JP=P.get('four_jp',3); ET=P.get('etapes',[(12,4,0,4),(0,0,24,6),(0,0,30,5)])
    stock={k:{} for k in POS}; ext={k:0 for k in POS}
    def S(p,r): return stock[p].get(r,0)
    def add(p,r,q): stock[p][r]=S(p,r)+q
    rep=0; etape=0; av=[0,0,0]; chx=0; t=0; dt=0.5; boost={k:0 for k in POS}; m=35
    porteurs={}  # id -> state
    four_ok=False
    add('talus','calcaire',P.get('talus0',6)); add('four','bois',P.get('four0',4))
    while etape<3 and t<JOUR*15:
        a=plan(four_ok,etape)
        # production
        pers={k:0 for k in POS}
        for w,(typ,x) in a.items():
            if typ=='poste': pers[x]+= (2 if (w=='Odon' and (x=='chantier' or (x=='four' and not four_ok))) else 1)
        for p,(r,rd,mx,res) in REND.items():
            n=min(pers[p],mx)
            if n and S(p,r)<20 and (res is None or ext[p]<res):
                q=n*rd*dt/JOUR*(2 if boost[p]>0 else 1)
                if res: q=min(q,res-ext[p])
                add(p,r,q); ext[p]+=q
        # four
        if not four_ok:
            cap=FOUR_JP*min(1,math.floor(S('four','pierre'))/4)
            rep=min(rep+pers['four']*dt/JOUR*(2 if boost['four']>0 else 1),cap)
            if rep>=FOUR_JP-1e-3:
                four_ok=True; stock['four']['pierre']-=4
                if verbose: print(f'four réparé jour {t/JOUR:.2f}')
        else:
            n=min(pers['four'],2)
            if n and S('four','chaux')<20:
                chx+=n*P.get('four_rate',2)*dt/JOUR*(2 if boost['four']>0 else 1)
                if chx>=1:
                    if S('four','calcaire')>=2 and S('four','bois')>=1:
                        add('four','calcaire',-2); add('four','bois',-1); add('four','chaux',2); chx-=1
                    else: chx=1
        # pont
        pi,ch,bo,jp=ET[etape]
        part=1
        for r,need in (('pierre',pi),('chaux',ch),('bois',bo)):
            if need>0: part=min(part,min(1,math.floor(S('chantier',r))/need))
        av[etape]=min(av[etape]+pers['chantier']*dt/JOUR*(2 if boost['chantier']>0 else 1), jp*part)
        if av[etape]>=jp-1e-3:
            for r,need in (('pierre',pi),('chaux',ch),('bois',bo)): add('chantier',r,-need)
            if verbose: print(f"étape {etape+1} finie jour {t/JOUR:.2f}")
            etape+=1
            if etape>=3: break
        # porteurs
        for w,(typ,x) in a.items():
            if typ!='porteur': continue
            s,d=x; r='chaux' if s=='four' else REND[s][0]
            st=porteurs.setdefault(w,{'phase':'aller','t':0,'q':0,'route':x})
            if st['route']!=x and st['q']==0: st.update(phase='aller',t=0,route=x)
            st['t']+=dt
            L=dist(*st['route'])
            if st['phase']=='aller':
                if st['t']>=L/3: st.update(phase='charge',t=0)
            elif st['phase']=='charge':
                if st['t']<dt*1.01 and st['q']==0:
                    q=min(CAP[r],math.floor(S(s,r)))
                    if q<1: st['t']=0; continue
                    add(s,r,-q); st['q']=q
                if st['t']>=5: st.update(phase='livre',t=0)
            elif st['phase']=='livre':
                if st['t']>=L/3+1.5:
                    add(st['route'][1],r,st['q']); st.update(phase='aller',t=-L/3+L/3,q=0)
                    st['t']=0; st['phase']='retour'
            elif st['phase']=='retour':
                if st['t']>=L/3: st.update(phase='charge',t=0)
        # mana : don sur le chantier (ou le four avant réparation) dès que possible
        for k in boost: boost[k]=max(0,boost[k]-dt)
        if mana:
            cible='chantier' if four_ok else 'four'
            if m>=10 and boost[cible]<=0 and pers[cible]>0: m-=10; boost[cible]=90
            m=min(35,m+0.08*dt)
        t+=dt
        if t%JOUR<dt: m=35  # on dort chaque nuit
    return t/JOUR
W=['Odon','Joss','Pell','Marthe','Bram','Agathe','Colin','Firmin']
def bon(four_ok,etape):
    a={'Odon':('poste','four' if not four_ok else 'chantier')}
    if not four_ok:
        a.update({'Joss':('poste','riviere'),'Pell':('poste','riviere'),'Marthe':('porteur',('riviere','four')),'Bram':('poste','foret')})
        return a
    a.update({'Joss':('poste','riviere') if etape==0 else ('poste','foret'),
              'Pell':('porteur',('riviere','chantier')) if etape==0 else ('porteur',('foret','chantier')),
              'Marthe':('porteur',('foret','chantier')),'Bram':('poste','foret'),
              'Agathe':('poste','talus') if etape==0 else ('poste','chantier'),
              'Colin':('porteur',('talus','four')) if etape==0 else ('porteur',('foret','chantier')),
              'Firmin':('poste','four') if etape==0 else ('poste','chantier')})
    if etape==0:
        a['Marthe']=('porteur',('four','chantier'))
        a['Bram']=('porteur',('foret','four'))
    return a
def moyen(four_ok,etape):
    a={'Odon':('poste','four' if not four_ok else 'chantier')}
    if not four_ok:
        a.update({'Joss':('poste','carriere'),'Pell':('porteur',('carriere','four')),'Marthe':('poste','foret'),'Bram':('poste','foret')})
        return a
    a.update({'Joss':('poste','carriere'),'Pell':('porteur',('carriere','chantier')),'Marthe':('poste','foret'),'Bram':('porteur',('foret','chantier')),
              'Agathe':('poste','talus'),'Colin':('porteur',('talus','four')),'Firmin':('porteur',('four','chantier'))})
    return a
P=eval(sys.argv[1]) if len(sys.argv)>1 else {}
print("bien organisé + mana :", round(simuler(bon,True,P,True),2), "jours")
print("bien organisé sans mana :", round(simuler(bon,False,P),2), "jours")
print("mal organisé (carrière, peu de bâtisseurs) :", round(simuler(moyen,False,P),2), "jours")
