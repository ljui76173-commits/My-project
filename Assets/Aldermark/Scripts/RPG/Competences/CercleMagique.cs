using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// Les dessins des cercles d'incantation, tracés en code une fois pour toutes (blanc sur transparent, teintés ensuite) :
    /// l'anneau de runes, l'étoile (pentagramme, hexagramme… selon l'élément), l'anneau intérieur, le sigle de l'élément au centre,
    /// et un halo doux. Tous ont la même taille, pour s'empiler exactement.
    /// </summary>
    public static class TexturesMagiques
    {
        const int T = 512;
        const float C = T / 2f;

        static Texture2D exterieur, interieur, halo, tache;
        static readonly Dictionary<Element, Texture2D> etoiles = new Dictionary<Element, Texture2D>();
        static readonly Dictionary<Element, Texture2D> sigles = new Dictionary<Element, Texture2D>();
        static readonly Dictionary<Texture2D, Material> materiaux = new Dictionary<Texture2D, Material>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reinitialiser() { materiaux.Clear(); }

        /// <summary>Trace tout d'avance (au chargement), pour qu'aucun sort ne fige l'image la première fois.</summary>
        public static void Prechauffer()
        {
            _ = Exterieur; _ = Interieur; _ = Halo;
            foreach (var d in CatalogueCompetences.Ordre)
                if (d.EstSort) { Etoile(d.element); Sigle(d.element); }
        }

        /// <summary>Un matériau additif (il éclaire ce qu'il recouvre), visible des deux côtés, avec ce dessin.</summary>
        public static Material Materiau(Texture2D t)
        {
            if (materiaux.TryGetValue(t, out var m) && m != null) return m;
            m = Effets.Transparent(Color.white);
            m.SetFloat("_Blend", 2f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_Cull", 0f);
            m.SetTexture("_BaseMap", t);
            m.name = "Cercle_" + t.name;
            materiaux[t] = m;
            return m;
        }

        /// <summary>Un matériau normal (assombrissant) avec ce dessin : une tache de terre retournée.</summary>
        public static Material Sombre(Texture2D t, Color c)
        {
            var m = Effets.Transparent(c);
            m.SetTexture("_BaseMap", t);
            return m;
        }

        // ---------- les dessins ----------

        /// <summary>L'anneau extérieur : deux filets, des graduations, une couronne de runes.</summary>
        public static Texture2D Exterieur
        {
            get
            {
                if (exterieur != null) return exterieur;
                var a = new float[T * T];
                Cercle(a, 246, 5f); Cercle(a, 231, 2f); Cercle(a, 172, 3f); Cercle(a, 160, 1.5f);
                for (int k = 0; k < 90; k++)
                {
                    float ang = k * 4f * Mathf.Deg2Rad;
                    float r0 = k % 5 == 0 ? 233 : 236, r1 = k % 5 == 0 ? 244 : 241;
                    Segment(a, Mathf.Cos(ang) * r0, Mathf.Sin(ang) * r0, Mathf.Cos(ang) * r1, Mathf.Sin(ang) * r1, k % 5 == 0 ? 2f : 1.3f);
                }
                var hasard = new System.Random(1717);
                const int runes = 30;
                for (int k = 0; k < runes; k++)
                {
                    float ang = (k + .5f) * Mathf.PI * 2f / runes;
                    Rune(a, Mathf.Cos(ang) * 202, Mathf.Sin(ang) * 202, ang, 13f, 2.4f, hasard);
                }
                return exterieur = Fabriquer(a, "Exterieur");
            }
        }

        /// <summary>L'anneau intérieur : filets, tirets, rayons et points.</summary>
        public static Texture2D Interieur
        {
            get
            {
                if (interieur != null) return interieur;
                var a = new float[T * T];
                Cercle(a, 113, 2.5f); Cercle(a, 98, 1.5f); Cercle(a, 44, 2.5f);
                for (int k = 0; k < 24; k++)
                {
                    float a0 = k * 15f * Mathf.Deg2Rad;
                    Arc(a, 105.5f, a0, a0 + 7f * Mathf.Deg2Rad, 3.5f);
                }
                for (int k = 0; k < 12; k++)
                {
                    float ang = k * 30f * Mathf.Deg2Rad;
                    Segment(a, Mathf.Cos(ang) * 48, Mathf.Sin(ang) * 48, Mathf.Cos(ang) * 92, Mathf.Sin(ang) * 92, 1.5f);
                    float am = ang + 15f * Mathf.Deg2Rad;
                    Disque(a, Mathf.Cos(am) * 72, Mathf.Sin(am) * 72, 3.2f);
                }
                return interieur = Fabriquer(a, "Interieur");
            }
        }

        /// <summary>Un halo doux, plus vif au bord du cercle : la lueur de fond.</summary>
        public static Texture2D Halo
        {
            get
            {
                if (halo != null) return halo;
                var a = new float[T * T];
                for (int y = 0; y < T; y++)
                    for (int x = 0; x < T; x++)
                    {
                        float d = new Vector2(x + .5f - C, y + .5f - C).magnitude / 250f;
                        float bord = Mathf.Exp(-Mathf.Pow((d - .93f) / .06f, 2f));
                        float coeur = Mathf.Exp(-d * d * 3.5f) * .45f;
                        a[y * T + x] = Mathf.Clamp01((bord * .55f + coeur) * Mathf.Clamp01((1.02f - d) * 12f));
                    }
                return halo = Fabriquer(a, "Halo");
            }
        }

        /// <summary>Une tache ronde aux bords doux (terre retournée, brûlure).</summary>
        public static Texture2D Tache
        {
            get
            {
                if (tache != null) return tache;
                var a = new float[T * T];
                var h = new System.Random(5);
                for (int y = 0; y < T; y++)
                    for (int x = 0; x < T; x++)
                    {
                        float dx = x + .5f - C, dy = y + .5f - C;
                        float ang = Mathf.Atan2(dy, dx);
                        // un bord irrégulier
                        float bord = 230f * (1f + .08f * Mathf.Sin(ang * 5f + 1.3f) + .05f * Mathf.Sin(ang * 11f));
                        float d = Mathf.Sqrt(dx * dx + dy * dy) / bord;
                        a[y * T + x] = Mathf.Clamp01((1f - d) * 2.2f) * (.85f + .15f * (float)h.NextDouble());
                    }
                return tache = Fabriquer(a, "Tache");
            }
        }

        /// <summary>L'étoile du cercle, selon l'élément (feu : pentagramme ; glace : hexagramme ; foudre : octogramme…).</summary>
        public static Texture2D Etoile(Element e)
        {
            if (etoiles.TryGetValue(e, out var t) && t != null) return t;
            int n, k;
            switch (e)
            {
                case Element.Feu: n = 5; k = 2; break;
                case Element.Glace: case Element.Eau: n = 6; k = 2; break;
                case Element.Foudre: n = 8; k = 3; break;
                case Element.Vent: n = 7; k = 3; break;
                case Element.Terre: n = 8; k = 2; break;
                case Element.Lumiere: n = 7; k = 2; break;
                default: n = 9; k = 4; break;
            }
            var a = new float[T * T];
            const float R = 168f;
            var pts = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float ang = Mathf.PI * .5f + i * Mathf.PI * 2f / n;
                pts[i] = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * R;
            }
            for (int i = 0; i < n; i++) Segment(a, pts[i].x, pts[i].y, pts[(i + k) % n].x, pts[(i + k) % n].y, 3f);
            Cercle(a, R, 2f); Cercle(a, 120, 2.5f);
            foreach (var p in pts) Anneau(a, p.x, p.y, 11f, 2f);
            t = Fabriquer(a, "Etoile" + e);
            etoiles[e] = t;
            return t;
        }

        /// <summary>Le sigle de l'élément, au centre du cercle.</summary>
        public static Texture2D Sigle(Element e)
        {
            if (sigles.TryGetValue(e, out var t) && t != null) return t;
            var a = new float[T * T];
            const float ep = 3f;
            switch (e)
            {
                case Element.Feu:
                    Polygone(a, 3, 34f, 90f, ep); Polygone(a, 3, 15f, 270f, 2f);
                    break;
                case Element.Glace:
                    for (int k = 0; k < 3; k++)
                    {
                        float ang = k * 60f * Mathf.Deg2Rad;
                        Segment(a, Mathf.Cos(ang) * 36, Mathf.Sin(ang) * 36, -Mathf.Cos(ang) * 36, -Mathf.Sin(ang) * 36, ep);
                    }
                    for (int k = 0; k < 6; k++)
                    {
                        float ang = k * 60f * Mathf.Deg2Rad;
                        var p = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 22f;
                        foreach (float s in new[] { -1f, 1f })
                        {
                            float b = ang + s * 50f * Mathf.Deg2Rad;
                            Segment(a, p.x, p.y, p.x + Mathf.Cos(b) * 10f, p.y + Mathf.Sin(b) * 10f, 2.2f);
                        }
                    }
                    break;
                case Element.Foudre:
                    Segment(a, -6, 38, 10, 6, ep); Segment(a, 10, 6, -8, 2, ep); Segment(a, -8, 2, 8, -38, ep);
                    Anneau(a, 0, 0, 30f, 1.5f);
                    break;
                case Element.Vent:
                    Arc(a, 32f, 0f, 2.7f, ep); Arc(a, 22f, 2.1f, 4.9f, ep); Arc(a, 12f, 4.2f, 7.2f, ep);
                    break;
                case Element.Terre:
                    Polygone(a, 3, 36f, 270f, ep);
                    Segment(a, -22, 6, 22, 6, ep);
                    break;
                case Element.Eau:
                    Polygone(a, 3, 36f, 270f, ep);
                    break;
                case Element.Lumiere:
                    Anneau(a, 0, 0, 14f, 2.5f);
                    for (int k = 0; k < 8; k++)
                    {
                        float ang = k * 45f * Mathf.Deg2Rad;
                        Segment(a, Mathf.Cos(ang) * 21, Mathf.Sin(ang) * 21, Mathf.Cos(ang) * 36, Mathf.Sin(ang) * 36, 2.5f);
                    }
                    break;
                default:
                    Polygone(a, 4, 30f, 90f, ep);
                    break;
            }
            t = Fabriquer(a, "Sigle" + e);
            sigles[e] = t;
            return t;
        }

        // ---------- le tracé ----------

        static Texture2D Fabriquer(float[] a, string nom)
        {
            var px = new Color32[T * T];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a[i]) * 255f));
            var t = new Texture2D(T, T, TextureFormat.RGBA32, true) { name = nom, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            t.SetPixels32(px);
            t.Apply(true, true);
            return t;
        }

        static void Poser(float[] a, int x, int y, float v)
        {
            if (x < 0 || y < 0 || x >= T || y >= T || v <= 0f) return;
            int i = y * T + x;
            if (v > a[i]) a[i] = v;
        }

        /// <summary>Un cercle de rayon r (pixels, depuis le centre), d'épaisseur ep.</summary>
        static void Cercle(float[] a, float r, float ep) => Anneau(a, 0, 0, r, ep);

        static void Anneau(float[] a, float cx, float cy, float r, float ep)
        {
            int x0 = Mathf.FloorToInt(C + cx - r - ep - 1), x1 = Mathf.CeilToInt(C + cx + r + ep + 1);
            int y0 = Mathf.FloorToInt(C + cy - r - ep - 1), y1 = Mathf.CeilToInt(C + cy + r + ep + 1);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float d = Mathf.Abs(new Vector2(x + .5f - C - cx, y + .5f - C - cy).magnitude - r);
                    if (d > ep) continue;
                    Poser(a, x, y, Mathf.Clamp01(ep * .5f + .5f - d));
                }
        }

        static void Disque(float[] a, float cx, float cy, float r)
        {
            for (int y = Mathf.FloorToInt(C + cy - r - 1); y <= Mathf.CeilToInt(C + cy + r + 1); y++)
                for (int x = Mathf.FloorToInt(C + cx - r - 1); x <= Mathf.CeilToInt(C + cx + r + 1); x++)
                {
                    float d = new Vector2(x + .5f - C - cx, y + .5f - C - cy).magnitude;
                    Poser(a, x, y, Mathf.Clamp01(r + .5f - d));
                }
        }

        /// <summary>Un arc de cercle centré, d'un angle à l'autre (radians, sens trigonométrique).</summary>
        static void Arc(float[] a, float r, float a0, float a1, float ep)
        {
            int n = Mathf.Max(4, Mathf.CeilToInt(Mathf.Abs(a1 - a0) * r / 4f));
            for (int i = 0; i < n; i++)
            {
                float u0 = Mathf.Lerp(a0, a1, i / (float)n), u1 = Mathf.Lerp(a0, a1, (i + 1) / (float)n);
                Segment(a, Mathf.Cos(u0) * r, Mathf.Sin(u0) * r, Mathf.Cos(u1) * r, Mathf.Sin(u1) * r, ep);
            }
        }

        static void Polygone(float[] a, int n, float r, float depart, float ep)
        {
            for (int i = 0; i < n; i++)
            {
                float u0 = (depart + i * 360f / n) * Mathf.Deg2Rad, u1 = (depart + (i + 1) * 360f / n) * Mathf.Deg2Rad;
                Segment(a, Mathf.Cos(u0) * r, Mathf.Sin(u0) * r, Mathf.Cos(u1) * r, Mathf.Sin(u1) * r, ep);
            }
        }

        /// <summary>Un trait d'épaisseur ep, bords adoucis (coordonnées depuis le centre).</summary>
        static void Segment(float[] a, float x0, float y0, float x1, float y1, float ep)
        {
            var p0 = new Vector2(x0 + C, y0 + C);
            var p1 = new Vector2(x1 + C, y1 + C);
            Vector2 d = p1 - p0;
            float l2 = Mathf.Max(1e-4f, d.sqrMagnitude);
            int xa = Mathf.FloorToInt(Mathf.Min(p0.x, p1.x) - ep - 1), xb = Mathf.CeilToInt(Mathf.Max(p0.x, p1.x) + ep + 1);
            int ya = Mathf.FloorToInt(Mathf.Min(p0.y, p1.y) - ep - 1), yb = Mathf.CeilToInt(Mathf.Max(p0.y, p1.y) + ep + 1);
            for (int y = ya; y <= yb; y++)
                for (int x = xa; x <= xb; x++)
                {
                    var p = new Vector2(x + .5f, y + .5f);
                    float u = Mathf.Clamp01(Vector2.Dot(p - p0, d) / l2);
                    float dist = (p - (p0 + d * u)).magnitude;
                    Poser(a, x, y, Mathf.Clamp01(ep * .5f + .5f - dist));
                }
        }

        /// <summary>Une rune inventée : quelques traits entre les nœuds d'une grille 3×3, tournée face au centre.</summary>
        static void Rune(float[] a, float cx, float cy, float angle, float taille, float ep, System.Random h)
        {
            // Repère de la rune : « haut » vers l'extérieur du cercle, « droite » le long de l'anneau
            var haut = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var droite = new Vector2(-haut.y, haut.x);
            Vector2 Noeud(int i, int j) => new Vector2(cx, cy) + droite * ((i - 1) * taille * .6f) + haut * ((j - 1) * taille);
            // Une hampe verticale presque toujours, puis 2 à 3 traits
            int colonne = h.Next(3);
            if (h.NextDouble() < .8) { var p = Noeud(colonne, 0); var q = Noeud(colonne, 2); Segment(a, p.x, p.y, q.x, q.y, ep); }
            int traits = 2 + h.Next(2);
            for (int k = 0; k < traits; k++)
            {
                int i0 = h.Next(3), j0 = h.Next(3);
                int i1 = Mathf.Clamp(i0 + h.Next(3) - 1, 0, 2), j1 = Mathf.Clamp(j0 + h.Next(3) - 1, 0, 2);
                if (i0 == i1 && j0 == j1) i1 = (i0 + 1) % 3;
                var p = Noeud(i0, j0); var q = Noeud(i1, j1);
                Segment(a, p.x, p.y, q.x, q.y, ep);
            }
            if (h.NextDouble() < .3) { var p = Noeud(h.Next(3), h.Next(3)); Disque(a, p.x, p.y, ep * .9f); }
        }
    }

    /// <summary>
    /// Un cercle d'incantation : plusieurs disques de runes qui tournent en sens contraires, apparaissent un à un,
    /// brillent à mesure que l'énergie monte, puis éclatent au lancer. Plus le sort est haut, plus il y a d'anneaux :
    /// anneau intérieur au niveau 4, second anneau et petits cercles en orbite au niveau 7, troisième anneau au 10.
    /// </summary>
    public class CercleMagique : MonoBehaviour
    {
        class Couche
        {
            public Transform t;
            public MeshRenderer r;
            public float vitesse, alpha, echelle, apparition, orbite, phase;
        }

        readonly List<Couche> couches = new List<Couche>();
        MaterialPropertyBlock bloc;
        Color couleur;
        Light lumiere;
        float rayon, t, progres, tFin = -1f;
        bool libere, annule;
        static readonly int IdCouleur = Shader.PropertyToID("_BaseColor");

        /// <summary>De 0 à 1 : l'énergie rassemblée (le cercle s'avive et tourne plus vite).</summary>
        public float Progres { set => progres = Mathf.Clamp01(value); }
        public float Rayon => rayon;

        public static CercleMagique Creer(Vector3 position, Quaternion rotation, float rayon, Color couleur, Element e, int palier, bool lumiere = true)
        {
            var go = new GameObject("CercleMagique");
            go.transform.SetPositionAndRotation(position, rotation);
            var c = go.AddComponent<CercleMagique>();
            c.rayon = rayon; c.couleur = couleur;
            c.bloc = new MaterialPropertyBlock();
            c.Ajouter(TexturesMagiques.Halo, 1.12f, 0f, .4f, 0f);
            c.Ajouter(TexturesMagiques.Exterieur, 1f, 16f, 1f, 0f);
            c.Ajouter(TexturesMagiques.Etoile(e), 1f, -26f, .95f, .06f);
            c.Ajouter(TexturesMagiques.Sigle(e), 1f, 0f, 1f, .14f);
            if (palier >= 2) c.Ajouter(TexturesMagiques.Interieur, 1f, 48f, .9f, .1f);
            if (palier >= 3)
            {
                c.Ajouter(TexturesMagiques.Exterieur, 1.3f, -9f, .5f, .2f);
                int n = palier >= 4 ? 6 : 4;
                for (int k = 0; k < n; k++)
                {
                    var o = c.Ajouter(TexturesMagiques.Interieur, .2f, 120f, .85f, .24f + k * .03f);
                    o.orbite = 1.3f; o.phase = k * Mathf.PI * 2f / n;
                }
            }
            if (palier >= 4)
            {
                c.Ajouter(TexturesMagiques.Exterieur, 1.6f, 6f, .32f, .3f);
                c.Ajouter(TexturesMagiques.Etoile(e), 1.3f, 14f, .35f, .32f);
            }
            if (lumiere)
            {
                var lg = new GameObject("Lumiere");
                lg.transform.SetParent(go.transform, false);
                lg.transform.localPosition = new Vector3(0, 0, -.3f);
                c.lumiere = lg.AddComponent<Light>();
                c.lumiere.type = LightType.Point; c.lumiere.color = couleur; c.lumiere.range = Mathf.Max(2.5f, rayon * 4f);
                c.lumiere.intensity = 0f; c.lumiere.shadows = LightShadows.None;
            }
            return c;
        }

        Couche Ajouter(Texture2D tex, float echelle, float vitesse, float alpha, float apparition)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.name = tex.name;
            q.transform.SetParent(transform, false);
            q.transform.localPosition = new Vector3(0, 0, -.002f * couches.Count);
            q.transform.localScale = Vector3.zero;
            var r = q.GetComponent<MeshRenderer>();
            r.sharedMaterial = TexturesMagiques.Materiau(tex);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var c = new Couche { t = q.transform, r = r, vitesse = vitesse, alpha = alpha, echelle = echelle, apparition = apparition, phase = Random.value * 6.28f };
            couches.Add(c);
            return c;
        }

        /// <summary>Le sort part : le cercle s'élargit d'un coup et s'efface.</summary>
        public void Liberer()
        {
            if (libere || annule) return;
            libere = true; tFin = t;
        }

        /// <summary>Le sort est interrompu : le cercle se défait.</summary>
        public void Annuler()
        {
            if (libere || annule) return;
            annule = true; tFin = t;
        }

        static float Rebond(float x)
        {
            // Apparition qui dépasse un peu puis se pose
            const float c1 = 1.9f, c3 = c1 + 1f;
            x -= 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;
            float fondu = 1f, gonfle = 1f;
            if (libere)
            {
                float u = Mathf.Clamp01((t - tFin) / .42f);
                gonfle = 1f + .75f * (1f - (1f - u) * (1f - u));
                fondu = 1f - u;
                if (u >= 1f) { Destroy(gameObject); return; }
            }
            else if (annule)
            {
                float u = Mathf.Clamp01((t - tFin) / .22f);
                gonfle = 1f - .4f * u;
                fondu = 1f - u;
                if (u >= 1f) { Destroy(gameObject); return; }
            }
            float vitesse = 1f + 1.8f * progres + (libere ? 4f : 0f);
            float eclat = (.5f + .5f * progres) * (libere ? 1.6f : 1f);
            foreach (var c in couches)
            {
                float a = Mathf.Clamp01((t - c.apparition) / .24f);
                if (a <= 0f) { c.t.localScale = Vector3.zero; continue; }
                float s = (a < 1f ? Rebond(a) : 1f) * c.echelle * rayon * 2f * gonfle;
                c.t.localScale = new Vector3(s, s, s);
                c.t.localRotation = Quaternion.Euler(0, 0, c.t.localEulerAngles.z + c.vitesse * vitesse * dt);
                if (c.orbite > 0f)
                {
                    float ang = c.phase + t * .9f * vitesse;
                    c.t.localPosition = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), -.004f) * (c.orbite * rayon * gonfle);
                }
                float scint = 1f + .12f * Mathf.Sin(t * 17f + c.phase);
                float alpha = c.alpha * Mathf.Clamp01(a * 1.5f) * eclat * scint * fondu;
                bloc.SetColor(IdCouleur, new Color(couleur.r * 1.6f, couleur.g * 1.6f, couleur.b * 1.6f, Mathf.Clamp01(alpha)));
                c.r.SetPropertyBlock(bloc);
            }
            if (lumiere != null) lumiere.intensity = (1f + 2.5f * progres) * fondu * (libere ? 2f : 1f);
        }
    }
}
