using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// Le catalogue des musiques et des bruitages déposés dans Assets/Aldermark/Audio. Il est tenu à jour tout seul
    /// par l'éditeur (dès qu'un fichier arrive) et rangé dans Audio/Resources, où le jeu le trouve sans réglage.
    /// <list type="bullet">
    /// <item>Musique/&lt;Situation&gt;/… : Exploration, Nuit, Village, Ville, Capitale, Combat, Taverne, Boss, Fanfares.</item>
    /// <item>Sons/… : chaque fichier est rangé sous un identifiant d'après son nom (footstep_grass → pasHerbe…).</item>
    /// </list>
    /// </summary>
    public class CatalogueAudio : ScriptableObject
    {
        [Serializable]
        public class Piste
        {
            public string situation, nom;
            public AudioClip clip;
        }

        [Serializable]
        public class Variantes
        {
            public string id;
            public List<AudioClip> clips = new List<AudioClip>();
        }

        public List<Piste> musiques = new List<Piste>();
        public List<Variantes> sons = new List<Variantes>();

        static CatalogueAudio charge;
        static bool cherche;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reinitialiser() { charge = null; cherche = false; }

        /// <summary>Le catalogue du projet (null s'il n'y a encore aucun fichier audio).</summary>
        public static CatalogueAudio Instance
        {
            get
            {
                if (!cherche) { cherche = true; charge = Resources.Load<CatalogueAudio>("CatalogueAudio"); }
                return charge;
            }
        }

        public List<Piste> Musiques(string situation)
        {
            var l = new List<Piste>();
            foreach (var p in musiques) if (p.clip != null && string.Equals(p.situation, situation, StringComparison.OrdinalIgnoreCase)) l.Add(p);
            return l;
        }

        // ---------- les noms de fichiers → identifiants de sons ----------

        /// <summary>
        /// Pour chaque identifiant de son du jeu, des morceaux de noms de fichiers qui lui correspondent
        /// (packs Kenney « RPG Audio », « Impact Sounds », « Interface Sounds », ou noms en français).
        /// Le premier identifiant qui correspond l'emporte.
        /// </summary>
        public static readonly (string id, string[] motifs)[] Regles =
        {
            ("pasHerbe", new[] { "footstep_grass", "pas_herbe", "pasherbe", "grass_step" }),
            ("pasNeige", new[] { "footstep_snow", "pas_neige", "pasneige", "snow_step" }),
            ("pasBois", new[] { "footstep_wood", "pas_bois", "pasbois", "wood_step" }),
            ("pasPierre", new[] { "footstep_concrete", "pas_pierre", "paspierre", "stone_step" }),
            ("pasEau", new[] { "pas_eau", "paseau", "splash", "water_step" }),
            ("pasTerre", new[] { "footstep0", "footstep1", "pas_terre", "pasterre", "dirt", "gravel" }),
            ("parade", new[] { "impactplate", "impactmetal", "clang", "parade", "sword_clash" }),
            ("impactBois", new[] { "impactwood", "impactplank", "impact_bois" }),
            ("impactChair", new[] { "impactpunch", "impactsoft", "punch", "impact_chair", "flesh" }),
            ("lameLourde", new[] { "lame_lourde", "swing_heavy", "whoosh_heavy" }),
            ("lame", new[] { "knifeslice", "swish", "sword_swing", "whoosh", "lame_" }),
            ("degainer", new[] { "drawknife", "degainer", "unsheath" }),
            ("pieces", new[] { "handlecoins", "coins", "pieces" }),
            ("porteOuvre", new[] { "dooropen", "porte_ouvre" }),
            ("porteFerme", new[] { "doorclose", "porte_ferme" }),
            ("cloche", new[] { "impactbell", "cloche", "church_bell" }),
            ("coup", new[] { "impactmining", "chop", "coup_outil" }),
            ("roulade", new[] { "cloth", "roulade" }),
            ("ouvrir", new[] { "open_0", "maximize_0", "bookopen" }),
            ("fermer", new[] { "close_0", "minimize_0", "bookclose" }),
            ("erreur", new[] { "error_0" }),
            ("clic", new[] { "click_0", "select_0", "tick_0", "switch_0", "toggle_0" }),
        };

        public static string IdPour(string nomFichier)
        {
            string n = nomFichier.ToLowerInvariant();
            foreach (var (id, motifs) in Regles)
                foreach (var m in motifs)
                    if (n.Contains(m)) return id;
            return null;
        }
    }
}
