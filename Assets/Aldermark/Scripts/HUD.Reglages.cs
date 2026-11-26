using UnityEngine;
using UnityEngine.InputSystem;

namespace Aldermark
{
    /// <summary>
    /// La fenêtre Réglages (Échap quand rien d'autre n'est ouvert) : le jeu se met en pause, et les cinq volumes
    /// (musique, ambiances, effets, voix, interface) se règlent à la souris (clic ou glisser sur la barre)
    /// ou au clavier (↑ ↓ pour choisir, ← → pour régler). Les réglages sont gardés d'une partie à l'autre.
    /// </summary>
    public partial class HUD
    {
        RectTransform panneauReglages;
        readonly RectTransform[] barresVolume = new RectTransform[5], remplVolume = new RectTransform[5];
        readonly UnityEngine.UI.Text[] textesVolume = new UnityEngine.UI.Text[5];
        bool reglagesOuverts;
        int ligneReglage;
        float tempsAvantPause = 1f;
        static readonly string[] NomsCanaux = { "Musique", "Ambiances", "Effets", "Voix", "Interface" };

        void ConstruireReglages()
        {
            if (panneauReglages != null) return;
            panneauReglages = Panneau("Reglages", toile, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(720, 440), new Color(.055f, .047f, .12f, .94f));
            var titre = Texte("Titre", panneauReglages, "PAUSE  ·  RÉGLAGES DU SON", 26, CouleurOr, TextAnchor.UpperCenter);
            titre.fontStyle = FontStyle.Bold;
            Etirer((RectTransform)titre.transform, new Vector2(20, 20), new Vector2(-20, -22));
            for (int k = 0; k < 5; k++)
            {
                float y = -100 - k * 56;
                var nom = Texte("Nom" + k, panneauReglages, NomsCanaux[k], 21, Color.white, TextAnchor.MiddleLeft);
                var rn = (RectTransform)nom.transform;
                rn.anchorMin = rn.anchorMax = rn.pivot = new Vector2(0, 1);
                rn.anchoredPosition = new Vector2(48, y); rn.sizeDelta = new Vector2(160, 40);
                barresVolume[k] = Panneau("Barre" + k, panneauReglages, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, .5f), new Vector2(220, y - 20), new Vector2(360, 18), new Color(1, 1, 1, .12f));
                remplVolume[k] = Panneau("Rempli", barresVolume[k], Vector2.zero, new Vector2(1, 1), Vector2.zero, Vector2.zero, Vector2.zero, CouleurOr);
                remplVolume[k].offsetMin = remplVolume[k].offsetMax = Vector2.zero;
                textesVolume[k] = Texte("Valeur" + k, panneauReglages, "", 20, Pale, TextAnchor.MiddleRight);
                var rv = (RectTransform)textesVolume[k].transform;
                rv.anchorMin = rv.anchorMax = rv.pivot = new Vector2(0, 1);
                rv.anchoredPosition = new Vector2(590, y); rv.sizeDelta = new Vector2(90, 40);
            }
            var aide = Texte("Aide", panneauReglages, "Clic ou glisser sur une barre  ·  ↑ ↓ choisir  ·  ← → régler  ·  Échap : reprendre", 16, Pale, TextAnchor.LowerCenter);
            Etirer((RectTransform)aide.transform, new Vector2(20, 18), new Vector2(-20, -20));
            panneauReglages.gameObject.SetActive(false);
        }

        void OuvrirReglages(bool ouvrir)
        {
            ConstruireReglages();
            if (reglagesOuverts == ouvrir) return;
            reglagesOuverts = ouvrir;
            panneauReglages.gameObject.SetActive(ouvrir);
            Cursor.lockState = ouvrir ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = ouvrir;
            if (ouvrir) { tempsAvantPause = Time.timeScale; Time.timeScale = 0f; }
            else Time.timeScale = tempsAvantPause > 0f ? tempsAvantPause : 1f;
            Sons.Jouer("clic", .5f, ouvrir ? 1.3f : .9f);
        }

        /// <summary>Appelé à la fin de MettreAJourRPG : rienOuvert dit si aucune fenêtre n'était ouverte au début de l'image.</summary>
        void MettreAJourReglages(Keyboard kb, bool rienOuvert)
        {
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame && (reglagesOuverts || (rienOuvert && Time.frameCount != ConsoleDebug.ImageFermeture)))
            {
                OuvrirReglages(!reglagesOuverts);
                return;
            }
            if (!reglagesOuverts) return;
            if (kb.upArrowKey.wasPressedThisFrame) ligneReglage = (ligneReglage + 4) % 5;
            if (kb.downArrowKey.wasPressedThisFrame) ligneReglage = (ligneReglage + 1) % 5;
            float pas = kb.rightArrowKey.wasPressedThisFrame ? .05f : kb.leftArrowKey.wasPressedThisFrame ? -.05f : 0f;
            if (pas != 0f) { Sons.FixerVolume((CanalSon)ligneReglage, Sons.Volume((CanalSon)ligneReglage) + pas); Sons.Jouer("clic", .5f, 1.1f); }
            var souris = Mouse.current;
            if (souris != null && souris.leftButton.isPressed)
                for (int k = 0; k < 5; k++)
                {
                    var b = barresVolume[k];
                    Vector2 ecran = souris.position.ReadValue();
                    var zone = new Rect(b.rect.x, b.rect.y - 10f, b.rect.width, b.rect.height + 20f);
                    if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(b, ecran, null, out var local) || !zone.Contains(local)) continue;
                    ligneReglage = k;
                    Sons.FixerVolume((CanalSon)k, Mathf.Clamp01((local.x - b.rect.x) / b.rect.width));
                }
            for (int k = 0; k < 5; k++)
            {
                float v = Sons.Volume((CanalSon)k);
                remplVolume[k].anchorMax = new Vector2(v, 1);
                textesVolume[k].text = $"{Mathf.RoundToInt(v * 100)} %";
                textesVolume[k].color = k == ligneReglage ? Color.white : Pale;
                barresVolume[k].GetComponent<UnityEngine.UI.Image>().color = new Color(1, 1, 1, k == ligneReglage ? .22f : .12f);
            }
        }
    }
}
