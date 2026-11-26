using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    public enum Stat { FOR, AGI, END, INT, ESP, PER }

    /// <summary>
    /// Les stats du héros, sans plafond : chaque stat = (base + points + entraînement) × 1,09^(niveau − 1).
    /// L'entraînement monte en faisant (courir, sauter, explorer, dépenser du mana…) ; les points se gagnent
    /// à chaque niveau (3 par niveau) et se répartissent dans la fenêtre de Statut.
    /// Le mana reste porté par JoueurAldric ; son maximum vient d'ici.
    /// </summary>
    [RequireComponent(typeof(JoueurAldric))]
    public class StatsHeros : MonoBehaviour, ISauvegardable
    {
        public const double Croissance = 1.09, CroissanceMonde = 1.05;
        public const int PointsParNiveau = 3;
        public const float TailleCellule = 60f;
        public static readonly string[] Noms = { "Force", "Agilité", "Endurance", "Intelligence", "Esprit", "Perception" };
        public static readonly string[] Abreges = { "FOR", "AGI", "END", "INT", "ESP", "PER" };
        public static readonly string[] Roles =
        {
            "Dégâts physiques, charge maximale",
            "Vitesse, saut, coups critiques",
            "Vie, endurance",
            "Dégâts magiques, Analyse des sorts",
            "Mana et sa régénération",
            "Analyse, Évaluation, repérage",
        };
        public static readonly string[] Entrainement =
        {
            "frapper, porter lourd (bientôt)",
            "courir, sauter",
            "marcher, courir longtemps",
            "lancer des sorts, toucher avec",
            "dépenser du mana",
            "explorer de nouveaux lieux",
        };

        public static StatsHeros Instance { get; private set; }

        public string nomHeros = "Aldric";
        public double baseStat = 5;

        public int Niveau { get; private set; } = 1;
        public double XP { get; private set; }
        public int PointsLibres { get; private set; }
        public double Vie { get; private set; }
        public double Endurance { get; private set; }
        public bool Essouffle { get; private set; }
        public int ZonesExplorees => cellules.Count;
        public string Titre { get; private set; } = "Nouveau venu";

        readonly double[] points = new double[6], usage = new double[6], progres = new double[6];
        readonly HashSet<int> cellules = new HashSet<int>();
        JoueurAldric joueur;
        float repitEndurance, repitVie;

        public string IdSauvegarde => "heros";

        // ---------- valeurs ----------

        public double Facteur => Math.Pow(Croissance, Niveau - 1);
        public double Valeur(Stat s) => (baseStat + points[(int)s] + usage[(int)s]) * Facteur;
        public double Points(Stat s) => points[(int)s];
        public double Usage(Stat s) => usage[(int)s];
        public double Progres(Stat s) => progres[(int)s];

        public double VieMax => 20 + 8 * Valeur(Stat.END) + 2 * Valeur(Stat.FOR);
        public double ManaMax => 10 + 4 * Valeur(Stat.ESP) + Valeur(Stat.INT);
        public double EnduranceMax => 40 + 5 * Valeur(Stat.END) + 3 * Valeur(Stat.AGI);
        public double RegenMana => ManaMax * .012;
        public double ChargeMax => 20 + 2 * Valeur(Stat.FOR);
        /// <summary>La vitesse suit l'agilité, mais lentement (logarithme), et plafonne à ×3.</summary>
        public float MultiplicateurVitesse => Mathf.Min(3f, 1f + .12f * Mathf.Log(Mathf.Max(1f, (float)(Valeur(Stat.AGI) / baseStat))));
        public float MultiplicateurSaut => Mathf.Min(1.8f, 1f + .05f * Mathf.Log(Mathf.Max(1f, (float)(Valeur(Stat.AGI) / baseStat))));

        public static double XPRequise(int niveau) => 100 * Math.Pow(1.12, niveau - 1);
        /// <summary>Ce que coûte le prochain point d'entraînement : de plus en plus long.</summary>
        public static double CoutEntrainement(double dejaGagnes) => 40 * Math.Pow(1.15, dejaGagnes);

        // ---------- cycle de vie ----------

        void Awake()
        {
            Instance = this;
            joueur = GetComponent<JoueurAldric>();
            Registre.InscrireSauvegarde(this);
            Vie = VieMax;
            Endurance = EnduranceMax;
        }

        void OnDestroy()
        {
            Registre.RetirerSauvegarde(this);
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (!MondeRPG.Actif || HUD.MenuOuvert) return;

            // Endurance : repart après un court répit
            if (repitEndurance > 0) repitEndurance -= dt;
            else Endurance = Math.Min(EnduranceMax, Endurance + EnduranceMax * .25 * dt);
            if (Essouffle && Endurance >= EnduranceMax * .25) Essouffle = false;

            // Vie : se referme doucement hors combat
            if (repitVie > 0) repitVie -= dt;
            else Vie = Math.Min(VieMax, Vie + VieMax * .01 * dt);

            // Exploration : chaque zone de 60 m découverte la première fois
            var p = transform.position;
            int cle = Mathf.FloorToInt(p.x / TailleCellule) * 10007 + Mathf.FloorToInt(p.z / TailleCellule);
            if (cellules.Add(cle) && cellules.Count > 1)
            {
                GagnerXP(4, "zone explorée");
                Exercer(Stat.PER, 3);
            }
        }

        // ---------- dépenses ----------

        /// <summary>Courir coûte de l'endurance ; essoufflé, on ne peut plus courir jusqu'à 25 %.</summary>
        public bool PeutCourir(float dt)
        {
            if (Essouffle) return false;
            Endurance -= 14 * dt;
            repitEndurance = .8f;
            if (Endurance <= 0) { Endurance = 0; Essouffle = true; HUD.Toast("Essoufflé"); return false; }
            return true;
        }

        public bool PeutSauter()
        {
            if (Endurance < 8) return false;
            Endurance -= 8;
            repitEndurance = .8f;
            Exercer(Stat.AGI, .5);
            return true;
        }

        /// <summary>
        /// Une action de combat coûte de l'endurance (coup, roulade, garde qui encaisse).
        /// Possible tant qu'il en reste ; tombé à zéro, on est essoufflé. Renvoie faux s'il n'en restait plus.
        /// </summary>
        public bool Depenser(double q)
        {
            if (Endurance <= 0) return false;
            Endurance = Math.Max(0, Endurance - q);
            repitEndurance = 1f;
            if (Endurance <= 0) { Essouffle = true; HUD.Toast("Essoufflé"); }
            return true;
        }

        /// <summary>Appelé par le joueur à chaque image où il avance au sol.</summary>
        public void Deplacement(float distance, bool course)
        {
            Exercer(Stat.END, distance * (course ? .02 : .006));
            if (course) Exercer(Stat.AGI, distance * .01);
        }

        public void ManaDepense(float q) => Exercer(Stat.ESP, q * .5);

        public void Blesser(double degats)
        {
            Vie = Math.Max(0, Vie - degats);
            repitVie = 8f;
        }

        /// <summary>Rend de la vie (nourriture, potion).</summary>
        public void Rendre(double vie) => Vie = Math.Min(VieMax, Vie + Math.Max(0, vie));

        public void Soigner() { Vie = VieMax; Endurance = EnduranceMax; Essouffle = false; joueur.FixerMana((float)ManaMax); }

        // ---------- progression ----------

        /// <summary>L'entraînement : une stat progresse à force d'être utilisée.</summary>
        public void Exercer(Stat s, double quantite)
        {
            int i = (int)s;
            progres[i] += quantite / CoutEntrainement(usage[i]);
            while (progres[i] >= 1)
            {
                progres[i] -= 1;
                usage[i] += 1;
                HUD.Toast($"{Noms[i]} +1 (entraînement)");
                Sons.Jouer("clic", .5f, 1.6f);
            }
        }

        public void GagnerXP(double q, string raison = null)
        {
            if (q <= 0) return;
            XP += q;
            if (!string.IsNullOrEmpty(raison)) HUD.Toast($"+{Nombres.Court(q)} XP · {raison}");
            int gagnes = 0;
            while (XP >= XPRequise(Niveau))
            {
                XP -= XPRequise(Niveau);
                Niveau++;
                PointsLibres += PointsParNiveau;
                gagnes++;
            }
            if (gagnes > 0) MonteeDeNiveau(gagnes);
        }

        void MonteeDeNiveau(int gagnes)
        {
            Soigner();
            Effets.Eclat(transform.position + Vector3.up, new Color(1f, .85f, .45f), 40, 4f, .16f, -.35f, 1.4f);
            HUD.EclairCombat(new Color(1f, .85f, .45f, .22f), .9f, false);
            Sons.Fanfare("niveau");
            HUD.Message($"NIVEAU {Niveau}",
                $"Toutes tes stats {Nombres.Facteur(Math.Pow(Croissance, gagnes))}\n+{PointsParNiveau * gagnes} points à répartir : ouvre ton Statut (C)\n\nVie, mana et endurance restaurés.", 6f);
            if (Niveau >= 5 && Titre == "Nouveau venu") Titre = "Aventurier en herbe";
        }

        public bool Repartir(Stat s)
        {
            if (PointsLibres <= 0) return false;
            PointsLibres--;
            points[(int)s] += 1;
            Sons.Jouer("clic", .7f, 1.2f);
            return true;
        }

        /// <summary>Console : fixe le niveau directement (les points correspondants sont ajoutés).</summary>
        public void FixerNiveau(int n)
        {
            n = Mathf.Clamp(n, 1, 100000);
            if (n > Niveau) PointsLibres += PointsParNiveau * (n - Niveau);
            Niveau = n;
            XP = 0;
            Soigner();
        }

        public void AjouterPoints(int n) => PointsLibres = Math.Max(0, PointsLibres + n);

        // ---------- sauvegarde ----------

        [Serializable]
        class Etat
        {
            public int niveau; public double xp; public int pointsLibres; public string titre;
            public double[] points, usage, progres;
            public double vie, endurance; public float mana;
            public float x, y, z, lacet;
            public List<int> cellules;
        }

        public string Ecrire()
        {
            var p = transform.position;
            return JsonUtility.ToJson(new Etat
            {
                niveau = Niveau, xp = XP, pointsLibres = PointsLibres, titre = Titre,
                points = (double[])points.Clone(), usage = (double[])usage.Clone(), progres = (double[])progres.Clone(),
                vie = Vie, endurance = Endurance, mana = joueur.Mana,
                x = p.x, y = p.y, z = p.z, lacet = transform.eulerAngles.y,
                cellules = new List<int>(cellules),
            });
        }

        public void Lire(string json)
        {
            var e = JsonUtility.FromJson<Etat>(json);
            if (e == null) return;
            Niveau = Mathf.Max(1, e.niveau); XP = e.xp; PointsLibres = e.pointsLibres;
            if (!string.IsNullOrEmpty(e.titre)) Titre = e.titre;
            for (int i = 0; i < 6; i++)
            {
                points[i] = e.points != null && i < e.points.Length ? e.points[i] : 0;
                usage[i] = e.usage != null && i < e.usage.Length ? e.usage[i] : 0;
                progres[i] = e.progres != null && i < e.progres.Length ? e.progres[i] : 0;
            }
            cellules.Clear();
            if (e.cellules != null) foreach (var c in e.cellules) cellules.Add(c);
            Vie = Math.Min(e.vie, VieMax); Endurance = Math.Min(e.endurance, EnduranceMax);
            joueur.manaMax = (float)ManaMax;
            joueur.FixerMana(Mathf.Min(e.mana, joueur.manaMax));
            joueur.Teleporter(new Vector3(e.x, e.y, e.z), e.lacet);
        }
    }
}
