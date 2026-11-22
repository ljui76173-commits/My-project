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
    public static class GameplayPont
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

            float BX = Monde.PontX;
            var K = Monde.Four;
            var C = Monde.Corvin;
            float hK = Sol(K.x, K.y);

            Indice(racine, "Pont", new Vector3(BX, 6f, 0),
                "Flux rompu", "Pont de Varenne — flux rompu", "#ff4d4d",
                "La ligne ambrée des charrettes s'arrête net au milieu de la rivière, rouge comme une plaie. Le pont de Varenne coupe la route du Comte.",
                "vu_pont", "", 0f, 0, 60f, 6f);
            Indice(racine, "Four", new Vector3(K.x, hK + 4.5f, K.y),
                "Four à chaux", "Four à chaux — inactif. Remise en état possible.", "#ffbf4a",
                "Un vieux four, éteint depuis des années. La voûte du foyer est fendue, la cheminée s'est effondrée à l'intérieur, la grille est rouillée et tordue. Remis en état, il donnerait la chaux du mortier.",
                "vu_four", "", .5f, 0, 50f, 4f);
            Indice(racine, "Calcaire", new Vector3(K.x - 12f, Sol(K.x - 12f, K.y + 6f) + 1.5f, K.y + 6f),
                "Calcaire déjà extrait", "Calcaire — prêt à cuire", "#ffbf4a",
                "Un tas de pierres blanches couvertes de mousse, sur le talus : du calcaire déjà extrait, mais jamais cuit.",
                "vu_calcaire", "", 0f, 0, 50f, 3f);
            Indice(racine, "BlocsRiviere", new Vector3(BX + 1f, .3f, 3f),
                "Blocs du pont", "Blocs du pont — 31 récupérables", "#ffbf4a",
                "Sous l'eau, les pierres taillées du pont effondré. Une trentaine sont intactes et récupérables.",
                "vu_blocs", "", 0f, 0, 45f, 4f);
            Indice(racine, "Foyer", new Vector3(K.x, hK + 1f, K.y),
                "Cendres soufrées", "Foyer — cendres récentes, soufre", "#ff4d4d",
                "Au milieu des vieilles cendres grises : des cendres noires, récentes, qui piquent le nez. Du soufre. Et dans la terre battue, des empreintes de bottes qui ne sont pas celles des villageois. Quelqu'un utilise ce four la nuit.",
                "vu_cendres", "vu_four", 1f, 1, 9f, 2.5f);
            var milieu = Vector2.Lerp(K, C, .5f);
            Indice(racine, "PisteCorvin", new Vector3(milieu.x, Sol(milieu.x, milieu.y) + 2f, milieu.y),
                "Flux récent : four → Corvin", "Flux récent — vers la maison de Corvin", "#fff0c0",
                "Une ligne fraîche, presque vive, part du four, longe la rivière… et file vers la maison de Corvin, le passeur.",
                "vu_corvin", "sabotage_connu", .5f, 0, 60f, 8f);

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

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Aldermark : gameplay du pont ajouté (6 indices, Odon, Bram, dialogues). Appuie sur Play.");
        }

        static float Sol(float x, float z) => Mathf.Max(Monde.Hauteur(x, z), -1.2f);

        static void Indice(Transform parent, string nom, Vector3 pos, string titre, string etiquette, string couleur,
            string description, string drapeau, string requis, float eveil, int confiance, float portee, float rayon)
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
            p.porteeMax = portee; p.rayon = rayon;
        }

        static void Personnage(Transform parent, string nom, string id, Vector2 pos, string tunique, string cheveux, float taille)
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
        }
    }
}
