using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    public enum Element { Aucun, Feu, Glace, Foudre, Vent, Terre, Eau, Lumiere, Ombre }

    /// <summary>Ce qu'un coup laisse derrière lui : brûlure (dégâts dans la durée), gel (ralentit ; trois de suite : gelé net),
    /// choc (étourdit), recul (projette).</summary>
    public enum Effet { Aucun, Brulure, Gel, Choc, Recul }

    public enum TypeCompetence { Sort, Technique }

    /// <summary>La forme d'un sort : un projectile qui vole, un trait instantané, un cône devant soi, une zone autour de soi, un soin.</summary>
    public enum FormeSort { Aucune, Projectile, Trait, Cone, Zone, Soin }

    /// <summary>
    /// Une compétence, décrite comme une fiche : ajouter un sort, c'est ajouter une fiche.
    /// Le héros et les monstres lancent les mêmes fiches (c'est ce qui permet d'apprendre en observant).
    /// </summary>
    public class DefCompetence
    {
        public string id, nom, abrege, description;
        public TypeCompetence type;
        public Element element;
        public FormeSort forme;
        /// <summary>Multiplicateur de l'attaque magique (dégâts) ou du soin.</summary>
        public double puissance = 1;
        public float cout, recharge = 1f, incantation = .3f;
        public float portee = 25f, vitesse = 24f, rayon, angle = 50f, poussee = .5f, taille = .22f;
        public Effet effet;
        public float dureeEffet = 3f, chanceEffet = 1f;
        public bool lourd;
        /// <summary>Ce que chaque niveau apporte, en une ligne (affiché dans la fenêtre).</summary>
        public string parNiveau;
        /// <summary>Ce qui change à l'œil aux niveaux 4, 7 et 10 (les paliers d'un sort).</summary>
        public string[] paliers;
        public Color couleur = Color.white;

        public bool EstSort => type == TypeCompetence.Sort;
    }

    public static class CatalogueCompetences
    {
        /// <summary>Le niveau où une compétence est « maîtrisée » (et peut évoluer). Ce n'est pas un plafond : les niveaux continuent.</summary>
        public const int NiveauMaitrise = 10;
        /// <summary>Garde-fou technique seulement (la console, une sauvegarde abîmée).</summary>
        public const int NiveauLimite = 9999;

        public static readonly Dictionary<string, DefCompetence> Toutes = new Dictionary<string, DefCompetence>();
        /// <summary>Dans l'ordre d'affichage : les sorts, puis les techniques.</summary>
        public static readonly List<DefCompetence> Ordre = new List<DefCompetence>();

        public static DefCompetence Get(string id) => id != null && Toutes.TryGetValue(id, out var d) ? d : null;

        public static string NomElement(Element e)
        {
            switch (e)
            {
                case Element.Feu: return "Feu";
                case Element.Glace: return "Glace";
                case Element.Foudre: return "Foudre";
                case Element.Vent: return "Vent";
                case Element.Terre: return "Terre";
                case Element.Eau: return "Eau";
                case Element.Lumiere: return "Lumière";
                case Element.Ombre: return "Ombre";
                default: return "";
            }
        }

        public static Color CouleurElement(Element e)
        {
            switch (e)
            {
                case Element.Feu: return new Color(1f, .5f, .15f);
                case Element.Glace: return new Color(.6f, .88f, 1f);
                case Element.Foudre: return new Color(.75f, .8f, 1f);
                case Element.Vent: return new Color(.78f, .95f, .85f);
                case Element.Terre: return new Color(.72f, .55f, .32f);
                case Element.Eau: return new Color(.3f, .6f, 1f);
                case Element.Lumiere: return new Color(.65f, 1f, .6f);
                case Element.Ombre: return new Color(.55f, .35f, .8f);
                default: return new Color(.85f, .82f, .75f);
            }
        }

        public static string NomEffet(Effet e)
        {
            switch (e)
            {
                case Effet.Brulure: return "Brûlure";
                case Effet.Gel: return "Gel";
                case Effet.Choc: return "Choc";
                case Effet.Recul: return "Recul";
                default: return "";
            }
        }

        static void Ajouter(DefCompetence d)
        {
            if (d.couleur == Color.white) d.couleur = CouleurElement(d.element);
            Toutes[d.id] = d;
            Ordre.Add(d);
        }

        static CatalogueCompetences()
        {
            // ---------- les sorts ----------
            Ajouter(new DefCompetence
            {
                id = "flamme", nom = "Flamme", abrege = "Fl", type = TypeCompetence.Sort, element = Element.Feu, forme = FormeSort.Projectile,
                puissance = 2, cout = 6, recharge = 1.2f, incantation = .3f, portee = 30f, vitesse = 22f, poussee = .4f, taille = .24f,
                effet = Effet.Brulure, dureeEffet = 3f,
                description = "Une boule de feu qui file droit sur la cible et l'enflamme.",
                parNiveau = "dégâts +12 % par niveau, coût et recharge en baisse, sans plafond",
                paliers = new[] { "des braises tournent autour de la boule de feu, l'impact fait une onde de flammes", "l'impact laisse un brasier qui brûle au sol", "un cœur de flamme bleue, un halo, une explosion au sol" },
            });
            Ajouter(new DefCompetence
            {
                id = "glace", nom = "Éclat de glace", abrege = "Gl", type = TypeCompetence.Sort, element = Element.Glace, forme = FormeSort.Projectile,
                puissance = 1.6, cout = 5, recharge = 1f, incantation = .25f, portee = 32f, vitesse = 30f, poussee = .3f, taille = .2f,
                effet = Effet.Gel, dureeEffet = 3f,
                description = "Un éclat de glace rapide. Il ralentit ; trois éclats de suite gèlent la cible sur place.",
                parNiveau = "dégâts +12 % par niveau, coût et recharge en baisse, sans plafond",
                paliers = new[] { "un éclat tourne autour du projectile, l'impact givre l'air", "l'impact fait jaillir des pics de glace", "un cristal pur, trois éclats en orbite, une couronne de pics" },
            });
            Ajouter(new DefCompetence
            {
                id = "foudre", nom = "Trait de foudre", abrege = "Fo", type = TypeCompetence.Sort, element = Element.Foudre, forme = FormeSort.Trait,
                puissance = 2.3, cout = 8, recharge = 2.2f, incantation = .45f, portee = 22f, poussee = .2f,
                effet = Effet.Choc, dureeEffet = .8f, chanceEffet = .4f,
                description = "La foudre frappe instantanément. Elle peut étourdir ; au niveau 5, elle rebondit sur un second ennemi.",
                parNiveau = "dégâts +12 % par niveau, coût et recharge en baisse, sans plafond ; rebond au niveau 5",
                paliers = new[] { "l'éclair se ramifie", "un double éclair, et le sol est frappé", "une foudre aveuglante aux mille branches" },
            });
            Ajouter(new DefCompetence
            {
                id = "vent", nom = "Souffle de vent", abrege = "Ve", type = TypeCompetence.Sort, element = Element.Vent, forme = FormeSort.Cone,
                puissance = 1, cout = 7, recharge = 3f, incantation = .2f, portee = 7f, angle = 55f, poussee = 5f,
                effet = Effet.Recul,
                description = "Une rafale devant toi qui repousse tout. Projeté contre un mur, un ennemi est sonné.",
                parNiveau = "dégâts et poussée +12 % par niveau, coût et recharge en baisse, sans plafond",
                paliers = new[] { "le souffle arrache herbes et feuilles", "des tourbillons se lèvent au bout du souffle", "une onde d'air visible qui balaie tout" },
            });
            Ajouter(new DefCompetence
            {
                id = "terre", nom = "Onde de terre", abrege = "Te", type = TypeCompetence.Sort, element = Element.Terre, forme = FormeSort.Zone,
                puissance = 1.8, cout = 10, recharge = 5f, incantation = .55f, rayon = 5f, poussee = 1.6f, lourd = true,
                effet = Effet.Choc, dureeEffet = .9f,
                description = "Tu frappes le sol : une onde soulève et étourdit tout ce qui t'entoure.",
                parNiveau = "dégâts +12 % par niveau, rayon plus grand, coût et recharge en baisse, sans plafond",
                paliers = new[] { "des pics de pierre jaillissent du sol", "une seconde onde suit la première", "une couronne de pics et un cratère" },
            });
            Ajouter(new DefCompetence
            {
                id = "soin", nom = "Soin", abrege = "So", type = TypeCompetence.Sort, element = Element.Lumiere, forme = FormeSort.Soin,
                puissance = 1.5, cout = 9, recharge = 6f, incantation = .8f,
                description = "Une lumière douce qui referme tes blessures. Longue à lancer : pas en plein milieu d'une mêlée.",
                parNiveau = "soin +12 % par niveau, coût et recharge en baisse, sans plafond",
                paliers = new[] { "un cercle de lumière s'ouvre sous tes pieds", "une colonne de lumière descend sur toi", "une pluie d'étincelles dorées" },
            });

            // ---------- les techniques ----------
            Ajouter(new DefCompetence
            {
                id = "epee", nom = "Épée", abrege = "Ép", type = TypeCompetence.Technique,
                description = "Le maniement des lames. Monte à chaque coup porté avec une épée.",
                parNiveau = "dégâts à l'épée +6 % par niveau, sans plafond",
            });
            Ajouter(new DefCompetence
            {
                id = "masse", nom = "Masse", abrege = "Ma", type = TypeCompetence.Technique,
                description = "Le maniement des gourdins et des masses. Monte à chaque coup porté avec.",
                parNiveau = "dégâts à la masse +6 % par niveau, sans plafond",
            });
            Ajouter(new DefCompetence
            {
                id = "mains", nom = "Mains nues", abrege = "Mn", type = TypeCompetence.Technique,
                description = "Poings, pieds, épaule. Monte à chaque coup porté sans arme.",
                parNiveau = "dégâts à mains nues +7 % par niveau, sans plafond",
            });
            Ajouter(new DefCompetence
            {
                id = "parade", nom = "Parade", abrege = "Pa", type = TypeCompetence.Technique,
                description = "Lever sa garde au bon moment. Monte à chaque coup encaissé en garde, et surtout à chaque parade parfaite.",
                parNiveau = "fenêtre de parade parfaite plus large, garde moins chère, sans plafond",
            });
            Ajouter(new DefCompetence
            {
                id = "esquive", nom = "Esquive", abrege = "Es", type = TypeCompetence.Technique,
                description = "Rouler sous les coups. Monte à chaque roulade, et surtout à chaque coup évité.",
                parNiveau = "roulade plus longtemps invulnérable et moins chère, sans plafond",
            });
        }
    }
}
