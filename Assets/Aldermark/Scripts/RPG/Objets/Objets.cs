using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    public enum Categorie { Arme, Nourriture, Materiau, Trophee, Monnaie }
    public enum FormeArme { Aucune, Epee, Gourdin }

    /// <summary>Ce qu'est un objet : nom, poids, valeur estimée, et selon sa catégorie ses stats d'arme ou ce qu'il soigne.</summary>
    public class DefObjet
    {
        public string id, nom, description;
        public Categorie categorie;
        public float poids;
        public int valeur, pileMax = 99;
        public Color couleur = Color.white;
        // Arme
        public FormeArme forme;
        public double degats;
        public float portee = 2f, poussee = 1f, critique;
        // Nourriture : part de la vie max rendue
        public float soin;
    }

    /// <summary>Une pile d'objets identiques (dans le sac, un coffre).</summary>
    [System.Serializable]
    public class Pile
    {
        public string id;
        public int n = 1;
        public Pile() { }
        public Pile(string id, int n) { this.id = id; this.n = n; }
    }

    /// <summary>Le catalogue des objets du jeu.</summary>
    public static class Objets
    {
        public const string Pieces = "pieces";
        static readonly Dictionary<string, DefObjet> defs = new Dictionary<string, DefObjet>();

        public static IEnumerable<DefObjet> Toutes => defs.Values;
        public static DefObjet Def(string id) => id != null && defs.TryGetValue(id, out var d) ? d : null;

        static void Ajouter(DefObjet d) => defs[d.id] = d;

        static Objets()
        {
            Ajouter(new DefObjet
            {
                id = "epee_courte", nom = "Épée courte", categorie = Categorie.Arme, poids = 1.5f, valeur = 40, pileMax = 1,
                description = "Une lame simple, bien équilibrée. Rapide, précise.",
                couleur = Pantin.Couleur("#c9ccd2"), forme = FormeArme.Epee, degats = 4, portee = 2.2f, poussee = 1f, critique = .02f,
            });
            Ajouter(new DefObjet
            {
                id = "gourdin_gobelin", nom = "Gourdin de gobelin", categorie = Categorie.Arme, poids = 2.6f, valeur = 18, pileMax = 1,
                description = "Lourd, noueux, clouté. Frappe fort et repousse loin, mais manque de finesse.",
                couleur = Pantin.Couleur("#6e4b2e"), forme = FormeArme.Gourdin, degats = 5.5, portee = 2f, poussee = 1.35f, critique = -.03f,
            });
            Ajouter(new DefObjet
            {
                id = "pain", nom = "Pain", categorie = Categorie.Nourriture, poids = .3f, valeur = 2, soin = .2f,
                description = "Une miche du hameau, encore souple. Rend 20 % de la vie.", couleur = Pantin.Couleur("#c8945a"),
            });
            Ajouter(new DefObjet
            {
                id = "viande_crue", nom = "Viande crue", categorie = Categorie.Nourriture, poids = .5f, valeur = 3, soin = .08f,
                description = "Mangeable… à peine. Rend 8 % de la vie. Bien meilleure cuite.", couleur = Pantin.Couleur("#b8574a"),
            });
            Ajouter(new DefObjet
            {
                id = "gelee", nom = "Gelée", categorie = Categorie.Materiau, poids = .2f, valeur = 3,
                description = "Visqueuse et tiède. Les alchimistes en tirent des onguents.", couleur = Pantin.Couleur("#5fb85a"),
            });
            Ajouter(new DefObjet
            {
                id = "noyau_gelee", nom = "Noyau de gelée", categorie = Categorie.Materiau, poids = .1f, valeur = 15,
                description = "Le cœur dur d'une grosse gelée. Rare, recherché.", couleur = Pantin.Couleur("#9be08a"),
            });
            Ajouter(new DefObjet
            {
                id = "peau_loup", nom = "Peau de loup", categorie = Categorie.Materiau, poids = 1.2f, valeur = 12,
                description = "Épaisse et chaude. De quoi faire une cape ou des bottes chez un tanneur.", couleur = Pantin.Couleur("#77716a"),
            });
            Ajouter(new DefObjet
            {
                id = "croc_loup", nom = "Croc de loup", categorie = Categorie.Materiau, poids = .05f, valeur = 5,
                description = "Pointu. On en fait des colliers, des pointes de flèche.", couleur = Pantin.Couleur("#e8dfc8"),
            });
            Ajouter(new DefObjet
            {
                id = "oreille_gobelin", nom = "Oreille de gobelin", categorie = Categorie.Trophee, poids = .05f, valeur = 4,
                description = "Les guildes paient une prime par oreille rapportée.", couleur = Pantin.Couleur("#7f9a4c"),
            });
            Ajouter(new DefObjet
            {
                id = "sang_troll", nom = "Sang de troll", categorie = Categorie.Materiau, poids = .3f, valeur = 60,
                description = "Il se referme encore sur lui-même dans la fiole. Très recherché.", couleur = Pantin.Couleur("#4f6b3a"),
            });
            Ajouter(new DefObjet
            {
                id = "croc_troll", nom = "Croc de troll", categorie = Categorie.Trophee, poids = 1f, valeur = 45,
                description = "Gros comme un poing. Une preuve que personne ne discute.", couleur = Pantin.Couleur("#e8dfc8"),
            });
            Ajouter(new DefObjet
            {
                id = Pieces, nom = "Pièces", categorie = Categorie.Monnaie, poids = 0f, valeur = 1, pileMax = 999999,
                description = "De la monnaie.", couleur = Pantin.Couleur("#f2c45a"),
            });
        }

        public static string NomCategorie(Categorie c) => c switch
        {
            Categorie.Arme => "Arme",
            Categorie.Nourriture => "Nourriture",
            Categorie.Materiau => "Matériau",
            Categorie.Trophee => "Trophée",
            _ => "Monnaie",
        };

        /// <summary>Un petit modèle de l'objet (au sol, dans la main) : arme, miche, fiole, peau…</summary>
        public static Transform Modele(DefObjet d, Transform parent)
        {
            var r = Pantin.Os("Modele", parent, Vector3.zero);
            if (d == null) return r;
            if (d.categorie == Categorie.Arme) { ConstruireArme(d.forme, r); return r; }
            var m = Pantin.Mat(ColorUtility.ToHtmlStringRGB(d.couleur).Insert(0, "#"), .3f);
            switch (d.id)
            {
                case "pain":
                    Pantin.Bloc(r, PrimitiveType.Capsule, Vector3.zero, new Vector3(.18f, .12f, .18f), m, new Vector3(0, 0, 90f));
                    break;
                case "sang_troll":
                    Pantin.Bloc(r, PrimitiveType.Cylinder, Vector3.zero, new Vector3(.1f, .09f, .1f), m);
                    Pantin.Bloc(r, PrimitiveType.Cylinder, new Vector3(0, .12f, 0), new Vector3(.04f, .04f, .04f), Pantin.Mat("#8a6a48", .1f));
                    break;
                case "peau_loup":
                    Pantin.Bloc(r, PrimitiveType.Cube, Vector3.zero, new Vector3(.4f, .04f, .3f), m, new Vector3(0, 15f, 4f));
                    break;
                case Pieces:
                    for (int k = 0; k < 4; k++)
                        Pantin.Bloc(r, PrimitiveType.Cylinder, new Vector3((k % 2) * .05f, k * .018f, (k / 2) * .04f), new Vector3(.07f, .008f, .07f), Pantin.Mat("#f2c45a", .7f));
                    break;
                case "croc_loup":
                case "croc_troll":
                    float t = d.id == "croc_troll" ? 2.2f : 1f;
                    Pantin.Bloc(r, PrimitiveType.Capsule, Vector3.zero, new Vector3(.04f, .07f, .04f) * t, m, new Vector3(0, 0, 25f));
                    break;
                default:
                    Pantin.Bloc(r, PrimitiveType.Sphere, Vector3.zero, Vector3.one * .16f, m);
                    break;
            }
            return r;
        }

        /// <summary>
        /// Une arme en volumes simples, la poignée à l'origine et la lame (ou la tête) vers +Z.
        /// </summary>
        public static void ConstruireArme(FormeArme forme, Transform racine)
        {
            switch (forme)
            {
                case FormeArme.Gourdin:
                    var bois = Pantin.Mat("#6e4b2e", .1f);
                    Pantin.Bloc(racine, PrimitiveType.Cylinder, new Vector3(0, 0, .2f), new Vector3(.05f, .24f, .05f), bois, new Vector3(90f, 0, 0));
                    Pantin.Bloc(racine, PrimitiveType.Capsule, new Vector3(0, 0, .55f), new Vector3(.13f, .2f, .13f), bois, new Vector3(90f, 0, 0));
                    var clou = Pantin.Mat("#5a5a60", .5f);
                    foreach (var p in new[] { new Vector3(.07f, 0, .58f), new Vector3(-.06f, .03f, .5f), new Vector3(0, -.07f, .63f) })
                        Pantin.Bloc(racine, PrimitiveType.Cube, p, Vector3.one * .035f, clou, new Vector3(45f, 45f, 0));
                    break;
                default:
                    var acier = Pantin.Mat("#c9ccd2", .65f);
                    var fer = Pantin.Mat("#4c4c54", .3f);
                    var cuir = Pantin.Mat("#5c3d26", .1f);
                    Pantin.Bloc(racine, PrimitiveType.Cube, new Vector3(0, 0, -.02f), new Vector3(.035f, .035f, .2f), cuir);
                    Pantin.Bloc(racine, PrimitiveType.Sphere, new Vector3(0, 0, -.13f), Vector3.one * .055f, fer);
                    Pantin.Bloc(racine, PrimitiveType.Cube, new Vector3(0, 0, .1f), new Vector3(.2f, .035f, .045f), fer);
                    Pantin.Bloc(racine, PrimitiveType.Cube, new Vector3(0, 0, .55f), new Vector3(.05f, .012f, .85f), acier);
                    Pantin.Bloc(racine, PrimitiveType.Cube, new Vector3(0, 0, .985f), new Vector3(.032f, .01f, .04f), acier, new Vector3(0, 45f, 0));
                    break;
            }
        }
    }
}
