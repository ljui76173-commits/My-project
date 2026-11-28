using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    public enum Element { Aucun, Feu, Glace, Foudre, Vent, Terre, Eau, Lumiere, Ombre }

    /// <summary>Ce qu'un coup laisse derrière lui : brûlure (dégâts dans la durée), gel (ralentit ; trois de suite : gelé net),
    /// choc (étourdit), recul (projette).</summary>
    public enum Effet { Aucun, Brulure, Gel, Choc, Recul, GelNet }

    public enum TypeCompetence { Sort, Technique }

    /// <summary>
    /// La forme d'un sort : un projectile qui vole, un trait instantané, un cône devant soi, une zone autour de soi (ou là où l'on vise), un soin ;
    /// et pour les sorts évolués : une pluie qui tombe du ciel, un éclair venu du ciel, une tornade qui avance,
    /// une ligne de pics, un sanctuaire qui dure.
    /// </summary>
    public enum FormeSort { Aucune, Projectile, Trait, Cone, Zone, Soin, Pluie, Ciel, Tornade, Ligne, Sanctuaire }

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

        // ---------- les sorts évolués et fusionnés ----------
        /// <summary>Les deux branches d'évolution (au niveau 10).</summary>
        public string[] evolutions;
        /// <summary>Le sort dont celui-ci est l'évolution.</summary>
        public string evolutionDe;
        /// <summary>Les deux éléments dont ce sort est la fusion.</summary>
        public Element[] fusionDe;
        /// <summary>Le projectile traverse les ennemis au lieu de s'arrêter au premier.</summary>
        public bool perce;
        /// <summary>Plusieurs projectiles (en éventail, sur « ecart » degrés), plusieurs météores pour une pluie.</summary>
        public int nombre = 1;
        public float ecart;
        /// <summary>Rebonds de la foudre ; vagues d'un séisme ou frappes d'un orage.</summary>
        public int rebonds, vagues = 1;
        /// <summary>Durée d'une tornade ou d'un sanctuaire (secondes).</summary>
        public float duree;
        /// <summary>La zone se forme là où l'on vise, pas autour de soi.</summary>
        public bool aDistance;
        /// <summary>Soin sur la durée (part du soin immédiat, étalée sur 8 s).</summary>
        public double regen;
        /// <summary>Le coup d'ouverture d'un sanctuaire offensif (magma), en multiple de l'attaque.</summary>
        public double eruption;
        /// <summary>Les dégâts autour du point d'impact d'un météore (rayon en mètres).</summary>
        public float eclaboussure;

        public bool EstSort => type == TypeCompetence.Sort;
        public bool EstEvolution => !string.IsNullOrEmpty(evolutionDe);
        public bool EstFusion => fusionDe != null && fusionDe.Length == 2;
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

        /// <summary>Les recettes de fusion : deux éléments maîtrisés (un sort de chaque au niveau 10) donnent un nouveau sort.</summary>
        public static readonly List<(Element a, Element b, string resultat)> Fusions = new List<(Element, Element, string)>
        {
            (Element.Feu, Element.Vent, "tempete"),
            (Element.Feu, Element.Terre, "magma"),
            (Element.Glace, Element.Foudre, "orage"),
        };

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
                case Effet.GelNet: return "Gel net";
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
                evolutions = new[] { "lance", "pluie" },
            });
            Ajouter(new DefCompetence
            {
                id = "glace", nom = "Éclat de glace", abrege = "Gl", type = TypeCompetence.Sort, element = Element.Glace, forme = FormeSort.Projectile,
                puissance = 1.6, cout = 5, recharge = 1f, incantation = .25f, portee = 32f, vitesse = 30f, poussee = .3f, taille = .2f,
                effet = Effet.Gel, dureeEffet = 3f,
                description = "Un éclat de glace rapide. Il ralentit ; trois éclats de suite gèlent la cible sur place.",
                parNiveau = "dégâts +12 % par niveau, coût et recharge en baisse, sans plafond",
                paliers = new[] { "un éclat tourne autour du projectile, l'impact givre l'air", "l'impact fait jaillir des pics de glace", "un cristal pur, trois éclats en orbite, une couronne de pics" },
                evolutions = new[] { "javelot", "prison" },
            });
            Ajouter(new DefCompetence
            {
                id = "foudre", nom = "Trait de foudre", abrege = "Fo", type = TypeCompetence.Sort, element = Element.Foudre, forme = FormeSort.Trait,
                puissance = 2.3, cout = 8, recharge = 2.2f, incantation = .45f, portee = 22f, poussee = .2f,
                effet = Effet.Choc, dureeEffet = .8f, chanceEffet = .4f,
                description = "La foudre frappe instantanément. Elle peut étourdir ; au niveau 5, elle rebondit sur un second ennemi.",
                parNiveau = "dégâts +12 % par niveau, coût et recharge en baisse, sans plafond ; rebond au niveau 5",
                paliers = new[] { "l'éclair se ramifie", "un double éclair, et le sol est frappé", "une foudre aveuglante aux mille branches" },
                evolutions = new[] { "chaine", "colere" },
            });
            Ajouter(new DefCompetence
            {
                id = "vent", nom = "Souffle de vent", abrege = "Ve", type = TypeCompetence.Sort, element = Element.Vent, forme = FormeSort.Cone,
                puissance = 1, cout = 7, recharge = 3f, incantation = .2f, portee = 7f, angle = 55f, poussee = 5f,
                effet = Effet.Recul,
                description = "Une rafale devant toi qui repousse tout. Projeté contre un mur, un ennemi est sonné.",
                parNiveau = "dégâts et poussée +12 % par niveau, coût et recharge en baisse, sans plafond",
                paliers = new[] { "le souffle arrache herbes et feuilles", "des tourbillons se lèvent au bout du souffle", "une onde d'air visible qui balaie tout" },
                evolutions = new[] { "tornade", "lames" },
            });
            Ajouter(new DefCompetence
            {
                id = "terre", nom = "Onde de terre", abrege = "Te", type = TypeCompetence.Sort, element = Element.Terre, forme = FormeSort.Zone,
                puissance = 1.8, cout = 10, recharge = 5f, incantation = .55f, rayon = 5f, poussee = 1.6f, lourd = true,
                effet = Effet.Choc, dureeEffet = .9f,
                description = "Tu frappes le sol : une onde soulève et étourdit tout ce qui t'entoure.",
                parNiveau = "dégâts +12 % par niveau, rayon plus grand, coût et recharge en baisse, sans plafond",
                paliers = new[] { "des pics de pierre jaillissent du sol", "une seconde onde suit la première", "une couronne de pics et un cratère" },
                evolutions = new[] { "seisme", "pics" },
            });
            Ajouter(new DefCompetence
            {
                id = "soin", nom = "Soin", abrege = "So", type = TypeCompetence.Sort, element = Element.Lumiere, forme = FormeSort.Soin,
                puissance = 1.5, cout = 9, recharge = 6f, incantation = .8f,
                description = "Une lumière douce qui referme tes blessures. Longue à lancer : pas en plein milieu d'une mêlée.",
                parNiveau = "soin +12 % par niveau, coût et recharge en baisse, sans plafond",
                paliers = new[] { "un cercle de lumière s'ouvre sous tes pieds", "une colonne de lumière descend sur toi", "une pluie d'étincelles dorées" },
                evolutions = new[] { "regeneration", "sanctuaire" },
            });


            // ---------- les évolutions (au niveau 10, une branche sur deux) ----------
            Ajouter(new DefCompetence
            {
                id = "lance", nom = "Lance de feu", abrege = "LF", type = TypeCompetence.Sort, element = Element.Feu, forme = FormeSort.Projectile, evolutionDe = "flamme",
                puissance = 2.8, cout = 9, recharge = 1.6f, incantation = .35f, portee = 40f, vitesse = 40f, poussee = .6f, taille = .2f, perce = true,
                effet = Effet.Brulure, dureeEffet = 3f,
                description = "Une lance de feu qui file droit et transperce tout ce qu'elle rencontre.",
                parNiveau = "dégâts +12 % par niveau, coût et recharge en baisse, sans plafond",
            });
            Ajouter(new DefCompetence
            {
                id = "pluie", nom = "Pluie de feu", abrege = "PF", type = TypeCompetence.Sort, element = Element.Feu, forme = FormeSort.Pluie, evolutionDe = "flamme",
                puissance = 1.5, cout = 15, recharge = 6f, incantation = .75f, portee = 35f, vitesse = 26f, rayon = 4.5f, nombre = 8, eclaboussure = 1.8f, taille = .32f,
                effet = Effet.Brulure, dureeEffet = 3f,
                description = "Des boules de feu tombent du ciel sur la zone que tu vises.",
                parNiveau = "dégâts +12 % par niveau, coût et recharge en baisse, sans plafond",
            });
            Ajouter(new DefCompetence
            {
                id = "javelot", nom = "Javelot de givre", abrege = "JG", type = TypeCompetence.Sort, element = Element.Glace, forme = FormeSort.Projectile, evolutionDe = "glace",
                puissance = 2.3, cout = 9, recharge = 2.4f, incantation = .35f, portee = 40f, vitesse = 36f, poussee = .4f, taille = .2f, perce = true,
                effet = Effet.GelNet, dureeEffet = 1.8f,
                description = "Un javelot de glace qui transperce et gèle net tout ce qu'il touche.",
                parNiveau = "dégâts +12 % par niveau, coût et recharge en baisse, sans plafond",
            });
            Ajouter(new DefCompetence
            {
                id = "prison", nom = "Prison de glace", abrege = "PG", type = TypeCompetence.Sort, element = Element.Glace, forme = FormeSort.Zone, evolutionDe = "glace",
                puissance = 1.3, cout = 14, recharge = 8f, incantation = .6f, portee = 30f, rayon = 3.5f, aDistance = true, poussee = .2f,
                effet = Effet.GelNet, dureeEffet = 2.5f,
                description = "Une prison de glace jaillit là où tu vises : tout ce qui s'y trouve est gelé net.",
                parNiveau = "dégâts +12 % par niveau, zone plus grande, coût et recharge en baisse, sans plafond",
            });
            Ajouter(new DefCompetence
            {
                id = "chaine", nom = "Foudre en chaîne", abrege = "FC", type = TypeCompetence.Sort, element = Element.Foudre, forme = FormeSort.Trait, evolutionDe = "foudre",
                puissance = 2.1, cout = 11, recharge = 2.6f, incantation = .45f, portee = 24f, rebonds = 4, poussee = .2f,
                effet = Effet.Choc, dureeEffet = .7f, chanceEffet = .5f,
                description = "La foudre saute d'ennemi en ennemi, quatre fois.",
                parNiveau = "dégâts +12 % par niveau, coût et recharge en baisse, sans plafond",
            });
            Ajouter(new DefCompetence
            {
                id = "colere", nom = "Colère du ciel", abrege = "CC", type = TypeCompetence.Sort, element = Element.Foudre, forme = FormeSort.Ciel, evolutionDe = "foudre",
                puissance = 3.2, cout = 14, recharge = 5f, incantation = .7f, portee = 32f, rayon = 3f, poussee = .8f,
                effet = Effet.Choc, dureeEffet = 1.2f,
                description = "Un éclair tombe du ciel sur ta cible et foudroie tout autour.",
                parNiveau = "dégâts +12 % par niveau, zone plus grande, coût et recharge en baisse, sans plafond",
            });
            Ajouter(new DefCompetence
            {
                id = "tornade", nom = "Tornade", abrege = "To", type = TypeCompetence.Sort, element = Element.Vent, forme = FormeSort.Tornade, evolutionDe = "vent",
                puissance = .55, cout = 14, recharge = 7f, incantation = .5f, vitesse = 6f, duree = 3.2f, rayon = 2.2f, poussee = .5f,
                description = "Une tornade avance devant toi, soulève et fouette tout ce qu'elle traverse.",
                parNiveau = "dégâts +12 % par niveau, tornade plus grande, coût et recharge en baisse, sans plafond",
            });
            Ajouter(new DefCompetence
            {
                id = "lames", nom = "Lames de vent", abrege = "LV", type = TypeCompetence.Sort, element = Element.Vent, forme = FormeSort.Projectile, evolutionDe = "vent",
                puissance = 1.1, cout = 10, recharge = 2.2f, incantation = .3f, portee = 24f, vitesse = 30f, taille = .22f, nombre = 5, ecart = 44f, perce = true, poussee = 1.5f,
                effet = Effet.Recul,
                description = "Cinq lames d'air en éventail, qui tranchent tout sur leur passage.",
                parNiveau = "dégâts +12 % par niveau, coût et recharge en baisse, sans plafond",
            });
            Ajouter(new DefCompetence
            {
                id = "seisme", nom = "Séisme", abrege = "Sé", type = TypeCompetence.Sort, element = Element.Terre, forme = FormeSort.Zone, evolutionDe = "terre",
                puissance = 1.5, cout = 16, recharge = 9f, incantation = .85f, rayon = 5.5f, vagues = 3, poussee = 1.8f, lourd = true,
                effet = Effet.Choc, dureeEffet = .9f,
                description = "Trois ondes de plus en plus larges secouent la terre autour de toi.",
                parNiveau = "dégâts +12 % par niveau, ondes plus larges, coût et recharge en baisse, sans plafond",
            });
            Ajouter(new DefCompetence
            {
                id = "pics", nom = "Pics de pierre", abrege = "PP", type = TypeCompetence.Sort, element = Element.Terre, forme = FormeSort.Ligne, evolutionDe = "terre",
                puissance = 2.2, cout = 12, recharge = 4f, incantation = .45f, portee = 18f, poussee = 1f, lourd = true,
                effet = Effet.Choc, dureeEffet = .8f,
                description = "Une ligne de pics de pierre jaillit du sol jusqu'à ta cible et soulève tout ce qu'elle croise.",
                parNiveau = "dégâts +12 % par niveau, coût et recharge en baisse, sans plafond",
            });
            Ajouter(new DefCompetence
            {
                id = "regeneration", nom = "Régénération", abrege = "Ré", type = TypeCompetence.Sort, element = Element.Lumiere, forme = FormeSort.Soin, evolutionDe = "soin",
                puissance = 1.1, regen = 1.2, cout = 12, recharge = 10f, incantation = .6f,
                description = "Un soin immédiat, puis ta vie remonte toute seule pendant 8 secondes.",
                parNiveau = "soin +12 % par niveau, coût et recharge en baisse, sans plafond",
            });
            Ajouter(new DefCompetence
            {
                id = "sanctuaire", nom = "Sanctuaire", abrege = "Sa", type = TypeCompetence.Sort, element = Element.Lumiere, forme = FormeSort.Sanctuaire, evolutionDe = "soin",
                puissance = .45, cout = 18, recharge = 16f, incantation = 1f, rayon = 4f, duree = 6f,
                description = "Un cercle de lumière sous tes pieds : tant que tu y restes, il te soigne et brûle tes ennemis.",
                parNiveau = "soin et dégâts +12 % par niveau, coût et recharge en baisse, sans plafond",
            });

            // ---------- les fusions (deux éléments maîtrisés) ----------
            Ajouter(new DefCompetence
            {
                id = "tempete", nom = "Tempête ardente", abrege = "TA", type = TypeCompetence.Sort, element = Element.Feu, forme = FormeSort.Tornade,
                fusionDe = new[] { Element.Feu, Element.Vent }, couleur = new Color(1f, .42f, .15f),
                puissance = .9, cout = 20, recharge = 10f, incantation = .7f, vitesse = 5f, duree = 4f, rayon = 2.8f, poussee = .6f,
                effet = Effet.Brulure, dureeEffet = 3f,
                description = "Feu et vent : une tornade de flammes qui embrase tout sur son passage.",
                parNiveau = "dégâts +12 % par niveau, tornade plus grande, coût et recharge en baisse, sans plafond",
            });
            Ajouter(new DefCompetence
            {
                id = "magma", nom = "Magma", abrege = "Mg", type = TypeCompetence.Sort, element = Element.Feu, forme = FormeSort.Sanctuaire,
                fusionDe = new[] { Element.Feu, Element.Terre }, couleur = new Color(1f, .35f, .05f),
                puissance = .5, eruption = 2.2, cout = 22, recharge = 12f, incantation = .8f, portee = 30f, rayon = 4f, duree = 5f, aDistance = true, lourd = true,
                effet = Effet.Brulure, dureeEffet = 3f,
                description = "Feu et terre : le sol s'ouvre là où tu vises ; la lave jaillit, puis brûle tout ce qui s'y aventure.",
                parNiveau = "dégâts +12 % par niveau, coût et recharge en baisse, sans plafond",
            });
            Ajouter(new DefCompetence
            {
                id = "orage", nom = "Orage", abrege = "Or", type = TypeCompetence.Sort, element = Element.Foudre, forme = FormeSort.Ciel,
                fusionDe = new[] { Element.Glace, Element.Foudre }, couleur = new Color(.72f, .65f, 1f),
                puissance = 1.6, cout = 24, recharge = 14f, incantation = .9f, portee = 34f, rayon = 9f, vagues = 7, poussee = .5f,
                effet = Effet.Choc, dureeEffet = .6f, chanceEffet = .6f,
                description = "Glace et foudre : un orage se forme au-dessus de la zone visée et la foudroie sept fois.",
                parNiveau = "dégâts +12 % par niveau, coût et recharge en baisse, sans plafond",
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
                id = "analyse", nom = "Analyse", abrege = "An", type = TypeCompetence.Technique,
                description = "Comprendre un sort en le regardant. Monte à chaque sort ennemi observé, et deux fois plus en l'encaissant.",
                parNiveau = "jauge d'analyse +10 % plus rapide par niveau, sans plafond",
            });
            Ajouter(new DefCompetence
            {
                id = "evaluation", nom = "Évaluation", abrege = "Év", type = TypeCompetence.Technique,
                description = "Jauger une créature ou un objet d'un coup d'œil. Monte à chaque créature croisée (bien plus pour une espèce inconnue) et à chaque objet examiné.",
                parNiveau = "estimations plus justes ; faiblesses (niv. 2), résistances (4), attaque et défense (5), immunités (6), chiffres exacts au niveau 10",
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
