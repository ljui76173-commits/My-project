using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    public enum EtatMonstre { Repos, Poursuite, Encerclement, Preparation, Frappe, Recuperation, Fuite, Sonne, Choc, Retour, Mort }

    /// <summary>Une attaque de créature : portée, préparation annoncée (lueur jaune, rouge si lourde), frappe, récupération.</summary>
    public class AttaqueMonstre
    {
        public string nom = "Coup";
        public float porteeMin, portee = 2f, preparation = .5f, frappe = .2f, recuperation = .6f;
        public float mult = 1f, poussee = .6f, cone = 70f, bond, sautVertical, rayonZone, recharge = 1.5f, poids = 1f;
        public bool lourde, deuxMains;
        /// <summary>Pose du bras armé (angles) au sommet de la préparation, puis à la fin de la frappe.</summary>
        public Vector3 poseArmement = new Vector3(-200f, 15f, 0f), poseFrappe = new Vector3(-55f, 0f, 0f);
        [System.NonSerialized] public float prete;
    }

    /// <summary>
    /// Une créature qui pense : elle erre près de son repaire, repère un ennemi, le poursuit,
    /// annonce ses attaques (une lueur au-dessus de la tête : jaune pour un coup normal, rouge pour un coup lourd),
    /// frappe, se découvre en récupérant, tourne autour de sa proie quand d'autres attaquent déjà,
    /// fuit si elle est lâche, rentre si on l'entraîne trop loin. Projetée contre un mur, elle prend un choc ;
    /// sonnée, elle s'offre au coup de grâce.
    /// </summary>
    public abstract class Monstre : Combattant
    {
        public static readonly List<Monstre> Actifs = new List<Monstre>();

        public Repaire repaire;
        public Vector3 origine;
        public float vitesseMarche = 1.8f, vitesseCourse = 4.5f, perception = 16f, laisse = 36f, rayonRepos = 7f;
        public float rayonCorps = .4f, hauteurCorps = 1.6f, hauteurTete = 1.7f, rotationPreparation = 300f, frequencePas = 4f;
        [Tooltip("Combien de créatures peuvent attaquer le héros en même temps (les loups : une seule, les autres tournent autour).")]
        public int maxAttaquants = 99;
        [Tooltip("Arme qui peut se bloquer contre l'épée du héros (choc des lames).")]
        public bool croiseLeFer;
        [Tooltip("S'enfuit sous 30 % de vie.")]
        public bool fuyard;

        public EtatMonstre Etat { get; private set; }
        public ICible Cible => cible;

        protected readonly List<AttaqueMonstre> attaques = new List<AttaqueMonstre>();
        protected CharacterController cc;
        protected Transform corps;
        protected ICible cible;
        protected AttaqueMonstre enCours;
        protected float tEtat, vitesseActuelle, phasePas;

        float vy, prochaineDecision, finFuite, attenteRepos, sensCercle = 1f, sensChute = 1f;
        Vector3 vitesseVoulue, elan, pointRepos;
        bool aPointRepos, coupPorte, murTouche;
        Transform marqueur, etoiles;
        Material matMarqueur;
        static readonly Color Jaune = new Color(1f, .82f, .3f), Rouge = new Color(1f, .22f, .12f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ViderActifs() => Actifs.Clear();

        // ---------- à définir par chaque espèce ----------

        /// <summary>Stats, vitesses, attaques (appelé avant tout le reste).</summary>
        protected abstract void Configurer();
        /// <summary>Le corps, sous « corps » (pieds à l'origine, regard vers +Z).</summary>
        protected abstract void Construire();
        protected abstract void Animer(float dt);
        /// <summary>Elle vient de repérer un ennemi (cri, appel de la meute).</summary>
        protected virtual void Alerter() { }
        protected virtual void SurPreparation(AttaqueMonstre a) { }
        protected virtual void SurFrappe(AttaqueMonstre a) { Sons.JouerA(a.lourde ? "lameLourde" : "lame", Centre, .7f, Random.Range(.75f, .9f)); }
        protected virtual void SurTouche(ICible c, AttaqueMonstre a) { }
        protected virtual void SurFuite() { }
        protected virtual void CriDeMort() { }
        protected virtual bool PeutCibler(ICible c) => true;

        // ---------- cycle de vie ----------

        protected override void Awake()
        {
            Configurer();
            base.Awake();
            hauteurCorps = Mathf.Max(hauteurCorps, rayonCorps * 2f + .05f);
            cc = gameObject.AddComponent<CharacterController>();
            cc.radius = rayonCorps;
            cc.height = hauteurCorps;
            cc.center = new Vector3(0, hauteurCorps / 2f, 0);
            cc.slopeLimit = 50f;
            cc.stepOffset = Mathf.Min(.45f, hauteurCorps * .3f);
            cc.skinWidth = .04f;
            cc.minMoveDistance = 0f;
            corps = new GameObject("Corps").transform;
            corps.SetParent(transform, false);
            Construire();
            ConstruireSignes();
            origine = transform.position;
            sensCercle = Random.value < .5f ? -1f : 1f;
            prochaineDecision = Time.time + Random.value * .4f;
        }

        protected override void OnEnable() { base.OnEnable(); Actifs.Add(this); }
        protected override void OnDisable() { base.OnDisable(); Actifs.Remove(this); }

        /// <summary>Lance la créature sur une cible (appel de la meute, gelées nées d'une division).</summary>
        public void Provoquer(ICible c)
        {
            if (!Cibles.Existe(c) || Etat == EtatMonstre.Mort) return;
            cible = c;
            if (Etat == EtatMonstre.Repos || Etat == EtatMonstre.Retour) Changer(EtatMonstre.Poursuite);
        }

        /// <summary>Une poussée (division d'une gelée, onde de choc).</summary>
        public void Pousser(Vector3 vitesse, float saut = 0f)
        {
            elan = new Vector3(vitesse.x, 0, vitesse.z);
            if (saut > 0f) vy = saut;
        }

        protected void Changer(EtatMonstre e)
        {
            Etat = e;
            tEtat = 0f;
            if (e == EtatMonstre.Repos) { aPointRepos = false; attenteRepos = Random.Range(.5f, 2.5f); }
        }

        protected override void Update()
        {
            base.Update();
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            tEtat += dt;
            if (Etat == EtatMonstre.Mort) { AnimerMort(dt); return; }
            var h = CombatHeros.Instance;
            if (h != null && (h.transform.position - transform.position).sqrMagnitude > 160f * 160f) return;   // trop loin : elle dort

            if (Etourdi > 0f && Etat != EtatMonstre.Choc)
            {
                if (Etat != EtatMonstre.Sonne) Changer(EtatMonstre.Sonne);
            }
            else if (Etat == EtatMonstre.Sonne) Changer(cible != null ? EtatMonstre.Poursuite : EtatMonstre.Retour);

            if (Time.time >= prochaineDecision && Etat != EtatMonstre.Choc && Etat != EtatMonstre.Sonne)
            {
                prochaineDecision = Time.time + .35f + Random.value * .2f;
                Decider();
            }

            vitesseVoulue = Vector3.zero;
            switch (Etat)
            {
                case EtatMonstre.Repos: Errer(dt); break;
                case EtatMonstre.Poursuite: Poursuivre(dt); break;
                case EtatMonstre.Encerclement: Encercler(dt); break;
                case EtatMonstre.Preparation: Preparer(dt); break;
                case EtatMonstre.Frappe: Frapper(dt); break;
                case EtatMonstre.Recuperation:
                    if (tEtat >= enCours.recuperation) Changer(cible != null ? EtatMonstre.Poursuite : EtatMonstre.Retour);
                    break;
                case EtatMonstre.Fuite: Fuir(dt); break;
                case EtatMonstre.Retour: Rentrer(dt); break;
                case EtatMonstre.Choc:
                    if (cible != null) Tourner(Plat(cible.Centre - transform.position), 200f, dt);
                    if (tEtat > 3f) Changer(EtatMonstre.Recuperation);
                    break;
            }
            Bouger(dt);
            AnimerSignes(dt);
            Animer(dt);
        }

        // ---------- décisions ----------

        void Decider()
        {
            if (cible != null && (!Cibles.Existe(cible) || !cible.Vivant)) cible = null;
            if (Etat == EtatMonstre.Fuite) return;
            if (cible != null)
            {
                float d = Distance(cible);
                if (d > perception * 2.2f || Plat(transform.position - origine).magnitude > laisse)
                {
                    cible = null;
                    if (Etat != EtatMonstre.Preparation && Etat != EtatMonstre.Frappe) Changer(EtatMonstre.Retour);
                }
                return;
            }
            if (Etat == EtatMonstre.Retour && tEtat < 3f) return;
            var c = ChercherCible();
            if (c == null) return;
            cible = c;
            if (Etat == EtatMonstre.Repos || Etat == EtatMonstre.Retour) { Alerter(); Changer(EtatMonstre.Poursuite); }
        }

        ICible ChercherCible()
        {
            ICible meilleur = null;
            float best = perception;
            if (camp == Camp.Hostile)
            {
                var h = CombatHeros.Instance;
                if (h != null && h.Vivant)
                {
                    float d = Distance(h);
                    if (d < best && PeutCibler(h)) { best = d; meilleur = h; }
                }
            }
            foreach (var c in Combattant.Tous)
            {
                if (c == this || c.invincible || !c.Vivant || !Ennemis(camp, c.Camp)) continue;
                float d = Distance(c);
                if (d < best && PeutCibler(c)) { best = d; meilleur = c; }
            }
            return meilleur;
        }

        public static bool Ennemis(Camp a, Camp b) =>
            (a == Camp.Hostile && (b == Camp.Heros || b == Camp.Allie)) || ((a == Camp.Allie || a == Camp.Heros) && b == Camp.Hostile);

        /// <summary>Combien de créatures sont en train d'attaquer cette cible (préparation ou frappe).</summary>
        public static int AttaquantsSur(ICible c)
        {
            int n = 0;
            foreach (var m in Actifs)
                if ((m.Etat == EtatMonstre.Preparation || m.Etat == EtatMonstre.Frappe) && ReferenceEquals(m.cible, c)) n++;
            return n;
        }

        bool PeutAttaquer() => !(cible is CombatHeros) || AttaquantsSur(cible) < maxAttaquants;

        AttaqueMonstre ChoisirAttaque(float d)
        {
            float total = 0f;
            foreach (var a in attaques) if (d >= a.porteeMin && d <= a.portee && Time.time >= a.prete) total += a.poids;
            if (total <= 0f) return null;
            float r = Random.value * total;
            foreach (var a in attaques)
            {
                if (d < a.porteeMin || d > a.portee || Time.time < a.prete) continue;
                r -= a.poids;
                if (r <= 0f) return a;
            }
            return null;
        }

        float PorteeCourte()
        {
            float p = float.MaxValue;
            foreach (var a in attaques) p = Mathf.Min(p, a.portee);
            return p == float.MaxValue ? 2f : p;
        }

        // ---------- comportements ----------

        void Errer(float dt)
        {
            if (!aPointRepos)
            {
                attenteRepos -= dt;
                if (attenteRepos > 0f) return;
                Vector2 r = Random.insideUnitCircle * rayonRepos;
                pointRepos = origine + new Vector3(r.x, 0, r.y);
                aPointRepos = true;
            }
            Vector3 vers = Plat(pointRepos - transform.position);
            if (vers.magnitude < .8f || tEtat > 12f) { Changer(EtatMonstre.Repos); attenteRepos = Random.Range(2f, 5f); return; }
            Aller(vers.normalized, vitesseMarche, dt);
        }

        void Poursuivre(float dt)
        {
            if (cible == null) { Changer(EtatMonstre.Retour); return; }
            Vector3 vers = Plat(cible.Centre - transform.position);
            float d = vers.magnitude;
            var a = ChoisirAttaque(d);
            if (a != null)
            {
                if (PeutAttaquer()) { Lancer(a); return; }
                if (d < 7f) { Changer(EtatMonstre.Encerclement); return; }
            }
            if (d > PorteeCourte() * .8f) Aller(vers.normalized, vitesseCourse, dt);
            else Tourner(vers, 400f, dt);
        }

        void Encercler(float dt)
        {
            if (cible == null) { Changer(EtatMonstre.Retour); return; }
            Vector3 depuis = Plat(transform.position - cible.Centre);
            float d = Mathf.Max(.1f, depuis.magnitude);
            if (tEtat > .8f && PeutAttaquer()) { Changer(EtatMonstre.Poursuite); return; }
            const float rayon = 4.6f;
            Vector3 tangente = Vector3.Cross(Vector3.up, depuis / d) * sensCercle;
            Vector3 v = tangente * vitesseMarche * 1.4f + (-depuis / d) * Mathf.Clamp(d - rayon, -1.5f, 3f) * 1.6f;
            vitesseVoulue = v;
            Tourner(-depuis, 360f, dt);
            if (Random.value < dt * .25f) sensCercle = -sensCercle;
        }

        void Lancer(AttaqueMonstre a)
        {
            enCours = a;
            a.prete = Time.time + a.recharge;
            coupPorte = false;
            Changer(EtatMonstre.Preparation);
            SurPreparation(a);
        }

        void Preparer(float dt)
        {
            if (cible != null && tEtat < enCours.preparation - .15f) Tourner(Plat(cible.Centre - transform.position), rotationPreparation, dt);
            if (tEtat < enCours.preparation) return;
            Changer(EtatMonstre.Frappe);
            elan = transform.forward * enCours.bond;
            if (enCours.sautVertical > 0f) vy = enCours.sautVertical;
            SurFrappe(enCours);
            if (enCours.rayonZone > 0f) FrapperZone(enCours);
        }

        void Frapper(float dt)
        {
            if (!coupPorte) TesterCoup();
            if (tEtat >= enCours.frappe) Changer(EtatMonstre.Recuperation);
        }

        void TesterCoup()
        {
            var a = enCours;
            if (cible == null || !Cibles.Existe(cible) || !cible.Vivant) return;
            // Les deux lames partent en même temps : elles se bloquent
            if (croiseLeFer && cible is CombatHeros h && h.TenterChoc(this)) { coupPorte = true; Changer(EtatMonstre.Choc); return; }
            Vector3 vers = Plat(cible.Centre - transform.position);
            if (vers.magnitude > a.portee + .5f) return;
            if (Vector3.Angle(transform.forward, vers) > a.cone * .5f) return;
            coupPorte = true;
            Porter(cible, a, vers.normalized);
        }

        void FrapperZone(AttaqueMonstre a)
        {
            coupPorte = true;
            Vector3 p = transform.position;
            Effets.OndeDeChoc(p, a.rayonZone);
            Sons.JouerA("onde", p, 1f, .8f);
            if (CombatHeros.Instance != null) CombatHeros.Instance.Secousse(Mathf.Clamp01(1f - Distance(CombatHeros.Instance) / 20f) * .15f);
            var h = CombatHeros.Instance;
            if (h != null && h.Vivant && Ennemis(camp, h.Camp) && Distance(h) <= a.rayonZone)
                Porter(h, a, Plat(h.Centre - p).normalized);
            foreach (var c in Combattant.Tous.ToArray())
                if (c != this && c.Vivant && !c.invincible && Ennemis(camp, c.Camp) && Distance(c) <= a.rayonZone)
                    Porter(c, a, Plat(c.Centre - p).normalized);
        }

        void Porter(ICible c, AttaqueMonstre a, Vector3 direction)
        {
            c.Recevoir(new Coup { degats = Attaque * a.mult, direction = direction, poussee = a.poussee, lourd = a.lourde, auteur = this, nom = a.nom });
            elan *= .2f;
            SurTouche(c, a);
        }

        void Fuir(float dt)
        {
            Vector3 loin = cible != null ? Plat(transform.position - cible.Centre) : Plat(transform.position - origine);
            if (loin.sqrMagnitude < .01f) loin = -transform.forward;
            Aller(loin.normalized, vitesseCourse * 1.1f, dt);
            if (Time.time > finFuite) { cible = null; Changer(EtatMonstre.Retour); }
        }

        void Rentrer(float dt)
        {
            Vector3 vers = Plat(origine - transform.position);
            Soigner(VieMax * .05 * dt);
            if (vers.magnitude < 2f) { Changer(EtatMonstre.Repos); return; }
            Aller(vers.normalized, vitesseMarche * 1.3f, dt);
        }

        // ---------- corps ----------

        protected void Aller(Vector3 direction, float vitesse, float dt)
        {
            vitesseVoulue = direction * vitesse;
            Tourner(direction, 420f, dt);
        }

        protected void Tourner(Vector3 direction, float degresParSeconde, float dt)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < .0001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), degresParSeconde * dt);
        }

        void Bouger(float dt)
        {
            if (!cc.enabled) return;
            Vector3 v = vitesseVoulue + elan;
            elan = Vector3.MoveTowards(elan, Vector3.zero, 14f * dt);
            foreach (var m in Actifs)   // ne pas s'empiler
            {
                if (m == this || m.Etat == EtatMonstre.Mort) continue;
                Vector3 d = Plat(transform.position - m.transform.position);
                float dist = d.magnitude, min = rayonCorps + m.rayonCorps + .3f;
                if (dist < min && dist > .01f) v += d / dist * (min - dist) * 4f;
            }
            vy = cc.isGrounded && vy < 0f ? -2f : vy - 20f * dt;
            cc.Move((v + Vector3.up * vy) * dt);
            vitesseActuelle = Plat(v).magnitude;
            phasePas += vitesseActuelle * dt * frequencePas;
            if (transform.position.y < -30f) { cc.enabled = false; transform.position = origine + Vector3.up; cc.enabled = true; }
        }

        /// <summary>Le recul d'un coup : glisse sur le sol ; contre un mur ou une paroi, c'est un choc.</summary>
        protected override void Deplacer(Vector3 pas)
        {
            if (cc == null || !cc.enabled || Etat == EtatMonstre.Mort) return;
            var flags = cc.Move(new Vector3(pas.x, 0, pas.z));
            if ((flags & CollisionFlags.Sides) != 0 && recul.magnitude > .8f && !murTouche) ImpactMur(pas.normalized);
            if (recul.magnitude < .1f) murTouche = false;
        }

        void ImpactMur(Vector3 direction)
        {
            murTouche = true;
            recul = Vector3.zero;
            Effets.Poussiere(Centre + direction * rayonCorps, 16);
            Effets.Eclat(Centre + direction * rayonCorps, new Color(.8f, .75f, .65f), 10, 3f, .12f, 1.5f, .5f, -direction);
            Sons.JouerA("impactBois", Centre, 1f, .65f);
            HUD.ChiffreTexte(Centre + Vector3.up * 1f, "Contre le mur !", new Color(1f, .68f, .4f));
            if (CombatHeros.Instance != null) CombatHeros.Instance.Secousse(.06f);
            Infliger(VieMax * .1, dernierAuteur);
            if (Vivant) Etourdir(1.3f);
        }

        public override void Recevoir(Coup c)
        {
            if (Etat == EtatMonstre.Mort) return;
            bool enPreparation = Etat == EtatMonstre.Preparation;
            base.Recevoir(c);
            if (!Vivant || Etat == EtatMonstre.Mort) return;
            if (c.auteur != null && Cibles.Existe(c.auteur) && Ennemis(camp, c.auteur.Camp))
            {
                bool nouveau = cible == null;
                cible = c.auteur;
                if (nouveau) Alerter();
                if (Etat == EtatMonstre.Repos || Etat == EtatMonstre.Retour || Etat == EtatMonstre.Encerclement) Changer(EtatMonstre.Poursuite);
            }
            // Un coup lourd ou critique interrompt une attaque annoncée (sauf les grosses créatures)
            if (enPreparation && (c.critique || c.lourd) && stature < .5f) Etourdir(.45f);
            if (fuyard && Vie < VieMax * .3 && Etat != EtatMonstre.Fuite)
            {
                Changer(EtatMonstre.Fuite);
                finFuite = Time.time + 7f;
                SurFuite();
            }
        }

        /// <summary>Fin d'un choc des lames : perdu, elle est sonnée et repoussée ; gagné, elle récupère.</summary>
        public void FinChoc(bool herosGagne, Vector3 direction)
        {
            if (Etat != EtatMonstre.Choc) return;
            if (herosGagne)
            {
                recul += Plat(direction).normalized * 2.4f / Mathf.Max(.1f, resistanceRecul);
                Etourdir(1.6f);
                Changer(EtatMonstre.Sonne);
            }
            else Changer(EtatMonstre.Recuperation);
        }

        protected override void Mourir(Coup c)
        {
            base.Mourir(c);
            Changer(EtatMonstre.Mort);
            sensChute = Random.value < .5f ? -1f : 1f;
            cc.enabled = false;
            marqueur.gameObject.SetActive(false);
            etoiles.gameObject.SetActive(false);
            if (repaire != null) repaire.Perdu(this);
            CriDeMort();
            if (c.auteur is CombatHeros h && !AutreEnnemiDuHeros(h)) h.RalentiFinal();
        }

        bool AutreEnnemiDuHeros(CombatHeros h)
        {
            foreach (var m in Actifs)
                if (m != this && m.Etat != EtatMonstre.Mort && m.camp == Camp.Hostile && ReferenceEquals(m.cible, h)
                    && (m.transform.position - h.transform.position).sqrMagnitude < 35f * 35f) return true;
            return false;
        }

        /// <summary>Tombe sur le flanc, s'enfonce, disparaît.</summary>
        protected virtual void AnimerMort(float dt)
        {
            float u = Mathf.Clamp01(tEtat / .45f);
            corps.localRotation = Quaternion.Euler(0, 0, 86f * sensChute * u * u);
            if (tEtat > 3f) corps.localPosition = Vector3.down * (tEtat - 3f) * .5f;
            if (tEtat > 5f) Destroy(gameObject);
        }

        // ---------- signes au-dessus de la tête ----------

        void ConstruireSignes()
        {
            matMarqueur = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            marqueur = Pantin.Bloc(transform, PrimitiveType.Sphere, new Vector3(0, hauteurTete + .35f, 0), Vector3.one, matMarqueur);
            marqueur.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            marqueur.gameObject.SetActive(false);
            etoiles = Pantin.Os("Etoiles", transform, new Vector3(0, hauteurTete + .22f, 0));
            var m = Pantin.Lumineux("#ffd27a");
            for (int k = 0; k < 3; k++)
            {
                float a = k * Mathf.PI * 2f / 3f;
                var e = Pantin.Bloc(etoiles, PrimitiveType.Sphere, new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (.22f + rayonCorps * .4f), Vector3.one * .09f, m);
                e.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            etoiles.gameObject.SetActive(false);
        }

        void AnimerSignes(float dt)
        {
            bool prep = Etat == EtatMonstre.Preparation && enCours != null;
            bool eclat = Etat == EtatMonstre.Frappe && tEtat < .08f;
            bool visible = prep || eclat;
            if (marqueur.gameObject.activeSelf != visible) marqueur.gameObject.SetActive(visible);
            if (visible)
            {
                float u = prep ? Mathf.Clamp01(tEtat / Mathf.Max(.01f, enCours.preparation)) : 1f;
                float taille = eclat ? .36f : Mathf.Lerp(.06f, .26f, u) * (1f + .18f * Mathf.Sin(Time.time * 30f));
                marqueur.localScale = Vector3.one * taille;
                matMarqueur.SetColor("_BaseColor", enCours.lourde ? Rouge : Jaune);
            }
            bool sonne = Etat == EtatMonstre.Sonne;
            if (etoiles.gameObject.activeSelf != sonne) etoiles.gameObject.SetActive(sonne);
            if (sonne) etoiles.Rotate(0, 260f * dt, 0, Space.Self);
        }

        // ---------- outils ----------

        protected static Vector3 Plat(Vector3 v) { v.y = 0; return v; }

        protected float Distance(ICible c) => Plat(c.Centre - transform.position).magnitude;
    }

    /// <summary>Une créature sur deux jambes (gobelin, troll, garde) : bras armé posé par l'attaque en cours.</summary>
    public abstract class MonstreBipede : Monstre
    {
        protected Pantin.Bipede b;
        protected float voute;   // dos courbé (troll)
        Quaternion brasD = Quaternion.identity, brasG = Quaternion.identity;
        protected Quaternion ReposBrasD = Quaternion.Euler(-28f, 0, 8f);
        protected bool bouclier;

        protected override void Animer(float dt)
        {
            float amp = Mathf.Clamp01(vitesseActuelle / Mathf.Max(.1f, vitesseCourse) * 1.7f);
            b.Marcher(phasePas, amp, false);
            float s = Mathf.Sin(phasePas);
            Quaternion cibleD = ReposBrasD * Quaternion.Euler(s * 18f * amp, 0, 0);
            Quaternion cibleG = bouclier ? Quaternion.Euler(-70f, -35f, 0) : Quaternion.Euler(-s * 22f * amp - 8f, 0, -8f);
            float penche = voute, lacet = 0f, roulis = 0f;
            float vitesse = 12f;
            var a = enCours;
            switch (Etat)
            {
                case EtatMonstre.Preparation:
                    float up = Mathf.SmoothStep(0, 1, Mathf.Clamp01(tEtat / a.preparation));
                    cibleD = Quaternion.Slerp(ReposBrasD, Quaternion.Euler(a.poseArmement), up);
                    if (a.deuxMains) cibleG = Quaternion.Slerp(cibleG, Quaternion.Euler(a.poseArmement.x, -a.poseArmement.y, -a.poseArmement.z), up);
                    penche = voute - 12f * up; lacet = a.poseArmement.y * .3f * up;
                    vitesse = 25f;
                    break;
                case EtatMonstre.Frappe:
                    float uf = Mathf.Clamp01(tEtat / Mathf.Max(.01f, a.frappe * .7f));
                    uf = 1f - (1f - uf) * (1f - uf);
                    cibleD = Quaternion.Slerp(Quaternion.Euler(a.poseArmement), Quaternion.Euler(a.poseFrappe), uf);
                    if (a.deuxMains) cibleG = Quaternion.Slerp(Quaternion.Euler(a.poseArmement.x, -a.poseArmement.y, -a.poseArmement.z), Quaternion.Euler(a.poseFrappe.x, -a.poseFrappe.y, -a.poseFrappe.z), uf);
                    penche = voute + 16f * uf; lacet = Mathf.Lerp(a.poseArmement.y, a.poseFrappe.y, uf) * .3f;
                    vitesse = 60f;
                    break;
                case EtatMonstre.Recuperation:
                    float ur = Mathf.Clamp01(tEtat / Mathf.Max(.01f, a.recuperation));
                    cibleD = Quaternion.Slerp(Quaternion.Euler(a.poseFrappe), ReposBrasD, ur * ur);
                    penche = voute + 16f * (1f - ur);
                    break;
                case EtatMonstre.Sonne:
                    roulis = Mathf.Sin(Time.time * 6f) * 9f; penche = voute + 8f;
                    cibleD = Quaternion.Euler(-10f, 0, 14f);
                    break;
                case EtatMonstre.Choc:
                    cibleD = Quaternion.Euler(-95f + Mathf.Sin(Time.time * 40f) * 4f, -25f, 0);
                    penche = voute + 10f;
                    vitesse = 30f;
                    break;
            }
            float k = Mathf.Clamp01(dt * vitesse);
            brasD = Quaternion.Slerp(brasD, cibleD, k);
            brasG = Quaternion.Slerp(brasG, cibleG, k);
            b.brasD.localRotation = brasD;
            b.brasG.localRotation = brasG;
            b.torse.localRotation = Quaternion.Slerp(b.torse.localRotation, Quaternion.Euler(penche, lacet, roulis), k);
            b.bassin.localPosition = new Vector3(0, .95f - Mathf.Abs(Mathf.Sin(phasePas)) * .04f * amp, 0);
        }
    }
}
