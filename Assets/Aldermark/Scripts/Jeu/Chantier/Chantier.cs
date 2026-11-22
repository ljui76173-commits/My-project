using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// Cœur logistique de la Boucle du pont : le temps (journées de 3 minutes), la réparation du four,
    /// les 3 étapes du pont provisoire en bois, l'arrivée des villageois et la vue logistique du Sceau.
    /// </summary>
    public class Chantier : MonoBehaviour
    {
        public const float JourDuree = 180f;

        [Header("Pont provisoire (pièces révélées au fil de l'avancement)")]
        public Transform etape1, etape2, etape3;
        public Collider tablier;

        public static Chantier Instance { get; private set; }
        public static bool Actif => Progression.A("pont_accepte");
        public static bool FourRepare => Progression.A("four_repare");
        public static float Temps { get; private set; }
        public static int Jour => 1 + (int)(Temps / JourDuree);
        public static float HeureDuJour => Mathf.Repeat(Temps, JourDuree) / JourDuree;

        // Réparation du four
        public const float FourJours = 3f, FourBlocs = 4f;
        public static float ReparationFour { get; private set; }

        // Pont en bois : pierre, chaux, bois, jours-personne
        public struct Besoin { public string nom; public float pierre, chaux, bois, jours; }
        public static readonly Besoin[] Etapes =
        {
            new Besoin { nom = "Piles renforcées", pierre = 12, chaux = 4, bois = 0, jours = 4 },
            new Besoin { nom = "Poutres maîtresses", pierre = 0, chaux = 0, bois = 24, jours = 6 },
            new Besoin { nom = "Tablier et garde-corps", pierre = 0, chaux = 0, bois = 30, jours = 5 },
        };
        public static readonly float[] Avancement = new float[3];
        public static int Etape => Progression.A("pont_ouvert") ? 3 : Progression.A("pont_etape2") ? 2 : Progression.A("pont_etape1") ? 1 : 0;
        public static string BlocageChantier { get; private set; } = "";

        bool annonce;
        float minuteurLignes;
        Material matLigne;
        readonly Dictionary<(Poste, Poste), LineRenderer> lignes = new Dictionary<(Poste, Poste), LineRenderer>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reinitialiser()
        {
            Temps = 0; ReparationFour = 0; BlocageChantier = "";
            for (int i = 0; i < Avancement.Length; i++) Avancement[i] = 0;
        }

        void Awake() => Instance = this;

        void Start()
        {
            matLigne = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            matLigne.SetColor("_BaseColor", new Color(1f, .75f, .29f));
            if (tablier != null) tablier.enabled = Etape >= 3;
            MettreAJourPont();
        }

        void Update()
        {
            RaccourcisDeTest();
            if (!Actif) return;
            float dt = Time.deltaTime;
            Temps += dt;

            if (!annonce)
            {
                annonce = true;
                HUD.Message("TABLEAU DU CHANTIER",
                    "Le village est prêt à travailler.\nAppuie sur Tab pour affecter chacun à un poste ou en faire un porteur.\nRien n'arrive au chantier sans porteur !", 8f);
            }

            CompterPersonnes();
            foreach (var p in Poste.Tous) if (p.Disponible) p.Simuler(dt);
            Four(dt);
            Pont(dt);

            minuteurLignes -= dt;
            if (minuteurLignes <= 0) { minuteurLignes = .5f; MettreAJourLignes(); }
        }

        /// <summary>
        /// Raccourcis réservés à l'éditeur Unity : F9 saute l'enquête (tous les indices, pont accepté),
        /// F10 accélère le temps ×4 pour tester l'équilibrage.
        /// </summary>
        static void RaccourcisDeTest()
        {
            if (!Application.isEditor) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (kb.f9Key.wasPressedThisFrame && !Actif)
            {
                foreach (var d in new[] { "lise", "test_four", "vu_pont", "vu_four", "vu_calcaire", "vu_blocs", "vu_cendres", "four_ok", "sabotage_connu", "bram", "vu_corvin" })
                    Progression.Mettre(d);
                Progression.Mettre("pont_accepte");
                Debug.Log("Aldermark (test) : enquête sautée, chantier ouvert.");
            }
            if (kb.f10Key.wasPressedThisFrame)
            {
                Time.timeScale = Time.timeScale > 1f ? 1f : 4f;
                HUD.Message("TEST", $"Vitesse du temps ×{Time.timeScale:0}", 2f);
            }
        }

        /// <summary>Personnes réellement au travail sur chaque poste (Odon compte double pour bâtir).</summary>
        static void CompterPersonnes()
        {
            var nombre = new Dictionary<Poste, int>();
            foreach (var p in Poste.Tous) p.personnes = 0;
            foreach (var o in Ouvrier.Tous)
            {
                if (!o.Disponible || !o.AuPoste || o.affectation.type != TypeAffectation.Poste) continue;
                var p = o.affectation.poste;
                nombre.TryGetValue(p, out int n);
                if (n >= p.maxPersonnes) continue;
                nombre[p] = n + 1;
                p.personnes += p.Batir ? o.force : 1f;
            }
        }

        // ---------- conseils d'Odon ----------

        static int Au(string id) { int n = 0; foreach (var o in Ouvrier.Tous) if (o.Disponible && o.affectation.type == TypeAffectation.Poste && o.affectation.poste != null && o.affectation.poste.id == id) n++; return n; }
        static int Porteurs(string src, string dst) { int n = 0; foreach (var o in Ouvrier.Tous) if (o.Disponible && o.affectation.type == TypeAffectation.Porteur && o.affectation.source.id == src && o.affectation.destination.id == dst) n++; return n; }
        static bool Ouvert(string id) { var p = Poste.Trouver(id); return p != null && p.Disponible; }
        static float EnChemin(string dst, Ressource r) { float q = 0; foreach (var o in Ouvrier.Tous) if (o.Livraison != null && o.Livraison.id == dst && o.charge == r) q += o.quantite; return q; }

        /// <summary>Le blocage du moment, dit simplement (affiché par Odon sur la carte du chantier).</summary>
        public static string Conseil()
        {
            if (!Actif) return "";
            if (!Progression.A("plan_bois")) return "Montre-moi d'abord ce que tu vois sur les ruines : projette le Plan du Registre (P).";
            foreach (var p in Poste.Tous)
                if (p.Disponible && p.blocage.StartsWith("Stock plein")) return $"Ça s'entasse à {p.nom} : personne ne vient chercher. Ajoute un porteur depuis {p.nom}.";
            string pierre = Ouvert("riviere") ? "riviere" : "carriere";
            string nomPierre = pierre == "riviere" ? "la Rivière" : "la Carrière";
            var four = Poste.Trouver("four");
            if (!FourRepare && four != null)
            {
                float blocs = four.Stock(Ressource.Pierre) + EnChemin("four", Ressource.Pierre);
                if (blocs < FourBlocs)
                {
                    if (Au("riviere") + Au("carriere") == 0) return $"Le four a besoin de 4 blocs. Mets quelqu'un à {nomPierre} pour les sortir.";
                    if (Porteurs("riviere", "four") + Porteurs("carriere", "four") == 0) return $"Les blocs attendent à {nomPierre}. Il faut un porteur jusqu'au Four.";
                }
                if (four.personnes <= 0) return "Il faut des bras au Four pour la voûte. Moi, je bâtis pour deux : mets-moi là-bas.";
                return "Le four se répare. Pense déjà au bois : le pont en dévore, et le four aussi.";
            }
            int e = Etape;
            if (e >= 3) return "Il tient. Les charrettes peuvent passer.";
            var c = Poste.Trouver("chantier");
            var b = Etapes[e];
            if (b.chaux > 0 && c.Stock(Ressource.Chaux) + EnChemin("chantier", Ressource.Chaux) < b.chaux)
            {
                if (Au("talus") == 0) return "Pas de chaux sans calcaire. Envoie quelqu'un casser le calcaire au Talus.";
                if (Porteurs("talus", "four") == 0) return "Le calcaire reste au Talus. Il faut un porteur Talus → Four.";
                if (Au("foret") == 0) return "Le four brûle du bois pour cuire la chaux. Mets quelqu'un à la Forêt.";
                if (Porteurs("foret", "four") == 0) return "Le four n'a pas de bois pour cuire. Il faut un porteur Forêt → Four.";
                if (four.personnes <= 0) return "Tout est au four, mais personne ne le fait tourner. Mets quelqu'un au Four.";
                if (Porteurs("four", "chantier") == 0) return "La chaux attend au four. Il faut un porteur Four → Chantier : c'est le mortier des piles.";
            }
            if (b.pierre > 0 && c.Stock(Ressource.Pierre) + EnChemin("chantier", Ressource.Pierre) < b.pierre)
            {
                if (Au("riviere") + Au("carriere") == 0) return $"Il faut des blocs pour les piles. Mets quelqu'un à {nomPierre}.";
                if (Porteurs("riviere", "chantier") + Porteurs("carriere", "chantier") == 0) return $"Les blocs attendent à {nomPierre}. Il faut un porteur jusqu'au chantier.";
            }
            if (b.bois > 0 && c.Stock(Ressource.Bois) + EnChemin("chantier", Ressource.Bois) < b.bois)
            {
                if (Au("foret") == 0) return "Le pont, c'est du bois maintenant. Mets du monde à la Forêt.";
                if (Porteurs("foret", "chantier") == 0) return "Les poutres attendent en forêt. Il faut des porteurs Forêt → Chantier.";
            }
            if (c.personnes <= 0) return "Les matériaux arrivent, mais personne ne bâtit. Mets du monde au chantier — moi compris.";
            return "Ça avance. Pour aller plus vite, renforce la liaison la plus lente.";
        }

        /// <summary>Liaisons qui devraient avoir un porteur pour la prochaine étape (pointillés rouges sur la carte).</summary>
        public static HashSet<(Poste, Poste)> LiaisonsManquantes()
        {
            var res = new HashSet<(Poste, Poste)>();
            if (!Actif || !Progression.A("plan_bois")) return res;
            Poste P(string id) => Poste.Trouver(id);
            var pierre = Ouvert("riviere") ? P("riviere") : P("carriere");
            void Ajouter(Poste a, Poste b) { if (a != null && b != null && a.Disponible && b.Disponible) res.Add((a, b)); }
            if (!FourRepare) { Ajouter(pierre, P("four")); return res; }
            int e = Etape;
            if (e >= 3) return res;
            var b = Etapes[e];
            if (b.chaux > 0) { Ajouter(P("talus"), P("four")); Ajouter(P("foret"), P("four")); Ajouter(P("four"), P("chantier")); }
            if (b.pierre > 0) Ajouter(pierre, P("chantier"));
            if (b.bois > 0) Ajouter(P("foret"), P("chantier"));
            return res;
        }

        // ---------- four ----------

        static void Four(float dt)
        {
            if (FourRepare) return;
            var four = Poste.Trouver("four");
            if (four == null || !four.Disponible) return;
            if (four.Stock(Ressource.Pierre) < FourBlocs)
            {
                four.blocage = $"Il manque {FourBlocs - Mathf.Floor(four.Stock(Ressource.Pierre)):0} blocs pour réparer";
            }
            float plafond = FourJours * Mathf.Clamp01(Mathf.Floor(four.Stock(Ressource.Pierre)) / FourBlocs);
            ReparationFour = Mathf.Min(ReparationFour + four.personnes * dt / JourDuree, plafond);
            if (ReparationFour >= FourJours - .001f)
            {
                four.Prendre(Ressource.Pierre, FourBlocs);
                Progression.Mettre("four_repare");
                Progression.AjouterConfiance(2);
                Sons.Jouer("decouverte", .8f);
                HUD.Message("FOUR À CHAUX RELANCÉ",
                    "La fumée blanche monte à nouveau au-dessus du four.\nAgathe, Colin et Firmin rejoignent le chantier.\nLe four produit de la chaux : 2 calcaire + 1 poutre → 2 sacs.", 7f);
            }
        }

        // ---------- pont ----------

        void Pont(float dt)
        {
            int e = Etape;
            BlocageChantier = "";
            if (e >= 3) return;
            var c = Poste.Trouver("chantier");
            if (c == null) return;
            var b = Etapes[e];
            float part = 1f;
            var manque = new List<string>();
            void Verifier(Ressource r, float besoin, string nom)
            {
                if (besoin <= 0) return;
                float f = Mathf.Clamp01(Mathf.Floor(c.Stock(r)) / besoin);
                part = Mathf.Min(part, f);
                if (f < 1f) manque.Add($"{besoin - Mathf.Floor(c.Stock(r)):0} {nom}");
            }
            Verifier(Ressource.Pierre, b.pierre, "blocs");
            Verifier(Ressource.Chaux, b.chaux, "sacs de chaux");
            Verifier(Ressource.Bois, b.bois, "poutres");
            if (c.personnes <= 0) BlocageChantier = "Personne ne bâtit";
            if (manque.Count > 0) BlocageChantier = "Il manque " + string.Join(", ", manque);
            c.blocage = Avancement[e] >= b.jours * part - .001f && part < 1f ? BlocageChantier : "";

            Avancement[e] = Mathf.Min(Avancement[e] + c.personnes * dt / JourDuree, b.jours * part);
            if (Avancement[e] >= b.jours - .001f)
            {
                c.Prendre(Ressource.Pierre, b.pierre);
                c.Prendre(Ressource.Chaux, b.chaux);
                c.Prendre(Ressource.Bois, b.bois);
                Progression.Mettre(e == 0 ? "pont_etape1" : e == 1 ? "pont_etape2" : "pont_ouvert");
                Progression.AjouterConfiance(e == 2 ? 3 : 1);
                Sons.Jouer("decouverte", .9f);
                if (e == 2)
                {
                    if (tablier != null) tablier.enabled = true;
                    HUD.Message("PONT PROVISOIRE OUVERT",
                        $"Le pont de bois tient, au jour {Jour}.\nOn peut traverser la rivière !", 8f);
                }
                else HUD.Message("ÉTAPE TERMINÉE", $"{b.nom} : terminé.\nÉtape suivante : {Etapes[e + 1].nom}.", 5f);
            }
            MettreAJourPont();
        }

        /// <summary>Révèle les pièces du pont au fur et à mesure (le pont se construit sous les yeux).</summary>
        void MettreAJourPont()
        {
            var groupes = new[] { etape1, etape2, etape3 };
            int e = Etape;
            for (int i = 0; i < 3; i++)
            {
                if (groupes[i] == null) continue;
                float f = i < e ? 1f : i == e ? Avancement[i] / Etapes[i].jours : 0f;
                int n = groupes[i].childCount;
                int visibles = Mathf.CeilToInt(f * n - .001f);
                for (int k = 0; k < n; k++)
                {
                    var g = groupes[i].GetChild(k).gameObject;
                    bool v = k < visibles;
                    if (g.activeSelf != v) g.SetActive(v);
                }
            }
        }

        // ---------- vue logistique du Sceau ----------

        /// <summary>Une ligne ambrée par liaison active, d'autant plus épaisse qu'il y a de porteurs.</summary>
        void MettreAJourLignes()
        {
            int couche = LayerMask.NameToLayer("Sceau");
            if (couche < 0) return;
            var debit = new Dictionary<(Poste, Poste), int>();
            foreach (var o in Ouvrier.Tous)
                if (o.Disponible && o.affectation.type == TypeAffectation.Porteur)
                {
                    var k = (o.affectation.source, o.affectation.destination);
                    debit.TryGetValue(k, out int n);
                    debit[k] = n + 1;
                }
            foreach (var kv in lignes) kv.Value.gameObject.SetActive(false);
            foreach (var kv in debit)
            {
                if (!lignes.TryGetValue(kv.Key, out var lr))
                {
                    var go = new GameObject($"Flux_{kv.Key.Item1.id}_{kv.Key.Item2.id}") { layer = couche };
                    go.transform.SetParent(transform, false);
                    lr = go.AddComponent<LineRenderer>();
                    lr.useWorldSpace = true;
                    lr.sharedMaterial = matLigne;
                    lr.positionCount = 24;
                    Vector3 a = kv.Key.Item1.transform.position, b = kv.Key.Item2.transform.position;
                    for (int i = 0; i < 24; i++)
                    {
                        float t = i / 23f;
                        Vector3 p = Vector3.Lerp(a, b, t);
                        p.y = Mathf.Max(Monde.Hauteur(p.x, p.z), -.1f) + 1.5f + Mathf.Sin(t * Mathf.PI) * 5f;
                        lr.SetPosition(i, p);
                    }
                    lignes[kv.Key] = lr;
                }
                lr.gameObject.SetActive(true);
                lr.widthMultiplier = .2f + .22f * kv.Value;
            }
        }
    }
}
