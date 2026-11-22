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
        public static float Nuit { get; private set; }
        public static float Crepuscule { get; private set; }
        Color ciel0, equateur0, sol0, brouillard0;
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
            ciel0 = RenderSettings.ambientSkyColor; equateur0 = RenderSettings.ambientEquatorColor;
            sol0 = RenderSettings.ambientGroundColor; brouillard0 = RenderSettings.fogColor;
        }

        /// <summary>
        /// Course du soleil selon l'heure (Chantier.Heure, de 0 à 24) : lever à l'est vers 6 h,
        /// midi haut dans le ciel, coucher à l'ouest vers 20 h, puis nuit bleutée.
        /// </summary>
        void Heure()
        {
            float h = Chantier.Heure;
            float jour = Mathf.Clamp01((h - 6f) / 14f);                 // 0 au lever, 1 au coucher
            float elev = (6f + 44f * Mathf.Sin(jour * Mathf.PI)) * Mathf.Deg2Rad;
            float az = Mathf.Lerp(.15f, Mathf.PI - .15f, jour);
            direction1 = new Vector3(Mathf.Cos(az) * Mathf.Cos(elev), Mathf.Sin(elev), -.55f * Mathf.Cos(elev) - .2f).normalized;
            Nuit = h < 12f ? 1f - Monde.Lisse(5f, 6.6f, h) : Monde.Lisse(19.4f, 20.6f, h);
            Crepuscule = Mathf.Max(1f - Mathf.Abs(h - 6.6f) / 1.4f, 1f - Mathf.Abs(h - 19.2f) / 1.6f, 0f) * (1f - Nuit);
            if (soleil != null) soleil.transform.rotation = Quaternion.LookRotation(-direction1);
            if (rendu != null) rendu.directionSoleil = direction1;
            Shader.SetGlobalVector("_Soleil1", direction1);
            var nuitCiel = new Color(.16f, .19f, .34f);
            RenderSettings.ambientSkyColor = Color.Lerp(Color.Lerp(ciel0, new Color(1f, .7f, .55f), Crepuscule * .5f), nuitCiel, Nuit);
            RenderSettings.ambientEquatorColor = Color.Lerp(equateur0, nuitCiel * .8f, Nuit);
            RenderSettings.ambientGroundColor = Color.Lerp(sol0, nuitCiel * .45f, Nuit);
            RenderSettings.fogColor = Color.Lerp(Color.Lerp(brouillard0, new Color(.95f, .66f, .55f), Crepuscule * .6f), new Color(.1f, .12f, .24f), Nuit);
            if (soleil != null) soleil.color = Color.Lerp(new Color(1f, .89f, .66f), new Color(1f, .62f, .4f), Crepuscule);
        }

        void Update()
        {
            temps += Time.deltaTime;
            Heure();
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

            if (soleil != null) soleil.intensity = intensiteSoleil * (1f - .62f * couverture) * (1f - .9f * Nuit);
            if (soleil2 != null) soleil2.intensity = intensiteSoleil2 * (1f - .5f * couverture) * (1f - .7f * Nuit);
            RenderSettings.ambientIntensity = intensiteAmbiante * (1f - .12f * couverture) * (1f - .45f * Nuit);
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
