using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// Les compétences du héros : celles qu'il connaît, leur niveau (1 à 10) qui monte à force de s'en servir,
    /// la recharge des sorts, et les 4 raccourcis (touches 1 à 4).
    /// Chaque niveau rend la compétence plus forte : dégâts, coût, recharge, ou le bonus propre à une technique.
    /// </summary>
    public class Competences : MonoBehaviour, ISauvegardable
    {
        public static Competences Instance { get; private set; }

        [Serializable]
        public class Maitrise
        {
            public string id;
            public int niveau = 1;
            public double xp;
            [NonSerialized] public float pret;
            public DefCompetence Def => CatalogueCompetences.Get(id);
        }

        public const int Raccourcis = 4;
        /// <summary>Les techniques qu'on a dès le départ : elles montent en se battant.</summary>
        static readonly string[] DeDepart = { "epee", "masse", "mains", "parade", "esquive" };

        readonly List<Maitrise> connues = new List<Maitrise>();
        readonly Dictionary<string, Maitrise> parId = new Dictionary<string, Maitrise>();
        readonly string[] raccourcis = new string[Raccourcis];
        StatsHeros stats;

        /// <summary>Une compétence vient d'être apprise.</summary>
        public static event Action<DefCompetence> Apprise;

        public IReadOnlyList<Maitrise> Connues => connues;
        public string IdSauvegarde => "competences";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reinitialiser() { Instance = null; Apprise = null; }

        void Awake()
        {
            Instance = this;
            stats = GetComponent<StatsHeros>();
            Registre.InscrireSauvegarde(this);
            foreach (var id in DeDepart) Apprendre(id, false);
        }

        void OnDestroy()
        {
            Registre.RetirerSauvegarde(this);
            if (Instance == this) Instance = null;
        }

        // ---------- ce qu'on connaît ----------

        public static double XPRequise(int niveau) => 6 * Math.Pow(1.4, niveau - 1);

        public Maitrise Get(string id) => id != null && parId.TryGetValue(id, out var m) ? m : null;
        public bool Connait(string id) => id != null && parId.ContainsKey(id);
        public int Niveau(string id) => Get(id)?.niveau ?? 0;
        public bool ConnaitUnSort
        {
            get { foreach (var m in connues) if (m.Def != null && m.Def.EstSort) return true; return false; }
        }

        /// <summary>Apprend une compétence (un sort se range dans le premier raccourci libre).</summary>
        public bool Apprendre(string id, bool annoncer)
        {
            var d = CatalogueCompetences.Get(id);
            if (d == null || Connait(id)) return false;
            var m = new Maitrise { id = id };
            connues.Add(m);
            parId[id] = m;
            Trier();
            if (d.EstSort)
                for (int k = 0; k < Raccourcis; k++)
                    if (string.IsNullOrEmpty(raccourcis[k])) { raccourcis[k] = id; break; }
            if (annoncer)
            {
                HUD.Message("NOUVELLE COMPÉTENCE", $"{d.nom}\n\n{d.description}" + (d.EstSort ? $"\n\nTouche {Touche(id)} · fenêtre Compétences (K)" : ""), 6f);
                Sons.Jouer("decouverte", .8f, 1f);
                Effets.Eclat(transform.position + Vector3.up, d.couleur, 36, 3.5f, .14f, -.3f, 1.3f);
            }
            Apprise?.Invoke(d);
            return true;
        }

        void Trier()
        {
            connues.Sort((a, b) => CatalogueCompetences.Ordre.IndexOf(a.Def).CompareTo(CatalogueCompetences.Ordre.IndexOf(b.Def)));
        }

        /// <summary>L'usage fait progresser : un sort lancé, un coup porté, une parade.</summary>
        public void Exercer(string id, double quantite)
        {
            var m = Get(id);
            if (m == null || m.niveau >= CatalogueCompetences.NiveauMax || quantite <= 0) return;
            m.xp += quantite;
            int avant = m.niveau;
            while (m.niveau < CatalogueCompetences.NiveauMax && m.xp >= XPRequise(m.niveau))
            {
                m.xp -= XPRequise(m.niveau);
                m.niveau++;
            }
            if (m.niveau >= CatalogueCompetences.NiveauMax) m.xp = 0;
            if (m.niveau > avant) NiveauGagne(m);
        }

        void NiveauGagne(Maitrise m)
        {
            var d = m.Def;
            if (m.niveau >= CatalogueCompetences.NiveauMax)
            {
                HUD.Message($"{d.nom.ToUpper()} MAÎTRISÉE", "Niveau 10 : le maximum.", 4.5f);
                Sons.Fanfare("niveau");
            }
            else
            {
                HUD.Toast($"<color=#{ColorUtility.ToHtmlStringRGB(d.couleur)}>{d.nom}</color> passe au niveau {m.niveau}");
                Sons.Jouer("quete", .55f, 1.15f);
            }
        }

        public void FixerNiveau(string id, int niveau)
        {
            if (!Connait(id)) Apprendre(id, false);
            var m = Get(id);
            if (m == null) return;
            m.niveau = Mathf.Clamp(niveau, 1, CatalogueCompetences.NiveauMax);
            m.xp = 0;
        }

        // ---------- raccourcis ----------

        public string Raccourci(int k) => k >= 0 && k < Raccourcis ? raccourcis[k] : null;

        /// <summary>Range une compétence dans un raccourci (si elle était déjà ailleurs, les deux échangent).</summary>
        public void Placer(int k, string id)
        {
            if (k < 0 || k >= Raccourcis) return;
            int ancien = Array.IndexOf(raccourcis, id);
            if (ancien >= 0) raccourcis[ancien] = raccourcis[k];
            raccourcis[k] = id;
        }

        public int Touche(string id) => Array.IndexOf(raccourcis, id) + 1;

        // ---------- ce que chaque niveau apporte ----------

        public static double Progression(int niveau) => 1 + .12 * (Mathf.Max(1, niveau) - 1);

        public float Cout(DefCompetence d) => d.cout * (1f - .03f * (Mathf.Max(1, Niveau(d.id)) - 1));
        public float Recharge(DefCompetence d) => d.recharge * (1f - .04f * (Mathf.Max(1, Niveau(d.id)) - 1));
        public float Rayon(DefCompetence d) => d.rayon * (1f + .04f * (Mathf.Max(1, Niveau(d.id)) - 1));

        /// <summary>Temps restant avant de pouvoir relancer (0 : prêt).</summary>
        public float Attente(string id)
        {
            var m = Get(id);
            return m == null ? 0f : Mathf.Max(0f, m.pret - Time.time);
        }

        public void Declencher(DefCompetence d)
        {
            var m = Get(d.id);
            if (m != null) m.pret = Time.time + Recharge(d);
        }

        double Valeur(Stat s) => stats != null ? stats.Valeur(s) : 5;

        /// <summary>L'attaque magique d'un sort : l'Intelligence surtout, un peu d'Esprit, et le niveau du sort.</summary>
        public double AttaqueMagique(DefCompetence d) => (Valeur(Stat.INT) + .4 * Valeur(Stat.ESP)) * d.puissance * Progression(Niveau(d.id));

        /// <summary>Ce que rend un soin : l'Esprit surtout, l'Intelligence aussi.</summary>
        public double Soin(DefCompetence d) => (2 * Valeur(Stat.ESP) + Valeur(Stat.INT)) * d.puissance * Progression(Niveau(d.id));

        /// <summary>Le bonus de dégâts au corps à corps selon l'arme en main.</summary>
        public double BonusArme(DefObjet arme)
        {
            if (arme == null) return 1 + .07 * (Mathf.Max(1, Niveau("mains")) - 1);
            string id = arme.forme == FormeArme.Gourdin ? "masse" : "epee";
            return 1 + .06 * (Mathf.Max(1, Niveau(id)) - 1);
        }

        public static string IdArme(DefObjet arme) => arme == null ? "mains" : arme.forme == FormeArme.Gourdin ? "masse" : "epee";

        public float FacteurParade => 1f + .06f * (Mathf.Max(1, Niveau("parade")) - 1);
        public float FacteurCoutGarde => 1f - .04f * (Mathf.Max(1, Niveau("parade")) - 1);
        public float FacteurEsquive => 1f + .04f * (Mathf.Max(1, Niveau("esquive")) - 1);
        public float FacteurCoutRoulade => 1f - .04f * (Mathf.Max(1, Niveau("esquive")) - 1);

        // ---------- sauvegarde ----------

        [Serializable]
        class Etat
        {
            public List<Maitrise> connues;
            public string[] raccourcis;
        }

        public string Ecrire() => JsonUtility.ToJson(new Etat { connues = new List<Maitrise>(connues), raccourcis = (string[])raccourcis.Clone() });

        public void Lire(string json)
        {
            var e = JsonUtility.FromJson<Etat>(json);
            if (e == null) return;
            if (e.connues != null)
                foreach (var m in e.connues)
                {
                    if (m == null || CatalogueCompetences.Get(m.id) == null) continue;
                    var ici = Get(m.id);
                    if (ici == null)
                    {
                        ici = new Maitrise { id = m.id };
                        connues.Add(ici);
                        parId[m.id] = ici;
                    }
                    ici.niveau = Mathf.Clamp(m.niveau, 1, CatalogueCompetences.NiveauMax);
                    ici.xp = m.xp;
                }
            Trier();
            for (int k = 0; k < Raccourcis; k++)
                raccourcis[k] = e.raccourcis != null && k < e.raccourcis.Length && Connait(e.raccourcis[k]) ? e.raccourcis[k] : null;
        }
    }
}
