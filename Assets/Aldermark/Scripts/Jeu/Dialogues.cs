using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Aldermark
{
    public class Choix
    {
        public string texte;
        public System.Func<Noeud> suite;
        public System.Action effet;
        public System.Func<bool> si;
    }

    public class Noeud
    {
        public string qui;
        public string texte;
        public readonly List<Choix> choix = new List<Choix>();

        public Noeud(string qui, string texte) { this.qui = qui; this.texte = texte; }

        public Noeud Choix(string texte, System.Func<Noeud> suite = null, System.Action effet = null, System.Func<bool> si = null)
        {
            choix.Add(new Choix { texte = texte, suite = suite, effet = effet, si = si });
            return this;
        }
    }

    /// <summary>
    /// Dialogues à choix numérotés (touches 1 à 4, ou A/B/X/Y à la manette).
    /// Le contenu de l'arc 1 est écrit dans ce fichier, d'après la partie jouée.
    /// </summary>
    public class Dialogues : MonoBehaviour
    {
        public static bool EnCours { get; private set; }
        public static Interlocuteur Courant { get; private set; }
        static Dialogues instance;
        Noeud courant;
        List<Choix> visibles;
        float delai;

        void Awake() { instance = this; EnCours = false; }

        public static void Commencer(Interlocuteur qui)
        {
            if (instance == null || qui == null) return;
            Courant = qui;
            Sons.Jouer("clic", .6f);
            Noeud n = qui.id switch
            {
                "lise" => Lise(),
                "odon" => Odon(),
                "bram" => Bram(),
                "lit" => Lit(),
                "ysolde" => Ysolde(),
                "ilse" => Ilse(),
                "forgeron" => new Noeud("Hadrien", !A("four_repare")
                    ? "Sans charbon de bois ni fer qui arrive, ma forge dort. Relance le four, l'étranger, et on verra pour le reste."
                    : !A("pont_ouvert")
                        ? "Le four refume, alors j'ai rallumé la forge : des clous et des crampons pour ton pont. Tiens bon."
                        : "Les charrettes passent, le fer arrive. Repasse me voir, j'aurai peut-être quelque chose pour toi."),
                "tobin" => Tobin(),
                "prisonnier" => new Noeud(qui.nom, "(Il crache par terre.) Je dirai rien. Corvin vous fera payer ça, tous autant que vous êtes."),
                _ => new Noeud(qui.nom, qui.GetComponent<Ouvrier>() is Ouvrier o ? o.Replique() : "…")
            };
            instance.Afficher(n);
        }

        void Afficher(Noeud n)
        {
            if (n == null) { Fermer(); return; }
            courant = n;
            visibles = n.choix.Where(c => c.si == null || c.si()).ToList();
            if (visibles.Count == 0) visibles.Add(new Choix { texte = "(Continuer)" });
            EnCours = true;
            delai = .25f;
            HUD.AfficherDialogue(n.qui, n.texte, visibles.Select(c => c.texte).ToList(), HauteurVoix(n.qui));
        }

        void Fermer()
        {
            EnCours = false;
            courant = null;
            if (Courant != null) { var h = Courant.GetComponent<Habitant>(); if (h != null) h.parle = false; }
            Courant = null;
            HUD.FermerDialogue();
        }

        void Update()
        {
            if (!EnCours) return;
            if (Courant != null) { var h = Courant.GetComponent<Habitant>(); if (h != null) h.parle = !HUD.TexteFini; }
            if (delai > 0) { delai -= Time.deltaTime; return; }
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            int i = -1;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) i = 0;
                else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) i = 1;
                else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) i = 2;
                else if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) i = 3;
                else if (kb.escapeKey.wasPressedThisFrame) { Fermer(); return; }
            }
            if (i < 0 && pad != null)
            {
                if (pad.buttonSouth.wasPressedThisFrame) i = 0;
                else if (pad.buttonEast.wasPressedThisFrame) i = 1;
                else if (pad.buttonWest.wasPressedThisFrame) i = 2;
                else if (pad.buttonNorth.wasPressedThisFrame) i = 3;
            }
            if (i < 0) return;
            if (!HUD.TexteFini) { HUD.FinirTexte(); return; }   // première pression : affiche tout le texte
            if (i >= visibles.Count) return;
            Sons.Jouer("clic", .5f, 1.2f);
            var c = visibles[i];
            c.effet?.Invoke();
            Afficher(c.suite?.Invoke());
        }

        // ------------------------------------------------------------------
        // Contenu de l'arc 1
        // ------------------------------------------------------------------

        static bool A(string d) => Progression.A(d);

        static float HauteurVoix(string qui) => qui switch { "Lise" => 1.75f, "Odon" => .72f, "Bram" => .95f, "Ysolde" => 1.35f, "Ilse" => 1.2f, "Vesk" => .55f, "Hadrien" => .8f, "Garr" => .65f, _ => 1.1f };

        /// <summary>Envoie un personnage marcher jusqu'à un point (Odon et Bram vont au four).</summary>
        static void Envoyer(string id, Vector2 pos, float rapprocher = 0f)
        {
            foreach (var i in Interlocuteur.Tous)
            {
                if (i.id != id) continue;
                var h = i.GetComponent<Habitant>();
                if (h == null) continue;
                // Bram « arrive en courant » : il apparaît déjà sur le chemin, à quelques mètres
                Vector3 p = i.transform.position;
                Vector2 d = new Vector2(p.x, p.z) - pos;
                if (rapprocher > 0 && d.magnitude > rapprocher)
                {
                    Vector2 q = pos + d.normalized * rapprocher;
                    i.transform.position = new Vector3(q.x, Monde.Hauteur(q.x, q.y), q.y);
                }
                h.AllerVers(pos);
            }
        }

        static Vector2 PresDuFour(float avance, float cote)
        {
            Vector2 dir = (Monde.Village - Monde.Four).normalized, perp = new Vector2(-dir.y, dir.x);
            return Monde.Four + dir * avance + perp * cote;
        }

        static Noeud Lise()
        {
            if (!A("lise"))
                return new Noeud("Lise", "Monsieur… pourquoi votre bras il brille ?")
                    .Choix("(Cacher ton poignet) Ce n'est rien. Qu'est-il arrivé au pont ?", LisePont)
                    .Choix("C'est une longue histoire. Tu peux m'emmener voir le chef du village ?", () =>
                        new Noeud("Lise", "Le chef, c'est un peu le vieux Odon. Il est près du puits. Mais il aime pas trop les étrangers…")
                            .Choix("Merci, Lise.", null, () => Progression.Mettre("lise")));
            if (A("pont_accepte")) return new Noeud("Lise", "C'est vrai que vous allez réparer le pont ? Je pourrai vous apporter des champignons !");
            if (A("test_four")) return new Noeud("Lise", "Odon dit que vous êtes peut-être pas un menteur. C'est bien, ça !");
            return new Noeud("Lise", "Le vieux Odon est près du puits, au milieu du village. Il a mal à la main…");
        }

        static Noeud LisePont() =>
            new Noeud("Lise", "Il s'est pas cassé tout seul. Papa dit qu'il y a eu un grand bruit, la nuit. Comme le tonnerre, mais sans orage. Maintenant, il y a que la barque de Maître Corvin. Une pièce d'argent par sac. Personne a d'argent.")
                .Choix("Et personne ne le répare ?", () =>
                    new Noeud("Lise", "Le vieux Odon a commencé. Après, des hommes en noir sont venus la nuit. Maintenant il peut plus se servir de sa main. Les charrettes du Comte attendent de l'autre côté, et nous on mange des champignons. (Elle soulève son panier.) Odon est près du puits.")
                        .Choix("Merci, Lise. Je vais le voir.", null, () => Progression.Mettre("lise")));

        static Noeud Odon()
        {
            if (!A("lise")) return new Noeud("Odon", "… (Le vieil homme te dévisage sans un mot. Sa main droite est enveloppée dans un bandage sale.)");

            if (!A("test_four"))
                return new Noeud("Odon", "Un monsieur des bois. (Il regarde tes bottes neuves, tes mains sans cal.) Tu viens de la part de Corvin ? Ou du Comte ?")
                    .Choix("Je viens d'un pays lointain. J'y ai étudié la construction.", OdonPresentation)
                    .Choix("Ni l'un ni l'autre. Je voudrais aider ce village.", () =>
                        new Noeud("Odon", "Aider. Le dernier qui voulait « aider », c'était un percepteur du Comte. Il est reparti avec nos poules.")
                            .Choix("Alors mettez-moi à l'épreuve.", OdonPresentation));

            if (!A("four_ok") || !A("sabotage_connu"))
            {
                var n = new Noeud("Odon", A("four_ok") || A("sabotage_connu")
                    ? "Bien. Et quoi d'autre ? Un maçon regarde tout."
                    : "Alors, l'étranger ? Qu'est-ce qu'il lui faut, à mon four ?");
                n.Choix("La voûte du foyer est fendue, la cheminée bouchée, la grille tordue. Le calcaire est déjà sur le talus. Ensuite, deux ou trois jours de feu.", () =>
                        new Noeud("Odon", "(Un coin de sa bouche se relève, presque un sourire.) Pas mal, pour des mains de scribe.").Choix("(Continuer)", Odon),
                    () => { Progression.Mettre("four_ok"); Progression.AjouterConfiance(1); },
                    () => A("vu_four") && A("vu_calcaire") && !A("four_ok"));
                n.Choix("Il y a autre chose. Regardez le foyer : du soufre, et des traces de bottes.", () =>
                        new Noeud("Odon", "(Il se penche, renifle les cendres, et son visage se ferme d'un coup.) Du soufre… Le tonnerre sans orage. Ils l'ont fait ICI. Dans MON four. Ce qui a fait sauter le pont de mon père, ils l'ont préparé dans mon four.")
                            .Choix("(Un bruit de pas derrière vous…)", Odon),
                    () =>
                    {
                        Progression.Mettre("sabotage_connu");
                        Progression.AjouterConfiance(1);
                        Envoyer("bram", PresDuFour(7f, -2.5f), 12f);
                    },
                    () => A("vu_cendres") && !A("sabotage_connu"));
                n.Choix("Je n'ai pas encore assez regardé.", () =>
                    new Noeud("Odon", "Bonne réponse. Un maçon qui parle avant de regarder, c'est un menteur. Va voir."));
                return n;
            }

            if (!A("bram")) return new Noeud("Odon", "(Il désigne Bram du menton, livide, derrière toi.) Celui-là a des choses à te dire.");

            if (!A("pont_accepte"))
                return new Noeud("Odon", "L'étranger. Ce pont, c'est mon père qui l'a monté. Tu sais le rebâtir ? Vraiment ?")
                    .Choix("Oui. Mais il me faut des bras, du bois et de la chaux.", () =>
                            new Noeud("Odon", "Alors on commence à l'aube. Joss, Pell et Marthe sont à toi. Et moi, j'ai encore une main qui sait poser une pierre. Montre-moi d'abord ce que tu vois sur ces ruines. Ensuite le four : sans chaux, pas de piles."),
                        () => { Progression.Mettre("pont_accepte"); Progression.AjouterConfiance(1); },
                        () => A("vu_corvin"))
                    .Choix("Pas encore. Je veux d'abord suivre la piste de ceux qui l'ont fait sauter.", () =>
                        new Noeud("Odon", "Sage. Mais ne traîne pas : le village mange des champignons depuis trois lunes."));

            if (!A("four_repare")) return new Noeud("Odon", Chantier.Conseil() + " (Tab : carte du chantier)");
            if (!A("pont_ouvert")) return new Noeud("Odon", Chantier.Conseil() + " (Tab : carte du chantier)");
            return new Noeud("Odon", "Il tient. Il tient vraiment. (Il pose sa main valide sur la rambarde.) Merci, Bâtisseur.");
        }

        // ---------- interrogatoire de Tobin ----------

        static Noeud Tobin()
        {
            if (A("tobin_rallie")) return new Noeud("Tobin", "Je porte, je porte. C'est plus honnête que ce que je faisais avant.");
            if (A("tobin_interroge")) return new Noeud("Tobin", "J'ai tout dit. Qu'est-ce que vous allez faire de moi ?")
                .Choix("Tu peux te racheter : viens travailler au pont.", TobinRallie, () => Rallier(), () => !A("tobin_maltraite"))
                .Choix("Le Comte décidera de ton sort.");
            return new Noeud("Tobin", "(Ligoté, il évite ton regard. Bram se tient à l'écart, les poings serrés.) J'ai rien fait de mal… On m'a juste payé pour porter des sacs.")
                .Choix("Le secouer pour le faire parler.", () =>
                    new Noeud("Odon", "(Odon t'attrape le bras, sa main bandée tremble.) Non. Pas comme ça. Si on fait comme eux, on vaut pas mieux qu'eux.")
                        .Choix("Insister : « Il parlera, de gré ou de force. »", () =>
                                new Noeud("Tobin", "(Terrifié.) C'est Corvin ! Corvin qui paie ! Les hommes en noir apportent la poudre, ils viennent de plus haut, je sais pas qui ! Arrêtez !"),
                            () => { Progression.Mettre("tobin_interroge"); Progression.Mettre("tobin_maltraite"); Progression.AjouterConfiance(-2); })
                        .Choix("Lâcher prise : « Tu as raison, Odon. »", TobinCalme))
                .Choix("L'interroger calmement.", TobinCalme);
        }

        static Noeud TobinCalme() =>
            new Noeud("Tobin", "(Il souffle.) Corvin nous payait. Une pièce d'argent par nuit, pour casser et pour voler. Les hommes en noir apportaient la poudre, le tonnerre sans orage. Ils disaient que le pont devait rester tombé.")
                .Choix("Pourquoi le pont doit-il rester tombé ?", () =>
                        new Noeud("Tobin", "Tant qu'il est cassé, tout passe par la barque de Corvin, au prix qu'il veut. Et les charrettes du Comte restent de l'autre côté. Quelqu'un veut que Varenne crève, je crois.")
                            .Choix("Tu peux te racheter : viens travailler au pont.", TobinRallie, () => Rallier())
                            .Choix("Le Comte décidera de ton sort."),
                    () => { Progression.Mettre("tobin_interroge"); Progression.AjouterConfiance(1); });

        static Noeud TobinRallie() =>
            new Noeud("Tobin", "(Il regarde Bram, qui finit par hocher la tête.) … D'accord. Je porterai ce que tu veux. Et pardon, pour le pont.");

        static void Rallier()
        {
            Progression.Mettre("tobin_rallie");
            Progression.AjouterConfiance(1);
            foreach (var i in Interlocuteur.Tous)
                if (i.id == "tobin")
                {
                    var corde = i.transform.Find("Corde");
                    if (corde != null) Object.Destroy(corde.gameObject);
                }
            HUD.Message("TOBIN REJOINT LE CHANTIER", "Il est libre, et il porte. Tu peux l'affecter sur la carte du chantier (Tab).", 5f);
        }

        // ---------- Ilse à l'auberge ----------

        static Noeud Ilse()
        {
            if (A("ilse_rencontre"))
            {
                if (A("noeud_touche") && A("ilse_a_vu") && !A("ilse_colonne"))
                {
                    Progression.Mettre("ilse_colonne");
                    return new Noeud("Ilse", "(Elle ne lève pas les yeux de son verre.) Cette nuit, une colonne d'argent a percé le ciel au-dessus de la rivière. Une seconde à peine. (Un temps.) Votre étranger devrait apprendre à respirer plus doucement.");
                }
                return new Noeud("Ilse", "Si vous croisez mon étranger, dites-lui ceci : il ne pourra pas affronter Vesk et Morwen en même temps. Mais avec l'aide de l'une, il pourrait survivre à l'autre.");
            }
            bool trouve = Pouvoirs.Presence > .55f && !A("pendentif_vide");
            if (trouve)
                return new Noeud("Ilse", "(Le pendentif à son cou s'agite, pivote vers toi et se fige. Elle sourit sans joie.) Tiens. Vous voilà, porteur. Ne faites pas de geste brusque : je suis du genre rapide.")
                    .Choix("Porteur ? Je suis un simple voyageur.", () =>
                        new Noeud("Ilse", "(Elle tapote le cristal, qui pointe toujours sur ta poitrine.) Les voyageurs ne brillent pas comme un feu dans la nuit. Asseyez-vous. Je ne suis pas là pour vous tuer. Pas encore.").Choix("(S'asseoir)", IlseParle))
                    .Choix("(S'asseoir en face d'elle.) Parlons.", IlseParle);
            return new Noeud("Ilse", "(Une femme en cape grise, seule à une table. Le pendentif à son cou hésite : il tourne vers toi, repart, s'immobilise sans savoir où pointer. Elle te dévisage longtemps.) Vous n'êtes pas d'ici. Je m'appelle Ilse. Je cherche quelqu'un. Asseyez-vous, je vous offre un verre.")
                .Choix("(S'asseoir et jouer le voyageur.) Merci. La route a été longue.", () =>
                    new Noeud("Ilse", "Elles le sont toutes, par ici. (Elle sert deux verres.) Vous avez vu passer un homme marqué ? Une marque d'argent, au bras.").Choix("Non. Mais qui cherchez-vous, au juste ?", IlseParle))
                .Choix("(S'asseoir et la faire parler.) Vous cherchez qui ?", IlseParle)
                .Choix("(Refuser poliment et ressortir.)", () => new Noeud("Ilse", "(Elle hausse les épaules.) Comme vous voudrez. Je reste quelques jours. On se reverra, l'étranger."));
        }

        static Noeud IlseParle() =>
            new Noeud("Ilse", Pouvoirs.Presence > .55f
                    ? "Vous savez très bien qui je cherche. (Elle boit une gorgée.) Je sers Dame Morwen, de la Tour d'obsidienne. On m'appelle une Lame grise."
                    : "Un homme apparu il y a quelques jours, quelque part dans cette vallée. Un étranger qui porte une marque que des gens très puissants voudraient récupérer. J'avais sa trace, claire comme un feu dans la nuit. Puis plus rien. Comme s'il avait soufflé sa propre bougie. (Elle sourit sans joie.) Soit il est mort, soit il est malin. Je penche pour malin.")
                .Choix("Ces gens puissants, ce sont ceux de la Tour ?", () =>
                    new Noeud("Ilse", "Oui. Je sers Dame Morwen. Une Lame grise. (Elle te fixe.) Ça ne vous fait pas peur ? En général, les gens d'ici changent de table.")
                        .Choix("Et les hommes en noir de Maître Vesk ? Vous servez la même cause ?", IlseVesk))
                .Choix("Et les hommes en noir de Maître Vesk ?", IlseVesk);

        static Noeud IlseVesk() =>
            new Noeud("Ilse", "(Pour la première fois, du mépris passe sur son visage.) Vesk est un chien qu'on a lâché sur les ponts. Il obéit au Seigneur des Cendres, pas à la Tour. Ma maîtresse veut le porteur vivant. Vesk, lui, le veut mort. Il arrive bientôt, et il ne fera pas la différence entre le porteur et le village qui le cache.")
                .Choix("D'où Vesk tire-t-il sa force ?", () =>
                        new Noeud("Ilse", "D'un nœud de mana, sous votre rivière, là où plongent les piles du vieux pont. C'est pour ça qu'il veut cet endroit. Sans sa source, ce n'est plus qu'un homme grand et méchant avec une épée. (Elle se lève et pose une pièce d'argent sur la table.) Bonne nuit, l'étranger. Vous n'avez même pas touché votre vin."),
                    () =>
                    {
                        Progression.Mettre("ilse_rencontre");
                        foreach (var it in Interlocuteur.Tous) if (it.id == "ilse") it.nom = "Ilse";
                        Progression.Mettre("vesk_annonce");
                        Progression.Mettre("info_noeud");
                        HUD.Message("INFORMATION CLÉ",
                            "La Tour veut le porteur vivant. Vesk le veut mort, et il arrive bientôt.\nVesk puise dans le nœud de mana sous l'ancien pont.\nLe Sceau peut voir les lignes de mana. Peut-être aussi… les assécher.", 9f);
                    });

        // ---------- l'Entre-Deux : Ysolde ----------

        static Noeud Ysolde()
        {
            if (A("pacte_signe")) return new Noeud("Ysolde", "Allez, Aldric. Le monde vous attend. Et pardon, encore, pour le camion.");
            return new Noeud("Ysolde", "(Une femme aux cheveux d'argent tamponne des parchemins sans lever les yeux.) Numéro 4 471… asseyez-vous. (Elle lit ta fiche et pâlit.) Ah. Ah non. Vous n'étiez pas prévu avant 61 ans. Encore une erreur d'aiguillage… Ce camion devait passer par l'autre rue.")
                .Choix("Où suis-je ? Qui êtes-vous ?", YsoldeQui)
                .Choix("Une erreur d'aiguillage ? Je suis mort à cause d'une erreur de planning ?", () =>
                    new Noeud("Ysolde", "(Elle grimace.) Si ça peut vous consoler, je déteste les erreurs de planning autant que vous.").Choix("(Continuer)", YsoldeQui));
        }

        static Noeud YsoldeQui() =>
            new Noeud("Ysolde", "Je suis Ysolde, déesse des Routes et des Passages. Je ne peux pas vous renvoyer : votre corps est… n'en parlons pas. Le règlement prévoit une compensation : une nouvelle vie à Aldermark, un monde qui manque cruellement de gens organisés. Et un don, au choix. (Trois orbes s'allument au-dessus du bureau.)")
                .Choix("L'Œil des Flux : voir les marchandises, les gens et les armées en lignes de lumière.", () => OrbeOrdinaire("L'Œil des Flux"))
                .Choix("L'Inventaire du Monde : un espace de stockage qui grandit si on le range bien.", () => OrbeOrdinaire("L'Inventaire du Monde"))
                .Choix("Le Plan du Bâtisseur : ce qu'on dessine se bâtit deux fois plus vite.", () => OrbeOrdinaire("Le Plan du Bâtisseur"))
                .Choix("Pardonnez-moi, mais n'auriez-vous pas plus d'informations ? Et un orbe bien plus puissant ?", YsoldeMonde);

        static Noeud OrbeOrdinaire(string nom) =>
            new Noeud("Ysolde", $"{nom}… (Sa main s'arrête au-dessus de l'orbe.) Vous hésitez. Un logisticien qui se contente d'un seul outil, ça m'étonnerait. Posez-moi plutôt la vraie question.")
                .Choix("N'auriez-vous pas plus d'informations ? Et un orbe plus puissant ?", YsoldeMonde);

        static Noeud YsoldeMonde() =>
            new Noeud("Ysolde", "(Elle claque des doigts : une carte se déploie dans le vide.) Aldermark. Il y a vingt ans, la Grande Rupture a détruit les ponts, les cols et les routes marchandes. Les villes du nord ont du fer mais pas de blé ; le sud a du blé mais pas d'outils. Les gens meurent de faim à côté de greniers pleins, parce que rien ne circule. Et à l'est, le Seigneur des Cendres avance, lui, parfaitement ravitaillé.")
                .Choix("Et personne n'a rien fait ?", () =>
                    new Noeud("Ysolde", "L'Empire a invoqué cinq Héros de votre monde. Des épées, du feu, de la force brute. Aucun n'a pensé à demander comment nourrir une armée. (Elle jette un coup d'œil derrière elle, vers le vide.)")
                        .Choix("Et cet orbe plus puissant ?", YsoldeSceau))
                .Choix("Et cet orbe plus puissant ?", YsoldeSceau);

        static Noeud YsoldeSceau() =>
            new Noeud("Ysolde", "(Elle ouvre un tiroir : un orbe noir et or y pulse comme un cœur.) Le Sceau du Grand Registre. Il voit, modifie et réorganise tout ce qui appartient à un réseau : routes, stocks, contrats, chaînes de commandement. Il appartenait au dieu qui a provoqué la Grande Rupture. Il s'éveille lentement, et son porteur est visible : ceux qui le cherchent sentiront votre présence. Vous ne vivrez plus jamais tranquille.")
                .Choix("Je prends le Sceau. Et je veux un orbe qui me permette de créer mes propres pouvoirs.", YsoldeGenese)
                .Choix("Visible ? Qui me chercherait ?", () =>
                    new Noeud("Ysolde", "La Tour d'obsidienne. Les Cendres. Peut-être d'autres. (Elle hausse les épaules.) Mais un homme qui voit les flux sait aussi les cacher, avec le temps.")
                        .Choix("Je prends le Sceau. Et je veux un orbe qui me permette de créer mes propres pouvoirs.", YsoldeGenese));

        static Noeud YsoldeGenese() =>
            new Noeud("Ysolde", "(Elle lâche son tampon, qui roule et tombe dans le vide.) Vous ne voulez pas créer de la matière. Vous voulez créer des pouvoirs. C'est comme ça que nous, les dieux, fabriquons les orbes. (Elle sort une boîte de fer scellée de sept chaînes et n'en défait qu'une.) L'Orbe de Genèse. Ses règles ne sont pas de moi : comprendre avant de créer. Une compétence au niveau 1, une de plus tous les dix niveaux. Rien sur la mort, le temps ou les dieux. Et chaque création laisse une trace dans le Registre divin.")
                .Choix("Le Sceau me montre comment marche le monde, la Genèse en fait des pouvoirs. J'accepte.", YsoldePacte);

        static Noeud YsoldePacte() =>
            new Noeud("Ysolde", "Alors voici le pacte. Vous serez traqué. Vous serez lié à moi : si vous mourez, je m'affaiblis ; si l'on me punit, vous le sentirez. Vous vous engagez à retrouver le dieu qui a provoqué la Grande Rupture, à décider ensuite s'il faut réparer le réseau ou le réécrire, et à me protéger si je tombe. (Elle fait glisser un parchemin vers toi.) Sous quel nom signez-vous ?")
                .Choix("Signer : « Aldric ». Et je vous promets de rendre ce monde meilleur.", () =>
                        new Noeud("Ysolde", "(Elle sourit, pour la première fois.) Aldric. Prenez aussi ceci : la Bénédiction du Passant, l'endurance d'un soldat aguerri. Vous en aurez besoin. Et… je suis désolée pour le camion. Vraiment."),
                    () =>
                    {
                        Progression.Mettre("pacte_signe");
                        Sons.Jouer("decouverte", .9f, .9f);
                        HUD.Message("PACTE SIGNÉ",
                            "ALDRIC\nSceau du Grand Registre · éveil 1 %\nOrbe de Genèse · 1 emplacement\nBénédiction du Passant\nMana : 35 / 35 · Classe : non classable\nPrésence détectable : moyenne", 9f);
                    });

        static Noeud Lit()
        {
            if (!Chantier.PeutDormir)
                return new Noeud("Paillasse", $"Une paillasse de foin, chez Odon. Il est {(int)Chantier.Heure} h : trop tôt pour dormir.");
            return new Noeud("Paillasse", "Une paillasse de foin qui sent bon l'été. Odon ronfle déjà de l'autre côté du mur.")
                .Choix("Dormir jusqu'à l'aube", () => null, () => Chantier.Dormir())
                .Choix("Pas encore");
        }

        static Noeud OdonPresentation() =>
            new Noeud("Odon", "J'ai été maître maçon pendant quarante ans. J'ai bâti la moitié des murs de cette vallée, et ce pont, c'est mon père qui l'a monté. Les hommes en noir m'ont brisé la main quand j'ai voulu le réparer.")
                .Choix("Je suis désolé.", OdonQuestion)
                .Choix("Alors vous savez ce qu'il faut pour le rebâtir.", OdonQuestion);

        static Noeud OdonQuestion()
        {
            Progression.Mettre("test_four");
            Envoyer("odon", PresDuFour(6.5f, 1.5f));
            return new Noeud("Odon", "Des mains de scribe… Mais tu as regardé mon four en arrivant. Personne ne regarde ce four. Il est mort depuis six ans. Viens. (Il prend son marteau de sa main valide.) On va voir si tu sais ce qu'il lui faut.");
        }

        static Noeud Bram()
        {
            if (A("bram")) return new Noeud("Bram", A("vu_corvin") ? "Corvin… Je savais qu'il trempait dans un sale coup. Dis-moi où porter, je porte." : "Suis la piste, l'étranger. Moi, je garde un œil sur Tobin.");
            if (!A("sabotage_connu")) return new Noeud("Bram", "Aider. (Il crache par terre.) Le dernier qui voulait aider est reparti avec nos poules.");
            return new Noeud("Bram", "(Il est livide.) C'est pas possible… Mon frère Tobin travaille pour Corvin. À la barque. Il m'a dit que c'était un accident, le pont…")
                .Choix("Les bottes, le soufre… Tout ramène à ceux qui profitent du pont cassé. Ce n'était pas un accident.", () =>
                        new Noeud("Bram", "Tobin… Espèce d'idiot. (Il serre les poings.) Si c'est Corvin, je veux savoir. Je suis avec toi, l'étranger."),
                    () => { Progression.Mettre("bram"); Progression.AjouterConfiance(1); })
                .Choix("Ton frère n'a peut-être rien fait. Aide-moi à trouver la vérité.", () =>
                        new Noeud("Bram", "… D'accord. Mais si tu touches à Tobin, tu auras affaire à moi."),
                    () => { Progression.Mettre("bram"); Progression.AjouterConfiance(1); });
        }
    }
}
