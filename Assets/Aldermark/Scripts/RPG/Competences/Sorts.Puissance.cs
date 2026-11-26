using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// La sensation de puissance d'un sort qui frappe : une boule de lumière qui gonfle, des anneaux d'onde,
    /// un grondement grave, la caméra qui tremble (même de loin), un arrêt sur image quand c'est le héros qui frappe,
    /// et au-delà de la maîtrise : cratères, colonnes d'énergie tombées du ciel, ondes de choc en série, écran blanc.
    /// </summary>
    public static partial class Sorts
    {
        static readonly Color Roche = new Color(.42f, .36f, .3f);
        static readonly Color TerreRetournee = new Color(.33f, .25f, .17f);
        static Material matTrou;

        /// <summary>L'explosion d'un sort à l'impact (projectile, foudre, onde), à l'échelle de son niveau.</summary>
        public static void Deflagration(Element e, Vector3 p, Vector3 direction, int niveau, Camp camp)
        {
            float a = Ampleur(niveau);
            int pal = Palier(niveau);
            Color c = CatalogueCompetences.CouleurElement(e);
            var cam = Camera.main;
            Vector3 versCam = cam != null ? cam.transform.position - p : -direction;

            // La boule de lumière et les anneaux
            Boule(p, (.45f + .22f * pal) * a, c, .2f + .02f * pal);
            Effets.Anneau(p, versCam, 1.1f * a, new Color(c.r, c.g, c.b, .8f), .32f);
            if (pal >= 3) Retard.Lancer(.07f, () => Effets.Anneau(p, versCam, 1.9f * a, new Color(c.r, c.g, c.b, .5f), .42f));

            // La caméra tremble selon la distance ; le héros qui frappe a son arrêt sur image
            var h = CombatHeros.Instance;
            if (h != null)
            {
                float dist = Vector3.Distance(h.transform.position, p);
                h.Secousse(Mathf.Clamp01(1f - dist / (30f + 12f * a)) * (.03f + .022f * pal) * a);
                if (camp == Camp.Heros && pal >= 2) h.ArretSurImage(Mathf.Min(.03f + .012f * pal, .12f));
            }

            // Le grondement, plus grave et plus lointain à chaque palier
            Sons.JouerA("onde", p, Mathf.Min(1f, .28f + .09f * pal), Mathf.Max(.35f, .78f - .05f * pal), 50f + 20f * pal);

            // Au-delà de la maîtrise
            Vector3 sol = new Vector3(p.x, Monde.Hauteur(p.x, p.z), p.z);
            bool presDuSol = p.y - sol.y < 4f;
            if (pal >= 5)
            {
                Colonne(presDuSol ? sol : p, .45f * a, 30f, new Color(c.r, c.g, c.b, .45f));
                if (presDuSol)
                {
                    Cratere(sol, (.7f + .25f * (pal - 5)) * a, 35f);
                    Effets.OndeDeChoc(sol, 3f * a, new Color(c.r, c.g, c.b, .85f));
                }
            }
            if (pal >= 7)
            {
                if (presDuSol)
                {
                    Retard.Lancer(.15f, () => Effets.OndeDeChoc(sol, 5f * a, new Color(c.r, c.g, c.b, .7f)));
                    Retard.Lancer(.3f, () => Effets.OndeDeChoc(sol, 7.5f * a, new Color(1f, 1f, 1f, .5f)));
                }
                HUD.EclairCombat(new Color(1f, 1f, 1f, .3f), .35f, false);
            }
            if (pal >= 9) HUD.EclairCombat(new Color(1f, 1f, 1f, .65f), .55f, false);
        }

        /// <summary>Une boule de lumière qui gonfle et s'efface en un éclair.</summary>
        public static void Boule(Vector3 p, float rayon, Color c, float duree)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = "Boule";
            go.transform.position = p;
            go.transform.localScale = Vector3.zero;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = Materiau(new Color(Mathf.Lerp(c.r, 1f, .45f), Mathf.Lerp(c.g, 1f, .45f), Mathf.Lerp(c.b, 1f, .45f), .75f));
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var g = go.AddComponent<Gonfle>();
            g.rayon = rayon; g.duree = duree;
            Lueur(p, c, rayon * 8f, duree * 1.6f);
        }

        /// <summary>Un cratère : terre retournée et un anneau de mottes et de pierres soulevées.</summary>
        public static void Cratere(Vector3 sol, float rayon, float duree)
        {
            Trou(sol, rayon, duree);
            int n = Mathf.Clamp(Mathf.RoundToInt(8 + rayon * 4f), 8, 28);
            for (int k = 0; k < n; k++)
            {
                float ang = (k + Random.Range(-.3f, .3f)) * Mathf.PI * 2f / n;
                Vector3 q = sol + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * rayon * Random.Range(.8f, 1.05f);
                Motte(q, rayon * Random.Range(.16f, .28f), duree * .8f, k % 3 == 0 ? Roche : TerreRetournee);
            }
            for (int k = 0; k < 6; k++)
                Effets.Terre(sol + Random.insideUnitSphere * rayon * .5f, Vector3.up * 3f);
            Effets.Poussiere(sol, Mathf.RoundToInt(16 + rayon * 6f));
        }

        /// <summary>Une tache sombre aux bords irréguliers : la terre retournée.</summary>
        public static void Trou(Vector3 sol, float rayon, float duree)
        {
            if (matTrou == null) matTrou = TexturesMagiques.Sombre(TexturesMagiques.Tache, new Color(.16f, .11f, .07f, .8f));
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = "Trou";
            go.transform.position = new Vector3(sol.x, Monde.Hauteur(sol.x, sol.z) + .035f, sol.z);
            go.transform.rotation = Quaternion.LookRotation(Vector3.down, Quaternion.Euler(0, Random.Range(0f, 360f), 0) * Vector3.forward);
            go.transform.localScale = new Vector3(rayon * 2f, rayon * 2f, 1f);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = matTrou;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<Fondu>().duree = duree;
        }

        static readonly System.Collections.Generic.List<GameObject> mottes = new System.Collections.Generic.List<GameObject>();

        /// <summary>Une motte de terre (ou une pierre) : petite pyramide aplatie qui reste un moment, puis s'enfonce.</summary>
        public static void Motte(Vector3 sol, float taille, float duree, Color? couleur = null)
        {
            while (mottes.Count > 200) { if (mottes[0] != null) Object.Destroy(mottes[0]); mottes.RemoveAt(0); }
            var go = new GameObject("Motte");
            go.transform.position = new Vector3(sol.x, Monde.Hauteur(sol.x, sol.z) - taille * .15f, sol.z);
            go.transform.rotation = Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(0f, 360f), Random.Range(-15f, 15f));
            go.transform.localScale = new Vector3(taille * Random.Range(1.1f, 1.6f), taille * Random.Range(.45f, .8f), taille * Random.Range(1f, 1.4f));
            go.AddComponent<MeshFilter>().sharedMesh = MeshPic();
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Pierre(couleur ?? TerreRetournee);
            go.AddComponent<Enfouir>().duree = duree;
            mottes.Add(go);
        }
    }

    /// <summary>Gonfle d'un coup jusqu'à son rayon et s'efface.</summary>
    public class Gonfle : MonoBehaviour
    {
        public float rayon = 1f, duree = .2f;
        float t;
        MeshRenderer r;
        MaterialPropertyBlock bloc;
        Color c;
        static readonly int Id = Shader.PropertyToID("_BaseColor");

        void Start()
        {
            r = GetComponent<MeshRenderer>();
            bloc = new MaterialPropertyBlock();
            c = r.sharedMaterial.GetColor(Id);
        }

        void Update()
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / duree);
            float s = rayon * 2f * (1f - (1f - u) * (1f - u) * (1f - u));
            transform.localScale = new Vector3(s, s, s);
            bloc.SetColor(Id, new Color(c.r, c.g, c.b, c.a * (1f - u * u)));
            r.SetPropertyBlock(bloc);
            if (u >= 1f) Destroy(gameObject);
        }
    }
}
