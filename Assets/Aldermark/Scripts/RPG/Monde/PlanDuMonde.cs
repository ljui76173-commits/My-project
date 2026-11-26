using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// Le plan du grand monde, calculé une fois à la génération : le relief naturel sur une grille de 32 m,
    /// l'écoulement des eaux (les rivières naissent là où assez d'eau se rassemble, les lacs dans les cuvettes),
    /// les villes et villages des deux royaumes, les routes qui les relient (en évitant les pentes, en réutilisant
    /// les routes existantes, avec un pont à chaque rivière). Il donne ensuite le relief final en tout point
    /// (lits des rivières creusés, villes aplanies, routes nivelées) et le type de sol.
    /// </summary>
    public class PlanDuMonde
    {
        public const int N = 256;
        public const float Pas = Geo.Taille / N;

        public readonly float[] hBase = new float[N * N];
        public readonly float[] hRemplie = new float[N * N];
        readonly int[] aval = new int[N * N];
        readonly float[] accumulation = new float[N * N];
        readonly int[] lacDe = new int[N * N];
        readonly float[] distEau = new float[N * N];      // distance (m) à une rivière ou un lac
        readonly float[] distMer = new float[N * N];      // distance (m) à la mer

        public readonly List<Trace> rivieres = new List<Trace>();
        public readonly List<Trace> routes = new List<Trace>();
        public readonly List<Lac> lacs = new List<Lac>();
        public readonly List<Site> sites = new List<Site>();
        public readonly List<Pont> ponts = new List<Pont>();
        public readonly List<(Vector2 position, int espece, int niveau)> repaires = new List<(Vector2, int, int)>();

        IndexTraces indexRivieres, indexRoutes;
        readonly System.Random hasard;

        public PlanDuMonde(int graine = 7) { hasard = new System.Random(graine); }

        public static Vector2 Centre(int c) => new Vector2(-Geo.Demi + (c % N + .5f) * Pas, -Geo.Demi + (c / N + .5f) * Pas);
        public static int Cellule(Vector2 p)
        {
            int i = Mathf.Clamp(Mathf.FloorToInt((p.x + Geo.Demi) / Pas), 0, N - 1), j = Mathf.Clamp(Mathf.FloorToInt((p.y + Geo.Demi) / Pas), 0, N - 1);
            return j * N + i;
        }

        public void Calculer(Action<string, float> progres = null)
        {
            progres?.Invoke("Relief de base", 0f);
            for (int c = 0; c < N * N; c++) { var p = Centre(c); hBase[c] = Geo.HauteurBase(p.x, p.y); }
            progres?.Invoke("Écoulement des eaux", .15f);
            Ecoulement();
            Lacs();
            Rivieres();
            Distances();
            progres?.Invoke("Villes et villages", .35f);
            Sites();
            progres?.Invoke("Routes", .5f);
            indexRivieres = new IndexTraces(rivieres, 64f);
            Routes();
            indexRoutes = new IndexTraces(routes, 64f);
            Ponts();
            ProfilsDesRoutes();
            Repaires();
            progres?.Invoke("Plan terminé", 1f);
        }

        // ---------- l'eau ----------

        static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 }, DZ = { 0, 0, 1, -1, 1, -1, 1, -1 };

        /// <summary>Remplissage par priorité : chaque case sait vers où son eau s'écoule, jusqu'à la mer ou au bord.</summary>
        void Ecoulement()
        {
            var tas = new TasMin(N * N);
            var vu = new bool[N * N];
            for (int c = 0; c < N * N; c++)
            {
                int i = c % N, j = c / N;
                aval[c] = -1;
                if (i == 0 || j == 0 || i == N - 1 || j == N - 1 || hBase[c] < 0f)
                {
                    hRemplie[c] = hBase[c];
                    vu[c] = true;
                    tas.Ajouter(hRemplie[c], c);
                }
            }
            while (tas.Nombre > 0)
            {
                int c = tas.Retirer();
                int i = c % N, j = c / N;
                for (int k = 0; k < 8; k++)
                {
                    int ni = i + DX[k], nj = j + DZ[k];
                    if (ni < 0 || nj < 0 || ni >= N || nj >= N) continue;
                    int n = nj * N + ni;
                    if (vu[n]) continue;
                    vu[n] = true;
                    hRemplie[n] = Math.Max(hBase[n], hRemplie[c] + .002f);
                    aval[n] = c;
                    // Un peu de hasard dans l'ordre : dans les plats, l'eau serpente au lieu d'aller tout droit
                    tas.Ajouter(hRemplie[n] + Geo.Hasard(ni, nj, 77) * 1.2f, n);
                }
            }
            // Accumulation : chaque case reçoit l'eau de toutes celles qui s'écoulent vers elle
            var ordre = new int[N * N];
            for (int c = 0; c < N * N; c++) { ordre[c] = c; accumulation[c] = hBase[c] > 0f ? 1f + Math.Max(0f, hBase[c]) * .002f : 0f; }
            Array.Sort(ordre, (a, b) => hRemplie[b].CompareTo(hRemplie[a]));
            foreach (int c in ordre) if (aval[c] >= 0) accumulation[aval[c]] += accumulation[c];
        }

        void Lacs()
        {
            for (int c = 0; c < N * N; c++) lacDe[c] = -1;
            var pile = new Stack<int>();
            for (int s = 0; s < N * N; s++)
            {
                if (lacDe[s] != -1 || hBase[s] < 2f || hRemplie[s] - hBase[s] < 1.5f) continue;
                var cellules = new List<int>();
                float profondeur = 0f, niveau = 0f;
                lacDe[s] = -2;
                pile.Push(s);
                while (pile.Count > 0)
                {
                    int c = pile.Pop();
                    cellules.Add(c);
                    profondeur = Math.Max(profondeur, hRemplie[c] - hBase[c]);
                    niveau = Math.Max(niveau, hRemplie[c]);
                    int i = c % N, j = c / N;
                    for (int k = 0; k < 4; k++)
                    {
                        int ni = i + DX[k], nj = j + DZ[k];
                        if (ni < 0 || nj < 0 || ni >= N || nj >= N) continue;
                        int n = nj * N + ni;
                        if (lacDe[n] != -1 || hBase[n] < 2f || hRemplie[n] - hBase[n] < 1.5f) continue;
                        lacDe[n] = -2;
                        pile.Push(n);
                    }
                }
                // Les petites et moyennes cuvettes deviennent des lacs ; les grandes sont percées par leur rivière (une gorge)
                bool garde = cellules.Count >= 10 && cellules.Count <= 260 && profondeur >= 3f && profondeur < 40f;
                int id = garde ? lacs.Count : -3;
                Vector2 centre = Vector2.zero;
                foreach (int c in cellules) { lacDe[c] = id; centre += Centre(c); }
                if (garde) lacs.Add(new Lac { niveau = niveau - .3f, cellules = cellules.ToArray(), centre = centre / cellules.Count });
            }
            for (int c = 0; c < N * N; c++) if (lacDe[c] < 0) lacDe[c] = -1;
        }

        void Rivieres()
        {
            // Les cases où assez d'eau se rassemble deviennent rivière
            const float Seuil = 900f;
            var estRiviere = new bool[N * N];
            for (int c = 0; c < N * N; c++) estRiviere[c] = accumulation[c] >= Seuil && hBase[c] >= -1f;
            var amont = new int[N * N];
            for (int c = 0; c < N * N; c++) if (estRiviere[c] && aval[c] >= 0 && estRiviere[aval[c]]) amont[aval[c]]++;
            var sources = new List<int>();
            for (int c = 0; c < N * N; c++) if (estRiviere[c] && amont[c] == 0) sources.Add(c);
            sources.Sort((a, b) => accumulation[a].CompareTo(accumulation[b]));
            var parcourue = new bool[N * N];
            // Les plus longues d'abord : tracer depuis chaque source jusqu'à la mer, au bord ou à une rivière déjà tracée
            sources.Sort((a, b) => hRemplie[b].CompareTo(hRemplie[a]));
            foreach (int s in sources)
            {
                var chemin = new List<int>();
                int c = s;
                while (c >= 0)
                {
                    chemin.Add(c);
                    if (parcourue[c]) break;
                    parcourue[c] = true;
                    if (hBase[c] < -1f) break;
                    c = aval[c];
                }
                if (chemin.Count < 6) continue;
                var pts = new List<Vector2>();
                var larg = new List<float>();
                var niv = new List<float>();
                // La surface de l'eau descend toujours : à plat dans un lac, puis elle creuse sa vallée (et perce les cuvettes)
                float courant = float.MaxValue;
                foreach (int k in chemin)
                {
                    pts.Add(Centre(k));
                    larg.Add(Mathf.Clamp(4f + Mathf.Sqrt(accumulation[k]) * .32f, 6f, 34f));
                    if (lacDe[k] >= 0) courant = lacs[lacDe[k]].niveau;
                    else courant = Math.Min(courant, hBase[k] - .8f);
                    niv.Add(Math.Max(.2f, courant));
                }
                // Une embouchure qui avance un peu dans la mer
                var fin = chemin[chemin.Count - 1];
                if (hBase[fin] < 0f && pts.Count >= 2) { pts.Add(pts[pts.Count - 1] + (pts[pts.Count - 1] - pts[pts.Count - 2]).normalized * 60f); larg.Add(larg[larg.Count - 1] * 1.4f); niv.Add(.2f); }
                var t = Lisser(new Trace { points = pts.ToArray(), largeurs = larg.ToArray(), niveaux = niv.ToArray() }, 3);
                Meandres(t, rivieres.Count);
                rivieres.Add(t);
            }
            rivieres.Sort((a, b) => b.points.Length.CompareTo(a.points.Length));
            string[] noms = { "la Brève", "l'Orvanne", "la Sauldre", "le Cléon", "la Morte-Eau", "l'Aubrac", "la Lisière", "le Gué-Noir", "la Vionne", "l'Ardel", "la Sorgue", "le Ruisseau-Clair" };
            for (int k = 0; k < rivieres.Count; k++) rivieres[k].nom = k < noms.Length ? noms[k] : "un ruisseau";
        }

        /// <summary>Des méandres : chaque point s'écarte de côté, selon un bruit le long de la rivière (pas au départ ni à l'arrivée).</summary>
        static void Meandres(Trace t, int graine)
        {
            int n = t.points.Length;
            if (n < 4) return;
            var p = (Vector2[])t.points.Clone();
            float s = 0f;
            for (int k = 1; k < n - 1; k++)
            {
                s += (t.points[k] - t.points[k - 1]).magnitude;
                Vector2 dir = (t.points[k + 1] - t.points[k - 1]).normalized;
                var cote = new Vector2(-dir.y, dir.x);
                float amp = 10f + t.largeurs[k] * .9f;
                float bord = Math.Min(1f, Math.Min(k, n - 1 - k) / 6f);
                float o = (Geo.Fbm(s / 170f, graine * 3.7f, 2, 91) - .5f) * 2.4f * amp * bord;
                p[k] = t.points[k] + cote * o;
            }
            t.points = p;
        }

        void Distances()
        {
            // Distance à l'eau douce (rivières, lacs) et à la mer, par propagation sur la grille
            var file = new Queue<int>();
            for (int c = 0; c < N * N; c++) { distEau[c] = float.MaxValue; distMer[c] = float.MaxValue; }
            foreach (var r in rivieres) foreach (var p in r.points) { int c = Cellule(p); if (distEau[c] > 0f) { distEau[c] = 0f; file.Enqueue(c); } }
            foreach (var l in lacs) foreach (int c in l.cellules) if (distEau[c] > 0f) { distEau[c] = 0f; file.Enqueue(c); }
            Propager(distEau, file);
            for (int c = 0; c < N * N; c++) if (hBase[c] < 0f) { distMer[c] = 0f; file.Enqueue(c); }
            Propager(distMer, file);
        }

        static void Propager(float[] dist, Queue<int> file)
        {
            while (file.Count > 0)
            {
                int c = file.Dequeue();
                int i = c % N, j = c / N;
                for (int k = 0; k < 8; k++)
                {
                    int ni = i + DX[k], nj = j + DZ[k];
                    if (ni < 0 || nj < 0 || ni >= N || nj >= N) continue;
                    int n = nj * N + ni;
                    float d = dist[c] + (k < 4 ? Pas : Pas * 1.4142f);
                    if (d < dist[n]) { dist[n] = d; file.Enqueue(n); }
                }
            }
        }

        // ---------- villes et villages ----------

        static readonly string[][] NomsVilles =
        {
            new[] { "Aldenne", "Brumefort", "Hautval", "Port-Salin" },
            new[] { "Valdor", "Castelroc", "Ombrevaux", "Port-Cendre" },
        };
        static readonly string[][] NomsVillages =
        {
            new[] { "Varenne", "Les Saules", "Morteau", "Combelaine", "Pierrefeu", "Sainte-Aude", "Brisac" },
            new[] { "Fontbrune", "Lirac", "Grisemont", "Belcombe", "Arvel", "Roche-Aigue", "Malvoisin" },
        };
        static readonly string[][] NomsHameaux =
        {
            new[] { "Ruisseau-Clair", "Les Ajoncs", "Champcreux", "Le Bois-Joli" },
            new[] { "Haute-Lande", "Les Cendres", "Coudray", "Le Gué-Noir" },
        };

        float Pente(int c)
        {
            int i = c % N, j = c / N;
            float m = 0f;
            for (int k = 0; k < 4; k++)
            {
                int ni = Mathf.Clamp(i + DX[k], 0, N - 1), nj = Mathf.Clamp(j + DZ[k], 0, N - 1);
                m = Math.Max(m, Math.Abs(hBase[nj * N + ni] - hBase[c]) / Pas);
            }
            return m;
        }

        /// <summary>Pente moyenne et hauteur moyenne dans un rayon (m) autour d'une case.</summary>
        (float pente, float hauteur, bool sec) Zone(int c, float rayon)
        {
            int r = Mathf.CeilToInt(rayon / Pas), ci = c % N, cj = c / N;
            float sp = 0f, sh = 0f; int n = 0; bool sec = true;
            for (int j = cj - r; j <= cj + r; j++)
                for (int i = ci - r; i <= ci + r; i++)
                {
                    if (i < 1 || j < 1 || i >= N - 1 || j >= N - 1) return (9f, 0f, false);
                    if ((i - ci) * (i - ci) + (j - cj) * (j - cj) > r * r) continue;
                    int k = j * N + i;
                    if (hBase[k] < 3f || lacDe[k] >= 0) sec = false;
                    sp += Pente(k); sh += hBase[k]; n++;
                }
            return (sp / Math.Max(1, n), sh / Math.Max(1, n), sec);
        }

        bool AssezLoin(Vector2 p, float minimum)
        {
            foreach (var s in sites) if ((s.position - p).magnitude < minimum + s.rayon) return false;
            return true;
        }

        /// <summary>Cherche la meilleure case selon un score (plus petit = mieux), parmi celles qui passent le filtre.</summary>
        int Meilleure(Func<int, float> score)
        {
            int best = -1; float bs = float.MaxValue;
            for (int j = 6; j < N - 6; j += 2)
                for (int i = 6; i < N - 6; i += 2)
                {
                    int c = j * N + i;
                    float s = score(c);
                    if (s < bs) { bs = s; best = c; }
                }
            return best;
        }

        Site Poser(int c, string nom, TypeSite type, int royaume, float rayon)
        {
            var p = Centre(c);
            var z = Zone(c, rayon);
            var s = new Site { nom = nom, type = type, royaume = royaume, position = p, rayon = rayon, altitude = z.hauteur, angle = (float)hasard.NextDouble() * 360f };
            if (type == TypeSite.Capitale)
            {
                float a = s.angle * Mathf.Deg2Rad;
                s.fort = p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rayon * .45f;
            }
            sites.Add(s);
            return s;
        }

        bool DansLeRoyaume(Vector2 p, int r, float marge) => r == 0 ? p.x < Geo.CreteX(p.y) - marge : p.x > Geo.CreteX(p.y) + marge;

        float ScoreSite(int c, int royaume, float rayon, float penteMax, float altMax, float margeEau)
        {
            var p = Centre(c);
            if (!DansLeRoyaume(p, royaume, 500f) || hBase[c] < 6f || hBase[c] > altMax || distEau[c] < rayon + margeEau || distMer[c] < rayon + 60f) return float.MaxValue;
            var z = Zone(c, rayon);
            if (!z.sec || z.pente > penteMax) return float.MaxValue;
            return z.pente * 10f + (float)hasard.NextDouble() * .4f;
        }

        void Sites()
        {
            // Le centre de chaque royaume (moyenne des terres habitables)
            var centre = new Vector2[2]; var compte = new int[2];
            for (int c = 0; c < N * N; c++)
            {
                if (hBase[c] < 8f || hBase[c] > 160f) continue;
                var p = Centre(c);
                int r = Geo.Royaume(p.x, p.y);
                if (!DansLeRoyaume(p, r, 500f)) continue;
                centre[r] += p; compte[r]++;
            }
            for (int r = 0; r < 2; r++) centre[r] /= Math.Max(1, compte[r]);

            for (int r = 0; r < 2; r++)
            {
                // La capitale : plate, près d'une rivière (mais pas dedans), pas loin du cœur du royaume
                int k = r;
                int c = Meilleure(x =>
                {
                    float s = ScoreSite(x, k, 240f, .16f, 170f, 50f);
                    if (s == float.MaxValue) return s;
                    float dr = distEau[x];
                    return s + (Centre(x) - centre[k]).magnitude / 900f + (dr > 700f ? 2f : 0f);
                });
                for (float souple = 1.3f; c < 0 && souple < 3f; souple += .35f)
                {
                    float sp = souple;
                    c = Meilleure(x =>
                    {
                        float s = ScoreSite(x, k, 240f * (2f - sp) * .5f + 120f, .16f * sp, 170f * sp, 30f);
                        return s == float.MaxValue ? s : s + (Centre(x) - centre[k]).magnitude / 900f;
                    });
                }
                if (c >= 0) Poser(c, NomsVilles[r][0], TypeSite.Capitale, r, 240f);
            }
            // Un port sur la côte de chaque royaume qui en a une
            for (int r = 0; r < 2; r++)
            {
                int k = r;
                int c = Meilleure(x =>
                {
                    var p = Centre(x);
                    if (!DansLeRoyaume(p, k, 400f) || hBase[x] < 4f || hBase[x] > 40f || distMer[x] < 130f || distMer[x] > 360f || distEau[x] < 150f || !AssezLoin(p, 1200f)) return float.MaxValue;
                    var z = Zone(x, 110f);
                    return z.sec && z.pente < .16f ? z.pente * 10f + (float)hasard.NextDouble() * .3f : float.MaxValue;
                });
                if (c >= 0) Poser(c, NomsVilles[r][3], TypeSite.Port, r, 110f);
            }
            // Deux villes par royaume
            for (int r = 0; r < 2; r++)
                for (int n = 1; n <= 2; n++)
                {
                    int k = r;
                    int c = Meilleure(x => AssezLoin(Centre(x), 1300f) ? ScoreSite(x, k, 120f, .17f, 220f, 40f) : float.MaxValue);
                    if (c >= 0) Poser(c, NomsVilles[r][n], TypeSite.Ville, r, 120f);
                }
            // Le hameau de départ : à l'écart, à 1,2–1,8 km de la capitale de l'ouest
            var capOuest = sites.Find(s => s.type == TypeSite.Capitale && s.royaume == 0);
            if (capOuest != null)
            {
                int c = Meilleure(x =>
                {
                    var p = Centre(x);
                    float d = (p - capOuest.position).magnitude;
                    if (d < 1200f || d > 1800f || !AssezLoin(p, 500f)) return float.MaxValue;
                    float s = ScoreSite(x, 0, 30f, .2f, 200f, 25f);
                    return s;
                });
                if (c >= 0)
                {
                    var depart = Poser(c, "Ruisseau-Clair", TypeSite.Depart, 0, 30f);
                    // Varenne, le village voisin, à 450–800 m
                    int v = Meilleure(x =>
                    {
                        var p = Centre(x);
                        float d = (p - depart.position).magnitude;
                        if (d < 450f || d > 800f || !AssezLoin(p, 380f)) return float.MaxValue;
                        return ScoreSite(x, 0, 60f, .18f, 200f, 25f);
                    });
                    if (v >= 0) Poser(v, "Varenne", TypeSite.Village, 0, 60f);
                }
            }
            // Les autres villages et les hameaux
            for (int r = 0; r < 2; r++)
            {
                int k = r;
                int premier = r == 0 ? 1 : 0;
                for (int n = premier; n < NomsVillages[r].Length; n++)
                {
                    int c = Meilleure(x => AssezLoin(Centre(x), 620f) ? ScoreSite(x, k, 60f, .18f, 240f, 25f) : float.MaxValue);
                    if (c >= 0) Poser(c, NomsVillages[r][n], TypeSite.Village, r, 60f);
                }
                int premierH = r == 0 ? 1 : 0;
                for (int n = premierH; n < NomsHameaux[r].Length; n++)
                {
                    int c = Meilleure(x => AssezLoin(Centre(x), 420f) ? ScoreSite(x, k, 30f, .22f, 280f, 20f) : float.MaxValue);
                    if (c >= 0) Poser(c, NomsHameaux[r][n], TypeSite.Hameau, r, 30f);
                }
            }
        }

        // ---------- routes ----------

        void Routes()
        {
            var surRoute = new bool[N * N];
            var liaisons = new List<(Site a, Site b, float largeur)>();
            Site Plus(Site s, Predicate<Site> f) { Site b = null; float bd = float.MaxValue; foreach (var o in sites) if (o != s && f(o)) { float d = (o.position - s.position).magnitude; if (d < bd) { bd = d; b = o; } } return b; }
            var caps = sites.FindAll(s => s.type == TypeSite.Capitale);
            if (caps.Count == 2) liaisons.Add((caps[0], caps[1], 7f));
            foreach (var s in sites.FindAll(x => x.type == TypeSite.Ville || x.type == TypeSite.Port))
            {
                var cap = caps.Find(c => c.royaume == s.royaume);
                if (cap != null) liaisons.Add((s, cap, 6.5f));
            }
            foreach (var s in sites.FindAll(x => x.type == TypeSite.Ville))
            {
                var autre = Plus(s, o => o.type == TypeSite.Ville && o.royaume == s.royaume);
                if (autre != null) liaisons.Add((s, autre, 6f));
            }
            foreach (var s in sites.FindAll(x => x.type == TypeSite.Village))
            {
                var b = Plus(s, o => o.royaume == s.royaume && o.type <= TypeSite.Port);
                var v = Plus(s, o => o.royaume == s.royaume && o.type == TypeSite.Village);
                if (b != null) liaisons.Add((s, v != null && (v.position - s.position).magnitude < (b.position - s.position).magnitude * .55f ? v : b, 5f));
            }
            foreach (var s in sites.FindAll(x => x.type == TypeSite.Hameau || x.type == TypeSite.Depart))
            {
                var v = Plus(s, o => o.type <= TypeSite.Village);
                if (v != null) liaisons.Add((s, v, 3.6f));
            }
            foreach (var (a, b, largeur) in liaisons)
            {
                var chemin = AEtoile(Cellule(a.position), Cellule(b.position), surRoute);
                if (chemin == null || chemin.Count < 2) continue;
                foreach (int c in chemin) surRoute[c] = true;
                var pts = new List<Vector2> { a.position };
                for (int k = 1; k < chemin.Count - 1; k++) pts.Add(Centre(chemin[k]));
                pts.Add(b.position);
                var lis = Lisser(new Trace { points = pts.ToArray(), largeurs = Remplir(pts.Count, largeur), niveaux = new float[pts.Count] }, 3);
                var t = Reechantillonner(lis, 9f);
                t.nom = $"Route de {a.nom} à {b.nom}";
                routes.Add(t);
            }
        }

        static float[] Remplir(int n, float v) { var a = new float[n]; for (int k = 0; k < n; k++) a[k] = v; return a; }

        List<int> AEtoile(int depart, int arrivee, bool[] surRoute)
        {
            var cout = new float[N * N];
            var prec = new int[N * N];
            for (int c = 0; c < N * N; c++) { cout[c] = float.MaxValue; prec[c] = -1; }
            var tas = new TasMin(N * N);
            cout[depart] = 0f;
            var fin = Centre(arrivee);
            tas.Ajouter((Centre(depart) - fin).magnitude * .55f, depart);
            var ferme = new bool[N * N];
            while (tas.Nombre > 0)
            {
                int c = tas.Retirer();
                if (ferme[c]) continue;
                ferme[c] = true;
                if (c == arrivee) break;
                int i = c % N, j = c / N;
                for (int k = 0; k < 8; k++)
                {
                    int ni = i + DX[k], nj = j + DZ[k];
                    if (ni < 1 || nj < 1 || ni >= N - 1 || nj >= N - 1) continue;
                    int n = nj * N + ni;
                    if (ferme[n] || hBase[n] < 1f || (lacDe[n] >= 0 && n != arrivee)) continue;
                    float d = k < 4 ? Pas : Pas * 1.4142f;
                    float pente = Math.Abs(hBase[n] - hBase[c]) / d;
                    float pas = d * (1f + 30f * pente * pente + (pente > .2f ? 60f * (pente - .2f) : 0f));
                    if (hBase[n] > 260f) pas *= 2.5f;
                    if (distEau[n] < Pas * .6f) pas += 700f;                // traverser une rivière : un pont, une fois
                    if (surRoute[n]) pas *= .45f;                           // reprendre une route existante
                    float total = cout[c] + pas;
                    if (total < cout[n])
                    {
                        cout[n] = total; prec[n] = c;
                        tas.Ajouter(total + (Centre(n) - fin).magnitude * .55f, n);
                    }
                }
            }
            if (prec[arrivee] < 0) return null;
            var chemin = new List<int>();
            for (int c = arrivee; c >= 0; c = prec[c]) chemin.Add(c);
            chemin.Reverse();
            return chemin;
        }

        void Ponts()
        {
            for (int r = 0; r < routes.Count; r++)
            {
                var t = routes[r];
                for (int k = 0; k < t.points.Length - 1; k++)
                {
                    Vector2 a = t.points[k], b = t.points[k + 1];
                    foreach (var riv in rivieres)
                        for (int m = 0; m < riv.points.Length - 1; m++)
                        {
                            if (!Croisement(a, b, riv.points[m], riv.points[m + 1], out var p, out float u)) continue;
                            if (ponts.Exists(o => (o.position - p).magnitude < 40f)) continue;
                            float larg = Mathf.Lerp(riv.largeurs[m], riv.largeurs[m + 1], u);
                            float niv = Mathf.Lerp(riv.niveaux[m], riv.niveaux[m + 1], u);
                            Vector2 d = (b - a).normalized;
                            ponts.Add(new Pont { position = p, angle = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg, longueur = larg + 14f, largeur = t.largeurs[k] + 1.2f, hauteur = niv + 2.6f });
                        }
                }
            }
        }

        static bool Croisement(Vector2 a, Vector2 b, Vector2 c, Vector2 d, out Vector2 p, out float u)
        {
            p = Vector2.zero; u = 0f;
            Vector2 r = b - a, s = d - c;
            float den = r.x * s.y - r.y * s.x;
            if (Math.Abs(den) < 1e-5f) return false;
            float t = ((c.x - a.x) * s.y - (c.y - a.y) * s.x) / den;
            u = ((c.x - a.x) * r.y - (c.y - a.y) * r.x) / den;
            if (t < 0f || t > 1f || u < 0f || u > 1f) return false;
            p = a + r * t;
            return true;
        }

        /// <summary>La hauteur de la chaussée : le relief lissé le long de la route, relevée sur les ponts.</summary>
        void ProfilsDesRoutes()
        {
            foreach (var t in routes)
            {
                int n = t.points.Length;
                var brut = new float[n];
                for (int k = 0; k < n; k++)
                {
                    var p = t.points[k];
                    brut[k] = Math.Max(HauteurSansRoutes(p.x, p.y), .8f);
                    foreach (var pt in ponts)
                        if ((pt.position - p).magnitude < pt.longueur * .5f + 4f) brut[k] = Math.Max(brut[k], pt.hauteur);
                }
                for (int k = 0; k < n; k++)
                {
                    float s = 0f, w = 0f;
                    for (int m = Math.Max(0, k - 5); m <= Math.Min(n - 1, k + 5); m++) { float p = 1f / (1f + Math.Abs(m - k)); s += brut[m] * p; w += p; }
                    t.niveaux[k] = s / w;
                    foreach (var pt in ponts)
                        if ((pt.position - t.points[k]).magnitude < pt.longueur * .5f + 2f) t.niveaux[k] = Math.Max(t.niveaux[k], pt.hauteur);
                }
            }
            foreach (var pt in ponts)
            {
                // Le tablier à la hauteur de la route qui l'emprunte
                if (indexRoutes.Proche(pt.position.x, pt.position.y, 10f, out _, out float niv, out _)) pt.hauteur = Math.Max(pt.hauteur, niv);
            }
        }

        // ---------- les repaires des créatures ----------

        void Repaires()
        {
            // Gelées près des villages, loups dans les bois, gobelins plus loin, trolls dans les montagnes
            var depart = sites.Find(s => s.type == TypeSite.Depart);
            Vector2 d0 = depart != null ? depart.position : Vector2.zero;
            int[] voulus = { 10, 10, 8, 5 };
            float[] distMin = { 180f, 400f, 700f, 1500f };
            for (int e = 0; e < 4; e++)
            {
                int place = 0, essai = 0;
                while (place < voulus[e] && essai++ < 4000)
                {
                    int c = hasard.Next(N * N);
                    var p = Centre(c);
                    float h = hBase[c];
                    if (h < 4f || lacDe[c] >= 0 || distEau[c] < 50f || distMer[c] < 80f || Math.Max(Math.Abs(p.x), Math.Abs(p.y)) > Geo.Demi * .86f) continue;
                    if (e == 3 ? (h < 180f || h > 420f) : h > 230f) continue;
                    if (e < 3 && Pente(c) > .25f) continue;
                    float dd = (p - d0).magnitude;
                    if (dd < distMin[e]) continue;
                    if (e == 0 && dd > 2600f && place < 4) continue;   // les premières gelées pas trop loin du départ
                    bool libre = true;
                    foreach (var s in sites) if ((s.position - p).magnitude < s.rayon + (s.type <= TypeSite.Port ? 450f : 220f)) { libre = false; break; }
                    foreach (var r in repaires) if ((r.position - p).magnitude < 350f) { libre = false; break; }
                    if (!libre || (indexRoutes != null && indexRoutes.Proche(p.x, p.y, 60f, out _, out _, out _))) continue;
                    int niveau = e switch { 0 => 1 + (int)(dd / 1500f), 1 => 3 + (int)(dd / 1400f), 2 => 4 + (int)(dd / 1200f), _ => 10 + (int)(dd / 1500f) };
                    repaires.Add((p, e, niveau));
                    place++;
                }
            }
        }

        // ---------- le relief final ----------

        /// <summary>Le relief avec les lits des rivières creusés et les villes aplanies (sans les routes).</summary>
        public float HauteurSansRoutes(float x, float z) => HauteurSansRoutes(x, z, out _);

        float HauteurSansRoutes(float x, float z, out bool dansLeLit)
        {
            float h = Geo.HauteurBase(x, z);
            dansLeLit = false;
            if (indexRivieres != null && indexRivieres.Proche(x, z, 170f, out float d, out float niveau, out float largeur))
            {
                float w = largeur * .5f, vallee = 25f + largeur * 2.6f;
                float lit = niveau - (1.3f + largeur * .06f);
                if (d < w)
                {
                    float t = d / w;
                    h = Math.Min(h, Mathf.Lerp(lit, niveau - .3f, t * t));
                    dansLeLit = true;
                }
                else if (d < w + vallee)
                {
                    float t = (d - w) / vallee;
                    float berge = niveau + .5f + t * 4f;
                    if (h > berge) h = Mathf.Lerp(h, berge, (1f - Geo.Lisse(0f, 1f, t)) * .85f);
                    if (d < w + 3f) h = Math.Max(h, niveau + .25f);
                    if (d < w + 2f) dansLeLit = true;
                }
            }
            foreach (var s in sites)
            {
                float marge = s.type <= TypeSite.Port ? 80f : 45f;
                float d2 = (new Vector2(x, z) - s.position).magnitude;
                if (d2 > s.rayon + marge) continue;
                h = Mathf.Lerp(h, s.altitude, 1f - Geo.Lisse(s.rayon * .9f, s.rayon + marge, d2));
                if (s.type == TypeSite.Capitale) h += 15f * (1f - Geo.Lisse(35f, 95f, (new Vector2(x, z) - s.fort).magnitude));
            }
            return h;
        }

        public float HauteurFinale(float x, float z)
        {
            float h = HauteurSansRoutes(x, z, out bool lit);
            if (!lit && indexRoutes != null && indexRoutes.Proche(x, z, 14f, out float d, out float niveau, out float largeur))
            {
                float w = largeur * .5f;
                h = Mathf.Lerp(h, niveau, 1f - Geo.Lisse(w, w + 7f, d));
            }
            return h;
        }

        /// <summary>La surface de l'eau ici (mer, lac, rivière), ou −10 000.</summary>
        public float NiveauEau(float x, float z, float hSol)
        {
            float n = -10000f;
            if (hSol < 0f && Geo.Terre(x, z) < .5f) n = 0f;
            int c = Cellule(new Vector2(x, z));
            int i = c % N, j = c / N;
            for (int dj = -1; dj <= 1; dj++)
                for (int di = -1; di <= 1; di++)
                {
                    int ni = i + di, nj = j + dj;
                    if (ni < 0 || nj < 0 || ni >= N || nj >= N) continue;
                    int l = lacDe[nj * N + ni];
                    if (l >= 0 && hSol < lacs[l].niveau) n = Math.Max(n, lacs[l].niveau);
                }
            if (indexRivieres.Proche(x, z, 40f, out float d, out float niv, out float larg) && d < larg * .5f + 1f && hSol < niv) n = Math.Max(n, niv);
            return n;
        }

        /// <summary>Le type de sol, et la densité de forêt (0…1) en ce point.</summary>
        public TypeSol Classer(float x, float z, float h, float pente, out float foret)
        {
            foret = 0f;
            var p = new Vector2(x, z);
            float eau = NiveauEau(x, z, h);
            if (eau > h + .05f) return h < -4f || eau <= .01f ? TypeSol.Sable : TypeSol.LitRiviere;
            if (h < 2.2f && distMer[Cellule(p)] < 260f) return TypeSol.Sable;
            if (indexRivieres.Proche(x, z, 40f, out float dr, out _, out float lr) && dr < lr * .5f + 2.5f) return TypeSol.Sable;

            // Dans les villes : pavés au cœur, chemins dans les villages
            Site proche = null; float dproche = float.MaxValue;
            foreach (var s in sites) { float d = (p - s.position).magnitude; if (d - s.rayon < dproche - (proche?.rayon ?? 0f)) { proche = s; dproche = d; } }
            if (proche != null)
            {
                if (proche.type <= TypeSite.Port && dproche < proche.rayon * .88f) return TypeSol.Pave;
                if (proche.type == TypeSite.Village && dproche < proche.rayon * .38f) return TypeSol.Chemin;
            }
            if (indexRoutes.Proche(x, z, 12f, out float d2, out _, out float l2) && d2 < l2 * .5f + .4f)
                return proche != null && proche.type <= TypeSite.Port && dproche < proche.rayon + 20f ? TypeSol.Pave : TypeSol.Chemin;

            // Roche dans les pentes et en altitude, neige sur les sommets (plus bas au nord)
            float neige = 430f - Math.Max(0f, z) * .02f;
            if (h > neige + (Geo.Fbm(x * .01f, z * .01f, 2, 41) - .5f) * 40f) return TypeSol.Neige;
            if (pente > .78f + (Geo.Fbm(x * .02f, z * .02f, 2, 42) - .5f) * .2f || h > neige - 70f + (Geo.Fbm(x * .008f, z * .008f, 2, 43) - .5f) * 60f) return TypeSol.Roche;

            // Les champs autour des villages et des villes
            if (proche != null && proche.type != TypeSite.Depart && pente < .16f)
            {
                float anneau = proche.type <= TypeSite.Port ? 300f : proche.type == TypeSite.Village ? 200f : 70f;
                if (dproche > proche.rayon && dproche < proche.rayon + anneau && Geo.Fbm(x * .006f, z * .006f, 2, 44) > .42f)
                {
                    float a = proche.angle * Mathf.Deg2Rad;
                    float u = (x - proche.position.x) * Mathf.Cos(a) + (z - proche.position.y) * Mathf.Sin(a);
                    float v = -(x - proche.position.x) * Mathf.Sin(a) + (z - proche.position.y) * Mathf.Cos(a);
                    int bande = (int)Math.Floor(u / 15f) + (int)Math.Floor(v / 70f) * 2;
                    return (TypeSol)((int)TypeSol.Champ0 + ((bande % 3) + 3) % 3);
                }
            }

            // Prairies et forêts
            float humide = Geo.Fbm(x * .0009f + 17f, z * .0009f - 5f, 3, 45);
            float bois = Geo.Fbm(x * .0016f - 3f, z * .0016f + 11f, 4, 46);
            float loinDesVilles = proche == null ? 1f : Geo.Lisse(proche.rayon + 40f, proche.rayon + (proche.type <= TypeSite.Port ? 380f : 160f), dproche);
            foret = Geo.Lisse(.5f - humide * .14f, .6f - humide * .1f, bois) * loinDesVilles * (1f - Geo.Lisse(.45f, .7f, pente)) * (1f - Geo.Lisse(neige - 120f, neige - 60f, h));
            if (foret > .5f) return TypeSol.HerbeSombre;
            float chaud = Geo.Fbm(x * .0007f - 9f, z * .0007f + 2f, 2, 47) + (-z / Geo.Taille) * .4f;
            if (chaud > .62f && humide < .55f) return TypeSol.HerbeDoree;
            if (humide > .6f || z > 1700f + (Geo.Fbm(x * .0012f, z * .0012f, 3, 48) - .5f) * 1400f) return TypeSol.HerbeSombre;
            return TypeSol.Herbe;
        }

        // ---------- outils ----------

        static Trace Lisser(Trace t, int iterations)
        {
            var p = new List<Vector2>(t.points); var l = new List<float>(t.largeurs); var n = new List<float>(t.niveaux);
            for (int it = 0; it < iterations; it++)
            {
                var p2 = new List<Vector2> { p[0] }; var l2 = new List<float> { l[0] }; var n2 = new List<float> { n[0] };
                for (int k = 0; k < p.Count - 1; k++)
                {
                    p2.Add(Vector2.Lerp(p[k], p[k + 1], .25f)); l2.Add(Mathf.Lerp(l[k], l[k + 1], .25f)); n2.Add(Mathf.Lerp(n[k], n[k + 1], .25f));
                    p2.Add(Vector2.Lerp(p[k], p[k + 1], .75f)); l2.Add(Mathf.Lerp(l[k], l[k + 1], .75f)); n2.Add(Mathf.Lerp(n[k], n[k + 1], .75f));
                }
                p2.Add(p[p.Count - 1]); l2.Add(l[l.Count - 1]); n2.Add(n[n.Count - 1]);
                p = p2; l = l2; n = n2;
            }
            return new Trace { nom = t.nom, points = p.ToArray(), largeurs = l.ToArray(), niveaux = n.ToArray() };
        }

        static Trace Reechantillonner(Trace t, float pas)
        {
            var p = new List<Vector2> { t.points[0] }; var l = new List<float> { t.largeurs[0] }; var n = new List<float> { t.niveaux[0] };
            float reste = pas;
            for (int k = 0; k < t.points.Length - 1; k++)
            {
                Vector2 a = t.points[k], b = t.points[k + 1];
                float seg = (b - a).magnitude, pos = 0f;
                while (seg - pos >= reste)
                {
                    pos += reste; reste = pas;
                    float u = pos / seg;
                    p.Add(Vector2.Lerp(a, b, u)); l.Add(Mathf.Lerp(t.largeurs[k], t.largeurs[k + 1], u)); n.Add(Mathf.Lerp(t.niveaux[k], t.niveaux[k + 1], u));
                }
                reste -= seg - pos;
            }
            p.Add(t.points[t.points.Length - 1]); l.Add(t.largeurs[t.largeurs.Length - 1]); n.Add(t.niveaux[t.niveaux.Length - 1]);
            return new Trace { nom = t.nom, points = p.ToArray(), largeurs = l.ToArray(), niveaux = n.ToArray() };
        }
    }

    /// <summary>Index spatial des segments (rivières, routes) : la distance au plus proche, vite.</summary>
    public class IndexTraces
    {
        readonly List<Trace> traces;
        readonly float taille;
        readonly int n;
        readonly List<(int t, int s)>[] cases;

        public IndexTraces(List<Trace> traces, float tailleCase)
        {
            this.traces = traces; taille = tailleCase;
            n = Mathf.CeilToInt(Geo.Taille / taille);
            cases = new List<(int, int)>[n * n];
            for (int t = 0; t < traces.Count; t++)
            {
                var pts = traces[t].points;
                for (int s = 0; s < pts.Length - 1; s++)
                {
                    Vector2 mn = Vector2.Min(pts[s], pts[s + 1]), mx = Vector2.Max(pts[s], pts[s + 1]);
                    float marge = traces[t].largeurs[s] * .5f;
                    int i0 = Case(mn.x - marge), i1 = Case(mx.x + marge), j0 = Case(mn.y - marge), j1 = Case(mx.y + marge);
                    for (int j = j0; j <= j1; j++)
                        for (int i = i0; i <= i1; i++)
                            (cases[j * n + i] ??= new List<(int, int)>()).Add((t, s));
                }
            }
        }

        int Case(float v) => Mathf.Clamp(Mathf.FloorToInt((v + Geo.Demi) / taille), 0, n - 1);

        /// <summary>Le segment le plus proche dans un rayon : distance au bord du tracé (0 sur l'axe), et le niveau et la largeur à cet endroit.</summary>
        public bool Proche(float x, float z, float rayon, out float distance, out float niveau, out float largeur)
        {
            distance = float.MaxValue; niveau = 0f; largeur = 0f;
            int i0 = Case(x - rayon), i1 = Case(x + rayon), j0 = Case(z - rayon), j1 = Case(z + rayon);
            var p = new Vector2(x, z);
            float meilleure = float.MaxValue;
            for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                {
                    var liste = cases[j * n + i];
                    if (liste == null) continue;
                    foreach (var (t, s) in liste)
                    {
                        var tr = traces[t];
                        Vector2 a = tr.points[s], b = tr.points[s + 1], ab = b - a;
                        float u = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                        float d = (p - (a + ab * u)).magnitude;
                        float l = Mathf.Lerp(tr.largeurs[s], tr.largeurs[s + 1], u);
                        float rel = d - l * .5f;   // on compare au bord, pour qu'une grande rivière l'emporte sur un ruisseau voisin
                        if (rel < meilleure) { meilleure = rel; distance = d; largeur = l; niveau = Mathf.Lerp(tr.niveaux[s], tr.niveaux[s + 1], u); }
                    }
                }
            return distance <= rayon;
        }
    }

    /// <summary>Un tas binaire (plus petite priorité d'abord).</summary>
    public class TasMin
    {
        float[] p; int[] v; int n;
        public TasMin(int capacite) { p = new float[capacite + 1]; v = new int[capacite + 1]; }
        public int Nombre => n;
        public void Ajouter(float priorite, int valeur)
        {
            if (n + 1 >= p.Length) { Array.Resize(ref p, p.Length * 2); Array.Resize(ref v, v.Length * 2); }
            int i = ++n;
            p[i] = priorite; v[i] = valeur;
            while (i > 1 && p[i >> 1] > p[i]) { Echanger(i, i >> 1); i >>= 1; }
        }
        public int Retirer()
        {
            int r = v[1];
            p[1] = p[n]; v[1] = v[n]; n--;
            int i = 1;
            while (true)
            {
                int g = i << 1, d = g + 1, m = i;
                if (g <= n && p[g] < p[m]) m = g;
                if (d <= n && p[d] < p[m]) m = d;
                if (m == i) break;
                Echanger(i, m); i = m;
            }
            return r;
        }
        void Echanger(int a, int b) { (p[a], p[b]) = (p[b], p[a]); (v[a], v[b]) = (v[b], v[a]); }
    }
}
