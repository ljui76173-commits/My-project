using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// État de la partie : drapeaux d'histoire, Éveil du Sceau, confiance du village,
    /// indices trouvés et quête en cours (calculée à partir des drapeaux).
    /// </summary>
    public static class Progression
    {
        public static float Eveil { get; private set; } = 1f;
        public static int PointsConfiance { get; private set; }
        public static int Palier { get; private set; }
        public static readonly List<string> Indices = new List<string>();
        public static string Quete { get; private set; } = "";
        public static event System.Action Change;

        static readonly HashSet<string> drapeaux = new HashSet<string>();
        static readonly string[] NomsPaliers = { "Méfiance", "Confiance", "Dévotion" };
        public static string NomConfiance => NomsPaliers[Palier];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reinitialiser()
        {
            Eveil = 1f; PointsConfiance = 0; Palier = 0;
            drapeaux.Clear(); Indices.Clear(); Change = null;
            Quete = CalculerQuete();
        }

        public static bool A(string d) => drapeaux.Contains(d);

        public static void Mettre(string d)
        {
            if (string.IsNullOrEmpty(d) || !drapeaux.Add(d)) return;
            Actualiser();
        }

        public static void AjouterEveil(float v) { Eveil += v; Actualiser(); }

        public static void AjouterIndice(string titre) { if (!Indices.Contains(titre)) Indices.Add(titre); Actualiser(); }

        public static void AjouterConfiance(int n)
        {
            PointsConfiance += n;
            int nouveau = PointsConfiance >= 6 ? 2 : (PointsConfiance >= 2 ? 1 : 0);
            if (nouveau > Palier)
            {
                Palier = nouveau;
                HUD.Message("CONFIANCE DU VILLAGE",
                    nouveau == 1 ? "Méfiance → Confiance\nLes villageois commencent à t'écouter." : "Confiance → Dévotion\nOn t'appelle « le Bâtisseur ».", 4.5f);
            }
            Actualiser();
        }

        public static void Actualiser()
        {
            Quete = CalculerQuete();
            Change?.Invoke();
        }

        /// <summary>Identifiant du personnage ou de l'indice que la quête désigne (repère à l'écran).</summary>
        public static string Cible { get; private set; } = "lise";

        static string CalculerQuete()
        {
            Cible = "";
            if (!A("lise")) { Cible = "lise"; return "Descendre le chemin. Une fillette t'observe au bord du chemin."; }
            if (!A("test_four")) { Cible = "odon"; return "Le pont de Varenne : parler à Odon, près du puits du village."; }
            if (!(A("vu_four") && A("vu_calcaire"))) { Cible = "four"; return "Odon t'attend au four à chaux. Inspecte le four et ses alentours avec le Sceau (E)."; }
            if (!A("vu_cendres")) { Cible = "four"; return "Le foyer du four cache quelque chose. Approche-toi et analyse-le."; }
            if (!A("four_ok") || !A("sabotage_connu")) { Cible = "odon"; return "Dire à Odon ce qu'il faut au four, et ce que tu as trouvé dans le foyer."; }
            if (!A("bram")) { Cible = "bram"; return "Bram a tout entendu. Parle-lui."; }
            if (!A("vu_corvin")) { Cible = "corvin"; return "Suivre la piste du sabotage avec le Sceau (E)."; }
            if (!A("pont_accepte")) { Cible = "odon"; return "Dire à Odon que tu vas rebâtir le pont."; }
            if (!A("plan_bois")) { Cible = "chantier"; return "Projette le Plan du Registre (P) sur les ruines du pont pour savoir ce qu'il faut."; }
            if (!A("four_repare")) { Cible = "four"; return "Relancer le four : envoie 4 blocs de pierre et des bras au four. Organise le village avec Tab."; }
            if (A("sonnailles_donnees") && !A("sonnailles_resolues")) { Cible = "four"; return $"La nuit des sonnailles : pose les sonnailles (T) sur le chemin des saboteurs, que le Sceau (E) montre entre chez Corvin et le four. Puis dors chez Odon, ou veille."; }
            if (A("saboteurs_captures") && !A("tobin_interroge")) { Cible = "tobin"; return "Trois saboteurs ont été pris au four. Interroge Tobin."; }
            if (A("ilse_arrivee") && !A("ilse_rencontre")) { Cible = "ilse"; return "Une femme en gris attend à l'auberge et cherche le porteur de la marque. Active le Voile (C) avant d'entrer, puis va lui parler."; }
            if (A("info_noeud") && !A("noeud_touche")) { Cible = "noeud"; return "Le nœud de mana sous l'ancien pont : va le voir avec le Sceau (E), de préférence la nuit. Tu pourrais l'affaiblir avant l'arrivée de Vesk."; }
            if (!A("pont_etape1")) { Cible = "chantier"; return "Pont en bois, étape 1 : les piles. Il faut 8 blocs, 4 sacs de chaux et des bâtisseurs au chantier."; }
            if (!A("pont_etape2")) { Cible = "chantier"; return "Pont en bois, étape 2 : les poutres maîtresses. Il faut 16 poutres : la forêt et ses porteurs sont la clé."; }
            if (!A("pont_ouvert")) { Cible = "chantier"; return "Pont en bois, étape 3 : le tablier. Encore 20 poutres."; }
            if (!A("convoi_arrive")) { Cible = "chantier"; return "Le pont provisoire est ouvert ! Regarde passer le convoi du Comte."; }
            if (!A("vesk_demain")) return "Le convoi est arrivé. Ce soir, Varenne mange du pain.";
            if (A("vesk_a_genoux") && !A("bataille_finie")) { Cible = "vesk"; return "Vesk est à genoux. Ilse attend ta réponse."; }
            if (A("bataille_lancee") && !A("bataille_finie")) return "La bataille du Gué : garde le Siphon (R) sur Vesk.";
            if (!A("bataille_lancee"))
            {
                if (!A("pieges_traces")) { Cible = "gue"; return "Vesk arrive à l'aube. Au débarcadère, active le Sceau (E) : il montre la route de la barge et le chemin de ses hommes. Trace les pièges (T) sur leur passage : 1 chaîne, 3 filets, 2 fosses, 1 chausse-trape, 1 fil à cloches."; }
                if (!A("discours")) { Cible = "odon"; return "Les pièges sont tracés. Le soir venu (dès 17 h), tout le village attend ton discours au puits : parle à Odon."; }
                if (!A("soiree_ilse")) { Cible = "ilse"; return "(Facultatif) Ilse est assise au bord du pont neuf. Puis va dormir chez Odon : Vesk arrive à l'aube."; }
                Cible = "lit"; return "Va dormir chez Odon. Vesk arrive à l'aube.";
            }
            return "FIN DE L'ARC 1 — Vainqueur du Gué. La suite du chapitre 1 arrive bientôt.";
        }
    }
}
