using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aldermark
{
    /// <summary>
    /// Les villes du grand monde, construites au lancement : le plan de chaque ville (enregistré à la génération)
    /// est changé en maisons, églises, remparts… sur les autres cœurs du processeur, la plus proche d'abord,
    /// puis chaque quartier (64 m) devient un maillage. Les collisions et les lieux de la carte sont déjà dans la scène.
    /// </summary>
    public class VillesDuMonde : MonoBehaviour
    {
        public static VillesDuMonde Instance { get; private set; }

        [Tooltip("Les plans des villes (JSON écrit par le générateur).")]
        public TextAsset donnees;
        [Tooltip("La matière des bâtiments (Aldermark/Bati : couleurs des sommets).")]
        public Material materiau;
        public float tailleQuartier = 64f;
        [Tooltip("Taille à l'écran (part de la hauteur) en dessous de laquelle un quartier passe en version simplifiée.")]
        public float seuilSimple = .22f;
        [Tooltip("Taille à l'écran en dessous de laquelle un quartier n'est plus dessiné.")]
        public float seuilCache = .01f;

        class Quartier { public string nom; public Maillage m, simple; }

        readonly ConcurrentQueue<Quartier> prets = new ConcurrentQueue<Quartier>();
        readonly ConcurrentQueue<string> erreurs = new ConcurrentQueue<string>();
        int enCours, quartiers, triangles;

        /// <summary>Vrai quand toutes les villes sont bâties.</summary>
        public bool Termine => enCours == 0 && prets.IsEmpty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reinitialiser() => Instance = null;

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            if (donnees == null || materiau == null) { Debug.LogWarning("Aldermark : pas de plans de villes (relance « Nouveau monde (RPG) »)."); return; }
            var d = JsonUtility.FromJson<DonneesVilles>(donnees.text);
            if (d == null || d.villes == null) return;
            var cam = Camera.main;
            Vector3 depuis = cam != null ? cam.transform.position : Vector3.zero;
            d.villes.Sort((a, b) => (a.centre - depuis).sqrMagnitude.CompareTo((b.centre - depuis).sqrMagnitude));
            enCours = d.villes.Count;
            foreach (var v in d.villes)
            {
                var ville = v;
                Task.Run(() => Batir(ville));
            }
        }

        void Batir(VilleCuite v)
        {
            try
            {
                var morceaux = new Dictionary<long, (Maillage complet, Maillage simple)>();
                var volumes = new List<Volume>();
                foreach (var k in v.commandes)
                {
                    int i = Mathf.FloorToInt((k.position.x - v.centre.x) / tailleQuartier), j = Mathf.FloorToInt((k.position.z - v.centre.z) / tailleQuartier);
                    long cle = ((long)i << 32) ^ (uint)j;
                    if (!morceaux.TryGetValue(cle, out var paire))
                    {
                        var o = new Vector3(v.centre.x + (i + .5f) * tailleQuartier, v.centre.y, v.centre.z + (j + .5f) * tailleQuartier);
                        paire = (new Maillage { origine = o }, new Maillage { origine = o, detail = false, nuance = .03f });
                        morceaux[cle] = paire;
                    }
                    try
                    {
                        volumes.Clear();
                        KitBatiment.Construire(paire.complet, k, volumes);
                        volumes.Clear();
                        KitBatiment.Construire(paire.simple, k, volumes);
                    }
                    catch (Exception e) { erreurs.Enqueue($"Aldermark : {k.type} de {v.nom} ignoré : {e.Message}"); }
                }
                foreach (var p in morceaux.Values) prets.Enqueue(new Quartier { nom = v.nom, m = p.complet, simple = p.simple });
            }
            catch (Exception e) { erreurs.Enqueue($"Aldermark : la ville {v.nom} n'a pas pu être bâtie : {e}"); }
            finally { Interlocked.Decrement(ref enCours); }
        }

        void Update()
        {
            while (erreurs.TryDequeue(out var e)) Debug.LogError(e);
            // Quelques quartiers par image, pour ne pas figer le jeu
            for (int n = 0; n < 4 && prets.TryDequeue(out var q); n++) Creer(q);
        }

        void Creer(Quartier q)
        {
            if (q.m.v.Count == 0) return;
            var go = new GameObject("Bati " + q.nom);
            go.transform.SetParent(transform, false);
            go.transform.position = q.m.origine;
            // Deux niveaux de détail : complet de près, simplifié de loin (sans ombres), rien quand le quartier ne fait que quelques pixels
            var complet = Rendu(go.transform, "Complet", q.m, ShadowCastingMode.On);
            var simple = Rendu(go.transform, "Simple", q.simple, ShadowCastingMode.Off);
            var lod = go.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(seuilSimple, new Renderer[] { complet }), new LOD(seuilCache, new Renderer[] { simple }) });
            lod.RecalculateBounds();
            quartiers++;
            triangles += q.m.Triangles;
            if (Termine) Debug.Log($"Aldermark : villes bâties ({quartiers} quartiers, {triangles / 1000} k triangles).");
        }

        Renderer Rendu(Transform parent, string nom, Maillage m, ShadowCastingMode ombres)
        {
            var mesh = new Mesh { name = nom, indexFormat = m.v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(m.v);
            mesh.SetNormals(m.n);
            mesh.SetColors(m.c);
            mesh.SetTriangles(m.t, 0);
            mesh.RecalculateBounds();
            var go = new GameObject(nom);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = materiau;
            r.shadowCastingMode = ombres;
            return r;
        }
    }
}
