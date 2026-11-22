using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Aldermark.Editeur
{
    /// <summary>
    /// Menu « Aldermark > Ajouter le gameplay du pont » : ajoute dans la scène ouverte
    /// les indices du Sceau, Odon, Bram et le système de dialogues, sans toucher au décor.
    /// Relancer le menu remplace seulement l'objet « Gameplay ».
    /// </summary>
    public static partial class GameplayPont
    {
        [MenuItem("Aldermark/Ajouter le gameplay du pont")]
        public static void Ajouter()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var ancien = GameObject.Find("Gameplay");
            if (ancien != null) Object.DestroyImmediate(ancien);

            // Les étiquettes fixes du générateur sont remplacées par les points d'intérêt
            var vue = GameObject.Find("VueDuSceau");
            if (vue != null)
                foreach (var e in vue.GetComponentsInChildren<EtiquetteSceau>(true))
                    if (e.GetComponent<PointInteret>() == null &&
                        (e.texte.StartsWith("Pont") || e.texte.StartsWith("Four") || e.texte.StartsWith("Maison de Corvin")))
                        Object.DestroyImmediate(e.gameObject);

            var racine = new GameObject("Gameplay").transform;
            racine.gameObject.AddComponent<Dialogues>();
            var sons = racine.gameObject.AddComponent<Sons>();
            sons.positionRiviere = new Vector3(Monde.PontX, 0, 0);
            if (AssetDatabase.IsValidFolder("Assets/Aldermark/Audio"))
                sons.musiques = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Aldermark/Audio" })
                    .Select(AssetDatabase.GUIDToAssetPath).OrderBy(c => c)
                    .Select(AssetDatabase.LoadAssetAtPath<AudioClip>).Where(c => c != null).ToArray();

            float BX = Monde.PontX;
            var K = Monde.Four;
            var C = Monde.Corvin;
            float hK = Sol(K.x, K.y);

            Indice(racine, "Pont", new Vector3(BX, 6f, 0),
                "Flux rompu", "Pont de Varenne — flux rompu", "#ff4d4d",
                "La ligne ambrée des charrettes s'arrête net au milieu de la rivière, rouge comme une plaie. Le pont de Varenne coupe la route du Comte.",
                "vu_pont", "", 0f, 0, 35f, 6f, "pont");
            Indice(racine, "Four", new Vector3(K.x, hK + 4.5f, K.y),
                "Four à chaux", "Four à chaux — inactif. Remise en état possible.", "#ffbf4a",
                "Un vieux four, éteint depuis des années. La voûte du foyer est fendue, la cheminée s'est effondrée à l'intérieur, la grille est rouillée et tordue. Remis en état, il donnerait la chaux du mortier.",
                "vu_four", "", .5f, 0, 20f, 4f, "four");
            Indice(racine, "Calcaire", new Vector3(K.x - 12f, Sol(K.x - 12f, K.y + 6f) + 1.5f, K.y + 6f),
                "Calcaire déjà extrait", "Calcaire — prêt à cuire", "#ffbf4a",
                "Un tas de pierres blanches couvertes de mousse, sur le talus : du calcaire déjà extrait, mais jamais cuit.",
                "vu_calcaire", "", 0f, 0, 15f, 3f, "four");
            Indice(racine, "BlocsRiviere", new Vector3(BX + 1f, .3f, 3f),
                "Blocs du pont", "Blocs du pont — 31 récupérables", "#ffbf4a",
                "Sous l'eau, les pierres taillées du pont effondré. Une trentaine sont intactes et récupérables.",
                "vu_blocs", "", 0f, 0, 20f, 4f, "pont");
            Vector2 versVillage = (Monde.Village - K).normalized;
            Vector2 foyer = K + versVillage * 4.2f;
            Indice(racine, "Foyer", new Vector3(foyer.x, Sol(foyer.x, foyer.y) + .6f, foyer.y),
                "Cendres soufrées", "Foyer — cendres récentes, soufre", "#ff4d4d",
                "Au milieu des vieilles cendres grises : des cendres noires, récentes, qui piquent le nez. Du soufre. Et dans la terre battue, des empreintes de bottes qui ne sont pas celles des villageois. Quelqu'un utilise ce four la nuit.",
                "vu_cendres", "vu_four", 1f, 1, 6f, 2f, "four");
            var milieu = Vector2.Lerp(K, C, .5f);
            Indice(racine, "PisteCorvin", new Vector3(milieu.x, Sol(milieu.x, milieu.y) + 2f, milieu.y),
                "Flux récent : four → Corvin", "Flux récent — vers la maison de Corvin", "#fff0c0",
                "Une ligne fraîche, presque vive, part du four, longe la rivière… et file vers la maison de Corvin, le passeur.",
                "vu_corvin", "bram", .5f, 0, 25f, 8f, "corvin");

            // Odon et Bram près du puits ; Lise devient une interlocutrice
            var V = Monde.Village;
            Personnage(racine, "Odon", "odon", new Vector2(V.x + 3f, V.y - 2.5f), "#7a6a58", "#c8c8c8", 1.05f);
            Personnage(racine, "Bram", "bram", new Vector2(V.x + 6f, V.y + 1.5f), "#6a5a3a", "#3a2a1e", 1f);
            var lise = GameObject.Find("Lise");
            if (lise != null && lise.GetComponent<Interlocuteur>() == null)
            {
                var il = lise.AddComponent<Interlocuteur>();
                il.id = "lise"; il.nom = "Lise";
            }

            // Les indices existent vraiment dans le décor
            var decor = new GameObject("DecorDesIndices").transform;
            decor.SetParent(racine, false);
            var calc = new Vector2(K.x - 12f, K.y + 6f);
            for (int i = 0; i < 11; i++)
            {
                float a = i * 2.4f, r = Monde.HashPublic(i, 21) * 1.8f;
                var q = calc + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
                float t = .5f + Monde.HashPublic(i, 22) * .7f;
                Piece(decor, PrimitiveType.Sphere, new Vector3(q.x, Sol(q.x, q.y) + t * .25f, q.y), new Vector3(t * 1.3f, t * .7f, t), new Vector3(0, i * 40f, 0), i % 4 == 0 ? "#8a9a52" : "#d9d4c6");
            }
            Piece(decor, PrimitiveType.Cylinder, new Vector3(foyer.x, Sol(foyer.x, foyer.y) + .02f, foyer.y), new Vector3(2.4f, .02f, 2.4f), Vector3.zero, "#262222");
            Vector2 versCorvin = (C - foyer).normalized, cote = new Vector2(-versCorvin.y, versCorvin.x);
            for (int i = 0; i < 9; i++)
            {
                var q = foyer + versCorvin * (1.4f + i * .75f) + cote * (i % 2 == 0 ? .18f : -.18f);
                float rot = Mathf.Atan2(versCorvin.x, versCorvin.y) * Mathf.Rad2Deg;
                Piece(decor, PrimitiveType.Cube, new Vector3(q.x, Sol(q.x, q.y) + .03f, q.y), new Vector3(.14f, .03f, .32f), new Vector3(0, rot, 0), "#3a2a20");
            }

            // Départ sur le chemin, face à Lise
            var depart = new Vector2(117.4f, -60.7f);
            var lisePos = lise != null ? new Vector2(lise.transform.position.x, lise.transform.position.z) : new Vector2(106.8f, -48.4f);
            var aldric = GameObject.Find("Aldric");
            if (aldric != null)
            {
                aldric.transform.position = new Vector3(depart.x, Sol(depart.x, depart.y) + .2f, depart.y);
                aldric.transform.rotation = Quaternion.LookRotation(new Vector3(lisePos.x - depart.x, 0, lisePos.y - depart.y));
            }
            if (lise != null)
                lise.transform.rotation = Quaternion.LookRotation(new Vector3(depart.x - lisePos.x, 0, depart.y - lisePos.y));

            ConstruireChantier(racine);
            Attelages(racine);
            ConstruireVillage(racine);
            ConstruireConvoi(racine);
            ConstruirePrologue(racine);
            ConstruireIlseEtNoeud(racine);
            ModelesPersonnages.BrancherAldric();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Aldermark : gameplay du pont ajouté (6 indices, Odon, Bram, dialogues). Appuie sur Play.");
        }

        static float Sol(float x, float z) => Mathf.Max(Monde.Hauteur(x, z), -1.2f);

        static Material Mat(string hex)
        {
            string chemin = $"Assets/Aldermark/Genere/Materiaux/Gameplay_{hex.TrimStart('#')}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(chemin);
            if (m != null) return m;
            if (!AssetDatabase.IsValidFolder("Assets/Aldermark/Genere")) AssetDatabase.CreateFolder("Assets/Aldermark", "Genere");
            if (!AssetDatabase.IsValidFolder("Assets/Aldermark/Genere/Materiaux")) AssetDatabase.CreateFolder("Assets/Aldermark/Genere", "Materiaux");
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            ColorUtility.TryParseHtmlString(hex, out var c);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", .05f);
            AssetDatabase.CreateAsset(m, chemin);
            return m;
        }

        static void Piece(Transform parent, PrimitiveType type, Vector3 pos, Vector3 echelle, Vector3 rot, string hex)
        {
            var g = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.position = pos;
            g.transform.rotation = Quaternion.Euler(rot);
            g.transform.localScale = echelle;
            g.GetComponent<MeshRenderer>().sharedMaterial = Mat(hex);
        }

        static void Indice(Transform parent, string nom, Vector3 pos, string titre, string etiquette, string couleur,
            string description, string drapeau, string requis, float eveil, int confiance, float portee, float rayon, string cible)
        {
            var go = new GameObject("Indice_" + nom);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var e = go.AddComponent<EtiquetteSceau>();
            e.texte = "";
            ColorUtility.TryParseHtmlString(couleur, out e.couleur);
            var p = go.AddComponent<PointInteret>();
            p.titre = titre; p.etiquette = etiquette; p.description = description;
            p.drapeau = drapeau; p.requis = requis; p.eveil = eveil; p.confiance = confiance;
            p.porteeMax = portee; p.rayon = rayon; p.cible = cible;
        }

        // ---------- chantier : postes, villageois, pont provisoire ----------

        static void ConstruireChantier(Transform racine)
        {
            var ch = new GameObject("Chantier").transform;
            ch.SetParent(racine, false);
            var chantier = ch.gameObject.AddComponent<Chantier>();
            ch.gameObject.AddComponent<PlanRegistre>().chantier = chantier;
            ch.gameObject.AddComponent<NuitDesSonnailles>();

            // Le pont provisoire, en aval des ruines, là où ses deux abords sont libres (pas de maison)
            Physics.SyncTransforms();
            float zp = -9f;
            foreach (float z in new[] { -9f, -13f, -17f, -21f, -25f, -6f })
                if (AbordsLibres(z)) { zp = z; break; }
            float cx = Monde.Riviere(zp);
            var K = Monde.Four;
            Vector2 versVillage = (Monde.Village - K).normalized;

            // Postes (positions à environ la distance prévue par la spec, décalées si une maison gêne)
            Vector2 pc = PlaceLibre(new Vector2(cx + 21f, zp - 4f), 4.5f);
            Vector2 pForet = PlaceLibre(new Vector2(40f, 74f), 4f), pCarriere = PlaceLibre(new Vector2(95f, -108f), 4f);
            CreerPoste(ch, "chantier", "Chantier du pont", Ressource.Aucune, 0, 8, -1, "plan_bois", pc);
            CreerPoste(ch, "riviere", "Rivière", Ressource.Pierre, 6, 2, 31, "vu_blocs", PlaceLibre(new Vector2(Monde.PontX + 10f, 7f), 3f));
            CreerPoste(ch, "carriere", "Carrière", Ressource.Pierre, 2, 4, -1, "", pCarriere);
            CreerPoste(ch, "foret", "Forêt", Ressource.Bois, 10, 4, -1, "", pForet);
            var talus = CreerPoste(ch, "talus", "Talus", Ressource.Calcaire, 4, 2, 40, "vu_calcaire", PlaceLibre(new Vector2(K.x - 14f, K.y + 9f), 3f));
            talus.ressourceDepart = Ressource.Calcaire; talus.quantiteDepart = 6;   // le calcaire déjà extrait, jamais cuit
            var four = CreerPoste(ch, "four", "Four", Ressource.Aucune, 0, 2, -1, "vu_four", PlaceLibre(K + versVillage * 7f, 2.5f));
            four.ressourceDepart = Ressource.Bois; four.quantiteDepart = 4;          // du bois mort entassé contre le four

            // Décor des postes
            DecorForet(pForet, ch);
            DecorCarriere(pCarriere, ch);
            for (int i = 0; i < 2; i++)   // tréteaux du chantier
            {
                var q = pc + new Vector2(-1.5f + i * 3f, 1.5f);
                Piece(ch, PrimitiveType.Cube, new Vector3(q.x, Sol(q.x, q.y) + .8f, q.y), new Vector3(2.4f, .12f, .6f), Vector3.zero, "#7a5636");
                Piece(ch, PrimitiveType.Cube, new Vector3(q.x - 1f, Sol(q.x, q.y) + .4f, q.y), new Vector3(.12f, .8f, .5f), Vector3.zero, "#5a3e26");
                Piece(ch, PrimitiveType.Cube, new Vector3(q.x + 1f, Sol(q.x, q.y) + .4f, q.y), new Vector3(.12f, .8f, .5f), Vector3.zero, "#5a3e26");
            }

            // Pont provisoire : piles, poutres maîtresses, tablier (révélés au fil des jours)
            var pont = new GameObject("PontProvisoire").transform;
            pont.SetParent(ch, false);
            chantier.etape1 = Groupe(pont, "Etape1_Piles");
            chantier.etape2 = Groupe(pont, "Etape2_Poutres");
            chantier.etape3 = Groupe(pont, "Etape3_Tablier");
            const float Y = 1.7f, L = 32f;
            foreach (float dx in new[] { -8f, 0f, 8f })
                foreach (float dz in new[] { -1.8f, 1.8f })
                {
                    float x = cx + dx, z = zp + dz, fond = Mathf.Max(Monde.Hauteur(x, z), -3.6f);
                    Piece(chantier.etape1, PrimitiveType.Cube, new Vector3(x, fond + .5f, z), new Vector3(1.4f, 1.2f, 1.4f), new Vector3(0, 8f * dx, 0), "#9a948a");
                    // Joint de mortier (la chaux) au sommet du socle
                    Piece(chantier.etape1, PrimitiveType.Cube, new Vector3(x, fond + 1.14f, z), new Vector3(1.5f, .1f, 1.5f), new Vector3(0, 8f * dx, 0), "#ece6d6");
                    float h = Y - fond;
                    Piece(chantier.etape1, PrimitiveType.Cylinder, new Vector3(x, fond + h / 2f, z), new Vector3(.45f, h / 2f, .45f), Vector3.zero, "#6b4a2e");
                }
            foreach (float dz in new[] { -1.6f, 1.6f })
                for (int k = 0; k < 4; k++)
                    Piece(chantier.etape2, PrimitiveType.Cube, new Vector3(cx - L / 2f + L / 8f + k * L / 4f, Y - .15f, zp + dz), new Vector3(L / 4f + .05f, .45f, .5f), Vector3.zero, "#7a5636");
            int planches = 26;
            for (int k = 0; k < planches; k++)
            {
                float x = cx - L / 2f + (k + .5f) * L / planches;
                Piece(chantier.etape3, PrimitiveType.Cube, new Vector3(x, Y + .14f, zp), new Vector3(L / planches - .06f, .12f, 4.4f), new Vector3(0, (Monde.HashPublic(k, 3) - .5f) * 3f, 0), k % 3 == 0 ? "#8a6440" : "#9a7048");
                if (k % 3 == 0)
                    foreach (float dz in new[] { -2.1f, 2.1f })
                        Piece(chantier.etape3, PrimitiveType.Cube, new Vector3(x, Y + .75f, zp + dz), new Vector3(.12f, 1.1f, .12f), Vector3.zero, "#5a3e26");
            }
            foreach (float dz in new[] { -2.1f, 2.1f })
                Piece(chantier.etape3, PrimitiveType.Cube, new Vector3(cx, Y + 1.25f, zp + dz), new Vector3(L, .1f, .1f), Vector3.zero, "#6b4a2e");
            var tab = new GameObject("TablierCollision").AddComponent<BoxCollider>();
            tab.transform.SetParent(pont, false);
            tab.transform.position = new Vector3(cx, Y + .1f, zp);
            tab.size = new Vector3(L + 2f, .3f, 4.4f);
            tab.enabled = false;
            chantier.tablier = tab;
            chantier.centrePont = new Vector3(cx, Y + .1f, zp);
            chantier.longueurPont = L;

            // Paillasse chez Odon : la maison du village la plus proche de lui
            Paillasse(ch);

            // Villageois : Odon et Bram, puis Joss, Pell, Marthe ; trois de plus quand le four est relancé
            var V = Monde.Village;
            CreerOuvrier(GameObject.Find("Odon"), "Odon", "pont_accepte", false, 2f);
            CreerOuvrier(GameObject.Find("Bram"), "Bram", "bram", true, 1f);
            CreerOuvrier(Personnage(racine, "Joss", "joss", new Vector2(V.x - 4f, V.y + 5f), "#5b6e8c", "#3a2a1e", 1f), "Joss", "pont_accepte", true, 1f);
            CreerOuvrier(Personnage(racine, "Pell", "pell", new Vector2(V.x + 7f, V.y - 6f), "#7a5a3c", "#c8a060", .95f), "Pell", "pont_accepte", true, 1f);
            CreerOuvrier(Personnage(racine, "Marthe", "marthe", new Vector2(V.x - 2f, V.y - 7f), "#8c5b6e", "#4a3020", .92f), "Marthe", "pont_accepte", true, 1f);
            CreerOuvrier(Personnage(racine, "Agathe", "agathe", new Vector2(V.x - 8f, V.y - 2f), "#6e8c5b", "#2a2020", .9f), "Agathe", "four_repare", true, 1f);
            CreerOuvrier(Personnage(racine, "Colin", "colin", new Vector2(V.x + 9f, V.y + 4f), "#8c7a5b", "#5a3a20", 1.02f), "Colin", "four_repare", true, 1f);
            CreerOuvrier(Personnage(racine, "Firmin", "firmin", new Vector2(V.x + 2f, V.y + 9f), "#5b5b8c", "#7a7a7a", 1f), "Firmin", "four_repare", true, 1f);
        }

        /// <summary>Vrai si rien de solide (maison, rocher, arbre…) ne se trouve dans ce rayon, au-dessus du sol.</summary>
        static bool Libre(Vector2 p, float rayon)
        {
            var centre = new Vector3(p.x, Sol(p.x, p.y) + 1.6f, p.y);
            foreach (var c in Physics.OverlapSphere(centre, rayon, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c is TerrainCollider) continue;
                var g = c.transform.root.name;
                if (g == "Gameplay" || g == "Aldric") continue;
                return false;
            }
            return true;
        }

        /// <summary>Le point voulu s'il est libre, sinon le plus proche point libre autour.</summary>
        static Vector2 PlaceLibre(Vector2 voulu, float rayon)
        {
            Physics.SyncTransforms();
            if (Libre(voulu, rayon)) return voulu;
            for (int anneau = 1; anneau <= 14; anneau++)
                for (int k = 0; k < 12; k++)
                {
                    float a = k * Mathf.PI / 6f + anneau * .4f;
                    var q = voulu + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * anneau * 2f;
                    if (Monde.Hauteur(q.x, q.y) > -.2f && Libre(q, rayon)) return q;
                }
            Debug.LogWarning($"Aldermark : pas de place libre trouvée près de {voulu}.");
            return voulu;
        }

        /// <summary>Les deux abords du pont provisoire sont-ils libres de maisons ?</summary>
        static bool AbordsLibres(float z)
        {
            float cx = Monde.Riviere(z);
            for (float d = 12f; d <= 27f; d += 3f)
                if (!Libre(new Vector2(cx + d, z), 3f) || !Libre(new Vector2(cx - d, z), 3f)) return false;
            return Libre(new Vector2(cx + 21f, z - 4f), 4.5f);
        }

        /// <summary>
        /// Un bœuf attelé devant chaque charrette du Comte (modèle « boeuf », « ox », « cheval »
        /// ou « horse » dans Models s'il existe, sinon un bœuf en formes simples).
        /// </summary>
        static void Attelages(Transform racine)
        {
            var groupe = new GameObject("Attelages").transform;
            groupe.SetParent(racine, false);
            var modele = TrouverModele("boeuf", "bœuf", "ox", "cheval", "horse");
            int n = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.name != "Charrette") continue;
                n++;
                var a = new GameObject("Attelage" + n).transform;
                a.SetParent(groupe, false);
                Vector3 devant = t.TransformPoint(new Vector3(4.6f, 0, 0));
                a.position = new Vector3(devant.x, Sol(devant.x, devant.z), devant.z);
                a.rotation = Quaternion.LookRotation(t.right, Vector3.up);   // la charrette avance vers +x
                // Brancards entre la charrette et l'animal
                foreach (float cote in new[] { -.55f, .55f })
                {
                    Vector3 p0 = t.TransformPoint(new Vector3(2f, 1.05f, cote)), p1 = t.TransformPoint(new Vector3(4.2f, 1.25f, cote * .9f));
                    var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Object.DestroyImmediate(b.GetComponent<Collider>());
                    b.name = "Brancard";
                    b.transform.SetParent(a, true);
                    b.transform.position = (p0 + p1) / 2f;
                    b.transform.rotation = Quaternion.LookRotation(p1 - p0);
                    b.transform.localScale = new Vector3(.09f, .09f, Vector3.Distance(p0, p1));
                    b.GetComponent<MeshRenderer>().sharedMaterial = Mat("#5a3e26");
                }
                if (modele != null)
                {
                    var g = (GameObject)PrefabUtility.InstantiatePrefab(modele, a);
                    g.transform.localPosition = Vector3.zero;
                    g.transform.localRotation = modele.transform.localRotation;
                    var rs = g.GetComponentsInChildren<Renderer>();
                    if (rs.Length > 0)
                    {
                        var bo = rs[0].bounds; foreach (var r in rs) bo.Encapsulate(r.bounds);
                        if (bo.size.y > 1e-4f) g.transform.localScale *= 1.6f / bo.size.y;
                    }
                    continue;
                }
                Boeuf(a, n % 2 == 0 ? "#8a6a4a" : "#d8cfc0");
            }
            if (n == 0) Debug.Log("Aldermark : aucune charrette trouvée dans la scène.");
        }

        static void ConstruireConvoi(Transform racine)
        {
            var charrettes = new System.Collections.Generic.List<Transform>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)) if (t.name == "Charrette") charrettes.Add(t);
            charrettes.Sort((a, b) => b.position.x.CompareTo(a.position.x));   // la plus proche du pont part en premier
            var attelages = new Transform[charrettes.Count];
            var groupe = racine.Find("Attelages");
            for (int i = 0; i < charrettes.Count && groupe != null; i++)
            {
                float best = float.MaxValue;
                foreach (Transform a in groupe)
                {
                    float d = Vector3.Distance(a.position, charrettes[i].TransformPoint(new Vector3(4.6f, 0, 0)));
                    if (d < best) { best = d; attelages[i] = a; }
                }
            }
            var charretiers = new System.Collections.Generic.List<Habitant>();
            foreach (var h in Object.FindObjectsByType<Habitant>(FindObjectsSortMode.None)) if (h.name.StartsWith("Charretier")) charretiers.Add(h);
            charretiers.Sort((a, b) => b.transform.position.x.CompareTo(a.transform.position.x));
            var c = racine.gameObject.AddComponent<Convoi>();
            c.charrettes = charrettes.ToArray();
            c.attelages = attelages;
            c.charretiers = charretiers.ToArray();
            c.chantier = Object.FindFirstObjectByType<Chantier>();
            var marche = racine.Find("VillageVivant/Marche");
            c.arrivee = marche;
        }

        static void Boeuf(Transform a, string robe)
        {
            void P(PrimitiveType t, Vector3 pos, Vector3 taille, string hex, Vector3 rot = default)
            {
                var g = GameObject.CreatePrimitive(t);
                Object.DestroyImmediate(g.GetComponent<Collider>());
                g.transform.SetParent(a, false);
                g.transform.localPosition = pos;
                g.transform.localRotation = Quaternion.Euler(rot);
                g.transform.localScale = taille;
                g.GetComponent<MeshRenderer>().sharedMaterial = Mat(hex);
            }
            P(PrimitiveType.Cube, new Vector3(0, 1.15f, -.2f), new Vector3(.95f, .85f, 2.1f), robe);                 // corps
            P(PrimitiveType.Cube, new Vector3(0, 1.25f, 1.05f), new Vector3(.55f, .55f, .7f), robe, new Vector3(25, 0, 0)); // cou et tête
            P(PrimitiveType.Cube, new Vector3(0, 1.0f, 1.45f), new Vector3(.42f, .36f, .3f), "#c9a98a");             // museau
            P(PrimitiveType.Cube, new Vector3(-.36f, 1.55f, 1.2f), new Vector3(.4f, .07f, .07f), "#e8e0cc", new Vector3(0, 0, -20)); // cornes
            P(PrimitiveType.Cube, new Vector3(.36f, 1.55f, 1.2f), new Vector3(.4f, .07f, .07f), "#e8e0cc", new Vector3(0, 0, 20));
            P(PrimitiveType.Cube, new Vector3(0, 1.62f, .85f), new Vector3(1.1f, .12f, .14f), "#5a3e26");            // joug
            foreach (var (x, z) in new[] { (-.32f, .6f), (.32f, .6f), (-.32f, -1f), (.32f, -1f) })
                P(PrimitiveType.Cube, new Vector3(x, .38f, z), new Vector3(.2f, .76f, .2f), robe);                 // pattes
            P(PrimitiveType.Cube, new Vector3(0, 1.1f, -1.3f), new Vector3(.06f, .7f, .06f), robe, new Vector3(-15, 0, 0)); // queue
        }

        static GameObject TrouverModele(params string[] motsCles)
        {
            foreach (var d in new[] { "Assets/Aldermark/Models", "Assets/Aldermark/Modeles" })
            {
                if (!AssetDatabase.IsValidFolder(d)) continue;
                foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { d }))
                {
                    string c = AssetDatabase.GUIDToAssetPath(guid);
                    string nom = System.IO.Path.GetFileNameWithoutExtension(c).ToLowerInvariant();
                    if (motsCles.Any(m => nom.Contains(m))) return AssetDatabase.LoadAssetAtPath<GameObject>(c);
                }
            }
            return null;
        }

        // ---------- village vivant ----------

        static Mesh toit;

        static Transform Batiment(Transform parent, string nom, Vector2 pos, Vector2 versCentre, Vector3 taille, string murs, string toiture, float hautToit)
        {
            var b = new GameObject(nom).transform;
            b.SetParent(parent, false);
            b.position = new Vector3(pos.x, Sol(pos.x, pos.y), pos.y);
            b.rotation = Quaternion.LookRotation(new Vector3(versCentre.x, 0, versCentre.y));
            Local(b, PrimitiveType.Cube, new Vector3(0, taille.y / 2f - .3f, 0), taille, murs, true);
            if (toit == null) toit = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Aldermark/Genere/Formes/Toit.asset");
            var t = new GameObject("Toit");
            t.transform.SetParent(b, false);
            t.transform.localPosition = new Vector3(0, taille.y - .3f, 0);
            t.transform.localScale = new Vector3(taille.x + .8f, hautToit, taille.z + .8f);
            if (toit != null) { t.AddComponent<MeshFilter>().sharedMesh = toit; t.AddComponent<MeshRenderer>().sharedMaterial = Mat(toiture); }
            // Porte
            Local(b, PrimitiveType.Cube, new Vector3(0, .8f, taille.z / 2f + .02f), new Vector3(1.1f, 1.9f, .08f), "#4a3020", false);
            return b;
        }

        /// <summary>Pièce en coordonnées locales du parent (avec collision si demandé).</summary>
        static Transform Local(Transform parent, PrimitiveType type, Vector3 pos, Vector3 echelle, string hex, bool collision, Vector3 rot = default)
        {
            var g = GameObject.CreatePrimitive(type);
            if (!collision) Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(rot);
            g.transform.localScale = echelle;
            g.GetComponent<MeshRenderer>().sharedMaterial = Mat(hex);
            return g.transform;
        }

        static GameObject Fenetre(Transform b, Vector3 pos)
        {
            var f = Local(b, PrimitiveType.Cube, pos, new Vector3(.8f, .7f, .06f), "#2a2430", false);
            var lum = Local(b, PrimitiveType.Cube, pos + new Vector3(0, 0, .01f), new Vector3(.7f, .6f, .06f), "#ffcf7a", false);
            string chemin = "Assets/Aldermark/Genere/Materiaux/Gameplay_FenetreAllumee.mat";
            var ex = AssetDatabase.LoadAssetAtPath<Material>(chemin);
            if (ex == null)
            {
                ex = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                ex.SetColor("_BaseColor", new Color(1f, .78f, .4f));
                AssetDatabase.CreateAsset(ex, chemin);
            }
            lum.GetComponent<MeshRenderer>().sharedMaterial = ex;
            return lum.gameObject;
        }

        static void ConstruireVillage(Transform racine)
        {
            var v = new GameObject("VillageVivant").transform;
            v.SetParent(racine, false);
            var vie = v.gameObject.AddComponent<VillageVivant>();
            var V = Monde.Village;
            var lumieres = new System.Collections.Generic.List<GameObject>();
            Vector2 Vers(Vector2 p) => (V - p).sqrMagnitude > .1f ? (V - p).normalized : Vector2.up;

            // Marché : trois étals au cœur du village
            var marche = new GameObject("Marche").transform;
            marche.SetParent(v, false);
            Vector2 pm = PlaceLibre(V + new Vector2(4f, 8f), 6f);
            marche.position = new Vector3(pm.x, Sol(pm.x, pm.y), pm.y);
            vie.marche = marche;
            string[] toiles = { "#b4523a", "#3f5a8c", "#c9a23a" };
            var lots = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                float a = i * 2.1f;
                var e = new GameObject("Etal" + (i + 1)).transform;
                e.SetParent(marche, false);
                e.localPosition = new Vector3(Mathf.Cos(a) * 4f, 0, Mathf.Sin(a) * 4f);
                e.localRotation = Quaternion.LookRotation(-e.localPosition);
                Local(e, PrimitiveType.Cube, new Vector3(0, .8f, 0), new Vector3(2.4f, .1f, 1.1f), "#7a5636", true);
                foreach (float x in new[] { -1.1f, 1.1f })
                {
                    Local(e, PrimitiveType.Cube, new Vector3(x, .4f, .45f), new Vector3(.1f, .8f, .1f), "#5a3e26", false);
                    Local(e, PrimitiveType.Cube, new Vector3(x, 1.1f, -.45f), new Vector3(.1f, 2.2f, .1f), "#5a3e26", false);
                }
                Local(e, PrimitiveType.Cube, new Vector3(0, 2.2f, 0), new Vector3(2.7f, .06f, 1.5f), toiles[i], false, new Vector3(-14f, 0, 0));
                var lot = new GameObject("Marchandises").transform;
                lot.SetParent(e, false);
                string[] couleurs = { "#d8b04a", "#9a3a2a", "#6a8a3a", "#e0d0a0", "#b06a2a" };
                for (int k = 0; k < 7; k++)
                    Local(lot, k % 2 == 0 ? PrimitiveType.Sphere : PrimitiveType.Cube, new Vector3(-.9f + k * .3f, .98f, Monde.HashPublic(i, k) * .4f - .2f), Vector3.one * (.22f + Monde.HashPublic(k, i) * .12f), couleurs[(k + i) % 5], false);
                lots[i] = lot;
            }
            vie.marchandises = lots;

            // Fanions entre les étals, pour l'ouverture du pont
            var fan = new GameObject("Fanions").transform;
            fan.SetParent(marche, false);
            for (int k = 0; k < 18; k++)
            {
                float a = k / 18f * Mathf.PI * 2f;
                Local(fan, PrimitiveType.Cube, new Vector3(Mathf.Cos(a) * 5f, 3.2f + Mathf.Sin(k) * .1f, Mathf.Sin(a) * 5f), new Vector3(.3f, .35f, .02f), toiles[k % 3], false, new Vector3(0, -a * Mathf.Rad2Deg, 0));
            }
            vie.fanions = fan.gameObject;

            // Auberge (avec enseigne et fenêtres qui s'allument la nuit)
            Vector2 pa = PlaceLibre(V + new Vector2(15f, -9f), 6f);
            var aub = Batiment(v, "Auberge", pa, Vers(pa), new Vector3(8f, 4.2f, 6f), "#d9c9a8", "#7a4a33", 3f);
            Local(aub, PrimitiveType.Cube, new Vector3(3.4f, 2.4f, 3.6f), new Vector3(.08f, 2.6f, .08f), "#5a3e26", false);
            Local(aub, PrimitiveType.Cube, new Vector3(3.4f, 3.2f, 4.1f), new Vector3(.06f, .6f, 1f), "#8a6440", false);
            lumieres.Add(Fenetre(aub, new Vector3(-2.2f, 1.9f, 3.04f)));
            lumieres.Add(Fenetre(aub, new Vector3(2f, 1.9f, 3.04f)));
            foreach (float x in new[] { -2.5f, 2.5f })
                Local(aub, PrimitiveType.Cube, new Vector3(x, .35f, 4.4f), new Vector3(1.8f, .12f, .45f), "#7a5636", true);
            vie.auberge = aub;

            // Forge : un appentis ouvert, l'enclume et le foyer
            Vector2 pf = PlaceLibre(V + new Vector2(18f, 13f), 5f);
            var forge = new GameObject("Forge").transform;
            forge.SetParent(v, false);
            forge.position = new Vector3(pf.x, Sol(pf.x, pf.y), pf.y);
            forge.rotation = Quaternion.LookRotation(new Vector3(Vers(pf).x, 0, Vers(pf).y));
            foreach (var (x, z) in new[] { (-2f, -1.5f), (2f, -1.5f), (-2f, 1.5f), (2f, 1.5f) })
                Local(forge, PrimitiveType.Cube, new Vector3(x, 1.4f, z), new Vector3(.18f, 2.8f, .18f), "#5a3e26", true);
            Local(forge, PrimitiveType.Cube, new Vector3(0, 2.9f, 0), new Vector3(4.8f, .12f, 3.8f), "#6b4a2e", false, new Vector3(-10f, 0, 0));
            Local(forge, PrimitiveType.Cube, new Vector3(-1f, .6f, -.9f), new Vector3(1.4f, 1.2f, 1f), "#6d6670", true);
            var braise = Local(forge, PrimitiveType.Cube, new Vector3(-1f, 1.22f, -.9f), new Vector3(.9f, .05f, .6f), "#ff7a2a", false);
            var mb = new Material(Shader.Find("Universal Render Pipeline/Unlit")); mb.SetColor("_BaseColor", new Color(1f, .45f, .15f));
            string cmb = "Assets/Aldermark/Genere/Materiaux/Gameplay_Braise.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(cmb) == null) AssetDatabase.CreateAsset(mb, cmb);
            braise.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(cmb);
            Local(forge, PrimitiveType.Cube, new Vector3(.9f, .45f, .2f), new Vector3(.6f, .9f, .4f), "#4a3626", true);
            Local(forge, PrimitiveType.Cube, new Vector3(.9f, .95f, .2f), new Vector3(.8f, .2f, .35f), "#3a3a42", false);
            var fumee = new GameObject("Fumee").AddComponent<ParticleSystem>();
            fumee.transform.SetParent(forge, false);
            fumee.transform.localPosition = new Vector3(-1f, 3.2f, -.9f);
            var pm2 = fumee.main; pm2.startLifetime = 4f; pm2.startSpeed = .8f; pm2.startSize = 1.2f; pm2.startColor = new Color(.75f, .75f, .78f, .5f); pm2.playOnAwake = false;
            var em = fumee.emission; em.rateOverTime = 3f;
            var sh = fumee.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 8f; sh.radius = .2f;
            fumee.GetComponent<ParticleSystemRenderer>().sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-ParticleSystem.mat");
            vie.fumeeForge = fumee;
            var forgeron = Personnage(v, "Forgeron", "forgeron", pf + Vers(pf) * 1.2f, "#4a3a2a", "#2a2020", 1.05f);
            forgeron.GetComponent<Interlocuteur>().nom = "Hadrien, le forgeron";
            vie.forgeron = forgeron.GetComponent<Habitant>();
            vie.enclume = forge;

            // Chapelle et son clocher
            Vector2 pc = PlaceLibre(V + new Vector2(-22f, 16f), 7f);
            var chap = Batiment(v, "Chapelle", pc, Vers(pc), new Vector3(5.5f, 5f, 9f), "#cfc6b4", "#5a5a66", 3.5f);
            Local(chap, PrimitiveType.Cube, new Vector3(0, 4.5f, -4.4f), new Vector3(2.4f, 9f, 2.4f), "#c4bba8", true);
            Local(chap, PrimitiveType.Cube, new Vector3(0, 9.4f, -4.4f), new Vector3(1.6f, 1.6f, 1.6f), "#5a5a66", false, new Vector3(0, 45f, 0));
            Local(chap, PrimitiveType.Cube, new Vector3(0, 7.4f, -3.18f), new Vector3(.7f, .9f, .06f), "#2a2430", false);
            lumieres.Add(Fenetre(chap, new Vector3(0, 2.6f, 4.54f)));

            // Lavoir : un toit sur quatre poteaux, au-dessus d'un bassin
            Vector2 pl = PlaceLibre(V + new Vector2(-13f, -11f), 4f);
            var lav = new GameObject("Lavoir").transform;
            lav.SetParent(v, false);
            lav.position = new Vector3(pl.x, Sol(pl.x, pl.y), pl.y);
            foreach (var (x, z) in new[] { (-1.8f, -1.3f), (1.8f, -1.3f), (-1.8f, 1.3f), (1.8f, 1.3f) })
                Local(lav, PrimitiveType.Cube, new Vector3(x, 1.2f, z), new Vector3(.16f, 2.4f, .16f), "#5a3e26", true);
            Local(lav, PrimitiveType.Cube, new Vector3(0, 2.5f, 0), new Vector3(4.6f, .1f, 3.4f), "#94403a", false, new Vector3(0, 0, 8f));
            Local(lav, PrimitiveType.Cube, new Vector3(0, .3f, 0), new Vector3(3f, .6f, 2f), "#9a948a", true);
            Local(lav, PrimitiveType.Cube, new Vector3(0, .55f, 0), new Vector3(2.6f, .05f, 1.6f), "#7aa8c8", false);

            // Moulin à eau sur la rivière, en amont du village
            float zm = 38f, xm = Monde.Riviere(zm) + 15.5f;
            Vector2 pmo = PlaceLibre(new Vector2(xm, zm), 5f);
            var moulin = Batiment(v, "Moulin", pmo, new Vector2(1f, 0f), new Vector3(6f, 6f, 6f), "#a29a8e", "#6b4a2e", 3f);
            var roue = new GameObject("Roue").transform;
            roue.SetParent(moulin, false);
            roue.localPosition = new Vector3(-3.6f, 2.4f, 0);   // côté rivière
            roue.localRotation = Quaternion.Euler(0, 90f, 0);
            Local(roue, PrimitiveType.Cylinder, Vector3.zero, new Vector3(4.2f, .25f, 4.2f), "#6b4a2e", false, new Vector3(0, 0, 90f));
            for (int k = 0; k < 8; k++)
                Local(roue, PrimitiveType.Cube, Quaternion.Euler(k * 45f, 0, 0) * new Vector3(0, 2.1f, 0), new Vector3(.6f, .15f, .9f), "#5a3e26", false, new Vector3(k * 45f, 0, 0));
            vie.roueMoulin = roue;

            // Enclos à moutons et poules
            Vector2 pe = PlaceLibre(V + new Vector2(26f, 30f), 7f);
            var enclos = new GameObject("Enclos").transform;
            enclos.SetParent(v, false);
            enclos.position = new Vector3(pe.x, Sol(pe.x, pe.y), pe.y);
            for (int k = 0; k < 20; k++)
            {
                float a = k / 20f * Mathf.PI * 2f, a2 = (k + 1) / 20f * Mathf.PI * 2f;
                Vector3 p1 = new Vector3(Mathf.Cos(a) * 6f, 0, Mathf.Sin(a) * 6f), p2 = new Vector3(Mathf.Cos(a2) * 6f, 0, Mathf.Sin(a2) * 6f);
                Local(enclos, PrimitiveType.Cube, p1 + Vector3.up * .5f, new Vector3(.12f, 1f, .12f), "#5a3e26", true);
                Local(enclos, PrimitiveType.Cube, (p1 + p2) / 2f + Vector3.up * .7f, new Vector3(.06f, .08f, (p2 - p1).magnitude), "#6b4a2e", false, new Vector3(0, Mathf.Atan2(p2.x - p1.x, p2.z - p1.z) * Mathf.Rad2Deg, 0));
            }
            for (int k = 0; k < 6; k++) Mouton(v, pe + new Vector2(Mathf.Cos(k) * 3f, Mathf.Sin(k) * 3f), pe);
            for (int k = 0; k < 7; k++)
            {
                var pp = PlaceLibre(V + new Vector2(Mathf.Cos(k * 1.3f) * 12f, Mathf.Sin(k * 1.3f) * 12f), .6f);
                Poule(v, pp, k);
            }
            vie.lumieresNuit = lumieres.ToArray();
        }

        static void Mouton(Transform parent, Vector2 pos, Vector2 centre)
        {
            var m = new GameObject("Mouton").transform;
            m.SetParent(parent, false);
            m.position = new Vector3(pos.x, Sol(pos.x, pos.y), pos.y);
            Local(m, PrimitiveType.Cube, new Vector3(0, .55f, 0), new Vector3(.6f, .5f, .9f), "#ece6d6", false);
            foreach (var (x, z) in new[] { (-.2f, .3f), (.2f, .3f), (-.2f, -.3f), (.2f, -.3f) })
                Local(m, PrimitiveType.Cube, new Vector3(x, .17f, z), new Vector3(.1f, .34f, .1f), "#2a2420", false);
            var tete = new GameObject("Tete").transform;
            tete.SetParent(m, false); tete.localPosition = new Vector3(0, .65f, .45f);
            Local(tete, PrimitiveType.Cube, new Vector3(0, -.05f, .12f), new Vector3(.25f, .28f, .32f), "#2a2420", false);
            var a = m.gameObject.AddComponent<Animal>();
            a.centre = centre; a.rayon = 4.5f; a.vitesse = .5f; a.tete = tete;
        }

        static void Poule(Transform parent, Vector2 pos, int k)
        {
            var m = new GameObject("Poule").transform;
            m.SetParent(parent, false);
            m.position = new Vector3(pos.x, Sol(pos.x, pos.y), pos.y);
            Local(m, PrimitiveType.Cube, new Vector3(0, .22f, 0), new Vector3(.22f, .22f, .3f), k % 3 == 0 ? "#8a5a2a" : "#ece6d6", false);
            Local(m, PrimitiveType.Cube, new Vector3(0, .05f, 0), new Vector3(.05f, .1f, .05f), "#d8a03a", false);
            var tete = new GameObject("Tete").transform;
            tete.SetParent(m, false); tete.localPosition = new Vector3(0, .34f, .12f);
            Local(tete, PrimitiveType.Cube, new Vector3(0, .02f, .04f), new Vector3(.11f, .13f, .11f), k % 3 == 0 ? "#8a5a2a" : "#ece6d6", false);
            Local(tete, PrimitiveType.Cube, new Vector3(0, .1f, .04f), new Vector3(.03f, .06f, .07f), "#c8322a", false);
            var a = m.gameObject.AddComponent<Animal>();
            a.centre = pos; a.rayon = 3f; a.vitesse = .6f; a.poule = true; a.tete = tete;
        }

        static void Paillasse(Transform parent)
        {
            var V = Monde.Village;
            Vector2 odon = new Vector2(V.x + 3f, V.y - 2.5f);
            Transform maison = null; float best = float.MaxValue;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.name.StartsWith("Maison") && t.name != "MaisonCorvin" && t.parent != null && t.parent.name == "Village")
                {
                    float d = Vector2.Distance(new Vector2(t.position.x, t.position.z), odon);
                    if (d < best) { best = d; maison = t; }
                }
            Vector2 c = maison != null ? new Vector2(maison.position.x, maison.position.z) : odon + new Vector2(6, 0);
            Vector2 versCentre = (V - c).sqrMagnitude > .01f ? (V - c).normalized : Vector2.right;
            Vector2 p = PlaceLibre(c + versCentre * 5.5f, 1.4f);
            var go = new GameObject("PaillasseOdon");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(p.x, Sol(p.x, p.y), p.y);
            go.transform.rotation = Quaternion.LookRotation(new Vector3(versCentre.x, 0, versCentre.y));
            Piece(go.transform, PrimitiveType.Cube, go.transform.position + Vector3.up * .2f, new Vector3(1.1f, .35f, 2f), new Vector3(0, go.transform.eulerAngles.y, 0), "#d9bf6a");
            Piece(go.transform, PrimitiveType.Cube, go.transform.position + Vector3.up * .45f + go.transform.forward * -.7f, new Vector3(.8f, .18f, .45f), new Vector3(0, go.transform.eulerAngles.y, 0), "#e8dcc0");
            // Petit auvent de bois au-dessus
            foreach (float s in new[] { -.7f, .7f })
                Piece(go.transform, PrimitiveType.Cube, go.transform.position + go.transform.right * s + Vector3.up * 1.1f - go.transform.forward * .9f, new Vector3(.1f, 2.2f, .1f), Vector3.zero, "#5a3e26");
            Piece(go.transform, PrimitiveType.Cube, go.transform.position + Vector3.up * 2.2f - go.transform.forward * .4f, new Vector3(1.8f, .1f, 1.6f), new Vector3(-12f, go.transform.eulerAngles.y, 0), "#7a5636");
            var i = go.AddComponent<Interlocuteur>();
            i.id = "lit"; i.nom = "la paillasse d'Odon"; i.verbe = "Se coucher dans";
        }

        static Poste CreerPoste(Transform parent, string id, string nom, Ressource produit, float rendement, int max, float reserve, string requis, Vector2 pos)
        {
            var go = new GameObject("Poste_" + nom);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, Sol(pos.x, pos.y), pos.y);
            var p = go.AddComponent<Poste>();
            p.id = id; p.nom = nom; p.produit = produit; p.rendement = rendement;
            p.maxPersonnes = max; p.reserve = reserve; p.requis = requis;
            return p;
        }

        static void CreerOuvrier(GameObject go, string nom, string requis, bool porte, float force)
        {
            if (go == null) { Debug.LogWarning($"Aldermark : {nom} introuvable dans la scène."); return; }
            var o = go.GetComponent<Ouvrier>();
            if (o == null) o = go.AddComponent<Ouvrier>();
            o.nom = nom; o.requis = requis; o.porte = porte; o.force = force;
        }

        static Transform Groupe(Transform parent, string nom)
        {
            var g = new GameObject(nom).transform;
            g.SetParent(parent, false);
            return g;
        }

        static void DecorForet(Vector2 c, Transform parent)
        {
            for (int i = 0; i < 9; i++)   // quelques arbres et souches
            {
                float a = i * 2.39f, r = 5f + Monde.HashPublic(i, 31) * 9f;
                var q = c + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
                float y = Sol(q.x, q.y), h = 5f + Monde.HashPublic(i, 32) * 3f;
                if (i % 3 == 0)
                {
                    Piece(parent, PrimitiveType.Cylinder, new Vector3(q.x, y + .25f, q.y), new Vector3(.6f, .25f, .6f), Vector3.zero, "#7a5636");
                    continue;
                }
                Piece(parent, PrimitiveType.Cylinder, new Vector3(q.x, y + h * .3f, q.y), new Vector3(.45f, h * .3f, .45f), Vector3.zero, "#4a3424");
                for (int k = 0; k < 3; k++)
                    Piece(parent, PrimitiveType.Sphere, new Vector3(q.x, y + h * (.55f + k * .17f), q.y), Vector3.one * (3.2f - k * .8f), Vector3.zero, k == 1 ? "#3f6a3a" : "#365e33");
            }
            for (int k = 0; k < 6; k++)   // tas de poutres
                Piece(parent, PrimitiveType.Cylinder, new Vector3(c.x + 2f, Sol(c.x + 2f, c.y - 2f) + .25f + (k / 3) * .45f, c.y - 2f + (k % 3) * .45f - .45f), new Vector3(.4f, 1.6f, .4f), new Vector3(0, 0, 90f), "#8a6440");
        }

        static void DecorCarriere(Vector2 c, Transform parent)
        {
            for (int i = 0; i < 8; i++)
            {
                float a = i * 0.9f + 1.2f, r = 5f + Monde.HashPublic(i, 41) * 4f;
                var q = c + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
                float t = 2f + Monde.HashPublic(i, 42) * 3f;
                Piece(parent, PrimitiveType.Cube, new Vector3(q.x, Sol(q.x, q.y) + t * .35f, q.y), new Vector3(t, t * .9f, t * 1.1f), new Vector3(Monde.HashPublic(i, 43) * 20f, i * 37f, 0), "#8e867c");
            }
            for (int k = 0; k < 5; k++)   // blocs taillés
                Piece(parent, PrimitiveType.Cube, new Vector3(c.x - 2f + k * .9f, Sol(c.x - 2f + k * .9f, c.y + 2f) + .35f, c.y + 2f), new Vector3(.8f, .7f, .7f), Vector3.zero, "#a29a8e");
        }

        static GameObject Personnage(Transform parent, string nom, string id, Vector2 pos, string tunique, string cheveux, float taille)
        {
            var go = new GameObject(nom);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, Sol(pos.x, pos.y), pos.y);
            go.transform.rotation = Quaternion.LookRotation(new Vector3(Monde.Depart.x - pos.x, 0, Monde.Depart.y - pos.y));
            var h = go.AddComponent<Habitant>();
            ColorUtility.TryParseHtmlString(tunique, out h.tunique);
            ColorUtility.TryParseHtmlString(cheveux, out h.cheveux);
            h.taille = taille;
            h.promeneur = false;
            var i = go.AddComponent<Interlocuteur>();
            i.id = id; i.nom = nom;
            return go;
        }
    }
}
