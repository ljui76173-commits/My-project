using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Aldermark
{
    /// <summary>
    /// Interface du mode RPG : barres de vie, mana et endurance, niveau et XP, horloge du monde,
    /// petites notifications, fenêtre de Statut (C) et console de debug (²).
    /// </summary>
    public partial class HUD
    {
        /// <summary>Un menu plein écran est ouvert (Statut, console) : le héros et le temps s'arrêtent.</summary>
        public static bool MenuOuvert => instance != null && (instance.statutOuvert || ConsoleDebug.Ouverte || instance.inventaireOuvert || Coffre.Ouvert != null);

        bool rpgConstruit, statutOuvert;
        RectTransform remplVie, remplMana, remplEndurance, remplXP, panneauStatut, remplXPStatut, panneauConsole;
        UnityEngine.UI.Text texteVie, texteManaRPG, texteEndurance, texteNiveau, texteHorloge, texteToasts;
        UnityEngine.UI.Text statutTitre, statutSousTitre, statutGauche, statutBas, consoleHistorique, consoleSaisie;
        readonly UnityEngine.UI.Text[] statNom = new UnityEngine.UI.Text[6], statValeur = new UnityEngine.UI.Text[6], statDetail = new UnityEngine.UI.Text[6], statPlus = new UnityEngine.UI.Text[6], statEntrainement = new UnityEngine.UI.Text[6];
        readonly RectTransform[] statProgres = new RectTransform[6], statBouton = new RectTransform[6];
        readonly List<(string texte, float fin)> toasts = new List<(string, float)>();

        static readonly Color CouleurVie = new Color(.86f, .3f, .3f), CouleurManaRPG = new Color(.44f, .7f, 1f),
            CouleurEndurance = new Color(.93f, .8f, .36f), CouleurOr = new Color(1f, .82f, .45f), Pale = new Color(.78f, .76f, .9f);

        public static void Toast(string texte)
        {
            if (instance == null || string.IsNullOrEmpty(texte)) return;
            instance.toasts.Add((texte, Time.unscaledTime + 4f));
            while (instance.toasts.Count > 6) instance.toasts.RemoveAt(0);
        }

        /// <summary>Affiche la console (historique + ligne en cours) ; null : cachée.</summary>
        public static void Console(string historique, string ligne)
        {
            if (instance == null) return;
            instance.ConstruireRPG();
            bool v = ligne != null;
            if (instance.panneauConsole.gameObject.activeSelf != v) instance.panneauConsole.gameObject.SetActive(v);
            if (!v) return;
            instance.consoleHistorique.text = historique;
            instance.consoleSaisie.text = ligne;
        }

        // ---------- construction ----------

        void ConstruireRPG()
        {
            if (rpgConstruit) return;
            rpgConstruit = true;

            // L'interface du scénario n'a plus cours
            foreach (var nom in new[] { "Mana", "Quete", "Etat" })
            {
                var t = toile.Find(nom);
                if (t != null) t.gameObject.SetActive(false);
            }
            var aide = toile.Find("Aide/Texte");
            if (aide != null)
            {
                aide.GetComponent<UnityEngine.UI.Text>().text =
                    "ZQSD marcher · Maj courir · Espace sauter · Clic G frapper (maintenu : lourd) · Clic D garde · Alt roulade · Molette cibler · H ranger l'arme · E ramasser · F parler · I inventaire · C Statut · V vue · ² console";
                ((RectTransform)aide.parent).sizeDelta = new Vector2(1560, 38);
            }

            // Barres en bas à gauche
            var bloc = Panneau("BarresRPG", toile, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(24, 70), new Vector2(380, 130), new Color(0, 0, 0, 0));
            (remplEndurance, texteEndurance) = Barre("Endurance", bloc, 0, 18, CouleurEndurance);
            (remplMana, texteManaRPG) = Barre("Mana", bloc, 24, 18, CouleurManaRPG);
            (remplVie, texteVie) = Barre("Vie", bloc, 48, 18, CouleurVie);
            remplXP = Barre("XP", bloc, 76, 6, CouleurOr).Item1;
            texteNiveau = Texte("Niveau", bloc, "", 24, Color.white, TextAnchor.LowerLeft);
            var rn = (RectTransform)texteNiveau.transform;
            rn.anchorMin = rn.anchorMax = rn.pivot = Vector2.zero;
            rn.anchoredPosition = new Vector2(0, 88); rn.sizeDelta = new Vector2(380, 34);

            // Horloge en haut à droite
            var h = Panneau("HorlogeRPG", toile, Vector2.one, Vector2.one, Vector2.one, new Vector2(-24, -24), new Vector2(420, 70), Verre);
            texteHorloge = Texte("Texte", h, "", 20, Color.white, TextAnchor.MiddleRight);
            Etirer((RectTransform)texteHorloge.transform, new Vector2(16, 6), new Vector2(-18, -6));

            // Notifications à droite
            var n = Panneau("Notifications", toile, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 200), new Vector2(620, 220), new Color(0, 0, 0, 0));
            texteToasts = Texte("Texte", n, "", 19, new Color(.95f, .93f, 1f), TextAnchor.LowerRight);
            Etirer((RectTransform)texteToasts.transform, Vector2.zero, Vector2.zero);

            ConstruireStatut();

            // Console de debug
            panneauConsole = Panneau("Console", toile, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -70), new Vector2(1280, 400), new Color(.03f, .03f, .06f, .92f));
            consoleHistorique = Texte("Historique", panneauConsole, "", 18, new Color(.8f, .85f, .95f), TextAnchor.LowerLeft);
            consoleHistorique.horizontalOverflow = HorizontalWrapMode.Wrap;
            Etirer((RectTransform)consoleHistorique.transform, new Vector2(20, 52), new Vector2(-20, -14));
            consoleSaisie = Texte("Saisie", panneauConsole, "", 21, CouleurOr, TextAnchor.MiddleLeft);
            var rs = (RectTransform)consoleSaisie.transform;
            rs.anchorMin = Vector2.zero; rs.anchorMax = new Vector2(1, 0); rs.pivot = new Vector2(.5f, 0);
            rs.offsetMin = new Vector2(20, 12); rs.offsetMax = new Vector2(-20, 44);
            panneauConsole.gameObject.SetActive(false);
        }

        (RectTransform, UnityEngine.UI.Text) Barre(string nom, Transform parent, float y, float hauteur, Color couleur)
        {
            var fond = Panneau(nom, parent, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(0, y), new Vector2(340, hauteur), new Color(0, 0, 0, .45f));
            var r = Panneau("Remplissage", fond, Vector2.zero, Vector2.one, new Vector2(0, .5f), Vector2.zero, Vector2.zero, couleur);
            r.offsetMin = r.offsetMax = Vector2.zero;
            UnityEngine.UI.Text t = null;
            if (hauteur >= 14)
            {
                t = Texte("Texte", fond, "", 14, Color.white, TextAnchor.MiddleLeft);
                Etirer((RectTransform)t.transform, new Vector2(8, 0), new Vector2(-6, 0));
            }
            return (r, t);
        }

        void ConstruireStatut()
        {
            panneauStatut = Panneau("Statut", toile, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 10), new Vector2(1240, 700), new Color(.055f, .047f, .12f, .94f));
            var cadre = Panneau("Cadre", panneauStatut, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0));
            cadre.offsetMin = new Vector2(6, 6); cadre.offsetMax = new Vector2(-6, -6);
            var contour = cadre.gameObject.AddComponent<UnityEngine.UI.Outline>();
            contour.effectColor = new Color(.81f, .78f, 1f, .8f); contour.effectDistance = new Vector2(2, -2);

            statutTitre = Haut(Texte("Titre", panneauStatut, "", 32, Color.white, TextAnchor.UpperLeft), 40, 28, 900, 44);
            statutTitre.fontStyle = FontStyle.Bold;
            statutSousTitre = Haut(Texte("SousTitre", panneauStatut, "", 20, CouleurOr, TextAnchor.UpperLeft), 40, 74, 1100, 30);

            // Colonne de gauche : expérience, vie, dérivés
            var fondXP = Panneau("BarreXP", panneauStatut, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -150), new Vector2(380, 10), new Color(1, 1, 1, .12f));
            remplXPStatut = Panneau("Remplissage", fondXP, Vector2.zero, Vector2.one, new Vector2(0, .5f), Vector2.zero, Vector2.zero, CouleurOr);
            remplXPStatut.offsetMin = remplXPStatut.offsetMax = Vector2.zero;
            statutGauche = Haut(Texte("Gauche", panneauStatut, "", 19, new Color(.92f, .9f, 1f), TextAnchor.UpperLeft), 40, 118, 400, 520);
            statutGauche.lineSpacing = 1.15f;

            // Colonne de droite : les six stats
            for (int i = 0; i < 6; i++)
            {
                float y = 122 + i * 84;
                Panneau("Ligne" + i, panneauStatut, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(470, -(y + 76)), new Vector2(730, 1), new Color(1, 1, 1, .08f));
                statNom[i] = Haut(Texte("Nom" + i, panneauStatut, "", 23, Color.white, TextAnchor.UpperLeft), 470, y, 420, 30);
                statDetail[i] = Haut(Texte("Detail" + i, panneauStatut, "", 15, Pale, TextAnchor.UpperLeft), 470, y + 34, 350, 22);
                statEntrainement[i] = Haut(Texte("Entraine" + i, panneauStatut, "", 14, new Color(.6f, .85f, .7f), TextAnchor.UpperLeft), 744, y + 52, 360, 20);
                var fp = Panneau("Entrainement" + i, panneauStatut, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(470, -(y + 60)), new Vector2(260, 5), new Color(1, 1, 1, .1f));
                statProgres[i] = Panneau("Remplissage", fp, Vector2.zero, Vector2.one, new Vector2(0, .5f), Vector2.zero, Vector2.zero, new Color(.6f, .85f, .7f));
                statProgres[i].offsetMin = statProgres[i].offsetMax = Vector2.zero;
                statValeur[i] = Haut(Texte("Valeur" + i, panneauStatut, "", 30, Color.white, TextAnchor.UpperRight), 830, y, 260, 40);
                statValeur[i].fontStyle = FontStyle.Bold;
                statBouton[i] = Panneau("Plus" + i, panneauStatut, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(1112, -(y + 4)), new Vector2(84, 50), new Color(1f, .82f, .45f, .18f));
                statPlus[i] = Texte("Texte", statBouton[i], $"+  [{i + 1}]", 20, CouleurOr, TextAnchor.MiddleCenter);
                Etirer((RectTransform)statPlus[i].transform, Vector2.zero, Vector2.zero);
            }
            statutBas = Texte("Bas", panneauStatut, "", 18, Pale, TextAnchor.LowerLeft);
            var rb = (RectTransform)statutBas.transform;
            rb.anchorMin = Vector2.zero; rb.anchorMax = new Vector2(1, 0); rb.pivot = new Vector2(.5f, 0);
            rb.offsetMin = new Vector2(40, 22); rb.offsetMax = new Vector2(-40, 56);
            panneauStatut.gameObject.SetActive(false);
        }

        /// <summary>Place un texte en haut à gauche du parent (x, y vers le bas, en pixels).</summary>
        static UnityEngine.UI.Text Haut(UnityEngine.UI.Text t, float x, float y, float l, float h)
        {
            var r = (RectTransform)t.transform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(l, h);
            return t;
        }

        // ---------- mise à jour ----------

        void MettreAJourRPG(float dt)
        {
            ConstruireRPG();
            var s = StatsHeros.Instance;
            var kb = Keyboard.current;

            if (s != null && joueur != null)
            {
                Remplir(remplVie, texteVie, s.Vie, s.VieMax, "Vie");
                Remplir(remplMana, texteManaRPG, joueur.Mana, joueur.manaMax, "Mana");
                Remplir(remplEndurance, texteEndurance, s.Endurance, s.EnduranceMax, s.Essouffle ? "Essoufflé" : "Endurance");
                remplEndurance.GetComponent<UnityEngine.UI.Image>().color = s.Essouffle ? new Color(.7f, .45f, .3f) : CouleurEndurance;
                remplXP.anchorMax = new Vector2(Mathf.Clamp01((float)(s.XP / StatsHeros.XPRequise(s.Niveau))), 1);
                texteNiveau.text = $"<b>Niv. {s.Niveau}</b>   <size=17>{s.nomHeros}" + (s.PointsLibres > 0 ? $"   <color=#ffd27a>+{s.PointsLibres} points (C)</color>" : "") + "</size>";
            }
            texteHorloge.text = $"<size=26><b>{Aldermark.Horloge.TexteHeure}</b></size>\nJour {Aldermark.Horloge.Jour} · {Aldermark.Horloge.NomSaison}" + (Aldermark.Horloge.Vitesse != 1f ? $"  (×{Aldermark.Horloge.Vitesse:0.#})" : "");

            // Notifications
            toasts.RemoveAll(t => t.fin < Time.unscaledTime);
            var sb = new System.Text.StringBuilder();
            foreach (var (texte, fin) in toasts)
            {
                float a = Mathf.Clamp01(fin - Time.unscaledTime);
                sb.AppendLine($"<color=#{ColorUtility.ToHtmlStringRGBA(new Color(1, 1, 1, a))}>{texte}</color>");
            }
            texteToasts.text = sb.ToString();

            // Statut : C pour ouvrir ou fermer, Échap pour fermer
            if (kb != null && !ConsoleDebug.Ouverte && !Dialogues.EnCours && !inventaireOuvert && Coffre.Ouvert == null && kb.cKey.wasPressedThisFrame) OuvrirStatut(!statutOuvert);
            if (statutOuvert && kb != null && kb.escapeKey.wasPressedThisFrame) OuvrirStatut(false);
            if (statutOuvert && s != null) MettreAJourStatut(s, kb);

            MettreAJourInventaire();
            MettreAJourCombat();
        }

        static void Remplir(RectTransform r, UnityEngine.UI.Text t, double v, double max, string nom)
        {
            r.anchorMax = new Vector2(max > 0 ? Mathf.Clamp01((float)(v / max)) : 0, 1);
            if (t != null) t.text = $"{nom}  {Nombres.Court(v)} / {Nombres.Court(max)}";
        }

        void OuvrirStatut(bool ouvrir)
        {
            statutOuvert = ouvrir;
            panneauStatut.gameObject.SetActive(ouvrir);
            Cursor.lockState = ouvrir ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = ouvrir;
            Sons.Jouer("clic", .5f, ouvrir ? 1.3f : .9f);
        }

        void MettreAJourStatut(StatsHeros s, Keyboard kb)
        {
            double requise = StatsHeros.XPRequise(s.Niveau);
            statutTitre.text = $"STATUT — {s.nomHeros.ToUpper()}";
            statutSousTitre.text = $"Niveau {s.Niveau}   ·   {s.Titre}   ·   toutes les stats {Nombres.Facteur(s.Facteur)} (×1,09 par niveau)";
            remplXPStatut.anchorMax = new Vector2(Mathf.Clamp01((float)(s.XP / requise)), 1);
            statutGauche.text =
                $"<b>Expérience</b>  {Nombres.Court(s.XP)} / {Nombres.Court(requise)}\n\n\n" +
                $"<color=#e06a6a>Vie</color>  {Nombres.Court(s.Vie)} / {Nombres.Court(s.VieMax)}\n" +
                $"<color=#7ab4ff>Mana</color>  {Nombres.Court(joueur.Mana)} / {Nombres.Court(joueur.manaMax)}\n" +
                $"<color=#eecc5c>Endurance</color>  {Nombres.Court(s.Endurance)} / {Nombres.Court(s.EnduranceMax)}\n\n" +
                $"Vitesse  {Nombres.Facteur(s.MultiplicateurVitesse)}\n" +
                $"Charge maximale  {Nombres.Court(s.ChargeMax)} kg\n" +
                $"Régénération du mana  {Nombres.Court(s.RegenMana)} / s\n\n" +
                $"Zones explorées  {s.ZonesExplorees}\n" +
                $"{Aldermark.Horloge.Texte}";
            for (int i = 0; i < 6; i++)
            {
                var st = (Stat)i;
                statNom[i].text = $"<b>{StatsHeros.Abreges[i]}</b>  {StatsHeros.Noms[i]}   <size=15><color=#a9a6c4>{StatsHeros.Roles[i]}</color></size>";
                statValeur[i].text = Nombres.Court(s.Valeur(st));
                statDetail[i].text = $"base {Nombres.Court(s.baseStat)} + points {Nombres.Court(s.Points(st))} + entraînement {Nombres.Court(s.Usage(st))}";
                statEntrainement[i].text = $"s'entraîne : {StatsHeros.Entrainement[i]}";
                statProgres[i].anchorMax = new Vector2(Mathf.Clamp01((float)s.Progres(st)), 1);
                bool dispo = s.PointsLibres > 0;
                statBouton[i].gameObject.SetActive(dispo);
            }
            statutBas.text = s.PointsLibres > 0
                ? $"<color=#ffd27a><b>{s.PointsLibres} points à répartir</b></color>   ·   clique sur + ou appuie sur 1 à 6   ·   C ou Échap : fermer"
                : "Chaque niveau donne 3 points à répartir   ·   les stats montent aussi en s'en servant   ·   C ou Échap : fermer";

            if (s.PointsLibres <= 0 || kb == null) return;
            var touches = new[] { kb.digit1Key, kb.digit2Key, kb.digit3Key, kb.digit4Key, kb.digit5Key, kb.digit6Key };
            var souris = Mouse.current;
            for (int i = 0; i < 6; i++)
            {
                bool clic = souris != null && souris.leftButton.wasPressedThisFrame &&
                            RectTransformUtility.RectangleContainsScreenPoint(statBouton[i], souris.position.ReadValue(), null);
                if (touches[i].wasPressedThisFrame || clic) s.Repartir((Stat)i);
            }
        }
    }
}
