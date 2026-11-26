using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Aldermark.Editeur
{
    /// <summary>
    /// Tient à jour le catalogue audio (Assets/Aldermark/Audio/Resources/CatalogueAudio.asset) dès qu'un fichier
    /// arrive dans Assets/Aldermark/Audio : les musiques par situation (Musique/&lt;Situation&gt;/), les bruitages
    /// rangés par identifiant d'après leur nom. Les musiques sont lues en continu (« Streaming ») pour ne pas remplir la mémoire.
    /// </summary>
    public class CatalogueAudioEditeur : AssetPostprocessor
    {
        const string Racine = "Assets/Aldermark/Audio";
        const string Chemin = Racine + "/Resources/CatalogueAudio.asset";
        static bool prevu;

        [MenuItem("Aldermark/Rafraîchir les sons et la musique", priority = 30)]
        public static void Rafraichir()
        {
            if (!AssetDatabase.IsValidFolder(Racine)) return;
            if (!AssetDatabase.IsValidFolder(Racine + "/Resources")) AssetDatabase.CreateFolder(Racine, "Resources");
            var cat = AssetDatabase.LoadAssetAtPath<CatalogueAudio>(Chemin);
            if (cat == null)
            {
                cat = ScriptableObject.CreateInstance<CatalogueAudio>();
                AssetDatabase.CreateAsset(cat, Chemin);
            }
            cat.musiques.Clear();
            cat.sons.Clear();
            var variantes = new Dictionary<string, CatalogueAudio.Variantes>();
            int ignores = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Racine }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(p);
                if (clip == null) continue;
                string rel = p.Substring(Racine.Length + 1);
                string[] parties = rel.Split('/');
                string nom = Path.GetFileNameWithoutExtension(p);
                if (parties[0].Equals("Musique", System.StringComparison.OrdinalIgnoreCase) || parties.Length == 1)
                {
                    // Musique/<Situation>/morceau ; un morceau posé directement dans Audio (ancienne façon) compte pour l'exploration
                    string situation = parties.Length >= 3 ? parties[1] : "Exploration";
                    cat.musiques.Add(new CatalogueAudio.Piste { situation = situation, nom = nom, clip = clip });
                    continue;
                }
                string id = CatalogueAudio.IdPour(nom);
                if (id == null) { ignores++; continue; }
                if (!variantes.TryGetValue(id, out var v)) variantes[id] = v = new CatalogueAudio.Variantes { id = id };
                v.clips.Add(clip);
            }
            cat.sons.AddRange(variantes.Values.OrderBy(v => v.id));
            cat.musiques.Sort((a, b) => string.Compare(a.situation + a.nom, b.situation + b.nom, System.StringComparison.Ordinal));
            EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssets();
            Debug.Log($"Aldermark : catalogue audio à jour : {cat.musiques.Count} musiques ({string.Join(", ", cat.musiques.Select(m => m.situation).Distinct())}), " +
                      $"{cat.sons.Sum(s => s.clips.Count)} bruitages pour {cat.sons.Count} sons" + (ignores > 0 ? $", {ignores} fichiers sans emploi (noms non reconnus)." : "."));
        }

        static void OnPostprocessAllAssets(string[] importes, string[] supprimes, string[] deplaces, string[] deplacesDe)
        {
            bool audio = importes.Concat(supprimes).Concat(deplaces).Concat(deplacesDe)
                .Any(p => p.StartsWith(Racine + "/") && !p.EndsWith("CatalogueAudio.asset") && !p.EndsWith(".meta") && !p.Contains("/Resources"));
            if (!audio || prevu) return;
            prevu = true;
            EditorApplication.delayCall += () => { prevu = false; Rafraichir(); };
        }

        /// <summary>Les musiques sont lues en continu depuis le disque (un morceau de 3 min décompressé ferait 60 Mo).</summary>
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Racine + "/")) return;
            string rel = assetPath.Substring(Racine.Length + 1);
            bool musique = rel.StartsWith("Musique/", System.StringComparison.OrdinalIgnoreCase) || !rel.Contains("/");
            var imp = (AudioImporter)assetImporter;
            var r = imp.defaultSampleSettings;
            if (musique)
            {
                r.loadType = AudioClipLoadType.Streaming;
                r.compressionFormat = AudioCompressionFormat.Vorbis;
                r.quality = .7f;
            }
            else
            {
                r.loadType = AudioClipLoadType.DecompressOnLoad;
                imp.forceToMono = true;
            }
            imp.defaultSampleSettings = r;
        }
    }
}
