using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// La musique adaptative du monde ouvert : elle suit ce que tu vis, sans jamais couper.
    /// Situations : Exploration (de jour, un thème par région si les noms de fichiers le disent : foret, montagne, cote, plaine),
    /// Nuit, Village, Ville, Capitale. Les passages se font en fondu (3 s). Le combat ajoute une couche par-dessus
    /// (Combat/, ou des tambours fabriqués en code) qui monte avec le danger, la musique de fond baissant d'autant.
    /// Les fanfares (Fanfares/ : niveau, decouverte…) passent devant le reste quelques secondes.
    /// </summary>
    public partial class Sons
    {
        AudioSource musiqueA, musiqueB, couche, fanfare;
        bool aCourante;                  // vrai : musiqueA est la piste en cours
        string situation = "", regionCourante = "";
        AudioClip pisteCourante, dernierePiste;
        float volumeCourante, minuterieSituation, silence, danger, baisseFanfare;
        bool coucheFichier;

        static readonly string[] Regions = { "foret", "montagne", "cote", "plaine" };

        void DemarrerMusique()
        {
            musiqueA = Source(false, false, 0f);
            musiqueB = Source(false, false, 0f);
            couche = Source(true, false, 0f);
            fanfare = Source(false, false, 0f);
            var cat = CatalogueAudio.Instance;
            var combat = cat != null ? cat.Musiques("Combat") : new List<CatalogueAudio.Piste>();
            coucheFichier = combat.Count > 0;
            couche.clip = coucheFichier ? combat[alea.Next(combat.Count)].clip : clips["tambours"];
            couche.Play();
            silence = 2f;
        }

        void MettreAJourMusique(float dt)
        {
            float vm = Volume(CanalSon.Musique) * volumeMusique * 2f;

            // Le danger : des monstres qui s'en prennent à Aldric, tout près
            float cibleDanger = 0f;
            var heros = CombatHeros.Instance;
            if (heros != null)
                foreach (var m in Monstre.Actifs)
                {
                    if (m == null || m.Cible != (ICible)heros) continue;
                    if (m.Etat == EtatMonstre.Repos || m.Etat == EtatMonstre.Retour || m.Etat == EtatMonstre.Mort || m.Etat == EtatMonstre.Fuite) continue;
                    if ((m.transform.position - heros.transform.position).sqrMagnitude > 40f * 40f) continue;
                    cibleDanger += m is Troll ? 1f : .45f;
                }
            danger = Mathf.MoveTowards(danger, Mathf.Clamp01(cibleDanger), dt * (cibleDanger > danger ? .8f : .15f));
            baisseFanfare = Mathf.MoveTowards(baisseFanfare, fanfare.isPlaying ? 1f : 0f, dt * 2f);
            float fond = (1f - .65f * danger) * (1f - .8f * baisseFanfare);
            couche.volume = Mathf.MoveTowards(couche.volume, danger * vm * (coucheFichier ? 1f : .55f) * (1f - .8f * baisseFanfare), dt * .8f);
            fanfare.volume = Volume(CanalSon.Musique) * .9f;

            // La situation, toutes les secondes
            minuterieSituation -= dt;
            if (minuterieSituation <= 0f)
            {
                minuterieSituation = 1f;
                string s = Situation(out string region);
                if (s != situation || (s == "Exploration" && region != regionCourante))
                {
                    situation = s; regionCourante = region;
                    var pistes = Pistes(situation, region);
                    // La piste en cours convient encore : on la garde
                    if (pisteCourante == null || !pistes.Exists(p => p.clip == pisteCourante)) Enchainer(pistes, 3f);
                }
            }

            // Fondus : la piste en cours monte, l'autre s'éteint
            var cour = aCourante ? musiqueA : musiqueB; var autre = aCourante ? musiqueB : musiqueA;
            cour.volume = Mathf.MoveTowards(cour.volume, volumeCourante * vm * fond, dt / 3f * vm);
            autre.volume = Mathf.MoveTowards(autre.volume, 0f, dt / 3f * vm);
            if (autre.volume <= 0f && autre.isPlaying) autre.Stop();

            // Fin du morceau : un court silence, puis le suivant de la même situation
            if (pisteCourante != null && !cour.isPlaying)
            {
                pisteCourante = null;
                silence = Rnd(5f, 14f);
            }
            if (pisteCourante == null && situation != "")
            {
                silence -= dt;
                if (silence <= 0f) Enchainer(Pistes(situation, regionCourante), 0f);
            }
        }

        string Situation(out string region)
        {
            region = "";
            if (auditeur == null) return "Exploration";
            var p = auditeur.position;
            if (Horloge.EstNuit) return "Nuit";
            if (Geo.Actif)
            {
                var s = Geo.SitePlusProche(new Vector2(p.x, p.z));
                if (s != null && (s.position - new Vector2(p.x, p.z)).magnitude < s.rayon + 25f)
                    return s.type == TypeSite.Capitale ? "Capitale" : s.type == TypeSite.Ville || s.type == TypeSite.Port ? "Ville" : "Village";
                float sol = Monde.Hauteur(p.x, p.z);
                region = sol > 280f ? "montagne" : foret > .5f ? "foret" : Geo.NiveauEau(p.x, p.z + 60f) > -9000f && sol < 12f ? "cote" : "plaine";
            }
            return "Exploration";
        }

        /// <summary>Les pistes d'une situation (repli : la plus proche qui existe, puis l'exploration).</summary>
        List<CatalogueAudio.Piste> Pistes(string s, string region)
        {
            var cat = CatalogueAudio.Instance;
            var l = new List<CatalogueAudio.Piste>();
            if (cat == null)
            {
                if (musiques != null) foreach (var c in musiques) if (c != null) l.Add(new CatalogueAudio.Piste { situation = "Exploration", nom = c.name, clip = c });
                return l;
            }
            l = cat.Musiques(s);
            if (l.Count == 0 && s == "Capitale") l = cat.Musiques("Ville");
            if (l.Count == 0 && s == "Ville") l = cat.Musiques("Village");
            if (l.Count == 0) l = cat.Musiques("Exploration");
            if (region != "")
            {
                var r = l.FindAll(p => p.nom.ToLowerInvariant().Contains(region));
                if (r.Count > 0) return r;
                // Les pistes marquées pour une autre région ne jouent pas ailleurs
                var neutres = l.FindAll(p => { string n = p.nom.ToLowerInvariant(); foreach (var g in Regions) if (n.Contains(g)) return false; return true; });
                if (neutres.Count > 0) return neutres;
            }
            return l;
        }

        void Enchainer(List<CatalogueAudio.Piste> pistes, float fondu)
        {
            if (pistes.Count == 0) { pisteCourante = null; return; }
            var choix = pistes[alea.Next(pistes.Count)];
            if (pistes.Count > 1 && choix.clip == dernierePiste) choix = pistes[(pistes.IndexOf(choix) + 1) % pistes.Count];
            aCourante = !aCourante;
            var s = aCourante ? musiqueA : musiqueB;
            s.Stop();
            s.clip = choix.clip;
            s.volume = fondu > 0f ? 0f : .01f;
            s.Play();
            pisteCourante = dernierePiste = choix.clip;
            volumeCourante = 1f;
        }

        /// <summary>
        /// Une fanfare courte (« niveau », « decouverte », « quete »…) : un fichier de Musique/Fanfares dont le nom
        /// contient ce mot, la musique baissant le temps qu'elle passe ; à défaut, le carillon fabriqué en code.
        /// </summary>
        public static void Fanfare(string quoi)
        {
            if (instance == null) return;
            var cat = CatalogueAudio.Instance;
            if (MondeRPG.Actif && cat != null)
            {
                var l = cat.Musiques("Fanfares").FindAll(p => p.nom.ToLowerInvariant().Contains(quoi));
                if (l.Count > 0)
                {
                    instance.fanfare.Stop();
                    instance.fanfare.clip = l[instance.alea.Next(l.Count)].clip;
                    instance.fanfare.volume = Volume(CanalSon.Musique) * .9f;
                    instance.fanfare.Play();
                    return;
                }
            }
            if (quoi == "niveau") { Jouer("decouverte", .9f, .9f); Jouer("quete", .7f, 1.2f); }
            else if (quoi == "quete") Jouer("quete", .8f);
            else Jouer("decouverte", .45f, 1.15f);
        }
    }
}
