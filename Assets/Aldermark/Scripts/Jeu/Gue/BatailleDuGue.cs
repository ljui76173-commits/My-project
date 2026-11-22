using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Aldermark
{
    public enum TypePiege { Chaine, Filet, Fosse, ChausseTrape, Cloches }

    /// <summary>
    /// La veillée d'armes et la bataille du Gué de Varenne (fin de l'arc 1).
    /// Après le convoi, Ilse annonce Vesk pour l'aube. Au débarcadère, le Sceau montre la route de la barge
    /// et le chemin de ses hommes ; Aldric trace les pièges (T) parmi les emplacements que propose le Plan :
    /// 1 chaîne, 3 filets lestés, 2 fosses à boue, 1 lot de chausse-trapes, 1 fil à cloches.
    /// Le soir, le discours au puits, puis (facultatif) la soirée avec Ilse sur le pont neuf.
    /// À l'aube, la bataille (voir BatailleDuGue.Combat.cs).
    /// </summary>
    public partial class BatailleDuGue : MonoBehaviour
    {
        public static bool EnCours { get; private set; }
        /// <summary>Les villageois qui ont un poste pendant la bataille : la routine du village ne les pilote plus.</summary>
        public static readonly HashSet<Habitant> Mobilises = new HashSet<Habitant>();
        /// <summary>Moral du village après le discours : 0 inquiet, 1 bon, 2 élevé.</summary>
        public static int Moral { get; private set; }
        static BatailleDuGue instance;

        class Emplacement
        {
            public TypePiege type;
            public string nom;
            public Vector2 pos;
            public bool bon, trace, tombe, detruit;
            public Transform racine, fantome, reel, arbre, filet;
            public Material matFantome, matChaine;
            public EtiquetteSceau etiquette;
            public int captures;
            public readonly List<Cendre> pris = new List<Cendre>();
        }

        static readonly Dictionary<TypePiege, int> Kits = new Dictionary<TypePiege, int>
        {
            { TypePiege.Chaine, 1 }, { TypePiege.Filet, 3 }, { TypePiege.Fosse, 2 }, { TypePiege.ChausseTrape, 1 }, { TypePiege.Cloches, 1 },
        };

        static string NomType(TypePiege t) => t switch
        {
            TypePiege.Chaine => "Chaîne de rivière",
            TypePiege.Filet => "Filet lesté",
            TypePiege.Fosse => "Fosse à boue",
            TypePiege.ChausseTrape => "Chausse-trapes",
            _ => "Fil à cloches",
        };

        static string Artisan(TypePiege t) => t switch
        {
            TypePiege.Chaine => "Hadrien la forgera cette nuit, et la chauffera au rouge avant le contact.",
            TypePiege.Filet => "Joss et Pell le suspendront, lesté de pierres, prêts à couper la corde.",
            TypePiege.Fosse => "Les fermiers la creuseront, la rempliront d'eau et d'argile, sous des planches minces.",
            TypePiege.ChausseTrape => "Hadrien forgera les pointes, on les sèmera dans l'herbe.",
            _ => Progression.A("tobin_rallie") ? "Tobin tendra le fil, comme la nuit des sonnailles." : "Bram tendra le fil, comme la nuit des sonnailles.",
        };

        readonly List<Emplacement> emplacements = new List<Emplacement>();
        Vector2 quaiBout, quaiRacine, plage, jonction, ligne;
        Vector2[] cheminA, cheminB, fuite, fuiteQuai;
        readonly List<LineRenderer> routes = new List<LineRenderer>();
        Transform quai;
        JoueurAldric j;
        float annonce = -1f, depuisAnnonce;
        bool lancementPrevu, aideMontree;
        const float CoutTrace = 3f, QuaiY = .6f;
        static readonly Color Or = new Color(1f, .82f, .35f), Bleu = new Color(.45f, .7f, 1f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reinitialiser() { EnCours = false; Mobilises.Clear(); Moral = 0; instance = null; }

        static float R(float z) => Monde.Riviere(z);
        static float Sol(Vector2 p) => Mathf.Max(Monde.Hauteur(p.x, p.y), -.1f);
        static int CoucheSceau { get { int c = LayerMask.NameToLayer("Sceau"); return c < 0 ? 0 : c; } }

        bool Preparation => Progression.A("vesk_demain") && !EnCours && !Progression.A("bataille_lancee");

        void Awake()
        {
            instance = this;
            quaiBout = new Vector2(R(14f) + 3.8f, 14f);    // en eau profonde : on y accoste, on y grimpe à la nage
            quaiRacine = new Vector2(R(14f) + 7.6f, 14f);  // la berge remonte au niveau des planches
            plage = new Vector2(R(26f) + 8f, 26f);
            ligne = new Vector2(33f, 5f);
            ConstruireQuai();
            ConstruireEmplacements();
            ConstruireRoutes();
            ConstruireGarr();
        }

        void Start()
        {
            j = FindFirstObjectByType<JoueurAldric>();
            Lieux.Declarer("gue", quai);
            Chantier.NouveauJour += Aube;
        }

        void OnDestroy() => Chantier.NouveauJour -= Aube;

        // ------------------------------------------------------------------
        // Le débarcadère, les emplacements proposés par le Plan, les routes
        // ------------------------------------------------------------------

        /// <summary>Le point voulu s'il est libre (pas de maison, de rocher, d'arbre), sinon le plus proche autour.</summary>
        static Vector2 Libre(Vector2 voulu)
        {
            Physics.SyncTransforms();
            for (int anneau = 0; anneau <= 4; anneau++)
                for (int k = 0; k < (anneau == 0 ? 1 : 10); k++)
                {
                    float a = k * Mathf.PI / 5f + anneau;
                    var q = voulu + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * anneau * 1.5f;
                    if (Monde.Hauteur(q.x, q.y) < -.3f) continue;
                    var centre = new Vector3(q.x, Monde.Hauteur(q.x, q.y) + 1.4f, q.y);
                    bool ok = true;
                    foreach (var c in Physics.OverlapSphere(centre, 1.2f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (c is TerrainCollider || c.GetComponentInParent<JoueurAldric>() != null) continue;
                        ok = false; break;
                    }
                    if (ok) return q;
                }
            return voulu;
        }

        void ConstruireQuai()
        {
            quai = new GameObject("Debarcadere").transform;
            quai.SetParent(transform, false);
            float L = quaiRacine.x - quaiBout.x + 1f;
            quai.position = new Vector3((quaiBout.x + quaiRacine.x) / 2f, QuaiY, 14f);
            var bois = Lit(new Color(.42f, .3f, .2f));
            for (int i = 0; i < 9; i++)
                Bloc(quai, new Vector3(quai.position.x, QuaiY - .05f, 14f - 1.2f + i * .3f), new Vector3(L, .1f, .27f), bois, 0, true);
            for (int i = 0; i < 4; i++)
                foreach (float dz in new[] { -1.25f, 1.25f })
                {
                    float x = quaiBout.x + i * (L - 1f) / 3f;
                    float fond = Monde.Hauteur(x, 14f + dz);
                    Bloc(quai, new Vector3(x, (QuaiY + fond) / 2f - .2f, 14f + dz), new Vector3(.22f, QuaiY - fond + .5f, .22f), bois, 0, false);
                }
        }

        void ConstruireEmplacements()
        {
            var na1 = Libre(quaiRacine + new Vector2(2.6f, -1.4f));
            var m1 = Libre(new Vector2(26.5f, 10.5f));
            jonction = Libre(new Vector2(29.5f, 8.5f));
            var nb = Libre(new Vector2(23.8f, 22.5f));
            var m2 = Libre(new Vector2(25.2f, 18.8f));
            var c1 = Libre(new Vector2(26.8f, 15.2f));
            var s2 = Libre(new Vector2(31f, 41f));
            var front = ligne - (Monde.Village - ligne).normalized * 1.3f;   // ils s'arrêtent face à la ligne de Garr
            cheminA = new[] { quaiBout, quaiRacine, na1, m1, jonction, front };
            cheminB = new[] { plage, nb, m2, c1, jonction, front };
            fuite = new[] { plage, new Vector2(25f, 33f), s2, new Vector2(38f, 56f), new Vector2(44f, 80f) };
            fuiteQuai = new[] { quaiBout, quaiRacine, new Vector2(24f, 24f), new Vector2(25f, 33f), s2, new Vector2(38f, 56f), new Vector2(44f, 80f) };

            Ajouter(TypePiege.Chaine, "Coude de la rivière", new Vector2(R(34f), 34f), true);
            Ajouter(TypePiege.Chaine, "Sous le pont neuf", new Vector2(R(-17f), -17f), false);
            Ajouter(TypePiege.Filet, "Saule du quai", na1, true);
            Ajouter(TypePiege.Filet, "Saule de la grève", nb, true);
            Ajouter(TypePiege.Filet, "Chêne du carrefour", jonction, true);
            Ajouter(TypePiege.Filet, "Verger de Corvin", Libre(new Vector2(33f, 21f)), false);
            Ajouter(TypePiege.Filet, "Saule du vieux pont", Libre(new Vector2(19.5f, 5f)), false);
            Ajouter(TypePiege.Fosse, "Chemin du quai", m1, true);
            Ajouter(TypePiege.Fosse, "Herbes de la grève", m2, true);
            Ajouter(TypePiege.Fosse, "Abords du vieux pont", Libre(new Vector2(24f, -4f)), false);
            Ajouter(TypePiege.ChausseTrape, "Prairie de la grève", c1, true);
            Ajouter(TypePiege.ChausseTrape, "Prés du village", Libre(new Vector2(40f, 15f)), false);
            Ajouter(TypePiege.Cloches, "Rive en amont — l'alerte", new Vector2(R(72f) + 11.5f, 72f), true);
            Ajouter(TypePiege.Cloches, "Sentier du bois — la fuite", s2, true);
        }

        void Ajouter(TypePiege t, string nom, Vector2 pos, bool bon)
        {
            var e = new Emplacement { type = t, nom = nom, pos = pos, bon = bon };
            e.racine = new GameObject("Piege_" + nom).transform;
            e.racine.SetParent(transform, false);
            float y = t == TypePiege.Chaine ? 0f : Sol(pos);
            e.racine.position = new Vector3(pos.x, y, pos.y);

            // Le fantôme, visible au Sceau pendant la préparation
            e.fantome = new GameObject("Fantome").transform;
            e.fantome.SetParent(e.racine, false);
            e.matFantome = Unlit(Bleu);
            int c = CoucheSceau;
            Vector3 o = e.racine.position;
            switch (t)
            {
                case TypePiege.Chaine:
                    Bloc(e.fantome, o + Vector3.up * .15f, new Vector3(19f, .25f, .25f), e.matFantome, c, false);
                    break;
                case TypePiege.Filet:
                    for (int k = -2; k <= 2; k++)
                    {
                        Bloc(e.fantome, o + new Vector3(k * .8f, .08f, 0), new Vector3(.06f, .04f, 3.4f), e.matFantome, c, false);
                        Bloc(e.fantome, o + new Vector3(0, .08f, k * .8f), new Vector3(3.4f, .04f, .06f), e.matFantome, c, false);
                    }
                    break;
                case TypePiege.Fosse:
                    Bloc(e.fantome, o + Vector3.up * .06f, new Vector3(3.2f, .05f, 2.6f), e.matFantome, c, false);
                    break;
                case TypePiege.ChausseTrape:
                    for (int k = 0; k < 9; k++)
                    {
                        float a = k * 2.4f, r = .5f + (k % 3) * .55f;
                        Bloc(e.fantome, o + new Vector3(Mathf.Cos(a) * r, .12f, Mathf.Sin(a) * r), Vector3.one * .2f, e.matFantome, c, false).rotation = Quaternion.Euler(45f, 45f, 0);
                    }
                    break;
                default:
                    Bloc(e.fantome, o + new Vector3(-1.5f, .45f, 0), new Vector3(.1f, .9f, .1f), e.matFantome, c, false);
                    Bloc(e.fantome, o + new Vector3(1.5f, .45f, 0), new Vector3(.1f, .9f, .1f), e.matFantome, c, false);
                    Bloc(e.fantome, o + new Vector3(0, .5f, 0), new Vector3(3f, .04f, .04f), e.matFantome, c, false);
                    break;
            }
            e.fantome.gameObject.SetActive(false);

            var et = new GameObject("Etiquette");
            et.transform.SetParent(e.racine, false);
            et.transform.position = o + Vector3.up * (t == TypePiege.Chaine ? 3f : 2.6f);
            e.etiquette = et.AddComponent<EtiquetteSceau>();
            e.etiquette.texte = "";
            e.etiquette.couleur = Bleu;

            // Les filets pendent à un arbre : l'arbre est là dès le début (décor)
            if (t == TypePiege.Filet) e.arbre = Arbre(e.racine, Libre(pos + new Vector2(1.7f, 1.1f)));
            emplacements.Add(e);
        }

        Transform Arbre(Transform parent, Vector2 p)
        {
            var a = new GameObject("Arbre").transform;
            a.SetParent(parent, false);
            float y = Sol(p);
            a.position = new Vector3(p.x, y, p.y);
            Bloc(a, new Vector3(p.x, y + 2f, p.y), new Vector3(.45f, 4f, .45f), Lit(new Color(.36f, .27f, .18f)), 0, true);
            var feuilles = Lit(new Color(.33f, .45f, .25f));
            Bloc(a, new Vector3(p.x, y + 4.3f, p.y), new Vector3(3.6f, 2.2f, 3.6f), feuilles, 0, false, PrimitiveType.Sphere);
            Bloc(a, new Vector3(p.x - 1f, y + 3.6f, p.y + .6f), new Vector3(2.4f, 2f, 2.4f), feuilles, 0, false, PrimitiveType.Sphere);
            Bloc(a, new Vector3(p.x + .9f, y + 3.4f, p.y - .7f), new Vector3(2.2f, 1.8f, 2.2f), feuilles, 0, false, PrimitiveType.Sphere);
            return a;
        }

        void ConstruireRoutes()
        {
            // La route de la barge (sur la rivière), le chemin des nageurs, celui de la barque, la fuite
            var barge = new List<Vector3>();
            for (float z = 120f; z >= 16f; z -= 4f) barge.Add(new Vector3(R(z), .25f, z));
            Route(barge, Bleu, .5f);
            Route(Chemin3D(cheminA), Or, .35f);
            Route(Chemin3D(cheminB), Or, .35f);
            Route(Chemin3D(fuite), new Color(.75f, .72f, .8f), .18f);
        }

        List<Vector3> Chemin3D(Vector2[] c)
        {
            var l = new List<Vector3>();
            for (int i = 0; i < c.Length - 1; i++)
                for (int k = 0; k < 6; k++)
                {
                    var p = Vector2.Lerp(c[i], c[i + 1], k / 6f);
                    l.Add(new Vector3(p.x, Sol(p) + .35f, p.y));
                }
            var f = c[c.Length - 1];
            l.Add(new Vector3(f.x, Sol(f) + .35f, f.y));
            return l;
        }

        void Route(List<Vector3> points, Color couleur, float largeur)
        {
            var go = new GameObject("Route") { layer = CoucheSceau };
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = points.Count;
            lr.SetPositions(points.ToArray());
            lr.widthMultiplier = largeur;
            lr.sharedMaterial = Unlit(couleur);
            lr.enabled = false;
            routes.Add(lr);
        }

        void ConstruireGarr()
        {
            var go = new GameObject("Garr");
            go.transform.SetParent(transform, false);
            var p = Libre(Monde.Village + new Vector2(-9f, 6f));
            go.transform.position = new Vector3(p.x, Sol(p), p.y);
            go.transform.rotation = Quaternion.LookRotation(new Vector3(-1f, 0, .3f));
            garr = go.AddComponent<Habitant>();
            garr.tunique = new Color(.36f, .3f, .24f); garr.cheveux = new Color(.2f, .17f, .15f); garr.taille = 1.1f; garr.promeneur = false;
            var it = go.AddComponent<Interlocuteur>();
            it.id = "garr"; it.nom = "Garr";
            go.SetActive(false);
        }

        // ------------------------------------------------------------------
        // Préparation : l'annonce, le tracé des pièges, le conseil de guerre
        // ------------------------------------------------------------------

        void Update()
        {
            RaccourciTest();
            if (j == null) return;
            Annonce();

            bool prep = Preparation;
            foreach (var e in emplacements)
            {
                if (e.fantome.gameObject.activeSelf != prep) e.fantome.gameObject.SetActive(prep);
                e.matFantome.SetColor("_BaseColor", e.trace ? Or : Bleu);
                e.etiquette.couleur = e.trace ? Or : Bleu;
                e.etiquette.texte = prep ? $"{NomType(e.type)} — {e.nom}{(e.trace ? "  ✓" : "")}" : "";
            }
            foreach (var r in routes) r.enabled = prep;
            if (prep) Tracer();

            if (lancementPrevu && !EnCours && !Dialogues.EnCours && !HUD.TableauOuvert) { lancementPrevu = false; Lancer(); }
            if (EnCours || phase == Phase.Fin) Combat();
            if (Progression.A("vesk_demain") && garr != null && !garr.gameObject.activeSelf) garr.gameObject.SetActive(true);
        }

        void Annonce()
        {
            if (Progression.A("convoi_arrive") && !Progression.A("vesk_demain"))
            {
                if (annonce < 0) annonce = 14f;
                annonce -= Time.deltaTime;
                if (annonce <= 0)
                {
                    Progression.Mettre("vesk_demain");
                    depuisAnnonce = 0;
                    Sons.Jouer("quete", .9f, .7f);
                    HUD.Message("ILSE — UNE BARGE NOIRE",
                        "« Mes corbeaux l'ont vu passer le col. Vesk descend la rivière sur une barge noire, avec douze Cendrés.\nIl sera au Gué demain, à l'aube. »\n\nGarr, un ancien sergent du Comte, rassemble une milice de dix hommes.\nVa au débarcadère : le Sceau (E) te montrera par où ils passeront. T : tracer un piège.", 12f);
                }
            }
            if (Progression.A("vesk_demain")) depuisAnnonce += Time.deltaTime;

            // Première visite au débarcadère avec le Sceau : l'explication
            if (Preparation && !aideMontree && j.SceauNiveau > .5f && Vector2.Distance(new Vector2(j.transform.position.x, j.transform.position.z), quaiRacine) < 35f)
            {
                aideMontree = true;
                HUD.Message("PLAN DU REGISTRE — LE DÉBARCADÈRE",
                    "Ligne bleue : la route de la barge. Lignes d'or : le chemin de ses hommes, à la nage jusqu'au quai et en barque jusqu'à la grève.\nLigne pâle : par où un fuyard filerait vers les bois.\nEn bleu, les emplacements que le Plan propose. Il y en a plus que de pièges : choisis ceux qui sont sur leur chemin.\n\nT : tracer ou effacer · 3 mana par piège", 14f);
            }
        }

        int Restants(TypePiege t)
        {
            int n = Kits[t];
            foreach (var e in emplacements) if (e.type == t && e.trace) n--;
            return n;
        }

        bool Trace(string nom)
        {
            foreach (var e in emplacements) if (e.nom == nom) return e.trace;
            return false;
        }

        void Tracer()
        {
            Emplacement vise = null;
            if (j.SceauNiveau > .5f && !Dialogues.EnCours && !HUD.TableauOuvert)
            {
                var cam = j.cam.transform;
                float meilleur = 12f;
                foreach (var e in emplacements)
                {
                    Vector3 p = e.racine.position + Vector3.up * .5f;
                    Vector3 d = p - cam.position;
                    if (d.magnitude > (e.type == TypePiege.Chaine ? 50f : 30f)) continue;
                    float a = Vector3.Angle(cam.forward, d);
                    if (a < meilleur) { meilleur = a; vise = e; }
                }
            }
            if (vise == null) return;
            int restants = Restants(vise.type);
            HUD.InviteForte(vise.trace ? $"[T]  Effacer : {NomType(vise.type)} — {vise.nom}"
                : restants > 0 ? $"[T]  Tracer : {NomType(vise.type)} — {vise.nom}   ({restants} en réserve · {CoutTrace:0} mana)"
                : $"{NomType(vise.type)} : plus rien en réserve. Efface-en un (T) pour le déplacer.");

            var kb = Keyboard.current; var pad = Gamepad.current;
            if (!((kb != null && kb.tKey.wasPressedThisFrame) || (pad != null && pad.rightShoulder.wasPressedThisFrame))) return;
            if (vise.trace)
            {
                vise.trace = false;
                if (vise.reel != null) Destroy(vise.reel.gameObject);
                vise.reel = null; vise.filet = null; vise.matChaine = null;
                Sons.Jouer("sceauOff", .4f, 1.3f);
                return;
            }
            if (restants <= 0) return;
            if (!j.Depenser(CoutTrace)) { HUD.Message("PLAN DU REGISTRE", "Pas assez de mana pour projeter ce piège.", 2.5f); return; }
            vise.trace = true;
            Construire(vise);
            Sons.JouerA("decouverte", vise.racine.position, .5f, 1.3f);
            HUD.Message("PIÈGE TRACÉ — " + NomType(vise.type).ToUpper(), $"{vise.nom}.\n{Artisan(vise.type)}", 4f);

            bool complet = true;
            foreach (var t in Kits.Keys) if (Restants(t) > 0) complet = false;
            if (complet && !Progression.A("pieges_traces"))
            {
                Progression.Mettre("pieges_traces");
                Sons.Jouer("quete", .8f, 1f);
                HUD.Message("CONSEIL DE GUERRE — DANS LA CUISINE DE MARTHE",
                    "1. Le blocage : la barge bute sur la chaîne, en eau profonde. Ils devront nager, ou prendre leur barque.\n2. La rive : ceux qui débarquent tombent dans la boue, sous les filets, sur les pointes. Marthe tire sur ceux qui se libèrent.\n3. La ligne : la milice de Garr tient le seul chemin sec, avec Hadrien et Bram.\n4. Vesk : dès qu'il puise dans le nœud, tu le siphonnes (R maintenu), à moins de 30 m.\n\n« Personne ne meurt sauf s'il le faut. » Garr lève les yeux au ciel. Ilse ne dit rien.", 16f);
            }
        }

        /// <summary>Le piège tracé devient réel (les villageois le préparent pendant la nuit).</summary>
        void Construire(Emplacement e)
        {
            e.reel = new GameObject("Reel").transform;
            e.reel.SetParent(e.racine, false);
            Vector3 o = e.racine.position;
            var fer = Lit(new Color(.2f, .2f, .22f));
            var bois = Lit(new Color(.4f, .28f, .17f));
            switch (e.type)
            {
                case TypePiege.Chaine:
                    e.matChaine = new Material(Lit(new Color(.2f, .2f, .22f)));
                    int maillon = 0;
                    for (float x = -9.4f; x <= 9.4f; x += .55f)
                        Bloc(e.reel, o + new Vector3(x, -.3f, 0), new Vector3(.5f, .16f, .16f), e.matChaine, 0, false).rotation = Quaternion.Euler(maillon++ % 2 == 0 ? 0f : 90f, 0, 0);
                    foreach (float x in new[] { -9.8f, 9.8f })
                        Bloc(e.reel, new Vector3(o.x + x, Monde.Hauteur(o.x + x, o.z) + .5f, o.z), new Vector3(.4f, 1.4f, .4f), bois, 0, false);
                    break;
                case TypePiege.Filet:
                    Vector3 cime = e.arbre != null ? e.arbre.position + Vector3.up * 3.3f : o + Vector3.up * 3.3f;
                    e.filet = new GameObject("Filet").transform;
                    e.filet.SetParent(e.reel, false);
                    e.filet.position = cime;
                    var corde = Lit(new Color(.62f, .55f, .4f));
                    for (int k = -3; k <= 3; k++)
                    {
                        Bloc(e.filet, cime + new Vector3(k * .55f, 0, 0), new Vector3(.05f, .05f, 3.6f), corde, 0, false);
                        Bloc(e.filet, cime + new Vector3(0, 0, k * .55f), new Vector3(3.6f, .05f, .05f), corde, 0, false);
                    }
                    foreach (var q in new[] { new Vector3(-1.7f, 0, -1.7f), new Vector3(1.7f, 0, -1.7f), new Vector3(-1.7f, 0, 1.7f), new Vector3(1.7f, 0, 1.7f) })
                        Bloc(e.filet, cime + q, Vector3.one * .25f, Lit(new Color(.5f, .5f, .48f)), 0, false, PrimitiveType.Sphere);
                    e.filet.localScale = new Vector3(.25f, 1f, .25f);   // roulé dans les branches
                    break;
                case TypePiege.Fosse:
                    Bloc(e.reel, o + Vector3.up * .03f, new Vector3(3f, .06f, 2.4f), Lit(new Color(.3f, .22f, .14f)), 0, false);
                    for (int k = -2; k <= 2; k++)
                        Bloc(e.reel, o + new Vector3(k * .6f, .08f, 0), new Vector3(.3f, .04f, 2.6f), Lit(new Color(.52f, .4f, .26f)), 0, false);
                    break;
                case TypePiege.ChausseTrape:
                    for (int k = 0; k < 16; k++)
                    {
                        float a = k * 2.4f, r = .3f + (k % 4) * .45f;
                        Bloc(e.reel, new Vector3(o.x + Mathf.Cos(a) * r, Sol(new Vector2(o.x + Mathf.Cos(a) * r, o.z + Mathf.Sin(a) * r)) + .06f, o.z + Mathf.Sin(a) * r), Vector3.one * .12f, fer, 0, false).rotation = Quaternion.Euler(45f, k * 30f, 45f);
                    }
                    break;
                default:
                    Bloc(e.reel, o + new Vector3(-1.5f, .45f, 0), new Vector3(.08f, .9f, .08f), bois, 0, false);
                    Bloc(e.reel, o + new Vector3(1.5f, .45f, 0), new Vector3(.08f, .9f, .08f), bois, 0, false);
                    Bloc(e.reel, o + new Vector3(0, .5f, 0), new Vector3(3f, .02f, .02f), Lit(new Color(.85f, .82f, .7f)), 0, false);
                    for (int k = -1; k <= 1; k++) Bloc(e.reel, o + new Vector3(k * .8f, .38f, 0), Vector3.one * .14f, Lit(new Color(.85f, .7f, .25f)), 0, false);
                    break;
            }
        }

        // ------------------------------------------------------------------
        // La veillée : le discours au puits, Ilse sur le pont
        // ------------------------------------------------------------------

        /// <summary>Le discours au puits : le village frappe le sol en rythme (moral 0 à 2).</summary>
        public static void Discours(int moral)
        {
            Moral = moral;
            Progression.Mettre("discours");
            if (instance == null) return;
            instance.StartCoroutine(instance.Frapper(moral));
            instance.IlseAuPont();
        }

        System.Collections.IEnumerator Frapper(int moral)
        {
            yield return new WaitForSeconds(1.2f);
            int coups = moral == 2 ? 22 : moral == 1 ? 12 : 3;
            for (int i = 0; i < coups; i++)
            {
                float rythme = Mathf.Lerp(.9f, .45f, i / (float)coups);
                Sons.Jouer("coup", Mathf.Lerp(.35f, .9f, i / (float)coups), .45f + (i % 2) * .05f);
                yield return new WaitForSeconds(rythme);
            }
            HUD.Message("MORAL DE VARENNE", moral == 2 ? "Élevé. Tout le village frappe le sol, en rythme." : moral == 1 ? "Bon. On serre les poings, on hoche la tête." : "Inquiet. Les gens rentrent chez eux en silence.", 4f);
        }

        void IlseAuPont()
        {
            var q = FindFirstObjectByType<QueteIlse>();
            var c = Chantier.Instance;
            if (q == null || q.ilse == null || c == null) return;
            q.ilse.SetActive(true);
            var p = new Vector3(c.centrePont.x + c.longueurPont / 2f + 1.6f, 0, c.centrePont.z + .8f);
            p.y = Monde.Hauteur(p.x, p.z);
            q.ilse.transform.SetPositionAndRotation(p, Quaternion.LookRotation(Vector3.left));
            var h = q.ilse.GetComponent<Habitant>();
            if (h != null) h.AllerVers(new Vector2(p.x, p.z), 1f);
        }

        /// <summary>À l'aube qui suit l'annonce (en dormant ou en veillant), Vesk arrive.</summary>
        void Aube()
        {
            if (Progression.A("vesk_demain") && !Progression.A("bataille_lancee") && depuisAnnonce > 40f) lancementPrevu = true;
            if (Progression.A("bataille_finie")) Nettoyer();
        }

        void RaccourciTest()
        {
            if (!Application.isEditor) return;
            var kb = Keyboard.current;
            if (kb == null || !kb.f7Key.wasPressedThisFrame) return;
            if (!Progression.A("vesk_demain"))
            {
                foreach (var d in new[] { "prologue_fini", "pacte_signe", "lise", "test_four", "vu_four", "vu_calcaire", "vu_cendres", "four_ok", "sabotage_connu", "bram", "vu_corvin", "pont_accepte", "plan_bois", "four_repare",
                                          "sonnailles_donnees", "sonnailles_resolues", "ilse_arrivee", "ilse_rencontre", "voile", "info_noeud", "noeud_touche", "siphon", "pont_etape1", "pont_etape2", "pont_ouvert", "convoi_arrive", "vesk_demain" })
                    Progression.Mettre(d);
                depuisAnnonce = 100f;
                HUD.Message("TEST", "Veille de la bataille. F7 de nouveau : Vesk arrive tout de suite.", 4f);
            }
            else if (!Progression.A("bataille_lancee")) { depuisAnnonce = 100f; lancementPrevu = true; }
        }

        // ------------------------------------------------------------------
        // Petits outils de construction
        // ------------------------------------------------------------------

        static readonly Dictionary<Color, Material> matsLit = new Dictionary<Color, Material>();

        static Material Lit(Color c)
        {
            if (matsLit.TryGetValue(c, out var m) && m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", 0f);
            matsLit[c] = m;
            return m;
        }

        static Material Unlit(Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", c);
            return m;
        }

        static Transform Bloc(Transform parent, Vector3 pos, Vector3 taille, Material m, int couche, bool collision, PrimitiveType type = PrimitiveType.Cube)
        {
            var g = GameObject.CreatePrimitive(type);
            if (!collision) Destroy(g.GetComponent<Collider>());
            g.layer = couche;
            g.transform.SetParent(parent, true);
            g.transform.position = pos;
            g.transform.localScale = taille;
            g.GetComponent<MeshRenderer>().sharedMaterial = m;
            return g.transform;
        }

        static Transform Local(Transform parent, Vector3 pos, Vector3 taille, Color c, Vector3 rot = default, PrimitiveType type = PrimitiveType.Cube)
        {
            var g = GameObject.CreatePrimitive(type);
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(rot);
            g.transform.localScale = taille;
            g.GetComponent<MeshRenderer>().sharedMaterial = Lit(c);
            return g.transform;
        }
    }
}
