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

        static float HauteurVoix(string qui) => qui switch { "Lise" => 1.75f, "Odon" => .72f, "Bram" => .95f, _ => 1.1f };

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
                            new Noeud("Odon", "Alors on commence à l'aube. Joss, Pell et Marthe sont à toi. Et moi, j'ai encore une main qui sait poser une pierre. D'abord le four : sans chaux, pas de piles. Il lui faut quatre blocs et des bras."),
                        () => { Progression.Mettre("pont_accepte"); Progression.AjouterConfiance(1); },
                        () => A("vu_corvin"))
                    .Choix("Pas encore. Je veux d'abord suivre la piste de ceux qui l'ont fait sauter.", () =>
                        new Noeud("Odon", "Sage. Mais ne traîne pas : le village mange des champignons depuis trois lunes."));

            if (!A("four_repare")) return new Noeud("Odon", "Le four d'abord. Quatre blocs, et des bras pour la voûte. La rivière en est pleine, de blocs : il suffit de les sortir et de les porter.");
            if (!A("pont_ouvert")) return new Noeud("Odon", Chantier.BlocageChantier != "" ? $"Le chantier attend. {Chantier.BlocageChantier}. Un pont, ça se bâtit avec des porteurs, pas avec des souhaits." : "Ça avance, l'étranger. Mon père serait content.");
            return new Noeud("Odon", "Il tient. Il tient vraiment. (Il pose sa main valide sur la rambarde.) Merci, Bâtisseur.");
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
