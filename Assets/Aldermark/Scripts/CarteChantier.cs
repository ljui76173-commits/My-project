using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UI = UnityEngine.UI;

namespace Aldermark
{
    /// <summary>
    /// Carte du chantier (touche Tab) : la vraie vallée vue d'en haut, en pixels, avec les postes,
    /// leur recette, les flèches de transport et les villageois en direct.
    /// On glisse un portrait sur un poste (il y travaille) ou sur une flèche (il devient porteur).
    /// Odon donne en haut le blocage du moment.
    /// </summary>
    public class CarteChantier : MonoBehaviour
    {
        const float TailleCarte = 860f;
        static readonly Color Fond = new Color(.045f, .05f, .1f, .96f), Argent = new Color(.78f, .82f, .92f),
            Ambre = new Color(1f, .78f, .35f), Rouge = new Color(1f, .42f, .42f), Gris = new Color(.62f, .62f, .72f);

        class Noeud { public Poste poste; public RectTransform carte, marqueur, attache; public UI.Text texte; public UI.Image cadre; public Vector2 pos, posCarte; }
        class Lien { public Poste source, dest; public RectTransform ligne, fleche; public UI.Image img, imgFleche; public UI.Text badge; public Vector2 a, b; }
        class Portrait { public Ouvrier o; public RectTransform rect; public UI.Image rond, anneau; public UI.Text legende; }

        Font police;
        Sprite rond;
        RectTransform panneau, carte, calqueLiens, calqueNoeuds, calqueOuvriers, barre, jeton;
        UI.Text titre, conseil, textePont, survol, texteJeton;
        UI.RawImage fondCarte;
        Camera camCarte;
        RenderTexture rt;
        Vector2 minMonde;
        float tailleMonde = 200f;
        bool construite, fogSauve;
        readonly List<Noeud> noeuds = new List<Noeud>();
        readonly List<Lien> liens = new List<Lien>();
        readonly List<Portrait> portraits = new List<Portrait>();
        readonly Dictionary<Ouvrier, RectTransform> jetons = new Dictionary<Ouvrier, RectTransform>();
        RectTransform repereAldric;
        Ouvrier glisse;
        int selection;

        public bool Ouverte => panneau != null && panneau.gameObject.activeSelf;

        // ---------- construction ----------

        public void Construire(RectTransform toile, Font p)
        {
            police = p;
            rond = SpriteRond();
            panneau = Bloc("CarteChantier", toile, new Vector2(.5f, .5f), new Vector2(1740, 960), Vector2.zero, Fond);
            Bloc("Cadre", panneau, new Vector2(.5f, .5f), new Vector2(1748, 968), Vector2.zero, new Color(Argent.r, Argent.g, Argent.b, .35f)).SetAsFirstSibling();

            // La carte (à gauche)
            Bloc("CadreCarte", panneau, new Vector2(0, .5f), new Vector2(TailleCarte + 8, TailleCarte + 8), new Vector2(28 + TailleCarte / 2f, -14), Argent);
            carte = new GameObject("Carte", typeof(RectTransform)).GetComponent<RectTransform>();
            carte.SetParent(panneau, false);
            carte.anchorMin = carte.anchorMax = new Vector2(0, .5f);
            carte.pivot = new Vector2(.5f, .5f);
            carte.sizeDelta = new Vector2(TailleCarte, TailleCarte);
            carte.anchoredPosition = new Vector2(28 + TailleCarte / 2f, -14);
            fondCarte = carte.gameObject.AddComponent<UI.RawImage>();
            fondCarte.raycastTarget = false;
            fondCarte.color = new Color(.9f, .9f, 1f);
            calqueLiens = Calque("Liens", carte);
            calqueNoeuds = Calque("Noeuds", carte);
            calqueOuvriers = Calque("Ouvriers", carte);

            // Colonne de droite
            float xd = 28 + TailleCarte + 34;
            titre = Texte("Titre", panneau, 26, Ambre, TextAnchor.UpperLeft, new Vector2(xd, -26), new Vector2(780, 40));
            titre.fontStyle = FontStyle.Bold;

            var boite = Bloc("Conseil", panneau, new Vector2(0, 1), new Vector2(790, 138), new Vector2(xd + 395, -138), new Color(.1f, .12f, .22f, .95f));
            var portraitOdon = Bloc("Odon", boite, new Vector2(0, .5f), new Vector2(76, 76), new Vector2(54, 0), new Color(.48f, .42f, .35f));
            portraitOdon.GetComponent<UI.Image>().sprite = rond;
            Texte("O", portraitOdon, 32, Color.white, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(76, 76), true).fontStyle = FontStyle.Bold;
            conseil = Texte("Texte", boite, 19, Color.white, TextAnchor.MiddleLeft, new Vector2(104, -14), new Vector2(670, 110));

            textePont = Texte("Pont", panneau, 18, Color.white, TextAnchor.UpperLeft, new Vector2(xd, -228), new Vector2(790, 300));

            var legende = Texte("Legende", panneau, 15, Gris, TextAnchor.UpperLeft, new Vector2(xd, -540), new Vector2(790, 60));
            legende.text = "<color=#ffc85a>━━ porteurs au travail</color>    <color=#ff6b6b>━━ il manque un porteur</color>    <color=#9a9ab4>── liaison possible</color>\n" +
                           "Pastilles sur la carte : les villageois, en direct. ▲ : toi.";

            barre = Bloc("Villageois", panneau, new Vector2(0, 0), new Vector2(790, 200), new Vector2(xd + 395, 160), new Color(.1f, .12f, .22f, .95f));
            Texte("TitreBarre", barre, 16, Ambre, TextAnchor.UpperLeft, new Vector2(14, -8), new Vector2(760, 24)).text = "<b>VILLAGEOIS</b> — glisse un portrait sur la carte";

            Texte("Aide", panneau, 16, Gris, TextAnchor.LowerLeft, new Vector2(xd, -900), new Vector2(790, 44)).text =
                "Sur un poste : il y travaille.  Sur une flèche : il devient porteur.  Dans cette barre : il se repose.\nTab : fermer   ·   manette : croix pour choisir, gauche / droite pour changer";

            survol = Texte("Survol", panneau, 17, Color.white, TextAnchor.LowerLeft, Vector2.zero, new Vector2(520, 60));
            jeton = Bloc("Jeton", panneau, new Vector2(0, 1), new Vector2(54, 54), Vector2.zero, Color.white);
            jeton.GetComponent<UI.Image>().sprite = rond;
            texteJeton = Texte("Initiale", jeton, 22, Color.white, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(54, 54), true);
            texteJeton.fontStyle = FontStyle.Bold;
            jeton.gameObject.SetActive(false);
            survol.gameObject.SetActive(false);
            panneau.gameObject.SetActive(false);
        }

        void PremiereOuverture()
        {
            construite = true;
            // Cadrage : tous les postes, avec une marge, dans un carré
            Vector2 mn = new Vector2(float.MaxValue, float.MaxValue), mx = new Vector2(float.MinValue, float.MinValue);
            foreach (var p in Poste.Tous)
            {
                var v = new Vector2(p.transform.position.x, p.transform.position.z);
                mn = Vector2.Min(mn, v); mx = Vector2.Max(mx, v);
            }
            if (Poste.Tous.Count == 0) { mn = Monde.Village - Vector2.one * 80; mx = Monde.Village + Vector2.one * 80; }
            Vector2 centre = (mn + mx) / 2f;
            tailleMonde = Mathf.Max(mx.x - mn.x, mx.y - mn.y) + 60f;
            minMonde = centre - Vector2.one * tailleMonde / 2f;

            // Caméra du dessus, rendu en basse définition (pixels nets)
            rt = new RenderTexture(380, 380, 24) { filterMode = FilterMode.Point, name = "CarteChantier" };
            var go = new GameObject("CameraCarte");
            camCarte = go.AddComponent<Camera>();
            camCarte.orthographic = true;
            camCarte.orthographicSize = tailleMonde / 2f;
            camCarte.transform.SetPositionAndRotation(new Vector3(centre.x, 220f, centre.y), Quaternion.Euler(90f, 0f, 0f));
            camCarte.nearClipPlane = 10f; camCarte.farClipPlane = 1200f;
            int sceau = LayerMask.NameToLayer("Sceau");
            camCarte.cullingMask = ~((sceau >= 0 ? 1 << sceau : 0) | (1 << 5));
            camCarte.clearFlags = CameraClearFlags.SolidColor;
            camCarte.backgroundColor = new Color(.2f, .24f, .2f);
            camCarte.targetTexture = rt;
            camCarte.enabled = false;
            fondCarte.texture = rt;

            // Postes : un repère sur la carte et une fiche à côté
            foreach (var p in Poste.Tous)
            {
                var n = new Noeud { poste = p, pos = VersCarte(p.transform.position) };
                n.marqueur = Bloc("Repere_" + p.id, calqueNoeuds, new Vector2(.5f, .5f), new Vector2(18, 18), n.pos, Ambre);
                n.marqueur.localRotation = Quaternion.Euler(0, 0, 45f);
                n.attache = Bloc("Attache", calqueNoeuds, new Vector2(.5f, .5f), new Vector2(2, 2), n.pos, new Color(Argent.r, Argent.g, Argent.b, .6f));
                n.attache.pivot = new Vector2(0, .5f);
                n.carte = Bloc("Fiche_" + p.id, calqueNoeuds, new Vector2(.5f, .5f), new Vector2(236, 112), n.pos, new Color(.06f, .07f, .14f, .9f));
                n.cadre = n.carte.GetComponent<UI.Image>();
                n.texte = Texte("Texte", n.carte, 15, Color.white, TextAnchor.UpperLeft, new Vector2(10, -6), new Vector2(216, 104));
                n.posCarte = n.pos + new Vector2(0, 76);
                noeuds.Add(n);
            }
            Ecarter();
            foreach (var n in noeuds)
            {
                n.carte.anchoredPosition = n.posCarte;
                Vector2 d = n.posCarte - n.pos;
                n.attache.anchoredPosition = n.pos;
                n.attache.sizeDelta = new Vector2(d.magnitude, 2);
                n.attache.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                n.attache.SetAsFirstSibling();
            }

            // Toutes les liaisons possibles
            foreach (var s in Poste.Tous)
                foreach (var d in Poste.Tous)
                {
                    if (s == d) continue;
                    bool possible = (s.EstFour || s.produit != Ressource.Aucune) && (d.EstChantier || d.EstFour) &&
                                    (d.Accepte(s.EstFour ? Ressource.Chaux : s.produit) || (s.EstFour && d.EstChantier));
                    if (!possible) continue;
                    var l = new Lien { source = s, dest = d };
                    Vector2 a = VersCarte(s.transform.position), b = VersCarte(d.transform.position), u = (b - a).normalized;
                    Vector2 decal = new Vector2(-u.y, u.x) * 5f;   // aller et retour côte à côte
                    l.a = a + u * 12f + decal; l.b = b - u * 14f + decal;
                    l.ligne = Bloc("Lien", calqueLiens, new Vector2(.5f, .5f), new Vector2((l.b - l.a).magnitude, 3), l.a, Gris);
                    l.ligne.pivot = new Vector2(0, .5f);
                    l.ligne.anchoredPosition = l.a;
                    l.ligne.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(u.y, u.x) * Mathf.Rad2Deg);
                    l.img = l.ligne.GetComponent<UI.Image>();
                    l.fleche = Bloc("Fleche", calqueLiens, new Vector2(.5f, .5f), new Vector2(12, 12), Vector2.Lerp(l.a, l.b, .62f), Gris);
                    l.fleche.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(u.y, u.x) * Mathf.Rad2Deg + 45f);
                    l.imgFleche = l.fleche.GetComponent<UI.Image>();
                    l.badge = Texte("Badge", calqueLiens, 14, Ambre, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(200, 22), true);
                    ((RectTransform)l.badge.transform).anchoredPosition = Vector2.Lerp(l.a, l.b, .5f) + decal * 3.2f;
                    liens.Add(l);
                }

            // Toi
            repereAldric = Bloc("Aldric", calqueOuvriers, new Vector2(.5f, .5f), new Vector2(14, 14), Vector2.zero, Color.white);
            repereAldric.localRotation = Quaternion.Euler(0, 0, 45f);

            // Portraits
            int i = 0;
            foreach (var o in Ouvrier.Tous)
            {
                var h = o.GetComponent<Habitant>();
                var pr = new Portrait { o = o };
                pr.rect = Bloc("Portrait_" + o.nom, barre, new Vector2(0, 1), new Vector2(84, 150), new Vector2(54 + i * 86, -110), new Color(0, 0, 0, 0));
                pr.anneau = Bloc("Anneau", pr.rect, new Vector2(.5f, 1), new Vector2(66, 66), new Vector2(0, -40), Ambre).GetComponent<UI.Image>();
                pr.anneau.sprite = rond;
                pr.rond = Bloc("Rond", pr.rect, new Vector2(.5f, 1), new Vector2(58, 58), new Vector2(0, -40), h != null ? h.tunique : Gris).GetComponent<UI.Image>();
                pr.rond.sprite = rond;
                var ini = Texte("Initiale", pr.rond.rectTransform, 24, Color.white, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(58, 58), true);
                ini.text = o.nom.Substring(0, 1); ini.fontStyle = FontStyle.Bold;
                pr.legende = Texte("Legende", pr.rect, 13, Color.white, TextAnchor.UpperCenter, new Vector2(-4, -74), new Vector2(92, 74));
                portraits.Add(pr);

                var j = Bloc("Jeton_" + o.nom, calqueOuvriers, new Vector2(.5f, .5f), new Vector2(22, 22), Vector2.zero, h != null ? h.tunique : Gris);
                j.GetComponent<UI.Image>().sprite = rond;
                var tj = Texte("I", j, 13, Color.white, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(22, 22), true);
                tj.text = o.nom.Substring(0, 1); tj.fontStyle = FontStyle.Bold;
                jetons[o] = j;
                i++;
            }
        }

        /// <summary>Écarte les fiches qui se chevauchent, en les gardant dans la carte.</summary>
        void Ecarter()
        {
            Vector2 taille = new Vector2(244, 120);
            for (int it = 0; it < 60; it++)
            {
                for (int a = 0; a < noeuds.Count; a++)
                    for (int b = a + 1; b < noeuds.Count; b++)
                    {
                        Vector2 d = noeuds[b].posCarte - noeuds[a].posCarte;
                        float ox = taille.x - Mathf.Abs(d.x), oy = taille.y - Mathf.Abs(d.y);
                        if (ox <= 0 || oy <= 0) continue;
                        Vector2 pousse = ox < oy ? new Vector2(Mathf.Sign(d.x == 0 ? 1 : d.x) * ox / 2f, 0) : new Vector2(0, Mathf.Sign(d.y == 0 ? 1 : d.y) * oy / 2f);
                        noeuds[a].posCarte -= pousse; noeuds[b].posCarte += pousse;
                    }
                float lim = TailleCarte / 2f;
                foreach (var n in noeuds)
                    n.posCarte = new Vector2(Mathf.Clamp(n.posCarte.x, -lim + 122, lim - 122), Mathf.Clamp(n.posCarte.y, -lim + 60, lim - 60));
            }
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
            glisse = null;
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

        // ---------- mise à jour ----------

        void Update()
        {
            if (!Ouverte) return;
            titre.text = $"REGISTRE DU CHANTIER   ·   Jour {Chantier.Jour}";
            conseil.text = $"<color=#ffd27a><b>Odon</b></color>\n« {Chantier.Conseil()} »";
            textePont.text = HUD.TextePont();

            var besoin = Chantier.LiaisonsManquantes();
            Vector2 souris = Souris(carte);
            Vector2 sourisPanneau = Souris(panneau);

            // Cible survolée pendant un glisser
            Affectation? cible = null;
            Noeud noeudSurvol = null; Lien lienSurvol = null;
            bool surBarre = false;
            if (glisse != null)
            {
                surBarre = Dedans(barre);
                if (!surBarre)
                {
                    foreach (var n in noeuds)
                        if (Dedans(n.carte) || Vector2.Distance(souris, n.pos) < 18f) { noeudSurvol = n; break; }
                    if (noeudSurvol == null)
                    {
                        float meilleur = 16f;
                        foreach (var l in liens)
                        {
                            float d = DistanceSegment(souris, l.a, l.b);
                            if (d < meilleur && l.source.Disponible && l.dest.Disponible) { meilleur = d; lienSurvol = l; }
                        }
                    }
                }
                if (noeudSurvol != null) cible = new Affectation { type = TypeAffectation.Poste, poste = noeudSurvol.poste };
                else if (lienSurvol != null) cible = new Affectation { type = TypeAffectation.Porteur, source = lienSurvol.source, destination = lienSurvol.dest };
                else if (surBarre) cible = new Affectation { type = TypeAffectation.Aucune };
            }

            // Fiches des postes
            foreach (var n in noeuds)
            {
                var p = n.poste;
                bool dispo = p.Disponible;
                bool alerte = dispo && p.blocage != "" && p.blocage != "Épuisé";
                n.texte.text = TexteFiche(p);
                n.cadre.color = n == noeudSurvol ? new Color(.2f, .24f, .4f, .95f) : new Color(.06f, .07f, .14f, dispo ? .9f : .6f);
                n.marqueur.GetComponent<UI.Image>().color = !dispo ? Gris : alerte ? Color.Lerp(Rouge, Color.white, Mathf.PingPong(Time.unscaledTime * 2f, 1f) * .4f) : Ambre;
            }

            // Flèches
            foreach (var l in liens)
            {
                int porteurs = 0;
                foreach (var o in Ouvrier.Tous)
                    if (o.Disponible && o.affectation.type == TypeAffectation.Porteur && o.affectation.source == l.source && o.affectation.destination == l.dest) porteurs++;
                bool visible = l.source.Disponible && l.dest.Disponible;
                bool manque = visible && porteurs == 0 && besoin.Contains((l.source, l.dest));
                Color c; float ep;
                if (!visible) { c = new Color(0, 0, 0, 0); ep = 0; }
                else if (l == lienSurvol) { c = Color.white; ep = 6; }
                else if (porteurs > 0) { c = Ambre; ep = 3 + 2 * porteurs; }
                else if (manque) { c = new Color(Rouge.r, Rouge.g, Rouge.b, .55f + .45f * Mathf.PingPong(Time.unscaledTime * 1.6f, 1f)); ep = 4; }
                else { c = new Color(Gris.r, Gris.g, Gris.b, glisse != null ? .6f : .22f); ep = 2; }
                l.img.color = c; l.imgFleche.color = c;
                l.ligne.sizeDelta = new Vector2(l.ligne.sizeDelta.x, ep);
                l.fleche.sizeDelta = Vector2.one * (8 + ep * 1.5f);
                Ressource r = l.source.EstFour ? Ressource.Chaux : l.source.produit;
                l.badge.text = !visible ? "" : porteurs > 0 ? $"{porteurs} × {Ouvrier.NomRessource(r, 2)}" : manque ? $"<color=#ff8a8a>porteur de {Ouvrier.NomRessource(r, 2)} ?</color>" : "";
            }

            // Villageois sur la carte, en direct
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
            foreach (var pr in portraits)
            {
                bool d = pr.o.Disponible;
                pr.rect.gameObject.SetActive(d);
                if (!d) continue;
                pr.rect.anchoredPosition = new Vector2(54 + dispos.Count * 86, -110);
                dispos.Add(pr);
                bool attente = pr.o.Etat.StartsWith("attend") || pr.o.Etat.StartsWith("rien");
                pr.legende.text = $"<b>{pr.o.nom}</b>\n<size=12>{Court(pr.o.affectation)}</size>" + (attente ? "\n<size=12><color=#ff8a8a>bloqué</color></size>" : "");
            }

            Clavier(dispos);
            Glisser(dispos, cible, sourisPanneau);
        }

        void Glisser(List<Portrait> dispos, Affectation? cible, Vector2 sourisPanneau)
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
            }
            jeton.gameObject.SetActive(glisse != null);
            survol.gameObject.SetActive(glisse != null);
            if (glisse == null) return;

            jeton.anchoredPosition = sourisPanneau + new Vector2(panneau.rect.width / 2f, -panneau.rect.height / 2f);
            ((RectTransform)survol.transform).anchoredPosition = sourisPanneau + new Vector2(panneau.rect.width / 2f + 34f, -panneau.rect.height / 2f + 70f);

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
            bool Appui(Key a, Key b, System.Func<Gamepad, UnityEngine.InputSystem.Controls.ButtonControl> g) =>
                (kb != null && (kb[a].wasPressedThisFrame || kb[b].wasPressedThisFrame)) || (pad != null && g(pad).wasPressedThisFrame);
            if (Appui(Key.LeftArrow, Key.A, g => g.dpad.left)) selection--;
            if (Appui(Key.RightArrow, Key.D, g => g.dpad.right)) selection++;
            selection = (selection % dispos.Count + dispos.Count) % dispos.Count;
            int sens = Appui(Key.UpArrow, Key.W, g => g.dpad.up) ? -1 : Appui(Key.DownArrow, Key.S, g => g.dpad.down) ? 1 : 0;
            var choisi = dispos[selection].o;
            if (sens != 0)
            {
                var options = choisi.Options();
                int i = options.FindIndex(x => x.Egale(choisi.affectation));
                choisi.Affecter(options[((i < 0 ? 0 : i) + sens + options.Count) % options.Count]);
                Sons.Jouer("clic", .5f);
            }
            for (int k = 0; k < dispos.Count; k++)
                dispos[k].anneau.color = k == selection ? Ambre : new Color(0, 0, 0, 0);
        }

        // ---------- textes ----------

        static string TexteFiche(Poste p)
        {
            if (!p.Disponible)
                return $"<b>{p.nom}</b>\n<color=#9a9ab4>À découvrir avec le Sceau (E)</color>";
            string recette = p.id switch
            {
                "riviere" => "Sortir les blocs de l'eau\n→ 4 blocs par personne et par jour",
                "carriere" => "Tailler la roche, loin du pont\n→ 2 blocs par personne et par jour",
                "foret" => "Abattre et ébrancher\n→ 5 poutres par personne et par jour",
                "talus" => "Casser le calcaire\n→ 4 calcaire par personne et par jour",
                "four" when !Chantier.FourRepare => "À réparer : 4 blocs\n+ 3 jours de travail (Odon compte double)",
                "four" => "2 calcaire + 1 poutre\n→ 2 sacs de chaux (le mortier)",
                "chantier" when Chantier.Etape < 3 => ChantierRecette(),
                _ => "Pont ouvert !",
            };
            string stock;
            if (p.EstChantier) stock = $"reçu : {Mathf.Floor(p.Stock(Ressource.Pierre)):0} blocs · {Mathf.Floor(p.Stock(Ressource.Chaux)):0} chaux · {Mathf.Floor(p.Stock(Ressource.Bois)):0} poutres";
            else if (p.EstFour && !Chantier.FourRepare) stock = $"blocs reçus {Mathf.Floor(p.Stock(Ressource.Pierre)):0}/4 · réparation {Chantier.ReparationFour / Chantier.FourJours * 100f:0} %";
            else if (p.EstFour) stock = $"chaux {Mathf.Floor(p.Stock(Ressource.Chaux)):0} · calcaire {Mathf.Floor(p.Stock(Ressource.Calcaire)):0} · bois {Mathf.Floor(p.Stock(Ressource.Bois)):0}";
            else stock = $"stock : {Mathf.Floor(p.Stock(p.produit)):0} {Ouvrier.NomRessource(p.produit, 2)}" + (p.reserve >= 0 ? $" (reste {Mathf.Max(0, p.reserve - p.extrait):0})" : "");
            string travail = $"{p.personnes:0.#} au travail / {p.maxPersonnes}";
            string alerte = p.blocage != "" && p.blocage != "Épuisé" ? $"\n<color=#ff8a8a>⚠ {p.blocage}</color>" : "";
            return $"<color=#ffd27a><b>{p.nom}</b></color>   <size=13><color=#9a9ab4>{travail}</color></size>\n<size=14>{recette}</size>\n<size=13>{stock}</size>{alerte}";
        }

        static string ChantierRecette()
        {
            var b = Chantier.Etapes[Chantier.Etape];
            var l = new List<string>();
            if (b.pierre > 0) l.Add($"{b.pierre:0} blocs");
            if (b.chaux > 0) l.Add($"{b.chaux:0} chaux (mortier)");
            if (b.bois > 0) l.Add($"{b.bois:0} poutres");
            return $"Étape {Chantier.Etape + 1} : {b.nom}\n{string.Join(" + ", l)} + des bâtisseurs";
        }

        static string Court(Affectation a) => a.type switch
        {
            TypeAffectation.Poste => a.poste.nom.Replace("Chantier du pont", "Chantier"),
            TypeAffectation.Porteur => $"{a.source.nom} → {a.destination.nom.Replace("Chantier du pont", "Chantier")}",
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

        Vector2 VersCarte(Vector3 w) =>
            new Vector2((w.x - minMonde.x) / tailleMonde - .5f, (w.z - minMonde.y) / tailleMonde - .5f) * TailleCarte;

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
            return m != null && RectTransformUtility.RectangleContainsScreenPoint(r, m.position.ReadValue(), null);
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

        /// <summary>Rectangle coloré ; position relative à l'ancre donnée (pivot au centre).</summary>
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

        /// <summary>Texte ; ancré en haut à gauche du parent (ou centré si centre = vrai).</summary>
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
