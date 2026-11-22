using UnityEngine;
using UnityEngine.InputSystem;

namespace Aldermark
{
    /// <summary>
    /// Prologue (scènes 0 à 2 de la tranche verticale) :
    /// « Le dernier quai » : l'entrepôt à 6 h, trois cartons à scanner, puis la rue sous la pluie et le camion ;
    /// « L'Entre-Deux » : le bureau d'Ysolde dans le vide blanc, les orbes, le pacte ;
    /// « Le réveil » : l'herbe haute de Varenne, et le rendu peint qui s'allume.
    /// Le rendu reste terne (non peint) jusqu'au réveil.
    /// </summary>
    public class Prologue : MonoBehaviour
    {
        public static bool EnCours { get; private set; }

        [Header("Le dernier quai")]
        public Transform departEntrepot;
        public Transform sortieEntrepot;
        public Transform[] cartons;
        public Transform departRue;
        public Transform passage;
        public Transform camion;
        public Transform finCamion;
        public ParticleSystem pluie;
        [Header("L'Entre-Deux")]
        public Transform departEntreDeux;
        public Interlocuteur ysolde;

        enum Phase { Entrepot, Rue, Camion, EntreDeux, Reveil, Fini }
        Phase phase;
        JoueurAldric joueur;
        RenduPeint rendu;
        Vector3 departVallee;
        float lacetVallee, minuteur, vitesseCamion;
        bool[] scannes;
        int nbScannes;
        bool sortieOuverte, klaxon, dialogueLance;
        Color brouillardSauve;
        float brouillardDebut, brouillardFin;
        AudioSource sourcePluie;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reinitialiser() => EnCours = false;

        void Awake()
        {
            // Avant tout le reste : le jeu sait dès la première image qu'on est dans le prologue
            if (!Progression.A("prologue_fini")) EnCours = true;
        }

        void Start()
        {
            joueur = FindFirstObjectByType<JoueurAldric>();
            if (!EnCours || joueur == null) { EnCours = false; gameObject.SetActive(false); return; }
            rendu = joueur.rendu;
            departVallee = joueur.transform.position;
            lacetVallee = joueur.transform.eulerAngles.y;
            scannes = new bool[cartons != null ? cartons.Length : 0];
            if (rendu != null) rendu.peint = false;
            HUD.ModePrologue(true);
            Chantier.FixerHeure(6.3f);
            joueur.Teleporter(departEntrepot.position, departEntrepot.eulerAngles.y);
            if (pluie != null) pluie.Stop();
            HUD.SousTitre("Six heures vingt-deux. L'entrepôt.\nTrois références à prélever. Comme toujours, elles sont rangées tout au fond.", 7f);
            HUD.Invite(null);
        }

        void Update()
        {
            if (!EnCours) return;
            var kb = Keyboard.current;
            // Raccourci : passer le prologue (F12), ou il est marqué fini par un raccourci de test
            if ((kb != null && kb.f12Key.wasPressedThisFrame) || Progression.A("prologue_fini")) { Terminer(); return; }
            minuteur += Time.deltaTime;

            switch (phase)
            {
                case Phase.Entrepot: Entrepot(kb); break;
                case Phase.Rue: Rue(); break;
                case Phase.Camion: Camion(); break;
                case Phase.EntreDeux: EntreDeux(); break;
                case Phase.Reveil: Reveil(); break;
            }
            if (sourcePluie != null && pluie != null) pluie.transform.position = joueur.transform.position + Vector3.up * 8f;
        }

        // ---------- l'entrepôt ----------

        void Entrepot(Keyboard kb)
        {
            Transform proche = null; int iProche = -1;
            var cam = joueur.cam.transform;
            for (int i = 0; i < scannes.Length; i++)
            {
                if (scannes[i] || cartons[i] == null) continue;
                Vector3 d = cartons[i].position - cam.position;
                if (d.magnitude < 2.8f && Vector3.Angle(cam.forward, d) < 32f) { proche = cartons[i]; iProche = i; }
            }
            if (proche != null)
            {
                HUD.Invite($"[F]  Scanner le carton — réf. {Reference(iProche)}");
                var pad = Gamepad.current;
                if ((kb != null && kb.fKey.wasPressedThisFrame) || (pad != null && pad.buttonWest.wasPressedThisFrame))
                {
                    scannes[iProche] = true; nbScannes++;
                    Sons.Jouer("voix", 1f, 2.6f);
                    var etiq = proche.Find("Etiquette");
                    if (etiq != null) etiq.GetComponent<MeshRenderer>().material.SetColor("_BaseColor", new Color(.3f, .9f, .4f));
                    if (nbScannes == 1) HUD.SousTitre("Bip. Une sur trois.", 2.5f);
                    if (nbScannes == 2) HUD.SousTitre("Dix-huit kilomètres à pied par jour, pour rien. Tu le sais : tu as compté.", 5f);
                    if (nbScannes == scannes.Length)
                    {
                        sortieOuverte = true;
                        HUD.SousTitre("Tu l'as signalé deux fois. Personne n'a écouté.\nFin de service. La sortie est au bout de l'allée.", 6f);
                    }
                }
            }
            else HUD.Invite(sortieOuverte ? "Pointer et rentrer chez soi : la sortie, au bout de l'allée" : null);

            if (sortieOuverte && Vector3.Distance(joueur.transform.position, sortieEntrepot.position) < 2.6f)
            {
                HUD.Invite(null);
                HUD.Flash(Color.black, 2.4f);
                phase = Phase.Rue; minuteur = 0;
                Invoke(nameof(AllerDansLaRue), 1.1f);
            }
        }

        static string Reference(int i) => i switch { 0 => "0417-A", 1 => "2203-C", _ => "0098-F" };

        void AllerDansLaRue()
        {
            Chantier.FixerHeure(22.3f);
            joueur.Teleporter(departRue.position, departRue.eulerAngles.y);
            if (pluie != null)
            {
                pluie.Play();
                sourcePluie = pluie.GetComponent<AudioSource>();
                if (sourcePluie != null) { sourcePluie.clip = Sons.Clip("pluie"); sourcePluie.loop = true; sourcePluie.Play(); }
            }
            HUD.SousTitre("Le soir, il pleut. Tu rentres à pied en redessinant dans ta tête le plan de l'entrepôt.\nLe feu passe au vert pour toi.", 7f);
        }

        // ---------- la rue, le camion ----------

        void Rue()
        {
            if (minuteur < 1.5f) return;
            Vector3 p = joueur.transform.position, c = passage.position;
            if (new Vector2(p.x - c.x, p.z - c.z).magnitude < 2.2f)
            {
                phase = Phase.Camion; minuteur = 0;
                vitesseCamion = 24f;
            }
        }

        void Camion()
        {
            if (camion == null) { Basculer(); return; }
            Vector3 vers = (finCamion.position - camion.position);
            vers.y = 0;
            camion.position += vers.normalized * vitesseCamion * Time.deltaTime;
            if (!klaxon && minuteur > .2f)
            {
                klaxon = true;
                Sons.Jouer("klaxon", 1f, 1f);
                HUD.SousTitre("Des phares, sur ta gauche. Trop vite, sur la chaussée mouillée.\n« Express 24h — Livré à temps, toujours. »", 3f);
            }
            float d = Vector3.Distance(new Vector3(camion.position.x, 0, camion.position.z), new Vector3(joueur.transform.position.x, 0, joueur.transform.position.z));
            if (d < 5f || minuteur > 5f) Basculer();
        }

        void Basculer()
        {
            phase = Phase.EntreDeux; minuteur = 0;
            Sons.Jouer("coup", 1f, .4f);
            HUD.Flash(Color.white, 3.5f);
            if (pluie != null) pluie.Stop();
            if (sourcePluie != null) sourcePluie.Stop();
            Invoke(nameof(AllerDansLEntreDeux), 1.2f);
        }

        void AllerDansLEntreDeux()
        {
            brouillardSauve = RenderSettings.fogColor; brouillardDebut = RenderSettings.fogStartDistance; brouillardFin = RenderSettings.fogEndDistance;
            RenderSettings.fogColor = Color.white; RenderSettings.fogStartDistance = 6f; RenderSettings.fogEndDistance = 70f;
            Chantier.FixerHeure(12f);
            joueur.Teleporter(departEntreDeux.position, departEntreDeux.eulerAngles.y);
            HUD.SousTitre("Puis plus rien.", 3f);
        }

        // ---------- l'Entre-Deux ----------

        void EntreDeux()
        {
            RenderSettings.fogColor = Color.white;
            if (!dialogueLance && minuteur > 3.5f && ysolde != null && !Dialogues.EnCours)
            {
                dialogueLance = true;
                Dialogues.Commencer(ysolde);
            }
            // Si le joueur ferme le dialogue sans avoir signé, Ysolde reprend la parole
            if (dialogueLance && !Dialogues.EnCours && !Progression.A("pacte_signe") && minuteur > 6f)
            {
                minuteur = 3.6f;
                dialogueLance = false;
            }
            if (Progression.A("pacte_signe") && !Dialogues.EnCours)
            {
                phase = Phase.Reveil; minuteur = 0;
                HUD.Flash(Color.white, 4f);
                Invoke(nameof(Reveiller), 1.4f);
            }
        }

        // ---------- le réveil ----------

        void Reveiller()
        {
            RenderSettings.fogColor = brouillardSauve; RenderSettings.fogStartDistance = brouillardDebut; RenderSettings.fogEndDistance = brouillardFin;
            Chantier.FixerHeure(7.4f);
            joueur.Teleporter(departVallee, lacetVallee);
            joueur.Regarder(-70f);   // il se réveille allongé, les yeux vers le ciel
            HUD.SousTitre("De l'herbe haute. Une odeur de terre chaude. Deux soleils dans le ciel.", 4f);
        }

        void Reveil()
        {
            joueur.Regarder(Mathf.Lerp(-70f, 0f, Mathf.SmoothStep(0, 1, (minuteur - 1.6f) / 2.5f)));
            if (minuteur > 2.6f && rendu != null && !rendu.peint)
            {
                rendu.peint = true;   // le shader s'allume : Aldermark
                HUD.Flash(new Color(1f, .95f, .8f), 1.2f);
                Sons.Jouer("decouverte", .9f, .75f);
            }
            if (minuteur > 4.6f) Terminer();
        }

        void Terminer()
        {
            if (phase != Phase.Reveil && joueur != null)
            {
                RenderSettings.fogColor = brouillardDebut > 0 ? brouillardSauve : RenderSettings.fogColor;
                if (brouillardDebut > 0) { RenderSettings.fogStartDistance = brouillardDebut; RenderSettings.fogEndDistance = brouillardFin; }
                joueur.Teleporter(departVallee, lacetVallee);
                Chantier.FixerHeure(7.4f);
            }
            CancelInvoke();
            if (pluie != null) pluie.Stop();
            if (sourcePluie != null) sourcePluie.Stop();
            if (rendu != null) rendu.peint = true;
            if (joueur != null) joueur.Regarder(0f);
            Progression.Mettre("prologue_fini");
            Progression.Mettre("pacte_signe");
            EnCours = false;
            phase = Phase.Fini;
            HUD.ModePrologue(false);
            HUD.Invite(null);
            gameObject.SetActive(false);
        }
    }
}
