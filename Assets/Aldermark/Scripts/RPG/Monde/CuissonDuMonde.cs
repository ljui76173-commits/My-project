using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// Les grilles de travail du grand monde (une case tous les 2 m) : hauteur, type de sol, forêt, eau.
    /// Calculées une fois à la génération, puis découpées en tuiles de terrain, en arbres, en carte peinte.
    /// Du calcul pur (aucune API de l'éditeur), réparti sur tous les cœurs.
    /// </summary>
    public class GrillesMonde
    {
        public const int G = 4096;
        public const float Pas = Geo.Taille / G;

        public readonly float[] h = new float[(G + 1) * (G + 1)];   // aux coins des cases
        public readonly byte[] sol = new byte[G * G];               // au centre des cases
        public readonly byte[] foret = new byte[G * G];             // 0…255
        public readonly float[] eau = new float[G * G];             // −10 000 : pas d'eau

        public static int Indice(float x, float z)
        {
            int i = Mathf.Clamp(Mathf.FloorToInt((x + Geo.Demi) / Pas), 0, G - 1);
            int j = Mathf.Clamp(Mathf.FloorToInt((z + Geo.Demi) / Pas), 0, G - 1);
            return j * G + i;
        }

        public float Hauteur(float x, float z)
        {
            const int N1 = G + 1;
            float fx = Mathf.Clamp((x + Geo.Demi) / Pas, 0f, G - .001f), fz = Mathf.Clamp((z + Geo.Demi) / Pas, 0f, G - .001f);
            int i = (int)fx, j = (int)fz;
            float u = fx - i, v = fz - j;
            float a = h[j * N1 + i], b = h[j * N1 + i + 1], c = h[(j + 1) * N1 + i], d = h[(j + 1) * N1 + i + 1];
            float ab = a + (b - a) * u, cd = c + (d - c) * u;
            return ab + (cd - ab) * v;
        }

        public static GrillesMonde Calculer(PlanDuMonde plan, Action<string, float> progres = null)
        {
            const int N1 = G + 1, bande = 128;
            var g = new GrillesMonde();
            for (int j0 = 0; j0 < N1; j0 += bande)
            {
                progres?.Invoke("Relief (16 millions de points)", .5f * j0 / N1);
                int j1 = Math.Min(N1, j0 + bande);
                Parallel.For(j0, j1, j =>
                {
                    float z = -Geo.Demi + j * Pas;
                    for (int i = 0; i < N1; i++) g.h[j * N1 + i] = plan.HauteurFinale(-Geo.Demi + i * Pas, z);
                });
            }
            for (int j0 = 0; j0 < G; j0 += bande)
            {
                progres?.Invoke("Sols, forêts et eaux", .5f + .5f * j0 / G);
                int j1 = Math.Min(G, j0 + bande);
                Parallel.For(j0, j1, j =>
                {
                    float z = -Geo.Demi + (j + .5f) * Pas;
                    int ja = Math.Max(0, j - 1), jb = Math.Min(G, j + 2);
                    for (int i = 0; i < G; i++)
                    {
                        float x = -Geo.Demi + (i + .5f) * Pas;
                        float hc = (g.h[j * N1 + i] + g.h[j * N1 + i + 1] + g.h[(j + 1) * N1 + i] + g.h[(j + 1) * N1 + i + 1]) * .25f;
                        // La pente sur ~6 m (le grain du sol ne compte pas)
                        int ia = Math.Max(0, i - 1), ib = Math.Min(G, i + 2);
                        float dx = (g.h[j * N1 + ib] + g.h[(j + 1) * N1 + ib] - g.h[j * N1 + ia] - g.h[(j + 1) * N1 + ia]) / (2f * (ib - ia) * Pas);
                        float dz = (g.h[jb * N1 + i] + g.h[jb * N1 + i + 1] - g.h[ja * N1 + i] - g.h[ja * N1 + i + 1]) / (2f * (jb - ja) * Pas);
                        var s = plan.Classer(x, z, hc, Mathf.Sqrt(dx * dx + dz * dz), out float f);
                        int k = j * G + i;
                        g.sol[k] = (byte)s;
                        g.foret[k] = (byte)Mathf.RoundToInt(Mathf.Clamp01(f) * 255f);
                        g.eau[k] = plan.NiveauEau(x, z, hc);
                    }
                });
            }
            return g;
        }

        // ---------- les couleurs du terrain ----------

        /// <summary>
        /// Le poids de chaque type de sol pour les cases d'une tuile (res × res × nc) : chaque case pèse ses voisines
        /// (bords doux), puis des transitions nettes et irrégulières, comme peintes au pinceau.
        /// </summary>
        public void PoidsTuile(int ti, int tj, int res, int nc, float[] poids)
        {
            Array.Clear(poids, 0, poids.Length);
            Parallel.For(0, res, b =>
            {
                int gj = tj * res + b;
                for (int a = 0; a < res; a++)
                {
                    int gi = ti * res + a, o = (b * res + a) * nc;
                    for (int dj = -1; dj <= 1; dj++)
                        for (int di = -1; di <= 1; di++)
                        {
                            int jj = Mathf.Clamp(gj + dj, 0, G - 1), ii = Mathf.Clamp(gi + di, 0, G - 1);
                            poids[o + sol[jj * G + ii]] += (di == 0 ? 2f : 1f) * (dj == 0 ? 2f : 1f);
                        }
                    float somme = 0f;
                    for (int k = 0; k < nc; k++)
                    {
                        float w = poids[o + k];
                        if (w <= 0f) continue;
                        w = w / 16f * (.7f + .6f * Geo.Hasard(gi + k * 7919, gj, 3));
                        w = w * w * w;
                        poids[o + k] = w;
                        somme += w;
                    }
                    for (int k = 0; k < nc; k++) poids[o + k] = somme > 0f ? poids[o + k] / somme : (k == 0 ? 1f : 0f);
                }
            });
        }

        // ---------- forêts, bosquets, rochers ----------

        public struct Plante
        {
            public int proto;            // 0-1 sapins, 2-4 feuillus dorés, 5-6 buissons, 8 rocher
            public float x, z, largeur, hauteur, rotation;
        }

        /// <summary>
        /// Un arbre, un buisson ou un rocher au plus tous les 7 m, selon la forêt, le sol, l'altitude.
        /// <paramref name="libre"/> dit si un point est libre de tout bâtiment (fermes, faubourgs).
        /// </summary>
        public List<Plante> Plantes(Func<float, float, bool> libre = null, Action<float> progres = null)
        {
            var liste = new List<Plante>(260000);
            const float pas = 7f;
            int n = Mathf.FloorToInt(Geo.Taille / pas);
            for (int cj = 0; cj < n; cj++)
            {
                if (cj % 64 == 0) progres?.Invoke((float)cj / n);
                for (int ci = 0; ci < n; ci++)
                {
                    float x = -Geo.Demi + (ci + .15f + .7f * Geo.Hasard(ci, cj, 101)) * pas;
                    float z = -Geo.Demi + (cj + .15f + .7f * Geo.Hasard(ci, cj, 102)) * pas;
                    int k = Indice(x, z);
                    var s = (TypeSol)sol[k];
                    float hh = Hauteur(x, z);
                    if (eau[k] > hh - .3f) continue;
                    if (s == TypeSol.Chemin || s == TypeSol.Pave || s == TypeSol.Sable || s == TypeSol.LitRiviere || s == TypeSol.Neige || Geo.EstChamp(s)) continue;
                    bool enVille = false;
                    foreach (var site in Geo.Sites)
                        if ((site.position - new Vector2(x, z)).sqrMagnitude < (site.rayon + 18f) * (site.rayon + 18f)) { enVille = true; break; }
                    if (enVille || Geo.DistanceRoute(x, z) < 3.5f || (libre != null && !libre(x, z))) continue;

                    float f = foret[k] / 255f;
                    float r = Geo.Hasard(ci, cj, 103), r2 = Geo.Hasard(ci, cj, 104), rot = Geo.Hasard(ci, cj, 105) * Mathf.PI * 2f;
                    if (s == TypeSol.Roche)
                    {
                        if (r < .07f) { float e = .8f + r2 * 2.2f; liste.Add(new Plante { proto = 8, x = x, z = z, largeur = e * 1.2f, hauteur = e * .8f, rotation = rot }); }
                        continue;
                    }
                    bool arbre = f > .04f ? r < f * .92f : r > .993f;
                    if (arbre)
                    {
                        // Des sapins en altitude et au nord, des feuillus dorés dans les basses terres du sud
                        float coni = .3f + Geo.Lisse(110f, 320f, hh) * .7f + Geo.Lisse(800f, 3600f, z) * .35f + (Geo.Fbm(x * .0022f, z * .0022f, 2, 60) - .5f) * 1.1f;
                        bool sapin = Geo.Hasard(ci, cj, 106) < Mathf.Clamp(coni, .08f, .97f);
                        int proto;
                        if (sapin) proto = r2 < .55f ? 0 : 1;
                        else
                        {
                            float teinte = Geo.Fbm(x * .006f + 4f, z * .006f, 2, 61);
                            proto = teinte < .44f ? 2 : teinte < .56f ? 3 : 4;
                        }
                        float e = .85f + r2 * .7f;
                        float hauteur = e * (sapin ? 1f + Geo.Hasard(ci, cj, 107) * .6f : .9f + Geo.Hasard(ci, cj, 107) * .35f);
                        liste.Add(new Plante { proto = proto, x = x, z = z, largeur = e, hauteur = hauteur, rotation = rot });
                    }
                    else if ((f > .1f && r2 < .22f) || (f <= .1f && r2 < .018f))
                    {
                        float e = .6f + Geo.Hasard(ci, cj, 108) * .8f;
                        liste.Add(new Plante { proto = 5 + (int)(Geo.Hasard(ci, cj, 109) * 1.999f), x = x, z = z, largeur = e, hauteur = e, rotation = rot });
                    }
                    else if (Geo.Hasard(ci, cj, 110) < .004f)
                    {
                        float e = .4f + r2 * 1.4f;
                        liste.Add(new Plante { proto = 8, x = x, z = z, largeur = e * 1.2f, hauteur = e * .7f, rotation = rot });
                    }
                }
            }
            return liste;
        }

        // ---------- la carte peinte ----------

        static Color Teinte(string hex)
        {
            int v = Convert.ToInt32(hex.TrimStart('#'), 16);
            return new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f, 1f);
        }

        /// <summary>La carte du monde, G × G (2 m par pixel) : relief ombré, courbes de niveau, eaux, forêts, champs, routes, villes.</summary>
        public Color32[] PeindreCarte(PlanDuMonde plan)
        {
            const int N1 = G + 1;
            var px = new Color32[G * G];
            var indexRivieres = new IndexTraces(plan.rivieres, 64f);
            Geo.DistanceRoute(0f, 0f);   // l'index des routes, construit avant les fils de calcul
            Color[] couleurs =
            {
                Teinte("#a3ba72"), Teinte("#cbb76c"), Teinte("#8ea563"), Teinte("#d8c070"), Teinte("#b4c16a"), Teinte("#b48e60"),
                Teinte("#e4d4a6"), Teinte("#9c978c"), Teinte("#d6c38f"), Teinte("#79a9c9"), Teinte("#ecebe6"), Teinte("#d9cfbf"),
            };
            Color eauProfonde = Teinte("#4f7fa6"), eauClaire = Teinte("#79a9c9"), feuillus = Teinte("#5a7a40"), sapins = Teinte("#3f5a36");
            Color teinteRoute = Teinte("#efe2b8"), bordRoute = Teinte("#7a6448"), toits = Teinte("#a0705a");
            var lumiere = Normer(-1f, 1.4f, 1f);
            Parallel.For(0, G, j =>
            {
                float z = -Geo.Demi + (j + .5f) * Pas;
                for (int i = 0; i < G; i++)
                {
                    float x = -Geo.Demi + (i + .5f) * Pas;
                    int k = j * G + i;
                    float hc = (h[j * N1 + i] + h[j * N1 + i + 1] + h[(j + 1) * N1 + i] + h[(j + 1) * N1 + i + 1]) * .25f;
                    var s = (TypeSol)sol[k];
                    Color c;
                    float n = eau[k];
                    bool dansEau = n > hc + .05f || (indexRivieres.Proche(x, z, 30f, out float dr, out _, out float lr) && dr < lr * .5f + 1.5f);
                    if (dansEau)
                    {
                        float prof = n > -9000f ? n - hc : 1f;
                        c = Color.Lerp(eauClaire, eauProfonde, Mathf.Clamp01(prof / 14f));
                    }
                    else
                    {
                        c = couleurs[(int)s];
                        if (s == TypeSol.Herbe || s == TypeSol.HerbeDoree || s == TypeSol.HerbeSombre)
                            c = Color.Lerp(c, couleurs[7], Geo.Lisse(260f, 420f, hc) * .5f);   // plus gris en altitude
                        float fo = foret[k] / 255f;
                        if (fo > .2f)
                        {
                            // Les forêts : une masse sombre piquée de cimes
                            bool resineux = Geo.Lisse(110f, 320f, hc) + Geo.Lisse(800f, 3600f, z) * .4f > .5f;
                            c = Color.Lerp(c, resineux ? sapins : feuillus, Mathf.Clamp01((fo - .2f) * 2.2f));
                            if (Geo.Hasard(i >> 1, j >> 1, 9) < fo * .5f) c *= .82f;
                        }
                    }
                    // Relief ombré (lumière du nord-ouest, pente mesurée sur 8 m) et courbes de niveau tous les 50 m
                    int ia = Math.Max(0, i - 2), ib = Math.Min(G, i + 2), ja = Math.Max(0, j - 2), jb = Math.Min(G, j + 2);
                    float dx = (h[j * N1 + ib] - h[j * N1 + ia]) / ((ib - ia) * Pas), dz = (h[jb * N1 + i] - h[ja * N1 + i]) / ((jb - ja) * Pas);
                    float nl = 1f / Mathf.Sqrt(dx * dx + 1f + dz * dz);
                    float ombre = Mathf.Clamp01((-dx * lumiere.x + lumiere.y - dz * lumiere.z) * nl);
                    c *= Mathf.Lerp(.62f, 1.12f, ombre);
                    if (!dansEau && hc > 2f)
                    {
                        float h2 = h[j * N1 + Math.Min(G, i + 1)], h3 = h[Math.Min(G, j + 1) * N1 + i];
                        if (Mathf.Floor(hc / 50f) != Mathf.Floor(h2 / 50f) || Mathf.Floor(hc / 50f) != Mathf.Floor(h3 / 50f)) c *= .84f;
                    }
                    // Les routes, un peu élargies pour se lire de loin
                    float d = Geo.DistanceRoute(x, z);
                    if (d < 1.6f) c = s == TypeSol.Pave ? Color.Lerp(teinteRoute, couleurs[11], .5f) : teinteRoute;
                    else if (d < 3.4f) c = Color.Lerp(c, bordRoute, .6f);
                    // Le cœur pavé des villes
                    if (s == TypeSol.Pave && d >= 1.6f) c = Color.Lerp(c, toits, Geo.Hasard(i >> 3, j >> 3, 12) < .55f ? .55f : .15f);
                    c *= .97f + Geo.Hasard(i, j, 13) * .06f;   // grain du papier
                    // Bord de parchemin
                    float bx = Math.Min(i, G - 1 - i) / (float)G, by = Math.Min(j, G - 1 - j) / (float)G;
                    c = Color.Lerp(c * .55f, c, Mathf.Clamp01(Math.Min(bx, by) / .03f));
                    c.a = 1f;
                    px[k] = c;
                }
            });
            return px;
        }

        static Vector3 Normer(float x, float y, float z)
        {
            float m = Mathf.Sqrt(x * x + y * y + z * z);
            return new Vector3(x / m, y / m, z / m);
        }
    }
}
