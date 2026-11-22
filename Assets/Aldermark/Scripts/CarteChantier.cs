using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UI = UnityEngine.UI;

namespace Aldermark
{
    /// <summary>
    /// Carte du chantier (touche Tab) : la vraie vallée vue d'en haut, en pixels.
    /// Molette : zoom vers le curseur. Glisser le fond (clic gauche ou droit) : déplacer la carte.
    /// Sur la carte : de petites étiquettes ; le détail d'un poste au survol, ou à droite une fois cliqué.
    /// Glisser un portrait (ou une pastille) sur un poste : il y travaille ; sur une flèche : il devient porteur.
    /// </summary>
    public class CarteChantier : MonoBehaviour
    {
        const float TailleCarte = 860f;
        static readonly Color Fond = new Color(.045f, .05f, .1f, .97f), Argent = new Color(.78f, .82f, .92f),
            Ambre = new Color(1f, .78f, .35f), Rouge = new Color(1f, .42f, .42f), Gris = new Color(.62f, .62f, .72f);

        class Noeud { public Poste poste; public RectTransform marqueur, etiquette; public UI.Text texte; public UI.Outline contour; public UI.Image fond; public Vector2 pos; }
        class Lien { public Poste source, dest; public RectTransform ligne, fleche; public UI.Image img, imgFleche; public UI.Text badge; public Vector2 a, b; }
        class Portrait { public Ouvrier o; public RectTransform rect; public UI.Image anneau; public UI.Text legende; }

        Font police;
        Sprite rond;
        RectTransform panneau, carte, calqueLiens, calqueNoeuds, calqueOuvriers, barre, jeton, bulle;
        UI.Text titre, conseil, textePont, texteSelection, survol, texteJeton, texteBulle;
        UI.RawImage fondCarte;
        Camera camCarte;
        RenderTexture rt;
        Vector2 minMonde, centreVue;
        float tailleMonde = 200f, tailleVue = 200f;
        bool construite, fogSauve;
        readonly List<Noeud> noeuds = new List<Noeud>();
        readonly List<Lien> liens = new List<Lien>();
        readonly Dictionary<Ouvrier, Portrait> portraits = new Dictionary<Ouvrier, Portrait>();
        readonly Dictionary<Ouvrier, RectTransform> jetons = new Dictionary<Ouvrier, RectTransform>();
        RectTransform repereAldric;
        Ouvrier glisse;
        Poste selection;
        bool deplacement;
        Vector2 sourisAvant;
        int choixClavier;

        public bool Ouverte => panneau != null && panneau.gameObject.activeSelf;

        // ---------- construction ----------

        public void Construire(RectTransform toile, Font p)
        {
            police = p;
            rond = SpriteRond();
            panneau = Bloc("CarteChantier", toile, new Vector2(.5f, .5f), new Vector2(1740, 960), Vector2.zero, Fond);
            var cadre = panneau.gameObject.AddComponent<UI.Outline>();
            cadre.effectColor = new Color(Argent.r, Argent.g, Argent.b, .55f); cadre.effectDistance = new Vector2(3, -3);

            // La carte (à gauche)
            var bord = Bloc("CadreCarte", panneau, new Vector2(0, .5f), new Vector2(TailleCarte + 6, TailleCarte + 6), new Vector2(28 + TailleCarte / 2f, -14), Argent);
            bord.GetComponent<UI.Image>().color = new Color(Argent.r, Argent.g, Argent.b, .7f);
            carte = new GameObject("Carte", typeof(RectTransform)).GetComponent<RectTransform>();
            carte.SetParent(panneau, false);
            carte.anchorMin = carte.anchorMax = new Vector2(0, .5f);
            carte.pivot = new Vector2(.5f, .5f);
            carte.sizeDelta = new Vector2(TailleCarte, TailleCarte);
            carte.anchoredPosition = new Vector2(28 + TailleCarte / 2f, -14);
            fondCarte = carte.gameObject.AddComponent<UI.RawImage>();
            fondCarte.raycastTarget = false;
            fondCarte.color = new Color(.92f, .92f, 1f);
            carte.gameObject.AddComponent<UI.RectMask2D>();
            calqueLiens = Calque("Liens", carte);
            calqueNoeuds = Calque("Noeuds", carte);
            calqueOuvriers = Calque("Ouvriers", carte);
            Texte("AideCarte", carte, 13, new Color(1, 1, 1, .8f), TextAnchor.LowerLeft, new Vector2(10, -TailleCarte + 26), new Vector2(600, 20))
                .text = "Molette : zoom · glisser le fond : déplacer · clic sur un poste : détail";

            // Colonne de droite
            float xd = 28 + TailleCarte + 34;
            titre = Texte("Titre", panneau, 24, Ambre, TextAnchor.UpperLeft, new Vector2(xd, -24), new Vector2(780, 36));
            titre.fontStyle = FontStyle.Bold;

            var boite = Bloc("Conseil", panneau, new Vector2(0, 1), new Vector2(790, 104), new Vector2(xd + 395, -118), new Color(.1f, .12f, .22f, 1f));
            var portraitOdon = Bloc("Odon", boite, new Vector2(0, .5f), new Vector2(62, 62), new Vector2(44, 0), new Color(.48f, .42f, .35f));
            portraitOdon.GetComponent<UI.Image>().sprite = rond;
            Texte("O", portraitOdon, 28, Color.white, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(62, 62), true).fontStyle = FontStyle.Bold;
            conseil = Texte("Texte", boite, 17, Color.white, TextAnchor.MiddleLeft, new Vector2(88, -8), new Vector2(690, 88));

            textePont = Texte("Pont", panneau, 16, Color.white, TextAnchor.UpperLeft, new Vector2(xd, -186), new Vector2(790, 190));
            texteSelection = Texte("Selection", panneau, 16, Color.white, TextAnchor.UpperLeft, new Vector2(xd, -384), new Vector2(790, 190));

            var legende = Texte("Legende", panneau, 14, Color.white, TextAnchor.UpperLeft, new Vector2(xd, -590), new Vector2(790, 44));
            legende.text = "<color=#ffc85a>━━ porteurs au travail</color>     <color=#ff7070>━━ il manque un porteur</color>     <color=#c8c8dc>── liaison possible</color>\n" +
                           "<color=#c8c8dc>Pastilles : les villageois, en direct  ·  ◆ blanc : toi</color>";

            barre = Bloc("Villageois", panneau, new Vector2(0, 0), new Vector2(790, 196), new Vector2(xd + 395, 156), new Color(.1f, .12f, .22f, 1f));
            Texte("TitreBarre", barre, 15, Ambre, TextAnchor.UpperLeft, new Vector2(14, -8), new Vector2(760, 22)).text = "<b>VILLAGEOIS</b> — glisse un portrait sur un poste ou sur une flèche";

            Texte("Aide", panneau, 14, new Color(.8f, .8f, .9f), TextAnchor.LowerLeft, new Vector2(xd, -902), new Vector2(790, 40)).text =
                "Dans la barre : il se repose.  G : donner 10 mana au poste choisi.  Tab : fermer.\nManette : gauche / droite pour choisir, haut / bas pour changer l'affectation.";

            bulle = Bloc("Bulle", panneau, new Vector2(0, 1), new Vector2(330, 120), Vector2.zero, new Color(.04f, .05f, .1f, .95f));
            bulle.pivot = new Vector2(0, 1);
            var cb = bulle.gameObject.AddComponent<UI.Outline>(); cb.effectColor = new Color(Ambre.r, Ambre.g, Ambre.b, .6f);
            texteBulle = Texte("Texte", bulle, 14, Color.white, TextAnchor.UpperLeft, new Vector2(10, -8), new Vector2(310, 110));
            survol = Texte("Survol", panneau, 16, Color.white, TextAnchor.LowerLeft, Vector2.zero, new Vector2(520, 50));
            jeton = Bloc("Jeton", panneau, new Vector2(0, 1), new Vector2(50, 50), Vector2.zero, Color.white);
            jeton.GetComponent<UI.Image>().sprite = rond;
            texteJeton = Texte("Initiale", jeton, 20, Color.white, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(50, 50), true);
            texteJeton.fontStyle = FontStyle.Bold;
            jeton.gameObject.SetActive(false);
            survol.gameObject.SetActive(false);
            bulle.gameObject.SetActive(false);
            panneau.gameObject.SetActive(false);
        }

        void PremiereOuverture()
        {
            construite = true;
            Vector2 mn = new Vector2(float.MaxValue, float.MaxValue), mx = new Vector2(float.MinValue, float.MinValue);
            foreach (var p in Poste.Tous)
            {
                var v = new Vector2(p.transform.position.x, p.transform.position.z);
                mn = Vector2.Min(mn, v); mx = Vector2.Max(mx, v);
            }
            if (Poste.Tous.Count == 0) { mn = Monde.Village - Vector2.one * 80; mx = Monde.Village + Vector2.one * 80; }
            centreVue = (mn + mx) / 2f;
            tailleMonde = Mathf.Max(mx.x - mn.x, mx.y - mn.y) + 70f;
            tailleVue = tailleMonde;
            minMonde = centreVue - Vector2.one * tailleMonde / 2f;

            rt = new RenderTexture(400, 400, 24) { filterMode = FilterMode.Point, name = "CarteChantier" };
            var go = new GameObject("CameraCarte");
            camCarte = go.AddComponent<Camera>();
            camCarte.orthographic = true;
            camCarte.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            camCarte.nearClipPlane = 10f; camCarte.farClipPlane = 900f;
            int sceau = LayerMask.NameToLayer("Sceau");
            camCarte.cullingMask = ~((sceau >= 0 ? 1 << sceau : 0) | (1 << 5));
            camCarte.clearFlags = CameraClearFlags.SolidColor;
            camCarte.backgroundColor = new Color(.2f, .24f, .2f);
            camCarte.targetTexture = rt;
            camCarte.enabled = false;
            fondCarte.texture = rt;

            foreach (var p in Poste.Tous)
            {
                var n = new Noeud { poste = p };
                n.marqueur = Bloc("Repere_" + p.id, calqueNoeuds, new Vector2(.5f, .5f), new Vector2(16, 16), Vector2.zero, Ambre);
                n.marqueur.localRotation = Quaternion.Euler(0, 0, 45f);
                n.etiquette = Bloc("Etiquette_" + p.id, calqueNoeuds, new Vector2(.5f, .5f), new Vector2(120, 24), Vector2.zero, new Color(.05f, .06f, .12f, .88f));
                n.etiquette.pivot = new Vector2(.5f, 0);
                n.fond = n.etiquette.GetComponent<UI.Image>();
                n.contour = n.etiquette.gameObject.AddComponent<UI.Outline>();
                n.contour.effectDistance = new Vector2(2, -2);
                n.texte = Texte("Texte", n.etiquette, 14, Color.white, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(300, 24), true);
                noeuds.Add(n);
            }

            foreach (var s in Poste.Tous)
                foreach (var d in Poste.Tous)
                {
                    if (s == d) continue;
                    bool possible = (s.EstFour || s.produit != Ressource.Aucune) && (d.EstChantier || d.EstFour) &&
                                    (d.Accepte(s.EstFour ? Ressource.Chaux : s.produit) || (s.EstFour && d.EstChantier));
                    if (!possible) continue;
                    var l = new Lien { source = s, dest = d };
                    l.ligne = Bloc("Lien", calqueLiens, new Vector2(.5f, .5f), new Vector2(10, 3), Vector2.zero, Gris);
                    l.ligne.pivot = new Vector2(0, .5f);
                    l.img = l.ligne.GetComponent<UI.Image>();
                    l.fleche = Bloc("Fleche", calqueLiens, new Vector2(.5f, .5f), new Vector2(12, 12), Vector2.zero, Gris);
                    l.imgFleche = l.fleche.GetComponent<UI.Image>();
                    l.badge = Texte("Badge", calqueLiens, 12, Ambre, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(200, 20), true);
                    liens.Add(l);
                }

            repereAldric = Bloc("Aldric", calqueOuvriers, new Vector2(.5f, .5f), new Vector2(14, 14), Vector2.zero, Color.white);
            repereAldric.localRotation = Quaternion.Euler(0, 0, 45f);
        }

        void CreerPortrait(Ouvrier o)
        {
            var h = o.GetComponent<Habitant>();
            Color c = h != null ? h.tunique : Gris;
            var pr = new Portrait { o = o };
            pr.rect = Bloc("Portrait_" + o.nom, barre, new Vector2(0, 1), new Vector2(80, 150), Vector2.zero, new Color(0, 0, 0, 0));
            pr.anneau = Bloc("Anneau", pr.rect, new Vector2(.5f, 1), new Vector2(62, 62), new Vector2(0, -38), new Color(0, 0, 0, 0)).GetComponent<UI.Image>();
            pr.anneau.sprite = rond;
            var r = Bloc("Rond", pr.rect, new Vector2(.5f, 1), new Vector2(54, 54), new Vector2(0, -38), c);
            r.GetComponent<UI.Image>().sprite = rond;
            var ini = Texte("Initiale", r, 22, Color.white, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(54, 54), true);
            ini.text = o.nom.Substring(0, 1); ini.fontStyle = FontStyle.Bold;
            pr.legende = Texte("Legende", pr.rect, 12, Color.white, TextAnchor.UpperCenter, new Vector2(-6, -68), new Vector2(92, 80));
            portraits[o] = pr;

            var j = Bloc("Jeton_" + o.nom, calqueOuvriers, new Vector2(.5f, .5f), new Vector2(20, 20), Vector2.zero, c);
            j.GetComponent<UI.Image>().sprite = rond;
            var tj = Texte("I", j, 12, Color.white, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(20, 20), true);
            tj.text = o.nom.Substring(0, 1); tj.fontStyle = FontStyle.Bold;
            jetons[o] = j;
        }

        // ---------- ouverture ----------

        public void Ouvrir()
        {
            if (!construite) PremiereOuverture();
            panneau.gameObject.SetActive(true);
            if (camCarte != null) camCarte.enabled = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Fermer(bool verrouiller = true)
        {
            if (panneau != null) panneau.gameObject.SetActive(false);
            if (camCarte != null) camCarte.enabled = false;
            glisse = null; deplacement = false;
            if (jeton != null) jeton.gameObject.SetActive(false);
            if (!verrouiller) return;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += AvantRendu;
            RenderPipelineManager.endCameraRendering += ApresRendu;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= AvantRendu;
            RenderPipelineManager.endCameraRendering -= ApresRendu;
        }

        // Pas de brouillard sur la carte (la caméra est haut au-dessus du sol)
        void AvantRendu(ScriptableRenderContext c, Camera cam) { if (cam == camCarte) { fogSauve = RenderSettings.fog; RenderSettings.fog = false; } }
        void ApresRendu(ScriptableRenderContext c, Camera cam) { if (cam == camCarte) RenderSettings.fog = fogSauve; }

        // ---------- vue : zoom et déplacement ----------

        Vector2 VersCarte(Vector3 w) =>
            new Vector2((w.x - centreVue.x) / tailleVue, (w.z - centreVue.y) / tailleVue) * TailleCarte;

        Vector2 VersMonde(Vector2 c) => centreVue + c / TailleCarte * tailleVue;

        void Vue(Vector2 souris)
        {
            var m = Mouse.current;
            if (m != null && Dedans(carte) && glisse == null)
            {
                float molette = m.scroll.ReadValue().y;
                if (Mathf.Abs(molette) > .01f)
                {
                    Vector2 avant = VersMonde(souris);
                    tailleVue = Mathf.Clamp(tailleVue * (molette > 0 ? .82f : 1.22f), 35f, tailleMonde);
                    centreVue += avant - VersMonde(souris);   // le point sous le curseur ne bouge pas
                }
                if (m.rightButton.wasPressedThisFrame || m.middleButton.wasPressedThisFrame) { deplacement = true; sourisAvant = souris; }
            }
            if (m != null && deplacement)
            {
                bool tenu = m.rightButton.isPressed || m.middleButton.isPressed || m.leftButton.isPressed;
                if (!tenu) deplacement = false;
                else { centreVue -= (souris - sourisAvant) / TailleCarte * tailleVue; sourisAvant = souris; }
            }
            float marge = tailleMonde / 2f - tailleVue / 2f + 20f;
            Vector2 milieu = minMonde + Vector2.one * tailleMonde / 2f;
            centreVue = new Vector2(Mathf.Clamp(centreVue.x, milieu.x - marge, milieu.x + marge), Mathf.Clamp(centreVue.y, milieu.y - marge, milieu.y + marge));
            camCarte.orthographicSize = tailleVue / 2f;
            camCarte.transform.position = new Vector3(centreVue.x, 220f, centreVue.y);
        }

        // ---------- mise à jour ----------

        void Update()
        {
            if (!Ouverte) return;
            foreach (var o in Ouvrier.Tous) if (!portraits.ContainsKey(o)) CreerPortrait(o);

            Vector2 souris = Souris(carte);
            Vector2 sourisPanneau = Souris(panneau);
            Vue(souris);

            titre.text = $"REGISTRE DU CHANTIER   ·   Jour {Chantier.Jour}, {(int)Chantier.Heure:00} h" + (Chantier.EstNuit ? "   ·   la nuit, on dort" : "");
            conseil.text = $"<color=#ffd27a><b>Odon</b></color>   « {Chantier.Conseil()} »";
            textePont.text = HUD.TextePont();

            var besoin = Chantier.LiaisonsManquantes();
            Noeud noeudSurvol = null; Lien lienSurvol = null;
            bool surBarre = glisse != null && Dedans(barre);

            // Postes
            foreach (var n in noeuds)
            {
                var p = n.poste;
                n.pos = VersCarte(p.transform.position);
                n.marqueur.anchoredPosition = n.pos;
                n.etiquette.anchoredPosition = n.pos + new Vector2(0, 12);
                bool dispo = p.Disponible;
                bool alerte = dispo && p.blocage != "" && p.blocage != "Épuisé";
                n.texte.text = Etiquette(p);
                n.etiquette.sizeDelta = new Vector2(n.texte.preferredWidth + 16, 24);
                if (!surBarre && noeudSurvol == null && (Dedans(n.etiquette) || Vector2.Distance(souris, n.pos) < 14f)) noeudSurvol = n;
                n.contour.effectColor = n.poste == selection ? Ambre : alerte ? Color.Lerp(Rouge, Color.white, Mathf.PingPong(Time.unscaledTime * 2f, 1f) * .3f) : new Color(0, 0, 0, 0);
                n.marqueur.GetComponent<UI.Image>().color = !dispo ? Gris : alerte ? Rouge : p.boost > 0 ? new Color(.55f, .82f, 1f) : Ambre;
                n.fond.color = new Color(.05f, .06f, .12f, dispo ? .9f : .6f);
            }
            if (glisse != null && noeudSurvol == null && !surBarre)
            {
                float meilleur = 14f;
                foreach (var l in liens)
                {
                    if (!l.source.Disponible || !l.dest.Disponible) continue;
                    float d = DistanceSegment(souris, l.a, l.b);
                    if (d < meilleur) { meilleur = d; lienSurvol = l; }
                }
            }

            // Flèches
            foreach (var l in liens)
            {
                Vector2 a = VersCarte(l.source.transform.position), b = VersCarte(l.dest.transform.position), u = (b - a).normalized;
                Vector2 decal = new Vector2(-u.y, u.x) * 4f;
                l.a = a + u * 10f + decal; l.b = b - u * 12f + decal;
                l.ligne.anchoredPosition = l.a;
                l.ligne.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(u.y, u.x) * Mathf.Rad2Deg);
                l.fleche.anchoredPosition = Vector2.Lerp(l.a, l.b, .62f);
                l.fleche.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(u.y, u.x) * Mathf.Rad2Deg + 45f);
                ((RectTransform)l.badge.transform).anchoredPosition = Vector2.Lerp(l.a, l.b, .5f) + decal * 3.5f;

                int porteurs = 0;
                foreach (var o in Ouvrier.Tous)
                    if (o.Disponible && o.affectation.type == TypeAffectation.Porteur && o.affectation.source == l.source && o.affectation.destination == l.dest) porteurs++;
                bool visible = l.source.Disponible && l.dest.Disponible;
                bool manque = visible && porteurs == 0 && besoin.Contains((l.source, l.dest));
                Color c; float ep;
                if (!visible) { c = new Color(0, 0, 0, 0); ep = 0; }
                else if (l == lienSurvol) { c = Color.white; ep = 6; }
                else if (porteurs > 0) { c = Ambre; ep = 2 + 2 * porteurs; }
                else if (manque) { c = new Color(Rouge.r, Rouge.g, Rouge.b, .55f + .45f * Mathf.PingPong(Time.unscaledTime * 1.6f, 1f)); ep = 3; }
                else { c = new Color(Gris.r, Gris.g, Gris.b, glisse != null ? .65f : .25f); ep = 1.5f; }
                l.img.color = c; l.imgFleche.color = c;
                l.ligne.sizeDelta = new Vector2((l.b - l.a).magnitude, ep);
                l.fleche.sizeDelta = Vector2.one * (7 + ep * 1.4f);
                Ressource r = l.source.EstFour ? Ressource.Chaux : l.source.produit;
                bool proche = tailleVue < tailleMonde * .75f || porteurs > 0 || manque;
                l.badge.text = !visible || !proche ? "" : porteurs > 0 ? $"{porteurs} × {Ouvrier.NomRessource(r, 2)}" : manque ? "<color=#ff9a9a>porteur ?</color>" : "";
            }

            // Villageois et toi, en direct
            foreach (var kv in jetons)
            {
                bool dispo = kv.Key.Disponible;
                kv.Value.gameObject.SetActive(dispo);
                if (dispo) kv.Value.anchoredPosition = VersCarte(kv.Key.transform.position);
            }
            var joueur = Camera.main != null ? Camera.main.transform.root : null;
            if (joueur != null) repereAldric.anchoredPosition = VersCarte(joueur.position);

            // Portraits
            var dispos = new List<Portrait>();
            foreach (var pr in portraits.Values)
            {
                bool d = pr.o.Disponible;
                pr.rect.gameObject.SetActive(d);
                if (!d) continue;
                pr.rect.anchoredPosition = new Vector2(50 + dispos.Count * 86, -104);
                dispos.Add(pr);
                bool attente = pr.o.Etat.StartsWith("attend") || pr.o.Etat.StartsWith("rien");
                pr.legende.text = $"<b>{pr.o.nom}</b>\n{Court(pr.o.affectation)}" + (attente ? "\n<color=#ff8a8a>bloqué</color>" : "");
            }

            // Affectation ciblée pendant un glisser
            Affectation? cible = null;
            if (glisse != null)
            {
                if (noeudSurvol != null) cible = new Affectation { type = TypeAffectation.Poste, poste = noeudSurvol.poste };
                else if (lienSurvol != null) cible = new Affectation { type = TypeAffectation.Porteur, source = lienSurvol.source, destination = lienSurvol.dest };
                else if (surBarre) cible = new Affectation { type = TypeAffectation.Aucune };
            }

            Clavier(dispos);
            Glisser(dispos, cible, sourisPanneau, noeudSurvol);

            // Bulle de détail au survol, et poste choisi à droite
            bulle.gameObject.SetActive(glisse == null && !deplacement && noeudSurvol != null);
            if (bulle.gameObject.activeSelf)
            {
                texteBulle.text = TexteFiche(noeudSurvol.poste);
                bulle.sizeDelta = new Vector2(330, texteBulle.preferredHeight + 18);
                bulle.anchoredPosition = sourisPanneau + new Vector2(panneau.rect.width / 2f + 18f, -panneau.rect.height / 2f - 18f);
            }
            texteSelection.text = selection == null
                ? "<color=#9a9ab4>Clique sur un poste de la carte pour voir sa recette et lui donner du mana.</color>"
                : $"<color=#ffd27a><b>POSTE CHOISI</b></color>\n{TexteFiche(selection)}\n" + (selection.boost > 0
                    ? $"<color=#8fd0ff>Mana donné : travail ×2 encore {selection.boost:0} s</color>"
                    : $"<color=#8fd0ff>G : donner {Poste.DonMana:0} mana à cette équipe (×2 pendant une demi-journée)</color>");
            var kb = Keyboard.current;
            if (selection != null && kb != null && kb.gKey.wasPressedThisFrame)
            {
                var j = FindFirstObjectByType<JoueurAldric>();
                if (j != null) j.DonnerMana(selection);
            }
        }

        void Glisser(List<Portrait> dispos, Affectation? cible, Vector2 sourisPanneau, Noeud noeudSurvol)
        {
            var m = Mouse.current;
            if (m == null) return;
            if (m.leftButton.wasPressedThisFrame && glisse == null)
            {
                foreach (var pr in dispos) if (Dedans(pr.rect)) { glisse = pr.o; break; }
                if (glisse == null)
                    foreach (var kv in jetons) if (kv.Value.gameObject.activeSelf && Dedans(kv.Value)) { glisse = kv.Key; break; }
                if (glisse != null)
                {
                    var h = glisse.GetComponent<Habitant>();
                    jeton.GetComponent<UI.Image>().color = h != null ? h.tunique : Gris;
                    texteJeton.text = glisse.nom.Substring(0, 1);
                    Sons.Jouer("clic", .4f, 1.3f);
                }
                else if (noeudSurvol != null) { selection = noeudSurvol.poste; Sons.Jouer("clic", .4f, 1.1f); }
                else if (Dedans(carte)) { deplacement = true; sourisAvant = Souris(carte); }
            }
            jeton.gameObject.SetActive(glisse != null);
            survol.gameObject.SetActive(glisse != null);
            if (glisse == null) return;

            jeton.anchoredPosition = sourisPanneau + new Vector2(panneau.rect.width / 2f, -panneau.rect.height / 2f);
            ((RectTransform)survol.transform).anchoredPosition = sourisPanneau + new Vector2(panneau.rect.width / 2f + 34f, -panneau.rect.height / 2f + 64f);

            bool valide = false;
            string message = $"{glisse.nom} : lâche-le sur un poste ou une flèche";
            if (cible.HasValue)
            {
                foreach (var opt in glisse.Options()) if (opt.Egale(cible.Value)) { valide = true; break; }
                message = valide ? Phrase(glisse, cible.Value) : Refus(glisse, cible.Value);
            }
            survol.text = $"<color={(valide ? "#ffffff" : "#ff8a8a")}>{message}</color>";

            if (m.leftButton.wasReleasedThisFrame)
            {
                if (valide) { glisse.Affecter(cible.Value); Sons.Jouer("clic", .6f, 1f); }
                glisse = null;
            }
        }

        void Clavier(List<Portrait> dispos)
        {
            if (dispos.Count == 0) return;
            var kb = Keyboard.current; var pad = Gamepad.current;
            bool Appui(Key a, System.Func<Gamepad, UnityEngine.InputSystem.Controls.ButtonControl> g) =>
                (kb != null && kb[a].wasPressedThisFrame) || (pad != null && g(pad).wasPressedThisFrame);
            if (Appui(Key.LeftArrow, g => g.dpad.left)) choixClavier--;
            if (Appui(Key.RightArrow, g => g.dpad.right)) choixClavier++;
            choixClavier = (choixClavier % dispos.Count + dispos.Count) % dispos.Count;
            int sens = Appui(Key.UpArrow, g => g.dpad.up) ? -1 : Appui(Key.DownArrow, g => g.dpad.down) ? 1 : 0;
            var choisi = dispos[choixClavier].o;
            if (sens != 0)
            {
                var options = choisi.Options();
                int i = options.FindIndex(x => x.Egale(choisi.affectation));
                choisi.Affecter(options[((i < 0 ? 0 : i) + sens + options.Count) % options.Count]);
                Sons.Jouer("clic", .5f);
            }
            for (int k = 0; k < dispos.Count; k++)
                dispos[k].anneau.color = new Color(Ambre.r, Ambre.g, Ambre.b, k == choixClavier ? .6f : 0f);
        }

        // ---------- textes ----------

        /// <summary>Étiquette courte sur la carte : nom, stock principal, ouvriers.</summary>
        static string Etiquette(Poste p)
        {
            if (!p.Disponible) return $"<color=#9a9ab4>{p.nom} ?</color>";
            string stock;
            if (p.EstChantier) stock = Chantier.Etape >= 3 ? "ouvert" : $"étape {Chantier.Etape + 1}/3";
            else if (p.EstFour && !Chantier.FourRepare) stock = $"réparation {Chantier.ReparationFour / Chantier.FourJours * 100f:0} %";
            else if (p.EstFour) stock = $"{Mathf.Floor(p.Stock(Ressource.Chaux)):0} chaux";
            else stock = $"{Mathf.Floor(p.Stock(p.produit)):0} {Court(p.produit)}";
            string alerte = p.blocage != "" && p.blocage != "Épuisé" ? " <color=#ff8a8a>!</color>" : "";
            string mana = p.boost > 0 ? " <color=#8fd0ff>×2</color>" : "";
            return $"<b>{p.nom.Replace("Chantier du pont", "Pont")}</b> · {stock} · {p.personnes:0.#}/{p.maxPersonnes}{mana}{alerte}";
        }

        static string Court(Ressource r) => r switch
        {
            Ressource.Pierre => "blocs",
            Ressource.Bois => "poutres",
            Ressource.Calcaire => "calcaire",
            Ressource.Chaux => "chaux",
            _ => "",
        };

        static string TexteFiche(Poste p)
        {
            if (!p.Disponible)
                return $"<b>{p.nom}</b>\n<color=#9a9ab4>À découvrir avec le Sceau (E).</color>";
            string recette = p.id switch
            {
                "riviere" => "Sortir les blocs de l'eau : 4 blocs par personne et par jour.",
                "carriere" => "Tailler la roche, loin du pont : 2 blocs par personne et par jour.",
                "foret" => "Abattre et ébrancher : 5 poutres par personne et par jour.",
                "talus" => "Casser le calcaire : 4 par personne et par jour.",
                "four" when !Chantier.FourRepare => "À réparer : 4 blocs, puis 3 jours de travail (Odon compte double).",
                "four" => "2 calcaire + 1 poutre donnent 2 sacs de chaux, le mortier des piles.",
                "chantier" when Chantier.Etape < 3 => ChantierRecette(),
                _ => "Le pont est ouvert.",
            };
            string stock;
            if (p.EstChantier) stock = $"Reçu : {Mathf.Floor(p.Stock(Ressource.Pierre)):0} blocs, {Mathf.Floor(p.Stock(Ressource.Chaux)):0} chaux, {Mathf.Floor(p.Stock(Ressource.Bois)):0} poutres";
            else if (p.EstFour && !Chantier.FourRepare) stock = $"Blocs reçus : {Mathf.Floor(p.Stock(Ressource.Pierre)):0}/4";
            else if (p.EstFour) stock = $"Chaux {Mathf.Floor(p.Stock(Ressource.Chaux)):0} · calcaire {Mathf.Floor(p.Stock(Ressource.Calcaire)):0} · bois {Mathf.Floor(p.Stock(Ressource.Bois)):0}";
            else stock = $"Stock : {Mathf.Floor(p.Stock(p.produit)):0} {Court(p.produit)}" + (p.reserve >= 0 ? $" (reste {Mathf.Max(0, p.reserve - p.extrait):0})" : "");
            string alerte = p.blocage != "" && p.blocage != "Épuisé" ? $"\n<color=#ff8a8a>⚠ {p.blocage}</color>" : "";
            return $"<color=#ffd27a><b>{p.nom}</b></color>  <color=#9a9ab4>{p.personnes:0.#} au travail / {p.maxPersonnes}</color>\n{recette}\n{stock}{alerte}";
        }

        static string ChantierRecette()
        {
            var b = Chantier.Etapes[Chantier.Etape];
            var l = new List<string>();
            if (b.pierre > 0) l.Add($"{b.pierre:0} blocs");
            if (b.chaux > 0) l.Add($"{b.chaux:0} sacs de chaux (le mortier)");
            if (b.bois > 0) l.Add($"{b.bois:0} poutres");
            return $"Étape {Chantier.Etape + 1} — {b.nom} : {string.Join(" + ", l)}, et des bâtisseurs.";
        }

        static string Court(Affectation a) => a.type switch
        {
            TypeAffectation.Poste => a.poste.nom.Replace("Chantier du pont", "Pont"),
            TypeAffectation.Porteur => $"{a.source.nom.Replace("Chantier du pont", "Pont")} → {a.destination.nom.Replace("Chantier du pont", "Pont")}",
            _ => "au repos",
        };

        static string Phrase(Ouvrier o, Affectation a) => a.type switch
        {
            TypeAffectation.Poste => $"{o.nom} travaillera à : {a.poste.nom}",
            TypeAffectation.Porteur => $"{o.nom} portera {Ouvrier.NomRessource(a.source.Fournit == Ressource.Aucune ? Ressource.Chaux : a.source.Fournit, 2)} de {a.source.nom} à {a.destination.nom}",
            _ => $"{o.nom} se repose",
        };

        static string Refus(Ouvrier o, Affectation a)
        {
            if (a.type == TypeAffectation.Porteur && !o.porte) return $"{o.nom} ne peut pas porter (main brisée)";
            if (a.type == TypeAffectation.Porteur && a.source.EstFour && !Chantier.FourRepare) return "Le four ne donne rien tant qu'il n'est pas réparé";
            if (a.type == TypeAffectation.Poste && !a.poste.Disponible) return "Ce poste reste à découvrir avec le Sceau";
            if (a.type == TypeAffectation.Poste && !o.porte) return $"{o.nom} bâtit, mais ne peut pas faire ce travail";
            return "Impossible ici";
        }

        // ---------- outils ----------

        static Vector2 Souris(RectTransform r)
        {
            var m = Mouse.current;
            if (m == null) return Vector2.zero;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(r, m.position.ReadValue(), null, out var local);
            return local;
        }

        static bool Dedans(RectTransform r)
        {
            var m = Mouse.current;
            return m != null && r.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(r, m.position.ReadValue(), null);
        }

        static float DistanceSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
            return Vector2.Distance(p, a + t * ab);
        }

        static RectTransform Calque(string nom, RectTransform parent)
        {
            var r = new GameObject(nom, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
            r.sizeDelta = Vector2.zero;
            return r;
        }

        static RectTransform Bloc(string nom, RectTransform parent, Vector2 ancre, Vector2 taille, Vector2 pos, Color couleur)
        {
            var r = new GameObject(nom, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = ancre;
            r.pivot = new Vector2(.5f, .5f);
            r.sizeDelta = taille;
            r.anchoredPosition = pos;
            var i = r.gameObject.AddComponent<UI.Image>();
            i.color = couleur; i.raycastTarget = false;
            return r;
        }

        UI.Text Texte(string nom, RectTransform parent, int taille, Color couleur, TextAnchor alignement, Vector2 pos, Vector2 dimension, bool centre = false)
        {
            var r = new GameObject(nom, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = centre ? new Vector2(.5f, .5f) : new Vector2(0, 1);
            r.pivot = centre ? new Vector2(.5f, .5f) : new Vector2(0, 1);
            r.sizeDelta = dimension;
            r.anchoredPosition = pos;
            var t = r.gameObject.AddComponent<UI.Text>();
            t.font = police; t.fontSize = taille; t.color = couleur; t.alignment = alignement;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false; t.supportRichText = true;
            var o = r.gameObject.AddComponent<UI.Shadow>();
            o.effectColor = new Color(0, 0, 0, .7f);
            return t;
        }

        static Sprite SpriteRond()
        {
            const int S = 64;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { name = "Rond", filterMode = FilterMode.Point };
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(S / 2f, S / 2f));
                    t.SetPixel(x, y, new Color(1, 1, 1, d <= S / 2f - 1 ? 1 : 0));
                }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(.5f, .5f));
        }
    }
}
