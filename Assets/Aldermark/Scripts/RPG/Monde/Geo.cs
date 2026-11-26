using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    public enum TypeSite { Capitale, Ville, Port, Village, Hameau, Depart }

    /// <summary>Types de sol du grand monde (couleurs du terrain, herbe, bruits de pas, carte).</summary>
    public enum TypeSol : byte { Herbe, HerbeDoree, HerbeSombre, Champ0, Champ1, Champ2, Chemin, Roche, Sable, LitRiviere, Neige, Pave }

    /// <summary>Une ville, un village, un hameau : son nom, son royaume, sa place et sa taille.</summary>
    [Serializable]
    public class Site
    {
        public string nom;
        public TypeSite type;
        public int royaume;          // 0 : Aldermark (ouest), 1 : Valdor (est)
        public Vector2 position;
        public float rayon, altitude, angle;
        public Vector2 fort;         // capitale : la butte du château
    }

    /// <summary>Une rivière ou une route : une ligne de points, une largeur et une hauteur (surface de l'eau, chaussée) en chaque point.</summary>
    [Serializable]
    public class Trace
    {
        public string nom;
        public Vector2[] points;
        public float[] largeurs;
        public float[] niveaux;
    }

    /// <summary>Un lac : sa surface et les cases (de 32 m) qu'il recouvre.</summary>
    [Serializable]
    public class Lac
    {
        public float niveau;
        public int[] cellules;
        public Vector2 centre;
    }

    /// <summary>Un pont : là où une route enjambe une rivière.</summary>
    [Serializable]
    public class Pont
    {
        public Vector2 position;
        public float angle, longueur, largeur, hauteur;
    }

    /// <summary>
    /// La géographie du grand monde (8 km de côté) : le relief « naturel » calculé par du bruit
    /// (plaines, collines, plateaux à falaises, une crête de montagnes entre les deux royaumes avec un col,
    /// des massifs, la mer au sud-ouest, des montagnes aux bords du monde), et les réponses dont le jeu a besoin
    /// en tout point (hauteur, type de sol, niveau de l'eau), lues dans les données cuites à la génération.
    /// </summary>
    public static class Geo
    {
        public const float Taille = 8192f, Demi = Taille / 2f;
        public const int Tuiles = 8;
        public const float TailleTuile = Taille / Tuiles;
        public const float YMin = -60f, HauteurMax = 1000f;

        /// <summary>Vrai quand la scène est le grand monde (posé par GrandMonde, ou par le générateur).</summary>
        public static bool Actif;
        public static Func<float, float, float> Echantillon;
        public static Func<float, float, TypeSol> Sols;
        public static Func<float, float, float> Eaux;
        public static readonly List<Site> Sites = new List<Site>();
        public static readonly List<Trace> Routes = new List<Trace>();
        public static readonly List<Trace> Rivieres = new List<Trace>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reinitialiser()
        {
            Actif = false; Echantillon = null; Sols = null; Eaux = null;
            Sites.Clear(); Routes.Clear(); Rivieres.Clear();
            indexRoutes = null;
        }

        /// <summary>Remet tout à zéro (le générateur, après avoir construit le monde dans l'éditeur).</summary>
        public static void Desactiver() => Reinitialiser();

        static IndexTraces indexRoutes;

        /// <summary>Distance (m) au bord de la route la plus proche : négative sur la chaussée, 99 au-delà de 40 m.</summary>
        public static float DistanceRoute(float x, float z)
        {
            if (Routes.Count == 0) return 99f;
            if (indexRoutes == null) indexRoutes = new IndexTraces(Routes, 64f);
            return indexRoutes.Proche(x, z, 40f, out float d, out _, out float l) ? d - l * .5f : 99f;
        }

        // ---------- questions du jeu ----------

        public static float Hauteur(float x, float z) => Echantillon != null ? Echantillon(x, z) : HauteurBase(x, z);
        public static TypeSol Sol(float x, float z) => Sols != null ? Sols(x, z) : TypeSol.Herbe;
        /// <summary>Hauteur de la surface de l'eau ici (mer, lac, rivière), ou −10 000 s'il n'y a pas d'eau.</summary>
        public static float NiveauEau(float x, float z) => Eaux != null ? Eaux(x, z) : (HauteurBase(x, z) < 0f ? 0f : -10000f);
        public static bool EstChemin(float x, float z) { var s = Sol(x, z); return s == TypeSol.Chemin || s == TypeSol.Pave; }
        /// <summary>Un sol où l'herbe pousse (prés, sous-bois, champs).</summary>
        public static bool EstHerbe(TypeSol s) => s <= TypeSol.Champ2;
        public static bool EstChamp(TypeSol s) => s >= TypeSol.Champ0 && s <= TypeSol.Champ2;
        public static int Royaume(float x, float z) => x < CreteX(z) ? 0 : 1;

        public static Vector2 Depart
        {
            get
            {
                foreach (var s in Sites) if (s.type == TypeSite.Depart) return s.position;
                return Vector2.zero;
            }
        }

        public static Site SitePlusProche(Vector2 p, Predicate<Site> filtre = null)
        {
            Site best = null; float bd = float.MaxValue;
            foreach (var s in Sites)
            {
                if (filtre != null && !filtre(s)) continue;
                float d = (s.position - p).sqrMagnitude;
                if (d < bd) { bd = d; best = s; }
            }
            return best;
        }

        // ---------- bruit ----------

        static uint Melange(int x, int y, int graine)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + graine * 982451653);
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }

        public static float Hasard(int x, int y, int graine = 0) => (Melange(x, y, graine) & 0xFFFFFF) / 16777215f;
        public static float Hasard(float x, float y, int graine = 0) => Hasard((int)Math.Floor(x * 7.31f), (int)Math.Floor(y * 5.17f), graine);

        public static float Valeur(float x, float y, int graine = 0)
        {
            int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y);
            float u = x - xi, v = y - yi;
            u = u * u * (3f - 2f * u); v = v * v * (3f - 2f * v);
            float a = Hasard(xi, yi, graine), b = Hasard(xi + 1, yi, graine), c = Hasard(xi, yi + 1, graine), d = Hasard(xi + 1, yi + 1, graine);
            float ab = a + (b - a) * u, cd = c + (d - c) * u;
            return ab + (cd - ab) * v;
        }

        /// <summary>Bruit fractal 0…1 (chaque octave tournée pour casser la grille).</summary>
        public static float Fbm(float x, float y, int octaves, int graine = 0)
        {
            float s = 0f, a = .5f, n = 0f;
            for (int o = 0; o < octaves; o++)
            {
                s += a * Valeur(x, y, graine + o * 31);
                n += a;
                float nx = x * 1.6f + y * 1.2f, ny = -x * 1.2f + y * 1.6f;
                x = nx + 13.7f; y = ny + 7.1f;
                a *= .5f;
            }
            return s / n;
        }

        /// <summary>Bruit en arêtes 0…1 (crêtes de montagnes).</summary>
        public static float Arete(float x, float y, int octaves, int graine = 0)
        {
            float s = 0f, a = .5f, n = 0f, poids = 1f;
            for (int o = 0; o < octaves; o++)
            {
                float v = 1f - Math.Abs(Valeur(x, y, graine + o * 17) * 2f - 1f);
                v *= v * poids;
                poids = Mathf.Clamp01(v * 1.6f);
                s += a * v; n += a;
                float nx = x * 1.7f + y * 1.1f, ny = -x * 1.1f + y * 1.7f;
                x = nx + 3.3f; y = ny + 9.9f;
                a *= .5f;
            }
            return s / n;
        }

        public static float Lisse(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        // ---------- le relief naturel ----------

        /// <summary>La ligne de crête qui sépare les deux royaumes (x en fonction de z).</summary>
        public static float CreteX(float z) => 420f + 520f * Mathf.Sin(z * .00055f + .8f) + 170f * Mathf.Sin(z * .0013f + 2f);

        /// <summary>Le col où la crête s'abaisse (z), passage entre les royaumes.</summary>
        public const float ColZ = 250f;

        static float MasqueCrete(float x, float z)
        {
            float m = 1f - Lisse(220f, 880f, Math.Abs(x - CreteX(z)));
            m *= 1f - (1f - Lisse(260f, 700f, Math.Abs(z - ColZ))) * .88f;   // le col
            m *= Lisse(-2900f, -1700f, z);                                        // la crête meurt avant la mer
            return m;
        }

        /// <summary>0 : fond de mer … 1 : haut pays. Sert aussi à savoir où est la côte.</summary>
        public static float Terre(float x, float z)
        {
            float dx = (Fbm(x * .00035f, z * .00035f, 3, 11) - .5f) * 900f;
            float dz = (Fbm(x * .00035f + 5.2f, z * .00035f + 1.3f, 3, 12) - .5f) * 900f;
            return TerreDeforme(x, z, x + dx, z + dz);
        }

        public static float HauteurBase(float x, float z)
        {
            float dx = (Fbm(x * .00035f, z * .00035f, 3, 11) - .5f) * 900f;
            float dz = (Fbm(x * .00035f + 5.2f, z * .00035f + 1.3f, 3, 12) - .5f) * 900f;
            float wx = x + dx, wz = z + dz;
            float terre = TerreDeforme(x, z, wx, wz);
            float interieur = Lisse(.4f, .56f, terre);

            // La mer au sud-ouest, puis des plaines qui montent doucement vers l'intérieur
            float h = Mathf.Lerp(-45f, 14f, Lisse(.3f, .44f, terre)) + Lisse(.44f, 1.05f, terre) * 60f;

            // Collines : de douces ondulations aux vraies buttes, des croupes allongées et leurs vallons
            float relief = Lisse(.3f, .68f, Fbm(x * .0004f + 40f, z * .0004f - 20f, 2, 8));
            float c = Lisse(.28f, .74f, Fbm(wx * .0016f, wz * .0016f, 4, 2));
            h += interieur * (Mathf.Pow(c, 1.3f) * (35f + 190f * relief) - 14f);
            float croupes = Lisse(.12f, .62f, Arete(wx * .0024f + 5f, wz * .0024f, 3, 9));
            h += interieur * (.25f + relief) * croupes * croupes * 85f;

            // Montagnes : la crête des royaumes, des massifs, les bords du monde
            float r = Arete(wx * .00105f, wz * .00105f, 5, 4);
            float crete = MasqueCrete(x, z) * interieur;
            float massifs = Lisse(.6f, .76f, Fbm(x * .00027f + 7f, z * .00027f - 3f, 3, 3)) * interieur;
            float bordure = Math.Max(Math.Abs(x), Math.Abs(z)) / Demi + (Fbm(x * .0009f + 31f, z * .0009f, 3, 23) - .5f) * .09f;
            float bords = Lisse(.865f, .985f, bordure) * Lisse(.36f, .5f, terre);
            h += crete * (140f + Mathf.Pow(r, 1.3f) * 430f);
            h += massifs * (40f + Mathf.Pow(r, 1.5f) * 420f) * (1f - crete);
            h += bords * (190f + Mathf.Pow(r, 1.2f) * 340f) * (1f - crete);
            float masque = Math.Max(crete, Math.Max(massifs, bords));

            // Plateaux à falaises : des marches nettes
            float plateau = Lisse(.64f, .7f, Fbm(x * .00055f - 13f, z * .00055f + 8f, 2, 5)) * interieur * (1f - masque);
            if (plateau > .001f) h = Mathf.Lerp(h, Terrasse(h + 30f, 26f), plateau);

            // Le grain du sol
            h += (Fbm(x * .013f, z * .013f, 2, 6) - .5f) * 3.5f;
            return h;
        }

        /// <summary>La « terre » avec une côte découpée : baies, caps, quelques îles.</summary>
        static float TerreDeforme(float x, float z, float wx, float wz)
        {
            float continent = Fbm(wx * .00021f, wz * .00021f, 4, 1);
            float t = continent * .75f + (x + z) / Taille * .55f + .24f;
            float cote = 1f - Math.Abs(t - .37f) / .12f;                    // près du rivage
            if (cote > 0f) t += (Fbm(x * .0011f + 3f, z * .0011f - 9f, 4, 21) - .5f) * .2f * cote;
            return t;
        }

        static float Terrasse(float h, float pas)
        {
            float k = h / pas, e = (float)Math.Floor(k), f = k - e;
            return (e + Lisse(.42f, .58f, f)) * pas;
        }
    }
}
