using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// Ce qui se bat dans le monde (monstres, PNJ armés, quintaine) : vie, attaque et défense
    /// qui suivent le niveau (×1,05 par niveau pour la population), étourdissement, recul,
    /// éclair blanc à l'impact, chiffres de dégâts, XP au héros quand il l'abat.
    /// </summary>
    public class Combattant : MonoBehaviour, ICible
    {
        public static readonly List<Combattant> Tous = new List<Combattant>();

        public string nom = "Créature";
        public int niveau = 1;
        public Camp camp = Camp.Hostile;
        [Tooltip("Vie, attaque et défense au niveau 1 ; ×1,05 par niveau ensuite.")]
        public double vieBase = 30, attaqueBase = 5, defenseBase = 2;
        public float hauteurCentre = 1f;
        [Tooltip("Ne meurt jamais (quintaine, mannequin) : on compte les dégâts.")]
        public bool invincible;
        public float resistanceRecul = 1f;
        [Tooltip("0 : un coup lourd le fait chanceler ; 1 : rien ne l'ébranle (troll).")]
        public float stature;
        [Tooltip("Ne tombe jamais sous 1 point de vie (gardes) : il est mis à terre.")]
        public bool increvable;
        public double xpBase = 20;

        /// <summary>Brûlure, gel : ce qui ronge ou ralentit ce combattant.</summary>
        public Alterations Alterations { get; } = new Alterations();
        readonly Dictionary<Element, float> affinites = new Dictionary<Element, float>();
        float prochaineFlamme;
        GameObject gangue;

        public double Vie { get; private set; }
        public double DegatsCumules { get; private set; }
        public float Etourdi { get; private set; }
        /// <summary>Sonné pour de bon (parade parfaite, choc gagné, mur) : le coup de grâce est possible.</summary>
        public bool Vulnerable => Etourdi > .15f && dureeEtourdi >= 1f && Vivant && !invincible;
        public float DernierCoup { get; private set; } = -99f;
        public event Action<Coup> Touche;
        public event Action<Coup> Mort;

        static readonly int IdCouleur = Shader.PropertyToID("_BaseColor");
        Renderer[] rendus;
        MaterialPropertyBlock bloc;
        float eclair, dureeEtourdi;
        protected Vector3 recul;
        protected ICible dernierAuteur;
        float protegeJusqua;

        /// <summary>Intouchable un instant (une gelée qui vient de naître ne meurt pas dans le coup qui a tué sa mère).</summary>
        public void Proteger(float secondes) => protegeJusqua = Time.time + secondes;
        public bool Protege => Time.time < protegeJusqua;

        public double Facteur => Math.Pow(StatsHeros.CroissanceMonde, niveau - 1);
        public double VieMax => vieBase * Facteur;
        public double Attaque => attaqueBase * Facteur;
        public double Defense => defenseBase * Facteur;

        public Camp Camp => camp;
        public bool Vivant => invincible || Vie > 0;
        public Vector3 Centre => transform.position + Vector3.up * hauteurCentre * transform.lossyScale.y;
        public string NomAffiche => nom;
        public int NiveauAffiche => niveau;
        public double VieActuelle => Vie;
        public double VieMaximale => VieMax;
        public Transform Corps => transform;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reinitialiser() => Tous.Clear();

        protected virtual void Awake() { Vie = VieMax; bloc = new MaterialPropertyBlock(); }
        protected virtual void OnEnable() => Tous.Add(this);
        protected virtual void OnDisable() => Tous.Remove(this);
        protected virtual void Start() => rendus = GetComponentsInChildren<Renderer>();

        protected virtual void Update()
        {
            float dt = Time.deltaTime;
            if (Etourdi > 0) Etourdi -= dt;
            if (Alterations.Actif && Vivant) MettreAJourAlterations(dt);
            if (eclair > 0)
            {
                eclair -= Time.unscaledDeltaTime;
                if (eclair <= 0) Eclairer(false);
            }
            if (recul.sqrMagnitude > .0001f)
            {
                Vector3 pas = recul * Mathf.Min(1f, dt * 12f);
                recul -= pas;
                Deplacer(pas);
            }
        }

        /// <summary>Pousse le corps (les monstres avec un contrôleur de déplacement le surchargent).</summary>
        protected virtual void Deplacer(Vector3 pas)
        {
            if (invincible) return;
            var p = transform.position + new Vector3(pas.x, 0, pas.z);
            p.y = Mathf.Max(Monde.Hauteur(p.x, p.z), -1.2f);
            transform.position = p;
        }

        public void Etourdir(float secondes)
        {
            if (secondes > Etourdi) { Etourdi = secondes; dureeEtourdi = secondes; }
        }

        /// <summary>Fixe le niveau (à l'apparition) et remet la vie au maximum.</summary>
        public void FixerNiveau(int n) { niveau = Mathf.Max(1, n); Vie = VieMax; }

        protected void Soigner(double q) => Vie = Math.Min(VieMax, Vie + q);

        /// <summary>Dégâts directs, sans défense (choc contre un mur, chute).</summary>
        public void Infliger(double degats, ICible auteur)
        {
            if (!Vivant || invincible) { DegatsCumules += degats; return; }
            DegatsCumules += degats;
            Vie = Math.Max(increvable ? 1 : 0, Vie - degats);
            HUD.ChiffreDegats(Centre + Vector3.up * .5f, degats, false, false);
            if (Vie <= 0) Mourir(new Coup { degats = degats, auteur = auteur, nom = "choc" });
        }

        /// <summary>×2 : faible à cet élément ; ×0,5 : résistant ; 0 : immunisé.</summary>
        public float Affinite(Element e) => e != Element.Aucun && affinites.TryGetValue(e, out var a) ? a : 1f;
        protected void FixerAffinite(Element e, float multiplicateur) => affinites[e] = multiplicateur;

        public virtual void Recevoir(Coup c)
        {
            if (!Vivant) return;
            if (Protege) return;
            DernierCoup = Time.time;
            if (c.auteur != null) dernierAuteur = c.auteur;
            // Le coup arrive brut, la défense s'applique ici (à moitié contre la magie), puis la faiblesse ou la résistance à l'élément
            float aff = Affinite(c.element);
            double reel = Degats.Calculer(c.degats, c.magique ? Defense * .5 : Defense) * aff;
            c.degats = reel;
            DegatsCumules += reel;
            if (!invincible) Vie = Math.Max(increvable ? 1 : 0, Vie - reel);
            Eclairer(true);
            eclair = .08f;
            if (c.poussee > 0) recul += new Vector3(c.direction.x, 0, c.direction.z).normalized * c.poussee / Mathf.Max(.1f, resistanceRecul);
            if (c.lourd && stature < 1f) Etourdir(.5f * (1f - stature));
            HUD.ChiffreDegats(Centre + Vector3.up * .4f, c.degats, c.critique, false);
            if (c.element != Element.Aucun)
            {
                if (aff >= 1.4f) HUD.ChiffreTexte(Centre + Vector3.up * 1.05f, "Faible !", new Color(1f, .62f, .3f));
                else if (aff <= 0f) HUD.ChiffreTexte(Centre + Vector3.up * 1.05f, "Immunisé", new Color(.7f, .7f, .75f));
                else if (aff <= .6f) HUD.ChiffreTexte(Centre + Vector3.up * 1.05f, "Résiste", new Color(.7f, .7f, .75f));
            }
            if (c.auteur is CombatHeros) CibleCombat.Signaler(this);
            Touche?.Invoke(c);
            if (!invincible && Vie <= 0) { Mourir(c); return; }
            if (c.effet != Effet.Aucun && aff > .5f) SubirEffet(c, aff);
        }

        void SubirEffet(Coup c, float aff)
        {
            switch (c.effet)
            {
                case Effet.Choc:
                    if (stature >= 1f && c.dureeEffet < 1f) break;   // un troll ne vacille pas pour une étincelle
                    Etourdir(c.dureeEffet * (1f - stature * .5f));
                    HUD.ChiffreTexte(Centre + Vector3.up * 1.3f, "Étourdi", new Color(1f, .95f, .5f));
                    break;
                case Effet.GelNet:
                    if (invincible) break;
                    float dg = c.dureeEffet * (stature >= 1f ? .5f : 1f);
                    Alterations.GelerNet(dg);
                    Etourdir(dg);
                    HUD.ChiffreTexte(Centre + Vector3.up * 1.3f, "Gelé !", new Color(.65f, .9f, 1f));
                    Gangue(dg);
                    break;
                case Effet.Brulure:
                case Effet.Gel:
                    if (invincible) break;
                    if (Alterations.Appliquer(c.effet, c.degats, c.dureeEffet, c.auteur))
                    {
                        Etourdir(Alterations.Gele);
                        HUD.ChiffreTexte(Centre + Vector3.up * 1.3f, "Gelé !", new Color(.65f, .9f, 1f));
                        Gangue(Alterations.Gele);
                    }
                    break;
            }
        }

        void MettreAJourAlterations(float dt)
        {
            double brule = Alterations.Avancer(dt);
            if (brule > 0)
            {
                DegatsCumules += brule;
                if (!invincible) Vie = Math.Max(increvable ? 1 : 0, Vie - brule);
                HUD.ChiffreTexte(Centre + Vector3.up * .5f, Nombres.Court(brule), new Color(1f, .6f, .25f));
                if (Vie <= 0 && !invincible) { Mourir(new Coup { degats = brule, auteur = Alterations.Auteur, nom = "brûlure" }); return; }
            }
            if (Time.time < prochaineFlamme) return;
            Vector3 p = Centre + UnityEngine.Random.insideUnitSphere * .3f;
            if (Alterations.Brulure > 0f)
            {
                prochaineFlamme = Time.time + .12f;
                Effets.Eclat(p, new Color(1f, UnityEngine.Random.Range(.35f, .65f), .1f, .9f), 3, 1.2f, .16f, -.7f, .45f);
            }
            else if (Alterations.Gel > 0f && Alterations.Gele <= 0f)
            {
                prochaineFlamme = Time.time + .3f;
                Effets.Eclat(p, new Color(.75f, .92f, 1f, .9f), 2, .6f, .08f, .25f, .7f);
            }
        }

        /// <summary>Une gangue de glace autour du corps, le temps du gel.</summary>
        void Gangue(float duree)
        {
            if (gangue != null) Destroy(gangue);
            if (rendus == null || rendus.Length == 0) return;
            var b = new Bounds(Centre, Vector3.zero);
            foreach (var r in rendus) if (r != null && r.enabled) b.Encapsulate(r.bounds);
            gangue = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(gangue.GetComponent<Collider>());
            gangue.name = "Gangue";
            gangue.transform.SetParent(transform, true);
            gangue.transform.position = b.center;
            gangue.transform.localScale = Vector3.Scale(b.size * 1.15f, new Vector3(1f / transform.lossyScale.x, 1f / transform.lossyScale.y, 1f / transform.lossyScale.z));
            var mr = gangue.GetComponent<MeshRenderer>();
            mr.sharedMaterial = Sorts.Materiau(new Color(.7f, .9f, 1f, .42f));
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Effets.Eclat(b.center, new Color(.8f, .95f, 1f), 18, 2.5f, .12f, 1f, .6f);
            Sons.JouerA("sortGlace", b.center, .8f, .7f);
            Destroy(gangue, duree);
        }

        /// <summary>Rend de la vie (un soin, une créature qui se régénère).</summary>
        public void Rendre(double q) { if (Vivant) Soigner(q); }

        protected virtual void Mourir(Coup c)
        {
            Mort?.Invoke(c);
            if (c.auteur != null && c.auteur.Camp == Camp.Heros && StatsHeros.Instance != null)
                StatsHeros.Instance.GagnerXP(xpBase * Math.Pow(1.13, niveau), nom);
        }

        void Eclairer(bool blanc)
        {
            if (rendus == null) return;
            bloc.Clear();
            if (blanc) bloc.SetColor(IdCouleur, Color.white);
            foreach (var r in rendus) if (r != null) r.SetPropertyBlock(bloc);   // un bloc vide rend la couleur d'origine
        }
    }

    /// <summary>La dernière cible touchée ou verrouillée, que le HUD affiche sous la boussole.</summary>
    public static class CibleCombat
    {
        public static ICible Courante { get; private set; }
        public static float Depuis { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reinitialiser() { Courante = null; Depuis = 0; }

        public static void Signaler(ICible c) { Courante = c; Depuis = Time.time; }
    }
}
