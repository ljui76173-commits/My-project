using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Aldermark.Editeur
{
    /// <summary>
    /// Menu « Aldermark > Générer la vallée de Varenne » : construit la scène complète
    /// (terrain sculptable, herbe, arbres, village, pont effondré, vue du Sceau,
    /// Aldric, lumières, ciel) et l'enregistre dans Assets/Scenes/Varenne.unity.
    /// Le terrain reste modifiable ensuite avec les outils Terrain d'Unity.
    /// </summary>
    public static class GenerateurVallee
    {
        const string Dossier = "Assets/Aldermark/Genere";
        const float YMin = -5f, HauteurMax = 115f;
        static readonly Vector3 Soleil1 = new Vector3(-.48f, .55f, -.68f);
        static readonly Vector3 Soleil2 = new Vector3(-.40f, .58f, -.71f);

        static int coucheSceau;
        static Transform racineSceau;
        static readonly Dictionary<string, Material> materiaux = new Dictionary<string, Material>();

        [MenuItem("Aldermark/Générer la vallée de Varenne")]
        public static void Generer()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            coucheSceau = LayerMask.NameToLayer("Sceau");
            if (coucheSceau < 0)
            {
                EditorUtility.DisplayDialog("Aldermark", "Le calque « Sceau » est introuvable. Ajoute-le dans Project Settings > Tags and Layers (calque 8).", "OK");
                return;
            }
            materiaux.Clear();
            foreach (var d in new[] { "", "/Materiaux", "/Terrain", "/Arbres", "/Formes" }) Directory.CreateDirectory(Dossier + d);
            AssetDatabase.Refresh();

            try
            {
                Progres("Nouvelle scène", 0f);
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var soleils = Lumieres();
                Progres("Terrain et relief", .05f);
                Terrain();
                Progres("Arbres, haies et rochers", .55f);
                Arbres();
                Progres("Eau et village", .7f);
                Eau();
                Village();
                Pont();
                Progres("Vue du Sceau", .8f);
                VueDuSceau();
                Progres("Aldric", .9f);
                var joueur = Joueur(out var cam, out var rendu);
                Systemes(soleils, joueur, cam, rendu);
                Habitants();
                ReglagesURP();

                const string chemin = "Assets/Scenes/Varenne.unity";
                EditorSceneManager.SaveScene(scene, chemin);
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(chemin, true) };
                AssetDatabase.SaveAssets();
                Debug.Log("Aldermark : vallée de Varenne générée dans " + chemin + ". Appuie sur Play.");
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        static void Progres(string etape, float f) => EditorUtility.DisplayProgressBar("Aldermark", etape, f);

        // ---------- utilitaires ----------

        static T Sauver<T>(T obj, string nom) where T : Object
        {
            string p = $"{Dossier}/{nom}";
            AssetDatabase.DeleteAsset(p);
            AssetDatabase.CreateAsset(obj, p);
            return obj;
        }

        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
        static float Hash(float a, float b) => Monde.HashPublic(a, b);
        static float H(float x, float z) => Mathf.Max(Monde.Hauteur(x, z), -1.2f);

        static Material Lit(string hex, float lisse = .1f)
        {
            string cle = "Lit" + hex + lisse;
            if (materiaux.TryGetValue(cle, out var m)) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", Hex(hex));
            m.SetFloat("_Smoothness", lisse);
            m.enableInstancing = true;
            materiaux[cle] = Sauver(m, $"Materiaux/Lit_{hex.TrimStart('#')}_{Mathf.RoundToInt(lisse * 100)}.mat");
            return m;
        }

        static Material Unlit(string hex)
        {
            string cle = "Unlit" + hex;
            if (materiaux.TryGetValue(cle, out var m)) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", Hex(hex));
            materiaux[cle] = Sauver(m, $"Materiaux/Unlit_{hex.TrimStart('#')}.mat");
            return m;
        }

        static Material Fumee()
        {
            if (materiaux.TryGetValue("Fumee", out var m)) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetColor("_BaseColor", Color.white);
            materiaux["Fumee"] = Sauver(m, "Materiaux/Fumee.mat");
            return m;
        }

        static GameObject Bloc(PrimitiveType type, Transform parent, Vector3 pos, Vector3 echelle, Material m, Vector3 rotation = default, bool collision = true, string nom = null)
        {
            var g = GameObject.CreatePrimitive(type);
            if (nom != null) g.name = nom;
            if (!collision) Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(rotation);
            g.transform.localScale = echelle;
            g.GetComponent<MeshRenderer>().sharedMaterial = m;
            return g;
        }

        static Transform Racine(string nom) => new GameObject(nom).transform;

        // ---------- modèles importés (Meshy, packs…) ----------

        static readonly string[] DossiersModeles = { "Assets/Aldermark/Models", "Assets/Aldermark/Modeles" };

        /// <summary>
        /// Cherche dans Assets/Aldermark/Models (ou Modeles) un modèle dont le nom contient
        /// l'un des mots-clés (sans tenir compte des majuscules). Renvoie null s'il n'y en a pas.
        /// </summary>
        static GameObject TrouverModele(params string[] motsCles)
        {
            var dossiers = DossiersModeles.Where(AssetDatabase.IsValidFolder).ToArray();
            if (dossiers.Length == 0) return null;
            foreach (var guid in AssetDatabase.FindAssets("t:GameObject", dossiers))
            {
                string chemin = AssetDatabase.GUIDToAssetPath(guid);
                string nom = Path.GetFileNameWithoutExtension(chemin).ToLowerInvariant();
                if (motsCles.Any(m => nom.Contains(m)))
                {
                    Debug.Log($"Aldermark : modèle utilisé pour « {motsCles[0]} » → {chemin}");
                    return AssetDatabase.LoadAssetAtPath<GameObject>(chemin);
                }
            }
            return null;
        }

        /// <summary>Tous les modèles dont le nom contient l'un des mots-clés, triés par nom.</summary>
        static GameObject[] TrouverModeles(params string[] motsCles)
        {
            var dossiers = DossiersModeles.Where(AssetDatabase.IsValidFolder).ToArray();
            if (dossiers.Length == 0) return new GameObject[0];
            var res = AssetDatabase.FindAssets("t:GameObject", dossiers)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(c => motsCles.Any(m => Path.GetFileNameWithoutExtension(c).ToLowerInvariant().Contains(m)))
                .OrderBy(c => c)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Where(g => g != null)
                .ToArray();
            if (res.Length > 0) Debug.Log($"Aldermark : {res.Length} modèle(s) utilisé(s) pour « {motsCles[0]} »");
            return res;
        }

        static Bounds Bornes(GameObject g)
        {
            var rs = g.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(g.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>
        /// Pose un modèle importé : mis à la hauteur voulue, centré sur le point au sol,
        /// légèrement enfoncé, avec des collisions sur chaque maillage.
        /// </summary>
        static GameObject PoserModele(GameObject modele, Transform parent, Vector3 sol, float hauteur, float rotY, float enfonce = .2f)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(modele, parent);
            // On garde la rotation d'import du FBX (axe Z vers le haut chez Meshy) et on tourne seulement autour de la verticale.
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0, rotY, 0) * modele.transform.localRotation);
            go.transform.localScale = modele.transform.localScale;
            var echelle0 = go.transform.localScale;
            var b = Bornes(go);
            if (b.size.y > 1e-4f) go.transform.localScale = echelle0 * (hauteur / b.size.y);
            b = Bornes(go);
            go.transform.position += new Vector3(sol.x - b.center.x, sol.y - enfonce - b.min.y, sol.z - b.center.z);
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh != null) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.On;
            return go;
        }

        // ---------- lumières et ciel ----------

        static (Light, Light) Lumieres()
        {
            var s1 = new GameObject("Soleil").AddComponent<Light>();
            s1.type = LightType.Directional;
            s1.color = Hex("#ffe2a8");
            s1.intensity = 1.5f;
            s1.shadows = LightShadows.Soft;
            s1.transform.rotation = Quaternion.LookRotation(-Soleil1.normalized);

            var s2 = new GameObject("PetitSoleil").AddComponent<Light>();
            s2.type = LightType.Directional;
            s2.color = Hex("#ffc0c8");
            s2.intensity = .15f;
            s2.shadows = LightShadows.None;
            s2.transform.rotation = Quaternion.LookRotation(-Soleil2.normalized);

            var ciel = new Material(Shader.Find("Aldermark/Ciel"));
            ciel.SetVector("_Soleil1", Soleil1);
            ciel.SetVector("_Soleil2", Soleil2);
            RenderSettings.skybox = Sauver(ciel, "Materiaux/Ciel.mat");
            RenderSettings.sun = s1;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex("#8f99cc");
            RenderSettings.ambientEquatorColor = Hex("#9c9aa8");
            RenderSettings.ambientGroundColor = Hex("#5f5232");
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Hex("#a3aad8");
            RenderSettings.fogStartDistance = 80f;
            RenderSettings.fogEndDistance = 1100f;
            return (s1, s2);
        }

        // ---------- terrain ----------

        static float BruitPeriodique(float x, float y, int p)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float u = x - xi, v = y - yi;
            u = u * u * (3 - 2 * u); v = v * v * (3 - 2 * v);
            float Hp(int a, int b) => Monde.HashPublic(((a % p) + p) % p + p * 7.3f, ((b % p) + p) % p + 3.1f);
            float A = Hp(xi, yi), B = Hp(xi + 1, yi), C = Hp(xi, yi + 1), D = Hp(xi + 1, yi + 1);
            float ab = A + (B - A) * u, cd = C + (D - C) * u;
            return ab + (cd - ab) * v;
        }

        /// <summary>
        /// Couche de sol avec une vraie texture peinte (128 px, se répète sans couture) :
        /// 3 aplats de teinte, plus des brins (h), des cailloux (t) ou des fissures (r).
        /// </summary>
        static TerrainLayer Couche(string nom, string hex, char type, float tuile = 3f)
        {
            const int S = 128;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, true) { name = nom, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            Color b = Hex(hex), sombre = b * .78f, clair = Color.Lerp(b, Color.white, .14f);
            sombre.a = 1; clair.a = 1;
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = (float)x / S, v = (float)y / S, n = 0, a = .5f;
                    int per = 4;
                    for (int o = 0; o < 4; o++) { n += a * BruitPeriodique(u * per, v * per, per); per *= 2; a *= .5f; }
                    Color c = n < .42f ? sombre : (n > .6f ? clair : b);
                    float h = Hash(x * 1.7f + nom.Length, y * 2.3f);
                    if (type == 'h' && h > .86f) c = Color.Lerp(c, h > .95f ? clair : sombre * .9f, .8f);
                    if (type == 't' && h > .94f) c = h > .975f ? clair * 1.08f : sombre * .85f;
                    if (type == 'r' && h > .9f) c = sombre * .85f;
                    c.a = 0;
                    px[y * S + x] = c;
                }
            if (type == 'h')
                for (int i = 0; i < 350; i++)
                {
                    int x0 = (int)(Hash(i, nom.Length) * S), y0 = (int)(Hash(i, 7.7f) * S), lg = 2 + (int)(Hash(i, 3.3f) * 3);
                    Color cc = Hash(i, 5.5f) > .5f ? clair : sombre * .92f; cc.a = 0;
                    for (int k = 0; k < lg; k++) px[((y0 + k) % S) * S + x0 % S] = cc;
                }
            if (type == 't')
                for (int i = 0; i < 120; i++)
                {
                    int x0 = (int)(Hash(i, 11.1f + nom.Length) * S), y0 = (int)(Hash(i, 13.7f) * S);
                    Color cc = Hash(i, 9.1f) > .5f ? clair * 1.08f : sombre * .85f; cc.a = 0;
                    for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++) px[((y0 + dy) % S) * S + (x0 + dx) % S] = cc;
                }
            tex.SetPixels(px);
            tex.Apply();
            Sauver(tex, $"Terrain/{nom}_Texture.asset");
            var tl = new TerrainLayer { diffuseTexture = tex, tileSize = new Vector2(tuile, tuile), name = nom, smoothness = 0f, metallic = 0f, specular = Color.black };
            return Sauver(tl, $"Terrain/{nom}.terrainlayer");
        }

        static void Terrain()
        {
            float T = Monde.Taille, demi = T / 2f;
            var td = new TerrainData();
            td.heightmapResolution = 513;
            td.size = new Vector3(T, HauteurMax, T);
            td.alphamapResolution = 512;
            Sauver(td, "Terrain/Varenne.asset");
            int n = td.heightmapResolution;
            var hauteurs = new float[n, n];
            for (int iz = 0; iz < n; iz++)
                for (int ix = 0; ix < n; ix++)
                {
                    float x = -demi + ix / (n - 1f) * T, z = -demi + iz / (n - 1f) * T;
                    hauteurs[iz, ix] = Mathf.Clamp01((H(x, z) - YMin) / HauteurMax);
                }
            td.SetHeights(0, 0, hauteurs);

            // Couleurs du sol
            var couches = new[]
            {
                Couche("Herbe", "#6f9a3a", 'h'), Couche("HerbeDoree", "#c9a43e", 'h'), Couche("HerbeSombre", "#4f7a34", 'h'),
                Couche("Ble", "#d8b048", 'h'), Couche("Culture", "#86a83e", 'h'), Couche("Labour", "#8a6a40", 't'),
                Couche("Chemin", "#9a7a48", 't', 2.5f), Couche("Roche", "#6f7488", 'r', 6f), Couche("Sable", "#b8a878", 't'), Couche("LitRiviere", "#6e6650", 't'),
                Couche("Neige", "#e8eaf4", 'p', 6f),
            };
            td.terrainLayers = couches;
            int a = td.alphamapResolution, nc = couches.Length;
            var carte = new float[a, a, nc];
            var w = new float[nc];
            for (int iz = 0; iz < a; iz++)
            {
                if (iz % 32 == 0) Progres("Couleurs du sol", .1f + .25f * iz / a);
                for (int ix = 0; ix < a; ix++)
                {
                    float x = -demi + (ix + .5f) / a * T, z = -demi + (iz + .5f) / a * T;
                    float y = Monde.Hauteur(x, z);
                    float pente = new Vector2(Monde.Hauteur(x + 1, z) - Monde.Hauteur(x - 1, z), Monde.Hauteur(x, z + 1) - Monde.Hauteur(x, z - 1)).magnitude * .5f;
                    System.Array.Clear(w, 0, nc);
                    float g = Monde.Lisse(.45f, .7f, Monde.Fbm(x * .03f + 9, z * .03f));
                    float s = Monde.Lisse(.45f, .25f, Monde.Fbm(x * .015f, z * .015f + 4)) * .7f;
                    w[0] = (1 - g) * (1 - s); w[1] = g * (1 - s); w[2] = s;
                    if (Monde.DansChamp(x, z)) { System.Array.Clear(w, 0, nc); w[3 + Monde.BandeChamp(x, z)] = 1; }
                    if (Vector2.Distance(new Vector2(x, z), Monde.Village) < 11 || Monde.DistanceRoute(x, z) < 2.3f) { System.Array.Clear(w, 0, nc); w[6] = 1; }
                    float roche = Monde.Lisse(.6f, .95f, pente);
                    for (int k = 0; k < nc; k++) w[k] *= 1 - roche;
                    w[7] += roche;
                    float sable = Monde.Lisse(1.7f, .6f, y);
                    for (int k = 0; k < nc; k++) w[k] *= 1 - sable;
                    w[8] += sable;
                    float neige = Monde.Lisse(52f, 64f, y);
                    for (int k = 0; k < nc; k++) w[k] *= 1 - neige;
                    w[10] += neige;
                    if (y < -.2f) { System.Array.Clear(w, 0, nc); w[9] = 1; }
                    // Transitions nettes et irrégulières, comme peintes au pinceau (plus d'effet « époxy »)
                    for (int k = 0; k < nc; k++)
                        if (w[k] > 0) { w[k] *= .7f + .6f * Hash(ix * .37f + k * 3.1f, iz * .53f); w[k] = w[k] * w[k] * w[k]; }
                    float somme = 0;
                    for (int k = 0; k < nc; k++) somme += w[k];
                    for (int k = 0; k < nc; k++) carte[iz, ix, k] = somme > 0 ? w[k] / somme : (k == 0 ? 1 : 0);
                }
            }
            td.SetAlphamaps(0, 0, carte);


            EditorUtility.SetDirty(td);
            var go = UnityEngine.Terrain.CreateTerrainGameObject(td);
            go.name = "Terrain";
            go.transform.position = new Vector3(-demi, YMin, -demi);
            var t = go.GetComponent<UnityEngine.Terrain>();
            t.materialTemplate = Sauver(new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit")), "Terrain/MateriauTerrain.mat");
            t.treeDistance = 2000f;
            t.treeBillboardDistance = 2000f;
            t.treeMaximumFullLODCount = 5000;
            t.detailObjectDistance = 90f;
            t.detailObjectDensity = 1f;
            t.heightmapPixelError = 3f;
            t.drawTreesAndFoliage = false; // l'herbe est dessinée par HerbeVivante
        }

        static Texture2D TextureHerbe(string nom, bool fleurs)
        {
            const int S = 64;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, true) { name = nom, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point };
            var px = new Color[S * S];
            var couleursFleurs = new[] { Hex("#f4f0ff"), Hex("#b9a2ff"), Hex("#ffd84a"), Hex("#ff8a8a"), Hex("#8fc4ff") };
            for (int b = 0; b < 8; b++)
            {
                float x0 = 4 + Hash(b, fleurs ? 3 : 1) * (S - 8), haut = S * (.55f + Hash(b, 2) * .45f), sommet = x0 + (Hash(b, 5) - .5f) * 12, larg = 3.5f;
                for (int y = 0; y < (int)haut; y++)
                {
                    float t = y / haut, cx = Mathf.Lerp(x0, sommet, t), demi = larg * (1 - t);
                    for (int x = Mathf.Max(0, (int)(cx - demi)); x <= Mathf.Min(S - 1, (int)(cx + demi)); x++)
                    {
                        float v = Mathf.Lerp(.55f, 1.1f, t);
                        px[y * S + x] = fleurs ? new Color(.45f * v, .62f * v, .3f * v, 1) : new Color(v, v, v, 1);
                    }
                }
                if (fleurs)
                {
                    var cf = couleursFleurs[b % couleursFleurs.Length];
                    int fx = (int)sommet, fy = (int)haut;
                    for (int y = fy - 3; y <= fy + 3; y++)
                        for (int x = fx - 3; x <= fx + 3; x++)
                            if (x >= 0 && x < S && y >= 0 && y < S && (x - fx) * (x - fx) + (y - fy) * (y - fy) <= 9) px[y * S + x] = cf;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return Sauver(tex, $"Terrain/{nom}.asset");
        }

        static void Herbe(TerrainData td)
        {
            var herbe = new DetailPrototype
            {
                prototypeTexture = TextureHerbe("TexHerbe", false), renderMode = DetailRenderMode.Grass, usePrototypeMesh = false,
                healthyColor = Hex("#7fb045"), dryColor = Hex("#a9b648"), minWidth = .3f, maxWidth = .55f, minHeight = .2f, maxHeight = .42f, noiseSpread = .3f
            };
            var ble = new DetailPrototype
            {
                prototypeTexture = herbe.prototypeTexture, renderMode = DetailRenderMode.Grass, usePrototypeMesh = false,
                healthyColor = Hex("#e8c454"), dryColor = Hex("#d6a744"), minWidth = .4f, maxWidth = .7f, minHeight = .7f, maxHeight = 1f, noiseSpread = .2f
            };
            var fleurs = new DetailPrototype
            {
                prototypeTexture = TextureHerbe("TexFleurs", true), renderMode = DetailRenderMode.Grass, usePrototypeMesh = false,
                healthyColor = Color.white, dryColor = Hex("#e8e0d0"), minWidth = .4f, maxWidth = .6f, minHeight = .3f, maxHeight = .5f, noiseSpread = .5f
            };
            td.detailPrototypes = new[] { herbe, ble, fleurs };
            td.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
            td.SetDetailResolution(1024, 32);
            int r = td.detailResolution;
            float T = Monde.Taille, demi = T / 2f;
            var c0 = new int[r, r]; var c1 = new int[r, r]; var c2 = new int[r, r];
            for (int iz = 0; iz < r; iz++)
                for (int ix = 0; ix < r; ix++)
                {
                    float x = -demi + (ix + .5f) / r * T, z = -demi + (iz + .5f) / r * T;
                    float y = Monde.Hauteur(x, z);
                    if (y < .9f || y > 44 || Monde.DistanceRoute(x, z) < 1.7f || Vector2.Distance(new Vector2(x, z), Monde.Village) < 11.5f) continue;
                    if (Monde.DansChamp(x, z))
                    {
                        int b = Monde.BandeChamp(x, z);
                        if (b == 0) c1[iz, ix] = 3;
                        else if (b == 1) c0[iz, ix] = 2;
                        continue;
                    }
                    c0[iz, ix] = 3;
                    if (Hash(ix * .37f, iz * .71f) < .08f) c2[iz, ix] = 2;
                }
            td.SetDetailLayer(0, 0, 0, c0);
            td.SetDetailLayer(0, 0, 1, c1);
            td.SetDetailLayer(0, 0, 2, c2);
            td.wavingGrassStrength = .15f;
            td.wavingGrassAmount = .25f;
            td.wavingGrassSpeed = .5f;
            td.wavingGrassTint = Color.white;
        }

        static GameObject PrefabArbre(string nom, Mesh mesh, Material[] mats, bool collision)
        {
            var go = new GameObject(nom);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = mats;
            if (collision)
            {
                var c = go.AddComponent<CapsuleCollider>();
                c.center = new Vector3(0, 2, 0); c.radius = .45f; c.height = 4f;
            }
            string p = $"{Dossier}/Arbres/{nom}.prefab";
            AssetDatabase.DeleteAsset(p);
            var pf = PrefabUtility.SaveAsPrefabAsset(go, p);
            Object.DestroyImmediate(go);
            return pf;
        }

        static void Arbres()
        {
            var sapin = Sauver(Formes.Sapin(), "Arbres/Sapin.asset");
            var dore = Sauver(Formes.ArbreDore(), "Arbres/ArbreDore.asset");
            var buisson = Sauver(Formes.Buisson(), "Arbres/Buisson.asset");
            var tronc = Lit("#5b4030");
            var protos = new[]
            {
                PrefabArbre("SapinSombre", sapin, new[] { tronc, Lit("#3d5a2e") }, true),
                PrefabArbre("SapinClair", sapin, new[] { tronc, Lit("#4c7040") }, true),
                PrefabArbre("ArbreJaune", dore, new[] { tronc, Lit("#e3ad3c") }, true),
                PrefabArbre("ArbreOrange", dore, new[] { tronc, Lit("#d9922e") }, true),
                PrefabArbre("ArbreRoux", dore, new[] { tronc, Lit("#c7782a") }, true),
                PrefabArbre("BuissonVert", buisson, new[] { Lit("#5f7f3a") }, false),
                PrefabArbre("BuissonOlive", buisson, new[] { Lit("#8a9a44") }, false),
                PrefabArbre("Haie", buisson, new[] { Lit("#4f6e34") }, false),
                PrefabArbre("Rocher", buisson, new[] { Lit("#8a8794") }, false),
            };
            var foret = Racine("Foret");
            void Poser(int proto, float x, float z, float rot, Vector3 echelle, float enfonce = .3f)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(protos[proto], foret);
                go.transform.SetPositionAndRotation(new Vector3(x, Monde.Hauteur(x, z) - enfonce, z), Quaternion.Euler(0, rot, 0));
                go.transform.localScale = echelle;
            }

            int np = 0, ng = 0, nb = 0;
            for (int i = 0; i < 16000 && (np < 1100 || ng < 260 || nb < 700); i++)
            {
                float x = (Hash(i, 1.3f) - .5f) * 560, z = (Hash(i, 7.9f) - .5f) * 560, y = Monde.Hauteur(x, z), d = Mathf.Abs(x - Monde.Riviere(z));
                float rd = Monde.DistanceRoute(x, z), r = Hash(i, 3.1f), dv = Vector2.Distance(new Vector2(x, z), Monde.Village);
                float s = .8f + Hash(i, 5.5f) * .7f;
                if (nb < 700 && r > .72f && r < .86f && d > Monde.Berge + 1 && y < 34 && rd > 3 && !Monde.DansChamp(x, z) && dv > 14)
                {
                    float sb = .6f + Hash(i, 5.5f) * .8f;
                    Poser(5 + nb % 2, x, z, r * 360f, Vector3.one * sb, .2f);
                    nb++;
                    continue;
                }
                var pos = new Vector2(x, z);
                if (d < Monde.Berge + 4 || y > 62 || rd < 5 || Monde.DansChamp(x, z) || dv < 36 ||
                    Vector2.Distance(pos, Monde.Depart) < 9 || DistSegment(pos, Monde.Depart, new Vector2(Monde.PontX, 0)) < 26) continue;
                float f = Monde.Fbm(x * .013f + 5, z * .013f);
                var e = new Vector3(s, s * (.9f + r * .4f), s);
                if ((f > .5f || r < .05f) && np < 1100) { Poser(r < .5f ? 0 : 1, x, z, r * 360f, e); np++; }
                else if (r > .93f && ng < 260 && y < 22) { Poser(2 + ng % 3, x, z, r * 360f, e); ng++; }
            }

            // Haies le long des bandes de champs (avec des trouées)
            float n2 = .85f * .85f + .3f * .3f;
            var dir = new Vector2(-.3f, .85f).normalized;
            for (int k = 6; k <= 30; k++)
            {
                var basePt = new Vector2(.85f, .3f) * (7f * k / n2);
                for (float t = -160; t <= 160; t += 1.4f)
                {
                    var p = basePt + dir * t;
                    if (!Monde.DansChamp(p.x, p.y) || Monde.DistanceRoute(p.x, p.y) < 3f) continue;
                    if (Monde.Fbm(p.x * .08f, p.y * .08f + k) < .38f) continue;
                    float h = Hash(p.x * .7f, p.y * .3f);
                    Poser(7, p.x, p.y, h * 360f, new Vector3(.75f + h * .35f, .6f + h * .4f, .75f + h * .35f), .15f);
                    if (h > .985f) Poser(2 + (int)(h * 1000) % 3, p.x, p.y, h * 720f, Vector3.one * .9f);
                }
            }

            // Rochers dans les prés et sur les pentes
            for (int i = 0; i < 260; i++)
            {
                float x = (Hash(i, 61.3f) - .5f) * 520, z = (Hash(i, 67.9f) - .5f) * 520, y = Monde.Hauteur(x, z);
                if (Mathf.Abs(x - Monde.Riviere(z)) < Monde.Berge + 2 || Monde.DistanceRoute(x, z) < 3 || Monde.DansChamp(x, z) ||
                    Vector2.Distance(new Vector2(x, z), Monde.Village) < 30 || y > 70) continue;
                float s = .4f + Hash(i, 63) * 1.4f;
                Poser(8, x, z, Hash(i, 64) * 360f, new Vector3(s * 1.2f, s * .7f, s), s * .25f);
            }
        }

        static float DistSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + t * ab);
        }

        // ---------- eau, village, pont ----------

        static void Eau()
        {
            var eau = Bloc(PrimitiveType.Plane, null, new Vector3(0, -.1f, 0), new Vector3(62, 1, 62), Lit("#8fb4d8", .9f), default, false, "Riviere");
            eau.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            // Montagnes lointaines (deux rangées) et sol au-delà du terrain
            var lointain = Racine("MontagnesLointaines");
            var c1 = new GameObject("Cordillere1");
            c1.transform.SetParent(lointain, false);
            c1.AddComponent<MeshFilter>().sharedMesh = Sauver(Formes.Cordillere(520, 700, 900, -6, 70, 190, 96, 1.7f), "Formes/Cordillere1.asset");
            c1.AddComponent<MeshRenderer>().sharedMaterial = Lit("#6a7096");
            var c2 = new GameObject("Cordillere2");
            c2.transform.SetParent(lointain, false);
            c2.transform.rotation = Quaternion.Euler(0, 11f, 0);
            c2.AddComponent<MeshFilter>().sharedMesh = Sauver(Formes.Cordillere(850, 1050, 1250, -6, 120, 300, 80, 4.3f), "Formes/Cordillere2.asset");
            c2.AddComponent<MeshRenderer>().sharedMaterial = Lit("#7c84aa");
            var sol = Bloc(PrimitiveType.Plane, lointain, new Vector3(0, -6f, 0), new Vector3(260, 1, 260), Lit("#4f6a3a"), default, false, "SolLointain");
            sol.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        static void Fumee(Transform parent, Vector3 pos)
        {
            var go = new GameObject("Fumee");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.rotation = Quaternion.Euler(-90f, 0, 0);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 6f;
            main.startSpeed = .6f;
            main.startSize = new ParticleSystem.MinMaxCurve(.35f, .8f);
            main.startColor = new Color(.82f, .82f, .88f, .22f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 50;
            main.prewarm = true;
            var em = ps.emission; em.rateOverTime = 2f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 6f; sh.radius = .15f;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(.6f);
            vel.y = new ParticleSystem.MinMaxCurve(.3f);
            vel.z = new ParticleSystem.MinMaxCurve(.2f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .15f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, .6f, 1, 1.8f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Fumee();
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        static void Village()
        {
            var racine = Racine("Village");
            var toit = Sauver(Formes.Toit(), "Formes/Toit.asset");
            string[] murs = { "#eadfc6", "#e2d3b4", "#d9c9a8", "#8d6b4b" }, toits = { "#b4523a", "#3f5a8c", "#7a4a33", "#94403a", "#4f6b8f" };
            var V = Monde.Village;
            var modelesMaisons = TrouverModeles("maison", "house");
            var places = new List<Vector2>();
            int essai = 0;
            while (places.Count < 28 && essai++ < 900)
            {
                float a = Hash(essai, 2.2f) * 6.283f, r = 9 + Hash(essai, 4.4f) * 30, x = V.x + Mathf.Cos(a) * r, z = V.y + Mathf.Sin(a) * r;
                var p = new Vector2(x, z);
                if (Monde.DistanceRoute(x, z) < 4.5f || places.Exists(q => Vector2.Distance(q, p) < 9)) continue;
                places.Add(p);
                float w = 4.5f + Hash(essai, 1) * 2.5f, dp = 4 + Hash(essai, 2) * 2, h = 3 + Hash(essai, 3) * 1.5f, rh = 2 + Hash(essai, 6) * 1.4f;
                float ry = Mathf.Atan2(V.x - x, V.y - z) * Mathf.Rad2Deg + (Hash(essai, 9) - .5f) * 23f;
                var maison = new GameObject("Maison" + places.Count).transform;
                maison.SetParent(racine, false);
                maison.position = new Vector3(x, H(x, z), z);
                maison.rotation = Quaternion.Euler(0, ry, 0);
                if (modelesMaisons.Length > 0)
                {
                    PoserModele(modelesMaisons[places.Count % modelesMaisons.Length], maison, maison.position, h + rh + 1f, ry, .3f);
                    if (Hash(essai, 8) > .65f) Fumee(maison, new Vector3(w * .2f, h + rh + 1.2f, 0));
                    continue;
                }
                Bloc(PrimitiveType.Cube, maison, new Vector3(0, h / 2 - .3f, 0), new Vector3(w, h, dp), Lit(murs[essai % 4]), default, true, "Murs");
                var t = new GameObject("Toit");
                t.transform.SetParent(maison, false);
                t.transform.localPosition = new Vector3(0, h - .3f, 0);
                t.transform.localScale = new Vector3(w + 1f, rh, dp + .9f);
                t.AddComponent<MeshFilter>().sharedMesh = toit;
                t.AddComponent<MeshRenderer>().sharedMaterial = Lit(toits[essai % 5]);
                if (Hash(essai, 8) > .65f)
                {
                    Bloc(PrimitiveType.Cube, maison, new Vector3(w * .25f, h + 1f, 0), new Vector3(.7f, 2.4f, .7f), Lit("#6d6670"), default, false, "Cheminee");
                    Fumee(maison, new Vector3(w * .25f, h + 2.3f, 0));
                }
            }

            // Puits
            var puits = new GameObject("Puits").transform;
            puits.SetParent(racine, false);
            puits.position = new Vector3(V.x, H(V.x, V.y), V.y);
            var modelePuits = TrouverModele("puits", "well");
            if (modelePuits != null) PoserModele(modelePuits, puits, puits.position, 3.4f, 0f);
            else
            {
            Bloc(PrimitiveType.Cylinder, puits, new Vector3(0, .4f, 0), new Vector3(2.7f, .55f, 2.7f), Lit("#8d8796"));
            Bloc(PrimitiveType.Cube, puits, new Vector3(-1.1f, 1.6f, 0), new Vector3(.2f, 2.4f, .2f), Lit("#5b4030"), default, false);
            Bloc(PrimitiveType.Cube, puits, new Vector3(1.1f, 1.6f, 0), new Vector3(.2f, 2.4f, .2f), Lit("#5b4030"), default, false);
            var tp = new GameObject("ToitPuits");
            tp.transform.SetParent(puits, false);
            tp.transform.localPosition = new Vector3(0, 2.7f, 0);
            tp.transform.localScale = new Vector3(3f, 1.1f, 2.6f);
            tp.AddComponent<MeshFilter>().sharedMesh = toit;
            tp.AddComponent<MeshRenderer>().sharedMaterial = Lit("#7a4a33");
            }

            // Four à chaux
            var K = Monde.Four;
            var four = new GameObject("FourAChaux").transform;
            four.SetParent(racine, false);
            four.position = new Vector3(K.x, H(K.x, K.y), K.y);
            var modeleFour = TrouverModele("four", "kiln", "chaux");
            if (modeleFour != null)
            {
                float versVillage = Mathf.Atan2(Monde.Village.x - K.x, Monde.Village.y - K.y) * Mathf.Rad2Deg;
                PoserModele(modeleFour, four, four.position, 6.5f, versVillage);
            }
            else
            {
                Bloc(PrimitiveType.Cylinder, four, new Vector3(0, 2.9f, 0), new Vector3(7.5f, 3.25f, 7.5f), Lit("#a49d92"));
                Bloc(PrimitiveType.Cube, four, new Vector3(0, 1f, 3.7f), new Vector3(2f, 2.2f, 1f), Lit("#2b2530"), default, false, "Foyer");
            }

            // Maison de Corvin et sa barque
            var C = Monde.Corvin;
            var corvin = new GameObject("MaisonCorvin").transform;
            corvin.SetParent(racine, false);
            corvin.position = new Vector3(C.x, H(C.x, C.y), C.y);
            corvin.rotation = Quaternion.Euler(0, 17f, 0);
            Bloc(PrimitiveType.Cube, corvin, new Vector3(0, 1.4f, 0), new Vector3(6f, 3.4f, 5f), Lit("#7d6047"), default, true, "Murs");
            var tc = new GameObject("Toit");
            tc.transform.SetParent(corvin, false);
            tc.transform.localPosition = new Vector3(0, 3.1f, 0);
            tc.transform.localScale = new Vector3(7f, 2.4f, 5.9f);
            tc.AddComponent<MeshFilter>().sharedMesh = toit;
            tc.AddComponent<MeshRenderer>().sharedMaterial = Lit("#5a3a2b");
            Fumee(corvin, new Vector3(1.5f, 5.2f, 0));
            Bloc(PrimitiveType.Cube, racine, new Vector3(Monde.Riviere(22) + 6, .1f, 22), new Vector3(5f, .7f, 1.8f), Lit("#5b4030"), new Vector3(0, 23f, 0), false, "Barque");

            // Meules de foin
            for (int i = 0; i < 9; i++)
            {
                float x = 80 + Hash(i, 11) * 55, z = -18 + Hash(i, 12) * 75;
                if (!Monde.DansChamp(x, z)) continue;
                var m = new GameObject("Meule").transform;
                m.SetParent(racine, false);
                m.position = new Vector3(x, H(x, z), z);
                Bloc(PrimitiveType.Cylinder, m, new Vector3(0, .6f, 0), new Vector3(3f, .8f, 3f), Lit("#d6b052"));
                Bloc(PrimitiveType.Sphere, m, new Vector3(0, 1.5f, 0), new Vector3(3f, 1.6f, 3f), Lit("#c49c45"), default, false);
            }

            // Charrettes du Comte, bloquées sur l'autre rive
            for (int i = 0; i < 3; i++)
            {
                float x = Monde.PontX - 26 - i * 7, z = -4 - i * 2.5f;
                var c = new GameObject("Charrette").transform;
                c.SetParent(racine, false);
                c.position = new Vector3(x, H(x, z), z);
                c.rotation = Quaternion.Euler(0, 9f, 0);
                Bloc(PrimitiveType.Cube, c, new Vector3(0, 1.3f, 0), new Vector3(4f, 1.2f, 2.2f), Lit("#7a5a3c"));
                Bloc(PrimitiveType.Cube, c, new Vector3(0, 2.3f, 0), new Vector3(3.2f, 1f, 1.8f), Lit("#d8c9a0"), default, false, "Sacs");
                Bloc(PrimitiveType.Cylinder, c, new Vector3(0, .75f, 1.2f), new Vector3(1.5f, .12f, 1.5f), Lit("#4a3626"), new Vector3(90, 0, 0), false, "Roue");
                Bloc(PrimitiveType.Cylinder, c, new Vector3(0, .75f, -1.2f), new Vector3(1.5f, .12f, 1.5f), Lit("#4a3626"), new Vector3(90, 0, 0), false, "Roue");
            }
        }

        static void Pont()
        {
            var racine = Racine("PontDeVarenne");
            var pierre = Lit("#a29a8e");
            var sombre = Lit("#8e867c");
            float BX = Monde.PontX, Y = 4.6f;
            foreach (int s in new[] { -1, 1 })
            {
                Bloc(PrimitiveType.Cube, racine, new Vector3(BX + s * 11, Y, 0), new Vector3(14, 1.3f, 5), pierre, default, true, "Tablier");
                Bloc(PrimitiveType.Cube, racine, new Vector3(BX + s * 9.5f, Y - 5, 0), new Vector3(3.2f, 9, 4.6f), sombre, default, true, "Pile");
                Bloc(PrimitiveType.Cube, racine, new Vector3(BX + s * 17.5f, Y - 4, 0), new Vector3(3.6f, 7, 5.2f), sombre, default, true, "Culee");
                Bloc(PrimitiveType.Cube, racine, new Vector3(BX + s * 11, Y + 1.1f, -2.3f), new Vector3(14, .9f, .4f), pierre, default, true, "Parapet");
                Bloc(PrimitiveType.Cube, racine, new Vector3(BX + s * 11, Y + 1.1f, 2.3f), new Vector3(14, .9f, .4f), pierre, default, true, "Parapet");
            }
            Bloc(PrimitiveType.Cube, racine, new Vector3(BX - 2.6f, .6f, .6f), new Vector3(6, 1.2f, 4.6f), pierre, new Vector3(0, 17f, 31f), true, "DalleEffondree");
            for (int i = 0; i < 7; i++)
                Bloc(PrimitiveType.Cube, racine, new Vector3(BX - 3 + Hash(i, 4) * 7, -.2f + Hash(i, 5) * .6f, -3 + Hash(i, 6) * 7),
                    new Vector3(1.4f, 1f, 1.2f), sombre, new Vector3(0, Hash(i, 7) * 170f, 0), true, "Bloc");
        }

        // ---------- vue du Sceau ----------

        static void Couche(GameObject g) { g.layer = coucheSceau; foreach (Transform c in g.transform) Couche(c.gameObject); }

        static void Ligne(string nom, List<Vector3> pts, float largeur, Material m)
        {
            var go = new GameObject(nom) { layer = coucheSceau };
            go.transform.SetParent(racineSceau, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = pts.Count;
            lr.SetPositions(pts.ToArray());
            lr.widthMultiplier = largeur;
            lr.sharedMaterial = m;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.numCornerVertices = 2;
            lr.alignment = LineAlignment.View;
        }

        static List<Vector3> SurRoute(Vector2[] pts, float hauteur)
        {
            var res = new List<Vector3>();
            for (int i = 0; i < pts.Length - 1; i++)
            {
                var a = pts[i]; var b = pts[i + 1];
                int n = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(a, b) / 3f));
                for (int k = 0; k < n; k++)
                {
                    var p = Vector2.Lerp(a, b, k / (float)n);
                    res.Add(new Vector3(p.x, Mathf.Max(Monde.Hauteur(p.x, p.y), 0) + hauteur, p.y));
                }
            }
            var l = pts[pts.Length - 1];
            res.Add(new Vector3(l.x, Mathf.Max(Monde.Hauteur(l.x, l.y), 0) + hauteur, l.y));
            return res;
        }

        static List<Vector3> Courbe(Vector3[] c, int parSegment)
        {
            var res = new List<Vector3>();
            for (int i = 0; i < c.Length - 1; i++)
            {
                Vector3 p0 = c[Mathf.Max(i - 1, 0)], p1 = c[i], p2 = c[i + 1], p3 = c[Mathf.Min(i + 2, c.Length - 1)];
                for (int k = 0; k < parSegment; k++)
                {
                    float t = k / (float)parSegment;
                    res.Add(.5f * (2 * p1 + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t * t * t));
                }
            }
            res.Add(c[c.Length - 1]);
            return res;
        }

        static void Etiquette(string texte, Vector3 pos, string hex)
        {
            var go = new GameObject("Etiquette_" + texte.Split(' ')[0]);
            go.transform.SetParent(racineSceau, false);
            go.transform.position = pos;
            var e = go.AddComponent<EtiquetteSceau>();
            e.texte = texte;
            e.couleur = Hex(hex);
        }

        static void VueDuSceau()
        {
            racineSceau = Racine("VueDuSceau");
            racineSceau.gameObject.layer = coucheSceau;
            var ambre = Unlit("#ffb23a"); var rouge = Unlit("#ff3b30"); var frais = Unlit("#fff0c0"); var bleu = Unlit("#2f7dff");
            float BX = Monde.PontX;
            foreach (var r in Monde.Routes) Ligne("FluxRoute", SurRoute(r, .9f), .7f, ambre);
            Ligne("FluxPontOuest", new List<Vector3> { new Vector3(BX - 17, 5.6f, 0), new Vector3(BX - 4.5f, 5.6f, 0) }, .8f, ambre);
            Ligne("FluxRompu", new List<Vector3> { new Vector3(BX - 4.5f, 5.6f, 0), new Vector3(BX + 4.5f, 5.6f, 0) }, 1.1f, rouge);
            Ligne("FluxPontEst", new List<Vector3> { new Vector3(BX + 4.5f, 5.6f, 0), new Vector3(BX + 17, 5.6f, 0) }, .8f, ambre);
            var K = Monde.Four; var C = Monde.Corvin;
            Ligne("FluxCache", SurRoute(new[] { K, new Vector2(70, -20), new Vector2(50, 6), new Vector2(C.x + 4, C.y - 6), C }, 1.4f), .45f, frais);

            var noeud = new Vector3(BX, -7, 8);
            foreach (var fin in new[] { new Vector3(BX - 30, -7, -270), new Vector3(BX + 20, -7, 270), new Vector3(270, -7, -60) })
            {
                Vector3 m1 = Vector3.Lerp(noeud, fin, .33f) + new Vector3(18, -3, 12), m2 = Vector3.Lerp(noeud, fin, .66f) + new Vector3(-16, 2, -10);
                Ligne("VeineMana", Courbe(new[] { noeud, m1, m2, fin }, 16), 2.4f, bleu);
            }
            var n = Bloc(PrimitiveType.Sphere, racineSceau, noeud, Vector3.one * 9f, Unlit("#9fd4ff"), default, false, "NoeudDeMana");
            n.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            Couche(racineSceau.gameObject);

            Etiquette("Pont de Varenne — flux rompu", new Vector3(BX, 8.5f, 0), "#ff4d4d");
            Etiquette("Four à chaux — inactif. Remise en état possible.", new Vector3(K.x, H(K.x, K.y) + 9, K.y), "#ffbf4a");
            Etiquette("Nœud de mana — classe : majeur", noeud + Vector3.up * 7, "#6fb4ff");
            Etiquette("Maison de Corvin — flux récent", new Vector3(C.x, H(C.x, C.y) + 8, C.y), "#ffbf4a");
            Etiquette("Charrettes du Comte — en attente", new Vector3(BX - 33, H(BX - 33, -6) + 5, -6), "#ffbf4a");
        }

        // ---------- Aldric, systèmes, habitants ----------

        static GameObject Joueur(out Camera cam, out RenduPeint rendu)
        {
            var D = Monde.Depart;
            var j = new GameObject("Aldric");
            j.transform.position = new Vector3(D.x, H(D.x, D.y) + .2f, D.y);
            j.transform.rotation = Quaternion.Euler(0, Mathf.Atan2(Monde.PontX - D.x, 0 - D.y) * Mathf.Rad2Deg, 0);
            var cc = j.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = .35f; cc.center = new Vector3(0, .9f, 0);
            cc.slopeLimit = 50f; cc.stepOffset = .4f;

            var camGo = new GameObject("Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(j.transform, false);
            camGo.transform.localPosition = new Vector3(0, 1.7f, 0);
            cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 68f; cam.nearClipPlane = .05f; cam.farClipPlane = 2000f;
            camGo.AddComponent<AudioListener>();
            rendu = camGo.AddComponent<RenduPeint>();
            rendu.materiauPeint = Sauver(new Material(Shader.Find("Aldermark/PeintPixel")), "Materiaux/PeintPixel.mat");
            rendu.directionSoleil = Soleil1;

            var ja = j.AddComponent<JoueurAldric>();
            ja.cam = cam; ja.rendu = rendu;
            return j;
        }

        static void Systemes((Light, Light) soleils, GameObject joueur, Camera cam, RenduPeint rendu)
        {
            var s = new GameObject("Systemes");
            var sn = s.AddComponent<SoleilNuages>();
            sn.soleil = soleils.Item1; sn.soleil2 = soleils.Item2; sn.rendu = rendu;
            sn.direction1 = Soleil1; sn.direction2 = Soleil2;
            sn.intensiteSoleil = 1.5f; sn.intensiteSoleil2 = .15f;
            var hud = s.AddComponent<HUD>();
            hud.joueur = joueur.GetComponent<JoueurAldric>(); hud.cam = cam;
            s.AddComponent<Oiseaux>();
            var herbe = s.AddComponent<HerbeVivante>();
            herbe.joueur = joueur.transform;
            herbe.materiau = Sauver(new Material(Shader.Find("Aldermark/HerbeVivante")) { enableInstancing = true }, "Materiaux/HerbeVivante.mat");
        }

        static void Habitants()
        {
            var racine = Racine("Habitants");
            string[] tuniques = { "#8c5a3c", "#5b6e8c", "#7a8c4a", "#a0484a", "#d8c9a0", "#6a4c7a" };
            var V = Monde.Village;
            for (int i = 0; i < 11; i++)
            {
                float a = Hash(i, 9) * 6.283f, r = 6 + Hash(i, 10) * 22;
                var go = new GameObject("Villageois" + (i + 1));
                go.transform.SetParent(racine, false);
                go.transform.position = new Vector3(V.x + Mathf.Cos(a) * r, 0, V.y + Mathf.Sin(a) * r);
                var h = go.AddComponent<Habitant>();
                h.tunique = Hex(tuniques[i % tuniques.Length]);
                h.cheveux = Hex(i % 3 == 0 ? "#2a2020" : "#4a3020");
                h.taille = .95f + Hash(i, 8) * .15f;
                h.centre = V;
            }

            var L = new Vector2(106.8f, -48.4f);
            var lise = new GameObject("Lise");
            lise.transform.SetParent(racine, false);
            lise.transform.position = new Vector3(L.x, 0, L.y);
            lise.transform.rotation = Quaternion.Euler(0, Mathf.Atan2(Monde.Depart.x - L.x, Monde.Depart.y - L.y) * Mathf.Rad2Deg, 0);
            var hl = lise.AddComponent<Habitant>();
            hl.tunique = Hex("#c98a8a"); hl.cheveux = Hex("#7a4a2a"); hl.taille = .68f; hl.promeneur = false; hl.panier = true;

            for (int i = 0; i < 2; i++)
            {
                float x = Monde.PontX - 24 - i * 9, z = -6.5f - i * 2.5f;
                var go = new GameObject("Charretier" + (i + 1));
                go.transform.SetParent(racine, false);
                go.transform.position = new Vector3(x, 0, z);
                go.transform.rotation = Quaternion.Euler(0, 90, 0);
                var h = go.AddComponent<Habitant>();
                h.tunique = Hex(i == 0 ? "#7a5a3c" : "#5b6e8c"); h.promeneur = false;
            }
        }

        static void ReglagesURP()
        {
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.shadowDistance = 160f;
                EditorUtility.SetDirty(urp);
            }
        }
    }
}
