using UnityEditor;
using UnityEngine;

namespace Aldermark.Editeur
{
    /// <summary>
    /// Décors du prologue, construits sous la vallée (600 m plus bas, invisibles depuis la surface) :
    /// l'entrepôt, la rue sous la pluie avec le camion, et le vide blanc de l'Entre-Deux.
    /// </summary>
    public static partial class GameplayPont
    {
        static readonly Vector3 O = new Vector3(0, -600f, 0);

        static Transform Cube(Transform parent, Vector3 pos, Vector3 taille, string hex, bool collision, Vector3 rot = default)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (!collision) Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.position = pos;
            g.transform.rotation = Quaternion.Euler(rot);
            g.transform.localScale = taille;
            g.GetComponent<MeshRenderer>().sharedMaterial = Mat(hex);
            return g.transform;
        }

        static Material Eclairant(string nom, Color c, bool doubleFace = false)
        {
            string chemin = $"Assets/Aldermark/Genere/Materiaux/Gameplay_{nom}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(chemin);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(m, chemin);
            }
            m.SetColor("_BaseColor", c);
            if (doubleFace) m.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Transform Repere(Transform parent, string nom, Vector3 pos, float lacet)
        {
            var t = new GameObject(nom).transform;
            t.SetParent(parent, false);
            t.position = pos;
            t.rotation = Quaternion.Euler(0, lacet, 0);
            return t;
        }

        static void Lampe(Transform parent, Vector3 pos, Color c, float intensite, float portee)
        {
            var l = new GameObject("Lampe").AddComponent<Light>();
            l.transform.SetParent(parent, false);
            l.transform.position = pos;
            l.type = LightType.Point; l.color = c; l.intensity = intensite; l.range = portee;
            l.shadows = LightShadows.None;
        }

        static void Texte3D(Transform parent, string texte, Vector3 pos, Vector3 rot, float taille, Color c)
        {
            var go = new GameObject("Texte");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(rot);
            var tm = go.AddComponent<TextMesh>();
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = f; tm.text = texte; tm.characterSize = taille; tm.fontSize = 48; tm.color = c;
            tm.anchor = TextAnchor.MiddleCenter;
            go.GetComponent<MeshRenderer>().sharedMaterial = f.material;
        }

        static void ConstruirePrologue(Transform racine)
        {
            var pro = new GameObject("Prologue").transform;
            pro.SetParent(racine, false);
            var p = pro.gameObject.AddComponent<Prologue>();

            // ---------- l'entrepôt ----------
            var ent = new GameObject("Entrepot").transform;
            ent.SetParent(pro, false);
            Cube(ent, O + new Vector3(0, -.1f, 0), new Vector3(46, .2f, 28), "#6a6a6c", true);
            Cube(ent, O + new Vector3(0, 9f, 0), new Vector3(46, .3f, 28), "#4a4a50", true);
            Cube(ent, O + new Vector3(0, 4.5f, -14f), new Vector3(46, 9, .3f), "#8a8a8e", true);
            Cube(ent, O + new Vector3(0, 4.5f, 14f), new Vector3(46, 9, .3f), "#8a8a8e", true);
            Cube(ent, O + new Vector3(-23f, 4.5f, 0), new Vector3(.3f, 9, 28), "#8a8a8e", true);
            Cube(ent, O + new Vector3(23f, 4.5f, -8f), new Vector3(.3f, 9, 12), "#8a8a8e", true);
            Cube(ent, O + new Vector3(23f, 4.5f, 8f), new Vector3(.3f, 9, 12), "#8a8a8e", true);
            Cube(ent, O + new Vector3(23f, 6.5f, 0), new Vector3(.3f, 5, 4), "#8a8a8e", true);
            // Lignes jaunes au sol
            foreach (float z in new[] { -2.2f, 2.2f, -7.8f, 7.8f })
                Cube(ent, O + new Vector3(0, .01f, z), new Vector3(40, .02f, .12f), "#d8b02a", false);
            // Racks : montants orange, lisses bleues, cartons et palettes filmées
            string[] cartonsCouleurs = { "#a07a4a", "#b08a58", "#8f6a3e" };
            int n = 0;
            foreach (float z in new[] { -10.5f, -5f, 5f, 10.5f })
            {
                var rang = new GameObject("Rack").transform;
                rang.SetParent(ent, false);
                Cube(rang, O + new Vector3(0, 2.6f, z), new Vector3(36, 5.2f, 1.2f), "#000000", true).GetComponent<MeshRenderer>().enabled = false;
                for (float x = -18f; x <= 18.01f; x += 3f)
                    foreach (float dz in new[] { -.55f, .55f })
                        Cube(rang, O + new Vector3(x, 2.6f, z + dz), new Vector3(.12f, 5.2f, .12f), "#d0562a", false);
                foreach (float y in new[] { .15f, 1.6f, 3.1f, 4.6f })
                    foreach (float dz in new[] { -.55f, .55f })
                        Cube(rang, O + new Vector3(0, y, z + dz), new Vector3(36, .12f, .1f), "#2a4a8a", false);
                for (float x = -16.5f; x <= 16.6f; x += 3f)
                    foreach (float y in new[] { .2f, 1.65f, 3.15f })
                    {
                        n++;
                        if (Monde.HashPublic(n, 3) < .25f) continue;
                        float h = .7f + Monde.HashPublic(n, 4) * .5f;
                        Cube(rang, O + new Vector3(x, y + h / 2f + .06f, z), new Vector3(2.4f, h, 1f), Monde.HashPublic(n, 5) < .3f ? "#d8d8dc" : cartonsCouleurs[n % 3], false);
                    }
            }
            // Les trois cartons du bon de préparation, tout au fond, dans trois allées différentes
            var cartons = new Transform[3];
            Vector3[] places = { new Vector3(16.6f, 1.94f, -5.35f), new Vector3(13.4f, .28f, 4.15f), new Vector3(17.8f, .28f, 9.85f) };
            float[] faces = { -1f, -1f, -1f };   // étiquette tournée vers l'allée
            for (int i = 0; i < 3; i++)
            {
                var c = Cube(ent, O + places[i], new Vector3(.7f, .55f, .5f), "#c8902a", true);
                c.name = "CartonAScanner" + (i + 1);
                var e = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(e.GetComponent<Collider>());
                e.name = "Etiquette";
                e.transform.SetParent(c, false);
                e.transform.localPosition = new Vector3(0, 0, faces[i] * .51f);
                e.transform.localScale = new Vector3(.45f, .35f, .02f);
                e.GetComponent<MeshRenderer>().sharedMaterial = Eclairant("EtiquetteCarton", new Color(1f, .85f, .2f));
                cartons[i] = c;
            }
            // Néons
            for (float x = -15f; x <= 15.1f; x += 10f)
                foreach (float z in new[] { -7.5f, 0f, 7.5f })
                {
                    Cube(ent, O + new Vector3(x, 8.7f, z), new Vector3(3f, .1f, .25f), "#ffffff", false).GetComponent<MeshRenderer>().sharedMaterial = Eclairant("Neon", new Color(.92f, .96f, 1f));
                    Lampe(ent, O + new Vector3(x, 8.3f, z), new Color(.9f, .95f, 1f), 4f, 16f);
                }
            // Sortie et pointeuse
            Texte3D(ent, "SORTIE", O + new Vector3(22.7f, 4.4f, 0), new Vector3(0, -90f, 0), .06f, new Color(.4f, 1f, .5f));
            Cube(ent, O + new Vector3(21.5f, 1.3f, -2.6f), new Vector3(.3f, .5f, .4f), "#3a3a42", true);
            p.departEntrepot = Repere(pro, "DepartEntrepot", O + new Vector3(-20f, .1f, 0), 90f);
            p.sortieEntrepot = Repere(pro, "SortieEntrepot", O + new Vector3(21.6f, 0, 0), 90f);
            p.cartons = cartons;

            // ---------- la rue sous la pluie ----------
            var rue = new GameObject("Rue").transform;
            rue.SetParent(pro, false);
            Vector3 R = O + new Vector3(0, 0, 90f);
            Cube(rue, R + new Vector3(0, -.1f, 0), new Vector3(110, .2f, 12), "#26262a", true);
            foreach (float s in new[] { -1f, 1f })
            {
                Cube(rue, R + new Vector3(0, .05f, s * 8.5f), new Vector3(110, .3f, 5), "#5e5e64", true);
                for (float x = -50f; x <= 50f; x += 9f)
                {
                    float h = 9f + Monde.HashPublic(x, s) * 10f;
                    Cube(rue, R + new Vector3(x, h / 2f, s * 15f), new Vector3(8.6f, h, 8f), Monde.HashPublic(s, x) < .5f ? "#3a3640" : "#4a4248", true);
                    for (int k = 0; k < 3; k++)
                        if (Monde.HashPublic(x + k, s * 7f) < .45f)
                            Cube(rue, R + new Vector3(x - 2.5f + k * 2.5f, 3f + (k % 2) * 3.5f, s * 10.95f), new Vector3(1.2f, 1.4f, .05f), "#ffffff", false)
                                .GetComponent<MeshRenderer>().sharedMaterial = Eclairant("FenetreVille", new Color(1f, .82f, .5f));
                }
                for (float x = -40f; x <= 40f; x += 20f)
                {
                    Cube(rue, R + new Vector3(x, 2.4f, s * 6.6f), new Vector3(.15f, 4.8f, .15f), "#2a2a2e", false);
                    Lampe(rue, R + new Vector3(x, 4.6f, s * 6f), new Color(1f, .78f, .45f), 3f, 14f);
                }
            }
            // Passage piéton et feu
            for (float z = -5f; z <= 5.01f; z += 1.1f) Cube(rue, R + new Vector3(0, .01f, z), new Vector3(3.2f, .02f, .55f), "#d8d8d8", false);
            Cube(rue, R + new Vector3(2.4f, 1.5f, -6.4f), new Vector3(.12f, 3f, .12f), "#2a2a2e", false);
            Cube(rue, R + new Vector3(2.4f, 2.9f, -6.4f), new Vector3(.3f, .3f, .2f), "#ffffff", false).GetComponent<MeshRenderer>().sharedMaterial = Eclairant("FeuVert", new Color(.3f, 1f, .45f));
            p.departRue = Repere(pro, "DepartRue", R + new Vector3(-20f, .3f, -8f), 90f);
            p.passage = Repere(pro, "Passage", R + new Vector3(0, 0, -.5f), 0f);
            // Le camion
            var cam = new GameObject("Camion").transform;
            cam.SetParent(rue, false);
            cam.position = R + new Vector3(-60f, 0, 2.2f);
            Cube(cam, cam.position + new Vector3(0, 2.1f, 0), new Vector3(7f, 3f, 2.5f), "#e8e8ea", false);
            Cube(cam, cam.position + new Vector3(0, 1.6f, -1.27f), new Vector3(7f, .5f, .02f), "#2a5ab0", false);
            Cube(cam, cam.position + new Vector3(4.4f, 1.6f, 0), new Vector3(1.9f, 2.4f, 2.4f), "#2a5ab0", false);
            Cube(cam, cam.position + new Vector3(5.1f, 2.1f, 0), new Vector3(.6f, 1f, 2.2f), "#1a1e28", false);
            foreach (float z in new[] { -.8f, .8f })
            {
                Cube(cam, cam.position + new Vector3(5.38f, 1f, z), new Vector3(.05f, .3f, .4f), "#ffffff", false).GetComponent<MeshRenderer>().sharedMaterial = Eclairant("Phare", new Color(1f, .97f, .85f));
                var phare = new GameObject("Phare").AddComponent<Light>();
                phare.transform.SetParent(cam, false);
                phare.transform.position = cam.position + new Vector3(5.5f, 1f, z);
                phare.transform.rotation = Quaternion.Euler(8f, 90f, 0);
                phare.type = LightType.Spot; phare.spotAngle = 50f; phare.range = 40f; phare.intensity = 6f; phare.color = new Color(1f, .96f, .85f);
            }
            foreach (float x in new[] { -2.5f, 2f, 4.2f })
                foreach (float z in new[] { -1.15f, 1.15f })
                    Cube(cam, cam.position + new Vector3(x, .5f, z), new Vector3(1f, 1f, .3f), "#1a1a1e", false);
            Texte3D(cam, "EXPRESS 24h — Livré à temps, toujours", cam.position + new Vector3(0, 2.6f, -1.28f), Vector3.zero, .05f, new Color(.15f, .3f, .7f));
            p.camion = cam;
            p.finCamion = Repere(pro, "FinCamion", R + new Vector3(70f, 0, 2.2f), 0);
            // La pluie
            var pluieGo = new GameObject("Pluie");
            pluieGo.transform.SetParent(rue, false);
            pluieGo.transform.position = R + Vector3.up * 8f;
            var ps = pluieGo.AddComponent<ParticleSystem>();
            var main = ps.main; main.startLifetime = 1.1f; main.startSpeed = 16f; main.startSize = .035f; main.maxParticles = 4000;
            main.startColor = new Color(.75f, .8f, .9f, .45f); main.simulationSpace = ParticleSystemSimulationSpace.World; main.playOnAwake = false;
            var em = ps.emission; em.rateOverTime = 2200f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(34f, .5f, 34f);
            pluieGo.transform.rotation = Quaternion.Euler(90f, 0, 0);
            var pr = pluieGo.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Stretch; pr.lengthScale = 6f;
            pr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-ParticleSystem.mat");
            var son = pluieGo.AddComponent<AudioSource>();
            son.playOnAwake = false; son.loop = true; son.volume = .5f; son.spatialBlend = 0f;
            p.pluie = ps;

            // ---------- l'Entre-Deux ----------
            var vide = new GameObject("EntreDeux").transform;
            vide.SetParent(pro, false);
            Vector3 E = O + new Vector3(0, 0, 220f);
            var blanc = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(blanc.GetComponent<Collider>());
            blanc.name = "VideBlanc";
            blanc.transform.SetParent(vide, false);
            blanc.transform.position = E + Vector3.up * 10f;
            blanc.transform.localScale = new Vector3(90f, 60f, 90f);
            blanc.GetComponent<MeshRenderer>().sharedMaterial = Eclairant("VideBlanc", new Color(.97f, .97f, .98f), true);
            blanc.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var sol = new GameObject("SolInvisible").AddComponent<BoxCollider>();
            sol.transform.SetParent(vide, false);
            sol.transform.position = E + Vector3.down * .5f;
            sol.size = new Vector3(80f, 1f, 80f);
            // Le bureau flottant et ses piles de parchemins
            Cube(vide, E + new Vector3(0, .88f, 6f), new Vector3(3.4f, .12f, 1.6f), "#6b4a2e", true);
            foreach (var (x, z) in new[] { (-1.5f, 5.35f), (1.5f, 5.35f), (-1.5f, 6.65f), (1.5f, 6.65f) })
                Cube(vide, E + new Vector3(x, .44f, z), new Vector3(.12f, .88f, .12f), "#5a3e26", false);
            for (int k = 0; k < 9; k++)
            {
                float x = -1.3f + (k % 5) * .55f, z = 5.6f + (k / 5) * .5f, h = .1f + Monde.HashPublic(k, 9) * .45f;
                Cube(vide, E + new Vector3(x, .95f + h / 2f, z), new Vector3(.4f, h, .3f), "#efe6cf", false, new Vector3(0, k * 13f, 0));
            }
            // Les orbes : trois ordinaires, le Sceau noir et or, la boîte de la Genèse
            (string nom, Color c, Vector3 pos)[] orbes =
            {
                ("OeilDesFlux", new Color(.35f, .6f, 1f), new Vector3(-1f, 1.8f, 5.6f)),
                ("Inventaire", new Color(.35f, .9f, .5f), new Vector3(0, 1.95f, 5.6f)),
                ("PlanDuBatisseur", new Color(1f, .62f, .25f), new Vector3(1f, 1.8f, 5.6f)),
                ("Sceau", new Color(.85f, .68f, .2f), new Vector3(1.9f, 1.15f, 6.3f)),
            };
            foreach (var (nom, c, pos) in orbes)
            {
                var o = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(o.GetComponent<Collider>());
                o.name = "Orbe_" + nom;
                o.transform.SetParent(vide, false);
                o.transform.position = E + pos;
                o.transform.localScale = Vector3.one * (nom == "Sceau" ? .32f : .38f);
                o.GetComponent<MeshRenderer>().sharedMaterial = Eclairant("Orbe" + nom, c);
                o.AddComponent<Flotter>();
            }
            var boite = Cube(vide, E + new Vector3(-1.9f, 1.15f, 6.3f), new Vector3(.5f, .4f, .4f), "#3a3a42", false);
            for (int k = 0; k < 3; k++) Cube(vide, E + new Vector3(-1.9f, 1.15f, 6.09f), new Vector3(.52f, .05f, .03f), "#8a8a92", false, new Vector3(0, 0, -30f + k * 30f));
            boite.name = "BoiteGenese";
            // Ysolde
            var ys = new GameObject("Ysolde");
            ys.transform.SetParent(vide, false);
            ys.transform.position = E + new Vector3(0, 0, 7.3f);
            ys.transform.rotation = Quaternion.Euler(0, 180f, 0);
            var hy = ys.AddComponent<Habitant>();
            hy.hauteurLibre = true; hy.promeneur = false; hy.taille = 1.08f;
            ColorUtility.TryParseHtmlString("#dcdce8", out hy.tunique);
            ColorUtility.TryParseHtmlString("#eef0f6", out hy.cheveux);
            var it = ys.AddComponent<Interlocuteur>();
            it.id = "ysolde"; it.nom = "Ysolde";
            p.ysolde = it;
            p.departEntreDeux = Repere(pro, "DepartEntreDeux", E + new Vector3(0, .1f, 1.5f), 0f);
        }
    }
}
