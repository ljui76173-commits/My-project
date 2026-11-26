using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aldermark.Editeur
{
    /// <summary>
    /// Le grand monde (menu « Aldermark > Nouveau monde (RPG) ») : 8 km × 8 km en 64 tuiles de terrain,
    /// deux royaumes séparés par une crête et un col, la mer au sud-ouest, des rivières qui descendent
    /// vraiment des montagnes, des lacs, des forêts, des routes entre les villes, des ponts.
    /// Tout vient de <see cref="PlanDuMonde"/> (calculé ici, sans hasard d'une fois sur l'autre : même graine, même monde).
    /// Les données dont le jeu a besoin (sol et eau tous les 4 m, carte peinte) sont cuites dans Assets/Aldermark/Genere/Monde.
    /// </summary>
    public static partial class GenerateurVallee
    {
        /// <summary>Le plan du monde pendant la génération (lu par GameplayPont.AjouterRPG pour les repaires et les villes).</summary>
        public static PlanDuMonde Plan { get; private set; }

        const int G = GrillesMonde.G;            // la grille de travail : une case tous les 2 m
        const int ResTuile = 512;                // cases par tuile (heightmap 513, splat 512)

        public static void GenererGrandMonde()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            coucheSceau = LayerMask.NameToLayer("Sceau");
            if (coucheSceau < 0)
            {
                EditorUtility.DisplayDialog("Aldermark", "Le calque « Sceau » est introuvable. Ajoute-le dans Project Settings > Tags and Layers (calque 8).", "OK");
                return;
            }
            materiaux.Clear();
            foreach (var d in new[] { "", "/Materiaux", "/Terrain", "/Arbres", "/Formes", "/Monde" }) Directory.CreateDirectory(Dossier + d);
            AssetDatabase.Refresh();
            var chrono = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                Progres("Plan du monde : relief, rivières, villes, routes", 0f);
                var plan = new PlanDuMonde(7);
                plan.Calculer((e, f) => Progres("Plan du monde : " + e, f * .08f));
                Plan = plan;
                Geo.Desactiver();
                Geo.Actif = true;
                Geo.Echantillon = plan.HauteurFinale;
                Geo.Sites.AddRange(plan.sites);
                Geo.Routes.AddRange(plan.routes);
                Geo.Rivieres.AddRange(plan.rivieres);

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var soleils = Lumieres();
                RenderSettings.fogStartDistance = 320f;
                RenderSettings.fogEndDistance = 5200f;

                var g = GrillesMonde.Calculer(plan, (e, f) => Progres(e, .08f + .24f * f));
                Geo.Sols = (x, z) => (TypeSol)g.sol[GrillesMonde.Indice(x, z)];
                Geo.Eaux = (x, z) => g.eau[GrillesMonde.Indice(x, z)];

                Progres("Villes, villages et hameaux : les plans", .31f);
                var villes = PlansDesVilles(plan);
                var terrains = TerrainsDuGrandMonde(g);
                ArbresDuGrandMonde(g, terrains, villes);
                Progres("Mer, lacs et rivières", .72f);
                EauxDuGrandMonde(plan);
                PontsDuGrandMonde(plan);
                Progres("Carte du monde et données du jeu", .78f);
                DonneesCuites(plan, g, terrains, villes);
                Progres("Villes : murs, lieux de la carte", .88f);
                VillesDuGrandMonde(villes);

                Progres("Aldric", .9f);
                var joueur = Joueur(out var cam, out var rendu);
                cam.nearClipPlane = .08f;
                cam.farClipPlane = 9000f;
                Systemes(soleils, joueur, cam, rendu);
                var oiseaux = Object.FindFirstObjectByType<Oiseaux>();
                if (oiseaux != null) { var hm = Monde.Hameau; oiseaux.centre = new Vector3(hm.x + 30f, Monde.Hauteur(hm.x, hm.y) + 45f, hm.y + 20f); }
                ReglagesURP();

                const string chemin = "Assets/Scenes/Aldermark.unity";
                EditorSceneManager.SaveScene(scene, chemin);
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(chemin, true) };
                AssetDatabase.SaveAssets();
                Progres("Le hameau, les villes, les repaires", .95f);
                GameplayPont.AjouterRPG();
                Debug.Log($"Aldermark : grand monde généré en {chrono.Elapsed.TotalSeconds:0} s ({plan.sites.Count} villes et villages, {plan.routes.Count} routes, " +
                          $"{plan.rivieres.Count} rivières, {plan.lacs.Count} lacs, {plan.repaires.Count} repaires). Appuie sur Play.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Plan = null;
                Geo.Desactiver();
            }
        }

        // ---------- les 64 tuiles de terrain ----------

        /// <summary>Une couche de terrain par type de sol, dans l'ordre de <see cref="TypeSol"/>.</summary>
        static TerrainLayer[] CouchesDuGrandMonde() => new[]
        {
            Couche("Herbe", "#6f9a3a", 'h'), Couche("HerbeDoree", "#c9a43e", 'h'), Couche("HerbeSombre", "#4f7a34", 'h'),
            Couche("Ble", "#d8b048", 'h'), Couche("Culture", "#86a83e", 'h'), Couche("Labour", "#8a6a40", 't'),
            Couche("Chemin", "#9a7a48", 't', 2.5f), Couche("Roche", "#6f7488", 'r', 6f), Couche("Sable", "#b8a878", 't'), Couche("LitRiviere", "#6e6650", 't'),
            Couche("Neige", "#e8eaf4", 'p', 6f), Couche("Pave", "#948e88", 'c', 2.5f),
        };

        static UnityEngine.Terrain[] TerrainsDuGrandMonde(GrillesMonde g)
        {
            var couches = CouchesDuGrandMonde();
            int nc = couches.Length, N1 = G + 1, T = Geo.Tuiles;
            var materiau = Sauver(new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit")), "Terrain/MateriauGrandMonde.mat");
            var racine = Racine("Terrain");
            var terrains = new UnityEngine.Terrain[T * T];
            var poids = new float[ResTuile * ResTuile * nc];
            for (int tj = 0; tj < T; tj++)
                for (int ti = 0; ti < T; ti++)
                {
                    Progres($"Terrain : tuile {tj * T + ti + 1} / {T * T}", .32f + .26f * (tj * T + ti) / (T * T));
                    var td = new TerrainData();
                    td.heightmapResolution = ResTuile + 1;
                    td.size = new Vector3(Geo.TailleTuile, Geo.HauteurMax, Geo.TailleTuile);
                    td.alphamapResolution = ResTuile;
                    Sauver(td, $"Terrain/Monde_{ti}_{tj}.asset");   // l'asset d'abord : les cartes de couleurs y sont rangées
                    var hs = new float[ResTuile + 1, ResTuile + 1];
                    for (int b = 0; b <= ResTuile; b++)
                        for (int a = 0; a <= ResTuile; a++)
                            hs[b, a] = Mathf.Clamp01((g.h[(tj * ResTuile + b) * N1 + ti * ResTuile + a] - Geo.YMin) / Geo.HauteurMax);
                    td.SetHeights(0, 0, hs);

                    g.PoidsTuile(ti, tj, ResTuile, nc, poids);
                    // Seulement les couches présentes dans la tuile (moins de passes de rendu)
                    var utilisees = new List<int>();
                    for (int k = 0; k < nc; k++)
                    {
                        float m = 0f;
                        for (int p = k; p < poids.Length && m <= .02f; p += nc) m = Mathf.Max(m, poids[p]);
                        if (m > .02f) utilisees.Add(k);
                    }
                    if (utilisees.Count == 0) utilisees.Add(0);
                    var cl = new TerrainLayer[utilisees.Count];
                    for (int u = 0; u < cl.Length; u++) cl[u] = couches[utilisees[u]];
                    td.terrainLayers = cl;
                    var carte = new float[ResTuile, ResTuile, cl.Length];
                    for (int b = 0; b < ResTuile; b++)
                        for (int a = 0; a < ResTuile; a++)
                        {
                            int o = (b * ResTuile + a) * nc;
                            float somme = 0f;
                            for (int u = 0; u < cl.Length; u++) somme += poids[o + utilisees[u]];
                            for (int u = 0; u < cl.Length; u++) carte[b, a, u] = somme > 0f ? poids[o + utilisees[u]] / somme : (u == 0 ? 1f : 0f);
                        }
                    td.SetAlphamaps(0, 0, carte);
                    EditorUtility.SetDirty(td);

                    var go = UnityEngine.Terrain.CreateTerrainGameObject(td);
                    go.name = $"Terrain_{ti}_{tj}";
                    go.transform.SetParent(racine, false);
                    go.transform.position = new Vector3(-Geo.Demi + ti * Geo.TailleTuile, Geo.YMin, -Geo.Demi + tj * Geo.TailleTuile);
                    var t = go.GetComponent<UnityEngine.Terrain>();
                    t.materialTemplate = materiau;
                    t.drawInstanced = true;
                    t.heightmapPixelError = 4f;
                    t.basemapDistance = 1500f;
                    t.treeDistance = 1100f;
                    t.treeBillboardDistance = 2000f;
                    t.treeMaximumFullLODCount = 100000;
                    t.detailObjectDistance = 0f;
                    t.groupingID = 8;
                    t.allowAutoConnect = true;
                    t.shadowCastingMode = ShadowCastingMode.On;
                    terrains[tj * T + ti] = t;
                }
            for (int tj = 0; tj < T; tj++)
                for (int ti = 0; ti < T; ti++)
                {
                    UnityEngine.Terrain Tuile(int i, int j) => i < 0 || j < 0 || i >= T || j >= T ? null : terrains[j * T + i];
                    terrains[tj * T + ti].SetNeighbors(Tuile(ti - 1, tj), Tuile(ti, tj + 1), Tuile(ti + 1, tj), Tuile(ti, tj - 1));
                }
            return terrains;
        }

        // ---------- forêts, bosquets, rochers ----------

        static void ArbresDuGrandMonde(GrillesMonde g, UnityEngine.Terrain[] terrains, List<(Site site, PlanVille plan)> villes)
        {
            Progres("Forêts, bosquets et rochers", .58f);
            var protos = ProtosArbres();
            var prototypes = new TreePrototype[protos.Length];
            for (int k = 0; k < protos.Length; k++) prototypes[k] = new TreePrototype { prefab = protos[k] };
            int T = Geo.Tuiles;
            var listes = new List<TreeInstance>[T * T];
            for (int k = 0; k < listes.Length; k++) listes[k] = new List<TreeInstance>();
            // Pas d'arbre sur une ferme, une maison de faubourg, un rempart
            var grille = new Dictionary<long, List<Emprise>>();
            long Cle(int i, int j) => ((long)i << 32) ^ (uint)j;
            foreach (var (_, pv) in villes)
                foreach (var e in pv.emprises)
                {
                    float r = e.hx + e.hz + 3f;
                    for (int j = Mathf.FloorToInt((e.c.y - r) / 32f); j <= Mathf.FloorToInt((e.c.y + r) / 32f); j++)
                        for (int i = Mathf.FloorToInt((e.c.x - r) / 32f); i <= Mathf.FloorToInt((e.c.x + r) / 32f); i++)
                        {
                            if (!grille.TryGetValue(Cle(i, j), out var l)) grille[Cle(i, j)] = l = new List<Emprise>();
                            l.Add(e);
                        }
                }
            bool Libre(float x, float z)
            {
                if (!grille.TryGetValue(Cle(Mathf.FloorToInt(x / 32f), Mathf.FloorToInt(z / 32f)), out var l)) return true;
                foreach (var e in l) if (e.Contient(new Vector2(x, z), 2.5f)) return false;
                return true;
            }
            var plantes = g.Plantes(Libre, f => Progres("Forêts, bosquets et rochers", .58f + .1f * f));
            int arbres = 0;
            foreach (var p in plantes)
            {
                int ti = Mathf.Clamp(Mathf.FloorToInt((p.x + Geo.Demi) / Geo.TailleTuile), 0, T - 1), tj = Mathf.Clamp(Mathf.FloorToInt((p.z + Geo.Demi) / Geo.TailleTuile), 0, T - 1);
                float u = (p.x + Geo.Demi - ti * Geo.TailleTuile) / Geo.TailleTuile, v = (p.z + Geo.Demi - tj * Geo.TailleTuile) / Geo.TailleTuile;
                listes[tj * T + ti].Add(new TreeInstance
                {
                    position = new Vector3(Mathf.Clamp01(u), 0f, Mathf.Clamp01(v)),
                    widthScale = p.largeur, heightScale = p.hauteur, rotation = p.rotation,
                    color = Color.white, lightmapColor = Color.white, prototypeIndex = p.proto,
                });
                if (p.proto <= 4) arbres++;
            }
            for (int k = 0; k < terrains.Length; k++)
            {
                var td = terrains[k].terrainData;
                td.treePrototypes = prototypes;
                td.SetTreeInstances(listes[k].ToArray(), true);
                EditorUtility.SetDirty(td);
            }
            Debug.Log($"Aldermark : {arbres} arbres, {plantes.Count - arbres} buissons et rochers.");
        }

        // ---------- les villes ----------

        static int GraineStable(string s)
        {
            unchecked { int h = 17; foreach (char c in s) h = h * 31 + c; return h & 0x7fffffff; }
        }

        /// <summary>Le plan de chaque ville, port, village, hameau ; plus les maisons des voisins du hameau de départ.</summary>
        static List<(Site site, PlanVille plan)> PlansDesVilles(PlanDuMonde plan)
        {
            var liste = new List<(Site, PlanVille)>();
            foreach (var s in plan.sites)
                if (s.type != TypeSite.Depart) liste.Add((s, PlanVille.Tracer(s, GraineStable(s.nom))));
            return liste;
        }

        /// <summary>Les maisons des voisins du hameau de départ (le hameau lui-même est posé par GameplayPont).</summary>
        public static readonly Vector2[] VoisinsDuHameau = { new Vector2(11f, -8f), new Vector2(12f, 8f), new Vector2(-9f, 12f), new Vector2(3f, -17f) };

        static VilleCuite HameauDeDepart()
        {
            var depart = Geo.SitePlusProche(Monde.Hameau, s => s.type == TypeSite.Depart);
            var H = Monde.Hameau;
            var v = new VilleCuite { nom = depart != null ? depart.nom : "Hameau", centre = new Vector3(H.x, Monde.Hauteur(H.x, H.y), H.y) };
            for (int i = 0; i < VoisinsDuHameau.Length; i++)
            {
                var p = H + VoisinsDuHameau[i];
                var t = new Tirage(4100 + i);
                var o = new OptionsMaison { largeur = t.Entre(6.2f, 7.5f), profondeur = t.Entre(5.2f, 6f), etages = 1 + i % 2, style = i % 2 == 0 ? StyleMur.Colombage : StyleMur.Pierre, toiture = i == 2 ? Toiture.Chaume : Toiture.Tuiles, graine = 4100 + i };
                KitBatiment.Varier(o, t, false);
                var d = (H - p).normalized;
                float y = Mathf.Min(Monde.Hauteur(p.x, p.y), Mathf.Min(Monde.Hauteur(p.x + 3f, p.y + 3f), Monde.Hauteur(p.x - 3f, p.y - 3f)));
                v.commandes.Add(new Commande { type = Gabarit.Maison, position = new Vector3(p.x, y, p.y), lacet = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg, maison = o, graine = 4100 + i });
            }
            return v;
        }

        /// <summary>
        /// Les villes dans la scène : leurs plans (JSON, bâtis au lancement par <see cref="VillesDuMonde"/>), leurs murs
        /// (collisions), et leurs lieux sur la carte (la ville, ses auberges, forges, églises, halles, moulins).
        /// </summary>
        static void VillesDuGrandMonde(List<(Site site, PlanVille plan)> villes)
        {
            var donnees = new DonneesVilles();
            var racine = Racine("Villes");
            var depart = HameauDeDepart();
            donnees.villes.Add(depart);
            var voisin = Geo.SitePlusProche(Monde.Hameau, s => s.type == TypeSite.Village);
            var scratch = new Maillage();
            var volumes = new List<Volume>();

            void Murs(Transform parent, List<Commande> commandes)
            {
                var murs = new GameObject("Murs").transform;
                murs.SetParent(parent, false);
                foreach (var k in commandes)
                {
                    scratch.Vider();
                    volumes.Clear();
                    try { KitBatiment.Construire(scratch, k, volumes); }
                    catch (System.Exception e) { Debug.LogWarning($"Aldermark : {k.type} en {k.position} ignoré ({e.Message})."); continue; }
                    foreach (var vo in volumes)
                    {
                        var go = new GameObject("Mur");
                        go.transform.SetParent(murs, false);
                        go.transform.SetPositionAndRotation(vo.centre, Quaternion.Euler(0f, vo.lacet, 0f));
                        go.AddComponent<BoxCollider>().size = vo.taille;
                    }
                }
            }

            var hameau = new GameObject(depart.nom).transform;
            hameau.SetParent(racine, false);
            hameau.position = depart.centre;
            Murs(hameau, depart.commandes);

            int numero = 0;
            foreach (var (s, pv) in villes)
            {
                var v = new VilleCuite { nom = s.nom, centre = new Vector3(s.position.x, s.altitude, s.position.y), commandes = pv.commandes };
                donnees.villes.Add(v);
                var go = new GameObject(s.nom).transform;
                go.SetParent(racine, false);
                go.position = new Vector3(s.position.x, Monde.Hauteur(s.position.x, s.position.y), s.position.y);
                string id = Identifiant(s.nom);
                go.gameObject.AddComponent<Entite>().id = "ville-" + id;
                var lieu = go.gameObject.AddComponent<Lieu>();
                lieu.nom = s.nom;
                lieu.type = s.type == TypeSite.Capitale ? TypeLieu.Capitale : s.type == TypeSite.Ville ? TypeLieu.Ville :
                            s.type == TypeSite.Port ? TypeLieu.Port : s.type == TypeSite.Village ? TypeLieu.Village : TypeLieu.Hameau;
                lieu.connu = s == voisin || (s.type == TypeSite.Capitale && s.royaume == 0);
                Murs(go, pv.commandes);
                foreach (var k in pv.commandes)
                {
                    if (!k.lieu) continue;
                    TypeLieu t = k.type == Gabarit.Auberge ? TypeLieu.Auberge : k.type == Gabarit.Forge ? TypeLieu.Forge : k.type == Gabarit.Halle ? TypeLieu.Marche :
                                 k.type == Gabarit.MoulinAVent ? TypeLieu.Moulin : TypeLieu.Chapelle;
                    var l = new GameObject(k.nom);
                    l.transform.SetParent(go, false);
                    l.transform.position = k.position;
                    l.AddComponent<Entite>().id = $"lieu-{id}-{k.type.ToString().ToLower()}-{++numero}";
                    var li = l.AddComponent<Lieu>();
                    li.nom = k.type == Gabarit.Halle || k.type == Gabarit.MoulinAVent ? k.nom : $"{k.nom} ({s.nom})";
                    li.type = t;
                }
            }

            string chemin = Dossier + "/Monde/Villes.json";
            File.WriteAllText(chemin, JsonUtility.ToJson(donnees));
            AssetDatabase.ImportAsset(chemin);
            var vd = racine.gameObject.AddComponent<VillesDuMonde>();
            vd.donnees = AssetDatabase.LoadAssetAtPath<TextAsset>(chemin);
            var shader = Shader.Find("Aldermark/Bati");
            if (shader == null)
            {
                Debug.LogError("Aldermark : le shader Aldermark/Bati est introuvable (Assets/Aldermark/Shaders/Bati.shader). Les maisons seront blanches.");
                shader = Shader.Find("Universal Render Pipeline/Particles/Simple Lit");
            }
            vd.materiau = Sauver(new Material(shader) { enableInstancing = false }, "Materiaux/Bati.mat");
            int n = 0; foreach (var v in donnees.villes) n += v.commandes.Count;
            Debug.Log($"Aldermark : {donnees.villes.Count} villes et villages, {n} bâtiments (bâtis au lancement).");
        }

        /// <summary>Les toits des villes sur la carte : chaque bâtiment, vu d'en haut, avec son ombre et son contour.</summary>
        static void PeindreBatiments(Color32[] px, List<(Site site, PlanVille plan)> villes)
        {
            Color32[] toits = { new Color32(168, 92, 66, 255), new Color32(150, 84, 62, 255), new Color32(110, 116, 136, 255), new Color32(176, 150, 96, 255) };
            var contour = new Color32(70, 52, 44, 255);
            foreach (var (_, pv) in villes)
                foreach (var e in pv.emprises)
                {
                    float r = e.hx + e.hz;
                    int i0 = Mathf.FloorToInt((e.c.x - r + Geo.Demi) / GrillesMonde.Pas), i1 = Mathf.CeilToInt((e.c.x + r + Geo.Demi) / GrillesMonde.Pas);
                    int j0 = Mathf.FloorToInt((e.c.y - r + Geo.Demi) / GrillesMonde.Pas), j1 = Mathf.CeilToInt((e.c.y + r + Geo.Demi) / GrillesMonde.Pas);
                    var toit = toits[(int)(Geo.Hasard(e.c.x, e.c.y, 5) * toits.Length) % toits.Length];
                    bool mur = e.hx < 2f || e.hz < 2f;
                    for (int j = Mathf.Max(0, j0); j <= Mathf.Min(G - 1, j1); j++)
                        for (int i = Mathf.Max(0, i0); i <= Mathf.Min(G - 1, i1); i++)
                        {
                            var p = new Vector2(-Geo.Demi + (i + .5f) * GrillesMonde.Pas, -Geo.Demi + (j + .5f) * GrillesMonde.Pas);
                            if (!e.Contient(p, 0f)) continue;
                            px[j * G + i] = mur ? new Color32(150, 144, 132, 255) : e.Contient(p, -1.6f) ? toit : contour;
                        }
                }
        }

        static string Identifiant(string nom)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in nom.Normalize(System.Text.NormalizationForm.FormD))
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
                sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-');
            }
            return sb.ToString();
        }

        // ---------- la mer, les lacs, les rivières ----------

        static void EauxDuGrandMonde(PlanDuMonde plan)
        {
            var racine = Racine("Eaux");
            Material douce = Lit("#7fa9d4", .9f), mer = Lit("#5d8dc2", .9f);

            // La mer : un grand plan à 0, qui file jusqu'à l'horizon
            var m = new Mesh { name = "Mer" };
            const float L = 30000f;
            m.vertices = new[] { new Vector3(-L, 0, -L), new Vector3(-L, 0, L), new Vector3(L, 0, -L), new Vector3(L, 0, L) };
            m.uv = new[] { Vector2.zero, Vector2.up, Vector2.right, Vector2.one };
            m.triangles = new[] { 0, 1, 2, 2, 1, 3 };
            m.RecalculateNormals();
            m.RecalculateBounds();
            Surface(racine, "Mer", Sauver(m, "Monde/Mer.asset"), mer);

            // Les lacs : leurs cases de 32 m, débordant d'une case sous les berges
            for (int l = 0; l < plan.lacs.Count; l++)
            {
                var lac = plan.lacs[l];
                var cases = new HashSet<int>();
                foreach (int c in lac.cellules)
                {
                    int ci = c % PlanDuMonde.N, cj = c / PlanDuMonde.N;
                    for (int dj = -1; dj <= 1; dj++)
                        for (int di = -1; di <= 1; di++)
                        {
                            int ni = ci + di, nj = cj + dj;
                            if (ni >= 0 && nj >= 0 && ni < PlanDuMonde.N && nj < PlanDuMonde.N) cases.Add(nj * PlanDuMonde.N + ni);
                        }
                }
                var v = new List<Vector3>(); var t = new List<int>();
                foreach (int c in cases)
                {
                    Vector2 centre = PlanDuMonde.Centre(c);
                    float d = PlanDuMonde.Pas * .5f;
                    int o = v.Count;
                    v.Add(new Vector3(centre.x - d, lac.niveau, centre.y - d)); v.Add(new Vector3(centre.x - d, lac.niveau, centre.y + d));
                    v.Add(new Vector3(centre.x + d, lac.niveau, centre.y - d)); v.Add(new Vector3(centre.x + d, lac.niveau, centre.y + d));
                    t.Add(o); t.Add(o + 1); t.Add(o + 2); t.Add(o + 2); t.Add(o + 1); t.Add(o + 3);
                }
                var ml = new Mesh { name = "Lac" + l, indexFormat = v.Count > 60000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                ml.SetVertices(v); ml.SetTriangles(t, 0);
                ml.RecalculateNormals(); ml.RecalculateBounds();
                Surface(racine, "Lac" + l, Sauver(ml, $"Monde/Lac{l}.asset"), douce);
            }

            // Les rivières : un ruban qui suit leur cours et leur niveau, un peu plus large que le lit
            for (int r = 0; r < plan.rivieres.Count; r++)
            {
                var tr = plan.rivieres[r];
                int n = tr.points.Length;
                if (n < 2) continue;
                var v = new Vector3[n * 2]; var uv = new Vector2[n * 2]; var t = new int[(n - 1) * 6];
                float s = 0f;
                for (int k = 0; k < n; k++)
                {
                    Vector2 p = tr.points[k], dir = (tr.points[Mathf.Min(k + 1, n - 1)] - tr.points[Mathf.Max(k - 1, 0)]).normalized;
                    var gauche = new Vector2(-dir.y, dir.x);
                    float w = tr.largeurs[k] * .5f + 2.5f;
                    if (k > 0) s += (p - tr.points[k - 1]).magnitude;
                    Vector2 a = p - gauche * w, b = p + gauche * w;
                    v[k * 2] = new Vector3(a.x, tr.niveaux[k], a.y);       // rive droite
                    v[k * 2 + 1] = new Vector3(b.x, tr.niveaux[k], b.y);   // rive gauche
                    uv[k * 2] = new Vector2(0f, s / 10f); uv[k * 2 + 1] = new Vector2(1f, s / 10f);
                }
                for (int k = 0; k < n - 1; k++)
                {
                    int o = k * 6, d0 = k * 2, g0 = k * 2 + 1, d1 = k * 2 + 2, g1 = k * 2 + 3;
                    t[o] = g0; t[o + 1] = g1; t[o + 2] = d0;
                    t[o + 3] = d0; t[o + 4] = g1; t[o + 5] = d1;
                }
                var mr = new Mesh { name = "Riviere" + r, indexFormat = v.Length > 60000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mr.vertices = v; mr.uv = uv; mr.triangles = t;
                mr.RecalculateNormals(); mr.RecalculateBounds();
                Surface(racine, string.IsNullOrEmpty(tr.nom) ? "Riviere" + r : tr.nom, Sauver(mr, $"Monde/Riviere{r}.asset"), douce);
            }

            // Des montagnes au loin, au-delà des bords du monde (et de l'autre côté de la mer)
            var lointain = Racine("MontagnesLointaines");
            var c1 = new GameObject("Cordillere1");
            c1.transform.SetParent(lointain, false);
            c1.AddComponent<MeshFilter>().sharedMesh = Sauver(Formes.Cordillere(6400, 8200, 10500, -60, 420, 1100, 180, 2.3f), "Formes/CordillereMonde1.asset");
            c1.AddComponent<MeshRenderer>().sharedMaterial = Lit("#7b83a8");
            var c2 = new GameObject("Cordillere2");
            c2.transform.SetParent(lointain, false);
            c2.transform.rotation = Quaternion.Euler(0, 7f, 0);
            c2.AddComponent<MeshFilter>().sharedMesh = Sauver(Formes.Cordillere(11000, 13500, 16000, -60, 900, 2200, 140, 5.1f), "Formes/CordillereMonde2.asset");
            c2.AddComponent<MeshRenderer>().sharedMaterial = Lit("#8d94b6");
            foreach (var mrd in lointain.GetComponentsInChildren<MeshRenderer>()) mrd.shadowCastingMode = ShadowCastingMode.Off;
        }

        static void Surface(Transform parent, string nom, Mesh mesh, Material m)
        {
            var go = new GameObject(nom);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        // ---------- les ponts ----------

        static void PontsDuGrandMonde(PlanDuMonde plan)
        {
            var racine = Racine("Ponts");
            Material pierre = Lit("#9a958c"), parapet = Lit("#8a857c"), bois = Lit("#6b4a30");
            for (int k = 0; k < plan.ponts.Count; k++)
            {
                var p = plan.ponts[k];
                var go = new GameObject("Pont" + (k + 1)).transform;
                go.SetParent(racine, false);
                go.SetPositionAndRotation(new Vector3(p.position.x, p.hauteur, p.position.y), Quaternion.Euler(0, p.angle, 0));
                float L = p.longueur, W = p.largeur;
                bool grand = L > 22f;
                Bloc(PrimitiveType.Cube, go, new Vector3(0, -.4f, 0), new Vector3(W, .8f, L), grand ? pierre : bois, default, true, "Tablier");
                foreach (float cote in new[] { -1f, 1f })
                    Bloc(PrimitiveType.Cube, go, new Vector3(cote * (W * .5f - .17f), .37f, 0), new Vector3(.35f, .75f, L), grand ? parapet : bois, default, true, "Parapet");
                // Les piles dans la rivière
                int piles = grand ? Mathf.Max(1, Mathf.RoundToInt(L / 16f) - 1) : 1;
                for (int i = 0; i < piles; i++)
                {
                    float z = Mathf.Lerp(-L * .5f, L * .5f, (i + 1f) / (piles + 1f));
                    Bloc(PrimitiveType.Cube, go, new Vector3(0, -5.2f, z), new Vector3(W * .85f, 9f, grand ? 1.8f : .5f), grand ? pierre : bois, default, true, "Pile");
                }
            }
        }

        // ---------- ce que le jeu lit : sols, eaux, carte ----------

        static void DonneesCuites(PlanDuMonde plan, GrillesMonde g, UnityEngine.Terrain[] terrains, List<(Site site, PlanVille plan)> villes)
        {
            int R = GrandMonde.Resolution, f = G / R;
            var sols = new byte[R * R];
            var eaux = new byte[R * R * 2];
            for (int j = 0; j < R; j++)
                for (int i = 0; i < R; i++)
                {
                    sols[j * R + i] = g.sol[(j * f) * G + i * f];
                    float n = -10000f;
                    for (int dj = 0; dj < f; dj++)
                        for (int di = 0; di < f; di++) n = Mathf.Max(n, g.eau[(j * f + dj) * G + i * f + di]);
                    short v = n < -9000f ? short.MinValue : (short)Mathf.Clamp(Mathf.RoundToInt(n * 10f), -32000, 32000);
                    eaux[(j * R + i) * 2] = (byte)(v & 0xff);
                    eaux[(j * R + i) * 2 + 1] = (byte)((v >> 8) & 0xff);
                }
            string dossier = Dossier + "/Monde";
            File.WriteAllBytes(dossier + "/Sols.bytes", sols);
            File.WriteAllBytes(dossier + "/Eaux.bytes", eaux);
            Progres("Carte du monde : peinture", .8f);
            var tex = new Texture2D(G, G, TextureFormat.RGBA32, false);
            var px = g.PeindreCarte(plan);
            PeindreBatiments(px, villes);
            tex.SetPixels32(px);
            tex.Apply(false);
            Progres("Carte du monde : enregistrement", .86f);
            File.WriteAllBytes(dossier + "/Carte.png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(dossier + "/Sols.bytes");
            AssetDatabase.ImportAsset(dossier + "/Eaux.bytes");
            AssetDatabase.ImportAsset(dossier + "/Carte.png");
            if (AssetImporter.GetAtPath(dossier + "/Carte.png") is TextureImporter ti)
            {
                ti.textureType = TextureImporterType.Default;
                ti.maxTextureSize = 4096;
                ti.mipmapEnabled = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.filterMode = FilterMode.Trilinear;
                ti.textureCompression = TextureImporterCompression.Compressed;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.SaveAndReimport();
            }

            var gm = new GameObject("GrandMonde").AddComponent<GrandMonde>();
            gm.terrains = terrains;
            gm.sols = AssetDatabase.LoadAssetAtPath<TextAsset>(dossier + "/Sols.bytes");
            gm.eaux = AssetDatabase.LoadAssetAtPath<TextAsset>(dossier + "/Eaux.bytes");
            gm.carte = AssetDatabase.LoadAssetAtPath<Texture2D>(dossier + "/Carte.png");
            gm.sites = new List<Site>(plan.sites);
            gm.routes = new List<Trace>(plan.routes);
            gm.rivieres = new List<Trace>(plan.rivieres);
        }
    }
}
