using System.Collections.Generic;

namespace Aldermark
{
    public enum TypeBonus { Aucun, Degats, Magie, Element, Stat, ToutesStats, XP, Analyse, Parade, Roulade, Critique }

    /// <summary>Un titre : ce qu'il faut accomplir (un compteur d'exploits et un objectif) et le bonus qu'il donne quand on le porte.</summary>
    public class DefTitre
    {
        public string id, nom, condition;
        /// <summary>Le compteur suivi (voir Exploits.Valeur) ; « @niveau », « @zones », « @maitrises » sont calculés.</summary>
        public string cle;
        public double objectif;
        public TypeBonus bonus;
        public float valeur;
        public Element element;
        public Stat stat;

        public string TexteBonus
        {
            get
            {
                string p = $"{valeur * 100:0}";
                switch (bonus)
                {
                    case TypeBonus.Degats: return $"+{p} % de dégâts";
                    case TypeBonus.Magie: return $"+{p} % de dégâts magiques";
                    case TypeBonus.Element: return $"+{p} % de dégâts de {CatalogueCompetences.NomElement(element).ToLowerInvariant()}";
                    case TypeBonus.Stat: return $"+{p} % {StatsHeros.Noms[(int)stat]}";
                    case TypeBonus.ToutesStats: return $"+{p} % à toutes les stats";
                    case TypeBonus.XP: return $"+{p} % d'expérience";
                    case TypeBonus.Analyse: return $"analyse des sorts {p} % plus rapide";
                    case TypeBonus.Parade: return $"fenêtre de parade parfaite +{p} %";
                    case TypeBonus.Roulade: return $"roulade {p} % moins chère";
                    case TypeBonus.Critique: return $"+{p} points de chance de critique";
                    default: return "aucun";
                }
            }
        }
    }

    /// <summary>Tous les titres du jeu, du plus simple au plus rare.</summary>
    public static class CatalogueTitres
    {
        public const string Defaut = "nouveau";
        static readonly List<DefTitre> tous = new List<DefTitre>();
        static readonly Dictionary<string, DefTitre> parId = new Dictionary<string, DefTitre>();

        public static IReadOnlyList<DefTitre> Tous { get { if (tous.Count == 0) Remplir(); return tous; } }
        public static DefTitre Get(string id) { if (tous.Count == 0) Remplir(); return id != null && parId.TryGetValue(id, out var t) ? t : null; }

        static void T(string id, string nom, string condition, string cle, double objectif, TypeBonus bonus = TypeBonus.Aucun, float valeur = 0f,
            Element element = Element.Aucun, Stat stat = Stat.FOR)
        {
            var t = new DefTitre { id = id, nom = nom, condition = condition, cle = cle, objectif = objectif, bonus = bonus, valeur = valeur, element = element, stat = stat };
            tous.Add(t);
            parId[id] = t;
        }

        static void Remplir()
        {
            T(Defaut, "Nouveau venu", "Arriver dans ce monde", "", 0);
            T("premier_sang", "Premier sang", "Vaincre une créature", "tues", 1, TypeBonus.XP, .03f);
            T("herbe", "Aventurier en herbe", "Atteindre le niveau 5", "@niveau", 5, TypeBonus.XP, .05f);
            T("vagabond", "Vagabond", "Explorer 50 zones", "@zones", 50, TypeBonus.XP, .06f);
            T("louvetier", "Louvetier", "Vaincre 25 loups", "tues:Loup", 25, TypeBonus.Stat, .06f, stat: Stat.AGI);
            T("fleau_gobelins", "Fléau des gobelins", "Vaincre 40 gobelins", "tues:Gobelin", 40, TypeBonus.Stat, .06f, stat: Stat.FOR);
            T("recureur", "Récureur de gelées", "Vaincre 30 gelées", "tues:Gelee", 30, TypeBonus.Stat, .06f, stat: Stat.END);
            T("tueur_trolls", "Tueur de trolls", "Vaincre 5 trolls", "tues:Troll", 5, TypeBonus.Degats, .06f);
            T("brise_sorts", "Brise-sorts", "Vaincre 15 lanceurs de sorts", "tues:lanceur", 15, TypeBonus.Analyse, .2f);
            T("centurion", "Centurion", "Vaincre 100 créatures", "tues", 100, TypeBonus.Degats, .05f);
            T("moissonneur", "Moissonneur", "Vaincre 1 000 créatures", "tues", 1000, TypeBonus.Degats, .1f);
            T("apprenti", "Apprenti mage", "Lancer 50 sorts", "sorts", 50, TypeBonus.Stat, .06f, stat: Stat.INT);
            T("archimage", "Archimage", "Lancer 2 000 sorts", "sorts", 2000, TypeBonus.Magie, .12f);
            T("voleur", "Voleur de sorts", "Apprendre un sort en l'observant", "observes", 1, TypeBonus.Analyse, .1f);
            T("copiste", "Copiste des arcanes", "Apprendre 5 sorts en les observant", "observes", 5, TypeBonus.Analyse, .3f);
            T("eveille", "Éveillé", "Faire évoluer un sort", "evolutions", 1, TypeBonus.Magie, .06f);
            T("alchimiste", "Alchimiste des éléments", "Réussir une fusion", "fusions", 1, TypeBonus.Magie, .1f);
            T("pyromane", "Pyromane", "Infliger 50 brûlures", "brulures", 50, TypeBonus.Element, .12f, Element.Feu);
            T("coeur_givre", "Cœur de givre", "Geler 30 ennemis", "gels", 30, TypeBonus.Element, .12f, Element.Glace);
            T("mur", "Mur vivant", "Réussir 50 parades parfaites", "parades", 50, TypeBonus.Parade, .15f);
            T("insaisissable", "Insaisissable", "Esquiver 100 coups", "esquives", 100, TypeBonus.Roulade, .15f);
            T("bourreau", "Bourreau", "Porter 20 coups de grâce", "graces", 20, TypeBonus.Critique, .05f);
            T("touche_a_tout", "Touche-à-tout", "Maîtriser 5 compétences (niveau 10)", "@maitrises", 5, TypeBonus.ToutesStats, .03f);
            T("legende", "Héros de légende", "Atteindre le niveau 25", "@niveau", 25, TypeBonus.ToutesStats, .06f);
        }
    }
}
