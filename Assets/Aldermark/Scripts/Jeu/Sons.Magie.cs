using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// Les sons des sorts, fabriqués en code : l'incantation qui monte, le feu qui part en soufflant et crépite,
    /// la glace qui tinte, la foudre qui claque, le vent qui siffle, la terre qui gronde, le soin qui carillonne.
    /// Un fichier déposé dans Audio/Sons avec le même nom les remplace.
    /// </summary>
    public partial class Sons
    {
        void ClipsMagie()
        {
            clips["incantation"] = Incantation();
            clips["sortFeu"] = SortFeu();
            clips["sortGlace"] = SortGlace();
            clips["sortFoudre"] = SortFoudre();
            clips["sortVent"] = SortVent();
            clips["sortTerre"] = SortTerre();
            clips["sortSoin"] = SortSoin();
        }

        /// <summary>Un bourdonnement qui monte et s'ouvre : l'énergie qui se rassemble dans la main.</summary>
        AudioClip Incantation()
        {
            const float d = .7f;
            float phase = 0f, b1 = 0f, b2 = 0f;
            return Creer("Incantation", d, (i, t) =>
            {
                float u = t / d;
                float f = Mathf.Lerp(180f, 420f, u * u) * (1f + .012f * Mathf.Sin(t * 2f * Mathf.PI * 6f));
                phase += 2f * Mathf.PI * f / Freq;
                float ton = Mathf.Sin(phase) * .6f + Mathf.Sin(phase * 2.01f) * .25f + Mathf.Sin(phase * 3.02f) * .12f;
                b1 += (Bruit() - b1) * .2f; b2 += (b1 - b2) * .2f;
                float env = Mathf.Clamp01(t / .15f) * Mathf.Clamp01((d - t) / .12f);
                return (ton * .55f + b2 * .5f * u) * env * .5f;
            });
        }

        /// <summary>Un souffle de flamme qui part, puis des crépitements.</summary>
        AudioClip SortFeu()
        {
            const float d = .9f;
            float b1 = 0f, b2 = 0f, b3 = 0f, crepit = 0f;
            return Creer("SortFeu", d, (i, t) =>
            {
                float u = t / d;
                float k = Mathf.Lerp(.35f, .05f, u);
                b1 += (Bruit() - b1) * k; b2 += (b1 - b2) * k; b3 += (b2 - b3) * .02f;
                float souffle = (b2 - b3) * 2.4f * Mathf.Clamp01(t / .03f) * Mathf.Exp(-t * 3.2f);
                if (Bruit() > .996f - u * .004f) crepit = Bruit();
                crepit *= .94f;
                float grondement = Mathf.Sin(t * 2f * Mathf.PI * 70f) * Mathf.Exp(-t * 6f) * .4f;
                return (souffle + crepit * .5f * Mathf.Exp(-t * 2f) + grondement) * .62f;
            });
        }

        /// <summary>Des tintements cristallins, aigus et inharmoniques, avec un petit craquement.</summary>
        AudioClip SortGlace()
        {
            const float d = .8f;
            float[] f = { 2093f, 2637f, 3322f, 4186f, 5274f };
            float[] a = { .5f, .4f, .32f, .25f, .18f };
            float b1 = 0f, b2 = 0f;
            return Creer("SortGlace", d, (i, t) =>
            {
                float s = 0f;
                for (int k = 0; k < f.Length; k++)
                {
                    float debut = k * .045f;
                    if (t < debut) continue;
                    float tt = t - debut;
                    s += Mathf.Sin(2f * Mathf.PI * f[k] * tt) * a[k] * Mathf.Exp(-tt * (6f + k * 1.5f)) * Mathf.Clamp01(tt / .002f);
                }
                b1 += (Bruit() - b1) * .7f; b2 += (b1 - b2) * .3f;
                float craque = (b1 - b2) * Mathf.Exp(-t * 40f) * 1.2f;
                float souffle = b2 * Mathf.Exp(-t * 5f) * .25f;
                return (s * .5f + craque + souffle) * .7f;
            });
        }

        /// <summary>Le claquement sec de la foudre, puis son roulement.</summary>
        AudioClip SortFoudre()
        {
            const float d = 1.4f;
            float b1 = 0f, b2 = 0f, r1 = 0f, r2 = 0f, bourdon = 0f;
            return Creer("SortFoudre", d, (i, t) =>
            {
                float n = Bruit();
                b1 += (n - b1) * .9f; b2 += (b1 - b2) * .5f;
                float claque = (b1 - b2) * Mathf.Exp(-t * 30f) * 2.2f;
                // quelques répliques du claquement
                foreach (float e in new[] { .035f, .07f, .12f })
                    if (t > e) claque += (b1 - b2) * Mathf.Exp(-(t - e) * 45f) * .9f;
                r1 += (n - r1) * .02f; r2 += (r1 - r2) * .02f;
                float roulement = r2 * 9f * Mathf.Clamp01(t / .05f) * Mathf.Exp(-t * 2.2f) * (1f + .5f * Mathf.Sin(t * 13f));
                bourdon += 2f * Mathf.PI * 120f / Freq;
                float elec = Mathf.Sign(Mathf.Sin(bourdon)) * .08f * Mathf.Exp(-t * 9f);
                return (claque + roulement + elec) * .58f;
            });
        }

        /// <summary>Une rafale : un souffle dont le filtre balaie vers l'aigu puis retombe.</summary>
        AudioClip SortVent()
        {
            const float d = .9f;
            float b1 = 0f, b2 = 0f, b3 = 0f;
            return Creer("SortVent", d, (i, t) =>
            {
                float u = t / d;
                float k = Mathf.Lerp(.02f, .25f, Mathf.Sin(Mathf.Min(1f, u * 1.6f) * Mathf.PI * .9f));
                b1 += (Bruit() - b1) * k; b2 += (b1 - b2) * k; b3 += (b2 - b3) * .01f;
                float env = Mathf.Sin(Mathf.Pow(u, .4f) * Mathf.PI);
                return (b2 - b3) * 3.5f * env * env * .6f;
            });
        }

        /// <summary>Un coup sourd dans le sol, puis la terre qui gronde et retombe.</summary>
        AudioClip SortTerre()
        {
            const float d = 1.3f;
            float r1 = 0f, r2 = 0f, g = 0f, cailloux = 0f;
            return Creer("SortTerre", d, (i, t) =>
            {
                float f = Mathf.Lerp(75f, 38f, Mathf.Min(1f, t / .4f));
                g += 2f * Mathf.PI * f / Freq;
                float coup = Mathf.Sin(g) * Mathf.Exp(-t * 5f) * Mathf.Clamp01(t / .004f);
                float n = Bruit();
                r1 += (n - r1) * .04f; r2 += (r1 - r2) * .04f;
                float gronde = r2 * 6f * Mathf.Exp(-t * 2.5f);
                if (t > .15f && n > .997f) cailloux = Bruit();
                cailloux *= .9f;
                return (coup * .9f + gronde + cailloux * .3f * Mathf.Exp(-t * 1.5f)) * .58f;
            });
        }

        /// <summary>Un accord doux qui s'égrène et scintille.</summary>
        AudioClip SortSoin()
        {
            const float d = 1.6f;
            float[] f = { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f };
            return Creer("SortSoin", d, (i, t) =>
            {
                float s = 0f;
                for (int k = 0; k < f.Length; k++)
                {
                    float debut = k * .09f;
                    if (t < debut) continue;
                    float tt = t - debut;
                    float env = Mathf.Clamp01(tt / .04f) * Mathf.Exp(-tt * 2.2f);
                    s += (Mathf.Sin(2f * Mathf.PI * f[k] * tt) + .3f * Mathf.Sin(4f * Mathf.PI * f[k] * tt)) * env;
                }
                float fin = Mathf.Clamp01((d - t) / .3f);
                return s * .16f * fin;
            });
        }
    }
}
