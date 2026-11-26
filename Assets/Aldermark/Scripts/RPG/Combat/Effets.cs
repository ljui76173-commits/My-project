using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// Effets de combat fabriqués en code : étincelles, terre qui gicle, sillons laissés dans le sol,
    /// onde de choc au sol. Tout disparaît seul.
    /// </summary>
    public static class Effets
    {
        static Material particules, sillon, onde;

        /// <summary>Le matériau des particules : un point doux (pas un carré).</summary>
        public static Material Particules()
        {
            if (particules != null) return particules;
            particules = Transparent(Color.white);
            // Un point doux plutôt qu'un carré
            const int N = 32;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "PointDoux" };
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = new Vector2(x - N / 2f + .5f, y - N / 2f + .5f).magnitude / (N / 2f);
                    float a = Mathf.Clamp01(1f - d);
                    px[y * N + x] = new Color(1, 1, 1, a * a * (3f - 2f * a));
                }
            tex.SetPixels(px);
            tex.Apply();
            particules.SetTexture("_BaseMap", tex);
            return particules;
        }

        public static Material Transparent(Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetColor("_BaseColor", c);
            return m;
        }

        /// <summary>Une gerbe de particules (couleur, nombre, vitesse, taille, gravité).</summary>
        public static void Eclat(Vector3 p, Color c, int nombre, float vitesse = 3f, float taille = .12f, float gravite = 1f, float vie = .5f, Vector3? direction = null)
        {
            var go = new GameObject("Eclat");
            go.transform.position = p;
            if (direction.HasValue && direction.Value.sqrMagnitude > .001f) go.transform.rotation = Quaternion.LookRotation(direction.Value);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main;
            m.loop = false; m.duration = .2f; m.playOnAwake = false;
            m.startColor = c; m.startLifetime = new ParticleSystem.MinMaxCurve(vie * .5f, vie);
            m.startSpeed = new ParticleSystem.MinMaxCurve(vitesse * .4f, vitesse);
            m.startSize = new ParticleSystem.MinMaxCurve(taille * .5f, taille);
            m.gravityModifier = gravite;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            var sh = ps.shape;
            if (direction.HasValue) { sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 35f; sh.radius = .05f; }
            else { sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = .15f; }
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)nombre) });
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Particules();
            ps.Play();
            Object.Destroy(go, vie + .4f);
        }

        public static void Etincelles(Vector3 p, Vector3 direction) =>
            Eclat(p, new Color(1f, .82f, .45f), 10, 5f, .06f, 1.2f, .35f, direction);

        public static void Terre(Vector3 p, Vector3 direction) =>
            Eclat(p, new Color(.42f, .33f, .22f), 6, 3f, .14f, 2.2f, .55f, direction);

        public static void Poussiere(Vector3 p, int nombre = 18) =>
            Eclat(p, new Color(.78f, .72f, .6f, .7f), nombre, 2.2f, .5f, -.05f, .9f);

        /// <summary>Un sillon sombre dans le sol (largeur et longueur en mètres), qui s'efface lentement.</summary>
        public static void Sillon(Vector3 p, Vector3 direction, float largeur = .18f, float longueur = .45f, float duree = 12f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = "Sillon";
            p.y = Monde.Hauteur(p.x, p.z) + .03f;
            go.transform.position = p;
            var plat = Vector3.ProjectOnPlane(direction, Vector3.up);
            go.transform.rotation = Quaternion.LookRotation(Vector3.down, plat.sqrMagnitude > .001f ? plat : Vector3.forward);
            go.transform.localScale = new Vector3(largeur, longueur, 1f);
            var r = go.GetComponent<MeshRenderer>();
            if (sillon == null) sillon = Transparent(new Color(.25f, .18f, .1f, .75f));
            r.sharedMaterial = sillon;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<Fondu>().duree = duree;
        }

        /// <summary>Une onde de choc : un anneau qui s'élargit au sol (blanc, ou de la couleur donnée), et de la poussière.</summary>
        public static void OndeDeChoc(Vector3 p, float rayon, Color? couleur = null)
        {
            var go = new GameObject("OndeDeChoc");
            p.y = Monde.Hauteur(p.x, p.z) + .1f;
            go.transform.position = p;
            go.transform.rotation = Quaternion.Euler(90f, 0, 0);
            go.AddComponent<MeshFilter>().sharedMesh = Formes.Anneau(1f, .08f, 48, 4);
            var r = go.AddComponent<MeshRenderer>();
            if (onde == null) onde = Transparent(new Color(1f, .95f, .85f, .8f));
            r.sharedMaterial = onde;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var o = go.AddComponent<Onde>();
            o.rayon = rayon;
            if (couleur.HasValue) o.couleur = couleur.Value;
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI / 4f;
                Poussiere(p + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * rayon * .5f, 6);
            }
        }

        /// <summary>Un anneau de couleur qui s'élargit dans l'espace, face à la direction donnée (onde d'impact en l'air).</summary>
        public static void Anneau(Vector3 p, Vector3 normale, float rayon, Color couleur, float duree = .35f)
        {
            var go = new GameObject("Anneau");
            go.transform.position = p;
            go.transform.rotation = Quaternion.LookRotation(normale.sqrMagnitude > .001f ? normale : Vector3.up);
            go.AddComponent<MeshFilter>().sharedMesh = Formes.Anneau(1f, .06f, 40, 4);
            var r = go.AddComponent<MeshRenderer>();
            if (onde == null) onde = Transparent(new Color(1f, .95f, .85f, .8f));
            r.sharedMaterial = onde;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var o = go.AddComponent<Onde>();
            o.rayon = rayon; o.duree = duree; o.couleur = couleur;
        }
    }

    /// <summary>Anneau d'onde de choc : grandit puis s'efface.</summary>
    public class Onde : MonoBehaviour
    {
        public float rayon = 3f, duree = .4f;
        public Color couleur = new Color(1f, .95f, .85f, .8f);
        float t;
        MeshRenderer r;
        MaterialPropertyBlock bloc;

        void Start() { r = GetComponent<MeshRenderer>(); bloc = new MaterialPropertyBlock(); }

        void Update()
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / duree);
            transform.localScale = Vector3.one * Mathf.Lerp(.3f, rayon, 1f - (1f - u) * (1f - u));
            bloc.SetColor("_BaseColor", new Color(couleur.r, couleur.g, couleur.b, couleur.a * (1f - u)));
            r.SetPropertyBlock(bloc);
            if (t >= duree) { Destroy(GetComponent<MeshFilter>().sharedMesh); Destroy(gameObject); }
        }
    }
}
