using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// Les ambiances du monde ouvert, selon le lieu et l'heure : vent (plus fort en altitude), la rivière la plus proche,
    /// le clapotis d'un lac ou les vagues de la mer, les oiseaux le jour (plus nombreux en forêt), les grillons et la chouette
    /// la nuit, la rumeur de la foule en ville, les cloches des églises qui sonnent les heures.
    /// Et les pas selon le sol : herbe, terre, pierre, bois, eau, neige.
    /// </summary>
    public partial class Sons
    {
        AudioSource sourceGrillons, sourceFoule, sourceEau, sourceCloche;
        float minuterieAmbiance, minuterieEau, minuterieChouette = 20f;
        float cibleVent = .2f, cibleGrillons, cibleFoule, cibleEau, cibleRiviere;
        float foret, altitude, nuit, ville;

        void ClipsAmbiances()
        {
            clips["pasPierre"] = PasPierre();
            clips["pasBois"] = PasBois();
            clips["pasEau"] = PasEau();
            clips["pasNeige"] = PasNeige();
            clips["grillons"] = Grillons();
            clips["foule"] = Foule();
            clips["vagues"] = Vagues();
            clips["chouette"] = Chouette();
            clips["cloche"] = Cloche();
            clips["tambours"] = Tambours();
            clips["pieces"] = Carillon(new[] { 1567.98f, 2093f, 1760f });
        }

        void DemarrerAmbiances()
        {
            sourceGrillons = Boucle("grillons", false);
            sourceFoule = Boucle("foule", false);
            var go = new GameObject("SonEau");
            go.transform.SetParent(transform, false);
            sourceEau = go.AddComponent<AudioSource>();
            sourceEau.clip = Choisir("vagues"); sourceEau.loop = true; sourceEau.spatialBlend = 1f; sourceEau.volume = 0f;
            sourceEau.minDistance = 8f; sourceEau.maxDistance = 90f; sourceEau.rolloffMode = AudioRolloffMode.Linear; sourceEau.dopplerLevel = 0f; sourceEau.Play();
            if (sourceRiviere != null) { sourceRiviere.maxDistance = 70f; sourceRiviere.minDistance = 6f; sourceRiviere.priority = 64; }
            sourceGrillons.priority = sourceFoule.priority = sourceEau.priority = sourceVent.priority = 64;
            // La cloche a sa propre source, prioritaire, qui porte loin
            var gc = new GameObject("SonCloche");
            gc.transform.SetParent(transform, false);
            sourceCloche = gc.AddComponent<AudioSource>();
            sourceCloche.playOnAwake = false; sourceCloche.spatialBlend = 1f; sourceCloche.dopplerLevel = 0f; sourceCloche.priority = 16;
            sourceCloche.rolloffMode = AudioRolloffMode.Linear; sourceCloche.minDistance = 40f; sourceCloche.maxDistance = 900f; sourceCloche.spread = 40f;
            Horloge.NouvelleHeure += SonnerLesCloches;
        }

        void OnDestroy() => Horloge.NouvelleHeure -= SonnerLesCloches;

        AudioSource Boucle(string id, bool spatial)
        {
            var s = Source(true, spatial, 0f);
            s.clip = Choisir(id);
            s.Play();
            return s;
        }

        void MettreAJourAmbiances(float dt)
        {
            if (auditeur == null) return;
            Vector3 p = auditeur.position;
            float va = Volume(CanalSon.Ambiances) * volumeAmbiance;

            minuterieAmbiance -= dt;
            if (minuterieAmbiance <= 0f)
            {
                minuterieAmbiance = .5f;
                float h = Horloge.Heure;
                nuit = h < 5f || h > 21f ? 1f : h < 6.5f ? 1f - (h - 5f) / 1.5f : h > 19.5f ? (h - 19.5f) / 1.5f : 0f;
                float sol = Monde.Hauteur(p.x, p.z);
                altitude = Geo.Actif ? Mathf.Clamp01((sol - 120f) / 380f) : 0f;
                foret = 0f;
                if (Geo.Actif)
                    for (int k = 0; k < 6; k++)
                    {
                        float a = k * 1.047f;
                        if (Geo.Sol(p.x + Mathf.Cos(a) * 14f, p.z + Mathf.Sin(a) * 14f) == TypeSol.HerbeSombre) foret += 1f / 6f;
                    }
                // La ville la plus proche : sa rumeur, selon sa taille et l'heure
                ville = 0f;
                if (Geo.Actif)
                {
                    var s = Geo.SitePlusProche(new Vector2(p.x, p.z));
                    if (s != null)
                    {
                        float d = (s.position - new Vector2(p.x, p.z)).magnitude;
                        float taille = s.type == TypeSite.Capitale ? 1f : s.type == TypeSite.Ville || s.type == TypeSite.Port ? .75f : s.type == TypeSite.Village ? .4f : .15f;
                        ville = taille * Mathf.Clamp01(1f - (d - s.rayon * .6f) / (s.rayon * .7f + 30f));
                    }
                }
                cibleVent = .14f + .45f * altitude + .08f * (1f - foret);
                cibleGrillons = nuit * (1f - ville) * (Horloge.Saison == 3 ? 0f : .35f);
                cibleFoule = ville * (1f - nuit * .85f) * .45f;
            }
            minuterieEau -= dt;
            if (minuterieEau <= 0f) { minuterieEau = 1.2f; PlacerEaux(p); }

            sourceVent.volume = Mathf.MoveTowards(sourceVent.volume, cibleVent * va, dt * .2f);
            sourceGrillons.volume = Mathf.MoveTowards(sourceGrillons.volume, cibleGrillons * va, dt * .15f);
            sourceFoule.volume = Mathf.MoveTowards(sourceFoule.volume, cibleFoule * va, dt * .2f);
            sourceEau.volume = Mathf.MoveTowards(sourceEau.volume, cibleEau * va, dt * .3f);
            if (sourceRiviere != null) sourceRiviere.volume = Mathf.MoveTowards(sourceRiviere.volume, cibleRiviere * va, dt * .3f);
            // Une boucle muette ne garde pas sa voix : elle se met en pause et reprend quand elle remonte
            Veiller(sourceGrillons); Veiller(sourceFoule); Veiller(sourceEau); Veiller(sourceRiviere);

            // Les oiseaux le jour (plus en forêt), la chouette la nuit en forêt
            minuterieOiseau -= dt;
            if (minuterieOiseau <= 0f)
            {
                minuterieOiseau = Mathf.Lerp(9f, 2.5f, foret) * (1f + (float)alea.NextDouble());
                if (nuit < .5f && altitude < .8f)
                    JouerA("oiseau", p + new Vector3(Rnd(-25, 25), Rnd(6, 14), Rnd(-25, 25)), .35f * volumeAmbiance * (1f - ville * .7f), Rnd(.85f, 1.25f));
            }
            minuterieChouette -= dt;
            if (minuterieChouette <= 0f)
            {
                minuterieChouette = Rnd(18f, 45f);
                if (nuit > .7f && foret > .3f) JouerA("chouette", p + new Vector3(Rnd(-40, 40), Rnd(8, 16), Rnd(-40, 40)), .4f * volumeAmbiance, Rnd(.9f, 1.05f), 90f);
            }
        }

        /// <summary>La rivière la plus proche et le lac ou la mer le plus proche : leurs sons s'y posent.</summary>
        void PlacerEaux(Vector3 p)
        {
            var ici = new Vector2(p.x, p.z);
            if (Geo.Actif && sourceRiviere != null)
            {
                Vector2 meilleur = ici; float md = float.MaxValue;
                foreach (var r in Geo.Rivieres)
                    for (int k = 0; k < r.points.Length; k += 2)
                    {
                        float d = (r.points[k] - ici).sqrMagnitude;
                        if (d < md) { md = d; meilleur = r.points[k]; }
                    }
                cibleRiviere = md < 90f * 90f ? .38f : 0f;
                if (md < 90f * 90f) sourceRiviere.transform.position = new Vector3(meilleur.x, Monde.Hauteur(meilleur.x, meilleur.y) + .5f, meilleur.y);
            }
            else cibleRiviere = .32f;

            // Lac ou mer : la première eau trouvée en tournant autour du joueur
            cibleEau = 0f;
            if (!Geo.Actif) return;
            for (float rayon = 15f; rayon <= 120f && cibleEau == 0f; rayon += 35f)
                for (int k = 0; k < 12; k++)
                {
                    float a = k * Mathf.PI / 6f;
                    float x = p.x + Mathf.Cos(a) * rayon, z = p.z + Mathf.Sin(a) * rayon;
                    float n = Geo.NiveauEau(x, z);
                    if (n < -9000f || n < Geo.Hauteur(x, z) + 1.5f) continue;   // pas une étendue d'eau profonde
                    sourceEau.transform.position = new Vector3(x, n + .3f, z);
                    cibleEau = n <= .5f ? .55f : .3f;    // la mer plus fort qu'un lac
                    break;
                }
        }

        static void Veiller(AudioSource s)
        {
            if (s == null) return;
            if (s.volume <= .001f) { if (s.isPlaying) s.Pause(); }
            else if (!s.isPlaying) s.UnPause();
        }

        // ---------- les cloches ----------

        void SonnerLesCloches(int heure)
        {
            if (!MondeRPG.Actif || auditeur == null || heure < 7 || heure > 21) return;
            Lieu proche = null; float md = 650f * 650f;
            foreach (var l in Lieu.Tous)
            {
                if (l.type != TypeLieu.Chapelle) continue;
                float d = (l.Position - auditeur.position).sqrMagnitude;
                if (d < md) { md = d; proche = l; }
            }
            if (proche != null) StartCoroutine(Coups(proche.Position + Vector3.up * 20f, heure % 12 == 0 ? 12 : heure % 12, proche.nom.StartsWith("Chapelle") ? 1.4f : 1f));
        }

        IEnumerator Coups(Vector3 ou, int n, float hauteur)
        {
            sourceCloche.transform.position = ou;
            sourceCloche.pitch = hauteur;
            var c = Choisir("cloche");
            for (int k = 0; k < n; k++)
            {
                sourceCloche.PlayOneShot(c, .9f * Volume(CanalSon.Ambiances));
                yield return new WaitForSeconds(2.6f);
            }
        }

        // ---------- les pas selon le sol ----------

        /// <summary>Un pas d'Aldric : le son dépend de ce qu'il y a sous ses pieds.</summary>
        public static void Pas(Vector3 pied, bool course)
        {
            if (instance == null) return;
            string id = SolSousLesPieds(pied);
            float v = (course ? .2f : .13f) * (id == "pasEau" ? 1.6f : id == "pasPierre" ? 1.15f : 1f);
            Jouer(id, v, Random.Range(.9f, 1.1f));
        }

        static string SolSousLesPieds(Vector3 pied)
        {
            if (Geo.Actif)
            {
                float eau = Geo.NiveauEau(pied.x, pied.z);
                if (eau > -9000f && pied.y < eau + .05f) return "pasEau";
            }
            else if (Monde.Hauteur(pied.x, pied.z) < -.15f && pied.y < .1f) return "pasEau";

            // Sur un pont, un plancher, un quai : ce n'est pas le terrain
            if (Physics.Raycast(pied + Vector3.up * .4f, Vector3.down, out var hit, 1f, ~0, QueryTriggerInteraction.Ignore) && !(hit.collider is TerrainCollider))
            {
                for (var t = hit.collider.transform; t != null; t = t.parent)
                    if (t.name.StartsWith("Maison") || t.name.StartsWith("Ponton") || t.name == "Lit") return "pasBois";
                return "pasPierre";
            }
            if (!Geo.Actif) return Monde.SurTerre(pied.x, pied.z) ? "pasTerre" : "pasHerbe";
            switch (Geo.Sol(pied.x, pied.z))
            {
                case TypeSol.Pave: case TypeSol.Roche: return "pasPierre";
                case TypeSol.Neige: return "pasNeige";
                case TypeSol.Chemin: case TypeSol.Champ2: case TypeSol.Sable: case TypeSol.LitRiviere: return "pasTerre";
                default: return Geo.DistanceRoute(pied.x, pied.z) < .5f ? "pasTerre" : "pasHerbe";
            }
        }

        // ---------- synthèse ----------

        /// <summary>Pas sur la pierre : un claquement sec et bref.</summary>
        AudioClip PasPierre()
        {
            float b = 0;
            return Creer("PasPierre", .11f, (i, t) =>
            {
                b += (Bruit() - b) * .5f;
                float env = Mathf.Clamp01(t / .002f) * Mathf.Exp(-t * 55f);
                return (b * .9f + Mathf.Sin(2f * Mathf.PI * 1900f * t) * .15f + Mathf.Sin(2f * Mathf.PI * 260f * t) * .3f) * env;
            });
        }

        /// <summary>Pas sur le bois : un coup creux qui résonne un peu.</summary>
        AudioClip PasBois()
        {
            float b = 0;
            return Creer("PasBois", .2f, (i, t) =>
            {
                b += (Bruit() - b) * .2f;
                float env = Mathf.Clamp01(t / .003f) * Mathf.Exp(-t * 24f);
                return (Mathf.Sin(2f * Mathf.PI * 150f * t) * .6f + Mathf.Sin(2f * Mathf.PI * 410f * t) * .22f + b * .35f) * env;
            });
        }

        /// <summary>Pas dans l'eau : une gerbe qui éclabousse.</summary>
        AudioClip PasEau()
        {
            float b1 = 0, b2 = 0;
            return Creer("PasEau", .35f, (i, t) =>
            {
                b1 += (Bruit() - b1) * .45f;
                b2 += (b1 - b2) * .08f;
                float env = Mathf.Clamp01(t / .01f) * Mathf.Exp(-t * 9f);
                float glou = Mathf.Sin(2f * Mathf.PI * (500f - 600f * t) * t) * .15f * Mathf.Exp(-t * 15f);
                return ((b1 - b2) * 1.4f + glou) * env;
            });
        }

        /// <summary>Pas dans la neige : un crissement étouffé.</summary>
        AudioClip PasNeige()
        {
            float b = 0, grain = 0;
            return Creer("PasNeige", .26f, (i, t) =>
            {
                b += (Bruit() - b) * .3f;
                if (Bruit() > .97f) grain = 1f;
                grain *= .9f;
                float env = Mathf.Sin(Mathf.Clamp01(t / .26f) * Mathf.PI);
                return b * (.5f + grain) * env * .9f;
            });
        }

        /// <summary>Grillons : des trilles aiguës qui se répondent, en boucle.</summary>
        AudioClip Grillons()
        {
            const float d = 4f;
            var debuts = new List<(float t0, float f, float n)>();
            for (int k = 0; k < 26; k++) debuts.Add(((float)alea.NextDouble() * (d - .4f), 4200f + (float)alea.NextDouble() * 900f, .5f + (float)alea.NextDouble() * .5f));
            return Creer("Grillons", d, (i, t) =>
            {
                float s = 0f;
                foreach (var (t0, f, n) in debuts)
                {
                    float u = t - t0;
                    if (u < 0f || u > .35f) continue;
                    float trille = Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * 32f * u));
                    s += Mathf.Sin(2f * Mathf.PI * f * u) * trille * Mathf.Sin(u / .35f * Mathf.PI) * .18f * n;
                }
                return s;
            });
        }

        /// <summary>La rumeur d'une foule : un brouhaha de voix (bruit filtré dans les fréquences de la parole) qui ondule.</summary>
        AudioClip Foule()
        {
            const float d = 7f;
            float b1 = 0, b2 = 0, b3 = 0;
            var voix = new List<(float t0, float l, float f)>();
            for (int k = 0; k < 70; k++) voix.Add(((float)alea.NextDouble() * d, .15f + (float)alea.NextDouble() * .5f, 140f + (float)alea.NextDouble() * 160f));
            return Creer("Foule", d, (i, t) =>
            {
                float n = Bruit();
                b1 += (n - b1) * .12f; b2 += (b1 - b2) * .12f; b3 += (b2 - b3) * .03f;
                float brouhaha = (b2 - b3) * 2.2f * (.7f + .3f * Mathf.Sin(t * 2.3f) * Mathf.Sin(t * .7f + 1f));
                float v = 0f;
                foreach (var (t0, l, f) in voix)
                {
                    float u = t - t0;
                    if (u < 0f || u > l) continue;
                    float syll = Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * 5.5f * u));
                    v += (Mathf.Sin(2f * Mathf.PI * f * u) + .4f * Mathf.Sin(2f * Mathf.PI * f * 2.7f * u)) * syll * Mathf.Sin(u / l * Mathf.PI) * .05f;
                }
                float fondu = Mathf.Min(1f, Mathf.Min(t, d - t) / .4f);
                return (brouhaha + v) * fondu;
            });
        }

        /// <summary>Des vagues : un ressac lent qui monte et se retire.</summary>
        AudioClip Vagues()
        {
            const float d = 9f;
            float b1 = 0, b2 = 0;
            return Creer("Vagues", d, (i, t) =>
            {
                b1 += (Bruit() - b1) * .25f; b2 += (b1 - b2) * .02f;
                float u = t / d * 3f;                    // trois vagues par boucle
                float houle = Mathf.Pow(Mathf.Sin((u - Mathf.Floor(u)) * Mathf.PI), 2.5f);
                float fondu = Mathf.Min(1f, Mathf.Min(t, d - t) / .6f);
                return (b1 - b2 * .5f) * (.15f + .85f * houle) * 1.3f * fondu;
            });
        }

        /// <summary>Une chouette hulotte : un hou… puis un trémolo.</summary>
        AudioClip Chouette()
        {
            return Creer("Chouette", 1.7f, (i, t) =>
            {
                float s = 0f;
                if (t < .45f) s = Mathf.Sin(2f * Mathf.PI * (520f - 40f * t) * t) * Mathf.Sin(t / .45f * Mathf.PI);
                float u = t - .75f;
                if (u > 0f && u < .9f) s += Mathf.Sin(2f * Mathf.PI * (560f - 60f * u) * u) * Mathf.Sin(u / .9f * Mathf.PI) * (.6f + .4f * Mathf.Sin(2f * Mathf.PI * 9f * u));
                return s * .45f;
            });
        }

        /// <summary>
        /// Une cloche d'église en bronze : les partiels d'une vraie cloche (bourdon, fondamentale, tierce mineure, quinte,
        /// nominale, puis les aigus), chacun avec sa durée (le bourdon grave tient le plus longtemps), quelques-uns
        /// doublés et à peine désaccordés (le lent battement d'une cloche), et le choc du battant au début.
        /// </summary>
        AudioClip Cloche()
        {
            const float nominale = 330f, d = 9f;
            //                      bourdon fond.  tierce quinte nomin. dixième onzième douzième octave
            float[] rapport = { .5f, 1f, 1.2f, 1.5f, 2f, 2.5f, 2.67f, 3f, 4f, 5.33f };
            float[] ampli = { .55f, .5f, .45f, .22f, .6f, .22f, .16f, .14f, .1f, .06f };
            float[] duree = { 7.5f, 5f, 4.2f, 2.6f, 3.4f, 1.8f, 1.4f, 1.1f, .8f, .5f };
            float[] battement = { .35f, 0f, .22f, 0f, .4f, 0f, 0f, 0f, 0f, 0f };
            float b1 = 0f, b2 = 0f;
            return Creer("Cloche", d, (i, t) =>
            {
                float s = 0f;
                for (int k = 0; k < rapport.Length; k++)
                {
                    float f = nominale * .5f * rapport[k];
                    float env = Mathf.Exp(-t * 6.9f / duree[k]);
                    float onde = battement[k] > 0f
                        ? (Mathf.Sin(2f * Mathf.PI * (f - battement[k] * .5f) * t) + Mathf.Sin(2f * Mathf.PI * (f + battement[k] * .5f) * t)) * .5f
                        : Mathf.Sin(2f * Mathf.PI * f * t);
                    s += onde * ampli[k] * env;
                }
                // Le battant : un claquement métallique de quelques centièmes de seconde
                b1 += (Bruit() - b1) * .55f; b2 += (b1 - b2) * .25f;
                s += (b1 - b2) * 1.6f * Mathf.Exp(-t * 70f);
                float attaque = Mathf.Clamp01(t / .002f), fin = Mathf.Clamp01((d - t) / .5f);
                return s * .3f * attaque * fin;
            });
        }

        /// <summary>Des tambours de guerre en boucle (couche de combat quand il n'y a pas de musique de combat) : 4 mesures à 112 battements par minute.</summary>
        AudioClip Tambours()
        {
            const float bpm = 112f, temps = 60f / bpm;
            float d = temps * 16f;
            // Motif : grosse caisse (G), tom (T), coup sec (S) sur 16 temps en croches
            string motif = "G.T.G.S.GGT.G.SS" + "G.T.G.S.GGT.GTSS";
            float b = 0;
            return Creer("Tambours", d, (i, t) =>
            {
                float pas = temps / 2f;
                int k = (int)(t / pas) % motif.Length;
                float u = t - Mathf.Floor(t / pas) * pas;
                float s = 0f;
                b += (Bruit() - b) * .4f;
                switch (motif[k])
                {
                    case 'G': s = Mathf.Sin(2f * Mathf.PI * (70f - 90f * u) * u) * Mathf.Exp(-u * 14f); break;
                    case 'T': s = Mathf.Sin(2f * Mathf.PI * (150f - 60f * u) * u) * Mathf.Exp(-u * 18f) * .7f; break;
                    case 'S': s = b * Mathf.Exp(-u * 40f) * .6f; break;
                }
                return s * .9f;
            });
        }
    }
}
