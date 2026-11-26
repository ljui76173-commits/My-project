using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// Géographie de la vallée de Varenne, partagée entre le générateur (éditeur) et le jeu.
    /// Même relief que le prototype navigateur.
    /// </summary>
    public static class Monde
    {
        public const float Taille = 620f;          // côté du terrain en mètres
        public const float Berge = 13f;            // demi-largeur du lit de la rivière
        public static readonly Vector2 Village = new Vector2(55f, 10f);
        public static readonly Vector2 Four = new Vector2(84f, -46f);
        public static readonly Vector2 Depart = new Vector2(118f, -80f);
        public static float PontX => Riviere(0f);
        public static Vector2 Corvin => new Vector2(PontX + 22f, 27f);
        /// <summary>Le hameau de départ du mode RPG : quelques maisons à l'écart, au sud de la route de l'ouest.</summary>
        public static Vector2 Hameau => Geo.Actif && Geo.Sites.Count > 0 ? Geo.Depart : HameauVallee;
        public static readonly Vector2 HameauVallee = new Vector2(-80f, -96f);
        public const float RayonHameau = 26f;
        /// <summary>La maison du héros dans le hameau (pas d'herbe sous son plancher).</summary>
        public static Vector2 MaisonHeros => Hameau + new Vector2(-10f, -6f);
        /// <summary>La quintaine d'entraînement, dans la cour à l'ouest du hameau.</summary>
        public static Vector2 Quintaine => Hameau + new Vector2(-13f, 2f);
        /// <summary>Les repaires autour du hameau, de plus en plus dangereux en s'éloignant.</summary>
        public static readonly Vector2 RepaireGelees = new Vector2(-63f, -49f), RepaireLoups = new Vector2(-65f, -12f),
            CampGobelins = new Vector2(-29f, 45f), AntreTroll = new Vector2(-62f, -164f);

        // Routes (x, z)
        public static readonly Vector2[][] Routes = new[]
        {
            new[] { new Vector2(175,-125), new Vector2(122,-66), new Vector2(84,-22), Village, new Vector2(32,4), new Vector2(PontX+18,0) },
            new[] { new Vector2(PontX-18,0), new Vector2(-34,-8), new Vector2(-84,-38), new Vector2(-150,-58), new Vector2(-230,-70) },
            new[] { Village, new Vector2(64,62), new Vector2(84,140), new Vector2(96,230) },
            new[] { new Vector2(84,-22), Four },
            new[] { new Vector2(-84,-38), new Vector2(-83,-62), new Vector2(-80,-84) },
        };

        public static float Riviere(float z) => 14f * Mathf.Sin(z / 55f) + 6f * Mathf.Sin(z / 23f + 1f);

        static float Hash(float x, float y)
        {
            double s = System.Math.Sin(x * 127.1 + y * 311.7) * 43758.5453;
            return (float)(s - System.Math.Floor(s));
        }

        static float Bruit(float x, float y)
        {
            float xi = Mathf.Floor(x), yi = Mathf.Floor(y), xf = x - xi, yf = y - yi;
            float u = xf * xf * (3 - 2 * xf), v = yf * yf * (3 - 2 * yf);
            float a = Hash(xi, yi), b = Hash(xi + 1, yi), c = Hash(xi, yi + 1), d = Hash(xi + 1, yi + 1);
            return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
        }

        public static float Fbm(float x, float y)
        {
            float s = 0, a = .5f;
            for (int i = 0; i < 4; i++) { s += a * Bruit(x, y); x *= 2.03f; y *= 2.03f; a *= .5f; }
            return s;
        }

        public static float Lisse(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3 - 2 * t);
        }

        public static float HashPublic(float x, float y) => Hash(x, y);

        /// <summary>Hauteur du sol en (x, z).</summary>
        public static float Hauteur(float x, float z)
        {
            if (Geo.Actif) return Geo.Hauteur(x, z);
            float d = Mathf.Abs(x - Riviere(z));
            float y = -3.6f + Lisse(0, Berge, d) * 4.8f;
            if (d > Berge) y = 1.2f + (d - Berge) * .11f + Mathf.Pow(Mathf.Max(0, d - 70) / 40f, 2) * 7f;
            y += (Fbm(x * .02f, z * .02f) - .5f) * 11f * Lisse(Berge, Berge + 30, d);
            float r = Mathf.Sqrt(x * x + z * z);
            float crete = 1f - Mathf.Abs(Fbm(x * .01f + 3, z * .01f) * 2f - 1f);
            y += Mathf.Pow(Mathf.Max(0, r - 185), 1.3f) * .11f * (.45f + .9f * crete);
            y += 15f * Mathf.Exp(-((x - Depart.x) * (x - Depart.x) + (z - Depart.y) * (z - Depart.y)) / (2 * 34 * 34));
            float k = Lisse(46, 24, Vector2.Distance(new Vector2(x, z), Village));
            y = y + (3.3f + (Fbm(x * .05f, z * .05f) - .5f) * .8f - y) * k;
            // Le hameau de départ est posé sur un replat
            float kh = Lisse(RayonHameau + 12f, RayonHameau - 6f, Vector2.Distance(new Vector2(x, z), HameauVallee));
            y = y + (5.6f + (Fbm(x * .05f, z * .05f) - .5f) * .6f - y) * kh;
            return y;
        }

        static float DistSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + t * ab);
        }

        public static float DistanceRoute(float x, float z)
        {
            if (Geo.Actif) return Geo.DistanceRoute(x, z) + 1.5f;
            var p = new Vector2(x, z);
            float m = float.MaxValue;
            foreach (var r in Routes)
                for (int i = 0; i < r.Length - 1; i++) m = Mathf.Min(m, DistSegment(p, r[i], r[i + 1]));
            return m;
        }

        public static bool DansChamp(float x, float z) => Geo.Actif ? Geo.EstChamp(Geo.Sol(x, z)) :
            x > 74 && x < 138 && z > -24 && z < 62 && Vector2.Distance(new Vector2(x, z), Village) > 30;

        /// <summary>Bande du champ (0 blé, 1 vert, 2 labour).</summary>
        public static int BandeChamp(float x, float z) => Geo.Actif ? Mathf.Clamp((int)Geo.Sol(x, z) - (int)TypeSol.Champ0, 0, 2) : (int)(((Mathf.Floor((x * .85f + z * .3f) / 7f) % 3) + 3) % 3);

        /// <summary>Un sol de terre (chemin, pavés, sable, roche, labour) : bruit de pas et poussière.</summary>
        public static bool SurTerre(float x, float z)
        {
            if (!Geo.Actif) return DistanceRoute(x, z) < 2.5f || Vector2.Distance(new Vector2(x, z), Village) < 12f;
            var s = Geo.Sol(x, z);
            return Geo.DistanceRoute(x, z) < 1f || s == TypeSol.Chemin || s == TypeSol.Pave || s == TypeSol.Sable || s == TypeSol.LitRiviere || s == TypeSol.Roche || s == TypeSol.Champ2;
        }

        // ---- Nuages : même bruit que le shader du ciel (hash sans sinus, stable entre CPU et GPU)
        static float Fr(float v) => v - Mathf.Floor(v);
        static float NH(float x, float y)
        {
            float a = Fr(x * .1031f), b = Fr(y * .1031f), c = Fr(x * .1031f);
            float d = a * (b + 33.33f) + b * (c + 33.33f) + c * (a + 33.33f);
            a += d; b += d; c += d;
            return Fr((a + b) * c);
        }
        static float NN(float x, float y)
        {
            float xi = Mathf.Floor(x), yi = Mathf.Floor(y), u = x - xi, v = y - yi;
            u = u * u * (3 - 2 * u); v = v * v * (3 - 2 * v);
            float A = NH(xi, yi), B = NH(xi + 1, yi), C = NH(xi, yi + 1), D = NH(xi + 1, yi + 1);
            float ab = A + (B - A) * u, cd = C + (D - C) * u;
            return ab + (cd - ab) * v;
        }
        public static float BruitNuages(float x, float y)
        {
            float v = 0, a = .5f;
            for (int i = 0; i < 5; i++) { v += a * NN(x, y); x *= 2.03f; y *= 2.03f; a *= .5f; }
            return v;
        }

        /// <summary>Densité des cumulus, identique au shader Aldermark/Ciel.</summary>
        public static float DensiteNuages(float px, float py)
        {
            float qx = BruitNuages(px * 1.1f, py * 1.1f), qy = BruitNuages(px * 1.1f + 5.2f, py * 1.1f + 5.2f);
            return BruitNuages(px * 1.6f + qx * 1.2f, py * 1.6f + qy * 1.2f);
        }
    }
}
