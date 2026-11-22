using UnityEngine;

namespace Aldermark
{
    /// <summary>Interface des pouvoirs : Présence détectable (œil), Voile, alarme de Surcharge.</summary>
    public partial class HUD
    {
        UnityEngine.UI.Text textePresence, texteSurcharge;
        UnityEngine.UI.Image voileRouge;
        float surcharge = -1f;

        /// <summary>Rapport mana / maximum pendant une absorption (négatif : pas de surcharge).</summary>
        public static void Surcharge(float rapport) { if (instance != null) instance.surcharge = rapport; }

        void ConstruirePouvoirs()
        {
            var p = Panneau("Presence", toile, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(24, 112), new Vector2(420, 26), new Color(0, 0, 0, 0));
            textePresence = Texte("Texte", p, "", 17, new Color(.85f, .88f, 1f), TextAnchor.MiddleLeft);
            Etirer((RectTransform)textePresence.transform, Vector2.zero, Vector2.zero);

            var v = Panneau("Surcharge", toile, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero, new Color(1, 0, 0, 0));
            v.offsetMin = v.offsetMax = Vector2.zero;
            voileRouge = v.GetComponent<UnityEngine.UI.Image>();
            texteSurcharge = Texte("Alarme", v, "", 30, new Color(1f, .9f, .85f), TextAnchor.MiddleCenter);
            Etirer((RectTransform)texteSurcharge.transform, new Vector2(0, 120), Vector2.zero);
            texteSurcharge.fontStyle = FontStyle.Bold;
        }

        void MettreAJourPouvoirs()
        {
            if (textePresence == null) ConstruirePouvoirs();
            bool visible = !Prologue.EnCours && Progression.A("pacte_signe");
            textePresence.gameObject.SetActive(visible);
            if (visible)
            {
                string oeil = Pouvoirs.VoileActif ? "◌" : "◉";
                string couleur = Pouvoirs.Presence < .34f ? "#a8e6a0" : Pouvoirs.Presence < .67f ? "#ffd27a" : "#ff8a8a";
                textePresence.text = $"{oeil}  Présence : <color={couleur}>{Pouvoirs.NomPresence}</color>" + (Pouvoirs.VoileActif ? "   ·   Voile actif" : "")
                    + (Pouvoirs.SiphonActif && Pouvoirs.Cible != null ? $"   ·   Siphon : {Pouvoirs.Cible.nom}" : "");
            }

            // Alarme du Réceptacle : écran qui brûle, texte qui monte
            float r = surcharge;
            if (r > 1f)
            {
                float t = Mathf.Clamp01((r - 1f) / 1.24f);
                float pouls = .5f + .5f * Mathf.Sin(Time.unscaledTime * (6f + 14f * t));
                voileRouge.color = new Color(.85f, .05f, .02f, t * (.25f + .25f * pouls));
                texteSurcharge.text = r < 1.3f ? "" : r < 1.9f ? "⚠ SURCHARGE — capacité dépassée" : "⚠ SURCHARGE — RUPTURE IMMINENTE";
            }
            else { voileRouge.color = new Color(1, 0, 0, 0); texteSurcharge.text = ""; }
        }
    }
}
