using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Aldermark
{
    /// <summary>
    /// Les compétences à l'écran : la barre des 4 raccourcis en bas (sort, coût en mana, recharge),
    /// la barre d'incantation sous le réticule, et la fenêtre Compétences (K) :
    /// la liste (sorts puis techniques) avec niveau et progression, le détail de celle qu'on choisit,
    /// et les raccourcis où l'on range ses sorts (1 à 4, ou clic sur une case).
    /// </summary>
    public partial class HUD
    {
        class CaseRaccourci
        {
            public RectTransform fond, recharge;
            public UnityEngine.UI.Image teinte;
            public UnityEngine.UI.Text abrege, touche, cout, attente, niveau;
            public UnityEngine.UI.Outline contour;
        }

        class LigneCompetence
        {
            public RectTransform fond, xp;
            public UnityEngine.UI.Text nom, niveau;
            public string id;   // null : un titre de section
        }

        bool competencesConstruites, competencesOuvertes;
        // La jauge d'analyse sous le réticule
        RectTransform barreAnalyse, remplAnalyse;
        UnityEngine.UI.Text texteAnalyseSort;
        DefCompetence analyseDef;
        float analyseValeur, analyseAffichee, analyseFin;
        bool analyseEncaisse;

        /// <summary>La jauge d'analyse d'un sort vient de monter : elle s'affiche quelques secondes sous le réticule.</summary>
        public static void AnalyseSort(DefCompetence d, float valeur, bool encaisse)
        {
            if (instance == null || d == null) return;
            if (instance.analyseDef != d) instance.analyseAffichee = Mathf.Max(0f, valeur - .15f);
            instance.analyseDef = d; instance.analyseValeur = valeur; instance.analyseEncaisse = encaisse;
            instance.analyseFin = Time.unscaledTime + (valeur >= 1f ? 2.5f : 3.5f);
        }
        UnityEngine.UI.Image voileBords, voileEcran;
        Color voileVoulu, voileActuel;
        float assombriVoulu, assombriActuel;

        /// <summary>Une lueur colorée sur les bords de l'écran (l'énergie qui monte), à redemander à chaque image.</summary>
        public static void Voile(Color c) { if (instance != null && c.a > instance.voileVoulu.a) instance.voileVoulu = c; }

        /// <summary>Le jour qui s'assombrit (0 à 1), à redemander à chaque image.</summary>
        public static void Assombrir(float force) { if (instance != null) instance.assombriVoulu = Mathf.Max(instance.assombriVoulu, force); }
        RectTransform barreRaccourcis, panneauCompetences, barreIncantation, remplIncantation, remplXPCompetence;
        UnityEngine.UI.Text texteIncantation, compTitre, compSousTitre, compNom, compType, compDescription, compChiffres, compNiveau, compParNiveau, compBas;
        readonly CaseRaccourci[] casesJeu = new CaseRaccourci[Competences.Raccourcis];
        readonly CaseRaccourci[] casesFenetre = new CaseRaccourci[Competences.Raccourcis];
        readonly List<LigneCompetence> lignesComp = new List<LigneCompetence>();
        string compChoisie;
        KeyControl toucheK;

        public static bool CompetencesOuvertes => instance != null && instance.competencesOuvertes;

        // ---------- construction ----------

        void ConstruireCompetences()
        {
            if (competencesConstruites) return;
            competencesConstruites = true;

            // Les voiles d'incantation, derrière tout le reste de l'interface
            voileEcran = Panneau("Assombri", toile, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0)).GetComponent<UnityEngine.UI.Image>();
            voileEcran.rectTransform.offsetMin = voileEcran.rectTransform.offsetMax = Vector2.zero;
            voileBords = Panneau("VoileSort", toile, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0)).GetComponent<UnityEngine.UI.Image>();
            voileBords.rectTransform.offsetMin = voileBords.rectTransform.offsetMax = Vector2.zero;
            voileBords.sprite = Vignette();
            voileBords.transform.SetAsFirstSibling();
            voileEcran.transform.SetAsFirstSibling();

            // Les raccourcis, en bas au centre
            barreRaccourcis = Panneau("Raccourcis", toile, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 60), new Vector2(4 * 58 - 6, 52), new Color(0, 0, 0, 0));
            for (int k = 0; k < Competences.Raccourcis; k++)
                casesJeu[k] = CreerCase(barreRaccourcis, new Vector2(k * 58, 0), new Vector2(52, 52), k, false);

            // La barre d'incantation sous le réticule
            barreIncantation = Panneau("Incantation", toile, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -78), new Vector2(220, 6), new Color(0, 0, 0, .5f));
            remplIncantation = Panneau("Remplissage", barreIncantation, Vector2.zero, new Vector2(0, 1), new Vector2(0, .5f), Vector2.zero, Vector2.zero, Color.white);
            remplIncantation.offsetMin = remplIncantation.offsetMax = Vector2.zero;
            texteIncantation = Texte("Nom", barreIncantation, "", 16, Color.white, TextAnchor.LowerCenter);
            var rt = (RectTransform)texteIncantation.transform;
            rt.anchorMin = new Vector2(.5f, 1); rt.anchorMax = new Vector2(.5f, 1); rt.pivot = new Vector2(.5f, 0);
            rt.anchoredPosition = new Vector2(0, 4); rt.sizeDelta = new Vector2(300, 22);
            barreIncantation.gameObject.SetActive(false);

            // La jauge d'analyse, plus bas
            barreAnalyse = Panneau("Analyse", toile, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -128), new Vector2(300, 8), new Color(0, 0, 0, .55f));
            var ca = barreAnalyse.gameObject.AddComponent<UnityEngine.UI.Outline>();
            ca.effectColor = new Color(1f, 1f, 1f, .25f); ca.effectDistance = new Vector2(1, -1);
            remplAnalyse = Panneau("Remplissage", barreAnalyse, Vector2.zero, new Vector2(0, 1), new Vector2(0, .5f), Vector2.zero, Vector2.zero, Color.white);
            remplAnalyse.offsetMin = remplAnalyse.offsetMax = Vector2.zero;
            texteAnalyseSort = Texte("Texte", barreAnalyse, "", 16, Color.white, TextAnchor.LowerCenter);
            var rta = (RectTransform)texteAnalyseSort.transform;
            rta.anchorMin = new Vector2(.5f, 1); rta.anchorMax = new Vector2(.5f, 1); rta.pivot = new Vector2(.5f, 0);
            rta.anchoredPosition = new Vector2(0, 4); rta.sizeDelta = new Vector2(520, 22);
            barreAnalyse.gameObject.SetActive(false);

            // La fenêtre
            panneauCompetences = Panneau("Competences", toile, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 10), new Vector2(1240, 700), new Color(.055f, .047f, .12f, .94f));
            var cadre = Panneau("Cadre", panneauCompetences, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0));
            cadre.offsetMin = new Vector2(6, 6); cadre.offsetMax = new Vector2(-6, -6);
            var contour = cadre.gameObject.AddComponent<UnityEngine.UI.Outline>();
            contour.effectColor = new Color(.81f, .78f, 1f, .8f); contour.effectDistance = new Vector2(2, -2);

            compTitre = Haut(Texte("Titre", panneauCompetences, "COMPÉTENCES", 32, Color.white, TextAnchor.UpperLeft), 40, 28, 900, 44);
            compTitre.fontStyle = FontStyle.Bold;
            compSousTitre = Haut(Texte("SousTitre", panneauCompetences, "", 20, CouleurOr, TextAnchor.UpperLeft), 40, 74, 1100, 30);

            // À droite : le détail
            compNom = Haut(Texte("Nom", panneauCompetences, "", 30, Color.white, TextAnchor.UpperLeft), 580, 118, 620, 40);
            compNom.fontStyle = FontStyle.Bold;
            compType = Haut(Texte("Type", panneauCompetences, "", 18, Pale, TextAnchor.UpperLeft), 580, 160, 620, 26);
            compDescription = Haut(Texte("Description", panneauCompetences, "", 18, new Color(.92f, .9f, 1f), TextAnchor.UpperLeft), 580, 192, 620, 56);
            compDescription.horizontalOverflow = HorizontalWrapMode.Wrap;
            compChiffres = Haut(Texte("Chiffres", panneauCompetences, "", 16, new Color(.92f, .9f, 1f), TextAnchor.UpperLeft), 580, 252, 620, 190);
            compChiffres.lineSpacing = 1.05f;
            compChiffres.horizontalOverflow = HorizontalWrapMode.Wrap;
            compNiveau = Haut(Texte("Niveau", panneauCompetences, "", 20, Color.white, TextAnchor.UpperLeft), 580, 446, 620, 28);
            var fondXP = Panneau("BarreXP", panneauCompetences, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(580, -478), new Vector2(620, 8), new Color(1, 1, 1, .12f));
            remplXPCompetence = Panneau("Remplissage", fondXP, Vector2.zero, new Vector2(0, 1), new Vector2(0, .5f), Vector2.zero, Vector2.zero, CouleurOr);
            remplXPCompetence.offsetMin = remplXPCompetence.offsetMax = Vector2.zero;
            compParNiveau = Haut(Texte("ParNiveau", panneauCompetences, "", 15, new Color(.6f, .85f, .7f), TextAnchor.UpperLeft), 580, 492, 620, 22);

            // Les raccourcis de la fenêtre
            Haut(Texte("TitreRaccourcis", panneauCompetences, "RACCOURCIS", 16, Pale, TextAnchor.UpperLeft), 580, 530, 300, 22);
            for (int k = 0; k < Competences.Raccourcis; k++)
                casesFenetre[k] = CreerCase(panneauCompetences, Vector2.zero, new Vector2(146, 58), k, true);

            compBas = Texte("Bas", panneauCompetences, "", 18, Pale, TextAnchor.LowerLeft);
            var rb = (RectTransform)compBas.transform;
            rb.anchorMin = Vector2.zero; rb.anchorMax = new Vector2(1, 0); rb.pivot = new Vector2(.5f, 0);
            rb.offsetMin = new Vector2(40, 22); rb.offsetMax = new Vector2(-40, 56);
            Panneau("Separation", panneauCompetences, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(548, -118), new Vector2(1, 520), new Color(1, 1, 1, .08f));
            panneauCompetences.gameObject.SetActive(false);
        }

        CaseRaccourci CreerCase(Transform parent, Vector2 pos, Vector2 taille, int k, bool large)
        {
            var c = new CaseRaccourci();
            c.fond = Panneau("Case" + k, parent, large ? new Vector2(0, 1) : Vector2.zero, large ? new Vector2(0, 1) : Vector2.zero, large ? new Vector2(0, 1) : Vector2.zero,
                large ? new Vector2(580 + k * 156, -558) : pos, taille, new Color(.086f, .07f, .15f, .78f));
            var o = c.fond.gameObject.AddComponent<UnityEngine.UI.Outline>();
            o.effectColor = new Color(1f, 1f, 1f, .18f); o.effectDistance = new Vector2(1, -1);
            c.contour = o;
            var t = Panneau("Teinte", c.fond, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0));
            t.offsetMin = new Vector2(3, 3); t.offsetMax = new Vector2(-3, -3);
            c.teinte = t.GetComponent<UnityEngine.UI.Image>();
            c.recharge = Panneau("Recharge", c.fond, Vector2.zero, new Vector2(1, 0), new Vector2(.5f, 0), Vector2.zero, Vector2.zero, new Color(0, 0, 0, .62f));
            c.recharge.offsetMin = Vector2.zero; c.recharge.offsetMax = Vector2.zero;
            c.abrege = Texte("Abrege", c.fond, "", large ? 18 : 22, Color.white, large ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
            c.abrege.fontStyle = FontStyle.Bold;
            Etirer((RectTransform)c.abrege.transform, large ? new Vector2(30, 0) : Vector2.zero, large ? new Vector2(-6, 0) : Vector2.zero);
            c.touche = Texte("Touche", c.fond, (k + 1).ToString(), 13, new Color(1f, 1f, 1f, .75f), TextAnchor.UpperLeft);
            Etirer((RectTransform)c.touche.transform, new Vector2(5, 2), new Vector2(-2, -2));
            c.cout = Texte("Cout", c.fond, "", 12, CouleurManaRPG, TextAnchor.LowerRight);
            Etirer((RectTransform)c.cout.transform, new Vector2(2, 2), new Vector2(-4, -1));
            c.attente = Texte("Attente", c.fond, "", 16, Color.white, TextAnchor.MiddleCenter);
            Etirer((RectTransform)c.attente.transform, Vector2.zero, Vector2.zero);
            c.niveau = Texte("Niveau", c.fond, "", 12, CouleurOr, TextAnchor.LowerLeft);
            Etirer((RectTransform)c.niveau.transform, new Vector2(5, 1), new Vector2(-2, -1));
            return c;
        }

        // ---------- mise à jour ----------

        void MettreAJourCompetences(Keyboard kb, bool autreMenu)
        {
            ConstruireCompetences();
            var comp = Competences.Instance;
            var h = CombatHeros.Instance;

            // Les voiles : montent vite, retombent doucement
            float dtv = Time.unscaledDeltaTime;
            voileActuel = Color.Lerp(voileActuel, voileVoulu, 1f - Mathf.Exp(-dtv * (voileVoulu.a > voileActuel.a ? 10f : 3f)));
            assombriActuel = Mathf.Lerp(assombriActuel, assombriVoulu, 1f - Mathf.Exp(-dtv * (assombriVoulu > assombriActuel ? 4f : 2.5f)));
            voileBords.color = voileActuel;
            voileEcran.color = new Color(.02f, .015f, .05f, assombriActuel * .55f);
            voileVoulu = new Color(voileVoulu.r, voileVoulu.g, voileVoulu.b, 0f);
            assombriVoulu = 0f;

            // La barre des raccourcis : seulement quand on connaît au moins un sort
            bool barre = comp != null && comp.ConnaitUnSort && !Dialogues.EnCours && !competencesOuvertes;
            if (barreRaccourcis.gameObject.activeSelf != barre) barreRaccourcis.gameObject.SetActive(barre);
            if (barre) for (int k = 0; k < Competences.Raccourcis; k++) RemplirCase(casesJeu[k], comp, k, false);

            // La jauge d'analyse
            bool analyse = analyseDef != null && Time.unscaledTime < analyseFin;
            if (barreAnalyse.gameObject.activeSelf != analyse) barreAnalyse.gameObject.SetActive(analyse);
            if (analyse)
            {
                analyseAffichee = Mathf.MoveTowards(analyseAffichee, analyseValeur, Time.unscaledDeltaTime * .8f);
                remplAnalyse.anchorMax = new Vector2(analyseAffichee, 1);
                Color ce = analyseDef.couleur;
                remplAnalyse.GetComponent<UnityEngine.UI.Image>().color = Color.Lerp(ce, Color.white, .5f + .5f * Mathf.Sin(Time.unscaledTime * 8f) * (analyseValeur >= 1f ? 1f : 0f));
                string nomC = $"<color=#{ColorUtility.ToHtmlStringRGB(Color.Lerp(ce, Color.white, .35f))}>{analyseDef.nom}</color>";
                texteAnalyseSort.text = analyseValeur >= 1f ? $"<b>COMPRIS :</b> {nomC}"
                    : $"<color=#a9a6c4>ANALYSE</color>  {nomC}  <b>{analyseAffichee * 100f:0} %</b>" + (analyseEncaisse ? "  <size=13><color=#ffb08a>encaissé ×2</color></size>" : "");
            }

            // L'incantation en cours
            var sort = h != null ? h.SortEnCours : null;
            if (barreIncantation.gameObject.activeSelf != (sort != null)) barreIncantation.gameObject.SetActive(sort != null);
            if (sort != null)
            {
                remplIncantation.anchorMax = new Vector2(h.ProgresIncantation, 1);
                remplIncantation.GetComponent<UnityEngine.UI.Image>().color = sort.couleur;
                texteIncantation.text = sort.nom;
                texteIncantation.color = Color.Lerp(sort.couleur, Color.white, .4f);
            }

            // La fenêtre : K pour ouvrir ou fermer, Échap pour fermer
            if (kb == null || comp == null) return;
            if (toucheK == null) toucheK = kb.FindKeyOnCurrentKeyboardLayout("k") ?? kb.kKey;
            if (!autreMenu && toucheK.wasPressedThisFrame) OuvrirCompetences(!competencesOuvertes);
            else if (competencesOuvertes && kb.escapeKey.wasPressedThisFrame) { if (ModaleOuverte) FermerModale(); else if (TitresOuverts) FermerTitres(); else OuvrirCompetences(false); }
            if (competencesOuvertes) MettreAJourFenetreCompetences(comp, kb);
        }

        void RemplirCase(CaseRaccourci c, Competences comp, int k, bool large)
        {
            var d = CatalogueCompetences.Get(comp.Raccourci(k));
            var joueurOk = joueur != null;
            if (d == null)
            {
                c.teinte.color = new Color(0, 0, 0, 0);
                c.abrege.text = large ? "<color=#8a87a6>vide</color>" : "";
                c.cout.text = ""; c.attente.text = ""; c.niveau.text = "";
                c.contour.effectColor = new Color(1f, 1f, 1f, .18f); c.contour.effectDistance = new Vector2(1, -1);
                c.recharge.anchorMax = new Vector2(1, 0);
                return;
            }
            float attente = comp.Attente(d.id), recharge = Mathf.Max(.01f, comp.Recharge(d));
            float cout = comp.Cout(d);
            bool mana = !joueurOk || joueur.Mana >= cout;
            Color e = d.couleur;
            c.teinte.color = new Color(e.r, e.g, e.b, mana ? .32f : .1f);
            string couleurNom = ColorUtility.ToHtmlStringRGB(mana ? Color.Lerp(e, Color.white, .55f) : new Color(.55f, .55f, .6f));
            int n = Mathf.Max(1, comp.Niveau(d.id));
            int pal = Sorts.Palier(comp.NiveauVisuel(d.id));
            c.abrege.text = large ? $"<color=#{couleurNom}>{d.nom}</color>" : $"<color=#{couleurNom}>{d.abrege}</color>";
            c.niveau.text = large ? "" : n.ToString();
            if (large) c.abrege.text += $"  <size=13><color=#ffd27a>niv. {n}</color></size>";
            // Le cadre de la case dit le palier : blanc discret, puis la couleur du sort, puis l'or du sort maîtrisé
            c.contour.effectColor = pal >= 5 ? Color.Lerp(new Color(1f, .82f, .45f, 1f), Color.white, .5f + .5f * Mathf.Sin(Time.unscaledTime * 4f))
                : pal >= 4 ? new Color(1f, .82f, .45f, 1f) : pal >= 2 ? new Color(e.r, e.g, e.b, pal >= 3 ? .9f : .55f) : new Color(1f, 1f, 1f, .18f);
            c.contour.effectDistance = pal >= 5 ? new Vector2(3, -3) : pal >= 3 ? new Vector2(2, -2) : new Vector2(1, -1);
            c.cout.text = Mathf.CeilToInt(cout).ToString();
            c.cout.color = mana ? CouleurManaRPG : new Color(1f, .45f, .45f);
            c.recharge.anchorMax = new Vector2(1, Mathf.Clamp01(attente / recharge));
            c.attente.text = attente > 0f && !large ? (attente >= 10f ? attente.ToString("0") : attente.ToString("0.0")) : "";
        }

        void OuvrirCompetences(bool ouvrir)
        {
            if (!ouvrir) { FermerModale(); FermerTitres(); }
            competencesOuvertes = ouvrir;
            panneauCompetences.gameObject.SetActive(ouvrir);
            Cursor.lockState = ouvrir ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = ouvrir;
            Sons.Jouer("clic", .5f, ouvrir ? 1.3f : .9f);
            if (ouvrir && CatalogueCompetences.Get(compChoisie) == null && Competences.Instance != null && Competences.Instance.Connues.Count > 0)
                compChoisie = Competences.Instance.Connues[0].id;
        }

        void ReconstruireLignes(Competences comp)
        {
            // Sorts, puis techniques, chacun sous son titre
            var voulu = new List<string>();
            bool sorts = false;
            foreach (var m in comp.Connues) if (m.Def.EstSort) { if (!sorts) { voulu.Add(null); sorts = true; } voulu.Add(m.id); }
            if (!sorts) voulu.Add(null);   // le titre « Sorts » et le message « aucun sort »
            voulu.Add("");
            foreach (var m in comp.Connues) if (!m.Def.EstSort) voulu.Add(m.id);
            bool pareil = voulu.Count == lignesComp.Count;
            for (int i = 0; pareil && i < voulu.Count; i++) if (lignesComp[i].id != voulu[i]) pareil = false;
            if (pareil) return;

            foreach (var l in lignesComp) Destroy(l.fond.gameObject);
            lignesComp.Clear();
            float y = 118;
            int nLignes = 0;
            foreach (var id0 in voulu) if (!string.IsNullOrEmpty(id0)) nLignes++;
            float hLigne = Mathf.Clamp((512f - 78f) / Mathf.Max(1, nLignes), 24f, 38f);
            for (int i = 0; i < voulu.Count; i++)
            {
                string id = voulu[i];
                var l = new LigneCompetence { id = id };
                bool titre = string.IsNullOrEmpty(id);
                float hauteur = titre ? 34 : hLigne;
                if (titre && i > 0) y += 10;
                l.fond = Panneau("Ligne", panneauCompetences, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -y), new Vector2(480, hauteur - 4), new Color(1, 1, 1, titre ? 0f : .04f));
                l.nom = Texte("Nom", l.fond, "", titre ? 16 : hLigne >= 34f ? 20 : 17, titre ? Pale : Color.white, TextAnchor.MiddleLeft);
                Etirer((RectTransform)l.nom.transform, new Vector2(titre ? 2 : 14, 0), new Vector2(-90, 0));
                if (titre)
                {
                    l.nom.text = id == null ? (sorts ? "SORTS" : "SORTS   <size=14><color=#8a87a6>aucun pour l'instant : on les apprend en regardant d'autres les lancer</color></size>") : "TECHNIQUES";
                }
                else
                {
                    l.niveau = Texte("Niveau", l.fond, "", 17, CouleurOr, TextAnchor.MiddleRight);
                    Etirer((RectTransform)l.niveau.transform, new Vector2(300, 0), new Vector2(-12, 0));
                    var fx = Panneau("XP", l.fond, Vector2.zero, new Vector2(1, 0), new Vector2(.5f, 0), Vector2.zero, new Vector2(0, 3), new Color(1, 1, 1, .08f));
                    fx.offsetMin = new Vector2(14, 2); fx.offsetMax = new Vector2(-12, 5);
                    l.xp = Panneau("Remplissage", fx, Vector2.zero, new Vector2(0, 1), new Vector2(0, .5f), Vector2.zero, Vector2.zero, CouleurOr);
                    l.xp.offsetMin = l.xp.offsetMax = Vector2.zero;
                }
                lignesComp.Add(l);
                y += hauteur;
            }
        }

        void MettreAJourFenetreCompetences(Competences comp, Keyboard kb)
        {
            ReconstruireLignes(comp);
            var souris = Mouse.current;
            Vector2 pos = souris != null ? souris.position.ReadValue() : Vector2.zero;
            bool clic = souris != null && souris.leftButton.wasPressedThisFrame;
            // L'écran d'évolution / de fusion, par-dessus : il prend la main
            if (TitresOuverts) { MettreAJourTitres(pos, clic); return; }
            if (ModaleOuverte) { MettreAJourModale(comp, pos, clic); return; }
            if (MettreAJourBoutonTitres(pos, clic)) return;
            if (MettreAJourBoutonsEvolution(comp, CatalogueCompetences.Get(compChoisie), pos, clic)) return;

            // Choisir : clic ou flèches
            var ids = new List<string>();
            foreach (var l in lignesComp) if (!string.IsNullOrEmpty(l.id)) ids.Add(l.id);
            int idx = ids.IndexOf(compChoisie);
            if (idx < 0 && ids.Count > 0) { idx = 0; compChoisie = ids[0]; }
            if (kb.downArrowKey.wasPressedThisFrame && ids.Count > 0) { compChoisie = ids[(idx + 1) % ids.Count]; Sons.Jouer("clic", .3f, 1.5f); }
            if (kb.upArrowKey.wasPressedThisFrame && ids.Count > 0) { compChoisie = ids[(idx - 1 + ids.Count) % ids.Count]; Sons.Jouer("clic", .3f, 1.5f); }
            foreach (var l in lignesComp)
            {
                if (string.IsNullOrEmpty(l.id)) continue;
                if (clic && RectTransformUtility.RectangleContainsScreenPoint(l.fond, pos, null)) { compChoisie = l.id; Sons.Jouer("clic", .35f, 1.4f); }
                var m = comp.Get(l.id);
                var d = m.Def;
                bool choisie = l.id == compChoisie;
                l.fond.GetComponent<UnityEngine.UI.Image>().color = choisie ? new Color(d.couleur.r, d.couleur.g, d.couleur.b, .22f) : new Color(1, 1, 1, .04f);
                int touche = comp.Touche(l.id);
                l.nom.text = $"<color=#{ColorUtility.ToHtmlStringRGB(Color.Lerp(d.couleur, Color.white, .5f))}>{d.nom}</color>" + (touche > 0 ? $"   <size=14><color=#a9a6c4>[{touche}]</color></size>" : "");
                l.niveau.text = $"Niv. {m.niveau}";
                l.xp.anchorMax = new Vector2(Mathf.Clamp01((float)(m.xp / Competences.XPRequise(m.niveau))), 1);
            }

            // Ranger un sort : 1 à 4, clic sur une case ; Suppr le retire
            var choisieDef = CatalogueCompetences.Get(compChoisie);
            var touches = new[] { kb.digit1Key, kb.digit2Key, kb.digit3Key, kb.digit4Key };
            for (int k = 0; k < Competences.Raccourcis; k++)
            {
                bool sur = clic && RectTransformUtility.RectangleContainsScreenPoint(casesFenetre[k].fond, pos, null);
                if (!touches[k].wasPressedThisFrame && !sur) continue;
                if (choisieDef == null) continue;
                if (!choisieDef.EstSort) { Toast("Seuls les sorts vont dans les raccourcis"); continue; }
                comp.Placer(k, choisieDef.id);
                Sons.Jouer("clic", .6f, 1.2f);
            }
            if (choisieDef != null && choisieDef.EstSort && (kb.deleteKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame))
            {
                int t = comp.Touche(choisieDef.id);
                if (t > 0) { comp.Placer(t - 1, null); Sons.Jouer("clic", .5f, .8f); }
            }
            for (int k = 0; k < Competences.Raccourcis; k++) RemplirCase(casesFenetre[k], comp, k, true);

            int nSorts = 0, nTech = 0;
            foreach (var m in comp.Connues) { if (m.Def.EstSort) nSorts++; else nTech++; }
            int nAnalyses = 0;
            foreach (var kv in comp.Analyses) nAnalyses++;
            compSousTitre.text = $"{nSorts} sort{(nSorts > 1 ? "s" : "")}  ·  {nTech} techniques" + (nAnalyses > 0 ? $"  ·  {nAnalyses} sort{(nAnalyses > 1 ? "s" : "")} en cours d'analyse" : "") + "  ·  elles montent en s'en servant, sans plafond";
            compBas.text = "Clic ou ↑ ↓ : choisir   ·   1 à 4 ou clic sur une case : placer le sort   ·   Suppr : le retirer   ·   K ou Échap : fermer";
            Detail(comp, choisieDef);
        }

        void Detail(Competences comp, DefCompetence d)
        {
            if (d == null) { compNom.text = compType.text = compDescription.text = compChiffres.text = compNiveau.text = compParNiveau.text = ""; return; }
            var m = comp.Get(d.id);
            int n = m != null ? m.niveau : 1;
            compNom.text = $"<color=#{ColorUtility.ToHtmlStringRGB(Color.Lerp(d.couleur, Color.white, .35f))}>{d.nom}</color>";
            compDescription.text = d.description;
            var sb = new System.Text.StringBuilder();
            if (d.EstSort)
            {
                string origine = d.EstFusion ? $"fusion {CatalogueCompetences.NomElement(d.fusionDe[0])} + {CatalogueCompetences.NomElement(d.fusionDe[1])}"
                    : d.EstEvolution ? $"évolution de {CatalogueCompetences.Get(d.evolutionDe)?.nom}" : CatalogueCompetences.NomElement(d.element);
                compType.text = $"Sort  ·  {origine}  ·  {NomForme(d)}";
                if (d.forme == FormeSort.Soin) sb.AppendLine($"Soin  <b>{Nombres.Court(comp.Soin(d))}</b> vie" + (d.regen > 0 ? $"  + <b>{Nombres.Court(comp.Soin(d) * d.regen)}</b> sur 8 s" : "") + "");
                else if (d.forme == FormeSort.Sanctuaire && d.element == Element.Lumiere)
                    sb.AppendLine($"Soin  <b>{Nombres.Court(comp.Soin(d) * .12)}</b> et dégâts  <b>{Nombres.Court(comp.AttaqueMagique(d))}</b> toutes les ½ s, {d.duree:0} s");
                else if (d.forme == FormeSort.Tornade || d.forme == FormeSort.Sanctuaire)
                    sb.AppendLine($"Puissance  <b>{Nombres.Court(comp.AttaqueMagique(d))}</b> par coup, pendant {d.duree:0.#} s" + (d.eruption > 0 ? $"  ·  éruption <b>{Nombres.Court(comp.AttaqueMagique(d) * d.eruption / d.puissance)}</b>" : ""));
                else sb.AppendLine($"Puissance  <b>{Nombres.Court(comp.AttaqueMagique(d))}</b>" + (d.nombre > 1 ? $" × {d.nombre}" : ""));
                sb.AppendLine($"Mana  <color=#7ab4ff><b>{comp.Cout(d):0.#}</b></color>      Recharge  <b>{comp.Recharge(d):0.#} s</b>      Incantation  <b>{d.incantation:0.##} s</b>");
                if (d.forme == FormeSort.Cone) sb.AppendLine($"Portée  {d.portee:0} m, sur {d.angle:0}°");
                else if (d.forme == FormeSort.Zone && !d.aDistance) sb.AppendLine($"Rayon  {comp.Rayon(d):0.#} m");
                else if (d.rayon > 0 && d.forme != FormeSort.Soin) sb.AppendLine($"Portée  {d.portee:0} m  ·  rayon  {d.rayon:0.#} m");
                else if (d.forme != FormeSort.Soin) sb.AppendLine($"Portée  {d.portee:0} m");
                if (d.effet != Effet.Aucun)
                {
                    string chance = d.chanceEffet < 1f ? $"{d.chanceEffet * 100f:0} % de chances : " : "";
                    string quoi = d.effet == Effet.Brulure ? $"brûle pendant {d.dureeEffet:0} s" : d.effet == Effet.Gel ? $"ralentit {d.dureeEffet:0} s ; 3 de suite : gelé net" :
                                  d.effet == Effet.GelNet ? $"gèle net {d.dureeEffet:0.#} s" : d.effet == Effet.Choc ? $"étourdit {d.dureeEffet:0.#} s" : "repousse fort";
                    sb.AppendLine($"Effet  {CatalogueCompetences.NomEffet(d.effet)} : {chance}{quoi}");
                }
                // Les évolutions possibles
                if (d.evolutions != null && d.evolutions.Length == 2)
                {
                    string a0 = CatalogueCompetences.Get(d.evolutions[0])?.nom, a1 = CatalogueCompetences.Get(d.evolutions[1])?.nom;
                    sb.AppendLine(comp.PeutEvoluer(d.id) ? $"<color=#ffd27a>Peut évoluer : {a0} ou {a1} (bouton Évoluer)</color>" : $"<color=#a9a6c4>Au niveau 10, évolue en {a0} ou {a1}</color>");
                }
                // L'aspect : ce que le niveau a déjà changé, et le prochain palier
                {
                    // L'aspect : le niveau du sort, plus 25 par niveau de héros
                    int bonus = comp.BonusAspect;
                    int pal = Sorts.Palier(n + bonus);
                    string nomPal = Sorts.NomPalier(pal);
                    if (pal >= 2) sb.AppendLine($"Aspect{(nomPal != "" ? $" ({nomPal})" : "")}  <color=#a9e0ff>{Sorts.DescriptionPalier(d, pal)}</color>");
                    int prochain = Sorts.DebutPalier(pal + 1);
                    if (prochain > 0) sb.AppendLine($"<color=#a9a6c4>Au niveau {Mathf.Max(n + 1, prochain - bonus)} : {Sorts.DescriptionPalier(d, pal + 1)}</color>");
                    if (bonus > 0) sb.AppendLine($"<size=14><color=#a9a6c4>Ton niveau de héros ajoute {bonus} niveaux à l'aspect de tes sorts</color></size>");
                }
                int t = comp.Touche(d.id);
                sb.AppendLine(t > 0 ? $"Raccourci  <b>{t}</b>" : "<color=#ffd27a>Pas encore dans un raccourci : appuie sur 1, 2, 3 ou 4</color>");
            }
            else
            {
                compType.text = "Technique de combat";
                double p = 0;
                string quoi = "";
                switch (d.id)
                {
                    case "epee": p = .06 * (n - 1); quoi = "dégâts à l'épée"; break;
                    case "masse": p = .06 * (n - 1); quoi = "dégâts à la masse"; break;
                    case "mains": p = .07 * (n - 1); quoi = "dégâts à mains nues"; break;
                    case "parade": p = comp.FacteurParade - 1f; quoi = "fenêtre de parade parfaite"; break;
                    case "esquive": p = comp.FacteurEsquive - 1f; quoi = "invulnérabilité de la roulade"; break;
                    case "analyse": p = .1 * (n - 1); quoi = "vitesse d'analyse"; break;
                }
                sb.AppendLine($"Bonus actuel  <b>+{p * 100:0} %</b> {quoi}");
                if (d.id == "parade") sb.AppendLine($"Coût de la garde  <b>−{(1f - comp.FacteurCoutGarde) * 100f:0} %</b>");
                if (d.id == "esquive") sb.AppendLine($"Coût de la roulade  <b>−{(1f - comp.FacteurCoutRoulade) * 100f:0} %</b>");
                if (d.id == "analyse")
                {
                    var enCours = new System.Text.StringBuilder();
                    foreach (var kv in comp.Analyses)
                    {
                        var ds = CatalogueCompetences.Get(kv.Key);
                        if (ds == null) continue;
                        if (enCours.Length > 0) enCours.Append("  ·  ");
                        enCours.Append($"<color=#{ColorUtility.ToHtmlStringRGB(Color.Lerp(ds.couleur, Color.white, .4f))}>{ds.nom}</color> {kv.Value * 100f:0} %");
                    }
                    sb.AppendLine(enCours.Length > 0 ? $"En cours  {enCours}" : "<color=#a9a6c4>Aucun sort en cours d'analyse : regarde une créature lancer un sort que tu ne connais pas.</color>");
                    var res = new System.Text.StringBuilder();
                    foreach (var kv in comp.Resistances)
                    {
                        if (kv.Value <= 0f) continue;
                        if (res.Length > 0) res.Append("  ·  ");
                        res.Append($"{CatalogueCompetences.NomElement(kv.Key)} {kv.Value * 100f:0} %");
                    }
                    sb.AppendLine(res.Length > 0 ? $"Résistances  {res}" : "<color=#a9a6c4>Résistances : aucune encore (elles viennent en encaissant des sorts, jusqu'à 50 %)</color>");
                }
            }
            compChiffres.text = sb.ToString();
            int palAspect = d.EstSort ? Sorts.Palier(comp.NiveauVisuel(d.id)) : 1;
            string titre = palAspect >= 5 ? $"  ·  {Sorts.NomPalier(palAspect)}" : n >= CatalogueCompetences.NiveauMaitrise ? "  ·  maîtrisée" : "";
            compNiveau.text = $"<b>Niveau {n}</b>{titre}   <size=16><color=#a9a6c4>{Nombres.Court(m != null ? m.xp : 0)} / {Nombres.Court(Competences.XPRequise(n))}</color></size>";
            remplXPCompetence.anchorMax = new Vector2(Mathf.Clamp01((float)((m != null ? m.xp : 0) / Competences.XPRequise(n))), 1);
            compParNiveau.text = "Chaque niveau : " + d.parNiveau;
        }
    }
}
