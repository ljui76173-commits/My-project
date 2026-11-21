using System.Collections.Generic;
using UnityEngine;

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

            // Fenêtre Système
            if (minuteurFenetre > 0) minuteurFenetre -= dt;
            groupeFenetre.alpha = Mathf.MoveTowards(groupeFenetre.alpha, minuteurFenetre > 0 ? 1 : 0, dt * 3f);

            // Étiquettes du Sceau (la plus proche d'abord, sans chevauchement)
            bool visibles = joueur != null && joueur.SceauNiveau > .5f && cam != null;
            var places = new List<Rect>();
            etiquettes.Sort((a, b) => Distance(a.source).CompareTo(Distance(b.source)));
            foreach (var (source, rect, _) in etiquettes)
            {
                bool vis = visibles;
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
            var t = Texte("Texte", bloc, "VARENNE · ZQSD — marcher · Maj — courir · E — Sceau du Grand Registre · R — rendu brut / peint · Échap — souris", 19, Color.white, TextAnchor.MiddleCenter);
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
