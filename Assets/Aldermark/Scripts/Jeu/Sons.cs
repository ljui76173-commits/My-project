using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// Sons d'Aldermark, fabriqués en code (aucun fichier audio nécessaire) :
    /// vent, rivière, oiseaux, pas, Sceau, analyse, découverte, voix des dialogues.
    /// Joue aussi la musique déposée dans Assets/Aldermark/Audio (assignée par le menu Gameplay).
    /// </summary>
    public class Sons : MonoBehaviour
    {
        public AudioClip[] musiques;
        [Range(0, 1)] public float volumeMusique = .35f;
        [Range(0, 1)] public float volumeAmbiance = .5f;
        public Vector3 positionRiviere = new Vector3(5, 0, 0);

        static Sons instance;
        const int Freq = 44100;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        AudioSource sourceUI, sourceAnalyse, sourceMusique, sourceVoix;
        Transform auditeur;
        float minuterieOiseau = 3f;
        int indexMusique = -1;
        System.Random alea = new System.Random(7);

        void Awake()
        {
            instance = this;
            clips["vent"] = Vent();
            clips["riviere"] = Riviere();
            clips["oiseau"] = Oiseau();
            clips["pasHerbe"] = PasHerbe();
            clips["pasTerre"] = PasTerre();
            clips["coup"] = Coup();
            clips["sceauOn"] = Souffle(true);
            clips["sceauOff"] = Souffle(false);
            clips["analyse"] = Bourdon();
            clips["decouverte"] = Carillon(new[] { 523.25f, 659.25f, 783.99f, 1046.5f });
            clips["quete"] = Carillon(new[] { 587.33f, 880f });
            clips["voix"] = Bip();
            clips["clic"] = Pas(.012f, 4000f);

            sourceUI = Source(false, false, 1f);
            sourceVoix = Source(false, false, .5f);
            sourceAnalyse = Source(true, false, 0f);
            sourceAnalyse.clip = clips["analyse"];
            sourceMusique = Source(false, false, volumeMusique);

            var vent = Source(true, false, .22f * volumeAmbiance);
            vent.clip = clips["vent"]; vent.Play();

            var goR = new GameObject("SonRiviere");
            goR.transform.SetParent(transform, false);
            goR.transform.position = positionRiviere;
            var riv = goR.AddComponent<AudioSource>();
            riv.clip = clips["riviere"]; riv.loop = true; riv.spatialBlend = 1f;
            riv.minDistance = 5f; riv.maxDistance = 55f; riv.rolloffMode = AudioRolloffMode.Linear;
            riv.volume = .32f * volumeAmbiance; riv.Play();
        }

        AudioSource Source(bool boucle, bool spatial, float volume)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.loop = boucle; s.spatialBlend = spatial ? 1f : 0f; s.volume = volume; s.playOnAwake = false;
            return s;
        }

        void Update()
        {
            if (auditeur == null && Camera.main != null) auditeur = Camera.main.transform;

            // Oiseaux autour d'Aldric
            minuterieOiseau -= Time.deltaTime;
            if (minuterieOiseau <= 0 && auditeur != null)
            {
                minuterieOiseau = 3f + (float)alea.NextDouble() * 7f;
                Vector3 p = auditeur.position + new Vector3(Rnd(-25, 25), Rnd(6, 14), Rnd(-25, 25));
                JouerA("oiseau", p, .35f * volumeAmbiance, Rnd(.85f, 1.25f));
            }

            // Musique : enchaîne les morceaux avec un silence entre deux
            if (musiques != null && musiques.Length > 0 && !sourceMusique.isPlaying)
            {
                indexMusique = (indexMusique + 1) % musiques.Length;
                sourceMusique.clip = musiques[indexMusique];
                sourceMusique.volume = volumeMusique;
                sourceMusique.PlayDelayed(indexMusique == 0 && Time.time < 5f ? 2f : 20f);
            }
        }

        float Rnd(float a, float b) => a + (float)alea.NextDouble() * (b - a);

        // ---------- API ----------

        public static void Jouer(string id, float volume = 1f, float hauteur = 1f)
        {
            if (instance == null || !instance.clips.TryGetValue(id, out var c)) return;
            instance.sourceUI.pitch = hauteur;
            instance.sourceUI.PlayOneShot(c, volume);
        }

        public static void JouerA(string id, Vector3 position, float volume = 1f, float hauteur = 1f)
        {
            if (instance == null || !instance.clips.TryGetValue(id, out var c)) return;
            var go = new GameObject("Son_" + id);
            go.transform.position = position;
            var s = go.AddComponent<AudioSource>();
            s.clip = c; s.volume = volume; s.pitch = hauteur; s.spatialBlend = 1f;
            s.minDistance = 4f; s.maxDistance = 60f; s.rolloffMode = AudioRolloffMode.Linear;
            s.Play();
            Destroy(go, c.length / Mathf.Max(.1f, hauteur) + .1f);
        }

        /// <summary>Bourdon d'analyse : progression de 0 à 1, ou négatif pour couper.</summary>
        public static void Analyse(float progression)
        {
            if (instance == null) return;
            var s = instance.sourceAnalyse;
            if (progression < 0) { if (s.isPlaying) s.Stop(); return; }
            if (!s.isPlaying) s.Play();
            s.volume = .18f + .22f * progression;
            s.pitch = .8f + .7f * progression;
        }

        /// <summary>Petite voix de dialogue : une syllabe à la hauteur du personnage.</summary>
        public static void Voix(float hauteur)
        {
            if (instance == null) return;
            instance.sourceVoix.pitch = hauteur * (.92f + .16f * (float)instance.alea.NextDouble());
            instance.sourceVoix.PlayOneShot(instance.clips["voix"], .55f);
        }

        // ---------- synthèse ----------

        AudioClip Creer(string nom, float duree, System.Func<int, float, float> echantillon)
        {
            int n = Mathf.CeilToInt(duree * Freq);
            var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(echantillon(i, (float)i / Freq), -1f, 1f);
            var c = AudioClip.Create(nom, n, 1, Freq, false);
            c.SetData(d, 0);
            return c;
        }

        float Bruit() => (float)alea.NextDouble() * 2f - 1f;

        AudioClip Vent()
        {
            float b = 0, lent = 0;
            const float duree = 6f;
            return Creer("Vent", duree, (i, t) =>
            {
                b += (Bruit() - b) * .02f;            // bruit filtré grave
                lent += (Bruit() - lent) * .0004f;
                float enveloppe = .6f + .4f * Mathf.Sin(t / duree * Mathf.PI * 2f) + lent * 2f;
                float fondu = Mathf.Min(1f, Mathf.Min(t, duree - t) / .5f);
                return b * 3.2f * enveloppe * fondu;
            });
        }

        /// <summary>Murmure d'eau : bruit grave et doux, qui ondule, avec quelques bulles.</summary>
        AudioClip Riviere()
        {
            float b1 = 0, b2 = 0, b3 = 0;
            const float duree = 6f;
            var bulles = new float[18];
            for (int k = 0; k < bulles.Length; k++) bulles[k] = (float)alea.NextDouble() * duree;
            return Creer("Riviere", duree, (i, t) =>
            {
                float n = Bruit();
                b1 += (n - b1) * .06f;          // trois passes basses : plus de sifflement
                b2 += (b1 - b2) * .06f;
                b3 += (b2 - b3) * .1f;
                float houle = .65f + .2f * Mathf.Sin(t * 2f * Mathf.PI / duree * 3f) + .15f * Mathf.Sin(t * 2f * Mathf.PI / duree * 7f + 1f);
                float bulle = 0;
                foreach (float t0 in bulles)
                {
                    float u = t - t0;
                    if (u < 0 || u > .06f) continue;
                    bulle += Mathf.Sin(2f * Mathf.PI * (300f + 900f * u / .06f) * u) * Mathf.Sin(u / .06f * Mathf.PI) * .12f;
                }
                float fondu = Mathf.Min(1f, Mathf.Min(t, duree - t) / .5f);
                return (b3 * 12f * houle + bulle) * fondu;
            });
        }

        AudioClip Oiseau()
        {
            return Creer("Oiseau", .55f, (i, t) =>
            {
                float s = 0;
                for (int k = 0; k < 3; k++)
                {
                    float t0 = k * .17f, u = t - t0;
                    if (u < 0 || u > .12f) continue;
                    float f = 2600f + 1400f * Mathf.Sin(u / .12f * Mathf.PI) + k * 300f;
                    s += Mathf.Sin(2f * Mathf.PI * f * u) * Mathf.Sin(u / .12f * Mathf.PI) * .5f;
                }
                return s;
            });
        }

        /// <summary>Clic d'interface, très court.</summary>
        AudioClip Pas(float duree, float coupure)
        {
            float b = 0, k = Mathf.Clamp01(coupure / Freq * 6.28f);
            return Creer("Clic", duree, (i, t) =>
            {
                b += (Bruit() - b) * k;
                return b * 2.5f * Mathf.Exp(-t / duree * 4f);
            });
        }

        /// <summary>Pas dans l'herbe : un froissement doux, sans attaque sèche.</summary>
        AudioClip PasHerbe()
        {
            float b1 = 0, b2 = 0;
            const float d = .2f;
            return Creer("PasHerbe", d, (i, t) =>
            {
                b1 += (Bruit() - b1) * .14f;
                b2 += (b1 - b2) * .14f;
                float env = Mathf.Sin(Mathf.Clamp01(t / .03f) * Mathf.PI * .5f) * Mathf.Exp(-t / d * 3.5f);
                return b2 * 1.6f * env;
            });
        }

        /// <summary>Pas sur la terre : un son sourd et grave.</summary>
        AudioClip PasTerre()
        {
            float b1 = 0, b2 = 0;
            const float d = .16f;
            return Creer("PasTerre", d, (i, t) =>
            {
                b1 += (Bruit() - b1) * .08f;
                b2 += (b1 - b2) * .08f;
                float env = Mathf.Clamp01(t / .008f) * Mathf.Exp(-t / d * 5f);
                float sourd = Mathf.Sin(2f * Mathf.PI * (95f - 30f * t / d) * t) * .45f;
                return (b2 * 1.4f + sourd) * env;
            });
        }

        /// <summary>Coup d'outil (hache, pioche, marteau) : un « toc » boisé.</summary>
        AudioClip Coup()
        {
            float b = 0;
            return Creer("Coup", .18f, (i, t) =>
            {
                b += (Bruit() - b) * .3f;
                float env = Mathf.Exp(-t * 32f);
                return (Mathf.Sin(2f * Mathf.PI * 420f * t) * .6f + Mathf.Sin(2f * Mathf.PI * 760f * t) * .25f + b * .5f) * env;
            });
        }

        AudioClip Souffle(bool montant)
        {
            float b = 0;
            return Creer(montant ? "SceauOn" : "SceauOff", .7f, (i, t) =>
            {
                float u = montant ? t / .7f : 1f - t / .7f;
                b += (Bruit() - b) * (.02f + .2f * u);
                float grave = Mathf.Sin(2f * Mathf.PI * (90f + 140f * u) * t) * .35f;
                return (b * 1.8f + grave) * Mathf.Sin(t / .7f * Mathf.PI);
            });
        }

        AudioClip Bourdon()
        {
            return Creer("Analyse", 2f, (i, t) =>
            {
                float s = Mathf.Sin(2f * Mathf.PI * 220f * t) * .5f + Mathf.Sin(2f * Mathf.PI * 330f * t) * .3f + Mathf.Sin(2f * Mathf.PI * 440.5f * t) * .15f;
                return s * (.7f + .3f * Mathf.Sin(2f * Mathf.PI * 6f * t)) * .5f;
            });
        }

        AudioClip Carillon(float[] notes)
        {
            float duree = notes.Length * .11f + 1.2f;
            return Creer("Carillon", duree, (i, t) =>
            {
                float s = 0;
                for (int k = 0; k < notes.Length; k++)
                {
                    float u = t - k * .11f;
                    if (u < 0) continue;
                    float env = Mathf.Exp(-u * 3.2f);
                    s += (Mathf.Sin(2f * Mathf.PI * notes[k] * u) + .3f * Mathf.Sin(2f * Mathf.PI * notes[k] * 2.01f * u)) * env * .35f;
                }
                return s;
            });
        }

        AudioClip Bip()
        {
            return Creer("Voix", .07f, (i, t) =>
            {
                float env = Mathf.Sin(t / .07f * Mathf.PI);
                float f = 320f;
                return (Mathf.Sin(2f * Mathf.PI * f * t) * .7f + Mathf.Sin(2f * Mathf.PI * f * 2f * t) * .2f) * env * .6f;
            });
        }
    }
}
