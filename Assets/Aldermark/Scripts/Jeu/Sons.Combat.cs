using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// Sons de combat fabriqués en code : la lame qui fend l'air, les impacts (chair, bois),
    /// le fracas d'une parade, la roulade, le souffle du héros touché, le raclement de l'épée
    /// plantée qui glisse, l'onde de choc. Remplaçables plus tard par de vrais enregistrements.
    /// </summary>
    public partial class Sons
    {
        void ClipsCombat()
        {
            clips["lame"] = Fendre(.24f, 1f);
            clips["lameLourde"] = Fendre(.42f, .65f);
            clips["impactChair"] = ImpactSourd();
            clips["impactBois"] = ImpactBois();
            clips["parade"] = Fracas();
            clips["roulade"] = Roulade();
            clips["blesse"] = Blesse();
            clips["raclement"] = Raclement();
            clips["onde"] = OndeDeChoc();
        }

        /// <summary>Une lame qui fend l'air : un souffle dont le filtre s'ouvre au milieu du geste.</summary>
        AudioClip Fendre(float d, float brillance)
        {
            float b1 = 0, b2 = 0, b3 = 0;
            return Creer("Lame", d, (i, t) =>
            {
                float u = t / d;
                float env = Mathf.Sin(Mathf.Pow(u, .55f) * Mathf.PI);
                float k = Mathf.Lerp(.015f, .32f * brillance, Mathf.Sin(u * Mathf.PI));
                b1 += (Bruit() - b1) * k;
                b2 += (b1 - b2) * k;
                b3 += (b2 - b3) * .05f;
                return (b2 - b3) * 3.2f * env * env;
            });
        }

        /// <summary>Un coup qui porte : un choc grave qui retombe, et un peu de souffle.</summary>
        AudioClip ImpactSourd()
        {
            float phase = 0, b = 0;
            const float d = .32f;
            return Creer("ImpactChair", d, (i, t) =>
            {
                float f = Mathf.Lerp(120f, 42f, Mathf.Clamp01(t / .18f));
                phase += 2f * Mathf.PI * f / Freq;
                b += (Bruit() - b) * .25f;
                return Mathf.Sin(phase) * Mathf.Exp(-t * 16f) * .9f + b * Mathf.Exp(-t * 45f) * .9f;
            });
        }

        /// <summary>Un coup sur du bois : quelques résonances sèches et un claquement.</summary>
        AudioClip ImpactBois()
        {
            float b = 0;
            float[] f = { 190f, 415f, 770f, 1260f }, a = { .55f, .35f, .22f, .12f }, k = { 14f, 22f, 32f, 48f };
            return Creer("ImpactBois", .36f, (i, t) =>
            {
                float s = 0;
                for (int n = 0; n < f.Length; n++) s += Mathf.Sin(2f * Mathf.PI * f[n] * t) * a[n] * Mathf.Exp(-t * k[n]);
                b += (Bruit() - b) * .5f;
                return s + b * Mathf.Exp(-t * 110f) * .8f;
            });
        }

        /// <summary>Le fracas métallique d'une parade : des partiels inharmoniques qui sonnent longtemps.</summary>
        AudioClip Fracas()
        {
            float[] f = { 523f, 1187f, 1742f, 2311f, 3156f, 4210f }, a = { .4f, .3f, .26f, .2f, .14f, .1f }, k = { 3f, 4.4f, 6f, 8f, 11f, 15f };
            return Creer("Parade", 1.3f, (i, t) =>
            {
                float s = 0;
                for (int n = 0; n < f.Length; n++) s += Mathf.Sin(2f * Mathf.PI * f[n] * t * (1f + .002f * Mathf.Sin(t * 9f + n))) * a[n] * Mathf.Exp(-t * k[n]);
                return s * .8f + Bruit() * Mathf.Exp(-t * 90f) * .55f;
            });
        }

        /// <summary>Une roulade : froissement de tissu et le dos qui touche le sol.</summary>
        AudioClip Roulade()
        {
            float b1 = 0, b2 = 0, phase = 0;
            const float d = .46f;
            return Creer("Roulade", d, (i, t) =>
            {
                b1 += (Bruit() - b1) * .09f;
                b2 += (b1 - b2) * .09f;
                float froissement = b2 * 2.2f * Mathf.Sin(Mathf.Clamp01(t / d) * Mathf.PI);
                float u = t - .26f, choc = 0;
                if (u > 0) { phase += 2f * Mathf.PI * 70f / Freq; choc = Mathf.Sin(phase) * Mathf.Exp(-u * 25f) * .6f; }
                return froissement + choc;
            });
        }

        /// <summary>Le héros touché : un souffle court, bouche fermée.</summary>
        AudioClip Blesse()
        {
            float phase = 0, b = 0, filtre = 0;
            const float d = .24f;
            return Creer("Blesse", d, (i, t) =>
            {
                float f = Mathf.Lerp(155f, 105f, t / d);
                phase += f / Freq;
                float scie = (phase % 1f) * 2f - 1f;
                filtre += (scie - filtre) * .06f;
                b += (Bruit() - b) * .2f;
                float env = Mathf.Clamp01(t / .012f) * Mathf.Exp(-t * 11f);
                return (filtre * 1.6f + b * .35f) * env;
            });
        }

        /// <summary>L'épée plantée qui racle le sol : un grain irrégulier et une pointe de métal.</summary>
        AudioClip Raclement()
        {
            float b1 = 0, b2 = 0, grain = 0;
            const float d = 1.1f;
            return Creer("Raclement", d, (i, t) =>
            {
                float n = Bruit();
                b1 += (n - b1) * .55f;
                b2 += (b1 - b2) * .2f;
                if (Bruit() > .995f) grain = 1f;
                grain *= .995f;
                float metal = Mathf.Sin(2f * Mathf.PI * 2380f * t) * .12f + Mathf.Sin(2f * Mathf.PI * 3510f * t) * .06f;
                float env = Mathf.Clamp01(t / .02f) * Mathf.Pow(1f - t / d, 1.6f);
                return ((b1 - b2) * (1.1f + grain * 1.5f) + metal * (.5f + grain)) * env;
            });
        }

        /// <summary>Une onde de choc : un grondement grave qui descend.</summary>
        AudioClip OndeDeChoc()
        {
            float phase = 0, b1 = 0, b2 = 0;
            const float d = 1.3f;
            return Creer("Onde", d, (i, t) =>
            {
                float f = Mathf.Lerp(72f, 26f, Mathf.Clamp01(t / .9f));
                phase += 2f * Mathf.PI * f / Freq;
                b1 += (Bruit() - b1) * .04f;
                b2 += (b1 - b2) * .04f;
                return Mathf.Sin(phase) * Mathf.Exp(-t * 3.2f) + b2 * 5f * Mathf.Exp(-t * 4.5f) + Bruit() * Mathf.Exp(-t * 60f) * .4f;
            });
        }
    }
}
