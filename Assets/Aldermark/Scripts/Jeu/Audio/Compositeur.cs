using System;
using System.Collections.Generic;

namespace Aldermark
{
    /// <summary>
    /// Une musique de remplacement, composée par le code en attendant les morceaux de Kyllian :
    /// une harpe (cordes pincées, synthèse Karplus-Strong), une flûte, un bourdon à la quinte (comme une vielle),
    /// un tambour sur cadre pour les villages et les villes, et une réverbération pour l'espace.
    /// Chaque situation a son mode, son tempo, sa tonalité et ses instruments ; la grille d'accords tourne
    /// sur 8 mesures, jouée trois fois avec des motifs tirés au sort à chaque passage.
    /// Du calcul pur (aucune API de Unity) : il tourne sur un autre cœur au lancement.
    /// </summary>
    public static class Compositeur
    {
        public const int Frequence = 32000;

        class Style
        {
            public double racine;          // fréquence de la tonique (Hz)
            public int[] mode;             // demi-tons de la gamme
            public double bpm;
            public int[] grille;           // degrés des accords, 8 mesures
            public double densiteHarpe, densiteFlute, tambour, cuivres, bourdon;
        }

        static readonly int[] Dorien = { 0, 2, 3, 5, 7, 9, 10 }, Eolien = { 0, 2, 3, 5, 7, 8, 10 }, Mixolydien = { 0, 2, 4, 5, 7, 9, 10 }, Ionien = { 0, 2, 4, 5, 7, 9, 11 };

        static Style StylePour(string situation)
        {
            switch (situation)
            {
                case "Nuit": return new Style { racine = 110.0, mode = Eolien, bpm = 54, grille = new[] { 0, 5, 3, 4, 0, 5, 6, 0 }, densiteHarpe = .45, densiteFlute = .25, bourdon = .8 };
                case "Village": return new Style { racine = 196.0, mode = Mixolydien, bpm = 96, grille = new[] { 0, 6, 3, 0, 0, 3, 4, 0 }, densiteHarpe = .85, densiteFlute = .75, tambour = .7, bourdon = .5 };
                case "Ville": return new Style { racine = 130.81, mode = Ionien, bpm = 88, grille = new[] { 0, 4, 5, 3, 0, 3, 4, 0 }, densiteHarpe = .9, densiteFlute = .7, tambour = .55, cuivres = .4, bourdon = .4 };
                case "Capitale": return new Style { racine = 174.61, mode = Ionien, bpm = 82, grille = new[] { 0, 3, 4, 0, 5, 3, 4, 0 }, densiteHarpe = .9, densiteFlute = .65, tambour = .6, cuivres = .8, bourdon = .5 };
                default: return new Style { racine = 146.83, mode = Dorien, bpm = 76, grille = new[] { 0, 6, 3, 0, 0, 6, 4, 0 }, densiteHarpe = .7, densiteFlute = .5, bourdon = .7 };
            }
        }

        /// <summary>Compose un morceau (échantillons mono à <see cref="Frequence"/> Hz) pour une situation.</summary>
        public static float[] Composer(string situation, int graine)
        {
            var st = StylePour(situation);
            var r = new Random(graine);
            double noire = 60.0 / st.bpm, mesure = noire * 4;
            const int passages = 3;
            double duree = mesure * 8 * passages + 4.0;
            int n = (int)(duree * Frequence);
            var sortie = new float[n];

            double Note(int degre, int octave)
            {
                int m = st.mode.Length;
                int oct = octave + (int)Math.Floor((double)degre / m);
                int d = ((degre % m) + m) % m;
                return st.racine * Math.Pow(2, oct + st.mode[d] / 12.0);
            }

            // Le bourdon : tonique et quinte, une octave en dessous, qui respire
            if (st.bourdon > 0)
            {
                double f0 = st.racine / 2, f1 = f0 * 1.5;
                for (int i = 0; i < n; i++)
                {
                    double t = (double)i / Frequence;
                    double env = Math.Min(1.0, t / 3.0) * Math.Min(1.0, (duree - t) / 3.0) * (.75 + .25 * Math.Sin(t * .7));
                    double s = Math.Sin(2 * Math.PI * f0 * t) + .55 * Math.Sin(2 * Math.PI * f1 * t) + .18 * Math.Sin(2 * Math.PI * f0 * 3 * t);
                    sortie[i] += (float)(s * .045 * st.bourdon * env);
                }
            }

            int precedent = 7;   // la note de flûte précédente (degré), pour des mélodies par degrés conjoints
            for (int p = 0; p < passages; p++)
                for (int b = 0; b < 8; b++)
                {
                    double t0 = 2.0 + (p * 8 + b) * mesure;
                    int accord = st.grille[b];
                    int[] tons = { accord, accord + 2, accord + 4 };

                    // Harpe : arpège en croches, des notes de l'accord, avec des silences
                    for (int k = 0; k < 8; k++)
                    {
                        if (r.NextDouble() > st.densiteHarpe * (k % 2 == 0 ? 1.0 : .65)) continue;
                        int deg = tons[r.Next(3)] + (r.NextDouble() < .3 ? st.mode.Length : 0);
                        Pincer(sortie, t0 + k * noire / 2, Note(deg, 0), .22 * (k == 0 ? 1.2 : 1.0), r);
                    }
                    // La basse de la harpe sur le premier temps
                    Pincer(sortie, t0, Note(accord, -1), .26, r);

                    // Flûte : une phrase sur la mesure, une fois sur deux selon le style
                    if (r.NextDouble() < st.densiteFlute)
                    {
                        double t = t0;
                        while (t < t0 + mesure - noire * .5)
                        {
                            double l = noire * (r.NextDouble() < .5 ? 1 : r.NextDouble() < .5 ? .5 : 2);
                            int pas = r.Next(-2, 3);
                            int deg = precedent + pas;
                            if (r.NextDouble() < .35) deg = tons[r.Next(3)] + st.mode.Length;   // revenir à l'accord
                            deg = Math.Max(st.mode.Length - 2, Math.Min(st.mode.Length * 2 + 2, deg));
                            precedent = deg;
                            if (r.NextDouble() < .85) Flute(sortie, t, Math.Min(l, t0 + mesure - t), Note(deg, 1), .11);
                            t += l;
                        }
                    }

                    // Cuivres (villes) : l'accord tenu, doux, sur toute la mesure
                    if (st.cuivres > 0 && b % 2 == 0)
                        foreach (int d in tons) Cuivre(sortie, t0, mesure * 2 - .1, Note(d, 0), .035 * st.cuivres);

                    // Tambour sur cadre : temps 1 et 3, et quelques croches
                    if (st.tambour > 0)
                        for (int k = 0; k < 8; k++)
                        {
                            bool fort = k == 0 || k == 4;
                            if (fort || r.NextDouble() < .18 * st.tambour) Tambour(sortie, t0 + k * noire / 2, (fort ? .3 : .14) * st.tambour, fort);
                        }
                }

            Reverb(sortie);
            // Fondus, normalisation douce
            double crete = 1e-6;
            foreach (var s in sortie) crete = Math.Max(crete, Math.Abs(s));
            double g = .8 / crete;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / Frequence;
                double env = Math.Min(1.0, t / 1.5) * Math.Min(1.0, (duree - t) / 3.0);
                sortie[i] = (float)Math.Tanh(sortie[i] * g * env * 1.1);
            }
            return sortie;
        }

        // ---------- instruments ----------

        /// <summary>Une corde pincée (Karplus-Strong) : un éclat de bruit qui tourne dans une boucle et s'adoucit.</summary>
        static void Pincer(float[] o, double t0, double f, double vol, Random r)
        {
            int debut = (int)(t0 * Frequence);
            // La boucle doit durer exactement une période : la partie entière dans la corde,
            // le reste (fraction d'échantillon) dans un filtre passe-tout, sinon les notes aiguës sonnent faux
            double D = Frequence / f + .5;   // la moyenne de deux échantillons voisins raccourcit la boucle d'un demi
            int periode = Math.Max(2, (int)Math.Floor(D));
            double frac = D - periode, C = (1 - frac) / (1 + frac), x1 = 0, y1 = 0;
            var corde = new double[periode];
            double lisse = 0;
            for (int k = 0; k < periode; k++) { lisse += (r.NextDouble() * 2 - 1 - lisse) * .6; corde[k] = lisse; }
            int duree = (int)(Frequence * 2.6);
            double amorti = .996 - Math.Min(.006, f / 200000.0);
            for (int i = 0, j = 0; i < duree && debut + i < o.Length; i++)
            {
                int k2 = (j + 1) % periode;
                double v = corde[j];
                double moyenne = (corde[j] + corde[k2]) * .5 * amorti;
                double ap = C * moyenne + x1 - C * y1;
                x1 = moyenne; y1 = ap;
                corde[j] = ap;
                j = k2;
                double env = Math.Min(1.0, i / 40.0);
                o[debut + i] += (float)(v * vol * env);
            }
        }

        /// <summary>La flûte : une fondamentale, un peu d'harmonique, du souffle, un vibrato qui arrive après l'attaque.</summary>
        static void Flute(float[] o, double t0, double l, double f, double vol)
        {
            int debut = (int)(t0 * Frequence), duree = (int)((l + .12) * Frequence);
            double phase = 0, souffle = 0;
            var r = new Random(debut);
            for (int i = 0; i < duree && debut + i < o.Length; i++)
            {
                double t = (double)i / Frequence;
                double vib = 1 + .006 * Math.Sin(2 * Math.PI * 5.2 * t) * Math.Min(1.0, Math.Max(0.0, (t - .18) / .2));
                phase += 2 * Math.PI * f * vib / Frequence;
                souffle += (r.NextDouble() * 2 - 1 - souffle) * .2;
                double env = Math.Min(1.0, t / .07) * Math.Min(1.0, Math.Max(0.0, (l + .12 - t) / .14));
                double s = Math.Sin(phase) + .18 * Math.Sin(2 * phase) + .06 * Math.Sin(3 * phase) + souffle * .12;
                o[debut + i] += (float)(s * vol * env);
            }
        }

        /// <summary>Un cuivre doux : une dent de scie adoucie (quelques harmoniques), qui enfle et retombe.</summary>
        static void Cuivre(float[] o, double t0, double l, double f, double vol)
        {
            int debut = (int)(t0 * Frequence), duree = (int)(l * Frequence);
            for (int i = 0; i < duree && debut + i < o.Length; i++)
            {
                double t = (double)i / Frequence;
                double s = 0;
                for (int h = 1; h <= 5; h++) s += Math.Sin(2 * Math.PI * f * h * t) / (h * h * .8);
                double env = Math.Sin(Math.PI * Math.Min(1.0, t / l)) * Math.Min(1.0, t / .3);
                o[debut + i] += (float)(s * vol * env);
            }
        }

        /// <summary>Tambour sur cadre : un coup grave qui tombe, et le claquement de la peau.</summary>
        static void Tambour(float[] o, double t0, double vol, bool grave)
        {
            int debut = (int)(t0 * Frequence), duree = (int)(.35 * Frequence);
            var r = new Random(debut);
            double phase = 0, b = 0;
            for (int i = 0; i < duree && debut + i < o.Length; i++)
            {
                double t = (double)i / Frequence;
                double f = (grave ? 95 : 160) * (1 + 1.2 * Math.Exp(-t * 30));
                phase += 2 * Math.PI * f / Frequence;
                b += (r.NextDouble() * 2 - 1 - b) * .5;
                double s = Math.Sin(phase) * Math.Exp(-t * (grave ? 11 : 18)) + b * Math.Exp(-t * 60) * .5;
                o[debut + i] += (float)(s * vol);
            }
        }

        /// <summary>Une réverbération simple (quatre peignes en parallèle, deux passe-tout en série) mêlée au son.</summary>
        static void Reverb(float[] o)
        {
            int[] peignes = { 1557, 1617, 1491, 1422 };
            int[] passeTout = { 225, 556 };
            var mouille = new float[o.Length];
            foreach (int d in peignes)
            {
                int l = d * Frequence / 44100;
                var buf = new double[l];
                double filtre = 0;
                for (int i = 0, j = 0; i < o.Length; i++, j = (j + 1) % l)
                {
                    double y = buf[j];
                    filtre = y * .7 + filtre * .3;
                    buf[j] = o[i] + filtre * .78;
                    mouille[i] += (float)(y * .25);
                }
            }
            foreach (int d in passeTout)
            {
                int l = d * Frequence / 44100;
                var buf = new double[l];
                for (int i = 0, j = 0; i < o.Length; i++, j = (j + 1) % l)
                {
                    double b = buf[j];
                    double x = mouille[i];
                    buf[j] = x + b * .5;
                    mouille[i] = (float)(b - x);
                }
            }
            for (int i = 0; i < o.Length; i++) o[i] = o[i] * .8f + mouille[i] * .35f;
        }
    }
}
