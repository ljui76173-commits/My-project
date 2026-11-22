using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    public enum TypeAffectation { Aucune, Poste, Porteur }

    /// <summary>Une affectation possible : travailler à un poste, ou porter d'un poste à un autre.</summary>
    public struct Affectation
    {
        public TypeAffectation type;
        public Poste poste, source, destination;

        public string Nom => type switch
        {
            TypeAffectation.Poste => poste.nom,
            TypeAffectation.Porteur => $"Porteur {source.nom} → {destination.nom}",
            _ => "Sans affectation",
        };

        public bool Egale(Affectation a) => type == a.type && poste == a.poste && source == a.source && destination == a.destination;
    }

    /// <summary>
    /// Un villageois du chantier. À un poste, il y travaille ; porteur, il fait la navette :
    /// il charge à la source (5 s), marche à 3 m/s, dépose à destination, et recommence.
    /// Changer son affectation ne lui fait pas lâcher sa charge : il finit sa livraison.
    /// </summary>
    [RequireComponent(typeof(Habitant))]
    public class Ouvrier : MonoBehaviour
    {
        public string nom = "Joss";
        [Tooltip("Drapeau de progression qui le rend disponible")] public string requis = "pont_accepte";
        public bool porte = true;
        [Tooltip("Efficacité sur les travaux de construction (Odon bâtit pour deux)")] public float force = 1f;

        public static readonly List<Ouvrier> Tous = new List<Ouvrier>();
        public const float VitessePorteur = 3f, DureeChargement = 5f, DureeDepot = 1.5f;

        public Affectation affectation;
        public Ressource charge;
        public float quantite;
        Poste livraison, sourceCharge;
        float minuteur;
        Vector2? cibleMarche;
        Habitant h;

        public bool AuPoste { get; private set; }
        public string Etat { get; private set; } = "";

        void OnEnable() => Tous.Add(this);
        void OnDisable() => Tous.Remove(this);
        void Awake() => h = GetComponent<Habitant>();

        public bool Disponible => Chantier.Actif && (string.IsNullOrEmpty(requis) || Progression.A(requis));
        public Poste Livraison => quantite > 0 ? livraison : null;

        public static int Capacite(Ressource r) => r == Ressource.Bois ? 4 : 3;

        public static Color Couleur(Ressource r) => r switch
        {
            Ressource.Pierre => new Color(.62f, .6f, .56f),
            Ressource.Bois => new Color(.5f, .34f, .2f),
            Ressource.Calcaire => new Color(.9f, .88f, .8f),
            Ressource.Chaux => new Color(.97f, .97f, .94f),
            _ => Color.white,
        };

        public static string NomRessource(Ressource r, float q)
        {
            bool pl = q >= 2;
            return r switch
            {
                Ressource.Pierre => pl ? "blocs" : "bloc",
                Ressource.Bois => pl ? "poutres" : "poutre",
                Ressource.Calcaire => pl ? "pierres de calcaire" : "pierre de calcaire",
                Ressource.Chaux => pl ? "sacs de chaux" : "sac de chaux",
                _ => "",
            };
        }

        public void Affecter(Affectation a)
        {
            if (a.Egale(affectation)) return;
            // Changement en plein chargement : la charge réservée retourne à la source
            if (quantite > 0 && livraison == null && sourceCharge != null)
            {
                sourceCharge.Ajouter(charge, quantite);
                quantite = 0;
            }
            affectation = a;
            AuPoste = false;
            minuteur = 0f;
            cibleMarche = null;
        }

        /// <summary>Toutes les affectations possibles pour ce villageois en ce moment.</summary>
        public List<Affectation> Options()
        {
            var res = new List<Affectation> { new Affectation { type = TypeAffectation.Aucune } };
            foreach (var p in Poste.Tous)
            {
                if (!p.Disponible) continue;
                bool travail = p.EstChantier || p.EstFour || p.produit != Ressource.Aucune;
                if (!travail) continue;
                if (!porte && !p.Batir && !p.EstFour) continue;   // Odon : main brisée, il bâtit seulement
                res.Add(new Affectation { type = TypeAffectation.Poste, poste = p });
            }
            if (porte)
                foreach (var s in Poste.Tous)
                {
                    if (!s.Disponible || s.Fournit == Ressource.Aucune) continue;
                    foreach (var d in Poste.Tous)
                        if (d != s && d.Disponible && d.Accepte(s.Fournit))
                            res.Add(new Affectation { type = TypeAffectation.Porteur, source = s, destination = d });
                }
            return res;
        }

        void Update()
        {
            if (!Disponible) { AuPoste = false; return; }
            float dt = Time.deltaTime;
            h.geste = Geste.Aucun;
            h.regarder = null;

            // Une charge en cours est toujours livrée, quelle que soit la nouvelle affectation
            if (quantite > 0 && livraison != null)
            {
                Etat = $"porte {quantite:0} {NomRessource(charge, quantite)} vers {livraison.nom}";
                if (Marcher(Proche(livraison), VitessePorteur))
                {
                    minuteur += dt;
                    if (minuteur >= DureeDepot)
                    {
                        livraison.Ajouter(charge, quantite);
                        quantite = 0; minuteur = 0; livraison = null;
                        h.Porter(null);
                    }
                }
                return;
            }

            switch (affectation.type)
            {
                case TypeAffectation.Aucune:
                    AuPoste = false;
                    Etat = "attend tes ordres";
                    break;

                case TypeAffectation.Poste:
                {
                    var p = affectation.poste;
                    int rang = Rang(p);
                    bool arrive = Marcher(p.Place(rang), 3.2f);
                    AuPoste = arrive;
                    if (arrive && p.blocage == "" && rang < p.maxPersonnes)
                    {
                        h.geste = GesteDuPoste(p);
                        h.regarder = p.transform.position;
                    }
                    Etat = !arrive ? $"va à {p.nom}" : (p.blocage != "" ? p.blocage.ToLowerInvariant() : "au travail");
                    if (arrive && rang >= p.maxPersonnes) Etat = "attend : le poste est complet";
                    break;
                }

                case TypeAffectation.Porteur:
                {
                    AuPoste = false;
                    var s = affectation.source;
                    var r = s.Fournit;
                    if (r == Ressource.Aucune || !affectation.destination.Accepte(r)) { Etat = "rien à porter"; break; }
                    if (!Marcher(Proche(s), VitessePorteur)) { Etat = $"va chercher {NomRessource(r, 2)} à {s.nom}"; minuteur = 0; break; }
                    if (s.Stock(r) < 1f && minuteur <= 0) { Etat = $"attend : rien à prendre à {s.nom}"; break; }
                    if (minuteur <= 0)
                    {
                        // On réserve la charge dès le début du chargement
                        charge = r;
                        sourceCharge = s;
                        quantite = s.Prendre(r, Capacite(r));
                        livraison = null;
                    }
                    minuteur += dt;
                    h.geste = Geste.Ramasser;
                    h.regarder = s.transform.position;
                    Etat = $"charge {NomRessource(r, 2)}";
                    if (minuteur >= DureeChargement)
                    {
                        minuteur = 0;
                        livraison = affectation.destination;
                        h.Porter(Couleur(charge));
                    }
                    break;
                }
            }
        }

        static Geste GesteDuPoste(Poste p) => p.id switch
        {
            "foret" => Geste.Hache,
            "carriere" => Geste.Pioche,
            "talus" => Geste.Pioche,
            "riviere" => Geste.Ramasser,
            "four" => Chantier.FourRepare ? Geste.Ramasser : Geste.Marteau,
            _ => Geste.Marteau,
        };

        /// <summary>Rang de cet ouvrier parmi ceux du même poste (pour sa place autour du poste).</summary>
        int Rang(Poste p)
        {
            int n = 0;
            foreach (var o in Tous)
            {
                if (o == this) return n;
                if (o.Disponible && o.affectation.type == TypeAffectation.Poste && o.affectation.poste == p) n++;
            }
            return n;
        }

        Vector2 Proche(Poste p)
        {
            // Les porteurs s'arrêtent au bord du poste, chacun un peu décalé
            float a = (GetInstanceID() % 12) * .52f;
            return new Vector2(p.transform.position.x + Mathf.Cos(a) * 2.6f, p.transform.position.z + Mathf.Sin(a) * 2.6f);
        }

        /// <summary>Marche vers un point ; renvoie vrai une fois arrivé.</summary>
        bool Marcher(Vector2 cible, float vitesse)
        {
            Vector2 ici = new Vector2(transform.position.x, transform.position.z);
            if (Vector2.Distance(ici, cible) < .6f) { cibleMarche = null; return true; }
            if (cibleMarche == null || Vector2.Distance(cibleMarche.Value, cible) > .1f || !h.EnRoute)
            {
                cibleMarche = cible;
                h.AllerVers(cible, vitesse);
            }
            return false;
        }

        /// <summary>Ce que le villageois répond quand on lui parle.</summary>
        public string Replique()
        {
            if (!Disponible) return "Bonjour, l'étranger.";
            if (quantite > 0 && livraison != null) return $"Je porte {quantite:0} {NomRessource(charge, quantite)} à {livraison.nom}. C'est lourd, tu sais !";
            return affectation.type switch
            {
                TypeAffectation.Aucune => "Dis-moi où tu as besoin de moi. (Tab : tableau du chantier)",
                TypeAffectation.Poste when affectation.poste.blocage != "" => $"Je suis à {affectation.poste.nom}, mais… {affectation.poste.blocage.ToLowerInvariant()}.",
                TypeAffectation.Poste => $"Je travaille à {affectation.poste.nom}.",
                _ when Etat.StartsWith("attend") => $"Je fais la navette entre {affectation.source.nom} et {affectation.destination.nom}, mais il n'y a rien à prendre là-bas !",
                _ => $"Je fais la navette entre {affectation.source.nom} et {affectation.destination.nom}.",
            };
        }
    }
}
