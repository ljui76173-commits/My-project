using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// L'écran des titres, dans la fenêtre Compétences : tous les titres sur deux colonnes,
    /// ceux obtenus en clair, les autres grisés avec leur progression. Un clic sur un titre obtenu le porte.
    /// </summary>
    public partial class HUD
    {
        class LigneTitre
        {
            public string id;
            public RectTransform fond;
            public UnityEngine.UI.Text nom, info, bonus;
        }

        RectTransform boutonTitres, modaleTitres, retourTitres;
        UnityEngine.UI.Text texteBoutonTitres, enteteTitres;
        readonly List<LigneTitre> lignesTitres = new List<LigneTitre>();
        int titresVus = -1;
        public bool TitresOuverts => modaleTitres != null;

        /// <summary>Le bouton « Titres » de la fenêtre ; vrai si un clic l'a utilisé.</summary>
        bool MettreAJourBoutonTitres(Vector2 souris, bool clic)
        {
            var x = Exploits.Instance;
            if (x == null) return false;
            if (boutonTitres == null) boutonTitres = Bouton(panneauCompetences, new Vector2(800, -28), new Vector2(190, 40), "TITRES", out texteBoutonTitres);
            if (titresVus < 0) titresVus = x.NombreObtenus;
            bool nouveau = x.NombreObtenus > titresVus;
            texteBoutonTitres.text = $"TITRES ({x.NombreObtenus}/{CatalogueTitres.Tous.Count})" + (nouveau ? " !" : "");
            texteBoutonTitres.color = nouveau ? Color.Lerp(new Color(1f, .9f, .65f), Color.white, .5f + .5f * Mathf.Sin(Time.unscaledTime * 5f)) : new Color(1f, .9f, .65f);
            if (clic && Sur(boutonTitres, souris)) { OuvrirTitres(); return true; }
            return false;
        }

        void OuvrirTitres()
        {
            FermerTitres();
            var x = Exploits.Instance;
            if (x == null) return;
            titresVus = x.NombreObtenus;
            Sons.Jouer("decouverte", .4f, 1.3f);
            modaleTitres = Panneau("Titres", panneauCompetences, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero, new Color(.02f, .015f, .05f, .94f));
            modaleTitres.offsetMin = modaleTitres.offsetMax = Vector2.zero;
            var t = Haut(Texte("Titre", modaleTitres, "TITRES", 32, Color.white, TextAnchor.UpperCenter), 0, 26, 1240, 44);
            t.fontStyle = FontStyle.Bold;
            enteteTitres = Haut(Texte("SousTitre", modaleTitres, "", 18, CouleurOr, TextAnchor.UpperCenter), 0, 72, 1240, 28);

            var tous = CatalogueTitres.Tous;
            int parColonne = (tous.Count + 1) / 2;
            for (int k = 0; k < tous.Count; k++)
            {
                var d = tous[k];
                int col = k / parColonne, rang = k % parColonne;
                var l = new LigneTitre { id = d.id };
                l.fond = Panneau("Ligne", modaleTitres, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(40 + col * 590, -(110 + rang * 41)), new Vector2(570, 37), new Color(1, 1, 1, .04f));
                l.nom = Texte("Nom", l.fond, d.nom, 17, Color.white, TextAnchor.UpperLeft);
                Etirer((RectTransform)l.nom.transform, new Vector2(12, 0), new Vector2(-240, -1));
                l.info = Texte("Info", l.fond, "", 13, Pale, TextAnchor.LowerLeft);
                Etirer((RectTransform)l.info.transform, new Vector2(12, 2), new Vector2(-12, 0));
                l.bonus = Texte("Bonus", l.fond, d.TexteBonus, 13, new Color(.6f, .85f, .7f), TextAnchor.UpperRight);
                Etirer((RectTransform)l.bonus.transform, new Vector2(250, 0), new Vector2(-12, -2));
                lignesTitres.Add(l);
            }
            retourTitres = Bouton(modaleTitres, new Vector2(520, -646), new Vector2(200, 40), "RETOUR", out _);
        }

        void FermerTitres()
        {
            if (modaleTitres != null) Destroy(modaleTitres.gameObject);
            modaleTitres = null; retourTitres = null;
            lignesTitres.Clear();
        }

        void MettreAJourTitres(Vector2 souris, bool clic)
        {
            var x = Exploits.Instance;
            if (x == null) { FermerTitres(); return; }
            var actif = CatalogueTitres.Get(x.Porte);
            enteteTitres.text = $"Porté : « {actif?.nom} »   ·   {actif?.TexteBonus}   ·   clic sur un titre obtenu pour le porter";
            foreach (var l in lignesTitres)
            {
                var d = CatalogueTitres.Get(l.id);
                bool a = x.Possede(l.id), porte = x.Porte == l.id, sur = Sur(l.fond, souris);
                l.fond.GetComponent<UnityEngine.UI.Image>().color = porte ? new Color(1f, .82f, .45f, .2f) : a && sur ? new Color(1, 1, 1, .1f) : new Color(1, 1, 1, a ? .05f : .02f);
                l.nom.text = porte ? $"<b>{d.nom}</b>  <color=#ffd27a>◆ porté</color>" : a ? $"<b>{d.nom}</b>" : d.nom;
                l.nom.color = a ? Color.white : new Color(.55f, .54f, .62f);
                l.bonus.color = a ? new Color(.6f, .85f, .7f) : new Color(.42f, .5f, .46f);
                if (a || d.objectif <= 0) l.info.text = d.condition;
                else
                {
                    double v = System.Math.Min(x.Valeur(d.cle), d.objectif);
                    l.info.text = $"{d.condition}   <color=#ffd27a>{Nombres.Court(v)} / {Nombres.Court(d.objectif)}</color>";
                }
            }
            if (!clic) return;
            if (Sur(retourTitres, souris)) { FermerTitres(); Sons.Jouer("clic", .5f, .9f); return; }
            foreach (var l in lignesTitres)
                if (Sur(l.fond, souris))
                {
                    if (x.Possede(l.id)) x.Porter(l.id);
                    else Sons.Jouer("clic", .3f, .7f);
                    return;
                }
        }
    }
}
