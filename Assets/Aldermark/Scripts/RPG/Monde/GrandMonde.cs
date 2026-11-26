using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// Le grand monde (8 km × 8 km) dans la scène : les 64 tuiles de terrain, et les données cuites
    /// à la génération (type de sol et niveau de l'eau tous les 4 m, la carte peinte, les villes, les routes,
    /// les rivières). Au réveil, il branche les questions de <see cref="Geo"/> sur ces données :
    /// la hauteur se lit dans le terrain, le sol et l'eau dans les tableaux.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class GrandMonde : MonoBehaviour
    {
        public static GrandMonde Instance { get; private set; }

        /// <summary>Côté des grilles cuites (sol, eau) : une case tous les 4 m.</summary>
        public const int Resolution = 2048;
        public const float Case = Geo.Taille / Resolution;

        [Tooltip("Les 64 tuiles, rangée par rangée (ouest → est, puis sud → nord).")]
        public Terrain[] terrains;
        [Tooltip("La carte du monde peinte à la génération.")]
        public Texture2D carte;
        [Tooltip("Le type de sol de chaque case (un octet par case).")]
        public TextAsset sols;
        [Tooltip("La surface de l'eau de chaque case, en décimètres (−32768 : pas d'eau).")]
        public TextAsset eaux;
        public List<Site> sites = new List<Site>();
        public List<Trace> routes = new List<Trace>();
        public List<Trace> rivieres = new List<Trace>();

        byte[] grilleSols;
        short[] grilleEaux;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reinitialiser() => Instance = null;

        void Awake()
        {
            Instance = this;
            grilleSols = sols != null ? sols.bytes : null;
            if (eaux != null)
            {
                var o = eaux.bytes;
                grilleEaux = new short[o.Length / 2];
                System.Buffer.BlockCopy(o, 0, grilleEaux, 0, grilleEaux.Length * 2);
            }
            Brancher();
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            Geo.Desactiver();
        }

        /// <summary>Branche Geo sur ce monde (hauteur du terrain, sols et eaux cuits, villes, routes, rivières).</summary>
        public void Brancher()
        {
            Geo.Desactiver();
            Geo.Actif = true;
            Geo.Echantillon = Hauteur;
            if (grilleSols != null && grilleSols.Length >= Resolution * Resolution) Geo.Sols = Sol;
            if (grilleEaux != null && grilleEaux.Length >= Resolution * Resolution) Geo.Eaux = Eau;
            Geo.Sites.AddRange(sites);
            Geo.Routes.AddRange(routes);
            Geo.Rivieres.AddRange(rivieres);
        }

        public Terrain TuileEn(float x, float z)
        {
            if (terrains == null || terrains.Length < Geo.Tuiles * Geo.Tuiles) return null;
            int i = Mathf.Clamp(Mathf.FloorToInt((x + Geo.Demi) / Geo.TailleTuile), 0, Geo.Tuiles - 1);
            int j = Mathf.Clamp(Mathf.FloorToInt((z + Geo.Demi) / Geo.TailleTuile), 0, Geo.Tuiles - 1);
            return terrains[j * Geo.Tuiles + i];
        }

        float Hauteur(float x, float z)
        {
            var t = TuileEn(x, z);
            if (t == null) return Geo.HauteurBase(x, z);
            return t.SampleHeight(new Vector3(x, 0f, z)) + t.transform.position.y;
        }

        static int Indice(float x, float z)
        {
            int i = Mathf.Clamp(Mathf.FloorToInt((x + Geo.Demi) / Case), 0, Resolution - 1);
            int j = Mathf.Clamp(Mathf.FloorToInt((z + Geo.Demi) / Case), 0, Resolution - 1);
            return j * Resolution + i;
        }

        TypeSol Sol(float x, float z) => (TypeSol)grilleSols[Indice(x, z)];

        float Eau(float x, float z)
        {
            short v = grilleEaux[Indice(x, z)];
            return v == short.MinValue ? -10000f : v * .1f;
        }
    }
}
