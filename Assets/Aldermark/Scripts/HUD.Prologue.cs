using UnityEngine;

namespace Aldermark
{
    /// <summary>Interface du prologue : sous-titres de narration, et HUD masqué avant l'Entre-Deux.</summary>
    public partial class HUD
    {
        UnityEngine.UI.Text sousTitre;
        float sousTitreDuree;

        /// <summary>Texte de narration en bas de l'écran (pas une fenêtre Système : on n'est pas encore à Aldermark).</summary>
        public static void SousTitre(string texte, float duree)
        {
            if (instance == null) return;
            if (instance.sousTitre == null)
            {
                var r = Panneau("SousTitre", instance.toile, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 190), new Vector2(1300, 110), new Color(0, 0, 0, 0));
                instance.sousTitre = instance.Texte("Texte", r, "", 26, new Color(.95f, .95f, .92f), TextAnchor.MiddleCenter);
                instance.sousTitre.fontStyle = FontStyle.Italic;
                instance.sousTitre.horizontalOverflow = HorizontalWrapMode.Wrap;
                Etirer((RectTransform)instance.sousTitre.transform, Vector2.zero, Vector2.zero);
            }
            instance.sousTitre.text = texte;
            instance.sousTitreDuree = duree;
        }

        /// <summary>Masque boussole, mana, quête et aide pendant le prologue.</summary>
        public static void ModePrologue(bool actif)
        {
            if (instance == null) return;
            foreach (var nom in new[] { "Boussole", "Mana", "Quete", "Etat", "Aide", "RepereQuete" })
            {
                var t = instance.toile.Find(nom);
                if (t != null) t.gameObject.SetActive(!actif);
            }
        }

        void MettreAJourSousTitre()
        {
            if (sousTitre == null) return;
            sousTitreDuree -= Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(sousTitreDuree / .6f);
            var c = sousTitre.color; c.a = a; sousTitre.color = c;
        }
    }
}
