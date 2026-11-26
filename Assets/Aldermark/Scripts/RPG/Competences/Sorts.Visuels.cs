using System;
using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// Les effets visuels des sorts : éclairs ramifiés, lueurs, brasiers au sol, pics de pierre ou de glace,
    /// tourbillons, colonnes de lumière. Leur taille et leur nombre suivent le niveau du sort.
    /// </summary>
    public static partial class Sorts
    {
        static Mesh meshPic;
        static readonly System.Collections.Generic.Dictionary<Color, Material> pierres = new System.Collections.Generic.Dictionary<Color, Material>();

        /// <summary>Un éclair : une ligne brisée qui scintille, ramifiée et doublée aux paliers hauts, et un flash de lumière.</summary>
        public static void Eclair(Vector3 a, Vector3 b, Color couleur, int niveau = 1)
        {
            float amp = Ampleur(niveau);
            int pal = Palier(niveau);
            float duree = .2f + .03f * pal;
            EclairVisuel.Creer(a, b, .09f * amp, duree, 1f);
            if (pal >= 3) EclairVisuel.Creer(a, b, .05f * amp, duree, .7f);
            Vector3 d = b - a;
            float l = d.magnitude;
            int branches = pal >= 4 ? 5 : pal - 1;
            for (int k = 0; k < branches; k++)
            {
                Vector3 depart = a + d * UnityEngine.Random.Range(.25f, .8f);
                Vector3 dir = Vector3.Lerp(d.normalized, UnityEngine.Random.onUnitSphere, .65f).normalized;
                EclairVisuel.Creer(depart, depart + dir * l * UnityEngine.Random.Range(.18f, .35f), .04f * amp, duree * .8f, .8f);
            }
            Lueur(b, couleur, 9f * amp, .18f + .03f * pal);
            Lueur(a, couleur, 4f * amp, .12f);
        }

        /// <summary>Une lumière brève (une explosion, un éclair).</summary>
        public static void Lueur(Vector3 p, Color c, float portee, float duree)
        {
            var go = new GameObject("Lueur");
            go.transform.position = p;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = c; l.range = portee; l.intensity = 3f; l.shadows = LightShadows.None;
            go.AddComponent<LueurBreve>().duree = duree;
        }

        /// <summary>Un brasier au sol : des flammes qui montent quelques secondes, et leur lumière qui vacille.</summary>
        public static void Brasier(Vector3 sol, float rayon, float duree)
        {
            var go = new GameObject("Brasier");
            go.transform.position = sol + Vector3.up * .05f;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main;
            m.loop = true; m.playOnAwake = false; m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.startLifetime = new ParticleSystem.MinMaxCurve(.4f, .9f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(.3f, 1.2f);
            m.startSize = new ParticleSystem.MinMaxCurve(.2f, .5f);
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, .45f, .1f, .85f), new Color(1f, .85f, .35f, .85f));
            m.gravityModifier = -.35f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = rayon; sh.rotation = new Vector3(-90f, 0, 0);
            var em = ps.emission; em.rateOverTime = 30f * rayon;
            var tl = ps.sizeOverLifetime; tl.enabled = true; tl.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, .1f));
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Effets.Particules();
            ps.Play();
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1f, .55f, .2f); l.range = 4f + rayon * 2f; l.intensity = 1.6f; l.shadows = LightShadows.None;
            var f = go.AddComponent<FeuDeSol>();
            f.duree = duree; f.feu = ps; f.lumiere = l;
            Effets.Sillon(sol, Vector3.forward);
        }

        /// <summary>Des pics (pierre ou glace) qui jaillissent du sol en cercle, restent un instant puis rentrent.</summary>
        public static void Pics(Vector3 centre, int nombre, float rayon, float hauteur, Color couleur, bool glace, float duree)
        {
            for (int k = 0; k < nombre; k++)
            {
                float ang = (k + UnityEngine.Random.Range(-.3f, .3f)) * Mathf.PI * 2f / nombre;
                Vector3 dir = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
                Vector3 p = centre + dir * rayon * UnityEngine.Random.Range(.6f, 1.1f);
                p.y = Monde.Hauteur(p.x, p.z);
                var go = new GameObject(glace ? "PicDeGlace" : "PicDePierre");
                go.transform.position = p;
                // Penché vers l'extérieur, tourné au hasard
                go.transform.rotation = Quaternion.AngleAxis(UnityEngine.Random.Range(10f, 28f), Vector3.Cross(Vector3.up, dir)) * Quaternion.Euler(0, UnityEngine.Random.Range(0f, 90f), 0);
                float h = hauteur * UnityEngine.Random.Range(.7f, 1.3f);
                float w = hauteur * UnityEngine.Random.Range(.28f, .42f);
                go.transform.localScale = new Vector3(w, h, w);
                go.AddComponent<MeshFilter>().sharedMesh = MeshPic();
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = glace ? Materiau(couleur) : Pierre(couleur);
                r.shadowCastingMode = glace ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                var pic = go.AddComponent<PicSortant>();
                pic.hauteur = h; pic.duree = duree + UnityEngine.Random.Range(0f, .2f); pic.retard = k * .012f;
                Effets.Eclat(p, glace ? new Color(.85f, .96f, 1f) : new Color(.42f, .33f, .22f), 4, 2.5f, glace ? .07f : .12f, 1.8f, .5f, Vector3.up);
            }
        }

        /// <summary>Un petit tourbillon d'air qui monte en tournant (poussière, brins d'herbe).</summary>
        public static void Tourbillon(Vector3 p, float hauteur, float rayon)
        {
            p.y = Monde.Hauteur(p.x, p.z) + .1f;
            var go = new GameObject("Tourbillon");
            go.transform.position = p;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main;
            m.loop = false; m.duration = .8f; m.playOnAwake = false; m.simulationSpace = ParticleSystemSimulationSpace.Local;
            m.startLifetime = new ParticleSystem.MinMaxCurve(.8f, 1.3f);
            m.startSpeed = 0f;
            m.startSize = new ParticleSystem.MinMaxCurve(.08f, .3f);
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(.82f, .78f, .66f, .6f), new Color(.5f, .62f, .3f, .8f));
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = rayon * .35f; sh.rotation = new Vector3(-90f, 0, 0);
            var em = ps.emission; em.rateOverTime = 70f;
            var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.Local;
            v.x = new ParticleSystem.MinMaxCurve(0f); v.z = new ParticleSystem.MinMaxCurve(0f);
            v.y = new ParticleSystem.MinMaxCurve(hauteur * 1.4f);
            v.orbitalY = new ParticleSystem.MinMaxCurve(7f);
            v.radial = new ParticleSystem.MinMaxCurve(.35f);
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Effets.Particules();
            ps.Play();
            UnityEngine.Object.Destroy(go, 2.6f);
        }

        /// <summary>Une colonne de lumière qui descend sur quelqu'un et s'efface.</summary>
        public static void Colonne(Vector3 sol, float rayon, float hauteur, Color couleur)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            UnityEngine.Object.Destroy(go.GetComponent<Collider>());
            go.name = "Colonne";
            go.transform.position = sol + Vector3.up * hauteur * .5f;
            go.transform.localScale = new Vector3(rayon * 2f, hauteur * .5f, rayon * 2f);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = Materiau(couleur);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<Fondu>().duree = 1.1f;
            Lueur(sol + Vector3.up * 1.5f, new Color(couleur.r, couleur.g, couleur.b), 7f, 1f);
        }

        /// <summary>Une pyramide à quatre faces (pointe en haut, base au sol), facettée.</summary>
        public static Mesh MeshPic()
        {
            if (meshPic != null) return meshPic;
            var c = new[] { new Vector3(-.5f, 0, -.5f), new Vector3(.5f, 0, -.5f), new Vector3(.5f, 0, .5f), new Vector3(-.5f, 0, .5f) };
            var sommet = new Vector3(0, 1, 0);
            var v = new Vector3[12];
            var t = new int[12];
            for (int k = 0; k < 4; k++)
            {
                // Vu de l'extérieur, dans le sens des aiguilles d'une montre
                v[k * 3] = c[k]; v[k * 3 + 1] = sommet; v[k * 3 + 2] = c[(k + 1) % 4];
                t[k * 3] = k * 3; t[k * 3 + 1] = k * 3 + 1; t[k * 3 + 2] = k * 3 + 2;
            }
            meshPic = new Mesh { name = "Pic", vertices = v, triangles = t };
            meshPic.RecalculateNormals();
            meshPic.RecalculateBounds();
            return meshPic;
        }

        /// <summary>Un matériau opaque mat (pierre, terre), éclairé, avec ombres ; un par couleur.</summary>
        public static Material Pierre(Color c)
        {
            if (pierres.TryGetValue(c, out var m) && m != null) return m;
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) return Materiau(c);
            m = new Material(sh) { name = "PierreSort" };
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", .08f);
            pierres[c] = m;
            return m;
        }
    }

    /// <summary>Exécute une action après un délai (une seconde onde, un écho).</summary>
    public class Retard : MonoBehaviour
    {
        float t;
        Action action;

        public static void Lancer(float delai, Action a)
        {
            var r = new GameObject("Retard").AddComponent<Retard>();
            r.t = delai; r.action = a;
        }

        void Update()
        {
            t -= Time.deltaTime;
            if (t > 0f) return;
            try { action?.Invoke(); }
            finally { Destroy(gameObject); }
        }
    }

    /// <summary>Une lumière qui s'éteint en quelques dixièmes de seconde.</summary>
    public class LueurBreve : MonoBehaviour
    {
        public float duree = .2f;
        float t, i0;
        Light l;
        void Start() { l = GetComponent<Light>(); i0 = l.intensity; }
        void Update()
        {
            t += Time.deltaTime;
            l.intensity = i0 * Mathf.Clamp01(1f - t / duree);
            if (t >= duree) Destroy(gameObject);
        }
    }

    /// <summary>Le brasier : il vacille, puis s'éteint et part en fumée.</summary>
    public class FeuDeSol : MonoBehaviour
    {
        public float duree = 2.5f;
        public ParticleSystem feu;
        public Light lumiere;
        float t;
        bool eteint;

        void Update()
        {
            t += Time.deltaTime;
            if (lumiere != null) lumiere.intensity = (eteint ? Mathf.Max(0f, 1f - (t - duree) * 2f) : 1f) * (1.3f + .5f * Mathf.PerlinNoise(t * 9f, 0f));
            if (!eteint && t >= duree)
            {
                eteint = true;
                if (feu != null) feu.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                Effets.Eclat(transform.position + Vector3.up * .3f, new Color(.3f, .28f, .26f, .5f), 8, .8f, .5f, -.2f, 1.4f);
            }
            if (t >= duree + 1.2f) Destroy(gameObject);
        }
    }

    /// <summary>Un pic qui jaillit du sol, reste un instant, puis rentre.</summary>
    public class PicSortant : MonoBehaviour
    {
        public float hauteur = 1f, duree = 1f, retard;
        float t;
        Vector3 haut;

        void Start()
        {
            haut = transform.position;
            transform.position = haut - transform.up * hauteur;
        }

        void Update()
        {
            t += Time.deltaTime;
            float u = t - retard;
            float sortie;
            if (u < 0f) sortie = 0f;
            else if (u < .12f) { float x = u / .12f; sortie = 1f - (1f - x) * (1f - x); }
            else if (u < duree) sortie = 1f;
            else sortie = Mathf.Clamp01(1f - (u - duree) / .45f);
            transform.position = haut - transform.up * hauteur * (1f - sortie);
            if (u > duree + .45f) Destroy(gameObject);
        }
    }

    /// <summary>Un trait d'éclair qui se redessine plusieurs fois avant de s'effacer.</summary>
    public class EclairVisuel : MonoBehaviour
    {
        Vector3 a, b;
        LineRenderer ligne;
        int n;
        float t, prochain, largeur, duree, intensite;

        public static void Creer(Vector3 a, Vector3 b, float largeur, float duree, float intensite)
        {
            var go = new GameObject("Eclair");
            var lr = go.AddComponent<LineRenderer>();
            int n = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(a, b) / .8f), 4, 30);
            lr.positionCount = n + 1;
            lr.useWorldSpace = true;
            lr.widthMultiplier = largeur;
            lr.numCapVertices = 2;
            lr.sharedMaterial = Sorts.Materiau(new Color(.92f, .95f, 1f, 1f));
            lr.startColor = lr.endColor = Color.white;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var e = go.AddComponent<EclairVisuel>();
            e.a = a; e.b = b; e.ligne = lr; e.n = n; e.largeur = largeur; e.duree = duree; e.intensite = intensite;
            e.Redessiner();
        }

        void Redessiner()
        {
            Vector3 d = b - a;
            Vector3 c1 = Vector3.Cross(d, Vector3.up);
            if (c1.sqrMagnitude < .001f) c1 = Vector3.right;
            c1.Normalize();
            Vector3 c2 = Vector3.Cross(d.normalized, c1);
            float amp = Mathf.Min(.6f, d.magnitude * .06f);
            for (int k = 0; k <= n; k++)
            {
                float u = k / (float)n;
                float bord = Mathf.Sin(u * Mathf.PI);   // les deux bouts restent en place
                ligne.SetPosition(k, a + d * u + (c1 * UnityEngine.Random.Range(-1f, 1f) + c2 * UnityEngine.Random.Range(-1f, 1f)) * amp * bord);
            }
        }

        void Update()
        {
            t += Time.deltaTime;
            if (t >= prochain) { prochain = t + .045f; Redessiner(); }
            float alpha = Mathf.Clamp01(1f - t / duree) * intensite;
            ligne.widthMultiplier = largeur * (.4f + alpha);
            ligne.startColor = ligne.endColor = new Color(1f, 1f, 1f, alpha);
            if (t >= duree) Destroy(gameObject);
        }
    }

    /// <summary>Un projectile de sort : il vole, s'incurve vers la cible verrouillée, et frappe la première chose hostile ou solide.
    /// Plus le sort est haut, plus il est gros et lumineux ; aux paliers, des satellites tournent autour, puis un halo.</summary>
    public class ProjectileSort : MonoBehaviour
    {
        Incantation s;
        Vector3 vitesse;
        float parcouru, taille, t;
        Transform visuel, satellites, halo;
        ParticleSystem trainee;
        bool fini;

        public static void Creer(Incantation s)
        {
            var d = s.def;
            float a = Sorts.Ampleur(s.niveau);
            int pal = Sorts.Palier(s.niveau);
            var go = new GameObject("Sort_" + d.id);
            go.transform.position = s.origine;
            go.transform.rotation = Quaternion.LookRotation(s.direction);
            var p = go.AddComponent<ProjectileSort>();
            p.s = s;
            p.vitesse = s.direction * d.vitesse;
            p.taille = d.taille * a;

            // Les couleurs : le feu blanchit puis bleuit au cœur en montant ; la glace devient cristal
            Color ext = d.couleur, coeurC = new Color(1f, .97f, .9f, .95f);
            bool eclat = d.element == Element.Glace;
            if (d.element == Element.Feu && pal >= 3) ext = new Color(1f, .66f, .22f);
            if (d.element == Element.Feu && pal >= 4) coeurC = new Color(.7f, .85f, 1f, .95f);
            if (eclat && pal >= 4) ext = new Color(.5f, .85f, 1f);

            // Le cœur : une sphère lumineuse (allongée en éclat pour la glace) et un noyau clair
            var coeur = Piece(go.transform, eclat ? PrimitiveType.Cube : PrimitiveType.Sphere, new Color(ext.r, ext.g, ext.b, .8f));
            coeur.localScale = eclat ? new Vector3(p.taille * .7f, p.taille * .7f, p.taille * 3.2f) : Vector3.one * p.taille * 2f;
            if (eclat) coeur.localRotation = Quaternion.Euler(0, 0, 45f);
            var noyau = Piece(go.transform, PrimitiveType.Sphere, coeurC);
            noyau.localScale = Vector3.one * p.taille * (eclat ? .6f : 1.1f);
            p.visuel = coeur;

            // Les satellites (un au palier 2, deux au 3, trois au 4) et le halo du sort maîtrisé
            if (pal >= 2)
            {
                p.satellites = new GameObject("Satellites").transform;
                p.satellites.SetParent(go.transform, false);
                int n = pal - 1;
                Color cs = eclat ? new Color(.85f, .96f, 1f, .9f) : new Color(1f, .85f, .35f, .9f);
                for (int k = 0; k < n; k++)
                {
                    float ang = k * Mathf.PI * 2f / n;
                    var sat = Piece(p.satellites, eclat ? PrimitiveType.Cube : PrimitiveType.Sphere, cs);
                    sat.localPosition = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0) * p.taille * 1.9f;
                    sat.localScale = eclat ? new Vector3(.25f, .25f, .9f) * p.taille : Vector3.one * p.taille * .4f;
                }
            }
            if (pal >= 4)
            {
                p.halo = Piece(go.transform, PrimitiveType.Sphere, new Color(ext.r, ext.g, ext.b, .22f));
                p.halo.localScale = Vector3.one * p.taille * 3.4f;
            }

            // La lumière qu'il porte
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = ext; l.range = 5f * a; l.intensity = 2.2f + .2f * (s.niveau - 1); l.shadows = LightShadows.None;

            // La traînée
            var tg = new GameObject("Trainee");
            tg.transform.SetParent(go.transform, false);
            var ps = tg.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main;
            m.loop = true; m.playOnAwake = false; m.simulationSpace = ParticleSystemSimulationSpace.World;
            float vie = 1f + .08f * (s.niveau - 1);
            m.startLifetime = new ParticleSystem.MinMaxCurve(.25f * vie, .45f * vie);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0f, .6f);
            m.startSize = new ParticleSystem.MinMaxCurve(p.taille * 1.2f, p.taille * 2.4f);
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(ext.r, ext.g, ext.b, .75f), new Color(coeurC.r, coeurC.g, coeurC.b, .55f));
            m.gravityModifier = d.element == Element.Feu ? -.3f : 0f;
            var em = ps.emission; em.rateOverTime = 0f; em.rateOverDistance = 14f * a;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = p.taille * .5f;
            var tl = ps.sizeOverLifetime; tl.enabled = true; tl.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            tg.GetComponent<ParticleSystemRenderer>().sharedMaterial = Effets.Particules();
            ps.Play();
            p.trainee = ps;
            Sons.JouerA(eclat ? "sortGlace" : "sortFeu", s.origine, .7f + .02f * s.niveau, (eclat ? 1.4f : 1.15f) - .02f * (s.niveau - 1));
        }

        static Transform Piece(Transform parent, PrimitiveType type, Color c)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = Sorts.Materiau(c);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        void Update()
        {
            if (fini) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            t += dt;
            var d = s.def;
            // S'incurve vers la cible verrouillée
            if (Sorts.Valide(s, s.cible))
            {
                Vector3 voulu = (s.cible.Centre - transform.position).normalized * d.vitesse;
                vitesse = Vector3.RotateTowards(vitesse, voulu, 2.2f * dt, 0f);
            }
            Vector3 pas = vitesse * dt;
            float l = pas.magnitude;
            if (Sorts.Premier(s, transform.position, pas / l, l, taille * .7f, out var c, out var point))
            {
                transform.position = point;
                Finir(c);
                return;
            }
            transform.position += pas;
            transform.rotation = Quaternion.LookRotation(vitesse);
            if (visuel != null && d.element == Element.Glace) visuel.Rotate(0, 0, 720f * dt, Space.Self);
            if (satellites != null) satellites.Rotate(0, 0, 540f * dt, Space.Self);
            if (halo != null) halo.localScale = Vector3.one * taille * 3.4f * (1f + .12f * Mathf.Sin(t * 14f));
            parcouru += l;
            if (parcouru >= d.portee) Finir(null, false);
        }

        void Finir(ICible c, bool impact = true)
        {
            fini = true;
            if (c != null) Sorts.Toucher(s, c, vitesse.normalized);
            else if (impact) Sorts.Impact(s.def.element, transform.position, vitesse.normalized, s.niveau);
            else Effets.Eclat(transform.position, s.def.couleur, 6, 1f, .1f, 0f, .3f);
            // La traînée finit de se dissiper seule
            if (trainee != null)
            {
                trainee.transform.SetParent(null, true);
                trainee.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                Destroy(trainee.gameObject, .8f);
            }
            Destroy(gameObject);
        }
    }
}
