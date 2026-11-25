using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Aldermark
{
    /// <summary>
    /// Le combat d'action du héros, l'arme en main ou à mains nues (H range ou dégaine l'arme équipée).
    /// Clic gauche : enchaînement de 3 coups (taille, revers, estoc ; à mains nues : direct, direct, crochet).
    /// Maintenu : coup lourd (à mains nues : coup de pied qui repousse fort).
    /// En courant : fente (un estoc qui avale 3 m). En tombant : coup plongeant et onde de choc.
    /// Clic droit : garde ; levée juste avant l'impact, parade parfaite, et le coup suivant est une riposte (×3).
    /// Alt : roulade (invulnérable un instant). Clic molette : verrouiller la cible.
    /// Un coup fort repousse : l'épée se plante dans le sol et on glisse en arrière ; très fort, il projette.
    /// La lame suit une trajectoire dans l'espace (devant la caméra ou devant le corps) ;
    /// avec un modèle riggé, la main droite suit la poignée (IK).
    /// </summary>
    [RequireComponent(typeof(JoueurAldric))]
    public class CombatHeros : MonoBehaviour, ICible
    {
        public static CombatHeros Instance { get; private set; }

        enum Etat { Libre, Preparation, Frappe, Recuperation, Roulade, Sonne, Glissade, Projete, Plongeon, Choc }

        class Mouvement
        {
            public string nom; public float prep, frappe, recup, mult, poussee, cout, cone, elan; public bool lourde, critiqueSur, pied;
            public Vector3 g0, g1, d0, d1;   // la main droite (repère caméra) et la direction de la lame, au début et à la fin de la frappe
            public Vector3 gg0, gg1;          // la main gauche (zéro : au repos)
        }

        static readonly Mouvement[] Enchainement =
        {
            new Mouvement { nom = "Taille", prep = .12f, frappe = .13f, recup = .26f, mult = 1f, poussee = .35f, cout = 8f, cone = 75f,
                g0 = new Vector3(.55f, -.12f, .5f), d0 = new Vector3(.85f, .25f, .45f), g1 = new Vector3(-.35f, -.38f, .7f), d1 = new Vector3(-.85f, -.15f, .5f) },
            new Mouvement { nom = "Revers", prep = .1f, frappe = .13f, recup = .26f, mult = 1.1f, poussee = .35f, cout = 8f, cone = 75f,
                g0 = new Vector3(-.4f, -.1f, .5f), d0 = new Vector3(-.85f, .3f, .45f), g1 = new Vector3(.45f, -.38f, .7f), d1 = new Vector3(.85f, -.2f, .45f) },
            new Mouvement { nom = "Estoc", prep = .16f, frappe = .12f, recup = .38f, mult = 1.35f, poussee = .9f, cout = 9f, cone = 40f,
                g0 = new Vector3(.25f, -.32f, .35f), d0 = new Vector3(0f, .05f, 1f), g1 = new Vector3(.08f, -.2f, 1.05f), d1 = new Vector3(0f, 0f, 1f) },
        };
        static readonly Mouvement Lourde = new Mouvement
        {
            nom = "Coup lourd", prep = .38f, frappe = .16f, recup = .5f, mult = 2f, poussee = 1.8f, cout = 20f, cone = 55f, lourde = true,
            g0 = new Vector3(.18f, .32f, .35f), d0 = new Vector3(0f, .85f, -.5f), g1 = new Vector3(.05f, -.55f, .75f), d1 = new Vector3(0f, -.7f, .7f),
        };
        static readonly Mouvement Fente = new Mouvement
        {
            nom = "Fente", prep = .08f, frappe = .2f, recup = .4f, mult = 1.5f, poussee = 1.2f, cout = 14f, cone = 35f, elan = 14f,
            g0 = new Vector3(.3f, -.3f, .3f), d0 = new Vector3(0f, .05f, 1f), g1 = new Vector3(.05f, -.18f, 1.15f), d1 = new Vector3(0f, -.02f, 1f),
        };
        static readonly Mouvement Riposte = new Mouvement
        {
            nom = "Riposte", prep = .05f, frappe = .12f, recup = .3f, mult = 3f, poussee = 1.6f, cout = 4f, cone = 70f, critiqueSur = true,
            g0 = new Vector3(.5f, .05f, .45f), d0 = new Vector3(.8f, .4f, .4f), g1 = new Vector3(-.4f, -.42f, .75f), d1 = new Vector3(-.8f, -.3f, .5f),
        };
        static readonly Mouvement Plongeante = new Mouvement
        {
            nom = "Coup plongeant", prep = 0f, frappe = 99f, recup = .45f, mult = 1.6f, poussee = 2f, cout = 12f, cone = 360f, lourde = true,
            g0 = new Vector3(.1f, .1f, .5f), d0 = new Vector3(0f, -.9f, .45f), g1 = new Vector3(.05f, -.5f, .7f), d1 = new Vector3(0f, -.95f, .3f),
        };
        static readonly Mouvement CoupDeGrace = new Mouvement
        {
            nom = "Coup de grâce", prep = .12f, frappe = .16f, recup = .55f, mult = 4f, poussee = 2.4f, cout = 10f, cone = 70f, lourde = true, critiqueSur = true,
            g0 = new Vector3(.15f, .35f, .35f), d0 = new Vector3(0f, .8f, -.4f), g1 = new Vector3(.05f, -.5f, .8f), d1 = new Vector3(0f, -.85f, .5f),
        };
        // ---------- à mains nues ----------
        static readonly Vector3 PoingD = new Vector3(.27f, -.3f, .55f), PoingG = new Vector3(-.27f, -.28f, .58f);
        static readonly Vector3 CroixD = new Vector3(-.09f, -.1f, .42f), CroixG = new Vector3(.09f, -.15f, .44f);   // garde bras en croix
        static readonly Vector3 GaucheBas = new Vector3(-.5f, -1f, .5f);   // main gauche hors champ quand l'épée est en main
        static readonly Mouvement[] Poings =
        {
            new Mouvement { nom = "Direct du gauche", prep = .06f, frappe = .1f, recup = .16f, mult = .7f, poussee = .2f, cout = 5f, cone = 60f,
                g0 = PoingD, g1 = PoingD, gg0 = new Vector3(-.3f, -.3f, .48f), gg1 = new Vector3(-.1f, -.13f, 1f) },
            new Mouvement { nom = "Direct du droit", prep = .07f, frappe = .1f, recup = .18f, mult = .8f, poussee = .25f, cout = 5f, cone = 60f,
                g0 = new Vector3(.32f, -.3f, .46f), g1 = new Vector3(.06f, -.11f, 1.02f), gg0 = PoingG, gg1 = PoingG },
            new Mouvement { nom = "Crochet", prep = .1f, frappe = .12f, recup = .3f, mult = 1.1f, poussee = .5f, cout = 7f, cone = 85f,
                g0 = new Vector3(.6f, -.15f, .5f), g1 = new Vector3(-.18f, -.1f, .8f), gg0 = PoingG, gg1 = PoingG },
        };
        static readonly Mouvement CoupDePied = new Mouvement
        {
            nom = "Coup de pied", prep = .3f, frappe = .15f, recup = .42f, mult = 1.5f, poussee = 2.1f, cout = 16f, cone = 50f, lourde = true, pied = true,
            g0 = PoingD, g1 = PoingD, gg0 = PoingG, gg1 = PoingG,
        };
        static readonly Mouvement Charge = new Mouvement
        {
            nom = "Charge à l'épaule", prep = .06f, frappe = .25f, recup = .45f, mult = 1.2f, poussee = 1.8f, cout = 14f, cone = 45f, elan = 13f,
            g0 = new Vector3(.18f, -.28f, .4f), g1 = new Vector3(.18f, -.28f, .4f), gg0 = new Vector3(-.18f, -.26f, .42f), gg1 = new Vector3(-.18f, -.26f, .42f),
        };
        static readonly Mouvement Uppercut = new Mouvement
        {
            nom = "Uppercut", prep = .05f, frappe = .12f, recup = .3f, mult = 2.6f, poussee = 1.4f, cout = 4f, cone = 70f, critiqueSur = true,
            g0 = new Vector3(.2f, -.62f, .55f), g1 = new Vector3(.05f, .08f, .85f), gg0 = PoingG, gg1 = PoingG,
        };
        static readonly Mouvement GraceMainsNues = new Mouvement
        {
            nom = "Coup de grâce", prep = .15f, frappe = .15f, recup = .55f, mult = 3.5f, poussee = 2.4f, cout = 10f, cone = 70f, lourde = true, critiqueSur = true,
            g0 = new Vector3(.62f, .12f, .3f), g1 = new Vector3(0f, -.12f, 1.02f), gg0 = PoingG, gg1 = PoingG,
        };

        static readonly Vector3 GardeRepos = new Vector3(.42f, -.42f, .75f), LameRepos = new Vector3(-.15f, .75f, .65f);
        static readonly Vector3 GardeHaute = new Vector3(.12f, -.22f, .6f), LameGarde = new Vector3(-.92f, .3f, .2f);
        static readonly Vector3 MainPlantee = new Vector3(.28f, -.5f, .62f), LamePlantee = new Vector3(.05f, -.95f, .35f);

        const float SeuilLourd = .28f, FenetreParade = .22f, FenetreRiposte = .7f, CoutRoulade = 18f, DureeRoulade = .45f, CoutGarde = 10f;

        JoueurAldric j;
        StatsHeros stats;
        Inventaire inv;
        Transform modeleArme, botte;
        string armeConstruite;
        Vector3 mainG = GaucheBas;
        float tRangement = -1f;   // > 0 : on range ou on dégaine
        bool rangementBascule;
        Etat etat;
        Mouvement courante;
        float tEtat, appui = -1f, debutGarde = -9f, finFige, finRalenti, sonne, finRiposte, dureeGlissade, prochainSillon, demandeRelevement = -9f, hauteurPlongeon;
        int etapeCombo;
        bool comboDemande, garde, verrouillePrecedent, mort, aQuitteLeSol;
        Vector3 dirGlissade;
        readonly HashSet<ICible> touches = new HashSet<ICible>();
        ICible verrou;
        Vector3 main = GardeRepos, lame = LameRepos;
        Material matTrace;
        Monstre adversaire;   // choc des lames en cours
        Combattant grace;     // ennemi sonné à portée : coup de grâce possible
        float equilibre, debutChoc, prochaineEtincelle, ratioChoc = 1f;

        public bool Occupe => etat != Etat.Libre || garde;
        public bool EnGarde => garde;
        /// <summary>L'arme en main (null : mains nues, arme rangée ou aucune).</summary>
        public DefObjet ArmeEnMain => inv != null ? inv.ArmeEnMain : null;
        public bool MainsNues => ArmeEnMain == null;
        float Portee => MainsNues ? 1.6f : ArmeEnMain.portee;
        public double Attaque
        {
            get
            {
                if (stats == null) return 5;
                var a = ArmeEnMain;
                return a == null ? stats.Valeur(Stat.FOR) * 1.1 : stats.Valeur(Stat.FOR) + a.degats * stats.Facteur;
            }
        }
        public double Defense => stats != null ? stats.Valeur(Stat.END) * .6 : 2;
        public double ChanceCritique => stats == null ? .05 : Mathf.Clamp(.05f + .02f * Mathf.Log(Mathf.Max(1f, (float)(stats.Valeur(Stat.AGI) / stats.baseStat))) + (ArmeEnMain != null ? ArmeEnMain.critique : 0f), 0f, .5f);

        Mouvement[] Combo => MainsNues ? Poings : Enchainement;
        Mouvement MvLourd => MainsNues ? CoupDePied : Lourde;
        Mouvement MvFente => MainsNues ? Charge : Fente;
        Mouvement MvRiposte => MainsNues ? Uppercut : Riposte;
        Mouvement MvGrace => MainsNues ? GraceMainsNues : CoupDeGrace;
        public ICible Verrou => verrou;

        // ---------- ICible (le héros reçoit des coups) ----------

        public Camp Camp => Camp.Heros;
        public bool Vivant => !mort && stats != null && stats.Vie > 0;
        public Vector3 Centre => transform.position + Vector3.up * 1.1f;
        public string NomAffiche => stats != null ? stats.nomHeros : "Aldric";
        public int NiveauAffiche => stats != null ? stats.Niveau : 1;
        public double VieActuelle => stats != null ? stats.Vie : 0;
        public double VieMaximale => stats != null ? stats.VieMax : 1;
        public Transform Corps => transform;

        void Awake()
        {
            Instance = this;
            j = GetComponent<JoueurAldric>();
            stats = GetComponent<StatsHeros>();
            inv = GetComponent<Inventaire>();
            if (inv == null && stats != null) inv = gameObject.AddComponent<Inventaire>();
        }

        void Start()
        {
            botte = ConstruireBotte();
            matTrace = Effets.Transparent(Color.white);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
        }

        void OnDisable() { Time.timeScale = 1f; finFige = finRalenti = 0; }

        // ---------- boucle ----------

        void Update()
        {
            if (!MondeRPG.Actif || stats == null) return;
            GererTemps();
            float dt = Time.deltaTime;
            var kb = Keyboard.current;
            var souris = Mouse.current;
            var pad = Gamepad.current;
            bool libreDeJouer = !HUD.MenuOuvert && !Dialogues.EnCours && !mort && Cursor.lockState == CursorLockMode.Locked && verrouillePrecedent;
            verrouillePrecedent = Cursor.lockState == CursorLockMode.Locked;

            bool attaqueEnfoncee = libreDeJouer && ((souris != null && souris.leftButton.isPressed) || (pad != null && pad.rightShoulder.isPressed));
            bool attaquePressee = libreDeJouer && ((souris != null && souris.leftButton.wasPressedThisFrame) || (pad != null && pad.rightShoulder.wasPressedThisFrame));
            bool gardeEnfoncee = libreDeJouer && ((souris != null && souris.rightButton.isPressed) || (pad != null && pad.leftShoulder.isPressed));
            bool gardePressee = libreDeJouer && ((souris != null && souris.rightButton.wasPressedThisFrame) || (pad != null && pad.leftShoulder.wasPressedThisFrame));
            bool roulerPresse = libreDeJouer && ((kb != null && kb.leftAltKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame));
            bool verrouPresse = libreDeJouer && ((souris != null && souris.middleButton.wasPressedThisFrame) || (pad != null && pad.rightStickButton.wasPressedThisFrame));

            if (!libreDeJouer) appui = -1f;
            if (etat == Etat.Choc)
            {
                MettreAJourChoc(attaquePressee);
                appui = -1f;
                j.courseInterdite = true;
                j.entrave = 0f;
                return;
            }
            bool rangerPresse = libreDeJouer && ((kb != null && kb.hKey.wasPressedThisFrame) || (pad != null && pad.dpad.down.wasPressedThisFrame));
            if (rangerPresse && etat == Etat.Libre && tRangement < 0f)
            {
                if (inv == null || inv.Arme == null) HUD.Toast("Pas d'arme équipée : ouvre l'inventaire (I)");
                else { tRangement = .32f; rangementBascule = false; garde = false; }
            }
            if (tRangement >= 0f)
            {
                tRangement -= dt;
                if (!rangementBascule && tRangement < .16f) { rangementBascule = true; inv.BasculerDegaine(); }
                if (tRangement < 0f) tRangement = -1f;
                appui = -1f; attaquePressee = false; gardeEnfoncee = false;   // on ne frappe pas en rangeant
            }
            grace = etat == Etat.Libre && libreDeJouer ? CibleDeGrace() : null;
            if (grace != null) HUD.InviteForte("[Clic gauche]  Coup de grâce");
            if (verrouPresse) BasculerVerrou();
            if (verrou != null && (!Cibles.Existe(verrou) || !verrou.Vivant || Vector3.Distance(verrou.Centre, transform.position) > 25f)) LacherVerrou();

            // En l'air, assez haut : le clic devient un coup plongeant
            if (attaquePressee && !j.AuSol && etat == Etat.Libre)
            {
                float h = HauteurSol();
                if (h > .85f) { Plonger(h); attaquePressee = false; }
            }

            // Appui long = coup lourd, appui court = enchaînement (ou fente en courant, ou riposte après une parade parfaite)
            if (attaquePressee) appui = 0f;
            if (appui >= 0f)
            {
                appui += dt;
                if (!attaqueEnfoncee)
                {
                    if (appui < SeuilLourd) DemanderCoupLeger();
                    appui = -1f;
                }
                else if (appui >= SeuilLourd && (etat == Etat.Libre || etat == Etat.Recuperation))
                {
                    Lancer(MvLourd);
                    appui = -1f;
                }
            }

            // Garde : seulement libre (ou en fin de coup), elle coûte de l'endurance quand elle encaisse
            if (gardePressee) debutGarde = Time.time;
            bool peutGarder = etat == Etat.Libre || etat == Etat.Recuperation;
            if (gardeEnfoncee && peutGarder && stats.Endurance > 0)
            {
                if (etat == Etat.Recuperation) { etat = Etat.Libre; courante = null; }
                garde = true;
            }
            else if (etat != Etat.Glissade) garde = false;

            if (roulerPresse && (etat == Etat.Libre || etat == Etat.Recuperation) && !garde) Rouler();
            if (roulerPresse && etat == Etat.Projete) demandeRelevement = Time.time;   // se relever d'une roulade à l'atterrissage

            AvancerEtat(dt);
            j.courseInterdite = etat != Etat.Libre || garde;
            j.entrave = Entrave();
            j.verrou = Cibles.Existe(verrou) ? verrou.Corps : null;
            if (mort && !j.troisiemePersonne) j.decalageCamera = Vector3.Lerp(j.decalageCamera, new Vector3(0, -1.15f, 0), Mathf.Clamp01(Time.unscaledDeltaTime * 2.5f));
        }

        /// <summary>Combien le héros peut marcher pendant ce qu'il fait (1 : librement, 0 : pas du tout).</summary>
        float Entrave()
        {
            if (mort) return 0f;
            if (garde && etat == Etat.Libre) return .45f;
            switch (etat)
            {
                case Etat.Preparation:
                case Etat.Frappe: return courante != null && courante.elan > 0 ? 0f : .3f;
                case Etat.Recuperation: return .6f;
                case Etat.Sonne: return .2f;
                case Etat.Roulade:
                case Etat.Glissade:
                case Etat.Projete: return 0f;
                case Etat.Plongeon: return .4f;
                case Etat.Choc: return 0f;
                default: return 1f;
            }
        }

        void LateUpdate()
        {
            if (!MondeRPG.Actif || botte == null) return;
            PoserLame();
        }

        void GererTemps()
        {
            float t = Time.unscaledTime;
            if (finFige > 0 && t >= finFige) { finFige = 0; Time.timeScale = finRalenti > t ? .35f : 1f; }
            if (finRalenti > 0 && t >= finRalenti && finFige <= 0) { finRalenti = 0; Time.timeScale = 1f; }
        }

        void Figer(float secondes)
        {
            finFige = Time.unscaledTime + secondes;
            Time.timeScale = .05f;
        }

        void Ralentir(float secondes)
        {
            finRalenti = Time.unscaledTime + secondes;
            if (finFige <= 0) Time.timeScale = .35f;
        }

        // ---------- attaques ----------

        void DemanderCoupLeger()
        {
            if (etat == Etat.Libre)
            {
                if (grace != null) { Lancer(MvGrace); Ralentir(.5f); return; }
                if (Time.time < finRiposte) { finRiposte = 0; Lancer(MvRiposte); Ralentir(.3f); return; }
                if (j.EnCourse) { Lancer(MvFente); return; }
                etapeCombo = 0;
                Lancer(Combo[0]);
            }
            else if ((etat == Etat.Frappe || etat == Etat.Recuperation || etat == Etat.Preparation) && courante != null && !courante.lourde && courante.elan <= 0) comboDemande = true;
        }

        void Lancer(Mouvement a)
        {
            if (stats.Endurance < a.cout * .5f)
            {
                HUD.Toast("Trop essoufflé pour frapper");
                comboDemande = false;
                if (etat == Etat.Recuperation && courante == null) etat = Etat.Libre;
                return;
            }
            stats.Depenser(a.cout);
            courante = a;
            etat = Etat.Preparation;
            tEtat = 0f;
            comboDemande = false;
            touches.Clear();
            garde = false;
        }

        void Plonger(float hauteur)
        {
            stats.Depenser(Plongeante.cout);
            courante = Plongeante;
            etat = Etat.Plongeon; tEtat = 0f;
            hauteurPlongeon = hauteur;
            touches.Clear();
            j.VitesseVerticale = Mathf.Min(j.VitesseVerticale, -16f);
            Sons.Jouer("lameLourde", .7f, .8f);
        }

        /// <summary>La hauteur sous les pieds (rayon vers le bas ; sinon le relief).</summary>
        float HauteurSol()
        {
            Vector3 p = transform.position;
            if (Physics.Raycast(p + Vector3.up * .2f, Vector3.down, out var hit, 60f, ~0, QueryTriggerInteraction.Ignore))
                return hit.distance - .2f;
            return p.y - Monde.Hauteur(p.x, p.z);
        }

        void AvancerEtat(float dt)
        {
            tEtat += dt;
            switch (etat)
            {
                case Etat.Preparation:
                    if (tEtat >= courante.prep)
                    {
                        etat = Etat.Frappe; tEtat = 0f;
                        if (MainsNues) Sons.Jouer("lame", courante.pied ? .5f : .35f, Random.Range(1.5f, 1.8f));
                        else
                        {
                            Sons.Jouer(courante.lourde ? "lameLourde" : "lame", .6f, Random.Range(.92f, 1.08f) * (ArmeEnMain.forme == FormeArme.Gourdin ? .8f : 1f));
                            Trace(courante);
                        }
                        if (courante.elan > 0) j.Elan(Vector3.ProjectOnPlane(j.cam.transform.forward, Vector3.up).normalized * courante.elan, courante.frappe, 3f);
                    }
                    break;
                case Etat.Frappe:
                    ChercherTouches(courante);
                    if (tEtat >= courante.frappe) { etat = Etat.Recuperation; tEtat = 0f; }
                    break;
                case Etat.Recuperation:
                    if (comboDemande && !courante.lourde && courante.elan <= 0 && etapeCombo < Combo.Length - 1 && tEtat > courante.recup * .25f)
                    {
                        etapeCombo++;
                        Lancer(Combo[etapeCombo]);
                    }
                    else if (tEtat >= courante.recup) { etat = Etat.Libre; courante = null; etapeCombo = 0; }
                    break;
                case Etat.Roulade:
                    float u = Mathf.Clamp01(tEtat / DureeRoulade);
                    if (!j.troisiemePersonne) j.decalageCamera = new Vector3(0, -Mathf.Sin(u * Mathf.PI) * .5f, 0);
                    if (tEtat >= DureeRoulade) { etat = Etat.Libre; j.decalageCamera = Vector3.zero; }
                    break;
                case Etat.Sonne:
                    sonne -= dt;
                    if (sonne <= 0) etat = Etat.Libre;
                    break;
                case Etat.Glissade:
                    Glisser();
                    if (tEtat >= dureeGlissade) { etat = Etat.Libre; garde = false; }
                    break;
                case Etat.Projete:
                    if (!j.AuSol) aQuitteLeSol = true;
                    if ((aQuitteLeSol && j.AuSol) || tEtat > 2f)
                    {
                        Effets.Poussiere(transform.position + transform.forward * .3f, 22);
                        Sons.Jouer("impactChair", .8f, .6f);
                        j.Secouer(.12f);
                        etat = Etat.Libre;
                        if (Time.time - demandeRelevement < .45f && Rouler())
                        {
                            HUD.ChiffreTexte(SurSoi(), "Relevé !", new Color(.7f, .95f, 1f));
                            stats.Exercer(Stat.AGI, 1.5);
                        }
                        else CommencerGlissade(dirGlissade, 3.2f, false);
                    }
                    break;
                case Etat.Plongeon:
                    if (j.AuSol || tEtat > 3f) Atterrir();
                    break;
            }
        }

        void ChercherTouches(Mouvement a)
        {
            Vector3 avant = Vector3.ProjectOnPlane(j.cam.transform.forward, Vector3.up).normalized;
            if (avant.sqrMagnitude < .01f) avant = transform.forward;
            Vector3 origine = transform.position + Vector3.up * 1.1f;
            float portee = Portee + (a.pied ? .3f : 0f);
            Vector3 centre = origine + avant * portee * .55f;
            foreach (var c in Physics.OverlapSphere(centre, portee * .65f, ~0, QueryTriggerInteraction.Collide))
            {
                var cible = c.GetComponentInParent<ICible>();
                if (cible == null || ReferenceEquals(cible, this) || !cible.Vivant || touches.Contains(cible)) continue;
                if (cible.Camp == Camp.Heros || cible.Camp == Camp.Allie) continue;
                Vector3 plat = Vector3.ProjectOnPlane(cible.Centre - origine, Vector3.up);
                if (plat.magnitude > portee + .8f || Vector3.Angle(avant, plat) > a.cone) continue;
                touches.Add(cible);
                Frapper(cible, a, plat.normalized);
            }
        }

        void Frapper(ICible cible, Mouvement a, Vector3 direction, double bonus = 1)
        {
            bool critique = a.critiqueSur || Random.value < ChanceCritique;
            double atq = Attaque * a.mult * bonus * (critique ? 1.75 : 1);
            float poussee = a.poussee * (ArmeEnMain != null ? ArmeEnMain.poussee : 1f);
            cible.Recevoir(new Coup { degats = atq, direction = direction, poussee = poussee, lourd = a.lourde, critique = critique, auteur = this, nom = a.nom });
            Figer(a.lourde || a.critiqueSur ? .1f : critique ? .07f : .05f);
            j.Secouer(a.lourde ? .09f : critique ? .06f : .035f);
            bool bois = cible is Quintaine;
            Sons.JouerA(bois ? "impactBois" : "impactChair", cible.Centre, .9f, Random.Range(.9f, 1.1f) * (MainsNues ? 1.2f : 1f));
            Effets.Eclat(cible.Centre, bois ? new Color(.75f, .6f, .4f) : new Color(1f, .9f, .75f), critique ? 14 : 7, 3.5f, .1f, 1f, .35f, direction);
            if (a.critiqueSur) HUD.ChiffreTexte(cible.Centre + Vector3.up * .9f, a.nom + " !", new Color(1f, .85f, .4f));
            stats.Exercer(Stat.FOR, (a.lourde ? 2 : 1) * (MainsNues ? .8 : 1));
            if (MainsNues) stats.Exercer(Stat.AGI, .3);
            if (critique) stats.Exercer(Stat.AGI, .5);
        }

        /// <summary>L'atterrissage d'un coup plongeant : onde de choc qui frappe et repousse tout autour.</summary>
        void Atterrir()
        {
            Vector3 p = transform.position;
            float h = Mathf.Min(hauteurPlongeon, 10f);
            float rayon = 2.6f + h * .3f;
            Effets.OndeDeChoc(p, rayon);
            Sons.Jouer("onde", 1f, 1.05f - h * .03f);
            j.Secouer(.12f + h * .015f);
            Figer(.08f);
            foreach (var c in Physics.OverlapSphere(p + Vector3.up, rayon, ~0, QueryTriggerInteraction.Collide))
            {
                var cible = c.GetComponentInParent<ICible>();
                if (cible == null || ReferenceEquals(cible, this) || !cible.Vivant || touches.Contains(cible)) continue;
                if (cible.Camp == Camp.Heros || cible.Camp == Camp.Allie) continue;
                touches.Add(cible);
                Frapper(cible, Plongeante, Vector3.ProjectOnPlane(cible.Centre - p, Vector3.up).normalized, 1 + h * .12);
            }
            stats.Exercer(Stat.AGI, 1);
            etat = Etat.Recuperation; tEtat = 0f;
        }

        /// <summary>La traînée de lame : un éventail translucide entre la direction de départ et d'arrivée.</summary>
        void Trace(Mouvement a)
        {
            const int N = 14;
            var mesh = new Mesh { name = "Trainee" };
            var v = new Vector3[(N + 1) * 2];
            var col = new Color[v.Length];
            var tri = new int[N * 6];
            for (int k = 0; k <= N; k++)
            {
                float u = k / (float)N;
                Vector3 d = Vector3.Slerp(DirMonde(a.d0).normalized, DirMonde(a.d1).normalized, u);
                Vector3 g = Vector3.Lerp(VersMonde(a.g0), VersMonde(a.g1), u);
                v[k * 2] = g + d * .35f;
                v[k * 2 + 1] = g + d * (Portee * .62f);
                float alpha = Mathf.Sin(u * Mathf.PI) * (a.lourde || a.critiqueSur ? .75f : .5f);
                col[k * 2] = new Color(1, 1, 1, 0);
                col[k * 2 + 1] = new Color(1f, .97f, .9f, alpha);
                if (k < N)
                {
                    int i = k * 6, b = k * 2;
                    tri[i] = b; tri[i + 1] = b + 1; tri[i + 2] = b + 2;
                    tri[i + 3] = b + 1; tri[i + 4] = b + 3; tri[i + 5] = b + 2;
                }
            }
            mesh.vertices = v; mesh.colors = col; mesh.triangles = tri;
            mesh.RecalculateBounds();
            var go = new GameObject("TraineeLame");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = matTrace;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<Fondu>().duree = a.lourde ? .28f : .2f;
        }

        // ---------- garde, parade, roulade, recul ----------

        bool Rouler()
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            if (!stats.Depenser(CoutRoulade)) { HUD.Toast("Trop essoufflé pour rouler"); return false; }
            Vector2 dep = Vector2.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed) dep.y += 1;
                if (kb.sKey.isPressed) dep.y -= 1;
                if (kb.dKey.isPressed) dep.x += 1;
                if (kb.aKey.isPressed) dep.x -= 1;
            }
            if (pad != null) dep += pad.leftStick.ReadValue();
            if (dep.sqrMagnitude < .01f) dep = new Vector2(0, -1);   // sans direction : on recule
            dep.Normalize();
            Vector3 dir = transform.forward * dep.y + transform.right * dep.x;
            j.Elan(dir * 9.5f, DureeRoulade * .9f, 0f);
            etat = Etat.Roulade; tEtat = 0f; courante = null;
            Sons.Jouer("roulade", .6f, Random.Range(.95f, 1.05f));
            stats.Exercer(Stat.AGI, .4);
            return true;
        }

        public bool Invulnerable => (etat == Etat.Roulade && tEtat > .04f && tEtat < .38f) || (etat == Etat.Projete && tEtat < .4f);

        public void Recevoir(Coup c)
        {
            if (!Vivant) return;
            if (etat == Etat.Choc) FinirChoc(false);
            var cb = c.auteur as Combattant;
            Vector3 depuis = cb != null ? cb.Centre : transform.position - c.direction;
            Vector3 recul = Vector3.ProjectOnPlane(c.direction, Vector3.up).normalized;
            if (recul.sqrMagnitude < .01f) recul = -transform.forward;
            if (Invulnerable)
            {
                HUD.ChiffreTexte(SurSoi(), "Esquive", new Color(.7f, .95f, 1f));
                stats.Exercer(Stat.AGI, 1);
                return;
            }
            Vector3 versAttaquant = Vector3.ProjectOnPlane(depuis - transform.position, Vector3.up);
            bool face = Vector3.Angle(transform.forward, versAttaquant) < 80f;
            // La force du coup : sa poussée, d'autant plus forte que l'attaquant dépasse notre défense
            float force = c.poussee * (float)(.5 + c.degats / (c.degats + Defense));

            if (garde && face && Time.time - debutGarde < FenetreParade)
            {
                // Parade parfaite : aucun dégât, l'attaquant est sonné, le temps ralentit, la riposte est prête
                if (cb != null) cb.Etourdir(1.4f);
                if (MainsNues)
                {
                    Sons.Jouer("impactChair", 1f, 1.4f);
                    Effets.Eclat(VersMonde(CroixD), new Color(1f, .95f, .85f), 10, 3f, .08f, .5f, .3f, versAttaquant.normalized);
                }
                else
                {
                    Sons.Jouer("parade", 1f, Random.Range(.95f, 1.05f));
                    Effets.Etincelles(VersMonde(GardeHaute) + DirMonde(LameGarde) * .4f, versAttaquant.normalized + Vector3.up * .3f);
                }
                HUD.EclairCombat(new Color(1f, .97f, .9f, .35f), .18f, false);
                HUD.ChiffreTexte(SurSoi(), "Parade parfaite !", new Color(1f, .85f, .4f));
                Ralentir(.35f);
                finRiposte = Time.time + FenetreRiposte;
                j.Secouer(.05f);
                stats.Exercer(Stat.AGI, 1);
                stats.Exercer(Stat.END, .5);
                if (force >= 2f) CommencerGlissade(recul, force * .6f, true);   // même parée, une masse énorme fait reculer
                return;
            }
            double reel = Degats.Calculer(c.degats, Defense);
            if (garde && face)
            {
                // Bras en croix : encaisse moitié moins bien qu'une lame
                reel *= MainsNues ? .5 : .25;
                if (MainsNues) Sons.Jouer("impactChair", .8f, .9f);
                else
                {
                    Sons.Jouer("parade", .7f, .8f);
                    Effets.Etincelles(VersMonde(GardeHaute) + DirMonde(LameGarde) * .4f, versAttaquant.normalized);
                }
                if (!stats.Depenser(MainsNues ? CoutGarde * 1.4f : CoutGarde))
                {
                    garde = false;
                    Sonner(.7f);
                    HUD.ChiffreTexte(SurSoi(), "Garde brisée", new Color(1f, .55f, .4f));
                }
                else if (force >= 1.4f) CommencerGlissade(recul, force * 1.6f, true);
                stats.Exercer(Stat.END, .6);
            }
            else
            {
                HUD.EclairCombat(new Color(.75f, .05f, .05f, .55f), .45f, true);
                Sons.Jouer("blesse", .7f, Random.Range(.9f, 1.05f));
                stats.Exercer(Stat.END, .3);
                if (force >= 2f) Projeter(recul, force);
                else if (force >= 1f) CommencerGlissade(recul, force * 1.4f, false);
                else
                {
                    j.Elan(recul * force * 6f, .12f, 0f);
                    if (c.lourd) Sonner(.35f);
                }
            }
            stats.Blesser(reel);
            j.Secouer(garde ? .03f : .07f);
            HUD.ChiffreDegats(SurSoi(), reel, c.critique, true);
            if (stats.Vie <= 0 && !mort) StartCoroutine(Mourir());
        }

        /// <summary>L'épée se plante dans le sol et l'on glisse en arrière : étincelles, terre, sillon.</summary>
        void CommencerGlissade(Vector3 direction, float distance, bool enGarde)
        {
            dirGlissade = direction.normalized;
            dureeGlissade = Mathf.Clamp(.3f + distance * .12f, .35f, 1.2f);
            float v0 = 2f * distance / dureeGlissade;
            j.Elan(dirGlissade * v0, dureeGlissade, v0 / dureeGlissade);
            etat = Etat.Glissade; tEtat = 0f; courante = null;
            garde = enGarde;
            prochainSillon = 0f;
            Sons.Jouer(MainsNues ? "frottement" : "raclement", .8f, Mathf.Lerp(1.1f, .8f, distance / 6f));
            j.Secouer(.08f);
        }

        void Glisser()
        {
            if (MainsNues)
            {
                // Sans lame à planter : les pieds labourent le sol
                if (tEtat >= prochainSillon && tEtat < dureeGlissade * .85f)
                {
                    prochainSillon = tEtat + .06f;
                    foreach (float cote in new[] { -.18f, .18f })
                    {
                        Vector3 pied = transform.position + transform.right * cote;
                        Vector3 sol = new Vector3(pied.x, Monde.Hauteur(pied.x, pied.z), pied.z);
                        Effets.Terre(sol, -dirGlissade + Vector3.up);
                        Effets.Sillon(sol, dirGlissade);
                    }
                    Effets.Poussiere(transform.position - dirGlissade * .3f, 3);
                    j.Secouer(.02f);
                }
                return;
            }
            Vector3 pointe = VersMonde(MainPlantee) + DirMonde(LamePlantee).normalized * .95f;
            pointe.y = Mathf.Max(Monde.Hauteur(pointe.x, pointe.z), pointe.y - 1f);
            if (tEtat >= prochainSillon && tEtat < dureeGlissade * .85f)
            {
                prochainSillon = tEtat + .05f;
                Vector3 sol = new Vector3(pointe.x, Monde.Hauteur(pointe.x, pointe.z), pointe.z);
                Effets.Etincelles(sol + Vector3.up * .05f, -dirGlissade + Vector3.up * .6f);
                Effets.Terre(sol, -dirGlissade + Vector3.up);
                Effets.Sillon(sol, dirGlissade);
                j.Secouer(.025f);
            }
        }

        void Projeter(Vector3 direction, float force)
        {
            dirGlissade = direction.normalized;
            j.Projeter(dirGlissade * (4f + force * 1.6f), 4.5f + force * .9f);
            etat = Etat.Projete; tEtat = 0f; courante = null; garde = false; aQuitteLeSol = false;
            Sons.Jouer("onde", .7f, 1.3f);
            HUD.ChiffreTexte(SurSoi(), "Projeté !", new Color(1f, .6f, .45f));
            j.Secouer(.14f);
        }

        /// <summary>Où afficher un chiffre ou un mot sur le héros : au-dessus de lui, ou devant les yeux à la première personne.</summary>
        Vector3 SurSoi()
        {
            if (j.troisiemePersonne) return Centre + Vector3.up * .55f;
            var c = j.cam.transform;
            return c.position + c.forward * 2.2f - c.up * .45f;
        }

        void Sonner(float secondes)
        {
            etat = Etat.Sonne; sonne = secondes; courante = null; garde = false;
        }

        System.Collections.IEnumerator Mourir()
        {
            mort = true;
            LacherVerrou();
            Time.timeScale = 1f; finFige = finRalenti = 0;
            if (MondeRPG.Instance != null) yield return MondeRPG.Instance.StartCoroutine(MondeRPG.Instance.Mort());
            mort = false;
            etat = Etat.Libre;
            j.decalageCamera = Vector3.zero;
        }

        // ---------- choc des lames, coup de grâce, ralenti final ----------

        /// <summary>
        /// Une créature armée frappe pendant que tu frappes, face à face : les lames se bloquent.
        /// Martèle le clic gauche pour l'emporter (plus tu es fort par rapport à elle, plus chaque clic pèse).
        /// </summary>
        public bool TenterChoc(Monstre m)
        {
            if (etat != Etat.Frappe || courante == null || courante == Plongeante || mort || m == null || MainsNues) return false;
            Vector3 vers = Vector3.ProjectOnPlane(m.Centre - Centre, Vector3.up);
            if (vers.magnitude > 4.2f) return false;
            Vector3 avant = Vector3.ProjectOnPlane(j.cam.transform.forward, Vector3.up);
            if (Vector3.Angle(avant, vers) > 55f || Vector3.Angle(m.transform.forward, -vers) > 65f) return false;
            adversaire = m;
            etat = Etat.Choc; tEtat = 0f; courante = null; garde = false;
            equilibre = .5f;
            debutChoc = Time.unscaledTime;
            ratioChoc = Mathf.Clamp((float)(Attaque / System.Math.Max(1.0, m.Attaque * 1.3)), .35f, 3f);
            finFige = finRalenti = 0f;
            Time.timeScale = .3f;
            Sons.Jouer("parade", 1f, .8f);
            Sons.Jouer("raclement", .6f, 1.2f);
            HUD.ChiffreTexte(SurSoi(), "Choc des lames !", new Color(1f, .85f, .4f));
            HUD.EclairCombat(new Color(1f, .97f, .9f, .3f), .25f, false);
            j.Secouer(.1f);
            return true;
        }

        void MettreAJourChoc(bool presse)
        {
            if (adversaire == null || !adversaire.Vivant || adversaire.Etat != EtatMonstre.Choc) { FinirChoc(true); return; }
            float dt = Time.unscaledDeltaTime;
            if (presse)
            {
                equilibre += .09f * ratioChoc;
                Sons.Jouer("clic", .35f, 1.5f + Random.value * .4f);
                j.Secouer(.025f);
            }
            equilibre -= dt * .22f / ratioChoc;
            if (Time.unscaledTime >= prochaineEtincelle)
            {
                prochaineEtincelle = Time.unscaledTime + .07f;
                Effets.Etincelles(VersMonde(main) + DirMonde(lame).normalized * .8f, Vector3.up + Random.insideUnitSphere * .6f);
            }
            HUD.BrasDeFer(equilibre);
            HUD.InviteForte("Choc des lames : martèle le clic gauche !");
            float ecoule = Time.unscaledTime - debutChoc;
            if (equilibre >= 1f) FinirChoc(true);
            else if (equilibre <= 0f) FinirChoc(false);
            else if (ecoule > 2.4f) FinirChoc(equilibre >= .5f);
        }

        void FinirChoc(bool gagne)
        {
            Time.timeScale = 1f;
            HUD.BrasDeFer(-1f);
            var m = adversaire;
            adversaire = null;
            Vector3 dir = m != null ? Vector3.ProjectOnPlane(m.Centre - Centre, Vector3.up).normalized : transform.forward;
            etat = Etat.Libre; tEtat = 0f;
            if (gagne)
            {
                if (m != null) m.FinChoc(true, dir);
                Figer(.08f);
                j.Secouer(.1f);
                Sons.Jouer("parade", 1f, 1.15f);
                HUD.ChiffreTexte(SurSoi(), "Tu l'emportes !", new Color(1f, .85f, .4f));
                finRiposte = Time.time + FenetreRiposte * 1.5f;
                stats.Exercer(Stat.FOR, 3);
            }
            else
            {
                if (m != null) m.FinChoc(false, dir);
                HUD.ChiffreTexte(SurSoi(), "Repoussé !", new Color(1f, .6f, .45f));
                CommencerGlissade(-dir, 2.4f + (1f - Mathf.Min(1f, ratioChoc)) * 2f, true);
                stats.Exercer(Stat.END, 1.5);
            }
        }

        /// <summary>Un ennemi sonné pour de bon, juste devant : le prochain clic l'achève (dégâts ×7).</summary>
        Combattant CibleDeGrace()
        {
            Vector3 avant = Vector3.ProjectOnPlane(j.cam.transform.forward, Vector3.up);
            Combattant meilleur = null;
            float best = 2.9f;
            foreach (var c in Combattant.Tous)
            {
                if (!c.Vulnerable || !Monstre.Ennemis(Camp.Heros, c.Camp)) continue;
                Vector3 v = Vector3.ProjectOnPlane(c.Centre - Centre, Vector3.up);
                float d = v.magnitude;
                if (d < best && Vector3.Angle(avant, v) < 55f) { best = d; meilleur = c; }
            }
            return meilleur;
        }

        /// <summary>Le dernier ennemi du combat tombe : un instant de ralenti.</summary>
        public void RalentiFinal()
        {
            Ralentir(1.1f);
            HUD.EclairCombat(new Color(1f, 1f, 1f, .16f), .6f, false);
            j.Secouer(.05f);
        }

        public void Secousse(float force) { if (j != null) j.Secouer(force); }

        // ---------- verrouillage ----------

        void BasculerVerrou()
        {
            if (verrou != null) { LacherVerrou(); return; }
            ICible meilleur = null;
            float score = float.MaxValue;
            Vector3 oeil = j.cam.transform.position, regard = j.cam.transform.forward;
            foreach (var c in Combattant.Tous)
            {
                if (c == null || !c.Vivant || c.Camp == Camp.Heros || c.Camp == Camp.Allie) continue;
                Vector3 d = c.Centre - oeil;
                float dist = d.magnitude, angle = Vector3.Angle(regard, d);
                if (dist > 18f || angle > 40f) continue;
                float s = angle * 2f + dist;
                if (s < score) { score = s; meilleur = c; }
            }
            if (meilleur == null) { HUD.Toast("Aucune cible devant toi"); return; }
            verrou = meilleur;
            CibleCombat.Signaler(meilleur);
            Sons.Jouer("clic", .5f, 1.4f);
        }

        void LacherVerrou() { verrou = null; if (j != null) j.verrou = null; }

        // ---------- le corps dans l'espace : mains, arme, pied ----------

        static readonly Vector3 Hanche = new Vector3(-.12f, -.75f, .35f);
        static Vector3 Gauche(Vector3 v, Vector3 repos) => v == Vector3.zero ? repos : v;

        void PoserLame()
        {
            float dt = Time.unscaledDeltaTime;
            bool nues = MainsNues;
            Vector3 reposD = nues ? PoingD : GardeRepos, reposG = nues ? PoingG : GaucheBas;
            Vector3 gCible, ggCible, dCible = LameRepos;
            float vitesse = 14f, uPied = 0f;
            bool pied = false;
            switch (etat)
            {
                case Etat.Preparation:
                    float up = Mathf.Clamp01(tEtat / Mathf.Max(.01f, courante.prep));
                    gCible = Vector3.Lerp(main, courante.g0, up); dCible = Vector3.Slerp(lame, courante.d0, up);
                    ggCible = Vector3.Lerp(mainG, Gauche(courante.gg0, reposG), up);
                    vitesse = 30f;
                    if (courante.pied) { pied = true; uPied = up * .45f; }
                    break;
                case Etat.Frappe:
                    float uf = Mathf.Clamp01(tEtat / Mathf.Max(.01f, courante.frappe));
                    uf = 1f - (1f - uf) * (1f - uf);   // rapide au départ, freine à la fin
                    main = Vector3.Lerp(courante.g0, courante.g1, uf); lame = Vector3.Slerp(courante.d0, courante.d1, uf);
                    mainG = Vector3.Lerp(Gauche(courante.gg0, reposG), Gauche(courante.gg1, reposG), uf);
                    if (courante.pied) { pied = true; uPied = .45f + .55f * uf; }
                    Appliquer(pied, uPied);
                    return;
                case Etat.Plongeon:
                    gCible = nues ? new Vector3(.15f, -.55f, .7f) : Plongeante.g1; dCible = Plongeante.d1;
                    ggCible = nues ? new Vector3(-.15f, -.55f, .7f) : reposG; vitesse = 25f;
                    break;
                case Etat.Glissade:
                    if (nues) { gCible = garde ? CroixD : reposD; ggCible = garde ? CroixG : reposG; }
                    else { gCible = MainPlantee; dCible = LamePlantee; ggCible = reposG; }
                    vitesse = 40f;
                    break;
                case Etat.Choc:
                    gCible = GardeHaute + new Vector3(Mathf.Sin(Time.unscaledTime * 47f), Mathf.Sin(Time.unscaledTime * 39f), 0) * .012f;
                    dCible = new Vector3(-.55f, .6f, .6f); ggCible = reposG; vitesse = 30f;
                    break;
                case Etat.Projete:
                    gCible = reposD + new Vector3(.15f, .3f, -.2f); dCible = new Vector3(.4f, .5f, -.6f);
                    ggCible = reposG + new Vector3(-.15f, .3f, -.2f); vitesse = 12f;
                    break;
                case Etat.Recuperation:
                    gCible = reposD; ggCible = reposG; vitesse = 8f;
                    if (courante != null && courante.pied) { pied = true; uPied = 1f - Mathf.Clamp01(tEtat / (courante.recup * .6f)); }
                    break;
                case Etat.Roulade:
                    gCible = reposD + new Vector3(0, -.2f, -.15f); ggCible = reposG + new Vector3(0, -.2f, -.15f);
                    break;
                default:
                    if (nues) { gCible = garde ? CroixD : reposD; ggCible = garde ? CroixG : reposG; }
                    else { gCible = garde ? GardeHaute : GardeRepos; dCible = garde ? LameGarde : LameRepos; ggCible = reposG; }
                    vitesse = garde ? 22f : 10f;
                    break;
            }
            if (tRangement >= 0f) { gCible = Hanche; vitesse = 26f; }   // ranger ou dégainer : la main passe par la hanche
            float k = Mathf.Clamp01(dt * vitesse);
            main = Vector3.Lerp(main, gCible, k);
            mainG = Vector3.Lerp(mainG, ggCible, k);
            lame = Vector3.Slerp(lame, dCible, k);
            Appliquer(pied, uPied);
        }

        void Appliquer(bool pied, float uPied)
        {
            // L'arme : dans la main (dégainée), à la hanche (rangée), nulle part (aucune)
            var def = inv != null ? inv.Arme : null;
            string id = def?.id;
            if (id != armeConstruite)
            {
                if (modeleArme != null) Destroy(modeleArme.gameObject);
                modeleArme = null;
                armeConstruite = id;
                if (def != null)
                {
                    modeleArme = new GameObject("Arme_" + id).transform;
                    modeleArme.SetParent(transform, false);
                    Objets.ConstruireArme(def.forme, modeleArme);
                }
            }
            if (modeleArme != null)
            {
                if (inv.Degainee)
                {
                    Vector3 poignee = VersMonde(main), dir = DirMonde(lame).normalized;
                    Vector3 haut = Vector3.Cross(dir, j.troisiemePersonne ? transform.right : j.cam.transform.right);
                    if (haut.sqrMagnitude < .01f) haut = Vector3.up;
                    modeleArme.SetPositionAndRotation(poignee, Quaternion.LookRotation(dir, haut));
                }
                else
                {
                    Vector3 hanche = transform.TransformPoint(new Vector3(-.22f, .95f, .1f));
                    Vector3 dir = (-transform.up * .85f - transform.forward * .45f).normalized;
                    modeleArme.SetPositionAndRotation(hanche, Quaternion.LookRotation(dir, transform.forward));
                }
            }

            // Les mains : bras en cubes à la première personne, mains du modèle riggé par IK
            j.cibleMainDroite = VersMonde(main);
            j.poidsMainDroite = 1f;
            j.cibleMainGauche = VersMonde(mainG);
            j.poidsMainGauche = MainsNues ? 1f : 0f;
            PoserBras(j.BrasDroit, main, new Vector3(.5f, -.75f, .15f));
            PoserBras(j.BrasGauche, mainG, new Vector3(-.5f, -.75f, .15f));

            // Le pied : une botte devant la caméra (première personne), la jambe du corps sinon
            bool premiere = !j.troisiemePersonne;
            if (botte.gameObject.activeSelf != (pied && premiere)) botte.gameObject.SetActive(pied && premiere);
            if (pied)
            {
                Vector3 a = new Vector3(.14f, -1.3f, .3f), b = new Vector3(.12f, -.95f, .5f), c = new Vector3(.08f, -.62f, 1.15f);
                Vector3 p = uPied < .45f ? Vector3.Lerp(a, b, uPied / .45f) : Vector3.Lerp(b, c, (uPied - .45f) / .55f);
                Vector3 avant = Vector3.ProjectOnPlane(j.cam.transform.forward, Vector3.up);
                if (premiere && avant.sqrMagnitude > .001f) botte.SetPositionAndRotation(j.cam.transform.TransformPoint(p), Quaternion.LookRotation(avant.normalized));
                j.ciblePiedDroit = transform.TransformPoint(new Vector3(.12f, Mathf.Lerp(.1f, 1f, uPied), Mathf.Lerp(.1f, .95f, uPied)));
                j.poidsPiedDroit = 1f;
                j.leverJambeDroite = uPied;
            }
            else { j.poidsPiedDroit = 0f; j.leverJambeDroite = 0f; }
        }

        static void PoserBras(Transform bras, Vector3 main, Vector3 coude)
        {
            if (bras == null || !bras.gameObject.activeSelf) return;
            bras.localPosition = main;
            Vector3 avantBras = main - coude;
            if (avantBras.sqrMagnitude > .001f) bras.localRotation = Quaternion.LookRotation(avantBras.normalized, Vector3.up);
        }

        /// <summary>Une pose du repère « caméra » vers le monde : devant les yeux à la première personne, devant le corps à la troisième.</summary>
        Vector3 VersMonde(Vector3 p)
        {
            if (!j.troisiemePersonne) return j.cam.transform.TransformPoint(p);
            return transform.TransformPoint(new Vector3(p.x * 1.1f, 1.32f + p.y * 1.1f, .18f + p.z * .75f));
        }

        Vector3 DirMonde(Vector3 d) => j.troisiemePersonne ? transform.TransformDirection(d) : j.cam.transform.TransformDirection(d);

        /// <summary>La botte et le bas de la jambe qu'on voit passer devant soi au coup de pied.</summary>
        Transform ConstruireBotte()
        {
            var b = new GameObject("Botte").transform;
            b.SetParent(transform, false);
            var cuir = Pantin.Mat("#3a2a1e", .2f);
            var toile = Pantin.Mat("#6b5240", .05f);
            Pantin.Bloc(b, PrimitiveType.Cube, Vector3.zero, new Vector3(.17f, .13f, .32f), cuir);
            Pantin.Bloc(b, PrimitiveType.Cube, new Vector3(0, .12f, -.08f), new Vector3(.15f, .22f, .16f), cuir);
            Pantin.Bloc(b, PrimitiveType.Cube, new Vector3(0, .42f, -.1f), new Vector3(.15f, .42f, .15f), toile);
            foreach (var r in b.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            b.gameObject.SetActive(false);
            return b;
        }
    }

    /// <summary>Fait disparaître un objet en fondu (traînées, sillons) puis le détruit ; garde sa couleur d'origine.</summary>
    public class Fondu : MonoBehaviour
    {
        public float duree = .2f;
        float t;
        MeshRenderer r;
        MaterialPropertyBlock bloc;
        Color couleur = Color.white;
        static readonly int Id = Shader.PropertyToID("_BaseColor");

        void Start()
        {
            r = GetComponent<MeshRenderer>();
            bloc = new MaterialPropertyBlock();
            if (r != null && r.sharedMaterial != null && r.sharedMaterial.HasProperty(Id)) couleur = r.sharedMaterial.GetColor(Id);
        }

        void Update()
        {
            t += Time.unscaledDeltaTime;
            if (r != null)
            {
                bloc.SetColor(Id, new Color(couleur.r, couleur.g, couleur.b, couleur.a * Mathf.Clamp01(1f - t / duree)));
                r.SetPropertyBlock(bloc);
            }
            if (t >= duree)
            {
                var mf = GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null && mf.sharedMesh.name == "Trainee") Destroy(mf.sharedMesh);
                Destroy(gameObject);
            }
        }
    }
}
