using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Aldermark
{
    /// <summary>
    /// Interface discrète façon Daisi : boussole, mana, barre d'aide,
    /// fenêtre « Système » et étiquettes de la vue du Sceau.
    /// Tout est construit en code (uGUI).
    /// </summary>
    public class HUD : MonoBehaviour
    {
        public JoueurAldric joueur;
        public Camera cam;

        static HUD instance;
        Font police;
        RectTransform toile, bandeBoussole, remplissageMana, fenetre;
        UnityEngine.UI.Text texteMana, titreFenetre, corpsFenetre;
        UnityEngine.UI.Text texteQuete, texteEtat, texteInvite, texteAnalyse, nomDialogue, corpsDialogue, choixDialogue, texteJournal;
        GameObject panneauDialogue, panneauJournal;
        string queteAffichee;
        float flashQuete;

        // Dialogue : machine à écrire
        string texteComplet = "", choixComplet = "";
        float lettres, hauteurVoix = 1f;
        int derniereLettre;
        public static bool TexteFini => instance == null || instance.lettres >= instance.texteComplet.Length;
        RectTransform barreHaut, barreBas;

        // Sceau : anneau d'analyse et réticule
        UnityEngine.UI.Image anneau, anneauFond, point;
        float progressionAnalyse = -1f, impulsion;

        // Noms au-dessus des personnages et repère de quête
        class NomPersonnage { public RectTransform rect; public UnityEngine.UI.Text texte; }
        readonly Dictionary<Interlocuteur, NomPersonnage> noms = new Dictionary<Interlocuteur, NomPersonnage>();
        RectTransform reperequete;
        UnityEngine.UI.Text texteRepere;
        CanvasGroup groupeFenetre;
        float minuteurFenetre;
        readonly List<(EtiquetteSceau source, RectTransform rect, UnityEngine.UI.Text texte)> etiquettes = new List<(EtiquetteSceau, RectTransform, UnityEngine.UI.Text)>();
        const float PixelsParDegre = 4f;
        static readonly Color Verre = new Color(.086f, .07f, .15f, .65f);

        public static void Message(string titre, string corps, float duree)
        {
            if (instance == null) return;
            instance.titreFenetre.text = titre;
            instance.corpsFenetre.text = corps;
            instance.minuteurFenetre = duree;
        }

        public static void Invite(string texte)
        {
            if (instance == null) return;
            instance.texteInvite.text = texte ?? "";
        }

        public static void Analyse(float progression)
        {
            if (instance == null) return;
            instance.progressionAnalyse = progression;
            instance.texteAnalyse.text = progression < 0 ? "" : "Analyse";
        }

        /// <summary>Onde de l'anneau au moment d'une découverte.</summary>
        public static void Impulsion() { if (instance != null) instance.impulsion = 1f; }

        public static void AfficherDialogue(string qui, string texte, List<string> choix, float hauteurVoix = 1f)
        {
            if (instance == null) return;
            instance.panneauDialogue.SetActive(true);
            instance.nomDialogue.text = qui;
            instance.texteComplet = texte;
            instance.lettres = 0;
            instance.derniereLettre = 0;
            instance.hauteurVoix = hauteurVoix;
            instance.corpsDialogue.text = "";
            var lignes = new System.Text.StringBuilder();
            for (int i = 0; i < choix.Count; i++) lignes.AppendLine($"[{i + 1}]  {choix[i]}");
            instance.choixComplet = lignes.ToString();
            instance.choixDialogue.text = "";
        }

        public static void FinirTexte() { if (instance != null) instance.lettres = instance.texteComplet.Length; }

        public static void FermerDialogue()
        {
            if (instance == null) return;
            instance.panneauDialogue.SetActive(false);
        }

        void Awake()
        {
            instance = this;
            police = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var goC = new GameObject("ToileHUD");
            var canvas = goC.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = goC.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            toile = (RectTransform)goC.transform;

            ConstruireBoussole();
            ConstruireMana();
            ConstruireAide();
            ConstruireFenetre();
            ConstruireJeu();
        }

        void Start()
        {
            foreach (var e in Object.FindObjectsByType<EtiquetteSceau>(FindObjectsSortMode.None))
            {
                var rect = Panneau("Etiquette", toile, new Vector2(0, 0), new Vector2(0, 0), new Vector2(.5f, 0), Vector2.zero, new Vector2(10, 34), Verre);
                var barre = Panneau("Barre", rect, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, .5f), Vector2.zero, new Vector2(5, 0), e.couleur);
                barre.sizeDelta = new Vector2(5, 0);
                var t = Texte("Texte", rect, e.texte, 20, Color.white, TextAnchor.MiddleLeft);
                Etirer((RectTransform)t.transform, new Vector2(14, 0), new Vector2(-10, 0));
                rect.sizeDelta = new Vector2(t.preferredWidth + 26, 34);
                rect.gameObject.SetActive(false);
                etiquettes.Add((e, rect, t));
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Vector2 taille = toile.rect.size;

            // Boussole
            if (cam != null)
            {
                float cap = cam.transform.eulerAngles.y;
                bandeBoussole.anchoredPosition = new Vector2(-cap * PixelsParDegre, 0);
            }

            // Mana
            if (joueur != null)
            {
                float f = joueur.Mana / joueur.manaMax;
                remplissageMana.anchorMax = new Vector2(f, 1);
                texteMana.text = $"Mana {Mathf.FloorToInt(joueur.Mana)} / {joueur.manaMax:0}";
            }

            // Dialogue : texte lettre par lettre, petite voix, bandes de cinéma
            if (panneauDialogue.activeSelf)
            {
                if (lettres < texteComplet.Length)
                {
                    lettres = Mathf.Min(texteComplet.Length, lettres + dt * 48f);
                    int n = (int)lettres;
                    for (int k = derniereLettre; k < n; k++)
                        if (k % 2 == 0 && char.IsLetter(texteComplet[k])) Sons.Voix(hauteurVoix);
                    derniereLettre = n;
                    corpsDialogue.text = texteComplet.Substring(0, n);
                }
                else { corpsDialogue.text = texteComplet; choixDialogue.text = choixComplet; }
            }
            float hBarres = Mathf.MoveTowards(barreHaut.sizeDelta.y, Dialogues.EnCours ? 90f : 0f, dt * 400f);
            barreHaut.sizeDelta = new Vector2(0, hBarres);
            barreBas.sizeDelta = new Vector2(0, hBarres);

            // Réticule et anneau d'analyse du Sceau
            bool sceau = joueur != null && joueur.SceauNiveau > .5f && !Dialogues.EnCours;
            point.enabled = sceau;
            anneau.enabled = anneauFond.enabled = progressionAnalyse >= 0 || impulsion > 0;
            anneau.fillAmount = progressionAnalyse >= 0 ? Mathf.Clamp01(progressionAnalyse) : 1f;
            if (impulsion > 0) impulsion = Mathf.Max(0, impulsion - dt * 1.6f);
            float echelle = 1f + (1f - impulsion) * (impulsion > 0 ? 1.2f : 0f);
            anneau.rectTransform.localScale = Vector3.one * (impulsion > 0 ? echelle : 1f);
            anneau.color = new Color(.75f, .9f, 1f, impulsion > 0 ? impulsion : 1f);

            // Noms au-dessus des personnages et repère de la quête
            MettreAJourNoms(taille);

            // Quête, Éveil, confiance, journal
            if (Progression.Quete != queteAffichee)
            {
                if (queteAffichee != null) Sons.Jouer("quete", .6f);
                queteAffichee = Progression.Quete; texteQuete.text = queteAffichee; flashQuete = 1.5f;
            }
            if (flashQuete > 0) flashQuete -= dt;
            texteQuete.color = Color.Lerp(Color.white, new Color(1f, .82f, .45f), Mathf.Clamp01(flashQuete));
            texteEtat.text = $"Éveil du Sceau {Progression.Eveil:0.#} %   ·   Village : {Progression.NomConfiance}";
            var kb = Keyboard.current;
            if (kb != null && kb.jKey.wasPressedThisFrame && !Dialogues.EnCours) panneauJournal.SetActive(!panneauJournal.activeSelf);
            if (panneauJournal.activeSelf)
            {
                var j = new System.Text.StringBuilder();
                j.AppendLine("QUÊTE EN COURS").AppendLine(Progression.Quete).AppendLine();
                j.AppendLine($"Éveil du Sceau : {Progression.Eveil:0.#} %   ·   Village : {Progression.NomConfiance}").AppendLine();
                j.AppendLine("INDICES TROUVÉS");
                if (Progression.Indices.Count == 0) j.AppendLine("Aucun pour l'instant. Active le Sceau (E) et fixe ce qui t'intrigue.");
                foreach (var i in Progression.Indices) j.AppendLine("·  " + i);
                texteJournal.text = j.ToString();
            }

            // Fenêtre Système
            if (minuteurFenetre > 0) minuteurFenetre -= dt;
            groupeFenetre.alpha = Mathf.MoveTowards(groupeFenetre.alpha, minuteurFenetre > 0 ? 1 : 0, dt * 3f);

            // Étiquettes du Sceau (la plus proche d'abord, sans chevauchement)
            bool visibles = joueur != null && joueur.SceauNiveau > .5f && cam != null;
            var places = new List<Rect>();
            etiquettes.Sort((a, b) => Distance(a.source).CompareTo(Distance(b.source)));
            foreach (var (source, rect, texteE) in etiquettes)
            {
                if (texteE.text != source.texte)
                {
                    texteE.text = source.texte;
                    rect.sizeDelta = new Vector2(texteE.preferredWidth + 26, 34);
                }
                bool vis = visibles && !string.IsNullOrEmpty(source.texte);
                Vector2 pos = Vector2.zero;
                if (vis)
                {
                    Vector3 vp = cam.WorldToViewportPoint(source.transform.position);
                    vis = vp.z > 0 && vp.z < 170 && vp.x > -.05f && vp.x < 1.05f && vp.y > -.05f && vp.y < 1.05f;
                    pos = new Vector2(vp.x * taille.x, vp.y * taille.y);
                    var r = new Rect(pos.x - rect.sizeDelta.x / 2, pos.y, rect.sizeDelta.x, rect.sizeDelta.y);
                    if (vis)
                    {
                        foreach (var p in places) if (p.Overlaps(r)) { vis = false; break; }
                        if (vis) places.Add(r);
                    }
                }
                rect.gameObject.SetActive(vis);
                if (vis) rect.anchoredPosition = pos;
            }
        }

        void MettreAJourNoms(Vector2 taille)
        {
            if (cam == null) return;
            Vector3? cibleQuete = null;
            foreach (var i in Interlocuteur.Tous)
            {
                if (i == null) continue;
                if (!noms.TryGetValue(i, out var n))
                {
                    var r = Panneau("Nom_" + i.nom, toile, Vector2.zero, Vector2.zero, new Vector2(.5f, 0), Vector2.zero, new Vector2(200, 30), new Color(0, 0, 0, 0));
                    var t = Texte("Texte", r, i.nom, 20, Color.white, TextAnchor.MiddleCenter);
                    Etirer((RectTransform)t.transform, Vector2.zero, Vector2.zero);
                    n = new NomPersonnage { rect = r, texte = t };
                    noms[i] = n;
                }
                Vector3 tete = i.transform.position + Vector3.up * (1.75f * i.transform.lossyScale.y);
                bool estCible = i.id == Progression.Cible;
                if (estCible) cibleQuete = tete;
                Vector3 vp = cam.WorldToViewportPoint(tete);
                bool vis = !Dialogues.EnCours && vp.z > 0 && vp.z < 28f && vp.x > 0 && vp.x < 1 && vp.y > 0 && vp.y < 1;
                n.rect.gameObject.SetActive(vis);
                if (vis)
                {
                    n.rect.anchoredPosition = new Vector2(vp.x * taille.x, vp.y * taille.y + 6);
                    n.texte.color = estCible ? new Color(1f, .82f, .45f) : new Color(1, 1, 1, .85f);
                }
            }
            if (cibleQuete == null)
                foreach (var p in PointInteret.Tous)
                    if (p.cible == Progression.Cible && !p.decouvert) { cibleQuete = p.transform.position; if (p.Disponible) break; }

            bool montrer = cibleQuete.HasValue && !Dialogues.EnCours;
            if (montrer)
            {
                Vector3 vp = cam.WorldToViewportPoint(cibleQuete.Value + Vector3.up * .6f);
                montrer = vp.z > 4f && vp.x > 0 && vp.x < 1 && vp.y > 0 && vp.y < 1;
                if (montrer)
                {
                    reperequete.anchoredPosition = new Vector2(vp.x * taille.x, vp.y * taille.y + 36);
                    texteRepere.text = $"{Mathf.RoundToInt(vp.z)} m";
                    reperequete.localScale = Vector3.one * (1f + .12f * Mathf.Sin(Time.time * 4f));
                }
            }
            reperequete.gameObject.SetActive(montrer);
        }

        static Sprite SpriteAnneau()
        {
            const int S = 128;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { name = "Anneau" };
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(S / 2f, S / 2f)) / (S / 2f);
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - .84f) / .1f);
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(.5f, .5f));
        }

        void ConstruireJeu()
        {
            // Bandes de cinéma (dialogues)
            barreHaut = new GameObject("BandeHaut", typeof(RectTransform)).GetComponent<RectTransform>();
            barreHaut.SetParent(toile, false);
            barreHaut.anchorMin = new Vector2(0, 1); barreHaut.anchorMax = Vector2.one; barreHaut.pivot = new Vector2(.5f, 1);
            barreHaut.sizeDelta = Vector2.zero; barreHaut.anchoredPosition = Vector2.zero;
            var ih = barreHaut.gameObject.AddComponent<UnityEngine.UI.Image>(); ih.color = Color.black; ih.raycastTarget = false;
            barreBas = new GameObject("BandeBas", typeof(RectTransform)).GetComponent<RectTransform>();
            barreBas.SetParent(toile, false);
            barreBas.anchorMin = Vector2.zero; barreBas.anchorMax = new Vector2(1, 0); barreBas.pivot = new Vector2(.5f, 0);
            barreBas.sizeDelta = Vector2.zero; barreBas.anchoredPosition = Vector2.zero;
            var ib = barreBas.gameObject.AddComponent<UnityEngine.UI.Image>(); ib.color = Color.black; ib.raycastTarget = false;
            barreHaut.SetAsFirstSibling(); barreBas.SetAsFirstSibling();

            // Réticule du Sceau et anneau d'analyse
            var spr = SpriteAnneau();
            var centre = Panneau("Reticule", toile, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(84, 84), new Color(0, 0, 0, 0));
            anneauFond = Panneau("AnneauFond", centre, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(84, 84), new Color(1, 1, 1, .2f)).GetComponent<UnityEngine.UI.Image>();
            anneauFond.sprite = spr;
            anneau = Panneau("Anneau", centre, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(84, 84), new Color(.75f, .9f, 1f)).GetComponent<UnityEngine.UI.Image>();
            anneau.sprite = spr;
            anneau.type = UnityEngine.UI.Image.Type.Filled;
            anneau.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
            anneau.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
            anneau.fillClockwise = true;
            point = Panneau("Point", centre, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(6, 6), new Color(.85f, .93f, 1f, .9f)).GetComponent<UnityEngine.UI.Image>();
            point.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);

            // Repère de quête (losange doré + distance)
            reperequete = Panneau("RepereQuete", toile, Vector2.zero, Vector2.zero, new Vector2(.5f, 0), Vector2.zero, new Vector2(120, 50), new Color(0, 0, 0, 0));
            var los = Panneau("Losange", reperequete, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, .5f), new Vector2(0, -10), new Vector2(16, 16), new Color(1f, .82f, .45f));
            los.localRotation = Quaternion.Euler(0, 0, 45);
            texteRepere = Texte("Distance", reperequete, "", 16, new Color(1f, .9f, .7f), TextAnchor.LowerCenter);
            Etirer((RectTransform)texteRepere.transform, Vector2.zero, new Vector2(0, -22));

            // Quête en haut à droite
            var q = Panneau("Quete", toile, Vector2.one, Vector2.one, Vector2.one, new Vector2(-24, -24), new Vector2(560, 96), Verre);
            var tq = Texte("Titre", q, "QUÊTE", 16, new Color(1f, .82f, .45f), TextAnchor.UpperLeft);
            Etirer((RectTransform)tq.transform, new Vector2(16, 8), new Vector2(-16, -8));
            texteQuete = Texte("Texte", q, "", 20, Color.white, TextAnchor.UpperLeft);
            texteQuete.horizontalOverflow = HorizontalWrapMode.Wrap;
            Etirer((RectTransform)texteQuete.transform, new Vector2(16, 8), new Vector2(-16, -32));

            // Éveil et confiance au-dessus du mana
            var e = Panneau("Etat", toile, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(24, 76), new Vector2(700, 30), new Color(0, 0, 0, 0));
            texteEtat = Texte("Texte", e, "", 18, new Color(.92f, .9f, 1f), TextAnchor.MiddleLeft);
            Etirer((RectTransform)texteEtat.transform, Vector2.zero, Vector2.zero);

            // Invite d'interaction et analyse
            var inv = Panneau("Invite", toile, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 120), new Vector2(700, 40), new Color(0, 0, 0, 0));
            texteInvite = Texte("Texte", inv, "", 24, Color.white, TextAnchor.MiddleCenter);
            Etirer((RectTransform)texteInvite.transform, Vector2.zero, Vector2.zero);
            var an = Panneau("Analyse", toile, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -70), new Vector2(600, 40), new Color(0, 0, 0, 0));
            texteAnalyse = Texte("Texte", an, "", 22, new Color(.75f, .88f, 1f), TextAnchor.MiddleCenter);
            Etirer((RectTransform)texteAnalyse.transform, Vector2.zero, Vector2.zero);

            // Dialogue
            var d = Panneau("Dialogue", toile, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 70), new Vector2(1240, 300), new Color(.055f, .047f, .12f, .88f));
            panneauDialogue = d.gameObject;
            nomDialogue = Texte("Nom", d, "", 24, new Color(1f, .82f, .45f), TextAnchor.UpperLeft);
            nomDialogue.fontStyle = FontStyle.Bold;
            Etirer((RectTransform)nomDialogue.transform, new Vector2(28, 20), new Vector2(-28, -18));
            corpsDialogue = Texte("Texte", d, "", 22, Color.white, TextAnchor.UpperLeft);
            corpsDialogue.horizontalOverflow = HorizontalWrapMode.Wrap;
            Etirer((RectTransform)corpsDialogue.transform, new Vector2(28, 120), new Vector2(-28, -54));
            choixDialogue = Texte("Choix", d, "", 21, new Color(.85f, .9f, 1f), TextAnchor.UpperLeft);
            choixDialogue.horizontalOverflow = HorizontalWrapMode.Wrap;
            Etirer((RectTransform)choixDialogue.transform, new Vector2(28, 14), new Vector2(-28, -190));
            panneauDialogue.SetActive(false);

            // Journal (touche J)
            var jn = Panneau("Journal", toile, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(900, 600), new Color(.055f, .047f, .12f, .92f));
            panneauJournal = jn.gameObject;
            texteJournal = Texte("Texte", jn, "", 21, Color.white, TextAnchor.UpperLeft);
            texteJournal.horizontalOverflow = HorizontalWrapMode.Wrap;
            Etirer((RectTransform)texteJournal.transform, new Vector2(36, 30), new Vector2(-36, -30));
            panneauJournal.SetActive(false);
        }

        float Distance(EtiquetteSceau e) => cam == null ? 0 : Vector3.Distance(cam.transform.position, e.transform.position);

        // ---------- construction ----------

        void ConstruireBoussole()
        {
            var cadre = Panneau("Boussole", toile, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -14), new Vector2(760, 44), new Color(0, 0, 0, 0));
            cadre.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var ligne = Panneau("Ligne", cadre, new Vector2(0, 0), new Vector2(1, 0), new Vector2(.5f, 0), new Vector2(0, 8), new Vector2(0, 2), new Color(1, 1, 1, .55f));
            ligne.sizeDelta = new Vector2(0, 2);
            var repere = Panneau("Repere", cadre, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(.5f, .5f), new Vector2(0, 9), new Vector2(10, 10), new Color(1f, .82f, .54f));
            repere.localRotation = Quaternion.Euler(0, 0, 45);

            bandeBoussole = new GameObject("Bande", typeof(RectTransform)).GetComponent<RectTransform>();
            bandeBoussole.SetParent(cadre, false);
            bandeBoussole.anchorMin = bandeBoussole.anchorMax = new Vector2(.5f, .5f);
            bandeBoussole.sizeDelta = Vector2.zero;
            var noms = new Dictionary<int, string> { { 0, "N" }, { 45, "NE" }, { 90, "E" }, { 135, "SE" }, { 180, "S" }, { 225, "SO" }, { 270, "O" }, { 315, "NO" } };
            for (int a = -360; a <= 720; a += 15)
            {
                int m = ((a % 360) + 360) % 360;
                bool principal = noms.TryGetValue(m, out var nom);
                var t = Texte("C" + a, bandeBoussole, principal ? nom : "·", principal ? 22 : 16, new Color(1, 1, 1, principal ? 1f : .7f), TextAnchor.MiddleCenter);
                var r = (RectTransform)t.transform;
                r.anchorMin = r.anchorMax = new Vector2(.5f, .5f);
                r.sizeDelta = new Vector2(60, 30);
                r.anchoredPosition = new Vector2(a * PixelsParDegre, 6);
            }
        }

        void ConstruireMana()
        {
            var bloc = Panneau("Mana", toile, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(24, 24), new Vector2(260, 46), new Color(0, 0, 0, 0));
            texteMana = Texte("Texte", bloc, "Mana 35 / 35", 20, Color.white, TextAnchor.UpperLeft);
            Etirer((RectTransform)texteMana.transform, Vector2.zero, Vector2.zero);
            var fond = Panneau("Jauge", bloc, new Vector2(0, 0), new Vector2(1, 0), new Vector2(.5f, 0), Vector2.zero, new Vector2(0, 12), new Color(1, 1, 1, .15f));
            fond.sizeDelta = new Vector2(0, 12);
            remplissageMana = Panneau("Remplissage", fond, Vector2.zero, Vector2.one, new Vector2(0, .5f), Vector2.zero, Vector2.zero, new Color(.44f, .7f, 1f));
            remplissageMana.offsetMin = remplissageMana.offsetMax = Vector2.zero;
        }

        void ConstruireAide()
        {
            var bloc = Panneau("Aide", toile, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 18), new Vector2(1180, 38), Verre);
            var t = Texte("Texte", bloc, "VARENNE · ZQSD — marcher · Maj — courir · Espace — sauter · E — Sceau · F — parler · J — journal · R — rendu · Échap — souris", 19, Color.white, TextAnchor.MiddleCenter);
            Etirer((RectTransform)t.transform, Vector2.zero, Vector2.zero);
        }

        void ConstruireFenetre()
        {
            fenetre = Panneau("FenetreSysteme", toile, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -80), new Vector2(470, 260), new Color(.055f, .047f, .12f, .82f));
            var cadre = Panneau("Cadre", fenetre, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0));
            cadre.offsetMin = new Vector2(4, 4); cadre.offsetMax = new Vector2(-4, -4);
            var contour = cadre.gameObject.AddComponent<UnityEngine.UI.Outline>();
            contour.effectColor = new Color(.81f, .78f, 1f, .9f);
            contour.effectDistance = new Vector2(2, -2);
            groupeFenetre = fenetre.gameObject.AddComponent<CanvasGroup>();
            groupeFenetre.alpha = 0; groupeFenetre.blocksRaycasts = false; groupeFenetre.interactable = false;
            titreFenetre = Texte("Titre", fenetre, "", 24, Color.white, TextAnchor.UpperLeft);
            titreFenetre.fontStyle = FontStyle.Bold;
            Etirer((RectTransform)titreFenetre.transform, new Vector2(20, 16), new Vector2(-20, -16));
            corpsFenetre = Texte("Corps", fenetre, "", 19, new Color(.92f, .9f, 1f), TextAnchor.UpperLeft);
            Etirer((RectTransform)corpsFenetre.transform, new Vector2(20, 16), new Vector2(-20, -52));
        }

        static RectTransform Panneau(string nom, Transform parent, Vector2 ancreMin, Vector2 ancreMax, Vector2 pivot, Vector2 pos, Vector2 taille, Color couleur)
        {
            var go = new GameObject(nom, typeof(RectTransform));
            var r = go.GetComponent<RectTransform>();
            r.SetParent(parent, false);
            r.anchorMin = ancreMin; r.anchorMax = ancreMax; r.pivot = pivot;
            r.sizeDelta = taille; r.anchoredPosition = pos;
            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.color = couleur; img.raycastTarget = false;
            return r;
        }

        UnityEngine.UI.Text Texte(string nom, Transform parent, string contenu, int taille, Color couleur, TextAnchor alignement)
        {
            var go = new GameObject(nom, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<UnityEngine.UI.Text>();
            t.font = police; t.fontSize = taille; t.color = couleur; t.alignment = alignement;
            t.text = contenu; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var ombre = go.AddComponent<UnityEngine.UI.Shadow>();
            ombre.effectColor = new Color(0, 0, 0, .6f);
            return t;
        }

        static void Etirer(RectTransform r, Vector2 bas, Vector2 haut)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(.5f, .5f);
            r.offsetMin = bas; r.offsetMax = haut;
        }
    }
}
