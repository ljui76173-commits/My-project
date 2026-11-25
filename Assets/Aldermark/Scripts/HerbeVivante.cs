using UnityEngine;
using UnityEngine.Rendering;

namespace Aldermark
{
    /// <summary>
    /// Herbe vivante autour d'Aldric : ~12 000 touffes dessinées en instanciation sur une zone
    /// de 72 m qui suit le joueur. Les touffes qui sortent de la zone sont recyclées devant lui.
    /// Le shader Aldermark/HerbeVivante gère le vent et l'herbe qui s'écarte.
    /// </summary>
    public class HerbeVivante : MonoBehaviour
    {
        public Transform joueur;
        public Material materiau;
        public float taille = 72f;
        public int cote = 110;

        Mesh touffe;
        Matrix4x4[] matrices;
        Vector2[] bases, actuelles;
        RenderParams rp;
        int nombre;
        static readonly Matrix4x4 Vide = Matrix4x4.Scale(Vector3.zero);
        const int Lot = 1000;

        void Start()
        {
            if (materiau == null) materiau = new Material(Shader.Find("Aldermark/HerbeVivante"));
            materiau.enableInstancing = true;
            touffe = CreerTouffe();
            nombre = cote * cote;
            matrices = new Matrix4x4[nombre];
            bases = new Vector2[nombre];
            actuelles = new Vector2[nombre];
            for (int i = 0; i < nombre; i++)
            {
                bases[i] = new Vector2(((i % cote) + Monde.HashPublic(i, .3f)) * taille / cote, ((i / cote) + Monde.HashPublic(i, .9f)) * taille / cote);
                actuelles[i] = new Vector2(float.NaN, float.NaN);
            }
            rp = new RenderParams(materiau)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true,
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 5000f)
            };
        }

        void Update()
        {
            if (joueur == null) return;
            Vector3 p = joueur.position;
            Shader.SetGlobalVector("_AldJoueur", p);
            for (int i = 0; i < nombre; i++)
            {
                Vector2 b = bases[i];
                float x = b.x + taille * Mathf.Round((p.x - b.x) / taille);
                float z = b.y + taille * Mathf.Round((p.z - b.y) / taille);
                if (x != actuelles[i].x || z != actuelles[i].y)
                {
                    actuelles[i] = new Vector2(x, z);
                    matrices[i] = Placer(i, x, z);
                }
            }
            for (int debut = 0; debut < nombre; debut += Lot)
                Graphics.RenderMeshInstanced(rp, touffe, 0, matrices, Mathf.Min(Lot, nombre - debut), debut);
        }

        static Matrix4x4 Placer(int i, float x, float z)
        {
            float y = Monde.Hauteur(x, z);
            if (y < .9f || y > 48f || Monde.DistanceRoute(x, z) < 1.7f || Vector2.Distance(new Vector2(x, z), Monde.Village) < 11.5f || Vector2.Distance(new Vector2(x, z), Monde.MaisonHeros) < 5.5f) return Vide;
            if (Mathf.Abs(Monde.Hauteur(x + .8f, z) - Monde.Hauteur(x - .8f, z)) > 1.2f) return Vide;
            float sy = .35f + Monde.HashPublic(i, 4.1f) * .3f, sxz = .8f + Monde.HashPublic(i, 4.2f) * .5f;
            if (Monde.DansChamp(x, z))
            {
                int bande = Monde.BandeChamp(x, z);
                if (bande == 0) sy = 1.35f + Monde.HashPublic(i, 4.3f) * .3f;
                else if (bande == 1) sy = .45f;
                else if (Monde.HashPublic(i, 4.4f) < .5f) return Vide; else sy = .25f;
            }
            var rot = Quaternion.Euler(0, Monde.HashPublic(i, 4.7f) * 360f, 0);
            return Matrix4x4.TRS(new Vector3(x, y - .04f, z), rot, new Vector3(sxz, sy, sxz));
        }

        static Mesh CreerTouffe()
        {
            var v = new System.Collections.Generic.List<Vector3>();
            var c = new System.Collections.Generic.List<Color>();
            var t = new System.Collections.Generic.List<int>();
            for (int k = 0; k < 5; k++)
            {
                float a = k * 1.26f + .3f, w = .07f;
                float ca = Mathf.Cos(a) * w, sa = Mathf.Sin(a) * w;
                float bx = (Monde.HashPublic(k, 1) - .5f) * .35f, bz = (Monde.HashPublic(k, 2) - .5f) * .35f;
                float ox = (Monde.HashPublic(k, 3) - .5f) * .2f, oz = (Monde.HashPublic(k, 4) - .5f) * .2f;
                float ht = .75f + Monde.HashPublic(k, 5) * .25f;
                int n = v.Count;
                v.Add(new Vector3(bx - ca, 0, bz - sa)); v.Add(new Vector3(bx + ca, 0, bz + sa)); v.Add(new Vector3(bx + ox, ht, bz + oz));
                c.Add(new Color(.62f, .62f, .62f)); c.Add(new Color(.62f, .62f, .62f)); c.Add(new Color(1.15f, 1.15f, 1.15f));
                t.Add(n); t.Add(n + 1); t.Add(n + 2);
            }
            var m = new Mesh { name = "TouffeHerbe" };
            m.SetVertices(v); m.SetColors(c); m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.bounds = new Bounds(new Vector3(0, .5f, 0), new Vector3(2, 2, 2));
            return m;
        }
    }
}
