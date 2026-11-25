using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// L'interface du combat : chiffres de dégâts qui jaillissent (blancs, dorés en critique, rouges sur le héros),
    /// petits mots (« Parade parfaite ! », « Esquive »), panneau de la cible sous la boussole
    /// (nom, niveau, vie avec traînée blanche, ou dégâts encaissés pour la quintaine),
    /// losange au-dessus de la cible verrouillée, éclairs d'écran (blanc à la parade, bords rouges quand on est touché).
    /// </summary>
    public partial class HUD
    {
        class Chiffre
        {
            public RectTransform rect;
            public UnityEngine.UI.Text texte;
            public Vector3 monde;
            public float age, duree, echelle, derive;
            public Color couleur;
        }

        bool combatConstruit;
        RectTransform calqueChiffres, panneauCible, remplCible, traineeCible, marqueVerrou;
        UnityEngine.UI.Text texteCibleNom, texteCibleInfo;
        UnityEngine.UI.Image eclairPlein, eclairBords;
        readonly List<Chiffre> chiffres = new List<Chiffre>();
        readonly Stack<Chiffre> chiffresLibres = new Stack<Chiffre>();
        float trainee = 1f, eclairAge, eclairDuree;
        bool eclairVignette;
        Color eclairCouleur;
        ICible cibleAffichee;

        /// <summary>Un chiffre de dégâts au-dessus d'un point du monde.</summary>
        public static void ChiffreDegats(Vector3 position, double valeur, bool critique, bool surLeHeros)
        {
            if (instance == null) return;
            Color c = surLeHeros ? new Color(1f, .4f, .34f) : critique ? new Color(1f, .82f, .35f) : Color.white;
            string t = (surLeHeros ? "−" : "") + Nombres.Court(valeur) + (critique ? " !" : "");
            instance.AjouterChiffre(position, t, c, critique ? 44 : surLeHeros ? 34 : 30, critique ? 1.7f : 1.3f, critique ? 1.05f : .85f);
        }

        /// <summary>Un petit mot au-dessus d'un point du monde (« Parade parfaite ! », « Esquive »).</summary>
        public static void ChiffreTexte(Vector3 position, string texte, Color couleur)
        {
            if (instance == null) return;
            instance.AjouterChiffre(position, texte, couleur, 26, 1.25f, 1.1f);
        }

        /// <summary>Un éclair d'écran : plein (parade) ou sur les bords seulement (blessure).</summary>
        public static void EclairCombat(Color couleur, float duree, bool bords)
        {
            if (instance == null) return;
            instance.ConstruireCombat();
            instance.eclairCouleur = couleur;
            instance.eclairDuree = duree;
            instance.eclairAge = 0f;
            instance.eclairVignette = bords;
        }

        void ConstruireCombat()
        {
            if (combatConstruit) return;
            combatConstruit = true;

            calqueChiffres = Panneau("ChiffresCombat", toile, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0));
            calqueChiffres.offsetMin = calqueChiffres.offsetMax = Vector2.zero;

            // Cible, sous la boussole
            panneauCible = Panneau("Cible", toile, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -66), new Vector2(460, 58), new Color(0, 0, 0, 0));
            texteCibleNom = Texte("Nom", panneauCible, "", 21, Color.white, TextAnchor.UpperCenter);
            Etirer((RectTransform)texteCibleNom.transform, new Vector2(0, 26), Vector2.zero);
            var fond = Panneau("Jauge", panneauCible, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 14), new Vector2(440, 9), new Color(0, 0, 0, .55f));
            traineeCible = Panneau("Trainee", fond, Vector2.zero, Vector2.one, new Vector2(0, .5f), Vector2.zero, Vector2.zero, new Color(1f, .95f, .85f, .85f));
            traineeCible.offsetMin = traineeCible.offsetMax = Vector2.zero;
            remplCible = Panneau("Vie", fond, Vector2.zero, Vector2.one, new Vector2(0, .5f), Vector2.zero, Vector2.zero, CouleurVie);
            remplCible.offsetMin = remplCible.offsetMax = Vector2.zero;
            texteCibleInfo = Texte("Info", panneauCible, "", 15, new Color(.85f, .83f, .95f), TextAnchor.LowerCenter);
            var ri = (RectTransform)texteCibleInfo.transform;
            ri.anchorMin = ri.anchorMax = ri.pivot = new Vector2(.5f, 0);
            ri.anchoredPosition = new Vector2(0, -8); ri.sizeDelta = new Vector2(460, 20);
            panneauCible.gameObject.SetActive(false);

            // Losange au-dessus de la cible verrouillée
            marqueVerrou = Panneau("Verrou", toile, Vector2.zero, Vector2.zero, new Vector2(.5f, .5f), Vector2.zero, new Vector2(14, 14), CouleurOr);
            marqueVerrou.localRotation = Quaternion.Euler(0, 0, 45);
            var contour = marqueVerrou.gameObject.AddComponent<UnityEngine.UI.Outline>();
            contour.effectColor = new Color(0, 0, 0, .6f); contour.effectDistance = new Vector2(1.5f, -1.5f);
            marqueVerrou.gameObject.SetActive(false);

            // Éclairs d'écran
            eclairPlein = Panneau("EclairPlein", toile, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0)).GetComponent<UnityEngine.UI.Image>();
            eclairPlein.rectTransform.offsetMin = eclairPlein.rectTransform.offsetMax = Vector2.zero;
            eclairBords = Panneau("EclairBords", toile, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0)).GetComponent<UnityEngine.UI.Image>();
            eclairBords.rectTransform.offsetMin = eclairBords.rectTransform.offsetMax = Vector2.zero;
            eclairBords.sprite = Vignette();
        }

        /// <summary>Un dégradé transparent au centre, opaque sur les bords.</summary>
        static Sprite Vignette()
        {
            const int N = 128;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Vignette" };
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + .5f) / N * 2f - 1f, dy = (y + .5f) / N * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx * .8f + dy * dy * .8f);
                    float a = Mathf.Clamp01((d - .55f) / .55f);
                    px[y * N + x] = new Color(1, 1, 1, a * a);
                }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(.5f, .5f));
        }

        void AjouterChiffre(Vector3 position, string texte, Color couleur, int taille, float echelle, float duree)
        {
            ConstruireCombat();
            var c = chiffresLibres.Count > 0 ? chiffresLibres.Pop() : null;
            if (c == null)
            {
                var t = Texte("Chiffre", calqueChiffres, "", taille, Color.white, TextAnchor.MiddleCenter);
                t.fontStyle = FontStyle.Bold;
                var r = (RectTransform)t.transform;
                r.anchorMin = r.anchorMax = Vector2.zero; r.pivot = new Vector2(.5f, .5f); r.sizeDelta = new Vector2(300, 60);
                var o = t.gameObject.AddComponent<UnityEngine.UI.Outline>();
                o.effectColor = new Color(0, 0, 0, .75f); o.effectDistance = new Vector2(1.5f, -1.5f);
                c = new Chiffre { rect = r, texte = t };
            }
            c.texte.text = texte;
            c.texte.fontSize = taille;
            c.couleur = couleur;
            c.monde = position + new Vector3(Random.Range(-.25f, .25f), Random.Range(-.1f, .15f), Random.Range(-.25f, .25f));
            c.age = 0f; c.duree = duree; c.echelle = echelle; c.derive = Random.Range(55f, 80f);
            c.rect.gameObject.SetActive(true);
            c.rect.SetAsLastSibling();
            chiffres.Add(c);
        }

        void MettreAJourCombat()
        {
            ConstruireCombat();
            float dt = Time.unscaledDeltaTime;
            Vector2 taille = toile.rect.size;
            var vue = cam != null ? cam : Camera.main;

            // Chiffres : jaillissent, montent, s'effacent
            for (int i = chiffres.Count - 1; i >= 0; i--)
            {
                var c = chiffres[i];
                c.age += dt;
                float u = c.age / c.duree;
                if (u >= 1f || vue == null)
                {
                    c.rect.gameObject.SetActive(false);
                    chiffres.RemoveAt(i);
                    chiffresLibres.Push(c);
                    continue;
                }
                Vector3 sp = vue.WorldToScreenPoint(c.monde);
                if (sp.z <= 0f) { c.rect.gameObject.SetActive(false); continue; }
                c.rect.gameObject.SetActive(true);
                float monte = 1f - (1f - u) * (1f - u);
                c.rect.anchoredPosition = new Vector2(sp.x / Screen.width * taille.x, sp.y / Screen.height * taille.y + monte * c.derive);
                c.rect.localScale = Vector3.one * (1f + (c.echelle - 1f) * Mathf.Clamp01(1f - c.age / .14f));
                c.texte.color = new Color(c.couleur.r, c.couleur.g, c.couleur.b, u < .65f ? 1f : 1f - (u - .65f) / .35f);
            }

            // Cible : la dernière touchée ou celle qu'on verrouille
            var verrou = CombatHeros.Instance != null ? CombatHeros.Instance.Verrou : null;
            if (!Cibles.Existe(verrou)) verrou = null;
            var cible = verrou ?? CibleCombat.Courante;
            bool montrer = Cibles.Existe(cible) && cible.Vivant && (verrou != null || Time.time - CibleCombat.Depuis < 6f);
            if (panneauCible.gameObject.activeSelf != montrer) panneauCible.gameObject.SetActive(montrer);
            if (montrer)
            {
                if (!ReferenceEquals(cible, cibleAffichee)) { cibleAffichee = cible; trainee = 1f; }
                var s = StatsHeros.Instance;
                int ecart = s != null ? cible.NiveauAffiche - s.Niveau : 0;
                string couleurNiveau = ecart >= 10 ? "#ff6a5a" : ecart >= 4 ? "#ffb35a" : ecart <= -10 ? "#9a98b0" : "#ffffff";
                texteCibleNom.text = $"<b>{cible.NomAffiche}</b>   <color={couleurNiveau}>Niv. {cible.NiveauAffiche}</color>";
                if (cible is Combattant cb && cb.invincible)
                {
                    remplCible.anchorMax = new Vector2(1, 1);
                    trainee = 1f;
                    texteCibleInfo.text = $"dégâts encaissés  {Nombres.Court(cb.DegatsCumules)}" + (cb.Etourdi > 0 ? "   ·   <color=#ffd27a>sonnée</color>" : "");
                }
                else
                {
                    float f = cible.VieMaximale > 0 ? Mathf.Clamp01((float)(cible.VieActuelle / cible.VieMaximale)) : 0f;
                    remplCible.anchorMax = new Vector2(f, 1);
                    trainee = trainee > f ? Mathf.MoveTowards(trainee, f, dt * .6f) : f;
                    var cc = cible as Combattant;
                    texteCibleInfo.text = $"{Nombres.Court(cible.VieActuelle)} / {Nombres.Court(cible.VieMaximale)}" + (cc != null && cc.Etourdi > 0 ? "   ·   <color=#ffd27a>sonné</color>" : "");
                }
                traineeCible.anchorMax = new Vector2(trainee, 1);
            }

            // Losange au-dessus de la cible verrouillée
            bool marque = verrou != null && vue != null;
            if (marque)
            {
                Vector3 sp = vue.WorldToScreenPoint(verrou.Centre + Vector3.up * .9f);
                marque = sp.z > 0f;
                if (marque)
                {
                    marqueVerrou.anchoredPosition = new Vector2(sp.x / Screen.width * taille.x, sp.y / Screen.height * taille.y + Mathf.Sin(Time.unscaledTime * 5f) * 3f);
                }
            }
            if (marqueVerrou.gameObject.activeSelf != marque) marqueVerrou.gameObject.SetActive(marque);

            // Éclairs d'écran
            eclairAge += dt;
            float a = eclairDuree > 0f ? Mathf.Clamp01(1f - eclairAge / eclairDuree) : 0f;
            var plein = new Color(eclairCouleur.r, eclairCouleur.g, eclairCouleur.b, eclairVignette ? 0f : eclairCouleur.a * a);
            var bords = new Color(eclairCouleur.r, eclairCouleur.g, eclairCouleur.b, eclairVignette ? eclairCouleur.a * a : 0f);
            eclairPlein.color = plein;
            eclairBords.color = bords;
        }
    }
}
