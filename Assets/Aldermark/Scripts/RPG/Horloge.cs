using System;
using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// L'horloge du monde : la seule source du temps, lue par tout le monde (soleil, PNJ, prix, saisons).
    /// 1 jour de jeu = 48 minutes réelles ; 4 saisons de 7 jours.
    /// </summary>
    public static class Horloge
    {
        public const float DureeJour = 2880f;
        public const int JoursParSaison = 7;
        public static readonly string[] Saisons = { "Printemps", "Été", "Automne", "Hiver" };

        /// <summary>Secondes de jeu écoulées depuis le jour 1 à minuit.</summary>
        public static double Temps { get; private set; }
        /// <summary>Accélération du temps (console de debug).</summary>
        public static float Vitesse = 1f;

        public static event Action<int> NouvelleHeure;
        public static event Action<int> NouveauJour;
        static int derniereHeure = -1, dernierJour = -1;

        public static int Jour => 1 + (int)(Temps / DureeJour);
        public static float Heure => (float)(Temps % DureeJour / DureeJour * 24.0);
        public static int Minute => (int)(Heure * 60f) % 60;
        public static int Saison => (Jour - 1) / JoursParSaison % 4;
        public static string NomSaison => Saisons[Saison];
        public static int JourDeSaison => (Jour - 1) % JoursParSaison + 1;
        public static int Annee => 1 + (Jour - 1) / (JoursParSaison * 4);
        public static bool EstNuit => Heure < 5.5f || Heure >= 20.5f;
        public static string TexteHeure => $"{(int)Heure:00} h {Minute:00}";
        public static string Texte => $"Jour {Jour} · {NomSaison} · {TexteHeure}";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reinitialiser()
        {
            Temps = 7.0 / 24.0 * DureeJour;   // on se réveille à 7 h, le premier jour du printemps
            Vitesse = 1f;
            NouvelleHeure = null; NouveauJour = null;
            derniereHeure = -1; dernierJour = -1;
        }

        public static void Avancer(float dt) => Fixer(Temps + dt * Vitesse);

        public static void Fixer(double t)
        {
            Temps = Math.Max(0, t);
            int j = Jour, h = (int)Heure;
            if (dernierJour >= 0 && j != dernierJour) NouveauJour?.Invoke(j);
            if (derniereHeure >= 0 && h != derniereHeure) NouvelleHeure?.Invoke(h);
            dernierJour = j; derniereHeure = h;
        }

        /// <summary>Met l'horloge à cette heure du jour en cours (console).</summary>
        public static void FixerHeure(float h)
        {
            double debut = Math.Floor(Temps / DureeJour) * DureeJour;
            Fixer(debut + Mathf.Repeat(h, 24f) / 24.0 * DureeJour);
        }

        /// <summary>Avance jusqu'à la prochaine fois qu'il est h heures (dormir jusqu'au matin).</summary>
        public static void AvancerJusqua(float h)
        {
            double debut = Math.Floor(Temps / DureeJour) * DureeJour;
            double cible = debut + Mathf.Repeat(h, 24f) / 24.0 * DureeJour;
            if (cible <= Temps + 1) cible += DureeJour;
            Fixer(cible);
        }

        /// <summary>Avance d'un certain nombre d'heures de jeu (se reposer).</summary>
        public static void AvancerHeures(float heures) => Fixer(Temps + heures / 24.0 * DureeJour);
    }
}
