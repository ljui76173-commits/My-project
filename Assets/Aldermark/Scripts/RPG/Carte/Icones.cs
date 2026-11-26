using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// Les icônes de la carte, dessinées en code : un badge rond de la couleur de l'onglet
    /// (or : lieux, bleu : commerces, vert d'eau : services, rouge : dangers, violet : repères)
    /// et un pictogramme blanc en pixels (maison, chope, enclume, bourse, croix, patte, crâne…).
    /// </summary>
    public static class Icones
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reinitialiser() => cache.Clear();

        public static Color Couleur(OngletCarte o)
        {
            switch (o)
            {
                case OngletCarte.Commerces: return new Color(.29f, .56f, .82f);
                case OngletCarte.Services: return new Color(.25f, .64f, .58f);
                case OngletCarte.Dangers: return new Color(.79f, .27f, .23f);
                case OngletCarte.Reperes: return new Color(.6f, .36f, .82f);
                default: return new Color(.85f, .66f, .26f);
            }
        }

        public static Sprite Lieu(TypeLieu t) => Badge(t.ToString(), Glyphe(t), Couleur(Aldermark.Lieu.OngletDe(t)));
        public static Sprite Repere => Badge("repere", Etoile, Couleur(OngletCarte.Reperes));
        public static Sprite Itineraire => Badge("itineraire", Drapeau, new Color(.62f, .3f, .85f));

        /// <summary>La flèche du joueur (pointe vers le haut).</summary>
        public static Sprite Joueur
        {
            get
            {
                if (cache.TryGetValue("joueur", out var s) && s != null) return s;
                const int N = 48;
                var px = new Color[N * N];
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        // Triangle (pointe en haut) avec une encoche en bas
                        float u = (x + .5f) / N, v = (y + .5f) / N;
                        float demi = (v - .08f) / .84f * .42f;   // largeur qui croît vers le bas
                        bool dans = v > .08f && v < .92f && Mathf.Abs(u - .5f) < (1f - (v - .08f) / .84f) * .42f && !(v < .3f && Mathf.Abs(u - .5f) < (.3f - v) * .9f);
                        bool bord = !dans && v > .04f && v < .96f && Mathf.Abs(u - .5f) < (1f - (v - .04f) / .92f) * .48f;
                        _ = demi;
                        px[y * N + x] = dans ? Color.white : bord ? new Color(.1f, .08f, .15f, .9f) : Color.clear;
                    }
                return cache["joueur"] = Creer(px, N, "Joueur");
            }
        }

        /// <summary>Un disque blanc (masque de la mini-carte, pastilles).</summary>
        public static Sprite Disque
        {
            get
            {
                if (cache.TryGetValue("disque", out var s) && s != null) return s;
                const int N = 256;
                var px = new Color[N * N];
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float d = new Vector2(x + .5f - N / 2f, y + .5f - N / 2f).magnitude;
                        px[y * N + x] = new Color(1, 1, 1, Mathf.Clamp01(N / 2f - d));
                    }
                return cache["disque"] = Creer(px, N, "Disque");
            }
        }

        static Sprite Badge(string cle, string[] glyphe, Color fond)
        {
            if (cache.TryGetValue(cle, out var s) && s != null) return s;
            const int N = 48;
            var px = new Color[N * N];
            var bord = new Color(.08f, .07f, .12f, .95f);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = new Vector2(x + .5f - N / 2f, y + .5f - N / 2f).magnitude;
                    Color c = Color.clear;
                    if (d < 23.5f) c = d > 20.5f ? bord : fond;
                    if (d >= 23.5f && d < 24.5f) c = new Color(bord.r, bord.g, bord.b, 24.5f - d);
                    // Pictogramme 16 × 16 agrandi 2 fois, centré
                    int gx = (x - 8) / 2, gy = 15 - (y - 8) / 2;
                    if (x >= 8 && x < 40 && y >= 8 && y < 40 && glyphe != null && gy < glyphe.Length && gx < glyphe[gy].Length && glyphe[gy][gx] == '#') c = Color.white;
                    px[y * N + x] = c;
                }
            return cache[cle] = Creer(px, N, "Icone_" + cle);
        }

        static Sprite Creer(Color[] px, int n, string nom)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = nom, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f));
        }

        static string[] Glyphe(TypeLieu t)
        {
            switch (t)
            {
                case TypeLieu.Maison: return Maison;
                case TypeLieu.Hameau: return Hameau;
                case TypeLieu.Village: return Donjon;
                case TypeLieu.Auberge: return Chope;
                case TypeLieu.Forge: return Enclume;
                case TypeLieu.Marche: return Bourse;
                case TypeLieu.Moulin: return Roue;
                case TypeLieu.Chapelle: return Croix;
                case TypeLieu.Lavoir: return Goutte;
                case TypeLieu.Puits: return Seau;
                case TypeLieu.Quintaine: return Epee;
                case TypeLieu.Gelees: return Gelee;
                case TypeLieu.Loups: return Patte;
                case TypeLieu.Gobelins: return Tente;
                default: return Crane;
            }
        }

        // ---------- pictogrammes 16 × 16 ----------

        static readonly string[] Maison =
        {
            "................", ".......##.......", "......####......", ".....######.....", "....########....", "...##########...",
            "..############..", "...##########...", "...##########...", "...####..####...", "...####..####...", "...####..####...",
            "...####..####...", "...####..####...", "................", "................",
        };
        static readonly string[] Hameau =
        {
            "................", "................", "....#......#....", "...###....###...", "..#####..#####..", ".#######.######.",
            "..#####..#####..", "..#####..#####..", "..##.##..##.##..", "..##.##..##.##..", "..##.##..##.##..", "................",
            "................", "................", "................", "................",
        };
        static readonly string[] Donjon =
        {
            "................", "..##.##..##.##..", "..############..", "..############..", "...##########...", "...####..####...",
            "...###....###...", "...##########...", "...##########...", "...####..####...", "...###....###...", "...###....###...",
            "..############..", "..############..", "................", "................",
        };
        static readonly string[] Chope =
        {
            "................", "....########....", "...##########...", "...##########...", "....#######.##..", "....#######..#..",
            "....#######..#..", "....#######..#..", "....#######.##..", "....########....", "....#######.....", "....#######.....",
            "...#########....", "................", "................", "................",
        };
        static readonly string[] Enclume =
        {
            "................", "................", "................", ".#############..", "..############..", "....#########...",
            ".....#######....", "......#####.....", "......#####.....", ".....#######....", "....#########...", "....#########...",
            "................", "................", "................", "................",
        };
        static readonly string[] Bourse =
        {
            "................", "......####......", ".......##.......", "......####......", ".....######.....", "....########....",
            "...####..####...", "...###.##.###...", "...####..####...", "...###.##.###...", "...####..####...", "....########....",
            ".....######.....", "................", "................", "................",
        };
        static readonly string[] Roue =
        {
            "......####......", "....##.##.##....", "...#...##...#...", "..#....##....#..", "..##...##...##..", ".#..##.##.##..#.",
            ".#....####....#.", ".##############.", ".#....####....#.", ".#..##.##.##..#.", "..##...##...##..", "..#....##....#..",
            "...#...##...#...", "....##.##.##....", "......####......", "................",
        };
        static readonly string[] Croix =
        {
            "......####......", "......####......", "......####......", "..############..", "..############..", "......####......",
            "......####......", "......####......", "......####......", "......####......", "......####......", "......####......",
            "......####......", "................", "................", "................",
        };
        static readonly string[] Goutte =
        {
            ".......##.......", ".......##.......", "......####......", "......####......", ".....######.....", "....########....",
            "...##########...", "...##########...", "..############..", "..############..", "..############..", "...##########...",
            "....########....", ".....######.....", "................", "................",
        };
        static readonly string[] Seau =
        {
            "................", "....########....", "...#........#...", "...#........#...", "..############..", "..############..",
            "...##########...", "...##########...", "....########....", "....########....", ".....######.....", "................",
            "................", "................", "................", "................",
        };
        static readonly string[] Epee =
        {
            ".............##.", "............###.", "...........###..", "..........###...", ".........###....", "........###.....",
            ".......###......", "..#...###.......", "..##.###........", "...####.........", "....##..........", "...####.........",
            "..##..##........", ".##.............", "................", "................",
        };
        static readonly string[] Gelee =
        {
            "................", "................", "......####......", "....########....", "...##########...", "..####.##.####..",
            "..####.##.####..", ".##############.", ".##############.", ".##############.", "..############..", "................",
            "................", "................", "................", "................",
        };
        static readonly string[] Patte =
        {
            "................", "....##....##....", "...####..####...", "...####..####...", "....##....##....", ".##..........##.",
            "####........####", "####..####..####", ".##..######..##.", "....########....", "...##########...", "...##########...",
            "....########....", ".....##..##.....", "................", "................",
        };
        static readonly string[] Tente =
        {
            "................", ".......##.......", "......####......", "......####......", ".....######.....", ".....##..##.....",
            "....##....##....", "....##....##....", "...##......##...", "...##..##..##...", "..##..####..##..", "..##.######.##..",
            ".##..######..##.", ".##############.", "................", "................",
        };
        static readonly string[] Crane =
        {
            "................", ".....######.....", "...##########...", "..############..", "..############..", "..###..##..###..",
            "..##....#...##..", "..###..##..###..", "..############..", "...####..####...", "....########....", "....#.#.#.#.....",
            "....########....", "................", "................", "................",
        };
        static readonly string[] Etoile =
        {
            ".......##.......", ".......##.......", "......####......", "......####......", ".##############.", "..############..",
            "...##########...", "....########....", "....########....", "...####..####...", "...###....###...", "..##........##..",
            "................", "................", "................", "................",
        };
        static readonly string[] Drapeau =
        {
            "...#............", "...########.....", "...#########....", "...##########...", "...#########....", "...########.....",
            "...#............", "...#............", "...#............", "...#............", "...#............", "..###...........",
            "................", "................", "................", "................",
        };
    }
}
