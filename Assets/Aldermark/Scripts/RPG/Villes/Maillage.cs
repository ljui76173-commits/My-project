using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// Un repère local : une origine et trois axes (x à gauche quand on regarde la façade de face, y vers le haut,
    /// z vers l'extérieur). Toujours direct : les faces gardent leur sens, donc restent visibles du bon côté.
    /// </summary>
    public struct Repere
    {
        public Vector3 o, x, y, z;

        public static Repere Lacet(Vector3 origine, float degres)
        {
            float a = degres * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Repere { o = origine, x = new Vector3(c, 0f, -s), y = new Vector3(0f, 1f, 0f), z = new Vector3(s, 0f, c) };
        }

        public Vector3 P(float a, float b, float c) => new Vector3(
            o.x + x.x * a + y.x * b + z.x * c,
            o.y + x.y * a + y.y * b + z.y * c,
            o.z + x.z * a + y.z * b + z.z * c);

        public Vector3 P(Vector3 l) => P(l.x, l.y, l.z);

        /// <summary>Un repère enfant : décalé (en coordonnées locales) et tourné autour de y.</summary>
        public Repere Sous(float a, float b, float c, float degres = 0f)
        {
            float r = degres * Mathf.Deg2Rad, cs = Mathf.Cos(r), sn = Mathf.Sin(r);
            var nx = new Vector3(x.x * cs - z.x * sn, x.y * cs - z.y * sn, x.z * cs - z.z * sn);
            var nz = new Vector3(x.x * sn + z.x * cs, x.y * sn + z.y * cs, x.z * sn + z.z * cs);
            return new Repere { o = P(a, b, c), x = nx, y = y, z = nz };
        }

        /// <summary>Le lacet (degrés) de ce repère dans le monde.</summary>
        public float LacetMonde => Mathf.Atan2(z.x, z.z) * Mathf.Rad2Deg;
    }

    /// <summary>
    /// Un maillage en construction : des faces plates (chaque face ses propres sommets, pour le style « taillé »),
    /// une couleur par sommet (nuancée face par face, assombrie près du sol). Une seule matière pour tout un quartier.
    /// </summary>
    public class Maillage
    {
        public readonly List<Vector3> v = new List<Vector3>(8192);
        public readonly List<Color32> c = new List<Color32>(8192);
        public readonly List<Vector3> n = new List<Vector3>(8192);   // normales plates (une par face)
        public readonly List<int> t = new List<int>(16384);

        /// <summary>Le repère courant : toutes les coordonnées passées aux méthodes y sont exprimées.</summary>
        public Repere r = Repere.Lacet(Vector3.zero, 0f);
        /// <summary>Retranché des positions du monde (origine du morceau de maillage).</summary>
        public Vector3 origine;
        /// <summary>La hauteur du sol (monde) : les murs s'assombrissent en approchant du sol.</summary>
        public float sol = -10000f;
        /// <summary>Écart de teinte d'une face à l'autre (0 : aucune).</summary>
        public float nuance = .05f;
        int compteur;

        public int Triangles => t.Count / 3;

        float Bruit() { unchecked { compteur++; uint h = (uint)(compteur * 374761393 + 668265263); h = (h ^ (h >> 13)) * 1274126177u; return ((h ^ (h >> 16)) & 0xFFFF) / 65535f; } }

        Color32 Teinte(Color col, Vector3 p, float k)
        {
            float bas = sol < -9000f ? 1f : .8f + .2f * Mathf.Clamp01((p.y - sol + .3f) / 2.6f);
            float f = k * bas;
            return new Color32((byte)(Mathf.Clamp01(col.r * f) * 255f), (byte)(Mathf.Clamp01(col.g * f) * 255f), (byte)(Mathf.Clamp01(col.b * f) * 255f), 255);
        }

        void Sommet(Vector3 monde, Color col, float k, Vector3 normale)
        {
            v.Add(new Vector3(monde.x - origine.x, monde.y - origine.y, monde.z - origine.z));
            c.Add(Teinte(col, monde, k));
            n.Add(normale);
        }

        public void Vider() { v.Clear(); c.Clear(); n.Clear(); t.Clear(); compteur = 0; }

        // ---------- faces ----------

        /// <summary>Un quadrilatère ; a, b, c, d dans le sens des aiguilles d'une montre vu de l'extérieur.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Color col)
        {
            float k = 1f + (Bruit() - .5f) * 2f * nuance;
            int i = v.Count;
            Vector3 A = r.P(a), B = r.P(b), Cm = r.P(cc), Dm = r.P(d);
            var nm = Normale(A, B, Cm);
            Sommet(A, col, k, nm); Sommet(B, col, k, nm); Sommet(Cm, col, k, nm); Sommet(Dm, col, k, nm);
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
            t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }

        public void Tri(Vector3 a, Vector3 b, Vector3 cc, Color col)
        {
            float k = 1f + (Bruit() - .5f) * 2f * nuance;
            int i = v.Count;
            Vector3 A = r.P(a), B = r.P(b), Cm = r.P(cc);
            var nm = Normale(A, B, Cm);
            Sommet(A, col, k, nm); Sommet(B, col, k, nm); Sommet(Cm, col, k, nm);
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
        }

        [Flags]
        public enum Faces { Avant = 1, Arriere = 2, Droite = 4, Gauche = 8, Dessus = 16, Dessous = 32, Toutes = 63, SansDessous = 31, Cotes = 15 }

        /// <summary>Une boîte alignée sur le repère courant (« avant » : +z, « droite » : +x).</summary>
        public void Boite(float x0, float y0, float z0, float x1, float y1, float z1, Color col, Faces f = Faces.SansDessous)
        {
            if ((f & Faces.Avant) != 0) Quad(new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), new Vector3(x0, y0, z1), new Vector3(x1, y0, z1), col);
            if ((f & Faces.Arriere) != 0) Quad(new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x1, y0, z0), new Vector3(x0, y0, z0), col);
            if ((f & Faces.Droite) != 0) Quad(new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x1, y0, z1), new Vector3(x1, y0, z0), col);
            if ((f & Faces.Gauche) != 0) Quad(new Vector3(x0, y1, z1), new Vector3(x0, y1, z0), new Vector3(x0, y0, z0), new Vector3(x0, y0, z1), col);
            if ((f & Faces.Dessus) != 0) Quad(new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), new Vector3(x1, y1, z0), new Vector3(x0, y1, z0), col);
            if ((f & Faces.Dessous) != 0) Quad(new Vector3(x1, y0, z1), new Vector3(x0, y0, z1), new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), col);
        }

        public void BoiteCentre(float cx, float y0, float cz, float lx, float h, float lz, Color col, Faces f = Faces.SansDessous)
            => Boite(cx - lx * .5f, y0, cz - lz * .5f, cx + lx * .5f, y0 + h, cz + lz * .5f, col, f);

        /// <summary>
        /// Une dalle épaisse (pan de toit, auvent) : a, b, c, d = la face du dessus, dans le sens des aiguilles d'une montre vue d'au-dessus ;
        /// l'épaisseur part vers le dessous.
        /// </summary>
        public void Dalle(Vector3 a, Vector3 b, Vector3 cc, Vector3 d, float ep, Color dessus, Color tranche)
        {
            var n = Normale(a, b, cc) * ep;
            Vector3 a2 = a - n, b2 = b - n, c2 = cc - n, d2 = d - n;
            Quad(a, b, cc, d, dessus);
            Quad(d2, c2, b2, a2, tranche * .8f);
            Quad(b, a, a2, b2, tranche);
            Quad(cc, b, b2, c2, tranche);
            Quad(d, cc, c2, d2, tranche);
            Quad(a, d, d2, a2, tranche);
        }

        static Vector3 Normale(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 u = new Vector3(b.x - a.x, b.y - a.y, b.z - a.z), w = new Vector3(c.x - a.x, c.y - a.y, c.z - a.z);
            var n = new Vector3(u.y * w.z - u.z * w.y, u.z * w.x - u.x * w.z, u.x * w.y - u.y * w.x);
            float m = Mathf.Sqrt(n.x * n.x + n.y * n.y + n.z * n.z);
            return m > 1e-6f ? new Vector3(n.x / m, n.y / m, n.z / m) : new Vector3(0, 1, 0);
        }

        /// <summary>Une bande plate posée sur une façade (z = décalage) : une pièce de bois en diagonale, par exemple.</summary>
        public void Bande(float u0, float v0, float u1, float v1, float largeur, float z, Color col)
        {
            float du = u1 - u0, dv = v1 - v0, l = Mathf.Sqrt(du * du + dv * dv);
            if (l < 1e-4f) return;
            float nu = -dv / l * largeur * .5f, nv = du / l * largeur * .5f;
            // p0 → p1 → p2 → p3 tourne toujours dans le même sens sur le papier (u, v) ; vu de face, x part vers la gauche : on l'inverse
            var p0 = new Vector3(u0 + nu, v0 + nv, z); var p1 = new Vector3(u1 + nu, v1 + nv, z);
            var p2 = new Vector3(u1 - nu, v1 - nv, z); var p3 = new Vector3(u0 - nu, v0 - nv, z);
            Quad(p1, p0, p3, p2, col);
        }

        // ---------- volumes ronds ----------

        public void Cylindre(float cx, float y0, float cz, float rayon, float h, int seg, Color col, bool dessus = true, float rayonHaut = -1f)
        {
            if (rayonHaut < 0f) rayonHaut = rayon;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                var b0 = new Vector3(cx + Mathf.Cos(a0) * rayon, y0, cz + Mathf.Sin(a0) * rayon);
                var b1 = new Vector3(cx + Mathf.Cos(a1) * rayon, y0, cz + Mathf.Sin(a1) * rayon);
                var h0 = new Vector3(cx + Mathf.Cos(a0) * rayonHaut, y0 + h, cz + Mathf.Sin(a0) * rayonHaut);
                var h1 = new Vector3(cx + Mathf.Cos(a1) * rayonHaut, y0 + h, cz + Mathf.Sin(a1) * rayonHaut);
                Quad(h0, h1, b1, b0, col);
                if (dessus) Tri(new Vector3(cx, y0 + h, cz), h1, h0, col);
            }
        }

        public void Cone(float cx, float y0, float cz, float rayon, float h, int seg, Color col)
        {
            var sommet = new Vector3(cx, y0 + h, cz);
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                Tri(sommet, new Vector3(cx + Mathf.Cos(a1) * rayon, y0, cz + Mathf.Sin(a1) * rayon), new Vector3(cx + Mathf.Cos(a0) * rayon, y0, cz + Mathf.Sin(a0) * rayon), col);
            }
        }

        /// <summary>Une pyramide à base rectangulaire (toit en pavillon, flèche de clocher).</summary>
        public void Pyramide(float cx, float y0, float cz, float demiX, float demiZ, float h, Color col)
        {
            var s = new Vector3(cx, y0 + h, cz);
            Vector3 a = new Vector3(cx - demiX, y0, cz + demiZ), b = new Vector3(cx + demiX, y0, cz + demiZ);
            Vector3 cc = new Vector3(cx + demiX, y0, cz - demiZ), d = new Vector3(cx - demiX, y0, cz - demiZ);
            Tri(s, a, b, col);   // avant (+z)
            Tri(s, b, cc, col);  // droite
            Tri(s, cc, d, col);  // arrière
            Tri(s, d, a, col);   // gauche
        }

        /// <summary>Un disque face à +z (rosace, oculus), centré en (cx, cy), dans le plan z.</summary>
        public void Disque(float cx, float cy, float z, float rayon, int seg, Color col)
        {
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                Tri(new Vector3(cx, cy, z), new Vector3(cx + Mathf.Cos(a0) * rayon, cy + Mathf.Sin(a0) * rayon, z), new Vector3(cx + Mathf.Cos(a1) * rayon, cy + Mathf.Sin(a1) * rayon, z), col);
            }
        }

        /// <summary>Des créneaux sur le pourtour d'un cercle (tours) : merlons tous les ~1,6 m.</summary>
        public void CreneauxRonds(float cx, float y0, float cz, float rayon, float ep, float h, Color col)
        {
            int n = Mathf.Max(6, Mathf.RoundToInt(rayon * Mathf.PI * 2f / 1.6f));
            n -= n % 2;
            var sauve = r;
            for (int i = 0; i < n; i += 2)
            {
                float a = (i + .5f) * 360f / n;
                r = sauve.Sous(cx, y0, cz, 90f - a);
                float l = rayon * Mathf.PI * 2f / n * 1.05f;
                Boite(-l * .5f, 0f, rayon - ep, l * .5f, h, rayon, col, Faces.SansDessous);
            }
            r = sauve;
        }
    }
}
