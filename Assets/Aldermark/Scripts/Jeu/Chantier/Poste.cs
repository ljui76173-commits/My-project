using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    public enum Ressource { Aucune, Pierre, Bois, Calcaire, Chaux }

    /// <summary>
    /// Un lieu de travail du chantier : il produit sur place (rivière, carrière, forêt, talus, four)
    /// ou reçoit les matériaux (four, chantier du pont). Rien ne voyage sans porteur.
    /// </summary>
    public class Poste : MonoBehaviour
    {
        public string id = "foret";
        public string nom = "Forêt";
        public Ressource produit = Ressource.Bois;
        [Tooltip("Unités produites par personne et par jour")] public float rendement = 5f;
        public int maxPersonnes = 6;
        [Tooltip("Quantité totale disponible (-1 = illimité)")] public float reserve = -1f;
        [Tooltip("Drapeau de progression qui débloque le poste (vide = toujours)")] public string requis = "";
        [Tooltip("Stock déjà présent au départ (calcaire déjà extrait au talus, bois mort au four)")]
        public Ressource ressourceDepart = Ressource.Aucune;
        public float quantiteDepart;

        public const float Capacite = 20f;
        public static readonly List<Poste> Tous = new List<Poste>();

        readonly Dictionary<Ressource, float> stock = new Dictionary<Ressource, float>();
        [System.NonSerialized] public float extrait, personnes, progresChaux;
        [System.NonSerialized] public string blocage = "";
        /// <summary>Don de mana : secondes de travail restantes à vitesse double.</summary>
        [System.NonSerialized] public float boost;
        public const float DonMana = 10f, DureeBoost = 90f;
        public float Multiplicateur => boost > 0 ? 2f : 1f;
        Transform repere;
        LineRenderer anneau;

        /// <summary>Donne 10 mana à l'équipe : elle travaille deux fois plus vite pendant une demi-journée.</summary>
        public void Donner()
        {
            boost = DureeBoost;
            Sons.Jouer("sceauOn", .7f, .8f);
            Sons.Jouer("decouverte", .5f, 1.3f);
        }

        void OnEnable() => Tous.Add(this);
        void OnDisable() => Tous.Remove(this);

        public static Poste Trouver(string id)
        {
            foreach (var p in Tous) if (p.id == id) return p;
            return null;
        }

        public bool Disponible => Chantier.Actif && (string.IsNullOrEmpty(requis) || Progression.A(requis));
        public bool EstChantier => id == "chantier";
        public bool EstFour => id == "four";
        /// <summary>Ce que le poste fournit aux porteurs (le four ne donne de la chaux qu'une fois réparé).</summary>
        public Ressource Fournit => EstFour ? (Chantier.FourRepare ? Ressource.Chaux : Ressource.Aucune) : produit;
        /// <summary>Travail de construction : Odon y compte double.</summary>
        public bool Batir => EstChantier || (EstFour && !Chantier.FourRepare);

        public float Stock(Ressource r) => stock.TryGetValue(r, out var v) ? v : 0f;
        public void Ajouter(Ressource r, float q) => stock[r] = Stock(r) + q;
        public float Prendre(Ressource r, float max)
        {
            float q = Mathf.Min(max, Mathf.Floor(Stock(r)));
            if (q > 0) stock[r] = Stock(r) - q;
            return q;
        }

        /// <summary>Le poste accepte-t-il qu'on lui livre cette ressource ?</summary>
        public bool Accepte(Ressource r)
        {
            if (EstChantier) return r == Ressource.Pierre || r == Ressource.Bois || r == Ressource.Chaux;
            if (EstFour) return r == Ressource.Calcaire || r == Ressource.Bois || (r == Ressource.Pierre && !Chantier.FourRepare);
            return false;
        }

        public bool Epuise => reserve >= 0 && extrait >= reserve - .01f;

        /// <summary>Production sur place pendant dt secondes (rivière, carrière, forêt, talus, four).</summary>
        public void Simuler(float dt)
        {
            blocage = "";
            if (EstChantier) return;
            if (EstFour)
            {
                if (!Chantier.FourRepare) return;   // la réparation est gérée par le chantier
                if (personnes <= 0) return;
                if (Stock(Ressource.Chaux) >= Capacite) { blocage = "Stock de chaux plein : il manque des porteurs"; return; }
                // 2 calcaire + 1 poutre → 2 sacs ; 8 sacs par personne et par jour
                progresChaux += personnes * Multiplicateur * 4f * dt / Chantier.JourDuree;
                if (progresChaux >= 1f)
                {
                    if (Stock(Ressource.Calcaire) >= 2f && Stock(Ressource.Bois) >= 1f)
                    {
                        stock[Ressource.Calcaire] -= 2f;
                        stock[Ressource.Bois] -= 1f;
                        Ajouter(Ressource.Chaux, 2f);
                        progresChaux -= 1f;
                    }
                    else
                    {
                        progresChaux = 1f;
                        blocage = Stock(Ressource.Calcaire) < 2f ? "Il manque du calcaire" : "Il manque du bois";
                    }
                }
                return;
            }
            if (produit == Ressource.Aucune || personnes <= 0) return;
            if (Epuise) { blocage = "Épuisé"; return; }
            if (Stock(produit) >= Capacite) { blocage = "Stock plein : il manque des porteurs"; return; }
            float q = personnes * Multiplicateur * rendement * dt / Chantier.JourDuree;
            if (reserve >= 0) q = Mathf.Min(q, reserve - extrait);
            Ajouter(produit, q);
            extrait += q;
        }

        /// <summary>Emplacement de travail du n-ième ouvrier autour du poste.</summary>
        public Vector2 Place(int n)
        {
            float a = n * 2.1f + transform.position.x;
            float r = 1.6f + (n % 3) * .7f;
            return new Vector2(transform.position.x + Mathf.Cos(a) * r, transform.position.z + Mathf.Sin(a) * r);
        }

        void Start()
        {
            if (ressourceDepart != Ressource.Aucune && quantiteDepart > 0) Ajouter(ressourceDepart, quantiteDepart);
            // Alerte rouge visible dans la vue du Sceau quand le poste est bloqué
            int couche = LayerMask.NameToLayer("Sceau");
            if (couche < 0) return;
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(g.GetComponent<Collider>());
            g.name = "AlerteSceau"; g.layer = couche;
            g.transform.SetParent(transform, false);
            g.transform.localPosition = Vector3.up * 5f;
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", new Color(1f, .3f, .3f));
            g.GetComponent<MeshRenderer>().sharedMaterial = m;
            repere = g.transform;
            repere.gameObject.SetActive(false);

            // Anneau bleu au sol pendant le don de mana
            var a = new GameObject("AnneauMana");
            a.transform.SetParent(transform, false);
            anneau = a.AddComponent<LineRenderer>();
            anneau.loop = true; anneau.positionCount = 40; anneau.useWorldSpace = true; anneau.widthMultiplier = .18f;
            var ma = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            ma.SetColor("_BaseColor", new Color(.5f, .8f, 1f));
            anneau.sharedMaterial = ma;
            anneau.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            anneau.enabled = false;
        }

        void Update()
        {
            if (anneau != null)
            {
                anneau.enabled = boost > 0;
                if (boost > 0)
                {
                    float r = 4.2f + .3f * Mathf.Sin(Time.time * 3f);
                    for (int k = 0; k < 40; k++)
                    {
                        float a = k * Mathf.PI * 2f / 40f;
                        float x = transform.position.x + Mathf.Cos(a) * r, z = transform.position.z + Mathf.Sin(a) * r;
                        anneau.SetPosition(k, new Vector3(x, Mathf.Max(Monde.Hauteur(x, z), -.1f) + .15f, z));
                    }
                }
            }
            if (repere == null) return;
            bool alerte = Disponible && blocage != "" && blocage != "Épuisé";
            repere.gameObject.SetActive(alerte && Mathf.Repeat(Time.time, 1f) < .6f);
            repere.localScale = Vector3.one * 1.2f;
        }
    }
}
