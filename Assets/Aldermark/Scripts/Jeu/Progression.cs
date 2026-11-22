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

        static void Actualiser()
        {
            Quete = CalculerQuete();
            Change?.Invoke();
        }

        static string CalculerQuete()
        {
            if (!A("lise")) return "Descendre vers le village. Quelqu'un t'observe au bord du chemin.";
            if (!A("test_four")) return "Le pont de Varenne : parler à Odon, près du puits.";
            if (!(A("vu_four") && A("vu_calcaire"))) return "Inspecter le four à chaux et ses alentours avec le Sceau (E).";
            if (!A("vu_cendres")) return "Le foyer du four cache quelque chose. Approche-toi et analyse-le.";
            if (!A("four_ok") || !A("sabotage_connu")) return "Retourner voir Odon et lui dire ce qu'il faut au four.";
            if (!A("vu_corvin")) return "Suivre la piste du sabotage avec le Sceau (E).";
            if (!A("bram")) return "Parler à Bram de la piste qui mène chez Corvin.";
            if (!A("pont_accepte")) return "Dire à Odon que tu vas rebâtir le pont.";
            return "Chapitre suivant : le Plan du Registre et le chantier (en développement).";
        }
    }
}
