using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Aldermark.Editeur
{
    /// <summary>
    /// Menu « Aldermark > Nouveau monde (RPG) » : génère la vallée puis le monde ouvert, sans scénario :
    /// le hameau de départ (ta maison, ton lit, quatre voisins, une quintaine), l'horloge du monde, la sauvegarde,
    /// les stats et le combat du héros, la console de debug. La scène est enregistrée dans Assets/Scenes/Aldermark.unity.
    /// </summary>
    public static partial class GameplayPont
    {
        [MenuItem("Aldermark/Nouveau monde (RPG)", priority = 0)]
        public static void MenuNouveauMonde() => GenerateurVallee.GenererGrandMonde();

        [MenuItem("Aldermark/Ancienne vallée (RPG, 620 m)", priority = 20)]
        public static void MenuAncienneVallee() => GenerateurVallee.Generer(true);

        public static void AjouterRPG()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var ancien = GameObject.Find("Gameplay");
            if (ancien != null) Object.DestroyImmediate(ancien);

            var racine = new GameObject("Gameplay").transform;
            racine.gameObject.AddComponent<Dialogues>();
            var sons = racine.gameObject.AddComponent<Sons>();
            sons.positionRiviere = new Vector3(Monde.PontX, 0, 0);
            if (Geo.Actif)
            {
                // Le murmure de l'eau : la rivière la plus proche du hameau
                Vector2 h = Monde.Hameau, meilleur = h + new Vector2(0, 500f);
                foreach (var r in Geo.Rivieres)
                    foreach (var p in r.points)
                        if ((p - h).sqrMagnitude < (meilleur - h).sqrMagnitude) meilleur = p;
                sons.positionRiviere = new Vector3(meilleur.x, Sol(meilleur.x, meilleur.y), meilleur.y);
            }
            if (AssetDatabase.IsValidFolder("Assets/Aldermark/Audio"))
                sons.musiques = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Aldermark/Audio" })
                    .Select(AssetDatabase.GUIDToAssetPath).OrderBy(c => c)
                    .Select(AssetDatabase.LoadAssetAtPath<AudioClip>).Where(c => c != null).ToArray();
            var monde = racine.gameObject.AddComponent<MondeRPG>();
            racine.gameObject.AddComponent<ConsoleDebug>();

            var aldric = GameObject.Find("Aldric");
            if (aldric == null) { Debug.LogError("Aldermark : pas d'Aldric dans la scène. Lance « Nouveau monde (RPG) »."); return; }
            if (aldric.GetComponent<StatsHeros>() == null) aldric.AddComponent<StatsHeros>();
            if (aldric.GetComponent<Inventaire>() == null) aldric.AddComponent<Inventaire>();
            if (aldric.GetComponent<CombatHeros>() == null) aldric.AddComponent<CombatHeros>();
            monde.joueur = aldric.GetComponent<JoueurAldric>();

            var reveil = ConstruireHameau(racine);
            if (Geo.Actif) ConstruireSites(racine);
            else ConstruireBourg(racine);
            monde.reveil = reveil;
            aldric.transform.SetPositionAndRotation(reveil.position + Vector3.up * .1f, reveil.rotation);
            if (!Geo.Actif) NommerHabitantsDeVarenne();
            ModelesPersonnages.BrancherAldric();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Aldermark : monde ouvert prêt (hameau de départ, horloge, sauvegarde, stats). Appuie sur Play. Console : touche ².");
        }

        // ---------- le hameau ----------

        static Transform ConstruireHameau(Transform racine)
        {
            var H = Monde.Hameau;
            var hameau = new GameObject("Hameau").transform;
            hameau.SetParent(racine, false);
            hameau.position = new Vector3(H.x, Sol(H.x, H.y), H.y);
            hameau.gameObject.AddComponent<Entite>().id = "lieu-hameau";

            var reveil = MaisonDuHeros(hameau, Monde.MaisonHeros, H);
            var siteDepart = Geo.Actif ? Geo.SitePlusProche(H, s => s.type == TypeSite.Depart) : null;
            MarquerLieu(hameau, siteDepart != null ? siteDepart.nom + " (ton hameau)" : "Ton hameau", TypeLieu.Hameau, true);
            MarquerLieu(reveil.parent, "Ta maison", TypeLieu.Maison, true);

            // Quatre maisons de voisins, portes vers le puits
            string[] murs = { "#e2d3b4", "#d9c9a8", "#eadfc6", "#c9b48f" }, toits = { "#7a4a33", "#94403a", "#4f6b8f", "#6b5a3a" };
            var places = new[] { new Vector2(10f, -7f), new Vector2(11f, 8f), new Vector2(-8f, 11f), new Vector2(3f, -16f) };
            for (int i = 0; i < places.Length; i++)
            {
                var p = H + places[i];
                var b = Batiment(hameau, "MaisonVoisin" + (i + 1), p, H - p, new Vector3(5f + i % 2, 3.2f, 4.5f), murs[i], toits[i], 2.2f);
                b.gameObject.AddComponent<Entite>().id = "maison-voisin-" + (i + 1);
                Fenetre(b, new Vector3(1.6f, 1.6f, 2.28f));
                Fenetre(b, new Vector3(-1.6f, 1.6f, 2.28f));
            }

            Puits(hameau, H);
            MarquerLieu(hameau.Find("PuitsDuHameau"), "Puits du hameau", TypeLieu.Puits, true);
            Potager(hameau, H + new Vector2(14f, -19f));
            TasDeBois(hameau, H + new Vector2(-4.5f, -9.5f));

            // La quintaine d'entraînement (elle se construit au lancement)
            var q = new GameObject("Quintaine");
            q.transform.SetParent(hameau, false);
            var pq = Monde.Quintaine;
            q.transform.position = new Vector3(pq.x, Sol(pq.x, pq.y), pq.y);
            q.AddComponent<Quintaine>();
            q.AddComponent<Entite>().id = "quintaine-hameau";
            MarquerLieu(q.transform, "Quintaine", TypeLieu.Quintaine, true);

            // Le garde du hameau, à l'entrée du chemin
            var garde = new GameObject("GardeDuHameau");
            garde.transform.SetParent(hameau, false);
            var pg = H + new Vector2(2f, 13f);
            garde.transform.position = new Vector3(pg.x, Sol(pg.x, pg.y) + .1f, pg.y);
            garde.AddComponent<GardeDuHameau>().niveau = 8;
            garde.AddComponent<Entite>().id = "garde-hameau";

            ConstruireRepaires(racine);

            // Les voisins
            string[] noms = { "Mère Agnès", "Thibault", "Colette", "Le vieux Gaspard" };
            string[] tuniques = { "#8c5a3c", "#5b6e8c", "#a0484a", "#6a6a5a" };
            for (int i = 0; i < noms.Length; i++)
            {
                float a = i * 1.7f;
                var p = H + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 4.5f;
                var go = new GameObject(noms[i]);
                go.transform.SetParent(hameau, false);
                go.transform.position = new Vector3(p.x, Sol(p.x, p.y), p.y);
                var h = go.AddComponent<Habitant>();
                ColorUtility.TryParseHtmlString(tuniques[i], out h.tunique);
                ColorUtility.TryParseHtmlString(i == 3 ? "#9a9a9a" : "#4a3020", out h.cheveux);
                h.taille = i == 3 ? .92f : 1f;
                h.promeneur = true; h.centre = H; h.rayon = 6.5f;
                var it = go.AddComponent<Interlocuteur>();
                it.id = "villageois"; it.nom = noms[i];
                go.AddComponent<Entite>().id = "pnj-hameau-" + (i + 1);
            }
            return reveil;
        }

        /// <summary>
        /// La maison du héros, où l'on entre : plancher, quatre murs avec une porte, plafond, toit,
        /// un lit (où l'on dort et sauvegarde), une table, un coffre, une cheminée allumée.
        /// Renvoie le point de réveil, à côté du lit, tourné vers la porte.
        /// </summary>
        static Transform MaisonDuHeros(Transform parent, Vector2 pos, Vector2 regardeVers)
        {
            const float W = 6.4f, D = 5.4f, Hm = 3f, E = .25f, Porte = 1.3f;
            float sol = Mathf.Max(Sol(pos.x - W / 2, pos.y - D / 2), Sol(pos.x + W / 2, pos.y - D / 2), Sol(pos.x - W / 2, pos.y + D / 2), Sol(pos.x + W / 2, pos.y + D / 2), Sol(pos.x, pos.y));
            var m = new GameObject("MaisonDuHeros").transform;
            m.SetParent(parent, false);
            m.position = new Vector3(pos.x, sol, pos.y);
            m.rotation = Quaternion.LookRotation(new Vector3(regardeVers.x - pos.x, 0, regardeVers.y - pos.y));
            m.gameObject.AddComponent<Entite>().id = "maison-heros";

            string bois = "#6b4a30", murs = "#e6d7b8", pierre = "#8d8796";
            Local(m, PrimitiveType.Cube, new Vector3(0, .1f, 0), new Vector3(W, .2f, D), "#7a5a3a", true);                       // plancher
            Local(m, PrimitiveType.Cube, new Vector3(0, -.45f, 0), new Vector3(W + .2f, .9f, D + .2f), pierre, true);             // soubassement
            Local(m, PrimitiveType.Cube, new Vector3(0, Hm / 2 + .2f, -D / 2), new Vector3(W, Hm, E), murs, true);               // fond
            Local(m, PrimitiveType.Cube, new Vector3(-W / 2, Hm / 2 + .2f, 0), new Vector3(E, Hm, D), murs, true);               // gauche
            Local(m, PrimitiveType.Cube, new Vector3(W / 2, Hm / 2 + .2f, 0), new Vector3(E, Hm, D), murs, true);                // droite
            float pan = (W - Porte) / 2f;
            Local(m, PrimitiveType.Cube, new Vector3(-(Porte / 2 + pan / 2), Hm / 2 + .2f, D / 2), new Vector3(pan, Hm, E), murs, true);
            Local(m, PrimitiveType.Cube, new Vector3(Porte / 2 + pan / 2, Hm / 2 + .2f, D / 2), new Vector3(pan, Hm, E), murs, true);
            Local(m, PrimitiveType.Cube, new Vector3(0, Hm - .2f, D / 2), new Vector3(Porte, .8f, E), murs, true);               // linteau
            foreach (float x in new[] { -W / 2, W / 2 })                                                                             // poutres d'angle
                foreach (float z in new[] { -D / 2, D / 2 })
                    Local(m, PrimitiveType.Cube, new Vector3(x, Hm / 2 + .2f, z), new Vector3(.32f, Hm, .32f), bois, false);
            Local(m, PrimitiveType.Cube, new Vector3(0, Hm + .22f, 0), new Vector3(W, .12f, D), "#5a4030", false);               // plafond
            for (int k = -2; k <= 2; k++)
                Local(m, PrimitiveType.Cube, new Vector3(k * 1.3f, Hm + .1f, 0), new Vector3(.2f, .2f, D - .2f), bois, false);  // solives
            if (toit == null) toit = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Aldermark/Genere/Formes/Toit.asset");
            var t = new GameObject("Toit");
            t.transform.SetParent(m, false);
            t.transform.localPosition = new Vector3(0, Hm + .2f, 0);
            t.transform.localScale = new Vector3(W + .9f, 2.1f, D + .9f);
            if (toit != null) { t.AddComponent<MeshFilter>().sharedMesh = toit; t.AddComponent<MeshRenderer>().sharedMaterial = Mat("#8a4632"); }

            // Le lit, contre le mur du fond, à gauche
            var lit = new GameObject("Lit").transform;
            lit.SetParent(m, false);
            lit.localPosition = new Vector3(-W / 2 + .8f, .2f, -D / 2 + 1.25f);
            Local(lit, PrimitiveType.Cube, new Vector3(0, .22f, 0), new Vector3(1.15f, .44f, 2.1f), bois, true);                  // cadre
            Local(lit, PrimitiveType.Cube, new Vector3(0, .52f, 0), new Vector3(1.02f, .18f, 1.95f), "#e8dcc2", false);            // matelas
            Local(lit, PrimitiveType.Cube, new Vector3(0, .66f, -.72f), new Vector3(.8f, .12f, .38f), "#f2ead8", false);           // oreiller
            Local(lit, PrimitiveType.Cube, new Vector3(0, .63f, .32f), new Vector3(1.06f, .1f, 1.3f), "#8c3a2e", false);           // couverture
            Local(lit, PrimitiveType.Cube, new Vector3(0, .55f, -1.02f), new Vector3(1.15f, 1.1f, .1f), bois, false);              // tête de lit
            var it = lit.gameObject.AddComponent<Interlocuteur>();
            it.id = "lit_rpg"; it.nom = "le lit"; it.verbe = "Utiliser";

            // Coffre au pied du lit, table et tabouret, cheminée sur le mur de droite
            var coffre = new GameObject("Coffre").transform;   // l'épée, du pain, quelques pièces
            coffre.SetParent(m, false);
            coffre.localPosition = new Vector3(-W / 2 + .8f, .2f, .05f);
            Local(coffre, PrimitiveType.Cube, new Vector3(0, .26f, 0), new Vector3(1f, .52f, .55f), "#5a3a22", true);
            var couvercle = new GameObject("Couvercle").transform;
            couvercle.SetParent(coffre, false);
            couvercle.localPosition = new Vector3(0, .52f, -.275f);
            Local(couvercle, PrimitiveType.Cube, new Vector3(0, .05f, .275f), new Vector3(1.04f, .1f, .58f), "#6b4a30", false);
            Local(coffre, PrimitiveType.Cube, new Vector3(0, .32f, .285f), new Vector3(.12f, .14f, .03f), "#c8a040", false);
            var cf = coffre.gameObject.AddComponent<Coffre>();
            cf.nom = "Ton coffre"; cf.idSauvegarde = "coffre-maison"; cf.couvercle = couvercle;
            cf.contenu = new List<Pile> { new Pile("epee_courte", 1), new Pile("pain", 2), new Pile(Objets.Pieces, 5) };
            Local(m, PrimitiveType.Cube, new Vector3(1.1f, .98f, -.4f), new Vector3(1.4f, .08f, .85f), bois, true);
            foreach (float x in new[] { .5f, 1.7f })
                foreach (float z in new[] { -.75f, -.05f })
                    Local(m, PrimitiveType.Cube, new Vector3(x, .6f, z), new Vector3(.08f, .76f, .08f), bois, false);
            Local(m, PrimitiveType.Cylinder, new Vector3(1.1f, .5f, .5f), new Vector3(.4f, .25f, .4f), bois, true);
            Local(m, PrimitiveType.Cube, new Vector3(W / 2 - .45f, 1f, -D / 2 + 1.2f), new Vector3(.7f, 1.6f, 1.5f), pierre, true);
            Local(m, PrimitiveType.Cube, new Vector3(W / 2 - .45f, 2.6f, -D / 2 + 1.2f), new Vector3(.5f, 1.6f, .8f), pierre, false);
            var feu = Local(m, PrimitiveType.Cube, new Vector3(W / 2 - .78f, .55f, -D / 2 + 1.2f), new Vector3(.12f, .35f, .7f), "#ff9a3a", false);
            feu.GetComponent<MeshRenderer>().sharedMaterial = Lumineux("#ff9a3a");
            var lumiere = new GameObject("LumiereCheminee").AddComponent<Light>();
            lumiere.transform.SetParent(m, false);
            lumiere.transform.localPosition = new Vector3(W / 2 - 1.2f, 1.1f, -D / 2 + 1.2f);
            lumiere.type = LightType.Point; lumiere.range = 8f; lumiere.intensity = 1.6f; lumiere.color = new Color(1f, .7f, .42f);
            lumiere.shadows = LightShadows.None;

            // Le réveil : debout à côté du lit, face à la porte
            var reveil = new GameObject("Reveil").transform;
            reveil.SetParent(m, false);
            reveil.localPosition = new Vector3(-W / 2 + 1.95f, .25f, -D / 2 + 1.3f);
            reveil.localRotation = Quaternion.Euler(0, 15f, 0);
            return reveil;
        }

        static Material Lumineux(string hex)
        {
            string chemin = $"Assets/Aldermark/Genere/Materiaux/Gameplay_Lumineux_{hex.TrimStart('#')}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(chemin);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            ColorUtility.TryParseHtmlString(hex, out var c);
            m.SetColor("_BaseColor", c);
            AssetDatabase.CreateAsset(m, chemin);
            return m;
        }

        // ---------- le bourg de Varenne ----------

        /// <summary>Varenne revit : marché, auberge, forge, chapelle, lavoir, moulin, enclos ; chaque lieu figure sur la carte.</summary>
        static void ConstruireBourg(Transform racine)
        {
            ConstruireVillage(racine);
            var village = racine.Find("VillageVivant");
            if (village != null)
            {
                var forgeron = village.Find("Forgeron");
                if (forgeron != null && forgeron.GetComponent<Interlocuteur>() != null) forgeron.GetComponent<Interlocuteur>().id = "villageois";
                MarquerLieu(village.Find("Marche"), "Marché de Varenne", TypeLieu.Marche, false);
                MarquerLieu(village.Find("Auberge"), "Auberge de Varenne", TypeLieu.Auberge, false);
                MarquerLieu(village.Find("Forge"), "Forge d'Hadrien", TypeLieu.Forge, false);
                MarquerLieu(village.Find("Chapelle"), "Chapelle de Varenne", TypeLieu.Chapelle, false);
                MarquerLieu(village.Find("Lavoir"), "Lavoir de Varenne", TypeLieu.Lavoir, false);
                MarquerLieu(village.Find("Moulin"), "Moulin à eau", TypeLieu.Moulin, false);
            }
            var bourg = new GameObject("Varenne");
            bourg.transform.SetParent(racine, false);
            bourg.transform.position = new Vector3(Monde.Village.x, Sol(Monde.Village.x, Monde.Village.y), Monde.Village.y);
            MarquerLieu(bourg.transform, "Varenne", TypeLieu.Village, true);
        }

        // ---------- les villes du grand monde ----------

        /// <summary>
        /// Chaque ville, port, village et hameau du grand monde figure sur la carte (le bourg voisin et ta capitale sont connus d'emblée).
        /// Leurs bâtiments arrivent avec le kit de construction (étape suivante).
        /// </summary>
        static void ConstruireSites(Transform racine)
        {
            var groupe = new GameObject("Villes").transform;
            groupe.SetParent(racine, false);
            var voisin = Geo.SitePlusProche(Monde.Hameau, s => s.type == TypeSite.Village);
            foreach (var s in Geo.Sites)
            {
                if (s.type == TypeSite.Depart) continue;
                var go = new GameObject(s.nom);
                go.transform.SetParent(groupe, false);
                go.transform.position = new Vector3(s.position.x, Sol(s.position.x, s.position.y), s.position.y);
                go.AddComponent<Entite>().id = "ville-" + Identifiant(s.nom);
                TypeLieu t = s.type == TypeSite.Capitale ? TypeLieu.Capitale : s.type == TypeSite.Ville ? TypeLieu.Ville :
                             s.type == TypeSite.Port ? TypeLieu.Port : s.type == TypeSite.Village ? TypeLieu.Village : TypeLieu.Hameau;
                bool connu = s == voisin || (s.type == TypeSite.Capitale && s.royaume == 0);
                MarquerLieu(go.transform, s.nom, t, connu);
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

        static void MarquerLieu(Transform t, string nom, TypeLieu type, bool connu)
        {
            if (t == null) { Debug.LogWarning($"Aldermark : lieu introuvable pour la carte : {nom}"); return; }
            var l = t.GetComponent<Lieu>();
            if (l == null) l = t.gameObject.AddComponent<Lieu>();
            l.nom = nom; l.type = type; l.connu = connu;
            if (t.GetComponent<Entite>() == null) t.gameObject.AddComponent<Entite>().id = "lieu-" + type.ToString().ToLower();
        }

        // ---------- les repaires ----------

        static void ConstruireRepaires(Transform racine)
        {
            var groupe = new GameObject("Repaires").transform;
            groupe.SetParent(racine, false);
            if (Geo.Actif && GenerateurVallee.Plan != null)
            {
                // Le grand monde : 33 repaires, de plus en plus dangereux en s'éloignant du hameau
                var compte = new int[4];
                foreach (var (p, e, niv) in GenerateurVallee.Plan.repaires)
                {
                    var espece = (Aldermark.Repaire.Espece)e;
                    int num = ++compte[e];
                    Transform t;
                    switch (espece)
                    {
                        case Aldermark.Repaire.Espece.Gelee:
                            t = Repaire(groupe, "RepaireDesGelees" + num, p, espece, 4, niv, niv + 1, 7f, 4f);
                            MareAuxGelees(t);
                            MarquerLieu(t, "Mare aux gelées", TypeLieu.Gelees, false);
                            break;
                        case Aldermark.Repaire.Espece.Loup:
                            t = Repaire(groupe, "TaniereDesLoups" + num, p, espece, 4, niv, niv + 1, 6f, 8f);
                            TaniereDesLoups(t);
                            MarquerLieu(t, "Tanière des loups", TypeLieu.Loups, false);
                            break;
                        case Aldermark.Repaire.Espece.Gobelin:
                            t = Repaire(groupe, "CampGobelin" + num, p, espece, 4, niv, niv + 2, 3.5f, 10f);
                            CampGobelin(t);
                            MarquerLieu(t, "Camp gobelin", TypeLieu.Gobelins, false);
                            break;
                        default:
                            t = Repaire(groupe, "AntreDuTroll" + num, p, espece, 1, niv, niv, 2f, 24f);
                            AntreDuTroll(t);
                            MarquerLieu(t, "Antre du troll", TypeLieu.Troll, false);
                            break;
                    }
                    t.GetComponent<Entite>().id = $"repaire-{espece.ToString().ToLower()}-{num}";
                }
                return;
            }
            var gelees = Repaire(groupe, "RepaireDesGelees", Monde.RepaireGelees, Aldermark.Repaire.Espece.Gelee, 4, 1, 2, 7f, 4f);
            MarquerLieu(gelees, "Mare aux gelées", TypeLieu.Gelees, false);
            MareAuxGelees(gelees);
            var loups = Repaire(groupe, "TaniereDesLoups", Monde.RepaireLoups, Aldermark.Repaire.Espece.Loup, 4, 3, 4, 6f, 8f);
            MarquerLieu(loups, "Tanière des loups", TypeLieu.Loups, false);
            TaniereDesLoups(loups);
            var camp = Repaire(groupe, "CampGobelin", Monde.CampGobelins, Aldermark.Repaire.Espece.Gobelin, 4, 4, 6, 3.5f, 10f);
            CampGobelin(camp);
            MarquerLieu(camp, "Camp gobelin", TypeLieu.Gobelins, false);
            var antre = Repaire(groupe, "AntreDuTroll", Monde.AntreTroll, Aldermark.Repaire.Espece.Troll, 1, 12, 12, 2f, 24f);
            AntreDuTroll(antre);
            MarquerLieu(antre, "Antre du troll", TypeLieu.Troll, false);
        }

        static Transform Repaire(Transform parent, string nom, Vector2 p, Aldermark.Repaire.Espece e, int nombre, int nivMin, int nivMax, float rayon, float heures)
        {
            var go = new GameObject(nom);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(p.x, Sol(p.x, p.y), p.y);
            var r = go.AddComponent<Aldermark.Repaire>();
            r.espece = e; r.nombre = nombre; r.niveauMin = nivMin; r.niveauMax = nivMax; r.rayon = rayon; r.heuresRepeuplement = heures;
            go.AddComponent<Entite>().id = "repaire-" + e.ToString().ToLower();
            return go.transform;
        }

        /// <summary>La mare aux gelées : une eau verdâtre, des joncs, des pierres moussues, des flaques de gelée.</summary>
        static void MareAuxGelees(Transform mare)
        {
            Local(mare, PrimitiveType.Cylinder, new Vector3(0, .04f, 0), new Vector3(7f, .04f, 5.5f), "#5c8a6a", false);
            for (int k = 0; k < 14; k++)
            {
                float a = k * .45f, r = 3.4f + (k % 3) * .35f;
                Local(mare, PrimitiveType.Cylinder, new Vector3(Mathf.Cos(a) * r, .55f, Mathf.Sin(a) * r * .8f), new Vector3(.05f, .55f + (k % 4) * .1f, .05f), "#6f8a3a", false, new Vector3((k % 5) * 4f - 8f, 0, (k % 3) * 5f - 5f));
            }
            for (int k = 0; k < 6; k++)
            {
                float a = k * 1.05f + .3f;
                Local(mare, PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * 4.6f, .2f, Mathf.Sin(a) * 3.8f), new Vector3(1f + (k % 2) * .5f, .55f, .9f), k % 2 == 0 ? "#6f7a5e" : "#7a7f6a", true, new Vector3(0, k * 50f, 0));
                Local(mare, PrimitiveType.Sphere, new Vector3(Mathf.Cos(a + .5f) * 2.4f, .03f, Mathf.Sin(a + .5f) * 2f), new Vector3(.9f, .05f, .7f), "#7fcf72", false);
            }
        }

        /// <summary>La tanière des loups : un terrier sous des rochers, des os rongés, un tronc abattu.</summary>
        static void TaniereDesLoups(Transform taniere)
        {
            Local(taniere, PrimitiveType.Sphere, new Vector3(0, .9f, -3.2f), new Vector3(5.5f, 3.2f, 4f), "#6e6a66", true, new Vector3(0, 20f, 0));
            Local(taniere, PrimitiveType.Sphere, new Vector3(2.6f, .7f, -2.4f), new Vector3(2.8f, 2.2f, 2.6f), "#7a7672", true, new Vector3(0, 60f, 0));
            Local(taniere, PrimitiveType.Sphere, new Vector3(0, .5f, -1.35f), new Vector3(1.8f, 1.3f, .8f), "#1a1614", false);   // l'entrée du terrier
            Local(taniere, PrimitiveType.Cylinder, new Vector3(-3.2f, .35f, .8f), new Vector3(.6f, 2.4f, .6f), "#5b4030", true, new Vector3(90f, 35f, 0));
            for (int k = 0; k < 8; k++)
                Local(taniere, PrimitiveType.Cylinder, new Vector3(-1.5f + (k % 4) * .9f, .06f, .6f + (k / 4) * .9f), new Vector3(.07f, .3f, .07f), "#e8dfc8", false, new Vector3(90, k * 47f, 0));
            Local(taniere, PrimitiveType.Sphere, new Vector3(1.2f, .12f, 1.4f), new Vector3(.3f, .25f, .35f), "#e8dfc8", false);   // un crâne
        }

        /// <summary>Un camp de gobelins : trois tentes de peaux, un feu, des os.</summary>
        static void CampGobelin(Transform camp)
        {
            for (int k = 0; k < 3; k++)
            {
                float a = k * 2.1f + .4f;
                var t = new GameObject("Tente" + k).transform;
                t.SetParent(camp, false);
                t.localPosition = new Vector3(Mathf.Cos(a) * 5.5f, 0, Mathf.Sin(a) * 5.5f);
                t.localRotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg + 90f, 0);
                Local(t, PrimitiveType.Cube, new Vector3(-.55f, .75f, 0), new Vector3(.08f, 1.7f, 2.2f), "#7a6448", true, new Vector3(0, 0, -32f));
                Local(t, PrimitiveType.Cube, new Vector3(.55f, .75f, 0), new Vector3(.08f, 1.7f, 2.2f), "#6b563c", true, new Vector3(0, 0, 32f));
                Local(t, PrimitiveType.Cylinder, new Vector3(0, 1.45f, 0), new Vector3(.08f, 1.2f, .08f), "#4a3524", false, new Vector3(90, 0, 0));
            }
            var feu = new GameObject("Feu").transform;
            feu.SetParent(camp, false);
            for (int k = 0; k < 6; k++)
                Local(feu, PrimitiveType.Cube, new Vector3(Mathf.Cos(k) * .5f, .1f, Mathf.Sin(k) * .5f), new Vector3(.25f, .2f, .25f), "#6e6a66", false);
            var braise = Local(feu, PrimitiveType.Cube, new Vector3(0, .2f, 0), new Vector3(.5f, .25f, .5f), "#ff8a3a", false);
            braise.GetComponent<MeshRenderer>().sharedMaterial = Lumineux("#ff8a3a");
            var l = new GameObject("Lueur").AddComponent<Light>();
            l.transform.SetParent(feu, false);
            l.transform.localPosition = new Vector3(0, 1f, 0);
            l.type = LightType.Point; l.range = 9f; l.intensity = 1.8f; l.color = new Color(1f, .62f, .32f); l.shadows = LightShadows.None;
            for (int k = 0; k < 4; k++)
                Local(camp, PrimitiveType.Cylinder, new Vector3(2f + k * .4f, .05f, -2f + k * .3f), new Vector3(.06f, .25f, .06f), "#e8dfc8", false, new Vector3(90, k * 40f, 0));
        }

        /// <summary>L'antre du troll : de gros rochers contre la montagne, une bouche sombre, des os.</summary>
        static void AntreDuTroll(Transform antre)
        {
            for (int k = 0; k < 7; k++)
            {
                float a = Mathf.PI * (.9f + k * .2f);
                float r = 6.5f + (k % 3) * 1.2f;
                Local(antre, PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * r, 1f + (k % 2) * .8f, Mathf.Sin(a) * r),
                      new Vector3(3.2f + (k % 3), 2.6f + (k % 2) * 1.4f, 3f), k % 2 == 0 ? "#77737a" : "#6a666e", true, new Vector3(k * 23f, k * 41f, 0));
            }
            Local(antre, PrimitiveType.Cube, new Vector3(0, 1.8f, -6.3f), new Vector3(3.4f, 3.6f, .4f), "#14121a", false);
            for (int k = 0; k < 6; k++)
                Local(antre, PrimitiveType.Cylinder, new Vector3(-2f + k * .8f, .08f, 2f + (k % 3) * .6f), new Vector3(.09f, .4f, .09f), "#e8dfc8", false, new Vector3(90, k * 33f, 0));
        }

        static void Puits(Transform parent, Vector2 p)
        {
            var w = new GameObject("PuitsDuHameau").transform;
            w.SetParent(parent, false);
            w.position = new Vector3(p.x, Sol(p.x, p.y), p.y);
            Local(w, PrimitiveType.Cylinder, new Vector3(0, .4f, 0), new Vector3(2.2f, .45f, 2.2f), "#8d8796", true);
            Local(w, PrimitiveType.Cylinder, new Vector3(0, .82f, 0), new Vector3(1.7f, .02f, 1.7f), "#2a3440", false);
            Local(w, PrimitiveType.Cube, new Vector3(-.95f, 1.4f, 0), new Vector3(.18f, 2.1f, .18f), "#5b4030", false);
            Local(w, PrimitiveType.Cube, new Vector3(.95f, 1.4f, 0), new Vector3(.18f, 2.1f, .18f), "#5b4030", false);
            Local(w, PrimitiveType.Cylinder, new Vector3(0, 2.1f, 0), new Vector3(.18f, .95f, .18f), "#6b4a30", false, new Vector3(0, 0, 90));
            Local(w, PrimitiveType.Cube, new Vector3(0, 2.55f, 0), new Vector3(2.5f, .1f, 1.4f), "#7a4a33", false, new Vector3(0, 0, 0));
        }

        static void Potager(Transform parent, Vector2 p)
        {
            var g = new GameObject("Potager").transform;
            g.SetParent(parent, false);
            g.position = new Vector3(p.x, Sol(p.x, p.y), p.y);
            for (int r = 0; r < 4; r++)
                Local(g, PrimitiveType.Cube, new Vector3(0, .08f, -2.4f + r * 1.6f), new Vector3(7f, .16f, .9f), "#5a4030", false);
            for (int r = 0; r < 4; r++)
                for (int k = 0; k < 9; k++)
                    Local(g, PrimitiveType.Sphere, new Vector3(-3f + k * .75f, .32f, -2.4f + r * 1.6f), Vector3.one * (.35f + (k + r) % 3 * .06f), r % 2 == 0 ? "#6d9a48" : "#88a858", false);
            for (float x = -4.2f; x <= 4.2f; x += 1.4f)
                foreach (float z in new[] { -3.6f, 3.6f })
                    Local(g, PrimitiveType.Cube, new Vector3(x, .45f, z), new Vector3(.12f, .9f, .12f), "#6b4a30", true);
            foreach (float z in new[] { -3.6f, 3.6f })
                Local(g, PrimitiveType.Cube, new Vector3(0, .7f, z), new Vector3(8.6f, .08f, .06f), "#7a5a3a", false);
        }

        static void TasDeBois(Transform parent, Vector2 p)
        {
            var g = new GameObject("TasDeBois").transform;
            g.SetParent(parent, false);
            g.position = new Vector3(p.x, Sol(p.x, p.y), p.y);
            for (int rangee = 0; rangee < 3; rangee++)
                for (int k = 0; k < 4 - rangee; k++)
                    Local(g, PrimitiveType.Cylinder, new Vector3(-.75f + k * .5f + rangee * .25f, .2f + rangee * .38f, 0), new Vector3(.4f, .8f, .4f), "#8a6a48", true, new Vector3(90, 0, 0));
            Local(g, PrimitiveType.Cylinder, new Vector3(1.6f, .3f, .3f), new Vector3(.7f, .3f, .7f), "#7a5a3a", true);
        }

        /// <summary>Les habitants de Varenne ont un nom et répondent quand on leur parle.</summary>
        static void NommerHabitantsDeVarenne()
        {
            var racine = GameObject.Find("Habitants");
            if (racine == null) return;
            string[] prenoms = { "Martin", "Jeanne", "Pierre", "Margot", "Hugues", "Isabeau", "Robin", "Aude", "Guillaume", "Perrine", "Étienne", "Lise", "Benoît", "Clémence" };
            int n = 0, numero = 0;
            foreach (var h in racine.GetComponentsInChildren<Habitant>(true))
            {
                if (h.GetComponent<Interlocuteur>() != null) continue;
                var it = h.gameObject.AddComponent<Interlocuteur>();
                it.id = "villageois";
                it.nom = h.name == "Lise" ? "Lise" : prenoms[n++ % prenoms.Length];
                h.gameObject.AddComponent<Entite>().id = "pnj-varenne-" + ++numero;
            }
        }
    }
}
