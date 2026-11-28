using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// Apprendre en observant : quand une créature lance devant toi un sort que tu ne connais pas, ta jauge d'Analyse
    /// pour ce sort monte. Elle monte plus vite avec ta Perception et ton Intelligence, de près, avec la technique Analyse ;
    /// deux fois plus vite quand tu encaisses le sort (et tu gagnes alors une résistance à son élément).
    /// À 100 %, le sort est à toi.
    /// </summary>
    public partial class Competences
    {
        /// <summary>Ce qu'apporte une observation de près, avec des stats de départ (environ 8 observations pour comprendre un sort).</summary>
        const float GainBase = .13f;
        /// <summary>Résistance gagnée à chaque sort encaissé, et son maximum.</summary>
        const float GainResistance = .02f, ResistanceMax = .5f;
        const float DistanceMax = 45f;

        readonly Dictionary<string, float> analyses = new Dictionary<string, float>();
        readonly Dictionary<Element, float> resistances = new Dictionary<Element, float>();

        /// <summary>La jauge d'analyse vient de bouger (pour l'écran) : le sort, la nouvelle valeur (0 à 1), vrai si encaissé.</summary>
        public static event System.Action<DefCompetence, float, bool> AnalyseProgresse;

        public float Analyse(string id) => id != null && analyses.TryGetValue(id, out var v) ? v : 0f;
        public IEnumerable<KeyValuePair<string, float>> Analyses => analyses;

        /// <summary>De 0 à 0,5 : la part des dégâts de cet élément que tu ignores.</summary>
        public float Resistance(Element e) => e != Element.Aucun && resistances.TryGetValue(e, out var r) ? r : 0f;
        public IEnumerable<KeyValuePair<Element, float>> Resistances => resistances;

        void OnEnable() => Sorts.Lance += SortLance;
        void OnDisable() => Sorts.Lance -= SortLance;

        /// <summary>Un sort vient d'être lancé quelque part : si c'est une créature, que tu le vois et que tu ne le connais pas, tu l'analyses.</summary>
        void SortLance(Incantation s)
        {
            var d = s.def;
            if (d == null || !d.EstSort || s.auteur == null || s.auteur is CombatHeros || Connait(d.id)) return;
            if (!Cibles.Existe(s.auteur)) return;
            var h = CombatHeros.Instance;
            if (h == null || !h.Vivant) return;
            if (!Visible(s.auteur, out float distance)) return;
            // De près : plein effet ; à 45 m : un tiers
            float proche = Mathf.Lerp(1f, .35f, Mathf.InverseLerp(8f, DistanceMax, distance));
            Progresser(d, GainBase * Facteur() * proche, false);
        }

        /// <summary>Tu viens d'encaisser un sort : tu le comprends deux fois mieux, et tu t'endurcis à son élément.</summary>
        public void Encaisser(Coup c)
        {
            if (string.IsNullOrEmpty(c.sort) || c.auteur is CombatHeros) return;
            var d = CatalogueCompetences.Get(c.sort);
            if (d == null) return;
            if (d.element != Element.Aucun && c.degats > 0)
            {
                float avant = Resistance(d.element);
                float apres = Mathf.Min(ResistanceMax, avant + GainResistance);
                resistances[d.element] = apres;
                if (Mathf.FloorToInt(apres * 10f) > Mathf.FloorToInt(avant * 10f))
                    HUD.Toast($"Résistance au {CatalogueCompetences.NomElement(d.element).ToLower()} : {apres * 100f:0} %");
            }
            if (!Connait(d.id)) Progresser(d, GainBase * 2f * Facteur(), true);
        }

        /// <summary>PER, INT et la technique Analyse accélèrent la compréhension.</summary>
        float Facteur()
        {
            float per = stats != null ? (float)stats.Valeur(Stat.PER) : 5f;
            float intel = stats != null ? (float)stats.Valeur(Stat.INT) : 5f;
            float f = (1f + .06f * (per - 5f)) * (1f + .04f * (intel - 5f));
            f *= 1f + .1f * (Mathf.Max(1, Niveau("analyse")) - 1);
            return Mathf.Max(.3f, f);
        }

        /// <summary>Le lanceur est-il à l'écran, assez près, et pas caché derrière un mur ?</summary>
        static bool Visible(ICible lanceur, out float distance)
        {
            distance = float.MaxValue;
            var cam = Camera.main;
            if (cam == null) return false;
            Vector3 p = lanceur.Centre;
            distance = Vector3.Distance(cam.transform.position, p);
            if (distance > DistanceMax) return false;
            Vector3 v = cam.WorldToViewportPoint(p);
            if (v.z <= 0f || v.x < -.05f || v.x > 1.05f || v.y < -.05f || v.y > 1.05f) return false;
            var hits = Physics.RaycastAll(cam.transform.position, (p - cam.transform.position).normalized, distance - .3f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                var c = hit.collider.GetComponentInParent<ICible>();
                if (c is CombatHeros || ReferenceEquals(c, lanceur)) continue;
                if (c != null) continue;   // une autre créature ne cache pas vraiment la vue
                return false;
            }
            return true;
        }

        void Progresser(DefCompetence d, float gain, bool encaisse)
        {
            float avant = Analyse(d.id);
            float apres = Mathf.Min(1f, avant + gain);
            analyses[d.id] = apres;
            Exercer("analyse", gain * 10f);
            if (stats != null) { stats.Exercer(Stat.PER, .4); stats.Exercer(Stat.INT, .3); }
            if (avant <= 0f) HUD.Toast($"Tu observes un sort inconnu : <color=#{ColorUtility.ToHtmlStringRGB(d.couleur)}>{d.nom}</color>");
            AnalyseProgresse?.Invoke(d, apres, encaisse);
            HUD.AnalyseSort(d, apres, encaisse);
            if (apres < 1f) { Sons.Jouer("clic", .35f, 1.2f + apres); return; }
            analyses.Remove(d.id);
            Apprendre(d.id, true, encaisse ? "Tu l'as encaissé assez souvent pour le comprendre." : "Tu l'as regardé assez longtemps pour le comprendre.");
        }
    }
}
