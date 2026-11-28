using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// Les formes des sorts évolués et fusionnés : éventail de lames, pluie de météores, éclair et orage venus du ciel,
    /// séisme en plusieurs ondes, ligne de pics, zone élémentaire à distance (prison de glace),
    /// tornade qui avance, sanctuaire qui dure (lumière ou lave).
    /// </summary>
    public static partial class Sorts
    {
        static Vector3 AuSol(Vector3 p) => new Vector3(p.x, Monde.Hauteur(p.x, p.z), p.z);

        /// <summary>Le point visé, ramené à portée.</summary>
        static Vector3 PointVise(Incantation s)
        {
            Vector3 p = Valide(s, s.cible) ? s.cible.Centre : s.point;
            Vector3 depuis = s.auteur != null && Cibles.Existe(s.auteur) ? s.auteur.Corps.position : s.origine;
            Vector3 v = Vector3.ProjectOnPlane(p - depuis, Vector3.up);
            if (v.magnitude > s.def.portee) p = depuis + v.normalized * s.def.portee;
            return AuSol(p);
        }

        /// <summary>Plusieurs projectiles en éventail (lames de vent).</summary>
        static void Eventail(Incantation s)
        {
            var d = s.def;
            int n = Mathf.Max(1, d.nombre);
            for (int k = 0; k < n; k++)
            {
                float angle = n == 1 ? 0f : Mathf.Lerp(-d.ecart * .5f, d.ecart * .5f, k / (float)(n - 1));
                var s2 = s;
                s2.direction = Quaternion.AngleAxis(angle, Vector3.up) * s.direction;
                s2.cible = k == n / 2 ? s.cible : null;   // seule la lame du milieu suit la cible
                ProjectileSort.Creer(s2);
            }
        }

        /// <summary>Des météores tombent du ciel sur la zone visée, l'un après l'autre.</summary>
        static void Pluie(Incantation s)
        {
            var d = s.def;
            float a = Ampleur(s.Visuel);
            Vector3 centre = PointVise(s);
            float rayon = d.rayon * (1f + .4f * (a - 1f));
            var c = CercleMagique.Creer(centre + Vector3.up * .06f, Quaternion.LookRotation(Vector3.down, Vector3.forward), rayon, d.couleur, d.element, Palier(s.Visuel), true);
            c.Progres = 1f;
            int n = Mathf.Max(1, d.nombre) + Mathf.Min(8, (int)((a - 1f) * 5f));
            for (int k = 0; k < n; k++)
            {
                float t = k * .14f + Random.Range(0f, .06f);
                Retard.Lancer(t, () =>
                {
                    Vector2 r = Random.insideUnitCircle * rayon * .85f;
                    Vector3 sol = AuSol(centre + new Vector3(r.x, 0, r.y));
                    Vector3 haut = sol + new Vector3(Random.Range(-3f, 3f), 18f, Random.Range(-3f, 3f));
                    var m = s;
                    m.origine = haut; m.direction = (sol - haut).normalized; m.cible = null;
                    m.portee = 30f; m.eclaboussure = d.eclaboussure;
                    ProjectileSort.Creer(m);
                });
            }
            Retard.Lancer(n * .14f + .6f, () => { if (c != null) c.Liberer(); });
        }

        /// <summary>Un éclair tombe du ciel sur la cible (et foudroie autour) ; un orage frappe plusieurs fois des cibles au hasard.</summary>
        static void Ciel(Incantation s)
        {
            var d = s.def;
            Vector3 centre = PointVise(s);
            Frappe(s, Valide(s, s.cible) ? s.cible.Centre : centre + Vector3.up);
            if (d.vagues <= 1) return;
            float a = Ampleur(s.Visuel);
            var c = CercleMagique.Creer(centre + Vector3.up * 22f, Quaternion.LookRotation(Vector3.down, Vector3.forward), d.rayon * .9f * Mathf.Sqrt(a), d.couleur, d.element, Palier(s.Visuel), true);
            c.Progres = 1f;
            for (int k = 1; k < d.vagues; k++)
                Retard.Lancer(k * .45f, () =>
                {
                    // Une cible au hasard dans la zone, sinon un point au hasard
                    var proies = new List<ICible>();
                    foreach (var x in Autour(s, centre, d.rayon)) proies.Add(x);
                    Vector2 r = Random.insideUnitCircle * d.rayon;
                    Vector3 p = proies.Count > 0 ? proies[Random.Range(0, proies.Count)].Centre : AuSol(centre + new Vector3(r.x, 0, r.y)) + Vector3.up;
                    Frappe(s, p);
                });
            Retard.Lancer(d.vagues * .45f + .3f, () => { if (c != null) c.Liberer(); });
        }

        /// <summary>Un éclair qui tombe du ciel en un point : tout ce qui est autour est foudroyé.</summary>
        static void Frappe(Incantation s, Vector3 p)
        {
            var d = s.def;
            float a = Ampleur(s.Visuel);
            Vector3 sol = AuSol(p);
            Vector3 haut = sol + Vector3.up * 28f + new Vector3(Random.Range(-2f, 2f), 0, Random.Range(-2f, 2f));
            Eclair(haut, sol, d.couleur, s.Visuel);
            EclairVisuel.Creer(haut, sol, .14f * a, .3f, 1f);
            Sons.JouerA("sortFoudre", sol, 1f, Random.Range(.8f, .95f), 140f);
            Effets.OndeDeChoc(sol, (d.vagues > 1 ? 2.2f : d.rayon) * Mathf.Sqrt(a), new Color(.85f, .85f, 1f, .85f));
            Effets.Sillon(sol, Vector3.forward, .6f * a, .6f * a, 18f);
            Deflagration(d.element, sol + Vector3.up * .5f, Vector3.down, s.Visuel, s.camp);
            float rayon = d.vagues > 1 ? 2.2f : d.rayon;
            foreach (var c in Autour(s, sol, rayon * Mathf.Sqrt(a)))
                Toucher(s, c, Vector3.ProjectOnPlane(c.Centre - sol, Vector3.up).normalized);
        }

        /// <summary>Plusieurs ondes de plus en plus larges.</summary>
        static void Seisme(Incantation s)
        {
            var d = s.def;
            float rayon = s.rayon > 0f ? s.rayon : d.rayon;
            Zone(s);
            for (int v = 1; v < d.vagues; v++)
            {
                var s2 = s;
                s2.rayon = rayon * (1f + .4f * v);
                Retard.Lancer(v * .42f, () => Zone(s2));
            }
        }

        /// <summary>Une ligne de pics de pierre qui jaillit du sol jusqu'à la cible.</summary>
        static void Ligne(Incantation s)
        {
            var d = s.def;
            float a = Ampleur(s.Visuel);
            Vector3 depart = s.auteur != null && Cibles.Existe(s.auteur) ? s.auteur.Corps.position : s.origine;
            Vector3 dir = Vector3.ProjectOnPlane(s.direction, Vector3.up);
            dir = dir.sqrMagnitude > .01f ? dir.normalized : Vector3.forward;
            float longueur = d.portee;
            if (Valide(s, s.cible)) longueur = Mathf.Min(d.portee, Vector3.ProjectOnPlane(s.cible.Centre - depart, Vector3.up).magnitude + 2f);
            int n = Mathf.Max(3, Mathf.CeilToInt(longueur / 1.5f));
            var touches = new HashSet<ICible>();
            for (int k = 0; k < n; k++)
            {
                Vector3 p = AuSol(depart + dir * (1.4f + k * 1.5f));
                bool fort = k % 3 == 0;
                Retard.Lancer(k * .05f, () =>
                {
                    Pics(p, fort ? 4 : 3, .45f * a, (fort ? 1.5f : 1.1f) * a, new Color(.48f, .4f, .32f), false, .9f);
                    Effets.Terre(p, Vector3.up * 2f);
                    if (fort) Sons.JouerA("sortTerre", p, .5f, 1.3f, 50f);
                    foreach (var c in Autour(s, p, 1.4f * Mathf.Sqrt(a)))
                    {
                        if (touches.Contains(c)) continue;
                        touches.Add(c);
                        Toucher(s, c, dir);
                        if (c is Monstre m && m.Vivant && m.stature < 1f) m.Pousser(dir * 1.5f, 6f);
                    }
                });
            }
            var h = CombatHeros.Instance;
            if (h != null && ReferenceEquals(s.auteur, h)) h.Secousse(.06f * a);
        }

        /// <summary>Une zone d'un autre élément que la terre (la prison de glace) : elle jaillit au point visé.</summary>
        static void ZoneElementaire(Incantation s, Vector3 centre, float rayon)
        {
            var d = s.def;
            float a = Ampleur(s.Visuel);
            Color c = d.couleur;
            Effets.OndeDeChoc(centre, rayon, new Color(c.r, c.g, c.b, .85f));
            Deflagration(d.element, centre + Vector3.up * .5f, Vector3.up, s.Visuel, s.camp);
            if (d.element == Element.Glace)
            {
                Pics(centre, 10 + 2 * Palier(s.Visuel), rayon * .85f, 1.4f * a, new Color(.72f, .9f, 1f, .7f), true, 2.6f);
                Pics(centre, 5, rayon * .4f, 1f * a, new Color(.8f, .95f, 1f, .65f), true, 2.4f);
                Effets.Eclat(centre + Vector3.up, new Color(.85f, .96f, 1f), 40, 4f, .1f, .6f, 1f);
            }
            else Effets.Eclat(centre + Vector3.up, c, 30, 4f, .14f, .2f, .8f);
            foreach (var x in Autour(s, centre, rayon))
                Toucher(s, x, Vector3.ProjectOnPlane(x.Centre - centre, Vector3.up).normalized);
        }

    }

    /// <summary>Une tornade (ou une tempête de feu) qui avance, soulève et fouette ce qu'elle traverse.</summary>
    public class TornadeSort : MonoBehaviour
    {
        Incantation s;
        Vector3 dir;
        float t, prochainCoup, rayon;
        ParticleSystem tourbillon;
        Light lumiere;

        public static void Creer(Incantation s)
        {
            var d = s.def;
            float a = Sorts.Ampleur(s.Visuel);
            Vector3 dir = Vector3.ProjectOnPlane(s.direction, Vector3.up);
            dir = dir.sqrMagnitude > .01f ? dir.normalized : Vector3.forward;
            Vector3 depart = s.auteur != null && Cibles.Existe(s.auteur) ? s.auteur.Corps.position : s.origine;
            depart += dir * 1.8f;
            depart.y = Monde.Hauteur(depart.x, depart.z);
            var go = new GameObject("Tornade");
            go.transform.position = depart;
            var tr = go.AddComponent<TornadeSort>();
            tr.s = s; tr.dir = dir;
            tr.rayon = d.rayon * (1f + .4f * (a - 1f));

            // Le tourbillon : des particules qui montent en tournant, de plus en plus larges vers le haut
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main;
            m.loop = true; m.playOnAwake = false; m.simulationSpace = ParticleSystemSimulationSpace.Local;
            m.startLifetime = new ParticleSystem.MinMaxCurve(.8f, 1.4f);
            m.startSpeed = 0f;
            m.startSize = new ParticleSystem.MinMaxCurve(.15f * a, .5f * a);
            bool feu = d.element == Element.Feu;
            m.startColor = feu
                ? new ParticleSystem.MinMaxGradient(new Color(1f, .45f, .1f, .8f), new Color(1f, .85f, .3f, .8f))
                : new ParticleSystem.MinMaxGradient(new Color(.85f, .92f, .88f, .55f), new Color(.6f, .66f, .55f, .7f));
            m.maxParticles = 600;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = tr.rayon * .35f; sh.rotation = new Vector3(-90f, 0, 0);
            var em = ps.emission; em.rateOverTime = 160f * a;
            var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.Local;
            v.x = new ParticleSystem.MinMaxCurve(0f); v.z = new ParticleSystem.MinMaxCurve(0f);
            v.y = new ParticleSystem.MinMaxCurve(4.5f * a);
            v.orbitalY = new ParticleSystem.MinMaxCurve(9f);
            v.radial = new ParticleSystem.MinMaxCurve(.9f * a);
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Effets.Particules();
            ps.Play();
            tr.tourbillon = ps;
            if (feu)
            {
                tr.lumiere = go.AddComponent<Light>();
                tr.lumiere.type = LightType.Point; tr.lumiere.color = d.couleur; tr.lumiere.range = 10f * a; tr.lumiere.intensity = 2.5f; tr.lumiere.shadows = LightShadows.None;
            }
            Sons.JouerA("sortVent", depart, 1f, .7f, 80f);
        }

        void Update()
        {
            var d = s.def;
            float dt = Time.deltaTime;
            t += dt;
            Vector3 p = transform.position + dir * d.vitesse * dt;
            p.y = Monde.Hauteur(p.x, p.z);
            transform.position = p;
            if (lumiere != null) lumiere.intensity = 2f + Mathf.PerlinNoise(t * 6f, 0f);
            if (t < d.duree && t >= prochainCoup)
            {
                prochainCoup = t + .35f;
                Effets.Poussiere(p, 6);
                if (Random.value < .3f) Sons.JouerA("sortVent", p, .5f, Random.Range(.6f, .8f), 60f);
                foreach (var c in Combattant.Tous.ToArray())
                {
                    if (!Sorts.Valide(s, c)) continue;
                    Vector3 vers = Vector3.ProjectOnPlane(c.Centre - p, Vector3.up);
                    if (vers.magnitude > rayon) continue;
                    Sorts.Toucher(s, c, vers.sqrMagnitude > .01f ? vers.normalized : dir);
                    if (c is Monstre m && m.Vivant && m.stature < 1f) m.Pousser(dir * 2.5f - vers.normalized * 1.5f, 4.5f);
                }
                var h = CombatHeros.Instance;
                if (h != null && Sorts.Valide(s, h) && Vector3.ProjectOnPlane(h.Centre - p, Vector3.up).magnitude <= rayon)
                    Sorts.Toucher(s, h, dir);
            }
            if (t >= d.duree && tourbillon != null && tourbillon.isEmitting)
                tourbillon.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (t >= d.duree + 1.5f) Destroy(gameObject);
        }
    }

    /// <summary>
    /// Une zone qui dure : le sanctuaire de lumière (soigne son lanceur, brûle ses ennemis)
    /// ou le magma (une éruption, puis la lave qui brûle tout ce qui s'y aventure).
    /// </summary>
    public class SanctuaireSort : MonoBehaviour
    {
        Incantation s;
        float t, prochain, rayon;
        CercleMagique cercle;
        Light lumiere;

        public static void Creer(Incantation s)
        {
            var d = s.def;
            float a = Sorts.Ampleur(s.Visuel);
            Vector3 centre = d.aDistance ? s.point : s.auteur != null && Cibles.Existe(s.auteur) ? s.auteur.Corps.position : s.origine;
            centre.y = Monde.Hauteur(centre.x, centre.z);
            var go = new GameObject(d.nom);
            go.transform.position = centre;
            var z = go.AddComponent<SanctuaireSort>();
            z.s = s;
            z.rayon = (s.rayon > 0f ? s.rayon : d.rayon) * (1f + .3f * (a - 1f));
            z.cercle = CercleMagique.Creer(centre + Vector3.up * .06f, Quaternion.LookRotation(Vector3.down, Vector3.forward), z.rayon, d.couleur, d.element, Sorts.Palier(s.Visuel), false);
            z.cercle.Progres = 1f;
            z.lumiere = go.AddComponent<Light>();
            z.lumiere.type = LightType.Point; z.lumiere.color = d.couleur; z.lumiere.range = z.rayon * 3f; z.lumiere.intensity = 2.5f; z.lumiere.shadows = LightShadows.None;
            if (d.element == Element.Feu)
            {
                // L'éruption : la terre s'ouvre, la lave jaillit
                Sorts.Trou(centre, z.rayon, d.duree + 8f);
                Sorts.Pics(centre, 10, z.rayon * .9f, 1.2f * a, new Color(.25f, .18f, .14f), false, d.duree);
                Effets.Eclat(centre + Vector3.up * .3f, new Color(1f, .45f, .05f), 60, 9f * Mathf.Sqrt(a), .25f, 1.4f, 1.1f, Vector3.up);
                Sorts.Deflagration(Element.Feu, centre + Vector3.up * .5f, Vector3.up, s.Visuel, s.camp);
                Effets.OndeDeChoc(centre, z.rayon * 1.3f, new Color(1f, .45f, .1f, .9f));
                Sons.JouerA("sortTerre", centre, 1f, .7f, 100f);
                if (d.eruption > 0)
                    foreach (var c in Combattant.Tous.ToArray())
                        if (Sorts.Valide(s, c) && Vector3.Distance(c.Centre, centre) <= z.rayon + 1f)
                        {
                            Sorts.Toucher(s, c, Vector3.ProjectOnPlane(c.Centre - centre, Vector3.up).normalized, d.eruption / System.Math.Max(.01, d.puissance));
                            if (c is Monstre m && m.Vivant && m.stature < 1f) m.Pousser(Vector3.zero, 7f);
                        }
            }
            else
            {
                Sorts.Colonne(centre, z.rayon * .4f, 14f, new Color(d.couleur.r, d.couleur.g, d.couleur.b, .35f));
                Sons.JouerA("sortSoin", centre, 1f, .8f);
            }
        }

        void Update()
        {
            var d = s.def;
            t += Time.deltaTime;
            Vector3 c0 = transform.position;
            bool feu = d.element == Element.Feu;
            if (lumiere != null) lumiere.intensity = (t < d.duree ? 1f : Mathf.Max(0f, 1f - (t - d.duree))) * (feu ? 2f + Mathf.PerlinNoise(t * 5f, 1f) : 2.2f);
            if (t < d.duree)
            {
                // Ce qui monte de la zone : des braises, ou des étincelles de lumière
                if (Random.value < Time.deltaTime * 30f)
                {
                    Vector2 r = Random.insideUnitCircle * rayon;
                    Vector3 p = c0 + new Vector3(r.x, .1f, r.y);
                    p.y = Monde.Hauteur(p.x, p.z) + .1f;
                    Effets.Eclat(p, feu ? new Color(1f, Random.Range(.3f, .6f), .05f) : d.couleur, 2, .8f, feu ? .18f : .1f, feu ? -.6f : -.9f, 1f);
                }
                if (t >= prochain)
                {
                    prochain = t + .5f;
                    foreach (var c in Combattant.Tous.ToArray())
                        if (Sorts.Valide(s, c) && Vector3.ProjectOnPlane(c.Centre - c0, Vector3.up).magnitude <= rayon)
                            Sorts.Toucher(s, c, Vector3.up);
                    var h = CombatHeros.Instance;
                    if (h != null && Sorts.Valide(s, h) && Vector3.ProjectOnPlane(h.Centre - c0, Vector3.up).magnitude <= rayon)
                        Sorts.Toucher(s, h, Vector3.up);
                    // Le sanctuaire soigne son lanceur tant qu'il reste dedans
                    if (s.soin > 0 && Cibles.Existe(s.auteur) && s.auteur.Vivant && Vector3.ProjectOnPlane(s.auteur.Centre - c0, Vector3.up).magnitude <= rayon)
                    {
                        double avant = s.auteur.VieActuelle;
                        if (s.auteur is CombatHeros && StatsHeros.Instance != null) StatsHeros.Instance.Rendre(s.soin);
                        else if (s.auteur is Combattant cb) cb.Rendre(s.soin);
                        double rendu = s.auteur.VieActuelle - avant;
                        if (rendu > 0) HUD.ChiffreTexte(s.auteur.Centre + Vector3.up * .6f, "+" + Nombres.Court(rendu), new Color(.55f, 1f, .55f));
                    }
                }
            }
            else if (cercle != null) { cercle.Liberer(); cercle = null; }
            if (t >= d.duree + 1.2f) Destroy(gameObject);
        }
    }
}
