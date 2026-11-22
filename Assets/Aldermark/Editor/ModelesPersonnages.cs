using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aldermark.Editeur
{
    /// <summary>
    /// Menu « Aldermark > Brancher le modèle d'Aldric » : cherche dans Assets/Aldermark/Models
    /// un fichier dont le nom contient « aldric » (sous-dossiers compris), le met à la taille
    /// d'Aldric, lui fabrique un matériau avec les textures posées à côté, et crée un
    /// Animator si le fichier contient des animations. Relancer le menu remplace le modèle.
    /// </summary>
    public static class ModelesPersonnages
    {
        const string DossierGenere = "Assets/Aldermark/Genere";

        [MenuItem("Aldermark/Brancher le modèle d'Aldric")]
        public static void BrancherAldric()
        {
            var joueur = Object.FindFirstObjectByType<JoueurAldric>();
            if (joueur == null) { Debug.LogWarning("Aldermark : pas d'Aldric dans la scène. Génère d'abord la vallée."); return; }

            var fichiers = TrouverTous("aldric");
            // Le modèle principal : sans « @ », de préférence riggé (Mixamo « With Skin »)
            string chemin = fichiers
                .Where(p => !Path.GetFileName(p).Contains("@"))
                .OrderBy(p => Rigge(p) ? 0 : 1)
                .ThenBy(p => p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .FirstOrDefault() ?? fichiers.FirstOrDefault();
            var ancien = joueur.transform.Find("Modele");
            if (ancien != null) Object.DestroyImmediate(ancien.gameObject);
            joueur.modele = null;
            if (chemin == null)
            {
                Debug.Log("Aldermark : aucun modèle « aldric » dans Assets/Aldermark/Models, Aldric garde son corps en cubes.");
                return;
            }

            // Rig humanoïde partagé par le modèle et les fichiers d'animation (aldric@marche.fbx…)
            Avatar avatar = null;
            bool rigge = Rigge(chemin);
            if (rigge)
            {
                avatar = PreparerHumanoide(chemin, null);
                if (avatar == null || !avatar.isHuman)
                {
                    Debug.LogWarning("Aldermark : le squelette d'Aldric n'a pas pu être reconnu comme humanoïde (rig générique utilisé).");
                    avatar = null;
                }
                else
                    foreach (var f in fichiers) if (f != chemin) PreparerHumanoide(f, avatar);
            }
            var clips = new List<(AnimationClip clip, string nom, bool principal)>();
            foreach (var f in fichiers.OrderBy(x => x == chemin ? 0 : 1))
                foreach (var c in PreparerAnimations(f))
                    clips.Add((c, (Path.GetFileNameWithoutExtension(f) + " " + c.name).ToLowerInvariant(), f == chemin));
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(chemin);

            // Conteneur neutre (c'est lui que le jeu balance), le modèle garde sa rotation d'import
            var conteneur = new GameObject("Modele").transform;
            conteneur.SetParent(joueur.transform, false);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(source, conteneur);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = source.transform.localRotation;
            go.transform.localScale = source.transform.localScale;

            // Taille : 1,8 m, pieds au sol, centré sur la capsule
            var b = Bornes(go);
            if (b.size.y > 1e-4f) go.transform.localScale *= 1.8f / b.size.y;
            b = Bornes(go);
            Vector3 decal = joueur.transform.InverseTransformVector(new Vector3(b.center.x, b.min.y, b.center.z) - joueur.transform.position);
            go.transform.localPosition -= decal;

            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            AppliquerMateriau(go, chemin, "Aldric");
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;

            // Animations
            if (clips.Count > 0 || avatar != null)
            {
                var anim = go.GetComponent<Animator>();
                if (anim == null) anim = go.AddComponent<Animator>();
                if (avatar != null) anim.avatar = avatar;
                if (clips.Count > 0) anim.runtimeAnimatorController = CreerControleur("Aldric", clips);
                anim.applyRootMotion = false;
                Debug.Log($"Aldermark : Aldric {(avatar != null ? "humanoïde" : "générique")}, {clips.Count} animation(s) : {string.Join(", ", clips.Select(c => c.nom))}");
            }
            else Debug.Log("Aldermark : le modèle d'Aldric n'a pas d'animations, il aura un balancement simple.");

            joueur.modele = conteneur;
            EditorUtility.SetDirty(joueur);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(joueur.gameObject.scene);
            Debug.Log($"Aldermark : modèle d'Aldric branché ({chemin}). V en jeu : vue à la troisième personne.");
        }

        // ---------- recherche ----------

        static List<string> TrouverTous(string motCle)
        {
            var res = new List<string>();
            foreach (var d in new[] { "Assets/Aldermark/Models", "Assets/Aldermark/Modeles" })
            {
                if (!AssetDatabase.IsValidFolder(d)) continue;
                res.AddRange(AssetDatabase.FindAssets("t:GameObject", new[] { d })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => Path.GetFileNameWithoutExtension(p).ToLowerInvariant().Contains(motCle))
                    .Distinct().OrderBy(p => p));
            }
            return res;
        }

        static bool Rigge(string chemin)
        {
            var g = AssetDatabase.LoadAssetAtPath<GameObject>(chemin);
            return g != null && g.GetComponentInChildren<SkinnedMeshRenderer>(true) != null;
        }

        /// <summary>Passe un fichier en rig humanoïde (avatar créé ici, ou copié du modèle principal).</summary>
        static Avatar PreparerHumanoide(string chemin, Avatar source)
        {
            if (AssetImporter.GetAtPath(chemin) is ModelImporter mi)
            {
                bool change = false;
                if (mi.animationType != ModelImporterAnimationType.Human) { mi.animationType = ModelImporterAnimationType.Human; change = true; }
                if (source == null)
                {
                    if (mi.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel) { mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; change = true; }
                }
                else if (mi.avatarSetup != ModelImporterAvatarSetup.CopyFromOther || mi.sourceAvatar != source)
                {
                    mi.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                    mi.sourceAvatar = source;
                    change = true;
                }
                if (change) mi.SaveAndReimport();
            }
            return AssetDatabase.LoadAllAssetsAtPath(chemin).OfType<Avatar>().FirstOrDefault();
        }

        static Bounds Bornes(GameObject g)
        {
            var rs = g.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(g.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        // ---------- matériau ----------

        /// <summary>
        /// Fabrique un matériau URP mat (pas brillant) avec les textures trouvées à côté du modèle
        /// (couleur, normale), ou à défaut avec la texture déjà présente dans le fichier.
        /// </summary>
        static void AppliquerMateriau(GameObject go, string cheminModele, string nom)
        {
            string dossier = Path.GetDirectoryName(cheminModele).Replace('\\', '/');
            var textures = AssetDatabase.FindAssets("t:Texture2D", new[] { dossier })
                .Select(AssetDatabase.GUIDToAssetPath).ToArray();

            string Cherche(params string[] mots) => textures.FirstOrDefault(t =>
            {
                string n = Path.GetFileNameWithoutExtension(t).ToLowerInvariant();
                return mots.Any(n.Contains);
            });
            string cheminNormale = Cherche("normal", "_nrm", "_nor");
            string cheminCouleur = Cherche("basecolor", "base_color", "albedo", "diffuse", "color", "colour", "_col", "texture")
                ?? textures.FirstOrDefault(t =>
                {
                    string n = Path.GetFileNameWithoutExtension(t).ToLowerInvariant();
                    return !n.Contains("normal") && !n.Contains("rough") && !n.Contains("metal") && !n.Contains("ao") && !n.Contains("height");
                });

            Texture couleur = cheminCouleur != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(cheminCouleur) : null;
            if (couleur == null)
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                    foreach (var m in r.sharedMaterials)
                        if (m != null && couleur == null) couleur = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.mainTexture;

            Texture normale = null;
            if (cheminNormale != null)
            {
                if (AssetImporter.GetAtPath(cheminNormale) is TextureImporter ti && ti.textureType != TextureImporterType.NormalMap)
                {
                    ti.textureType = TextureImporterType.NormalMap;
                    ti.SaveAndReimport();
                }
                normale = AssetDatabase.LoadAssetAtPath<Texture2D>(cheminNormale);
            }

            Dossier(DossierGenere); Dossier(DossierGenere + "/Materiaux");
            string cheminMat = $"{DossierGenere}/Materiaux/Personnage_{nom}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(cheminMat);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, cheminMat);
            }
            mat.SetColor("_BaseColor", Color.white);
            if (couleur != null) mat.SetTexture("_BaseMap", couleur);
            if (normale != null) { mat.SetTexture("_BumpMap", normale); mat.EnableKeyword("_NORMALMAP"); }
            else mat.DisableKeyword("_NORMALMAP");
            mat.SetFloat("_Smoothness", 0f);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_SpecularHighlights", 0f);
            mat.SetFloat("_EnvironmentReflections", 0f);
            mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(mat);

            foreach (var r in go.GetComponentsInChildren<Renderer>())
                r.sharedMaterials = Enumerable.Repeat(mat, Mathf.Max(1, r.sharedMaterials.Length)).ToArray();
            Debug.Log($"Aldermark : matériau de {nom} — couleur : {(cheminCouleur ?? (couleur != null ? "texture du fichier" : "aucune"))}, normale : {cheminNormale ?? "aucune"}");
        }

        static void Dossier(string chemin)
        {
            if (AssetDatabase.IsValidFolder(chemin)) return;
            AssetDatabase.CreateFolder(Path.GetDirectoryName(chemin).Replace('\\', '/'), Path.GetFileName(chemin));
        }

        // ---------- animations ----------

        /// <summary>Met les animations du fichier en boucle et les renvoie (vide si aucune).</summary>
        static AnimationClip[] PreparerAnimations(string chemin)
        {
            if (AssetImporter.GetAtPath(chemin) is ModelImporter mi)
            {
                var defs = mi.clipAnimations.Length > 0 ? mi.clipAnimations : mi.defaultClipAnimations;
                // En boucle, et « sur place » : le déplacement vient du jeu, pas de l'animation
                if (defs.Length > 0 && defs.Any(d => !d.loopTime || !d.lockRootPositionXZ || !d.lockRootRotation || !d.lockRootHeightY))
                {
                    foreach (var d in defs)
                    {
                        d.loopTime = true;
                        d.lockRootRotation = true; d.keepOriginalOrientation = true;
                        d.lockRootHeightY = true; d.keepOriginalPositionY = true;
                        d.lockRootPositionXZ = true; d.keepOriginalPositionXZ = true;
                    }
                    mi.clipAnimations = defs;
                    mi.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAllAssetsAtPath(chemin).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToArray();
        }

        /// <summary>
        /// Contrôleur avec un paramètre « Vitesse » : repos → marche → course,
        /// selon les noms des animations (idle, walk, run…).
        /// </summary>
        static AnimatorController CreerControleur(string nom, List<(AnimationClip clip, string nom, bool principal)> clips)
        {
            Dossier(DossierGenere);
            string chemin = $"{DossierGenere}/{nom}.controller";
            AssetDatabase.DeleteAsset(chemin);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(chemin);
            ctrl.AddParameter("Vitesse", AnimatorControllerParameterType.Float);

            // Avec Mixamo, les clips s'appellent tous « mixamo.com » : on lit le nom du fichier
            AnimationClip Clip(params string[] mots) =>
                clips.Where(c => mots.Any(m => c.nom.Contains(m))).Select(c => c.clip).FirstOrDefault();
            var repos = Clip("idle", "stand", "repos", "breath", "attente")
                ?? clips.Where(c => c.principal).Select(c => c.clip).FirstOrDefault();
            var marche = Clip("walk", "marche");
            var course = Clip("run", "course", "sprint");
            if (marche == null) marche = Clip("jog");

            var couches = ctrl.layers;
            couches[0].iKPass = true;   // pour lever la main gauche quand le Sceau s'active
            ctrl.layers = couches;
            var etat = ctrl.layers[0].stateMachine;
            if (repos == null && marche == null && course == null)
            {
                etat.AddState("Anim").motion = clips[0].clip;
                return ctrl;
            }
            var arbre = new BlendTree { name = "Deplacement", blendType = BlendTreeType.Simple1D, blendParameter = "Vitesse", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(arbre, ctrl);
            if (repos != null) arbre.AddChild(repos, 0f);
            if (marche != null) arbre.AddChild(marche, 4.5f);
            if (course != null) arbre.AddChild(course, 8.5f);
            var e = etat.AddState("Deplacement");
            e.motion = arbre;
            etat.defaultState = e;
            AssetDatabase.SaveAssets();
            return ctrl;
        }
    }
}
