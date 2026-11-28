using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Aldermark
{
    /// <summary>
    /// Fenêtres d'inventaire (I) et de coffre (E sur un coffre), et le rappel de l'arme en main près des barres.
    /// Inventaire : la liste du sac (poids, valeur), le détail de l'objet survolé, équiper (E ou clic droit),
    /// utiliser (U), jeter (X, Maj+X pour toute la pile). Coffre : deux colonnes, un clic fait passer une pile de l'autre côté,
    /// T prend tout.
    /// </summary>
    public partial class HUD
    {
        class ListeUI
        {
            public RectTransform cadre;
            public readonly List<(RectTransform rect, UnityEngine.UI.Image fond, UnityEngine.UI.Text[] cellules)> lignes =
                new List<(RectTransform, UnityEngine.UI.Image, UnityEngine.UI.Text[])>();
            public int debut, survol = -1, nombre;
        }

        const float HauteurLigne = 30f;
        bool inventaireConstruit, inventaireOuvert;
        RectTransform panneauInventaire, panneauCoffre;
        ListeUI listeSac, listeCoffre, listeSacCoffre;
        UnityEngine.UI.Text invSousTitre, invDetail, coffreTitre, coffreBas, texteArme;
        int selectionSac, cadreCoffre = -1;

        public static bool InventaireOuvert => instance != null && instance.inventaireOuvert;

        /// <summary>Affiche la fenêtre du coffre ouvert (Coffre.Ouvert).</summary>
        public static void OuvrirCoffre()
        {
            if (instance == null) return;
            instance.ConstruireInventaire();
            instance.cadreCoffre = Time.frameCount;
            instance.OuvrirInventaire(false);
            instance.panneauCoffre.gameObject.SetActive(true);
            instance.listeCoffre.debut = instance.listeSacCoffre.debut = 0;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void FermerCoffre()
        {
            Coffre.Fermer();
            panneauCoffre.gameObject.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void OuvrirInventaire(bool ouvrir)
        {
            ConstruireInventaire();
            if (inventaireOuvert == ouvrir) return;
            inventaireOuvert = ouvrir;
            panneauInventaire.gameObject.SetActive(ouvrir);
            Cursor.lockState = ouvrir ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = ouvrir;
            Sons.Jouer("clic", .5f, ouvrir ? 1.25f : .9f);
        }

        // ---------- construction ----------

        void ConstruireInventaire()
        {
            if (inventaireConstruit) return;
            inventaireConstruit = true;
            var fondFenetre = new Color(.055f, .047f, .12f, .95f);

            // Rappel de l'arme, au-dessus des barres
            var barres = toile.Find("BarresRPG");
            texteArme = Texte("Arme", barres != null ? barres : toile, "", 16, new Color(.9f, .88f, 1f), TextAnchor.LowerLeft);
            var ra = (RectTransform)texteArme.transform;
            ra.anchorMin = ra.anchorMax = ra.pivot = Vector2.zero;
            ra.anchoredPosition = new Vector2(0, 124); ra.sizeDelta = new Vector2(420, 24);

            // Inventaire
            panneauInventaire = Panneau("Inventaire", toile, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 10), new Vector2(1200, 680), fondFenetre);
            Contour(panneauInventaire);
            var titre = Haut(Texte("Titre", panneauInventaire, "INVENTAIRE", 32, Color.white, TextAnchor.UpperLeft), 40, 26, 600, 44);
            titre.fontStyle = FontStyle.Bold;
            invSousTitre = Haut(Texte("SousTitre", panneauInventaire, "", 19, CouleurOr, TextAnchor.UpperLeft), 40, 72, 1120, 28);
            listeSac = CreerListe(panneauInventaire, new Vector2(40, 118), 660, 15, new[] { 0f, 360f, 480f, 570f }, new[] { "Objet", "Catégorie", "Poids", "Valeur" });
            invDetail = Haut(Texte("Detail", panneauInventaire, "", 18, new Color(.92f, .9f, 1f), TextAnchor.UpperLeft), 740, 118, 420, 470);
            invDetail.horizontalOverflow = HorizontalWrapMode.Wrap;
            Haut(Texte("Bas", panneauInventaire, "Survole un objet  ·  E ou clic droit : équiper / utiliser  ·  U : utiliser  ·  X : jeter 1  ·  Maj+X : jeter tout  ·  I ou Échap : fermer",
                15, Pale, TextAnchor.UpperLeft), 40, 640, 1120, 24);
            panneauInventaire.gameObject.SetActive(false);

            // Coffre
            panneauCoffre = Panneau("Coffre", toile, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 10), new Vector2(1200, 620), fondFenetre);
            Contour(panneauCoffre);
            coffreTitre = Haut(Texte("Titre", panneauCoffre, "COFFRE", 32, Color.white, TextAnchor.UpperLeft), 40, 26, 1100, 44);
            coffreTitre.fontStyle = FontStyle.Bold;
            Haut(Texte("Gauche", panneauCoffre, "Dans le coffre", 19, CouleurOr, TextAnchor.UpperLeft), 40, 78, 500, 26);
            Haut(Texte("Droite", panneauCoffre, "Dans ton sac", 19, CouleurOr, TextAnchor.UpperLeft), 620, 78, 500, 26);
            listeCoffre = CreerListe(panneauCoffre, new Vector2(40, 112), 540, 13, new[] { 0f, 330f, 440f }, new[] { "Objet", "Poids", "Valeur" });
            listeSacCoffre = CreerListe(panneauCoffre, new Vector2(620, 112), 540, 13, new[] { 0f, 330f, 440f }, new[] { "Objet", "Poids", "Valeur" });
            coffreBas = Haut(Texte("Bas", panneauCoffre, "", 15, Pale, TextAnchor.UpperLeft), 40, 584, 1120, 24);
            panneauCoffre.gameObject.SetActive(false);
        }

        static void Contour(RectTransform panneau)
        {
            var cadre = Panneau("Cadre", panneau, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0));
            cadre.offsetMin = new Vector2(6, 6); cadre.offsetMax = new Vector2(-6, -6);
            var c = cadre.gameObject.AddComponent<UnityEngine.UI.Outline>();
            c.effectColor = new Color(.81f, .78f, 1f, .8f); c.effectDistance = new Vector2(2, -2);
        }

        ListeUI CreerListe(RectTransform parent, Vector2 hautGauche, float largeur, int nbLignes, float[] colonnes, string[] entetes)
        {
            var l = new ListeUI();
            l.cadre = Panneau("Liste", parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(hautGauche.x, -hautGauche.y), new Vector2(largeur, HauteurLigne * (nbLignes + 1)), new Color(1, 1, 1, .03f));
            for (int c = 0; c < entetes.Length; c++)
            {
                var t = Texte("Entete" + c, l.cadre, entetes[c], 15, Pale, TextAnchor.MiddleLeft);
                var r = (RectTransform)t.transform;
                r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
                r.anchoredPosition = new Vector2(colonnes[c] + 10, 0); r.sizeDelta = new Vector2(200, HauteurLigne);
            }
            for (int i = 0; i < nbLignes; i++)
            {
                var ligne = Panneau("Ligne" + i, l.cadre, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -HauteurLigne * (i + 1)), new Vector2(largeur, HauteurLigne - 2), new Color(1, 1, 1, 0));
                var cellules = new UnityEngine.UI.Text[colonnes.Length];
                for (int c = 0; c < colonnes.Length; c++)
                {
                    cellules[c] = Texte("C" + c, ligne, "", 17, Color.white, TextAnchor.MiddleLeft);
                    var r = (RectTransform)cellules[c].transform;
                    r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, .5f);
                    r.anchoredPosition = new Vector2(colonnes[c] + 10, 0);
                    r.sizeDelta = new Vector2((c + 1 < colonnes.Length ? colonnes[c + 1] - colonnes[c] : largeur - colonnes[c]) - 14, HauteurLigne);
                }
                l.lignes.Add((ligne, ligne.GetComponent<UnityEngine.UI.Image>(), cellules));
            }
            return l;
        }

        /// <summary>Remplit une liste avec des piles ; renvoie l'indice (dans les piles) de la ligne survolée, ou -1.</summary>
        int RemplirListe(ListeUI l, List<Pile> piles, int selection, string equipee, bool categorie, Vector2 souris, float molette)
        {
            l.nombre = piles.Count;
            bool dedans = RectTransformUtility.RectangleContainsScreenPoint(l.cadre, souris, null);
            if (dedans && molette != 0f) l.debut -= (int)Mathf.Sign(molette);
            l.debut = Mathf.Clamp(l.debut, 0, Mathf.Max(0, piles.Count - l.lignes.Count));
            if (selection >= 0 && selection < l.debut) l.debut = selection;
            if (selection >= l.debut + l.lignes.Count) l.debut = selection - l.lignes.Count + 1;
            int survole = -1;
            for (int i = 0; i < l.lignes.Count; i++)
            {
                int k = l.debut + i;
                var (rect, fond, cellules) = l.lignes[i];
                if (k >= piles.Count)
                {
                    fond.color = new Color(1, 1, 1, 0);
                    foreach (var c in cellules) c.text = "";
                    continue;
                }
                var p = piles[k];
                var d = Objets.Def(p.id);
                bool sur = dedans && RectTransformUtility.RectangleContainsScreenPoint(rect, souris, null);
                if (sur) survole = k;
                fond.color = k == selection ? new Color(1f, .82f, .45f, .22f) : sur ? new Color(1, 1, 1, .08f) : new Color(1, 1, 1, (k % 2) * .025f);
                string nom = d != null ? d.nom : p.id;
                if (p.n > 1) nom += $"  ×{p.n}";
                if (p.id == equipee) nom += "  <color=#ffd27a>[équipée]</color>";
                string couleur = d != null ? ColorUtility.ToHtmlStringRGB(d.couleur) : "ffffff";
                cellules[0].text = $"<color=#{couleur}>■</color>  {nom}";
                int c0 = 1;
                if (categorie) cellules[c0++].text = d != null ? Objets.NomCategorie(d.categorie) : "";
                cellules[c0++].text = d != null ? $"{d.poids * p.n:0.##} kg" : "";
                cellules[c0].text = d != null ? (Competences.Instance != null ? Competences.Instance.TexteValeur(d, p.n) : $"{d.valeur * p.n}") + " p." : "";
            }
            return survole;
        }

        // ---------- mise à jour ----------

        void MettreAJourInventaire()
        {
            ConstruireInventaire();
            var inv = Inventaire.Instance;
            var kb = Keyboard.current;
            var souris = Mouse.current;
            if (inv == null) return;

            var arme = inv.Arme;
            texteArme.text = arme == null ? "<color=#a9a6c4>Mains nues</color>   ·   I : inventaire"
                : inv.Degainee ? $"<b>{arme.nom}</b> en main   ·   H : ranger" : $"{arme.nom} rangée (mains nues)   ·   H : dégainer";

            // Coffre ouvert : sa fenêtre seulement
            if (Coffre.Ouvert != null)
            {
                if (!panneauCoffre.gameObject.activeSelf) OuvrirCoffre();
                MettreAJourCoffre(inv, kb, souris);
                return;
            }
            if (panneauCoffre.gameObject.activeSelf) FermerCoffre();

            bool basculer = kb != null && !ConsoleDebug.Ouverte && !Dialogues.EnCours && kb.iKey.wasPressedThisFrame;
            if (basculer && !statutOuvert && !carteOuverte && !competencesOuvertes) OuvrirInventaire(!inventaireOuvert);
            if (inventaireOuvert && kb != null && kb.escapeKey.wasPressedThisFrame) OuvrirInventaire(false);
            if (!inventaireOuvert) return;

            Vector2 pos = souris != null ? souris.position.ReadValue() : Vector2.zero;
            float molette = souris != null ? souris.scroll.ReadValue().y : 0f;
            if (kb != null && kb.downArrowKey.wasPressedThisFrame) selectionSac++;
            if (kb != null && kb.upArrowKey.wasPressedThisFrame) selectionSac--;
            selectionSac = Mathf.Clamp(selectionSac, 0, Mathf.Max(0, inv.sac.Count - 1));
            int survole = RemplirListe(listeSac, inv.sac, selectionSac, inv.ArmeId, true, pos, molette);
            if (survole >= 0 && souris != null && (souris.delta.ReadValue().sqrMagnitude > .1f || souris.leftButton.wasPressedThisFrame)) selectionSac = survole;

            double charge = inv.ChargeMax;
            float poids = inv.Poids;
            invSousTitre.text = $"Bourse : {inv.Argent} pièce{(inv.Argent > 1 ? "s" : "")}   ·   Charge : " +
                (poids > charge ? $"<color=#ff7a7a>{poids:0.#} / {Nombres.Court(charge)} kg (surchargé)</color>" : $"{poids:0.#} / {Nombres.Court(charge)} kg") +
                $"   ·   Arme : {(arme == null ? "aucune" : arme.nom + (inv.Degainee ? " (en main)" : " (rangée)"))}";

            if (inv.sac.Count == 0)
            {
                invDetail.text = "<color=#a9a6c4>Ton sac est vide.\n\nOuvre le coffre de ta maison (E), ramasse ce que les créatures lâchent.</color>";
                return;
            }
            var pile = inv.sac[selectionSac];
            var d = Objets.Def(pile.id);
            if (d == null) { invDetail.text = pile.id; return; }
            var sb = new System.Text.StringBuilder();
            sb.Append($"<size=26><b>{d.nom}</b></size>\n<color=#ffd27a>{Objets.NomCategorie(d.categorie)}</color>\n\n{d.description}\n\n");
            if (d.categorie == Categorie.Arme)
            {
                var s = StatsHeros.Instance;
                double f = s != null ? s.Facteur : 1;
                sb.Append($"Dégâts de l'arme  {Nombres.Court(d.degats * f)}  <size=14><color=#a9a6c4>({d.degats} × ta croissance)</color></size>\n");
                sb.Append($"Portée  {d.portee:0.#} m\nRecul  ×{d.poussee:0.##}\nCritique  {(d.critique >= 0 ? "+" : "")}{d.critique * 100:0} %\n\n");
            }
            if (d.categorie == Categorie.Nourriture) sb.Append($"Rend  {d.soin * 100:0} % de la vie\n\n");
            sb.Append($"Poids  {d.poids:0.##} kg" + (pile.n > 1 ? $"  (pile : {d.poids * pile.n:0.##} kg)" : "") + "\n");
            var ev = Competences.Instance;
            if (ev != null) ev.EvaluerObjet(pile.id);
            string val1 = ev != null ? ev.TexteValeur(d, 1) : d.valeur.ToString(), valPile = ev != null ? ev.TexteValeur(d, pile.n) : (d.valeur * pile.n).ToString();
            sb.Append($"Valeur estimée  {val1} pièce{(d.valeur > 1 ? "s" : "")}" + (pile.n > 1 ? $"  (pile : {valPile})" : "")
                + (ev != null && !ev.EvaluationExacte && d.categorie != Categorie.Monnaie ? $"  <size=14><color=#a9a6c4>Évaluation niv. {ev.NiveauEvaluation}</color></size>" : "") + "\n\n");
            string action = d.categorie == Categorie.Arme ? (pile.id == inv.ArmeId ? "retirer" : "équiper") : d.categorie == Categorie.Nourriture ? "manger" : "—";
            sb.Append($"<color=#a9a6c4>E / clic droit : {action}   ·   X : jeter</color>");
            invDetail.text = sb.ToString();

            bool clicDroit = souris != null && souris.rightButton.wasPressedThisFrame && survole >= 0;
            if (kb != null && (kb.eKey.wasPressedThisFrame || kb.uKey.wasPressedThisFrame) || clicDroit) inv.Utiliser(pile.id);
            else if (kb != null && kb.xKey.wasPressedThisFrame) inv.Jeter(pile.id, kb.shiftKey.isPressed ? pile.n : 1);
        }

        void MettreAJourCoffre(Inventaire inv, Keyboard kb, Mouse souris)
        {
            var coffre = Coffre.Ouvert;
            Vector2 pos = souris != null ? souris.position.ReadValue() : Vector2.zero;
            float molette = souris != null ? souris.scroll.ReadValue().y : 0f;
            coffreTitre.text = coffre.nom.ToUpper();
            int surCoffre = RemplirListe(listeCoffre, coffre.contenu, -1, null, false, pos, molette);
            int surSac = RemplirListe(listeSacCoffre, inv.sac, -1, inv.ArmeId, false, pos, molette);
            coffreBas.text = $"Clic : faire passer la pile de l'autre côté   ·   T : tout prendre   ·   E ou Échap : fermer   ·   Charge {inv.Poids:0.#} / {Nombres.Court(inv.ChargeMax)} kg   ·   {inv.Argent} pièces";
            bool clic = souris != null && souris.leftButton.wasPressedThisFrame;
            if (clic && surCoffre >= 0 && coffre.Prendre(surCoffre, out var p))
            {
                inv.Ajouter(p.id, p.n);
                var d = Objets.Def(p.id);
                if (d != null && d.categorie == Categorie.Arme && inv.Arme == null) inv.Equiper(p.id);
            }
            else if (clic && surSac >= 0)
            {
                var p2 = inv.sac[surSac];
                string id = p2.id; int n = p2.n;
                if (inv.Retirer(id, n)) { coffre.Deposer(id, n); Sons.Jouer("clic", .5f, .95f); }
            }
            if (kb != null && kb.tKey.wasPressedThisFrame)
            {
                while (coffre.contenu.Count > 0 && coffre.Prendre(0, out var q))
                {
                    inv.Ajouter(q.id, q.n);
                    var d = Objets.Def(q.id);
                    if (d != null && d.categorie == Categorie.Arme && inv.Arme == null) inv.Equiper(q.id);
                }
            }
            if (kb != null && Time.frameCount != cadreCoffre && (kb.escapeKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame)) FermerCoffre();
        }
    }
}
