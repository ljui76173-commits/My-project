using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aldermark
{
    /// <summary>Petits maillages low-poly à facettes, générés en code.</summary>
    public static class Formes
    {
        // Construit un maillage à facettes (sommets non partagés) à partir de triangles.
        class Constructeur
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<List<int>> sous = new List<List<int>>();
            public Constructeur(int n) { for (int i = 0; i < n; i++) sous.Add(new List<int>()); }
            public void Tri(int s, Vector3 a, Vector3 b, Vector3 c)
            {
                int i = v.Count; v.Add(a); v.Add(b); v.Add(c);
                sous[s].Add(i); sous[s].Add(i + 1); sous[s].Add(i + 2);
            }
            public void Quad(int s, Vector3 a, Vector3 b, Vector3 c, Vector3 d) { Tri(s, a, b, c); Tri(s, a, c, d); }
            public Mesh Fin(string nom)
            {
                var m = new Mesh { name = nom };
                if (v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
                m.SetVertices(v);
                m.subMeshCount = sous.Count;
                for (int i = 0; i < sous.Count; i++) m.SetTriangles(sous[i], i);
                m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            }
        }

        static void Cone(Constructeur k, int s, float r, float h, float y0, int seg)
        {
            var top = new Vector3(0, y0 + h, 0);
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2 / seg, a1 = (i + 1) * Mathf.PI * 2 / seg;
                var p0 = new Vector3(Mathf.Cos(a0) * r, y0, Mathf.Sin(a0) * r);
                var p1 = new Vector3(Mathf.Cos(a1) * r, y0, Mathf.Sin(a1) * r);
                k.Tri(s, p0, top, p1);
                k.Tri(s, p1, new Vector3(0, y0, 0), p0);
            }
        }

        static void Cylindre(Constructeur k, int s, float rb, float rh, float h, float y0, int seg)
        {
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2 / seg, a1 = (i + 1) * Mathf.PI * 2 / seg;
                var b0 = new Vector3(Mathf.Cos(a0) * rb, y0, Mathf.Sin(a0) * rb);
                var b1 = new Vector3(Mathf.Cos(a1) * rb, y0, Mathf.Sin(a1) * rb);
                var h0 = new Vector3(Mathf.Cos(a0) * rh, y0 + h, Mathf.Sin(a0) * rh);
                var h1 = new Vector3(Mathf.Cos(a1) * rh, y0 + h, Mathf.Sin(a1) * rh);
                k.Quad(s, b0, h0, h1, b1);
                k.Tri(s, h0, new Vector3(0, y0 + h, 0), h1);
            }
        }

        static void Icosa(Constructeur k, int s, Vector3 centre, Vector3 echelle)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var p = new[] {
                new Vector3(-1,t,0), new Vector3(1,t,0), new Vector3(-1,-t,0), new Vector3(1,-t,0),
                new Vector3(0,-1,t), new Vector3(0,1,t), new Vector3(0,-1,-t), new Vector3(0,1,-t),
                new Vector3(t,0,-1), new Vector3(t,0,1), new Vector3(-t,0,-1), new Vector3(-t,0,1) };
            int[] f = { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                        3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
            for (int i = 0; i < f.Length; i += 3)
            {
                Vector3 A = Vector3.Scale(p[f[i]].normalized, echelle) + centre;
                Vector3 B = Vector3.Scale(p[f[i + 1]].normalized, echelle) + centre;
                Vector3 C = Vector3.Scale(p[f[i + 2]].normalized, echelle) + centre;
                k.Tri(s, A, B, C);
            }
        }

        /// <summary>Sapin : sous-maillage 0 = tronc, 1 = feuillage.</summary>
        public static Mesh Sapin()
        {
            var k = new Constructeur(2);
            Cylindre(k, 0, .4f, .3f, 2.2f, 0, 5);
            Cone(k, 1, 2.7f, 4.4f, 1.4f, 7);
            Cone(k, 1, 2.1f, 3.8f, 3.8f, 7);
            Cone(k, 1, 1.4f, 3.2f, 6.1f, 7);
            return k.Fin("Sapin");
        }

        /// <summary>Arbre doré : sous-maillage 0 = tronc, 1 = feuillage.</summary>
        public static Mesh ArbreDore()
        {
            var k = new Constructeur(2);
            Cylindre(k, 0, .35f, .25f, 3f, 0, 5);
            Icosa(k, 1, new Vector3(0, 4.6f, 0), new Vector3(2.3f, 2.9f, 2.3f));
            return k.Fin("ArbreDore");
        }

        public static Mesh Buisson()
        {
            var k = new Constructeur(1);
            Icosa(k, 0, new Vector3(0, .45f, 0), new Vector3(1.4f, .85f, 1.4f));
            return k.Fin("Buisson");
        }

        /// <summary>Toit à deux pans de 1 x 1 x 1 (à mettre à l'échelle).</summary>
        public static Mesh Toit()
        {
            var k = new Constructeur(1);
            Vector3 a = new Vector3(-.5f, 0, -.5f), b = new Vector3(.5f, 0, -.5f), c = new Vector3(0, 1, -.5f);
            Vector3 a2 = new Vector3(-.5f, 0, .5f), b2 = new Vector3(.5f, 0, .5f), c2 = new Vector3(0, 1, .5f);
            k.Tri(0, a, c, b); k.Tri(0, a2, b2, c2);
            k.Quad(0, a, a2, c2, c); k.Quad(0, b, c, c2, b2);
            k.Quad(0, a, b, b2, a2);
            return k.Fin("Toit");
        }

        /// <summary>Anneau de montagnes lointaines autour de la vallée.</summary>
        public static Mesh Cordillere(float rInt, float rSommet, float rExt, float hBase, float hMin, float hMax, int seg, float graine)
        {
            var k = new Constructeur(1);
            Vector3 P(float r, float ang, float h) => new Vector3(Mathf.Cos(ang) * r, h, Mathf.Sin(ang) * r);
            float Pic(int i)
            {
                float ang = i * Mathf.PI * 2 / seg;
                float n = Monde.Fbm(Mathf.Cos(ang) * 2.2f + graine, Mathf.Sin(ang) * 2.2f + graine);
                float c = 1f - Mathf.Abs(Monde.Fbm(Mathf.Cos(ang) * 6f + graine, Mathf.Sin(ang) * 6f) * 2f - 1f);
                return hMin + (hMax - hMin) * Mathf.Clamp01(Monde.Lisse(.25f, .75f, n) * .75f + c * .35f);
            }
            float Decal(int i) { float ang = i * Mathf.PI * 2 / seg; return (Monde.Fbm(Mathf.Cos(ang) * 4f + graine * 2, Mathf.Sin(ang) * 4f) - .5f) * (rExt - rInt) * .4f; }
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2 / seg, a1 = (i + 1) * Mathf.PI * 2 / seg;
                Vector3 i0 = P(rInt, a0, hBase), i1 = P(rInt, a1, hBase);
                Vector3 s0 = P(rSommet + Decal(i), a0, Pic(i)), s1 = P(rSommet + Decal(i + 1), a1, Pic(i + 1));
                Vector3 e0 = P(rExt, a0, hBase), e1 = P(rExt, a1, hBase);
                k.Quad(0, i0, i1, s1, s0);
                k.Quad(0, e0, s0, s1, e1);
            }
            return k.Fin("Cordillere");
        }

        public static Mesh Anneau(float r, float e, int seg = 18, int segTube = 6)
        {
            var k = new Constructeur(1);
            for (int i = 0; i < seg; i++)
                for (int j = 0; j < segTube; j++)
                {
                    Vector3 P(int ii, int jj)
                    {
                        float u = ii * Mathf.PI * 2 / seg, w = jj * Mathf.PI * 2 / segTube;
                        float rr = r + e * Mathf.Cos(w);
                        return new Vector3(Mathf.Cos(u) * rr, Mathf.Sin(u) * rr, e * Mathf.Sin(w));
                    }
                    k.Quad(0, P(i, j), P(i + 1, j), P(i + 1, j + 1), P(i, j + 1));
                }
            return k.Fin("Anneau");
        }
    }
}
