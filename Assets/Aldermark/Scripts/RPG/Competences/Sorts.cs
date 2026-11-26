using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    /// <summary>Un sort sur le point de partir : qui le lance, avec quelle force, d'où, vers où.</summary>
    public struct Incantation
    {
        public DefCompetence def;
        public ICible auteur;
        public Camp camp;
        /// <summary>Attaque magique brute (ou quantité soignée pour un soin).</summary>
        public double attaque;
        public int niveau;
        public Vector3 origine, direction;
        /// <summary>La cible verrouillée, s'il y en a une : le projectile s'incurve vers elle, la foudre la frappe.</summary>
        public ICible cible;
        public float rayon, poussee;
        /// <summary>Appelé à chaque cible touchée (vrai si le coup l'a achevée).</summary>
        public Action<ICible, bool> touche;
    }

    /// <summary>
    /// Ce que font les sorts, pour tout le monde (le héros comme les monstres) :
    /// projectiles, traits de foudre, cônes, zones, soins, et leurs effets visuels.
    /// </summary>
    public static class Sorts
    {
        /// <summary>Quelqu'un vient de lancer un sort (pour apprendre en regardant).</summary>
        public static event Action<Incantation> Lance;

        static readonly Dictionary<Color, Material> materiaux = new Dictionary<Color, Material>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reinitialiser() { Lance = null; materiaux.Clear(); }

        /// <summary>Un matériau translucide et lumineux, partagé par couleur.</summary>
        public static Material Materiau(Color c)
        {
            if (materiaux.TryGetValue(c, out var m) && m != null) return m;
            m = Effets.Transparent(c);
            materiaux[c] = m;
            return m;
        }

        public static bool Hostile(Camp auteur, Camp cible) =>
            auteur == Camp.Heros || auteur == Camp.Allie ? cible != Camp.Heros && cible != Camp.Allie : Monstre.Ennemis(auteur, cible);

        public static void Lancer(Incantation s)
        {
            if (s.def == null) return;
            if (s.direction.sqrMagnitude < .001f) s.direction = Vector3.forward;
            s.direction.Normalize();
            Lance?.Invoke(s);
            switch (s.def.forme)
            {
                case FormeSort.Projectile: ProjectileSort.Creer(s); break;
                case FormeSort.Trait: Trait(s); break;
                case FormeSort.Cone: Cone(s); break;
                case FormeSort.Zone: Zone(s); break;
                case FormeSort.Soin: Soin(s); break;
            }
        }

        /// <summary>Un sort atteint une cible : le coup magique, son effet (selon sa chance), l'éclat à l'impact.</summary>
        public static void Toucher(Incantation s, ICible c, Vector3 direction, double mult = 1)
        {
            if (!Cibles.Existe(c) || !c.Vivant) return;
            var d = s.def;
            bool effet = d.effet != Effet.Aucun && UnityEngine.Random.value < d.chanceEffet;
            bool vivant = c.Vivant;
            c.Recevoir(new Coup
            {
                degats = s.attaque * mult, direction = direction, poussee = s.poussee, lourd = d.lourd, magique = true,
                auteur = s.auteur, nom = d.nom, element = d.element, effet = effet ? d.effet : Effet.Aucun, dureeEffet = d.dureeEffet,
            });
            s.touche?.Invoke(c, vivant && Cibles.Existe(c) && !c.Vivant);
            Impact(d.element, c.Centre, direction);
        }

        /// <summary>L'éclat d'un sort qui frappe (une cible, un mur, le sol).</summary>
        public static void Impact(Element e, Vector3 p, Vector3 direction)
        {
            Color c = CatalogueCompetences.CouleurElement(e);
            switch (e)
            {
                case Element.Feu:
                    Effets.Eclat(p, new Color(1f, .55f, .15f), 22, 4.5f, .2f, -.4f, .6f);
                    Effets.Eclat(p, new Color(1f, .9f, .5f), 10, 7f, .07f, .8f, .4f);
                    Sons.JouerA("sortFeu", p, .7f, .75f);
                    Lueur(p, c, 5f, .25f);
                    break;
                case Element.Glace:
                    Effets.Eclat(p, new Color(.85f, .96f, 1f), 18, 5f, .09f, 1.6f, .55f, -direction);
                    Effets.Eclat(p, new Color(.6f, .85f, 1f, .6f), 8, 1.5f, .3f, -.05f, .5f);
                    Sons.JouerA("sortGlace", p, .7f, 1.15f);
                    break;
                case Element.Foudre:
                    Effets.Eclat(p, new Color(.9f, .92f, 1f), 16, 6f, .06f, .5f, .3f);
                    Lueur(p, c, 8f, .15f);
                    break;
                case Element.Terre:
                    Effets.Terre(p, Vector3.up);
                    break;
                case Element.Lumiere:
                    Effets.Eclat(p, c, 12, 1.5f, .12f, -.4f, .8f);
                    break;
                default:
                    Effets.Eclat(p, c, 10, 3f, .12f, .4f, .4f);
                    break;
            }
        }

        // ---------- les formes ----------

        /// <summary>La foudre : instantanée, elle frappe la cible verrouillée ou ce qu'on vise ; au niveau 5, elle rebondit.</summary>
        static void Trait(Incantation s)
        {
            var d = s.def;
            Vector3 fin = s.origine + s.direction * d.portee;
            ICible touchee = null;
            if (Valide(s, s.cible) && Vector3.Distance(s.origine, s.cible.Centre) <= d.portee + 2f)
            {
                touchee = s.cible;
                fin = s.cible.Centre;
            }
            else if (Premier(s, s.origine, s.direction, d.portee, .35f, out var c, out var point))
            {
                touchee = c;
                fin = c != null ? c.Centre : point;
            }
            Eclair(s.origine, fin, d.couleur);
            Sons.JouerA("sortFoudre", fin, 1f, UnityEngine.Random.Range(.92f, 1.08f), 90f);
            if (touchee == null) { Impact(d.element, fin, s.direction); return; }
            Toucher(s, touchee, (fin - s.origine).normalized);
            if (s.niveau < 5) return;
            // Le rebond : sur l'ennemi le plus proche de la première cible
            Combattant suivant = null;
            float best = 7f;
            foreach (var cb in Combattant.Tous)
            {
                if (ReferenceEquals(cb, touchee) || !Valide(s, cb)) continue;
                float dist = Vector3.Distance(cb.Centre, fin);
                if (dist < best) { best = dist; suivant = cb; }
            }
            if (suivant == null) return;
            Eclair(fin, suivant.Centre, d.couleur);
            Toucher(s, suivant, (suivant.Centre - fin).normalized, .6);
        }

        /// <summary>Le vent : tout ce qui est devant, dans le cône, est frappé et repoussé.</summary>
        static void Cone(Incantation s)
        {
            var d = s.def;
            Vector3 avant = Vector3.ProjectOnPlane(s.direction, Vector3.up).normalized;
            if (avant.sqrMagnitude < .01f) avant = s.direction;
            Vector3 pied = s.auteur != null && Cibles.Existe(s.auteur) ? s.auteur.Corps.position : s.origine;
            Effets.Eclat(s.origine, new Color(.9f, .97f, .93f, .55f), 46, 15f, .3f, 0f, .45f, s.direction);
            Effets.Eclat(s.origine, new Color(1f, 1f, 1f, .8f), 20, 22f, .06f, 0f, .3f, s.direction);
            for (int k = 1; k <= 3; k++) Effets.Poussiere(pied + avant * k * 1.8f, 5);
            Sons.JouerA("sortVent", s.origine, 1f, UnityEngine.Random.Range(.92f, 1.08f));
            foreach (var c in Autour(s, pied, d.portee))
            {
                Vector3 vers = Vector3.ProjectOnPlane(c.Centre - pied, Vector3.up);
                if (vers.magnitude > d.portee || (vers.magnitude > .8f && Vector3.Angle(avant, vers) > d.angle * .5f)) continue;
                Toucher(s, c, vers.sqrMagnitude > .01f ? vers.normalized : avant);
            }
        }

        /// <summary>La terre : une onde autour du lanceur qui frappe, soulève et étourdit.</summary>
        static void Zone(Incantation s)
        {
            Vector3 centre = s.auteur != null && Cibles.Existe(s.auteur) ? s.auteur.Corps.position : s.origine;
            centre.y = Monde.Hauteur(centre.x, centre.z);
            float rayon = s.rayon > 0f ? s.rayon : s.def.rayon;
            Effets.OndeDeChoc(centre, rayon);
            for (int k = 0; k < 10; k++)
            {
                float a = k * Mathf.PI * .2f;
                Vector3 p = centre + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * rayon * UnityEngine.Random.Range(.45f, .9f);
                p.y = Monde.Hauteur(p.x, p.z);
                Effets.Terre(p, Vector3.up * 2f + (p - centre).normalized);
                Effets.Sillon(p, p - centre);
            }
            Sons.JouerA("sortTerre", centre, 1f, UnityEngine.Random.Range(.9f, 1.05f), 80f);
            var h = CombatHeros.Instance;
            if (h != null) h.Secousse(Mathf.Clamp01(1f - Vector3.Distance(h.transform.position, centre) / 25f) * .14f);
            foreach (var c in Autour(s, centre, rayon))
            {
                Vector3 vers = Vector3.ProjectOnPlane(c.Centre - centre, Vector3.up);
                if (vers.magnitude > rayon) continue;
                Vector3 dir = vers.sqrMagnitude > .01f ? vers.normalized : Vector3.forward;
                Toucher(s, c, dir);
                if (c is Monstre m && m.Vivant && m.stature < 1f) m.Pousser(dir * 2.5f, 5.5f);
            }
        }

        /// <summary>Le soin : sur le lanceur lui-même.</summary>
        static void Soin(Incantation s)
        {
            var c = s.auteur;
            if (!Cibles.Existe(c) || !c.Vivant) return;
            double avant = c.VieActuelle;
            if (c is CombatHeros && StatsHeros.Instance != null) StatsHeros.Instance.Rendre(s.attaque);
            else if (c is Combattant cb) cb.Rendre(s.attaque);
            double rendu = c.VieActuelle - avant;
            Vector3 p = c.Corps.position;
            for (int k = 0; k < 6; k++)
            {
                float a = k * Mathf.PI / 3f;
                Effets.Eclat(p + new Vector3(Mathf.Cos(a) * .6f, .2f, Mathf.Sin(a) * .6f), s.def.couleur, 6, .8f, .14f, -.9f, 1.1f);
            }
            Effets.Eclat(c.Centre, new Color(1f, 1f, .85f), 14, 1.2f, .1f, -.6f, 1f);
            Lueur(c.Centre, s.def.couleur, 6f, .6f);
            Sons.JouerA("sortSoin", c.Centre, .9f, 1f);
            Vector3 ou = c is CombatHeros ? c.Centre + Vector3.up * .6f : c.Centre + Vector3.up * .9f;
            HUD.ChiffreTexte(ou, "+" + Nombres.Court(Math.Max(0, rendu)), new Color(.55f, 1f, .55f));
        }

        // ---------- utilitaires ----------

        public static bool Valide(Incantation s, ICible c) =>
            Cibles.Existe(c) && c.Vivant && !ReferenceEquals(c, s.auteur) && Hostile(s.camp, c.Camp);

        /// <summary>Les cibles hostiles autour d'un point (le héros compris, si c'est un monstre qui lance).</summary>
        static List<ICible> Autour(Incantation s, Vector3 centre, float rayon)
        {
            var l = new List<ICible>();
            var h = CombatHeros.Instance;
            if (h != null && Valide(s, h) && Vector3.Distance(h.Centre, centre) <= rayon + 1.5f) l.Add(h);
            foreach (var c in Combattant.Tous.ToArray())
                if (Valide(s, c) && Vector3.Distance(c.Centre, centre) <= rayon + 1.5f) l.Add(c);
            return l;
        }

        /// <summary>
        /// Ce qu'un sort rencontre en premier sur sa trajectoire : une cible hostile (c), ou un obstacle (c nul, point d'impact).
        /// Les alliés, le lanceur et les zones invisibles sont traversés.
        /// </summary>
        public static bool Premier(Incantation s, Vector3 depuis, Vector3 dir, float distance, float rayon, out ICible c, out Vector3 point)
        {
            c = null;
            point = depuis + dir * distance;
            var hits = Physics.SphereCastAll(depuis, rayon, dir, distance, ~0, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                var cible = hit.collider.GetComponentInParent<ICible>();
                if (cible != null)
                {
                    if (ReferenceEquals(cible, s.auteur) || !cible.Vivant) continue;
                    if (!Hostile(s.camp, cible.Camp)) continue;
                    c = cible;
                    point = hit.distance > 0f ? hit.point : depuis;
                    return true;
                }
                if (hit.collider.isTrigger) continue;
                point = hit.distance > 0f ? hit.point : depuis;
                return true;
            }
            // Le relief (le terrain a son collider, mais au cas où)
            Vector3 bout = depuis + dir * distance;
            if (bout.y < Monde.Hauteur(bout.x, bout.z))
            {
                for (int k = 1; k <= 8; k++)
                {
                    Vector3 p = depuis + dir * distance * k / 8f;
                    if (p.y < Monde.Hauteur(p.x, p.z)) { point = p; return true; }
                }
            }
            return false;
        }

        /// <summary>Un éclair : une ligne brisée qui scintille un instant, et un flash de lumière.</summary>
        public static void Eclair(Vector3 a, Vector3 b, Color couleur)
        {
            var go = new GameObject("Eclair");
            var lr = go.AddComponent<LineRenderer>();
            int n = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(a, b) / .8f), 4, 30);
            lr.positionCount = n + 1;
            lr.useWorldSpace = true;
            lr.widthMultiplier = .09f;
            lr.numCapVertices = 2;
            lr.sharedMaterial = Materiau(new Color(.92f, .95f, 1f, 1f));
            lr.startColor = lr.endColor = Color.white;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var e = go.AddComponent<EclairVisuel>();
            e.a = a; e.b = b; e.ligne = lr; e.n = n;
            Lueur(b, couleur, 9f, .18f);
            Lueur(a, couleur, 4f, .12f);
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

    /// <summary>L'éclair qui se redessine plusieurs fois avant de s'effacer.</summary>
    public class EclairVisuel : MonoBehaviour
    {
        public Vector3 a, b;
        public LineRenderer ligne;
        public int n;
        float t, prochain;
        const float Duree = .22f;

        void Update()
        {
            t += Time.deltaTime;
            if (t >= prochain)
            {
                prochain = t + .045f;
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
            float alpha = Mathf.Clamp01(1f - t / Duree);
            ligne.widthMultiplier = .09f * (.4f + alpha);
            ligne.startColor = ligne.endColor = new Color(1f, 1f, 1f, alpha);
            if (t >= Duree) Destroy(gameObject);
        }
    }

    /// <summary>Un projectile de sort : il vole, s'incurve vers la cible verrouillée, et frappe la première chose hostile ou solide.</summary>
    public class ProjectileSort : MonoBehaviour
    {
        Incantation s;
        Vector3 vitesse;
        float parcouru;
        Transform visuel;
        ParticleSystem trainee;
        bool fini;

        public static void Creer(Incantation s)
        {
            var d = s.def;
            var go = new GameObject("Sort_" + d.id);
            go.transform.position = s.origine;
            go.transform.rotation = Quaternion.LookRotation(s.direction);
            var p = go.AddComponent<ProjectileSort>();
            p.s = s;
            p.vitesse = s.direction * d.vitesse;

            // Le cœur : une sphère lumineuse (allongée en éclat pour la glace) et un noyau clair
            bool eclat = d.element == Element.Glace;
            var coeur = GameObject.CreatePrimitive(eclat ? PrimitiveType.Cube : PrimitiveType.Sphere);
            Destroy(coeur.GetComponent<Collider>());
            coeur.transform.SetParent(go.transform, false);
            coeur.transform.localScale = eclat ? new Vector3(d.taille * .7f, d.taille * .7f, d.taille * 3.2f) : Vector3.one * d.taille * 2f;
            if (eclat) coeur.transform.localRotation = Quaternion.Euler(0, 0, 45f);
            var mr = coeur.GetComponent<MeshRenderer>();
            mr.sharedMaterial = Sorts.Materiau(new Color(d.couleur.r, d.couleur.g, d.couleur.b, .8f));
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var noyau = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(noyau.GetComponent<Collider>());
            noyau.transform.SetParent(go.transform, false);
            noyau.transform.localScale = Vector3.one * d.taille * (eclat ? .6f : 1.1f);
            var mn = noyau.GetComponent<MeshRenderer>();
            mn.sharedMaterial = Sorts.Materiau(new Color(1f, .97f, .9f, .95f));
            mn.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            p.visuel = coeur.transform;

            // La lumière qu'il porte
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = d.couleur; l.range = 5f; l.intensity = 2.2f; l.shadows = LightShadows.None;

            // La traînée
            var tg = new GameObject("Trainee");
            tg.transform.SetParent(go.transform, false);
            var ps = tg.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main;
            m.loop = true; m.playOnAwake = false; m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.startLifetime = new ParticleSystem.MinMaxCurve(.25f, .45f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0f, .6f);
            m.startSize = new ParticleSystem.MinMaxCurve(d.taille * .8f, d.taille * 1.6f);
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(d.couleur.r, d.couleur.g, d.couleur.b, .7f), new Color(1f, .95f, .8f, .5f));
            m.gravityModifier = d.element == Element.Feu ? -.3f : 0f;
            var em = ps.emission; em.rateOverTime = 0f; em.rateOverDistance = 14f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = d.taille * .5f;
            var tl = ps.sizeOverLifetime; tl.enabled = true; tl.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            tg.GetComponent<ParticleSystemRenderer>().sharedMaterial = Sorts.Materiau(Color.white);
            ps.Play();
            p.trainee = ps;
            Sons.JouerA(eclat ? "sortGlace" : "sortFeu", s.origine, .7f, eclat ? 1.4f : 1.15f);
        }

        void Update()
        {
            if (fini) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            var d = s.def;
            // S'incurve vers la cible verrouillée
            if (Sorts.Valide(s, s.cible))
            {
                Vector3 voulu = (s.cible.Centre - transform.position).normalized * d.vitesse;
                vitesse = Vector3.RotateTowards(vitesse, voulu, 2.2f * dt, 0f);
            }
            Vector3 pas = vitesse * dt;
            float l = pas.magnitude;
            if (Sorts.Premier(s, transform.position, pas / l, l, d.taille * .7f, out var c, out var point))
            {
                transform.position = point;
                Finir(c);
                return;
            }
            transform.position += pas;
            transform.rotation = Quaternion.LookRotation(vitesse);
            if (visuel != null && d.element == Element.Glace) visuel.Rotate(0, 0, 720f * dt, Space.Self);
            parcouru += l;
            if (parcouru >= d.portee) Finir(null, false);
        }

        void Finir(ICible c, bool impact = true)
        {
            fini = true;
            if (c != null) Sorts.Toucher(s, c, vitesse.normalized);
            else if (impact) Sorts.Impact(s.def.element, transform.position, vitesse.normalized);
            else Effets.Eclat(transform.position, s.def.couleur, 6, 1f, .1f, 0f, .3f);
            // La traînée finit de se dissiper seule
            if (trainee != null)
            {
                trainee.transform.SetParent(null, true);
                trainee.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                Destroy(trainee.gameObject, .6f);
            }
            Destroy(gameObject);
        }
    }
}
