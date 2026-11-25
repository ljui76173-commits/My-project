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
        public double xpBase = 20;

        public double Vie { get; private set; }
        public double DegatsCumules { get; private set; }
        public float Etourdi { get; private set; }
        public float DernierCoup { get; private set; } = -99f;
        public event Action<Coup> Touche;
        public event Action<Coup> Mort;

        static readonly int IdCouleur = Shader.PropertyToID("_BaseColor");
        Renderer[] rendus;
        MaterialPropertyBlock bloc;
        float eclair;
        Vector3 recul;

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

        public void Etourdir(float secondes) => Etourdi = Mathf.Max(Etourdi, secondes);

        public virtual void Recevoir(Coup c)
        {
            if (!Vivant) return;
            DernierCoup = Time.time;
            double reel = Degats.Calculer(c.degats, Defense);   // le coup arrive brut, la défense s'applique ici
            c.degats = reel;
            DegatsCumules += reel;
            if (!invincible) Vie = Math.Max(0, Vie - reel);
            Eclairer(true);
            eclair = .08f;
            if (c.poussee > 0) recul += new Vector3(c.direction.x, 0, c.direction.z).normalized * c.poussee / Mathf.Max(.1f, resistanceRecul);
            if (c.lourd) Etourdir(.5f);
            HUD.ChiffreDegats(Centre + Vector3.up * .4f, c.degats, c.critique, false);
            CibleCombat.Signaler(this);
            Touche?.Invoke(c);
            if (!invincible && Vie <= 0) Mourir(c);
        }

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
