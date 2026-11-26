using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// La carte du monde : peinte au lancement à partir du vrai terrain (relief ombré, courbes de niveau,
    /// rivière, routes, champs, forêts, bâtiments), un brouillard qui se lève là où l'on passe,
    /// les lieux découverts, les repères posés par le joueur, et l'itinéraire vers une destination
    /// (par les routes quand c'est plus court en pratique, comme un GPS).
    /// </summary>
    public class CarteMonde : MonoBehaviour, ISauvegardable
    {
        public static CarteMonde Instance { get; private set; }

        public const int Resolution = 512;
        /// <summary>Le brouillard d'exploration : 64 × 64 cases dans la vallée, 256 × 256 (32 m) dans le grand monde.</summary>
        public static int Grille => Geo.Actif ? 256 : 64;
        public static float Taille => Geo.Actif ? Geo.Taille : Monde.Taille;
        static float RayonVue => Geo.Actif ? 170f : 42f;

        public Texture2D Peinture { get; private set; }
        public Texture2D Brouillard { get; private set; }
        public bool Prete { get; private set; }
        public float Avancement { get; private set; }
        /// <summary>Change à chaque modification des repères, de la destination ou de l'itinéraire.</summary>
        public int Version { get; private set; }

        public readonly List<Vector2> reperes = new List<Vector2>();
        public readonly List<Vector2> route = new List<Vector2>();
        public Vector2? Destination { get; private set; }

        bool[] explore;
        readonly HashSet<string> decouverts = new HashSet<string>();
        bool brouillardSale;
        float minuteur, minuteurRoute;

        public string IdSauvegarde => "carte";

        void Awake()
        {
            Instance = this;
            Registre.InscrireSauvegarde(this);
            explore = new bool[Grille * Grille];
            Brouillard = new Texture2D(Grille, Grille, TextureFormat.RGBA32, false) { name = "Brouillard", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            brouillardSale = true;
            AppliquerBrouillard();
        }

        void OnDestroy()
        {
            Registre.RetirerSauvegarde(this);
            if (Instance == this) Instance = null;
        }

        void Start() => StartCoroutine(Peindre());

        // ---------- repères du monde ----------

        public static Vector2 UV(Vector3 p) => new Vector2((p.x + Taille / 2f) / Taille, (p.z + Taille / 2f) / Taille);
        public static Vector2 UV(Vector2 xz) => new Vector2((xz.x + Taille / 2f) / Taille, (xz.y + Taille / 2f) / Taille);
        public static Vector2 DepuisUV(Vector2 uv) => new Vector2(uv.x * Taille - Taille / 2f, uv.y * Taille - Taille / 2f);

        public bool EstDecouvert(string id) => decouverts.Contains(id);

        public bool Explore(Vector3 p)
        {
            Vector2 uv = UV(p);
            int i = Mathf.FloorToInt(uv.x * Grille), j = Mathf.FloorToInt(uv.y * Grille);
            return i >= 0 && j >= 0 && i < Grille && j < Grille && explore[j * Grille + i];
        }

        // ---------- chaque instant ----------

        void Update()
        {
            if (!MondeRPG.Actif) return;
            var h = CombatHeros.Instance;
            if (h == null) return;
            minuteur -= Time.unscaledDeltaTime;
            if (minuteur <= 0f)
            {
                minuteur = .4f;
                Explorer(h.transform.position);
                DecouvrirLieux();
                if (brouillardSale) AppliquerBrouillard();
            }
            if (Destination.HasValue)
            {
                Vector2 p = new Vector2(h.transform.position.x, h.transform.position.z);
                if (Vector2.Distance(p, Destination.Value) < 8f)
                {
                    HUD.Toast("Tu es arrivé à destination");
                    Sons.Jouer("quete", .5f, 1.2f);
                    EffacerDestination();
                }
                else
                {
                    minuteurRoute -= Time.unscaledDeltaTime;
                    if (minuteurRoute <= 0f) { minuteurRoute = 1f; CalculerRoute(p); }
                }
            }
        }

        void Explorer(Vector3 p)
        {
            Vector2 uv = UV(p);
            float cellule = Taille / Grille;
            int r = Mathf.CeilToInt(RayonVue / cellule);
            int ci = Mathf.FloorToInt(uv.x * Grille), cj = Mathf.FloorToInt(uv.y * Grille);
            for (int j = cj - r; j <= cj + r; j++)
                for (int i = ci - r; i <= ci + r; i++)
                {
                    if (i < 0 || j < 0 || i >= Grille || j >= Grille || explore[j * Grille + i]) continue;
                    Vector2 centre = DepuisUV(new Vector2((i + .5f) / Grille, (j + .5f) / Grille));
                    if (Vector2.Distance(centre, new Vector2(p.x, p.z)) > RayonVue) continue;
                    explore[j * Grille + i] = true;
                    brouillardSale = true;
                }
        }

        void DecouvrirLieux()
        {
            foreach (var l in Lieu.Tous)
            {
                if (l.connu || decouverts.Contains(l.Id) || !Explore(l.Position)) continue;
                decouverts.Add(l.Id);
                HUD.Toast($"Lieu découvert : {l.nom}");
                Sons.Jouer("decouverte", .45f, 1.15f);
                if (StatsHeros.Instance != null) StatsHeros.Instance.GagnerXP(8);
                Version++;
            }
        }

        void AppliquerBrouillard()
        {
            brouillardSale = false;
            var px = new Color32[Grille * Grille];
            for (int k = 0; k < px.Length; k++) px[k] = new Color32(255, 255, 255, explore[k] ? (byte)0 : (byte)255);
            Brouillard.SetPixels32(px);
            Brouillard.Apply();
        }

        // ---------- repères et destination ----------

        public void AjouterRepere(Vector2 xz)
        {
            if (reperes.Count >= 30) reperes.RemoveAt(0);
            reperes.Add(xz);
            Version++;
            Sons.Jouer("clic", .5f, 1.4f);
        }

        public void RetirerRepere(int i)
        {
            if (i < 0 || i >= reperes.Count) return;
            reperes.RemoveAt(i);
            Version++;
            Sons.Jouer("clic", .5f, .8f);
        }

        public void FixerDestination(Vector2 xz)
        {
            Destination = xz;
            var h = CombatHeros.Instance;
            if (h != null) CalculerRoute(new Vector2(h.transform.position.x, h.transform.position.z));
            Sons.Jouer("clic", .6f, 1.1f);
        }

        public void EffacerDestination()
        {
            Destination = null;
            route.Clear();
            Version++;
        }

        // ---------- itinéraire par les routes ----------

        List<Vector2> noeuds;
        List<(int a, int b)> aretes;

        void ConstruireReseau()
        {
            noeuds = new List<Vector2>();
            aretes = new List<(int, int)>();
            int Noeud(Vector2 p)
            {
                for (int k = 0; k < noeuds.Count; k++) if (Vector2.Distance(noeuds[k], p) < 1.5f) return k;
                noeuds.Add(p);
                return noeuds.Count - 1;
            }
            if (Geo.Actif)
            {
                // Les routes du grand monde : un nœud tous les ~70 m, les bouts soudés aux carrefours et aux villes
                var grille = new Dictionary<Vector2Int, List<int>>();
                int NoeudGeo(Vector2 p, float soudure)
                {
                    var c = new Vector2Int(Mathf.FloorToInt(p.x / 20f), Mathf.FloorToInt(p.y / 20f));
                    if (soudure > 0f)
                        for (int dj = -1; dj <= 1; dj++)
                            for (int di = -1; di <= 1; di++)
                                if (grille.TryGetValue(new Vector2Int(c.x + di, c.y + dj), out var l))
                                    foreach (int k in l) if (Vector2.Distance(noeuds[k], p) < soudure) return k;
                    noeuds.Add(p);
                    if (!grille.TryGetValue(c, out var liste)) grille[c] = liste = new List<int>();
                    liste.Add(noeuds.Count - 1);
                    return noeuds.Count - 1;
                }
                foreach (var r in Geo.Routes)
                {
                    var pts = r.points;
                    if (pts == null || pts.Length < 2) continue;
                    int prec = NoeudGeo(pts[0], 14f);
                    for (int k = 8; k < pts.Length; k += 8)
                    {
                        int n = NoeudGeo(pts[k], 12f);
                        if (n != prec) aretes.Add((prec, n));
                        prec = n;
                    }
                    int fin = NoeudGeo(pts[pts.Length - 1], 14f);
                    if (fin != prec) aretes.Add((prec, fin));
                }
                return;
            }
            foreach (var r in Monde.Routes)
                for (int k = 0; k < r.Length - 1; k++) aretes.Add((Noeud(r[k]), Noeud(r[k + 1])));
            // Le pont relie les deux rives
            aretes.Add((Noeud(new Vector2(Monde.PontX - 18f, 0)), Noeud(new Vector2(Monde.PontX + 18f, 0))));
        }

        (int arete, Vector2 point) PlusProche(Vector2 p)
        {
            int best = -1; float bd = float.MaxValue; Vector2 bp = p;
            for (int k = 0; k < aretes.Count; k++)
            {
                Vector2 a = noeuds[aretes[k].a], b = noeuds[aretes[k].b], ab = b - a;
                float t = ab.sqrMagnitude > .001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                Vector2 q = a + ab * t;
                float d = Vector2.Distance(p, q);
                if (d < bd) { bd = d; best = k; bp = q; }
            }
            return (best, bp);
        }

        void CalculerRoute(Vector2 depart)
        {
            route.Clear();
            Version++;
            if (!Destination.HasValue) return;
            Vector2 arrivee = Destination.Value;
            route.Add(depart);
            float direct = Vector2.Distance(depart, arrivee);
            if (noeuds == null) ConstruireReseau();
            var (sa, pa) = PlusProche(depart);
            var (sb, pb) = PlusProche(arrivee);
            if (direct < (Geo.Actif ? 150f : 70f) || sa < 0 || sb < 0 || Vector2.Distance(depart, pa) + Vector2.Distance(arrivee, pb) > direct * .85f)
            {
                route.Add(arrivee);
                return;
            }
            // Dijkstra sur les carrefours, avec deux nœuds de plus : l'entrée et la sortie de la route
            int n = noeuds.Count, S = n, E = n + 1;
            var dist = new float[n + 2];
            var prec = new int[n + 2];
            var fait = new bool[n + 2];
            for (int k = 0; k < n + 2; k++) { dist[k] = float.MaxValue; prec[k] = -1; }
            var voisins = new List<(int, float)>[n + 2];
            for (int k = 0; k < n + 2; k++) voisins[k] = new List<(int, float)>();
            void Lier(int a, int b, float d) { voisins[a].Add((b, d)); voisins[b].Add((a, d)); }
            Vector2 Pos(int k) => k == S ? pa : k == E ? pb : noeuds[k];
            foreach (var (a, b) in aretes) Lier(a, b, Vector2.Distance(noeuds[a], noeuds[b]));
            Lier(S, aretes[sa].a, Vector2.Distance(pa, noeuds[aretes[sa].a]));
            Lier(S, aretes[sa].b, Vector2.Distance(pa, noeuds[aretes[sa].b]));
            Lier(E, aretes[sb].a, Vector2.Distance(pb, noeuds[aretes[sb].a]));
            Lier(E, aretes[sb].b, Vector2.Distance(pb, noeuds[aretes[sb].b]));
            if (sa == sb) Lier(S, E, Vector2.Distance(pa, pb));
            dist[S] = 0f;
            for (int iter = 0; iter < n + 2; iter++)
            {
                int u = -1; float du = float.MaxValue;
                for (int k = 0; k < n + 2; k++) if (!fait[k] && dist[k] < du) { du = dist[k]; u = k; }
                if (u < 0 || u == E) break;
                fait[u] = true;
                foreach (var (v, d) in voisins[u])
                    if (dist[u] + d < dist[v]) { dist[v] = dist[u] + d; prec[v] = u; }
            }
            if (prec[E] < 0 || dist[E] + Vector2.Distance(depart, pa) + Vector2.Distance(arrivee, pb) > direct * 2.4f)
            {
                route.Add(arrivee);
                return;
            }
            var chemin = new List<Vector2>();
            for (int k = E; k >= 0; k = prec[k]) chemin.Add(Pos(k));
            chemin.Reverse();
            route.AddRange(chemin);
            route.Add(arrivee);
        }

        /// <summary>Longueur de l'itinéraire restant, en mètres.</summary>
        public float LongueurRoute()
        {
            float l = 0f;
            for (int k = 1; k < route.Count; k++) l += Vector2.Distance(route[k - 1], route[k]);
            return l;
        }

        // ---------- la peinture ----------

        IEnumerator Peindre()
        {
            // Le grand monde : la carte a été peinte à la génération
            if (Geo.Actif && GrandMonde.Instance != null && GrandMonde.Instance.carte != null)
            {
                Peinture = GrandMonde.Instance.carte;
                Avancement = 1f;
                Prete = true;
                Version++;
                yield break;
            }
            const int N = Resolution;
            float cellule = Taille / N;
            var h = new float[N * N];
            for (int j = 0; j < N; j++)
            {
                float z = -Taille / 2f + (j + .5f) * cellule;
                for (int i = 0; i < N; i++) h[j * N + i] = Monde.Hauteur(-Taille / 2f + (i + .5f) * cellule, z);
                if (j % 24 == 23) { Avancement = .5f * j / N; yield return null; }
            }

            var px = new Color[N * N];
            Color eauProfonde = Pantin.Couleur("#4f7fa6"), eau = Pantin.Couleur("#79a9c9"), sable = Pantin.Couleur("#cdbb8a");
            Color pre = Pantin.Couleur("#a3ba72"), colline = Pantin.Couleur("#7f9656"), roche = Pantin.Couleur("#9c978c"), neige = Pantin.Couleur("#ebe9e3");
            Color teinteRoute = Pantin.Couleur("#e4d4a6"), bordRoute = Pantin.Couleur("#8a7454");
            Color[] champs = { Pantin.Couleur("#cdb86c"), Pantin.Couleur("#b4c16a"), Pantin.Couleur("#d6c386") };
            var lumiere = new Vector3(-1f, 1.4f, 1f).normalized;
            for (int j = 0; j < N; j++)
            {
                float z = -Taille / 2f + (j + .5f) * cellule;
                for (int i = 0; i < N; i++)
                {
                    float x = -Taille / 2f + (i + .5f) * cellule;
                    float y = h[j * N + i];
                    Color c;
                    if (y < -.15f) c = Color.Lerp(eauProfonde, eau, Mathf.Clamp01((y + 3.6f) / 3.4f));
                    else if (Mathf.Abs(x - Monde.Riviere(z)) < Monde.Berge + 1.5f && y < 1.4f) c = sable;
                    else
                    {
                        c = Color.Lerp(pre, colline, Mathf.InverseLerp(2f, 26f, y));
                        c = Color.Lerp(c, roche, Mathf.InverseLerp(24f, 46f, y));
                        c = Color.Lerp(c, neige, Mathf.InverseLerp(72f, 95f, y));
                        if (Monde.DansChamp(x, z)) c = Color.Lerp(c, champs[Monde.BandeChamp(x, z)], .85f);
                    }
                    // Relief ombré (lumière du nord-ouest) et courbes de niveau tous les 5 m
                    int i0 = Mathf.Max(0, i - 1), i1 = Mathf.Min(N - 1, i + 1), j0 = Mathf.Max(0, j - 1), j1 = Mathf.Min(N - 1, j + 1);
                    float dx = (h[j * N + i1] - h[j * N + i0]) / ((i1 - i0) * cellule), dz = (h[j1 * N + i] - h[j0 * N + i]) / ((j1 - j0) * cellule);
                    var n = new Vector3(-dx, 1f, -dz).normalized;
                    float ombre = Mathf.Clamp01(Vector3.Dot(n, lumiere));
                    c *= Mathf.Lerp(.68f, 1.12f, ombre);
                    if (y > 0f && (Mathf.Floor(y / 5f) != Mathf.Floor(h[j * N + i1] / 5f) || Mathf.Floor(y / 5f) != Mathf.Floor(h[j1 * N + i] / 5f))) c *= .86f;
                    float dr = Monde.DistanceRoute(x, z);
                    if (dr < 2.3f) c = teinteRoute;
                    else if (dr < 3.3f) c = Color.Lerp(c, bordRoute, .55f);
                    c *= .97f + Monde.HashPublic(i * .37f, j * .53f) * .06f;   // grain du papier
                    c.a = 1f;
                    px[j * N + i] = c;
                }
                if (j % 32 == 31) { Avancement = .5f + .35f * j / N; yield return null; }
            }

            PeindreForet(px, N, cellule);
            yield return null;
            PeindreBatiments(px, N, cellule);
            Avancement = .95f;
            yield return null;

            // Bord de parchemin
            for (int j = 0; j < N; j++)
                for (int i = 0; i < N; i++)
                {
                    float bx = Mathf.Min(i, N - 1 - i) / (float)N, by = Mathf.Min(j, N - 1 - j) / (float)N;
                    float b = Mathf.Clamp01(Mathf.Min(bx, by) / .05f);
                    px[j * N + i] = Color.Lerp(px[j * N + i] * .55f, px[j * N + i], b);
                    px[j * N + i].a = 1f;
                }

            Peinture = new Texture2D(N, N, TextureFormat.RGBA32, true) { name = "CarteDuMonde", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            Peinture.SetPixels(px);
            Peinture.Apply(true);
            Avancement = 1f;
            Prete = true;
            Version++;
        }

        void PeindreForet(Color[] px, int N, float cellule)
        {
            var foret = GameObject.Find("Foret");
            if (foret == null) return;
            foreach (Transform t in foret.transform)
            {
                string n = t.name;
                Color c; float r;
                if (n.StartsWith("Sapin")) { c = Pantin.Couleur(n.Contains("Clair") ? "#4c6e3e" : "#3b5732"); r = 2.6f; }
                else if (n.StartsWith("ArbreJaune")) { c = Pantin.Couleur("#d3a845"); r = 2.6f; }
                else if (n.StartsWith("ArbreOrange")) { c = Pantin.Couleur("#c98c3a"); r = 2.6f; }
                else if (n.StartsWith("ArbreRoux")) { c = Pantin.Couleur("#b5743a"); r = 2.6f; }
                else if (n.StartsWith("Rocher")) { c = Pantin.Couleur("#8a8794"); r = 1.3f; }
                else { c = Pantin.Couleur("#5c7c3a"); r = 1.2f; }
                r *= Mathf.Max(.6f, t.localScale.x) / cellule;
                Vector2 uv = UV(t.position);
                Tampon(px, N, uv.x * N, uv.y * N, r, c * .92f, .85f);
            }
        }

        static void Tampon(Color[] px, int N, float cx, float cy, float r, Color c, float force)
        {
            int x0 = Mathf.FloorToInt(cx - r - 1), x1 = Mathf.CeilToInt(cx + r + 1), y0 = Mathf.FloorToInt(cy - r - 1), y1 = Mathf.CeilToInt(cy + r + 1);
            for (int y = Mathf.Max(0, y0); y <= Mathf.Min(N - 1, y1); y++)
                for (int x = Mathf.Max(0, x0); x <= Mathf.Min(N - 1, x1); x++)
                {
                    float d = new Vector2(x + .5f - cx, y + .5f - cy).magnitude;
                    float a = Mathf.Clamp01(r - d + .5f) * force;
                    if (a <= 0f) continue;
                    // Ombre portée vers le sud-est, puis la couronne
                    px[y * N + x] = Color.Lerp(px[y * N + x], c * (d > r * .55f ? .85f : 1.05f), a);
                }
        }

        static readonly string[] NomsBatiments = { "Maison", "Auberge", "Chapelle", "Moulin", "Forge", "Lavoir", "FourAChaux", "Tente" };

        void PeindreBatiments(Color[] px, int N, float cellule)
        {
            Color toit = Pantin.Couleur("#86644f"), bord = Pantin.Couleur("#3e3029");
            foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                bool batiment = false;
                foreach (var nom in NomsBatiments) if (t.name.StartsWith(nom)) { batiment = true; break; }
                if (!batiment || (t.parent != null && t.parent.name.StartsWith("Maison"))) continue;
                // Emprise du bâtiment dans son propre repère (pour le dessiner tourné)
                Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
                int pieces = 0;
                foreach (var mf in t.GetComponentsInChildren<MeshFilter>())
                {
                    if (mf.sharedMesh == null) continue;
                    var b = mf.sharedMesh.bounds;
                    for (int k = 0; k < 8; k++)
                    {
                        var coin = new Vector3((k & 1) == 0 ? b.min.x : b.max.x, (k & 2) == 0 ? b.min.y : b.max.y, (k & 4) == 0 ? b.min.z : b.max.z);
                        Vector3 l = t.InverseTransformPoint(mf.transform.TransformPoint(coin));
                        min = Vector2.Min(min, new Vector2(l.x, l.z));
                        max = Vector2.Max(max, new Vector2(l.x, l.z));
                    }
                    pieces++;
                }
                if (pieces == 0) continue;
                Vector2 taille = max - min;
                if (taille.x < 1f || taille.y < 1f || taille.x > 26f || taille.y > 26f) continue;
                float rayon = taille.magnitude / 2f + 1f;
                Vector2 uv = UV(t.position);
                int r = Mathf.CeilToInt(rayon / cellule);
                int ci = Mathf.FloorToInt(uv.x * N), cj = Mathf.FloorToInt(uv.y * N);
                float marge = cellule * 1.1f;
                for (int j = cj - r; j <= cj + r; j++)
                    for (int i = ci - r; i <= ci + r; i++)
                    {
                        if (i < 0 || j < 0 || i >= N || j >= N) continue;
                        Vector2 w = DepuisUV(new Vector2((i + .5f) / N, (j + .5f) / N));
                        Vector3 l = t.InverseTransformPoint(new Vector3(w.x, t.position.y, w.y));
                        if (l.x < min.x || l.x > max.x || l.z < min.y || l.z > max.y) continue;
                        bool contour = l.x < min.x + marge || l.x > max.x - marge || l.z < min.y + marge || l.z > max.y - marge;
                        px[j * N + i] = contour ? bord : toit;
                    }
            }
        }

        // ---------- sauvegarde ----------

        [System.Serializable]
        class Etat
        {
            public string explore;
            public List<string> decouverts = new List<string>();
            public List<Vector2> reperes = new List<Vector2>();
            public bool aDestination;
            public Vector2 destination;
        }

        public string Ecrire()
        {
            var octets = new byte[(explore.Length + 7) / 8];
            for (int k = 0; k < explore.Length; k++) if (explore[k]) octets[k / 8] |= (byte)(1 << (k % 8));
            return JsonUtility.ToJson(new Etat
            {
                explore = System.Convert.ToBase64String(octets),
                decouverts = new List<string>(decouverts),
                reperes = new List<Vector2>(reperes),
                aDestination = Destination.HasValue,
                destination = Destination ?? Vector2.zero,
            });
        }

        public void Lire(string json)
        {
            var e = JsonUtility.FromJson<Etat>(json);
            if (e == null) return;
            if (!string.IsNullOrEmpty(e.explore))
            {
                var octets = System.Convert.FromBase64String(e.explore);
                for (int k = 0; k < explore.Length; k++) explore[k] = k / 8 < octets.Length && (octets[k / 8] & (1 << (k % 8))) != 0;
            }
            decouverts.Clear();
            if (e.decouverts != null) foreach (var d in e.decouverts) decouverts.Add(d);
            reperes.Clear();
            if (e.reperes != null) reperes.AddRange(e.reperes);
            Destination = e.aDestination ? e.destination : (Vector2?)null;
            route.Clear();
            brouillardSale = true;
            AppliquerBrouillard();
            Version++;
        }
    }
}
