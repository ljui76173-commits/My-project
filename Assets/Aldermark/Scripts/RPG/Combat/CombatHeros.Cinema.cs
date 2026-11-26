using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// L'épée plantée dans le sol, en grand : quand un coup te repousse fort et que tu plantes ta lame pour freiner,
    /// la caméra coupe sur un plan au ras du sol, à côté de la lame, le temps ralentit, et l'on te voit reculer
    /// pendant que la pointe laboure la terre. Plus le coup était fort, plus le sillon est large et profond :
    /// bourrelets de terre sur les côtés, tas de terre et trou au bout.
    /// </summary>
    public partial class CombatHeros
    {
        bool glissadePieds, cinema;
        float intensiteGlissade, tCinema, attenteFinCinema, prochaineCinema;
        int compteBourrelets;
        Vector3 camCinema;
        static readonly Color Terre = new Color(.33f, .25f, .17f);

        /// <summary>La pointe de l'épée plantée, au niveau du sol.</summary>
        Vector3 PointeLame()
        {
            Vector3 p = VersMonde(MainPlantee) + DirMonde(LamePlantee).normalized * .95f;
            p.y = Mathf.Max(Monde.Hauteur(p.x, p.z), p.y - 1f);
            return p;
        }

        /// <summary>L'instant où la lame mord le sol.</summary>
        void PlanterLame(float distance)
        {
            float I = intensiteGlissade;
            Vector3 p = PointeLame();
            Vector3 sol = new Vector3(p.x, Monde.Hauteur(p.x, p.z), p.z);
            Effets.Eclat(sol, Terre, 10 + Mathf.RoundToInt(20 * I), 4f + 3f * I, .16f + .1f * I, 2.4f, .7f, Vector3.up - dirGlissade * .5f);
            Effets.Eclat(sol, new Color(1f, .85f, .5f), 8 + Mathf.RoundToInt(14 * I), 7f, .05f, 1.2f, .35f, Vector3.up + transform.right * .3f);
            Effets.Poussiere(sol, 8 + Mathf.RoundToInt(14 * I));
            if (I > .35f)
            {
                Effets.OndeDeChoc(sol, 1f + 2f * I, new Color(.85f, .75f, .6f, .7f));
                Sons.Jouer("onde", .3f + .3f * I, .55f);
            }
            if (distance >= 2.6f) DebutCinema();
        }

        /// <summary>De la terre soulevée sur les deux bords du sillon (une motte sur deux appels, pour ne pas en semer trop).</summary>
        void Bourrelets(Vector3 sol, float I)
        {
            if (++compteBourrelets % 2 != 0) return;
            Vector3 cote = Vector3.Cross(Vector3.up, dirGlissade).normalized;
            float largeur = .18f + .34f * I;
            foreach (float s in new[] { -1f, 1f })
                Motte(sol + cote * s * (largeur * .5f + .05f), .07f + .12f * I, 14f + 16f * I);
        }

        /// <summary>Le bout de la glissade : la terre poussée par la lame s'entasse, et il reste un trou.</summary>
        void FinGlissade()
        {
            float I = intensiteGlissade;
            if (glissadePieds || I < .1f) return;
            Vector3 p = PointeLame();
            Vector3 sol = new Vector3(p.x, Monde.Hauteur(p.x, p.z), p.z);
            int n = 3 + Mathf.RoundToInt(4 * I);
            for (int k = 0; k < n; k++)
            {
                Vector3 q = sol + dirGlissade * Random.Range(.1f, .35f) + Random.insideUnitSphere * (.15f + .2f * I);
                Motte(new Vector3(q.x, sol.y, q.z), .12f + .2f * I, 18f + 20f * I);
            }
            Trou(sol, .35f + .6f * I, 20f + 25f * I);
            Effets.Eclat(sol, Terre, 12 + Mathf.RoundToInt(16 * I), 3f, .18f, 2.2f, .7f, Vector3.up);
            Effets.Poussiere(sol, 10 + Mathf.RoundToInt(10 * I));
            j.Secouer(.04f + .06f * I);
        }

        static void Motte(Vector3 sol, float taille, float duree) => Sorts.Motte(sol, taille, duree, Terre);
        static void Trou(Vector3 sol, float rayon, float duree) => Sorts.Trou(sol, rayon, duree);

        float prochainArret;

        /// <summary>Un arrêt sur image bref quand un coup ou un sort puissant porte (pas plus d'un tous les tiers de seconde).</summary>
        public void ArretSurImage(float secondes)
        {
            if (Time.unscaledTime < prochainArret || finFige > 0f) return;
            prochainArret = Time.unscaledTime + .35f;
            Figer(secondes);
        }

        // ---------- le plan de cinéma ----------

        void DebutCinema()
        {
            if (cinema || mort || Time.unscaledTime < prochaineCinema || j == null) return;
            prochaineCinema = Time.unscaledTime + 6f;
            cinema = true; tCinema = 0f; attenteFinCinema = 0f;
            CalculerPlan(0f, out camCinema, out var cible);
            j.cinemaPosition = camCinema;
            j.cinemaRotation = Quaternion.LookRotation(cible - camCinema);
            j.cinemaFov = 46f;
            j.DebutCinema();
            Ralentir(.5f + .3f * intensiteGlissade);
        }

        /// <summary>
        /// Le plan : au ras du sol, du côté de l'épée, un peu en avant dans le sens de la glissade, regardant la lame et le héros.
        /// La caméra recule doucement (travelling arrière) pendant que le héros glisse.
        /// </summary>
        void CalculerPlan(float t, out Vector3 pos, out Vector3 cible)
        {
            Vector3 heros = transform.position;
            Vector3 cote = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            float u = Mathf.Clamp01(t / .9f);
            u = u * u * (3f - 2f * u);
            float ecart = Mathf.Lerp(1.25f, 2.5f, u);
            pos = heros + cote * ecart + Vector3.up * Mathf.Lerp(.28f, .6f, u) + dirGlissade * Mathf.Lerp(.7f, 1.3f, u);
            Vector3 pointe = PointeLame();
            cible = Vector3.Lerp(pointe, heros + Vector3.up * 1.15f, Mathf.Lerp(.35f, .6f, u));
            // Ni dans le sol, ni derrière un mur
            float sol = Monde.Hauteur(pos.x, pos.z) + .22f;
            if (pos.y < sol) pos.y = sol;
            Vector3 depuis = heros + Vector3.up * 1.1f;
            Vector3 vers = pos - depuis;
            if (Physics.SphereCast(depuis, .2f, vers.normalized, out var hit, vers.magnitude, ~0, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(transform))
                pos = depuis + vers.normalized * Mathf.Max(.4f, hit.distance - .1f);
        }

        void MettreAJourCinema()
        {
            if (!cinema) return;
            float dt = Time.unscaledDeltaTime;
            tCinema += dt;
            CalculerPlan(tCinema, out var voulu, out var cible);
            camCinema = Vector3.Lerp(camCinema, voulu, 1f - Mathf.Exp(-dt * 8f));
            j.cinemaPosition = camCinema;
            Vector3 regard = cible - camCinema;
            if (regard.sqrMagnitude > .001f)
                j.cinemaRotation = Quaternion.Slerp(j.cinemaRotation, Quaternion.LookRotation(regard), 1f - Mathf.Exp(-dt * 12f));
            j.cinemaFov = Mathf.Lerp(46f, 60f, Mathf.Clamp01(tCinema / 1.4f));
            // On garde le plan un instant après l'arrêt, puis la caméra revient
            if (etat != Etat.Glissade) attenteFinCinema += dt;
            if (attenteFinCinema > .45f || tCinema > 4f) FinirCinema();
        }

        void FinirCinema()
        {
            if (!cinema) return;
            cinema = false;
            if (j != null) j.FinCinema();
        }
    }

    /// <summary>Un débris qui reste au sol un moment, puis s'y enfonce et disparaît.</summary>
    public class Enfouir : MonoBehaviour
    {
        public float duree = 15f;
        float t;
        Vector3 depart;
        float hauteur;

        void Start()
        {
            depart = transform.position;
            hauteur = Mathf.Max(.05f, transform.lossyScale.y * 1.1f);
        }

        void Update()
        {
            t += Time.deltaTime;
            if (t < duree) return;
            float u = (t - duree) / 1.5f;
            transform.position = depart - Vector3.up * hauteur * Mathf.Clamp01(u);
            if (u >= 1f) Destroy(gameObject);
        }
    }
}
