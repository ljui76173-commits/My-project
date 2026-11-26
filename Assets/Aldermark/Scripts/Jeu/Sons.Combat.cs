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
            clips["hurlement"] = Hurlement();
            clips["grognement"] = Grognement();
            clips["ricanement"] = Ricanement();
            clips["rugissement"] = Rugissement();
            clips["gelee"] = Gelee();
            clips["frottement"] = Frottement();
        }

        /// <summary>Des pieds qui glissent sur la terre : un grain sourd, sans métal.</summary>
        AudioClip Frottement()
        {
            float b1 = 0, b2 = 0, grain = 0;
            const float d = .9f;
            return Creer("Frottement", d, (i, t) =>
            {
                b1 += (Bruit() - b1) * .3f;
                b2 += (b1 - b2) * .1f;
                if (Bruit() > .993f) grain = 1f;
                grain *= .993f;
                float env = Mathf.Clamp01(t / .02f) * Mathf.Pow(1f - t / d, 1.4f);
                return (b1 - b2) * (1.3f + grain * 1.6f) * env;
            });
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

        /// <summary>Le hurlement du loup : une note qui monte, tient en vibrant, retombe.</summary>
        AudioClip Hurlement()
        {
            float phase = 0, b = 0;
            const float d = 2.2f;
            return Creer("Hurlement", d, (i, t) =>
            {
                float u = t / d;
                float f = u < .25f ? Mathf.Lerp(380f, 610f, u / .25f) : u < .75f ? 610f + 12f * Mathf.Sin(t * 30f) : Mathf.Lerp(610f, 420f, (u - .75f) / .25f);
                phase += f / Freq;
                float voix = Mathf.Sin(2f * Mathf.PI * phase) + .35f * Mathf.Sin(4f * Mathf.PI * phase) + .12f * Mathf.Sin(6f * Mathf.PI * phase);
                b += (Bruit() - b) * .3f;
                float env = Mathf.Clamp01(t / .25f) * Mathf.Clamp01((d - t) / .5f);
                return (voix * .45f + b * .06f) * env;
            });
        }

        /// <summary>Un grognement : un bruit grave qui roule.</summary>
        AudioClip Grognement()
        {
            float b1 = 0, b2 = 0, phase = 0;
            const float d = .55f;
            return Creer("Grognement", d, (i, t) =>
            {
                b1 += (Bruit() - b1) * .08f;
                b2 += (b1 - b2) * .08f;
                phase += 95f / Freq;
                float roule = .55f + .45f * Mathf.Sin(t * 2f * Mathf.PI * 28f);
                float env = Mathf.Clamp01(t / .05f) * Mathf.Clamp01((d - t) / .15f);
                return (b2 * 4f * roule + Mathf.Sin(2f * Mathf.PI * phase) * .25f * roule) * env;
            });
        }

        /// <summary>Le ricanement du gobelin : une rafale de petits cris aigus.</summary>
        AudioClip Ricanement()
        {
            float phase = 0;
            const float d = .7f;
            return Creer("Ricanement", d, (i, t) =>
            {
                int n = (int)(t / .1f);
                float u = t - n * .1f;
                float f = 520f + (n % 2) * 90f - n * 18f;
                phase += f / Freq;
                float scie = (phase % 1f) * 2f - 1f;
                float env = u < .07f ? Mathf.Sin(u / .07f * Mathf.PI) : 0f;
                return scie * .4f * env * Mathf.Clamp01((d - t) / .2f);
            });
        }

        /// <summary>Le rugissement du troll : une voix énorme et grave, du souffle.</summary>
        AudioClip Rugissement()
        {
            float phase = 0, b1 = 0, b2 = 0, filtre = 0;
            const float d = 1.6f;
            return Creer("Rugissement", d, (i, t) =>
            {
                float f = Mathf.Lerp(85f, 62f, t / d) * (1f + .04f * Mathf.Sin(t * 37f));
                phase += f / Freq;
                float scie = (phase % 1f) * 2f - 1f;
                filtre += (scie - filtre) * .1f;
                b1 += (Bruit() - b1) * .15f;
                b2 += (b1 - b2) * .15f;
                float env = Mathf.Clamp01(t / .12f) * Mathf.Clamp01((d - t) / .5f);
                return (filtre * 1.3f + b2 * 2f) * env;
            });
        }

        /// <summary>Une gelée : un « splotch » mouillé.</summary>
        AudioClip Gelee()
        {
            float phase = 0, b = 0;
            const float d = .3f;
            return Creer("Gelee", d, (i, t) =>
            {
                float f = Mathf.Lerp(420f, 110f, Mathf.Sqrt(t / d));
                phase += f / Freq;
                b += (Bruit() - b) * .35f;
                float env = Mathf.Exp(-t * 11f);
                return (Mathf.Sin(2f * Mathf.PI * phase) * .7f + b * .4f * Mathf.Exp(-t * 30f)) * env;
            });
        }
    }
}
