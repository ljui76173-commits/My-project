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
        static Dialogues instance;
        Noeud courant;
        List<Choix> visibles;
        float delai;

        void Awake() { instance = this; EnCours = false; }

        public static void Commencer(Interlocuteur qui)
        {
            if (instance == null || qui == null) return;
            Noeud n = qui.id switch
            {
                "lise" => Lise(),
                "odon" => Odon(),
                "bram" => Bram(),
                _ => new Noeud(qui.nom, "…")
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
            HUD.AfficherDialogue(n.qui, n.texte, visibles.Select(c => c.texte).ToList());
        }

        void Fermer()
        {
            EnCours = false;
            courant = null;
            HUD.FermerDialogue();
        }

        void Update()
        {
            if (!EnCours) return;
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
            if (i < 0 || i >= visibles.Count) return;
            var c = visibles[i];
            c.effet?.Invoke();
            Afficher(c.suite?.Invoke());
        }

        // ------------------------------------------------------------------
        // Contenu de l'arc 1
        // ------------------------------------------------------------------

        static bool A(string d) => Progression.A(d);

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
                    new Noeud("Lise", "Le vieux Odon a commencé. Après, des hommes en noir sont venus la nuit. Maintenant il peut plus se servir de sa main. Il est près du puits.")
                        .Choix("Merci, Lise. Je vais le voir.", null, () => Progression.Mettre("lise")));

        static Noeud Odon()
        {
            if (!A("lise")) return new Noeud("Odon", "… (Le vieil homme te dévisage sans un mot.)");

            if (!A("test_four"))
                return new Noeud("Odon", "Un monsieur des bois. Tu viens de la part de Corvin ? Ou du Comte ?")
                    .Choix("Je viens d'un pays lointain. J'y ai étudié la construction.", OdonQuestion)
                    .Choix("Ni l'un ni l'autre. Je voudrais aider ce village.", () =>
                        new Noeud("Odon", "Aider. Le dernier qui voulait « aider », c'était un percepteur du Comte. Il est reparti avec nos poules.")
                            .Choix("Alors mettez-moi à l'épreuve.", OdonQuestion));

            if (!A("four_ok") || !A("sabotage_connu"))
            {
                var n = new Noeud("Odon", A("four_ok") || A("sabotage_connu")
                    ? "Bien. Et quoi d'autre ? Un maçon regarde tout."
                    : "Alors, l'étranger ? Qu'est-ce qu'il lui faut, à mon four ?");
                n.Choix("La voûte du foyer est fendue, la cheminée bouchée, la grille tordue. Et le calcaire est déjà sur le talus.", () =>
                        new Noeud("Odon", "(Un coin de sa bouche se relève.) Pas mal, pour des mains de scribe.").Choix("(Continuer)", Odon),
                    () => { Progression.Mettre("four_ok"); Progression.AjouterConfiance(1); },
                    () => A("vu_four") && A("vu_calcaire") && !A("four_ok"));
                n.Choix("Il y a autre chose : du soufre et des traces de bottes dans le foyer.", () =>
                        new Noeud("Odon", "Du soufre… (Il renifle les cendres sur tes doigts.) Le tonnerre sans orage. Ils l'ont préparé ici. Dans MON four. Ce qui a fait sauter le pont de mon père…").Choix("(Continuer)", Odon),
                    () => { Progression.Mettre("sabotage_connu"); Progression.AjouterConfiance(1); },
                    () => A("vu_cendres") && !A("sabotage_connu"));
                n.Choix("Je n'ai pas encore assez regardé.", () =>
                    new Noeud("Odon", "Bonne réponse. Un maçon qui parle avant de regarder, c'est un menteur. Va voir."));
                return n;
            }

            if (!A("pont_accepte"))
                return new Noeud("Odon", "L'étranger. Ce pont, c'est mon père qui l'a monté. Tu sais le rebâtir ? Vraiment ?")
                    .Choix("Oui. Mais il me faut des bras, du bois et de la chaux.", () =>
                            new Noeud("Odon", "Alors on commence à l'aube. Joss, Pell et Marthe sont à toi. Et moi, j'ai encore une main qui sait poser une pierre."),
                        () => { Progression.Mettre("pont_accepte"); Progression.AjouterConfiance(1); })
                    .Choix("Pas encore. Il faut d'abord comprendre qui l'a fait sauter.", () =>
                        new Noeud("Odon", "Sage. Mais ne traîne pas : le village mange des champignons depuis trois lunes."));

            return new Noeud("Odon", "Le chantier t'attend, l'étranger.");
        }

        static Noeud OdonQuestion()
        {
            Progression.Mettre("test_four");
            return new Noeud("Odon", "Des mains de scribe… Mais tu as regardé mon four en arrivant. Personne ne regarde ce four. Il est mort depuis six ans. Qu'est-ce qu'il faut pour le relancer ? Va le voir, et reviens me dire.");
        }

        static Noeud Bram()
        {
            if (A("bram")) return new Noeud("Bram", "Dis-moi où porter, je porte.");
            if (!A("vu_corvin")) return new Noeud("Bram", "Aider. (Il crache par terre.) Le dernier qui voulait aider est reparti avec nos poules.");
            return new Noeud("Bram", "Mon frère Tobin travaille pour Corvin. À la barque. Il m'a dit que c'était un accident, le pont…")
                .Choix("J'ai suivi la trace : l'explosion a été préparée dans le four, et la piste mène chez Corvin.", () =>
                        new Noeud("Bram", "… Tobin. Espèce d'idiot. (Il serre les poings.) Je suis avec toi, l'étranger. Dis-moi où porter."),
                    () => { Progression.Mettre("bram"); Progression.AjouterConfiance(1); })
                .Choix("Je ne sais pas encore. Garde ça pour toi.", () => new Noeud("Bram", "Hm. Je te surveille."));
        }
    }
}
