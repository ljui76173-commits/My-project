using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// L'Évaluation : jauger d'un coup d'œil une créature (niveau, vie, faiblesses, force) et un objet (sa valeur).
    /// Au niveau 1, des fourchettes larges (±50 %) ; elles se resserrent à chaque niveau ; au niveau 10, les chiffres exacts.
    /// Ce qu'on voit s'enrichit aussi : faiblesses (niv. 2), résistances (4), attaque et défense (5), immunités (6).
    /// </summary>
    public partial class Competences
    {
        readonly HashSet<string> especesEvaluees = new HashSet<string>();
        readonly HashSet<string> objetsEvalues = new HashSet<string>();
        readonly HashSet<int> ciblesEvaluees = new HashSet<int>();   // chaque créature croisée, pour la partie en cours

        public int NiveauEvaluation => Mathf.Max(1, Niveau("evaluation"));

        /// <summary>L'imprécision relative : ±50 % au niveau 1, 0 au niveau 10.</summary>
        public static float Marge(int n)
        {
            int m = CatalogueCompetences.NiveauMaitrise;
            return n >= m ? 0f : .5f * (m - n) / (m - 1f);
        }

        public float MargeEvaluation => Marge(NiveauEvaluation);
        public bool EvaluationExacte => MargeEvaluation <= 0f;

        /// <summary>Une créature regardée pour la première fois : un peu d'expérience, beaucoup plus pour une espèce inconnue.</summary>
        public void Evaluer(ICible c)
        {
            if (c == null || c.Corps == null || c is CombatHeros) return;
            if (c is Combattant cb && cb.invincible) return;   // un mannequin n'apprend rien
            if (!ciblesEvaluees.Add(c.Corps.GetInstanceID())) return;
            double xp = 1;
            if (especesEvaluees.Add(c.NomAffiche)) xp += 3;
            Exercer("evaluation", xp);
        }

        /// <summary>Un objet examiné pour la première fois dans le sac.</summary>
        public void EvaluerObjet(string id)
        {
            if (string.IsNullOrEmpty(id) || !objetsEvalues.Add(id)) return;
            Exercer("evaluation", .5);
        }

        /// <summary>Un bruit fixe dans [0, 1[ : la même créature ou le même objet donne toujours la même estimation.</summary>
        static float Bruit(int graine, int canal)
        {
            unchecked
            {
                uint h = (uint)graine * 2654435761u ^ (uint)(canal + 1) * 40503u;
                h ^= h >> 15; h *= 2246822519u; h ^= h >> 13; h *= 3266489917u; h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        /// <summary>Une graine stable tirée d'un texte (l'identifiant d'un objet).</summary>
        public static int Graine(string s)
        {
            unchecked
            {
                int h = (int)2166136261;
                if (s != null) foreach (char ch in s) h = (h ^ ch) * 16777619;
                return h;
            }
        }

        /// <summary>Le niveau d'une créature : une fourchette qui contient toujours le vrai niveau.</summary>
        public string EstimerNiveau(int niveau, int graine)
        {
            float m = MargeEvaluation;
            if (m <= 0f) return niveau.ToString();
            int largeur = Mathf.Max(1, Mathf.RoundToInt(niveau * m * 1.2f));
            int bas = Mathf.Max(1, niveau - Mathf.FloorToInt(Bruit(graine, 0) * (largeur + 1)));
            return $"{bas}–{bas + largeur}";
        }

        /// <summary>Une quantité estimée : décalée d'au plus la marge, arrondie à deux chiffres tant qu'on n'est pas expert.</summary>
        public double Estimer(double vrai, int graine, int canal)
        {
            float m = MargeEvaluation;
            if (m <= 0f || vrai <= 0) return vrai;
            return DeuxChiffres(vrai * (1 + (Bruit(graine, canal) * 2 - 1) * m));
        }

        static double DeuxChiffres(double v)
        {
            if (v <= 0) return 0;
            double p = Math.Pow(10, Math.Floor(Math.Log10(v)) - 1);
            if (p < 1) p = 1;
            return Math.Max(1, Math.Round(v / p) * p);
        }

        /// <summary>La vie d'une créature, en clair : exacte au niveau 10, approchée avant.</summary>
        public string TexteVie(double actuelle, double max, int graine)
        {
            if (EvaluationExacte) return $"{Nombres.Court(actuelle)} / {Nombres.Court(max)}";
            double m = Estimer(max, graine, 1);
            double a = max > 0 ? DeuxChiffres(m * actuelle / max) : 0;
            if (actuelle > 0 && a < 1) a = 1;
            return $"~{Nombres.Court(a)} / ~{Nombres.Court(m)}";
        }

        /// <summary>Faiblesses, résistances, immunités, attaque et défense : ce que le niveau d'Évaluation laisse voir.</summary>
        public string TexteDetail(Combattant c, int graine)
        {
            if (c == null) return "";
            int n = NiveauEvaluation;
            var morceaux = new List<string>();
            if (n >= 2)
            {
                var faibles = new List<string>();
                var resiste = new List<string>();
                var insensible = new List<string>();
                foreach (Element e in Enum.GetValues(typeof(Element)))
                {
                    if (e == Element.Aucun) continue;
                    float a = c.Affinite(e);
                    string nom = CatalogueCompetences.NomElement(e);
                    if (a > 1.01f) faibles.Add(nom);
                    else if (a <= .01f) { if (n >= 6) insensible.Add(nom); else if (n >= 4) resiste.Add(nom); }
                    else if (a < .99f && n >= 4) resiste.Add(nom);
                }
                if (faibles.Count > 0) morceaux.Add($"<color=#ffa060>faible : {string.Join(", ", faibles)}</color>");
                if (resiste.Count > 0) morceaux.Add($"<color=#9fb8d8>résiste : {string.Join(", ", resiste)}</color>");
                if (insensible.Count > 0) morceaux.Add($"<color=#a8a6b8>insensible : {string.Join(", ", insensible)}</color>");
                if (morceaux.Count == 0 && n >= 6) morceaux.Add("<color=#a8a6b8>ni faiblesse ni résistance</color>");
            }
            if (n >= 5)
            {
                string t = EvaluationExacte ? "" : "~";
                morceaux.Add($"Atq {t}{Nombres.Court(Estimer(c.Attaque, graine, 2))} · Déf {t}{Nombres.Court(Estimer(c.Defense, graine, 3))}");
            }
            return string.Join("   ·   ", morceaux);
        }

        /// <summary>La valeur d'un objet, telle que le héros l'estime (les pièces, elles, se comptent).</summary>
        public string TexteValeur(DefObjet d, int quantite)
        {
            if (d == null) return "";
            if (EvaluationExacte || d.categorie == Categorie.Monnaie) return $"{d.valeur * quantite}";
            double v = Estimer(d.valeur, Graine(d.id), 4);
            return $"~{Nombres.Court(v * quantite)}";
        }
    }
}
