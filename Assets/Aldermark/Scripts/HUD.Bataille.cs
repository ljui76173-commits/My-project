using UnityEngine;

namespace Aldermark
{
    /// <summary>Interface de la bataille du Gué : jauge du bras de fer (sous la boussole) et décompte en haut à droite.</summary>
    public partial class HUD
    {
        RectTransform jauge, jaugePleine;
        UnityEngine.UI.Text texteJauge, texteBilan;

        /// <summary>Affiche la jauge (valeur de 0 à 1) avec son titre ; valeur négative : cachée.</summary>
        public static void Jauge(string titre, float valeur)
        {
            if (instance == null) return;
            instance.ConstruireBataille();
            bool visible = valeur >= 0f;
            if (instance.jauge.gameObject.activeSelf != visible) instance.jauge.gameObject.SetActive(visible);
            if (!visible) return;
            instance.texteJauge.text = titre ?? "";
            instance.jaugePleine.anchorMax = new Vector2(Mathf.Clamp01(valeur), 1f);
        }

        /// <summary>Le décompte de la bataille (null : caché).</summary>
        public static void Bilan(string texte)
        {
            if (instance == null) return;
            instance.ConstruireBataille();
            instance.texteBilan.text = texte ?? "";
        }

        void ConstruireBataille()
        {
            if (jauge != null) return;
            jauge = Panneau("Jauge", toile, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -100), new Vector2(520, 14), new Color(.06f, .05f, .1f, .85f));
            jaugePleine = Panneau("Plein", jauge, Vector2.zero, new Vector2(0, 1), new Vector2(0, .5f), Vector2.zero, Vector2.zero, new Color(.45f, .7f, 1f, .95f));
            jaugePleine.offsetMin = jaugePleine.offsetMax = Vector2.zero;
            texteJauge = Texte("Titre", jauge, "", 17, new Color(.88f, .92f, 1f), TextAnchor.LowerCenter);
            var rt = (RectTransform)texteJauge.transform;
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(.5f, 0);
            rt.anchoredPosition = new Vector2(0, 4); rt.sizeDelta = new Vector2(200, 24);
            texteJauge.horizontalOverflow = HorizontalWrapMode.Overflow;
            jauge.gameObject.SetActive(false);

            var b = Panneau("BilanBataille", toile, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -160), new Vector2(420, 90), new Color(0, 0, 0, 0));
            texteBilan = Texte("Texte", b, "", 18, new Color(.95f, .93f, .88f), TextAnchor.UpperRight);
            Etirer((RectTransform)texteBilan.transform, Vector2.zero, Vector2.zero);
        }
    }
}
