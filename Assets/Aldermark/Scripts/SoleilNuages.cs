using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Aldermark
{
    /// <summary>
    /// Interaction soleil / nuages :
    /// - envoie le temps au ciel (_AldTemps) ;
    /// - calcule si un nuage passe devant le soleil (même bruit que le ciel) et baisse la lumière ;
    /// - fait défiler l'ombre des nuages au sol grâce à une « cookie » sur la lumière du soleil.
    /// </summary>
    public class SoleilNuages : MonoBehaviour
    {
        public Light soleil;
        public Light soleil2;
        public RenduPeint rendu;
        public Vector3 direction1 = new Vector3(-.48f, .55f, -.68f);
        public Vector3 direction2 = new Vector3(-.40f, .58f, -.71f);
        public float intensiteSoleil = 1.35f;
        public float intensiteSoleil2 = .15f;
        public float intensiteAmbiante = 1f;
        public float tailleOmbresNuages = 260f;
        [Range(.3f, .7f)] public float couvertureCiel = .5f;
        public Vector2 vent = new Vector2(7f, 2.5f);

        float temps, couverture;
        UniversalAdditionalLightData donneesSoleil;

        void Start()
        {
            if (soleil == null) return;
            soleil.transform.rotation = Quaternion.LookRotation(-direction1.normalized);
            if (soleil2 != null) soleil2.transform.rotation = Quaternion.LookRotation(-direction2.normalized);
            soleil.cookie = CreerCookie(256, 4);
            donneesSoleil = soleil.GetUniversalAdditionalLightData();
            donneesSoleil.lightCookieSize = new Vector2(tailleOmbresNuages, tailleOmbresNuages);
            if (rendu != null) rendu.directionSoleil = direction1;
            Shader.SetGlobalVector("_Soleil1", direction1.normalized);
            Shader.SetGlobalVector("_Soleil2", direction2.normalized);
        }

        void Update()
        {
            temps += Time.deltaTime;
            Shader.SetGlobalFloat("_AldTemps", temps);

            // Nuage devant le soleil ? (même calcul que le shader du ciel)
            Shader.SetGlobalFloat("_Couverture", couvertureCiel);
            Vector3 d = direction1.normalized;
            float k = .5f / (Mathf.Max(d.y, 0f) + .25f);
            float n = Monde.DensiteNuages(d.x * k + temps * .012f, d.z * k + temps * .004f);
            float seuil = 1f - couvertureCiel;
            float bande = Monde.Lisse(0f, .06f, d.y) * (1f - Monde.Lisse(.3f, .75f, d.y) * .85f);
            float cible = Monde.Lisse(seuil, seuil + .06f, n) * bande;
            couverture = Mathf.Lerp(couverture, cible, Mathf.Clamp01(Time.deltaTime * 2f));

            if (soleil != null) soleil.intensity = intensiteSoleil * (1f - .62f * couverture);
            if (soleil2 != null) soleil2.intensity = intensiteSoleil2 * (1f - .5f * couverture);
            RenderSettings.ambientIntensity = intensiteAmbiante * (1f - .12f * couverture);
            if (rendu != null) rendu.soleilDegage = 1f - couverture;

            if (donneesSoleil != null) donneesSoleil.lightCookieOffset = vent * temps;
        }

        /// <summary>Texture d'ombres de nuages qui se répète sans couture.</summary>
        static Texture2D CreerCookie(int taille, int periode)
        {
            var tex = new Texture2D(taille, taille, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                name = "OmbresNuages"
            };
            var px = new Color32[taille * taille];
            for (int y = 0; y < taille; y++)
                for (int x = 0; x < taille; x++)
                {
                    float u = (float)x / taille, v = (float)y / taille, s = 0, a = .5f;
                    int p = periode;
                    for (int o = 0; o < 4; o++) { s += a * BruitPeriodique(u * p, v * p, p); p *= 2; a *= .5f; }
                    float ombre = Monde.Lisse(.5f, .66f, s);
                    byte b = (byte)(255 * (1f - .5f * ombre));
                    px[y * taille + x] = new Color32(b, b, b, b);
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        static float BruitPeriodique(float x, float y, int p)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float u = x - xi, v = y - yi;
            u = u * u * (3 - 2 * u); v = v * v * (3 - 2 * v);
            float H(int a, int b) => Monde.HashPublic(((a % p) + p) % p + p * 13.7f, ((b % p) + p) % p);
            float A = H(xi, yi), B = H(xi + 1, yi), C = H(xi, yi + 1), D = H(xi + 1, yi + 1);
            float ab = A + (B - A) * u, cd = C + (D - C) * u;
            return ab + (cd - ab) * v;
        }
    }
}
