using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Aldermark
{
    /// <summary>
    /// L'écran d'évolution et de fusion, dans la fenêtre Compétences :
    /// un bouton « Évoluer » sur un sort maîtrisé, un bouton « Fusions » quand deux éléments sont maîtrisés ;
    /// chacun ouvre des cartes côte à côte. Un clic choisit, un second clic confirme.
    /// </summary>
    public partial class HUD
    {
        class Carte
        {
            public string id;
            public RectTransform fond, bouton;
            public UnityEngine.UI.Text texteBouton;
        }

        RectTransform boutonEvoluer, boutonFusions, modale, boutonRetour;
        UnityEngine.UI.Text texteEvoluer, texteFusions;
        readonly List<Carte> cartes = new List<Carte>();
        string modaleSource, choixModale;
        bool modaleFusion;
        public bool ModaleOuverte => modale != null;

        void ConstruireBoutonsEvolution()
        {
            boutonEvoluer = Bouton(panneauCompetences, new Vector2(1000, -118), new Vector2(200, 40), "ÉVOLUER", out texteEvoluer);
            boutonFusions = Bouton(panneauCompetences, new Vector2(1010, -28), new Vector2(190, 40), "FUSIONS", out texteFusions);
            boutonEvoluer.gameObject.SetActive(false);
            boutonFusions.gameObject.SetActive(false);
        }

        /// <summary>Un bouton : un cadre doré et un texte (cliquable par RectTransformUtility).</summary>
        RectTransform Bouton(Transform parent, Vector2 pos, Vector2 taille, string texte, out UnityEngine.UI.Text t)
        {
            var b = Panneau("Bouton", parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), pos, taille, new Color(.35f, .26f, .08f, .85f));
            var o = b.gameObject.AddComponent<UnityEngine.UI.Outline>();
            o.effectColor = new Color(1f, .82f, .45f, .9f); o.effectDistance = new Vector2(2, -2);
            t = Texte("Texte", b, texte, 18, new Color(1f, .9f, .65f), TextAnchor.MiddleCenter);
            t.fontStyle = FontStyle.Bold;
            Etirer((RectTransform)t.transform, Vector2.zero, Vector2.zero);
            return b;
        }

        static bool Sur(RectTransform r, Vector2 souris) => r != null && r.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(r, souris, null);

        /// <summary>Les boutons de la fenêtre (affichés selon le sort choisi) ; vrai si un clic les a utilisés.</summary>
        bool MettreAJourBoutonsEvolution(Competences comp, DefCompetence choisie, Vector2 souris, bool clic)
        {
            if (boutonEvoluer == null) ConstruireBoutonsEvolution();
            bool evo = choisie != null && comp.PeutEvoluer(choisie.id);
            if (boutonEvoluer.gameObject.activeSelf != evo) boutonEvoluer.gameObject.SetActive(evo);
            var fusions = comp.FusionsPossibles();
            bool fus = fusions.Count > 0;
            if (boutonFusions.gameObject.activeSelf != fus) boutonFusions.gameObject.SetActive(fus);
            if (fus) texteFusions.text = $"FUSIONS ({fusions.Count})";
            float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * 5f);
            if (evo) texteEvoluer.color = Color.Lerp(new Color(1f, .9f, .65f), Color.white, pulse);
            if (fus) texteFusions.color = Color.Lerp(new Color(1f, .9f, .65f), Color.white, pulse);
            if (!clic) return false;
            if (evo && Sur(boutonEvoluer, souris)) { OuvrirModale(comp, choisie.id, false); return true; }
            if (fus && Sur(boutonFusions, souris)) { OuvrirModale(comp, null, true); return true; }
            return false;
        }

        void OuvrirModale(Competences comp, string source, bool fusion)
        {
            FermerModale();
            modaleSource = source; modaleFusion = fusion; choixModale = null;
            var propositions = new List<DefCompetence>();
            if (fusion) propositions.AddRange(comp.FusionsPossibles());
            else
            {
                var d = CatalogueCompetences.Get(source);
                if (d?.evolutions != null) foreach (var e in d.evolutions) { var x = CatalogueCompetences.Get(e); if (x != null) propositions.Add(x); }
            }
            if (propositions.Count == 0) return;
            Sons.Jouer("decouverte", .5f, 1.2f);

            // Un voile sombre sur toute la fenêtre, et le panneau par-dessus
            modale = Panneau("Modale", panneauCompetences, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero, new Color(.02f, .015f, .05f, .9f));
            modale.offsetMin = modale.offsetMax = Vector2.zero;
            int niveau = fusion ? 1 : comp.Niveau(source);
            var src = CatalogueCompetences.Get(source);
            string titre = fusion ? "FUSIONS" : $"ÉVOLUTION : {src.nom.ToUpper()}";
            string sous = fusion ? "Deux éléments maîtrisés donnent un sort nouveau. Tu gardes tous tes sorts."
                                 : $"Choisis une branche : le sort se transforme, garde son niveau ({niveau}), son expérience et son raccourci. C'est définitif.";
            var t = Haut(Texte("Titre", modale, titre, 32, Color.white, TextAnchor.UpperCenter), 0, 30, 1240, 44);
            t.fontStyle = FontStyle.Bold;
            Haut(Texte("SousTitre", modale, sous, 18, CouleurOr, TextAnchor.UpperCenter), 0, 78, 1240, 28);

            int n = propositions.Count;
            float largeur = n <= 2 ? 440 : 360, ecart = 30;
            float x0 = (1240 - (n * largeur + (n - 1) * ecart)) / 2f;
            for (int k = 0; k < n; k++)
            {
                var d = propositions[k];
                var c = new Carte { id = d.id };
                c.fond = Panneau("Carte", modale, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(x0 + k * (largeur + ecart), -124), new Vector2(largeur, 460),
                    new Color(d.couleur.r * .18f, d.couleur.g * .18f, d.couleur.b * .18f, .95f));
                var o = c.fond.gameObject.AddComponent<UnityEngine.UI.Outline>();
                o.effectColor = new Color(d.couleur.r, d.couleur.g, d.couleur.b, .8f); o.effectDistance = new Vector2(2, -2);
                var nom = Haut(Texte("Nom", c.fond, d.nom, 28, Color.Lerp(d.couleur, Color.white, .35f), TextAnchor.UpperCenter), 10, 18, largeur - 20, 38);
                nom.fontStyle = FontStyle.Bold;
                string origine = d.EstFusion ? $"{CatalogueCompetences.NomElement(d.fusionDe[0])} + {CatalogueCompetences.NomElement(d.fusionDe[1])}" : CatalogueCompetences.NomElement(d.element);
                Haut(Texte("Type", c.fond, $"{origine}  ·  {NomForme(d)}", 16, Pale, TextAnchor.UpperCenter), 10, 58, largeur - 20, 24);
                var desc = Haut(Texte("Description", c.fond, d.description, 18, new Color(.94f, .92f, 1f), TextAnchor.UpperLeft), 22, 98, largeur - 44, 110);
                desc.horizontalOverflow = HorizontalWrapMode.Wrap;
                var ch = Haut(Texte("Chiffres", c.fond, ChiffresApercu(comp, d, niveau), 16, new Color(.92f, .9f, 1f), TextAnchor.UpperLeft), 22, 214, largeur - 44, 160);
                ch.horizontalOverflow = HorizontalWrapMode.Wrap;
                c.bouton = Bouton(c.fond, new Vector2((largeur - 220) / 2f, -390), new Vector2(220, 46), fusion ? "FUSIONNER" : "CHOISIR", out c.texteBouton);
                cartes.Add(c);
            }
            boutonRetour = Bouton(modale, new Vector2(520, -610), new Vector2(200, 42), "RETOUR", out _);
        }

        void FermerModale()
        {
            if (modale != null) Destroy(modale.gameObject);
            modale = null; boutonRetour = null;
            cartes.Clear();
            choixModale = null;
        }

        /// <summary>L'écran de choix : un clic choisit une carte, un second clic sur son bouton confirme.</summary>
        void MettreAJourModale(Competences comp, Vector2 souris, bool clic)
        {
            foreach (var c in cartes)
            {
                bool choisie = c.id == choixModale;
                c.texteBouton.text = choisie ? "CONFIRMER ?" : modaleFusion ? "FUSIONNER" : "CHOISIR";
                c.texteBouton.color = choisie ? Color.Lerp(new Color(1f, .8f, .4f), Color.white, .5f + .5f * Mathf.Sin(Time.unscaledTime * 8f)) : new Color(1f, .9f, .65f);
                c.fond.localScale = Vector3.one * (choisie ? 1.03f : 1f);
            }
            if (!clic) return;
            if (Sur(boutonRetour, souris)) { FermerModale(); Sons.Jouer("clic", .5f, .9f); return; }
            foreach (var c in cartes)
            {
                if (!Sur(c.fond, souris)) continue;
                if (choixModale == c.id && Sur(c.bouton, souris))
                {
                    string id = c.id;
                    bool ok = modaleFusion ? comp.Fusionner(id) : comp.Evoluer(modaleSource, id);
                    FermerModale();
                    if (ok) compChoisie = id;
                    return;
                }
                choixModale = c.id;
                Sons.Jouer("clic", .5f, 1.3f);
                return;
            }
        }

        /// <summary>Le nom de la forme d'un sort, en clair.</summary>
        public static string NomForme(DefCompetence d)
        {
            switch (d.forme)
            {
                case FormeSort.Projectile: return d.nombre > 1 ? $"{d.nombre} projectiles en éventail" : d.perce ? "projectile perçant" : "projectile";
                case FormeSort.Trait: return d.rebonds > 0 ? $"foudre en chaîne ({d.rebonds} rebonds)" : "trait instantané";
                case FormeSort.Cone: return "cône devant toi";
                case FormeSort.Zone: return d.aDistance ? "zone là où tu vises" : d.vagues > 1 ? $"{d.vagues} ondes autour de toi" : "zone autour de toi";
                case FormeSort.Soin: return d.regen > 0 ? "soin, puis régénération" : "sur toi";
                case FormeSort.Pluie: return $"{d.nombre} météores sur la zone visée";
                case FormeSort.Ciel: return d.vagues > 1 ? $"orage : {d.vagues} éclairs" : "éclair venu du ciel";
                case FormeSort.Tornade: return "tornade qui avance";
                case FormeSort.Ligne: return "ligne de pics jusqu'à la cible";
                case FormeSort.Sanctuaire: return d.aDistance ? "zone de lave là où tu vises" : "cercle sous tes pieds";
                default: return "";
            }
        }

        static string ChiffresApercu(Competences comp, DefCompetence d, int niveau)
        {
            var sb = new System.Text.StringBuilder();
            if (d.forme == FormeSort.Soin) sb.AppendLine($"Soin  <b>{Nombres.Court(comp.Soin(d, niveau))}</b>" + (d.regen > 0 ? $"  + <b>{Nombres.Court(comp.Soin(d, niveau) * d.regen)}</b> sur 8 s" : ""));
            else if (d.forme == FormeSort.Sanctuaire && d.element == Element.Lumiere)
                sb.AppendLine($"Soin  <b>{Nombres.Court(comp.Soin(d, niveau) * .12)}</b> / ½ s   ·   Dégâts  <b>{Nombres.Court(comp.AttaqueMagique(d, niveau))}</b> / ½ s");
            else if (d.forme == FormeSort.Tornade || d.forme == FormeSort.Sanctuaire)
                sb.AppendLine($"Puissance  <b>{Nombres.Court(comp.AttaqueMagique(d, niveau))}</b> par coup" + (d.eruption > 0 ? $"  ·  éruption <b>{Nombres.Court(comp.AttaqueMagique(d, niveau) * d.eruption / d.puissance)}</b>" : ""));
            else sb.AppendLine($"Puissance  <b>{Nombres.Court(comp.AttaqueMagique(d, niveau))}</b>" + (d.nombre > 1 ? $" × {d.nombre}" : ""));
            sb.AppendLine($"Mana  <color=#7ab4ff><b>{Competences.Cout(d, niveau):0.#}</b></color>   ·   Recharge  <b>{Competences.Recharge(d, niveau):0.#} s</b>   ·   Incantation  <b>{d.incantation:0.##} s</b>");
            if (d.duree > 0) sb.AppendLine($"Durée  {d.duree:0.#} s");
            if (d.rayon > 0) sb.AppendLine($"Rayon  {d.rayon:0.#} m");
            if (d.effet != Effet.Aucun) sb.AppendLine($"Effet  {CatalogueCompetences.NomEffet(d.effet)}");
            return sb.ToString();
        }
    }
}
