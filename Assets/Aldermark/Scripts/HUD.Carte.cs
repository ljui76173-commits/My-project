using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Aldermark
{
    /// <summary>
    /// La carte façon GTA. Grande carte (M) : zoom à la molette, glisser pour se déplacer, Espace pour se recentrer,
    /// clic gauche pour poser ou retirer un repère, clic droit pour fixer une destination (itinéraire par les routes) ;
    /// légende à onglets (Tout, Lieux, Commerces, Services, Dangers, Mes repères) avec une icône par type,
    /// un clic sur une ligne centre la carte. Mini-carte ronde (N) qui tourne avec le héros.
    /// La destination et les repères s'affichent aussi sur la boussole.
    /// </summary>
    public partial class HUD
    {
        class IconeCarte { public RectTransform rect; public UnityEngine.UI.Image image; }

        struct EntreeCarte
        {
            public Vector2 xz;
            public Sprite sprite;
            public string nom, type;
            public OngletCarte onglet;
            public int repere;      // indice du repère perso, sinon −1
            public bool destination;
        }

        static readonly OngletCarte[] Onglets = { OngletCarte.Tout, OngletCarte.Lieux, OngletCarte.Commerces, OngletCarte.Services, OngletCarte.Dangers, OngletCarte.Reperes };
        const float PorteeMiniCarte = 150f, TailleMiniCarte = 230f;

        bool carteConstruite, carteOuverte, miniCarteVisible = true, clicCarte, glisseCarte;
        RectTransform panneauCarte, vueCarte, imageCarte, legendeCarte, infobulleCarte, panneauMini, pivotMini, imageMini, nordMini;
        UnityEngine.UI.RawImage rawCarte, rawBrouillard, rawMini, rawBrouillardMini;
        UnityEngine.UI.Text texteChargement, texteInfobulle, texteRoute, texteBoussoleDestination;
        readonly RectTransform[] rectOnglets = new RectTransform[6];
        readonly UnityEngine.UI.Text[] texteOnglets = new UnityEngine.UI.Text[6];
        readonly List<(RectTransform rect, UnityEngine.UI.Image icone, UnityEngine.UI.Text texte)> lignesLegende = new List<(RectTransform, UnityEngine.UI.Image, UnityEngine.UI.Text)>();
        readonly List<IconeCarte> iconesCarte = new List<IconeCarte>(), iconesMini = new List<IconeCarte>(), iconesBoussole = new List<IconeCarte>();
        readonly List<RectTransform> segmentsCarte = new List<RectTransform>(), segmentsMini = new List<RectTransform>();
        readonly List<EntreeCarte> entrees = new List<EntreeCarte>(), entreesLegende = new List<EntreeCarte>();
        RectTransform joueurCarte;
        OngletCarte onglet = OngletCarte.Tout;
        float zoomCarte = 1f;
        Vector2 panCarte, debutClicCarte;
        bool texturesPosees;
        KeyControl toucheM;

        public static bool CarteOuverte => instance != null && instance.carteOuverte;

        // ---------- construction ----------

        void ConstruireCarte()
        {
            if (carteConstruite) return;
            carteConstruite = true;
            miniCarteVisible = PlayerPrefs.GetInt("Aldermark.MiniCarte", 1) == 1;

            // Grande carte
            panneauCarte = Panneau("Carte", toile, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero, new Color(.04f, .035f, .08f, .94f));
            panneauCarte.offsetMin = panneauCarte.offsetMax = Vector2.zero;
            var titre = Haut(Texte("Titre", panneauCarte, "CARTE — LA VALLÉE", 30, Color.white, TextAnchor.UpperLeft), 36, 20, 900, 40);
            titre.fontStyle = FontStyle.Bold;
            vueCarte = Panneau("Vue", panneauCarte, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero, new Color(.1f, .09f, .14f, 1f));
            vueCarte.offsetMin = new Vector2(30, 56); vueCarte.offsetMax = new Vector2(-430, -70);
            vueCarte.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            Contour(vueCarte);
            imageCarte = new GameObject("Image", typeof(RectTransform)).GetComponent<RectTransform>();
            imageCarte.SetParent(vueCarte, false);
            imageCarte.anchorMin = imageCarte.anchorMax = imageCarte.pivot = new Vector2(.5f, .5f);
            rawCarte = imageCarte.gameObject.AddComponent<UnityEngine.UI.RawImage>();
            rawCarte.raycastTarget = false;
            rawBrouillard = Etendu("Brouillard", imageCarte).gameObject.AddComponent<UnityEngine.UI.RawImage>();
            rawBrouillard.color = new Color(.07f, .06f, .11f, .9f);
            rawBrouillard.raycastTarget = false;
            joueurCarte = Panneau("Joueur", imageCarte, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(30, 30), Color.white);
            joueurCarte.GetComponent<UnityEngine.UI.Image>().sprite = Icones.Joueur;
            texteChargement = Texte("Chargement", vueCarte, "", 24, Color.white, TextAnchor.MiddleCenter);
            Etirer((RectTransform)texteChargement.transform, Vector2.zero, Vector2.zero);
            texteRoute = Haut(Texte("Route", panneauCarte, "", 19, CouleurOr, TextAnchor.UpperLeft), 520, 26, 900, 30);
            var bas = Texte("Bas", panneauCarte, "Molette : zoom  ·  Glisser : déplacer  ·  Clic : poser / retirer un repère  ·  Clic droit : destination (itinéraire)  ·  Espace : te retrouver  ·  N : mini-carte  ·  M ou Échap : fermer",
                16, Pale, TextAnchor.LowerLeft).rectTransform;
            bas.anchorMin = bas.anchorMax = bas.pivot = Vector2.zero;
            bas.anchoredPosition = new Vector2(36, 16); bas.sizeDelta = new Vector2(1500, 26);

            // Légende à droite
            legendeCarte = Panneau("Legende", panneauCarte, new Vector2(1, 0), Vector2.one, new Vector2(1, .5f), Vector2.zero, new Vector2(390, 0), new Color(.07f, .06f, .13f, .96f));
            legendeCarte.offsetMin = new Vector2(-414, 56); legendeCarte.offsetMax = new Vector2(-24, -70);
            Contour(legendeCarte);
            Haut(Texte("Titre", legendeCarte, "LÉGENDE", 20, Color.white, TextAnchor.UpperLeft), 18, 14, 340, 26).fontStyle = FontStyle.Bold;
            for (int k = 0; k < Onglets.Length; k++)
            {
                int col = k % 3, ligne = k / 3;
                var r = Panneau("Onglet" + k, legendeCarte, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(14 + col * 122, -48 - ligne * 38), new Vector2(116, 32), new Color(1, 1, 1, .06f));
                var t = Texte("Texte", r, Lieu.NomOnglet(Onglets[k]), 15, Color.white, TextAnchor.MiddleCenter);
                Etirer((RectTransform)t.transform, Vector2.zero, Vector2.zero);
                rectOnglets[k] = r; texteOnglets[k] = t;
            }
            for (int k = 0; k < 15; k++)
            {
                var r = Panneau("Ligne" + k, legendeCarte, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -136 - k * 36), new Vector2(370, 34), new Color(1, 1, 1, 0));
                UnityEngine.UI.Image ic = Panneau("Icone", r, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(6, 0), new Vector2(28, 28), Color.white).GetComponent<UnityEngine.UI.Image>();
                var t = Texte("Texte", r, "", 16, Color.white, TextAnchor.MiddleLeft);
                var rt = (RectTransform)t.transform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(42, 0); rt.offsetMax = new Vector2(-4, 0);
                lignesLegende.Add((r, ic, t));
            }

            // Infobulle
            infobulleCarte = Panneau("Infobulle", panneauCarte, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, Vector2.zero, new Vector2(320, 30), new Color(.05f, .04f, .1f, .92f));
            texteInfobulle = Texte("Texte", infobulleCarte, "", 16, Color.white, TextAnchor.MiddleLeft);
            Etirer((RectTransform)texteInfobulle.transform, new Vector2(10, 0), new Vector2(-8, 0));
            panneauCarte.gameObject.SetActive(false);

            // Mini-carte ronde sous l'horloge
            panneauMini = Panneau("MiniCarte", toile, Vector2.one, Vector2.one, Vector2.one, new Vector2(-24, -104), new Vector2(TailleMiniCarte, TailleMiniCarte), new Color(0, 0, 0, 0));
            var anneau = Panneau("Anneau", panneauMini, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(TailleMiniCarte + 8, TailleMiniCarte + 8), new Color(.81f, .78f, 1f, .75f));
            anneau.GetComponent<UnityEngine.UI.Image>().sprite = Icones.Disque;
            var masque = Panneau("Masque", panneauMini, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(TailleMiniCarte, TailleMiniCarte), new Color(.07f, .06f, .11f, 1f));
            masque.GetComponent<UnityEngine.UI.Image>().sprite = Icones.Disque;
            masque.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = true;
            pivotMini = Etendu("Pivot", masque);
            imageMini = new GameObject("Image", typeof(RectTransform)).GetComponent<RectTransform>();
            imageMini.SetParent(pivotMini, false);
            imageMini.anchorMin = imageMini.anchorMax = imageMini.pivot = new Vector2(.5f, .5f);
            float tailleImage = TailleMiniCarte * CarteMonde.Taille / PorteeMiniCarte;
            imageMini.sizeDelta = new Vector2(tailleImage, tailleImage);
            rawMini = imageMini.gameObject.AddComponent<UnityEngine.UI.RawImage>();
            rawMini.raycastTarget = false;
            rawBrouillardMini = Etendu("Brouillard", imageMini).gameObject.AddComponent<UnityEngine.UI.RawImage>();
            rawBrouillardMini.color = new Color(.07f, .06f, .11f, .9f);
            rawBrouillardMini.raycastTarget = false;
            var fleche = Panneau("Joueur", masque, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(24, 24), Color.white);
            fleche.GetComponent<UnityEngine.UI.Image>().sprite = Icones.Joueur;
            var n = Texte("Nord", panneauMini, "N", 18, CouleurOr, TextAnchor.MiddleCenter);
            n.fontStyle = FontStyle.Bold;
            nordMini = (RectTransform)n.transform;
            nordMini.anchorMin = nordMini.anchorMax = new Vector2(.5f, .5f); nordMini.sizeDelta = new Vector2(30, 30);
            panneauMini.gameObject.SetActive(miniCarteVisible);

            // Destination sur la boussole
            var boussole = toile.Find("Boussole");
            if (boussole != null)
            {
                texteBoussoleDestination = Texte("Distance", boussole, "", 13, new Color(.85f, .7f, 1f), TextAnchor.MiddleLeft);
                var rd = (RectTransform)texteBoussoleDestination.transform;
                rd.anchorMin = rd.anchorMax = new Vector2(.5f, 0); rd.pivot = new Vector2(0, .5f); rd.sizeDelta = new Vector2(90, 20);
            }
        }

        static RectTransform Etendu(string nom, Transform parent)
        {
            var r = new GameObject(nom, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent, false);
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            return r;
        }

        IconeCarte Icone(List<IconeCarte> reserve, int i, Transform parent)
        {
            while (reserve.Count <= i)
            {
                var r = Panneau("Icone", parent, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(32, 32), Color.white);
                reserve.Add(new IconeCarte { rect = r, image = r.GetComponent<UnityEngine.UI.Image>() });
            }
            var ic = reserve[i];
            if (!ic.rect.gameObject.activeSelf) ic.rect.gameObject.SetActive(true);
            return ic;
        }

        static void Masquer<T>(List<T> reserve, int depuis) where T : class
        {
            for (int k = depuis; k < reserve.Count; k++)
            {
                object o = reserve[k];
                var go = o is IconeCarte ic ? ic.rect.gameObject : (o as RectTransform)?.gameObject;
                if (go != null && go.activeSelf) go.SetActive(false);
            }
        }

        RectTransform Segment(List<RectTransform> reserve, int i, Transform parent, float epaisseur)
        {
            while (reserve.Count <= i)
            {
                var r = Panneau("Segment", parent, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, .5f), Vector2.zero, new Vector2(10, epaisseur), new Color(.68f, .36f, .95f, .9f));
                r.SetSiblingIndex(Mathf.Min(1, parent.childCount - 1));   // sous les icônes
                reserve.Add(r);
            }
            if (!reserve[i].gameObject.activeSelf) reserve[i].gameObject.SetActive(true);
            return reserve[i];
        }

        // ---------- mise à jour ----------

        void OuvrirCarte(bool ouvrir)
        {
            if (carteOuverte == ouvrir) return;
            carteOuverte = ouvrir;
            panneauCarte.gameObject.SetActive(ouvrir);
            Cursor.lockState = ouvrir ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = ouvrir;
            Sons.Jouer("clic", .5f, ouvrir ? 1.2f : .9f);
            if (ouvrir) { zoomCarte = 2.2f; CentrerSurJoueur(); }
        }

        void CentrerSurJoueur()
        {
            var h = CombatHeros.Instance;
            if (h == null) return;
            float taille = TailleCarte();
            panCarte = -(CarteMonde.UV(h.transform.position) - new Vector2(.5f, .5f)) * taille;
        }

        float TailleCarte() => Mathf.Max(200f, Mathf.Min(vueCarte.rect.width, vueCarte.rect.height)) * zoomCarte;

        void MettreAJourCarte()
        {
            ConstruireCarte();
            var carte = CarteMonde.Instance;
            var kb = Keyboard.current;
            if (carte == null || kb == null) return;
            if (!texturesPosees && carte.Prete)
            {
                texturesPosees = true;
                rawCarte.texture = rawMini.texture = carte.Peinture;
            }
            rawBrouillard.texture = rawBrouillardMini.texture = carte.Brouillard;
            if (!texturesPosees) rawCarte.color = rawMini.color = new Color(.2f, .18f, .25f);
            else rawCarte.color = rawMini.color = Color.white;

            // Touches : M (selon la disposition du clavier) et N
            if (toucheM == null) toucheM = kb.FindKeyOnCurrentKeyboardLayout("m") ?? kb.mKey;
            bool autreMenu = statutOuvert || inventaireOuvert || Coffre.Ouvert != null || ConsoleDebug.Ouverte || Dialogues.EnCours;
            if (!autreMenu && toucheM.wasPressedThisFrame) OuvrirCarte(!carteOuverte);
            if (carteOuverte && kb.escapeKey.wasPressedThisFrame) OuvrirCarte(false);
            if (!autreMenu && kb.nKey.wasPressedThisFrame)
            {
                miniCarteVisible = !miniCarteVisible;
                PlayerPrefs.SetInt("Aldermark.MiniCarte", miniCarteVisible ? 1 : 0);
            }

            ListerEntrees(carte);
            MettreAJourMiniCarte(carte);
            MettreAJourBoussole(carte);
            if (carteOuverte) MettreAJourGrandeCarte(carte, kb);
        }

        void ListerEntrees(CarteMonde carte)
        {
            entrees.Clear();
            foreach (var l in Lieu.Tous)
                if (l.Decouvert)
                    entrees.Add(new EntreeCarte { xz = new Vector2(l.Position.x, l.Position.z), sprite = Icones.Lieu(l.type), nom = l.nom, type = Lieu.NomType(l.type), onglet = l.Onglet, repere = -1 });
            for (int k = 0; k < carte.reperes.Count; k++)
                entrees.Add(new EntreeCarte { xz = carte.reperes[k], sprite = Icones.Repere, nom = $"Repère {k + 1}", type = "Repère", onglet = OngletCarte.Reperes, repere = k });
            if (carte.Destination.HasValue)
                entrees.Add(new EntreeCarte { xz = carte.Destination.Value, sprite = Icones.Itineraire, nom = "Destination", type = "Itinéraire", onglet = OngletCarte.Reperes, repere = -1, destination = true });
        }

        static float Cap(CombatHeros h) => h != null ? h.transform.eulerAngles.y : 0f;

        void MettreAJourMiniCarte(CarteMonde carte)
        {
            bool visible = miniCarteVisible && !carteOuverte;
            if (panneauMini.gameObject.activeSelf != visible) panneauMini.gameObject.SetActive(visible);
            var h = CombatHeros.Instance;
            if (!visible || h == null) return;
            float cap = Cap(h);
            float taille = imageMini.sizeDelta.x;
            imageMini.anchoredPosition = -(CarteMonde.UV(h.transform.position) - new Vector2(.5f, .5f)) * taille;
            pivotMini.localRotation = Quaternion.Euler(0, 0, cap);
            float r = TailleMiniCarte / 2f - 2f;
            nordMini.anchoredPosition = new Vector2(-Mathf.Sin(cap * Mathf.Deg2Rad), Mathf.Cos(cap * Mathf.Deg2Rad)) * r;

            int n = 0;
            foreach (var e in entrees)
            {
                var ic = Icone(iconesMini, n++, imageMini);
                ic.image.sprite = e.sprite;
                Vector2 uv = CarteMonde.UV(e.xz);
                ic.rect.anchorMin = ic.rect.anchorMax = uv;
                ic.rect.anchoredPosition = Vector2.zero;
                ic.rect.sizeDelta = Vector2.one * (e.destination ? 26f : 22f);
                ic.rect.localRotation = Quaternion.Euler(0, 0, -cap);
            }
            Masquer(iconesMini, n);
            TracerRoute(carte, segmentsMini, imageMini, taille, 4f);
        }

        void TracerRoute(CarteMonde carte, List<RectTransform> reserve, RectTransform parent, float taille, float epaisseur)
        {
            int s = 0;
            var h = CombatHeros.Instance;
            for (int k = 1; k < carte.route.Count; k++)
            {
                Vector2 a = k == 1 && h != null ? new Vector2(h.transform.position.x, h.transform.position.z) : carte.route[k - 1];
                Vector2 pa = (CarteMonde.UV(a) - new Vector2(.5f, .5f)) * taille, pb = (CarteMonde.UV(carte.route[k]) - new Vector2(.5f, .5f)) * taille;
                Vector2 d = pb - pa;
                if (d.magnitude < .5f) continue;
                var seg = Segment(reserve, s++, parent, epaisseur);
                seg.anchoredPosition = pa;
                seg.sizeDelta = new Vector2(d.magnitude, epaisseur);
                seg.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            }
            Masquer(reserve, s);
        }

        void MettreAJourBoussole(CarteMonde carte)
        {
            var boussole = toile.Find("Boussole");
            if (boussole == null || cam == null) return;
            var h = CombatHeros.Instance;
            float cap = cam.transform.eulerAngles.y;
            int n = 0;
            string distance = "";
            float xDistance = 0f;
            if (h != null)
            {
                Vector2 p = new Vector2(h.transform.position.x, h.transform.position.z);
                foreach (var e in entrees)
                {
                    if (e.repere < 0 && !e.destination) continue;
                    Vector2 v = e.xz - p;
                    float relatif = Mathf.DeltaAngle(cap, Mathf.Atan2(v.x, v.y) * Mathf.Rad2Deg);
                    float x = Mathf.Clamp(relatif * PixelsParDegre, -360f, 360f);
                    var ic = Icone(iconesBoussole, n++, boussole);
                    ic.image.sprite = e.sprite;
                    ic.rect.anchorMin = ic.rect.anchorMax = new Vector2(.5f, 0);
                    ic.rect.anchoredPosition = new Vector2(x, 13);
                    ic.rect.sizeDelta = Vector2.one * (e.destination ? 24f : 18f);
                    if (e.destination)
                    {
                        float l = carte.route.Count > 1 ? carte.LongueurRoute() : v.magnitude;
                        distance = l >= 1000f ? $"{l / 1000f:0.0} km" : $"{l:0} m";
                        xDistance = x + 14f;
                    }
                }
            }
            Masquer(iconesBoussole, n);
            if (texteBoussoleDestination != null)
            {
                texteBoussoleDestination.text = distance;
                texteBoussoleDestination.rectTransform.anchoredPosition = new Vector2(Mathf.Min(xDistance, 290f), 13);
            }
        }

        void MettreAJourGrandeCarte(CarteMonde carte, Keyboard kb)
        {
            var souris = Mouse.current;
            if (souris == null) return;
            Vector2 ecran = souris.position.ReadValue();
            bool dansVue = RectTransformUtility.RectangleContainsScreenPoint(vueCarte, ecran, null);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(vueCarte, ecran, null, out var local);
            local += (vueCarte.pivot - new Vector2(.5f, .5f)) * vueCarte.rect.size;   // repère centré sur la vue

            texteChargement.text = carte.Prete ? "" : $"Cartographie de la vallée…  {carte.Avancement * 100f:0} %";

            // Zoom autour du curseur
            float molette = souris.scroll.ReadValue().y;
            if (dansVue && molette != 0f)
            {
                float avant = zoomCarte;
                zoomCarte = Mathf.Clamp(zoomCarte * (molette > 0 ? 1.2f : 1f / 1.2f), 1f, 8f);
                panCarte = local - (local - panCarte) * (zoomCarte / avant);
            }
            if (kb.spaceKey.wasPressedThisFrame) CentrerSurJoueur();

            // Glisser pour se déplacer, cliquer pour un repère
            if (souris.leftButton.wasPressedThisFrame && dansVue) { clicCarte = true; glisseCarte = false; debutClicCarte = ecran; }
            if (clicCarte && souris.leftButton.isPressed)
            {
                if ((ecran - debutClicCarte).magnitude > 6f) glisseCarte = true;
                if (glisseCarte)
                {
                    var toileCanvas = toile.GetComponent<Canvas>();
                    panCarte += souris.delta.ReadValue() / (toileCanvas != null ? Mathf.Max(.01f, toileCanvas.scaleFactor) : 1f);
                }
            }
            float taille = TailleCarte();
            panCarte.x = Mathf.Clamp(panCarte.x, -taille / 2f, taille / 2f);
            panCarte.y = Mathf.Clamp(panCarte.y, -taille / 2f, taille / 2f);
            imageCarte.sizeDelta = new Vector2(taille, taille);
            imageCarte.anchoredPosition = panCarte;

            // Le joueur
            var h = CombatHeros.Instance;
            if (h != null)
            {
                joueurCarte.anchorMin = joueurCarte.anchorMax = CarteMonde.UV(h.transform.position);
                joueurCarte.anchoredPosition = Vector2.zero;
                joueurCarte.localRotation = Quaternion.Euler(0, 0, -Cap(h));
                joueurCarte.SetAsLastSibling();
            }

            // Les icônes (onglet choisi) et celle qu'on survole
            int n = 0, survolee = -1;
            float best = 18f;
            Vector2 uvSouris = (local - panCarte) / taille + new Vector2(.5f, .5f);
            for (int k = 0; k < entrees.Count; k++)
            {
                var e = entrees[k];
                if (onglet != OngletCarte.Tout && e.onglet != onglet && !e.destination) continue;
                var ic = Icone(iconesCarte, n++, imageCarte);
                ic.image.sprite = e.sprite;
                Vector2 uv = CarteMonde.UV(e.xz);
                ic.rect.anchorMin = ic.rect.anchorMax = uv;
                ic.rect.anchoredPosition = Vector2.zero;
                ic.rect.sizeDelta = Vector2.one * (e.destination ? 38f : 32f);
                float d = ((uv - uvSouris) * taille).magnitude;
                if (dansVue && d < best) { best = d; survolee = k; }
            }
            Masquer(iconesCarte, n);
            TracerRoute(carte, segmentsCarte, imageCarte, taille, 6f);
            if (h != null) joueurCarte.SetAsLastSibling();

            // Infobulle
            bool bulle = survolee >= 0;
            if (infobulleCarte.gameObject.activeSelf != bulle) infobulleCarte.gameObject.SetActive(bulle);
            if (bulle)
            {
                var e = entrees[survolee];
                float dist = h != null ? Vector2.Distance(e.xz, new Vector2(h.transform.position.x, h.transform.position.z)) : 0f;
                texteInfobulle.text = $"<b>{e.nom}</b>   <color=#a9a6c4>{e.type} · {dist:0} m</color>";
                infobulleCarte.sizeDelta = new Vector2(texteInfobulle.preferredWidth + 22, 30);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(panneauCarte, ecran, null, out var surPanneau);
                infobulleCarte.anchoredPosition = surPanneau + new Vector2(18, 10);
                infobulleCarte.SetAsLastSibling();
            }

            // Relâchement du clic gauche : poser ou retirer un repère
            if (clicCarte && souris.leftButton.wasReleasedThisFrame)
            {
                clicCarte = false;
                if (!glisseCarte && dansVue)
                {
                    if (survolee >= 0 && entrees[survolee].repere >= 0) carte.RetirerRepere(entrees[survolee].repere);
                    else if (survolee < 0) carte.AjouterRepere(CarteMonde.DepuisUV(uvSouris));
                }
            }
            // Clic droit : destination (sur une icône : ce lieu) ou l'effacer
            if (souris.rightButton.wasPressedThisFrame && dansVue)
            {
                if (survolee >= 0 && entrees[survolee].destination) carte.EffacerDestination();
                else carte.FixerDestination(survolee >= 0 ? entrees[survolee].xz : CarteMonde.DepuisUV(uvSouris));
            }
            texteRoute.text = carte.Destination.HasValue
                ? $"Itinéraire : {carte.LongueurRoute():0} m   ·   clic droit sur le drapeau pour l'effacer"
                : "";

            MettreAJourLegende(carte, souris, ecran);
        }

        void MettreAJourLegende(CarteMonde carte, Mouse souris, Vector2 ecran)
        {
            bool clic = souris.leftButton.wasPressedThisFrame, clicDroit = souris.rightButton.wasPressedThisFrame;
            for (int k = 0; k < Onglets.Length; k++)
            {
                bool actif = Onglets[k] == onglet;
                bool sur = RectTransformUtility.RectangleContainsScreenPoint(rectOnglets[k], ecran, null);
                if (sur && clic) { onglet = Onglets[k]; actif = true; Sons.Jouer("clic", .4f, 1.3f); }
                var c = Onglets[k] == OngletCarte.Tout ? new Color(.85f, .82f, 1f) : Icones.Couleur(Onglets[k]);
                rectOnglets[k].GetComponent<UnityEngine.UI.Image>().color = actif ? new Color(c.r, c.g, c.b, .55f) : sur ? new Color(1, 1, 1, .12f) : new Color(1, 1, 1, .05f);
                texteOnglets[k].fontStyle = actif ? FontStyle.Bold : FontStyle.Normal;
            }

            // Les lignes : les entrées de l'onglet, des plus proches aux plus lointaines
            var h = CombatHeros.Instance;
            Vector2 p = h != null ? new Vector2(h.transform.position.x, h.transform.position.z) : Vector2.zero;
            entreesLegende.Clear();
            foreach (var e in entrees) if (onglet == OngletCarte.Tout || e.onglet == onglet) entreesLegende.Add(e);
            entreesLegende.Sort((a, b) => Vector2.Distance(a.xz, p).CompareTo(Vector2.Distance(b.xz, p)));
            for (int k = 0; k < lignesLegende.Count; k++)
            {
                var (rect, icone, texte) = lignesLegende[k];
                if (k >= entreesLegende.Count)
                {
                    if (rect.gameObject.activeSelf) rect.gameObject.SetActive(false);
                    continue;
                }
                if (!rect.gameObject.activeSelf) rect.gameObject.SetActive(true);
                var e = entreesLegende[k];
                icone.sprite = e.sprite;
                float d = Vector2.Distance(e.xz, p);
                texte.text = $"{e.nom}   <size=13><color=#a9a6c4>{e.type} · {(d >= 1000f ? $"{d / 1000f:0.0} km" : $"{d:0} m")}</color></size>";
                bool sur = RectTransformUtility.RectangleContainsScreenPoint(rect, ecran, null);
                rect.GetComponent<UnityEngine.UI.Image>().color = sur ? new Color(1, 1, 1, .1f) : new Color(1, 1, 1, 0);
                if (sur && clic)
                {
                    zoomCarte = Mathf.Max(zoomCarte, 3f);
                    panCarte = -(CarteMonde.UV(e.xz) - new Vector2(.5f, .5f)) * TailleCarte();
                    Sons.Jouer("clic", .4f, 1.2f);
                }
                if (sur && clicDroit) carte.FixerDestination(e.xz);
            }
            if (entreesLegende.Count == 0)
            {
                var (rect, icone, texte) = lignesLegende[0];
                rect.gameObject.SetActive(true);
                icone.sprite = null; icone.color = new Color(0, 0, 0, 0);
                texte.text = onglet == OngletCarte.Reperes ? "<color=#a9a6c4>Clique sur la carte pour poser un repère.</color>"
                    : "<color=#a9a6c4>Rien de découvert ici pour l'instant.</color>";
            }
            else lignesLegende[0].icone.color = Color.white;
        }
    }
}
