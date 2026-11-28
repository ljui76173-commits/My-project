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
    public partial class Competences : MonoBehaviour, ISauvegardable
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
        static readonly string[] DeDepart = { "epee", "masse", "mains", "parade", "esquive", "analyse", "evaluation" };

        readonly List<Maitrise> connues = new List<Maitrise>();
        readonly Dictionary<string, Maitrise> parId = new Dictionary<string, Maitrise>();
        readonly string[] raccourcis = new string[Raccourcis];
        StatsHeros stats;

        /// <summary>Une compétence vient d'être apprise.</summary>
        public static event Action<DefCompetence> Apprise;

        public IReadOnlyList<Maitrise> Connues => connues;
        public string IdSauvegarde => "competences";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reinitialiser() { Instance = null; Apprise = null; AnalyseProgresse = null; }

        void Awake()
        {
            Instance = this;
            stats = GetComponent<StatsHeros>();
            Registre.InscrireSauvegarde(this);
            foreach (var id in DeDepart) Apprendre(id, false);
            TexturesMagiques.Prechauffer();   // les cercles d'incantation, tracés au chargement plutôt qu'au premier sort
        }

        void OnDestroy()
        {
            Registre.RetirerSauvegarde(this);
            if (Instance == this) Instance = null;
        }

        // ---------- ce qu'on connaît ----------

        /// <summary>L'expérience pour passer au niveau suivant : vite au début, puis de plus en plus, sans jamais bloquer.</summary>
        public static double XPRequise(int niveau) =>
            niveau <= 10 ? 6 * Math.Pow(1.4, niveau - 1) : 6 * Math.Pow(1.4, 9) * (1 + .3 * (niveau - 10));

        public Maitrise Get(string id) => id != null && parId.TryGetValue(id, out var m) ? m : null;
        public bool Connait(string id) => id != null && parId.ContainsKey(id);
        public int Niveau(string id) => Get(id)?.niveau ?? 0;
        public bool ConnaitUnSort
        {
            get { foreach (var m in connues) if (m.Def != null && m.Def.EstSort) return true; return false; }
        }

        /// <summary>Apprend une compétence (un sort se range dans le premier raccourci libre).</summary>
        public bool Apprendre(string id, bool annoncer) => Apprendre(id, annoncer, null);

        public bool Apprendre(string id, bool annoncer, string comment)
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
                HUD.Message("NOUVELLE COMPÉTENCE", (comment != null ? comment + "\n\n" : "") + $"{d.nom}\n\n{d.description}" + (d.EstSort ? $"\n\nTouche {Touche(id)} · fenêtre Compétences (K)" : ""), 6.5f);
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
            if (m == null || quantite <= 0) return;
            m.xp += quantite;
            int avant = m.niveau;
            while (m.niveau < CatalogueCompetences.NiveauLimite && m.xp >= XPRequise(m.niveau))
            {
                m.xp -= XPRequise(m.niveau);
                m.niveau++;
            }
            if (m.niveau > avant) NiveauGagne(m);
        }

        void NiveauGagne(Maitrise m)
        {
            var d = m.Def;
            // Le niveau se voit sur le héros : une gerbe de la couleur du sort, d'autant plus grande qu'il est haut
            int visuel = m.niveau + BonusAspect;
            float a = Sorts.Ampleur(visuel);
            Vector3 p = transform.position + Vector3.up;
            Effets.Eclat(p, d.couleur, Mathf.RoundToInt(24 * a), 3f * a, .14f, -.4f, 1.2f);
            Sorts.Lueur(p, d.couleur, 6f * a, .5f);
            int pal = Sorts.Palier(visuel);
            bool nouveauPalier = d.EstSort && pal >= 2 && pal > Sorts.Palier(visuel - 1);
            string aspect = nouveauPalier ? Sorts.DescriptionPalier(d, pal) : "";
            if (m.niveau == CatalogueCompetences.NiveauMaitrise)
            {
                string evo = d.evolutions != null && d.evolutions.Length == 2
                    ? $"\n\nElle peut évoluer : <b>{CatalogueCompetences.Get(d.evolutions[0])?.nom}</b> ou <b>{CatalogueCompetences.Get(d.evolutions[1])?.nom}</b>. Ouvre tes Compétences (K)."
                    : "";
                HUD.Message($"{d.nom.ToUpper()} MAÎTRISÉE", "Niveau 10. Et ça continue : il n'y a pas de plafond." + evo + (aspect != "" && evo == "" ? $"\n\nSon aspect : {aspect}." : ""), 7f);
                Sons.Fanfare("niveau");
                VerifierFusions(true);
            }
            else if (nouveauPalier && pal >= 5)
            {
                HUD.Message($"{d.nom.ToUpper()} : {Sorts.NomPalier(pal).ToUpper()}", $"Niveau {m.niveau}.\n\nDésormais : {aspect}.", 6f);
                Sons.Fanfare("niveau");
            }
            else if (nouveauPalier)
            {
                HUD.Message($"{d.nom.ToUpper()} S'INTENSIFIE", $"Niveau {m.niveau}.\n\nPlus grand, plus vif, et désormais : {aspect}.", 5f);
                Sons.Jouer("decouverte", .7f, 1.1f);
            }
            else
            {
                HUD.Toast($"<color=#{ColorUtility.ToHtmlStringRGB(d.couleur)}>{d.nom}</color> passe au niveau {m.niveau}" + (d.EstSort ? " : plus grand, plus vif" : ""));
                Sons.Jouer("quete", .55f, 1.15f);
            }
        }

        public void FixerNiveau(string id, int niveau)
        {
            if (!Connait(id)) Apprendre(id, false);
            var m = Get(id);
            if (m == null) return;
            m.niveau = Mathf.Clamp(niveau, 1, CatalogueCompetences.NiveauLimite);
            m.xp = 0;
            VerifierFusions(true);
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

        // ---------- l'aspect des sorts : le héros surpuissant ----------

        /// <summary>Chaque niveau de héros au-delà du premier ajoute 25 niveaux à l'ASPECT de tous ses sorts (pas à leurs dégâts) :
        /// au niveau 5 du héros, ses sorts ont déjà l'allure d'un sort de niveau 100.</summary>
        public const int AspectParNiveauHeros = 25;

        public int BonusAspect => stats != null ? AspectParNiveauHeros * (stats.Niveau - 1) : 0;

        /// <summary>Le niveau qui règle l'aspect d'un sort : le sien plus l'apport du niveau du héros.</summary>
        public int NiveauVisuel(string id) => Mathf.Max(1, Niveau(id)) + BonusAspect;

        /// <summary>
        /// Appelé quand le héros monte de niveau : si ses sorts changent de palier d'aspect, une ligne pour le message de niveau.
        /// </summary>
        public string AnnonceNiveauHeros(int avant, int apres)
        {
            int meilleurAvant = 0, meilleurApres = 0;
            DefCompetence exemple = null;
            foreach (var m in connues)
            {
                var d = m.Def;
                if (d == null || !d.EstSort) continue;
                int pa = Sorts.Palier(m.niveau + AspectParNiveauHeros * (avant - 1));
                int pb = Sorts.Palier(m.niveau + AspectParNiveauHeros * (apres - 1));
                if (pa > meilleurAvant) meilleurAvant = pa;
                if (pb > meilleurApres) { meilleurApres = pb; exemple = d; }
            }
            if (exemple == null || meilleurApres <= meilleurAvant) return "";
            string nom = Sorts.NomPalier(meilleurApres);
            return $"\n\nTa magie s'élève : tes sorts deviennent « {nom} ».\nDésormais : {Sorts.DescriptionPalier(exemple, meilleurApres)}.";
        }

        // ---------- ce que chaque niveau apporte ----------

        public static double Progression(int niveau) => 1 + .12 * (Mathf.Max(1, niveau) - 1);

        // Le coût et la recharge baissent à chaque niveau sans jamais atteindre zéro ; le rayon suit la taille du sort
        public float Cout(DefCompetence d) => Cout(d, Niveau(d.id));
        public float Recharge(DefCompetence d) => Recharge(d, Niveau(d.id));
        public static float Cout(DefCompetence d, int niveau) => d.cout / (1f + .035f * (Mathf.Max(1, niveau) - 1));
        public static float Recharge(DefCompetence d, int niveau) => d.recharge / (1f + .045f * (Mathf.Max(1, niveau) - 1));
        public float Rayon(DefCompetence d) => d.rayon * (1f + .4f * (Sorts.Ampleur(Niveau(d.id)) - 1f));

        /// <summary>Les niveaux utiles d'un bonus qui ralentit après la maîtrise (pleine pente jusqu'au 10, un quart ensuite).</summary>
        static float Pente(int niveau) => Mathf.Min(niveau - 1, 9) + .25f * Mathf.Max(0, niveau - 10);

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
        public double AttaqueMagique(DefCompetence d) => AttaqueMagique(d, Niveau(d.id));
        public double AttaqueMagique(DefCompetence d, int niveau) => (Valeur(Stat.INT) + .4 * Valeur(Stat.ESP)) * d.puissance * Progression(niveau);

        /// <summary>Ce que rend un soin : l'Esprit surtout, l'Intelligence aussi.</summary>
        public double Soin(DefCompetence d) => Soin(d, Niveau(d.id));
        public double Soin(DefCompetence d, int niveau) => (2 * Valeur(Stat.ESP) + Valeur(Stat.INT)) * d.puissance * Progression(niveau);

        /// <summary>Le bonus de dégâts au corps à corps selon l'arme en main.</summary>
        public double BonusArme(DefObjet arme)
        {
            if (arme == null) return 1 + .07 * (Mathf.Max(1, Niveau("mains")) - 1);
            string id = arme.forme == FormeArme.Gourdin ? "masse" : "epee";
            return 1 + .06 * (Mathf.Max(1, Niveau(id)) - 1);
        }

        public static string IdArme(DefObjet arme) => arme == null ? "mains" : arme.forme == FormeArme.Gourdin ? "masse" : "epee";

        public float FacteurParade => (1f + .06f * Pente(Mathf.Max(1, Niveau("parade")))) * (1f + Exploits.Bonus(TypeBonus.Parade));
        public float FacteurCoutGarde => 1f / (1f + .045f * (Mathf.Max(1, Niveau("parade")) - 1));
        public float FacteurEsquive => 1f + .04f * Pente(Mathf.Max(1, Niveau("esquive")));
        public float FacteurCoutRoulade => 1f / (1f + .045f * (Mathf.Max(1, Niveau("esquive")) - 1)) * (1f - Exploits.Bonus(TypeBonus.Roulade));

        // ---------- sauvegarde ----------

        [Serializable]
        class Etat
        {
            public List<Maitrise> connues;
            public string[] raccourcis;
            public List<string> analyseIds = new List<string>();
            public List<float> analyseValeurs = new List<float>();
            public List<int> resistanceElements = new List<int>();
            public List<float> resistanceValeurs = new List<float>();
            public List<string> especesEvaluees = new List<string>();
            public List<string> objetsEvalues = new List<string>();
        }

        public string Ecrire()
        {
            var e = new Etat { connues = new List<Maitrise>(connues), raccourcis = (string[])raccourcis.Clone() };
            foreach (var kv in analyses) { e.analyseIds.Add(kv.Key); e.analyseValeurs.Add(kv.Value); }
            foreach (var kv in resistances) { e.resistanceElements.Add((int)kv.Key); e.resistanceValeurs.Add(kv.Value); }
            e.especesEvaluees.AddRange(especesEvaluees);
            e.objetsEvalues.AddRange(objetsEvalues);
            return JsonUtility.ToJson(e);
        }

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
                    ici.niveau = Mathf.Clamp(m.niveau, 1, CatalogueCompetences.NiveauLimite);
                    ici.xp = m.xp;
                }
            Trier();
            for (int k = 0; k < Raccourcis; k++)
                raccourcis[k] = e.raccourcis != null && k < e.raccourcis.Length && Connait(e.raccourcis[k]) ? e.raccourcis[k] : null;
            analyses.Clear();
            if (e.analyseIds != null && e.analyseValeurs != null)
                for (int k = 0; k < e.analyseIds.Count && k < e.analyseValeurs.Count; k++)
                    if (CatalogueCompetences.Get(e.analyseIds[k]) != null && !Connait(e.analyseIds[k])) analyses[e.analyseIds[k]] = Mathf.Clamp01(e.analyseValeurs[k]);
            resistances.Clear();
            if (e.resistanceElements != null && e.resistanceValeurs != null)
                for (int k = 0; k < e.resistanceElements.Count && k < e.resistanceValeurs.Count; k++)
                    resistances[(Element)e.resistanceElements[k]] = Mathf.Clamp(e.resistanceValeurs[k], 0f, ResistanceMax);
            especesEvaluees.Clear();
            if (e.especesEvaluees != null) foreach (var x in e.especesEvaluees) especesEvaluees.Add(x);
            objetsEvalues.Clear();
            if (e.objetsEvalues != null) foreach (var x in e.objetsEvalues) objetsEvalues.Add(x);
            fusionsAnnoncees.Clear();
            VerifierFusions(false);
        }
    }
}
