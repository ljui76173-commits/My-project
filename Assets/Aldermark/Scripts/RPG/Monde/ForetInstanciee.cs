using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aldermark
{
    /// <summary>
    /// Les arbres, buissons et rochers du grand monde, dessinés par lots (GPU) au lieu d'un par un.
    /// Le terrain de Unity dessinait chaque arbre à part (deux appels de dessin par arbre : tronc et feuillage) :
    /// en regardant vers une grande forêt, des dizaines de milliers d'appels, d'où les saccades.
    /// Ici : le monde est découpé en cases de 128 m ; chaque image, seules les cases dans le champ de vision
    /// sont retenues, et chaque sorte d'arbre part en quelques lots de 1 000.
    /// <list type="bullet">
    /// <item>moins de 140 m : l'arbre complet, avec son ombre ;</item>
    /// <item>moins de 400 m : l'arbre complet, sans ombre ;</item>
    /// <item>au-delà, jusqu'à 1 800 m : une silhouette de quelques triangles (cône pour les sapins, boule taillée pour les autres) ;
    /// les buissons et les rochers, eux, s'arrêtent à 400 m.</item>
    /// </list>
    /// Les arbres restent dans le terrain pour les collisions ; le terrain ne les dessine simplement plus.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class ForetInstanciee : MonoBehaviour
    {
        public float distanceOmbres = 140f, distanceDetail = 400f, distanceMax = 1800f;

        const float Cote = 128f;
        const int Lot = 1000;

        class Sorte
        {
            public Mesh mesh, silhouette;
            public Material[] materiaux;
            public Material matSilhouette;
            public readonly List<Matrix4x4> ombre = new List<Matrix4x4>(), proche = new List<Matrix4x4>(), loin = new List<Matrix4x4>();
        }

        class Case
        {
            public Vector3 centre;
            public Bounds bornes;
            public Matrix4x4[][] parSorte;
        }

        Sorte[] sortes;
        readonly List<Case> cases = new List<Case>();
        readonly Plane[] plans = new Plane[6];
        int arbres;

        void Start()
        {
            var gm = GrandMonde.Instance;
            if (gm == null || gm.terrains == null || gm.terrains.Length == 0 || gm.terrains[0] == null) { enabled = false; return; }
            var protos = gm.terrains[0].terrainData.treePrototypes;
            sortes = new Sorte[protos.Length];
            for (int k = 0; k < protos.Length; k++)
            {
                var pf = protos[k].prefab;
                var mf = pf != null ? pf.GetComponent<MeshFilter>() : null;
                var mr = pf != null ? pf.GetComponent<MeshRenderer>() : null;
                if (mf == null || mr == null || mf.sharedMesh == null) continue;
                var s = new Sorte { mesh = mf.sharedMesh, materiaux = mr.sharedMaterials };
                foreach (var m in s.materiaux) if (m != null) m.enableInstancing = true;
                bool sapin = pf.name.StartsWith("Sapin"), arbre = sapin || pf.name.StartsWith("Arbre");
                if (arbre)
                {
                    s.silhouette = Silhouette(s.mesh.bounds, sapin);
                    s.matSilhouette = s.materiaux[s.materiaux.Length - 1];   // le feuillage
                }
                sortes[k] = s;
            }

            // Les arbres de chaque tuile, rangés par cases de 128 m
            var parCase = new Dictionary<long, List<Matrix4x4>[]>();
            var hauteurs = new Dictionary<long, Vector2>();
            foreach (var t in gm.terrains)
            {
                if (t == null) continue;
                var td = t.terrainData;
                Vector3 o = t.transform.position, taille = td.size;
                foreach (var a in td.treeInstances)
                {
                    if (a.prototypeIndex < 0 || a.prototypeIndex >= sortes.Length || sortes[a.prototypeIndex] == null) continue;
                    var p = new Vector3(o.x + a.position.x * taille.x, o.y + a.position.y * taille.y, o.z + a.position.z * taille.z);
                    int ci = Mathf.FloorToInt(p.x / Cote), cj = Mathf.FloorToInt(p.z / Cote);
                    long cle = ((long)ci << 32) ^ (uint)cj;
                    if (!parCase.TryGetValue(cle, out var listes))
                    {
                        listes = new List<Matrix4x4>[sortes.Length];
                        parCase[cle] = listes;
                        hauteurs[cle] = new Vector2(p.y, p.y);
                    }
                    if (listes[a.prototypeIndex] == null) listes[a.prototypeIndex] = new List<Matrix4x4>();
                    listes[a.prototypeIndex].Add(Matrix4x4.TRS(p, Quaternion.Euler(0f, a.rotation * Mathf.Rad2Deg, 0f), new Vector3(a.widthScale, a.heightScale, a.widthScale)));
                    var h = hauteurs[cle];
                    hauteurs[cle] = new Vector2(Mathf.Min(h.x, p.y), Mathf.Max(h.y, p.y));
                    arbres++;
                }
                t.drawTreesAndFoliage = false;   // les collisions des arbres restent
            }
            foreach (var kv in parCase)
            {
                int ci = (int)(kv.Key >> 32), cj = (int)(uint)(kv.Key & 0xffffffff);
                var h = hauteurs[kv.Key];
                var centre = new Vector3((ci + .5f) * Cote, (h.x + h.y) * .5f + 6f, (cj + .5f) * Cote);
                var c = new Case
                {
                    centre = centre,
                    bornes = new Bounds(centre, new Vector3(Cote + 16f, h.y - h.x + 30f, Cote + 16f)),
                    parSorte = new Matrix4x4[sortes.Length][],
                };
                for (int k = 0; k < sortes.Length; k++) c.parSorte[k] = kv.Value[k]?.ToArray();
                cases.Add(c);
            }
            Debug.Log($"Aldermark : {arbres} arbres, buissons et rochers dessinés par lots ({cases.Count} cases de 128 m).");
        }

        void Update()
        {
            var cam = Camera.main;
            if (cam == null || sortes == null) return;
            GeometryUtility.CalculateFrustumPlanes(cam, plans);
            Vector3 c = cam.transform.position;
            foreach (var s in sortes) if (s != null) { s.ombre.Clear(); s.proche.Clear(); s.loin.Clear(); }
            float max = distanceMax + Cote;
            foreach (var cs in cases)
            {
                float dx = cs.centre.x - c.x, dz = cs.centre.z - c.z;
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d > max || !GeometryUtility.TestPlanesAABB(plans, cs.bornes)) continue;
                d = Mathf.Max(0f, d - Cote * .7f);   // le bord le plus proche de la case
                for (int k = 0; k < sortes.Length; k++)
                {
                    var m = cs.parSorte[k];
                    var s = sortes[k];
                    if (m == null || s == null) continue;
                    if (d < distanceOmbres) s.ombre.AddRange(m);
                    else if (d < distanceDetail) s.proche.AddRange(m);
                    else if (s.silhouette != null && d < distanceMax) s.loin.AddRange(m);
                }
            }
            var bornes = new Bounds(c, Vector3.one * (distanceMax * 2.2f));
            foreach (var s in sortes)
            {
                if (s == null) continue;
                for (int sm = 0; sm < s.materiaux.Length && sm < s.mesh.subMeshCount; sm++)
                {
                    Dessiner(s.mesh, sm, s.materiaux[sm], s.ombre, ShadowCastingMode.On, bornes);
                    Dessiner(s.mesh, sm, s.materiaux[sm], s.proche, ShadowCastingMode.Off, bornes);
                }
                if (s.silhouette != null) Dessiner(s.silhouette, 0, s.matSilhouette, s.loin, ShadowCastingMode.Off, bornes);
            }
        }

        static void Dessiner(Mesh mesh, int sousMaillage, Material mat, List<Matrix4x4> liste, ShadowCastingMode ombres, Bounds bornes)
        {
            if (liste.Count == 0 || mat == null) return;
            var rp = new RenderParams(mat) { shadowCastingMode = ombres, receiveShadows = true, worldBounds = bornes };
            for (int debut = 0; debut < liste.Count; debut += Lot)
                Graphics.RenderMeshInstanced(rp, mesh, sousMaillage, liste, Mathf.Min(Lot, liste.Count - debut), debut);
        }

        /// <summary>Une silhouette lointaine : un cône à 6 pans (sapin) ou une boule taillée à 6 pans (feuillu), à la taille de l'arbre.</summary>
        static Mesh Silhouette(Bounds b, bool sapin)
        {
            const int n = 6;
            float r = Mathf.Max(b.extents.x, b.extents.z) * (sapin ? .85f : .95f), h = b.size.y;
            var v = new List<Vector3>(); var t = new List<int>();
            float y0 = b.min.y + h * (sapin ? .12f : .3f), yMilieu = b.min.y + h * .62f, yHaut = b.max.y;
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                var p0 = new Vector3(Mathf.Cos(a0) * r, 0f, Mathf.Sin(a0) * r);
                var p1 = new Vector3(Mathf.Cos(a1) * r, 0f, Mathf.Sin(a1) * r);
                var haut = new Vector3(b.center.x, yHaut, b.center.z);
                if (sapin)
                {
                    int o = v.Count;
                    v.Add(haut); v.Add(new Vector3(b.center.x + p1.x, y0, b.center.z + p1.z)); v.Add(new Vector3(b.center.x + p0.x, y0, b.center.z + p0.z));
                    t.Add(o); t.Add(o + 1); t.Add(o + 2);
                }
                else
                {
                    var m0 = new Vector3(b.center.x + p0.x, yMilieu, b.center.z + p0.z); var m1 = new Vector3(b.center.x + p1.x, yMilieu, b.center.z + p1.z);
                    var bas = new Vector3(b.center.x, y0, b.center.z);
                    int o = v.Count;
                    v.Add(haut); v.Add(m1); v.Add(m0);
                    v.Add(bas); v.Add(m0); v.Add(m1);
                    t.Add(o); t.Add(o + 1); t.Add(o + 2);
                    t.Add(o + 3); t.Add(o + 4); t.Add(o + 5);
                }
            }
            var mesh = new Mesh { name = sapin ? "SilhouetteSapin" : "SilhouetteArbre" };
            mesh.SetVertices(v);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
