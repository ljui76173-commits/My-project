using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Aldermark
{
    /// <summary>
    /// Le chef d'orchestre du mode RPG (monde ouvert, sans scénario) : fait tourner l'horloge,
    /// reprend la partie sauvegardée au lancement, sauvegarde toutes les 5 minutes, en dormant,
    /// avec F5 et en quittant ; F9 recharge la dernière sauvegarde.
    /// </summary>
    public class MondeRPG : MonoBehaviour
    {
        public static bool Actif { get; private set; }
        public static MondeRPG Instance { get; private set; }

        public JoueurAldric joueur;
        [Tooltip("Où se réveiller la première fois (le lit de la maison du hameau).")]
        public Transform reveil;
        public float intervalleSauvegarde = 300f;

        float minuteurSauvegarde;
        bool pret;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reinitialiser() { Actif = false; Instance = null; }

        void Awake()
        {
            Actif = true;
            Instance = this;
            if (joueur == null) joueur = FindFirstObjectByType<JoueurAldric>();
            if (joueur != null && joueur.GetComponent<CombatHeros>() == null) joueur.gameObject.AddComponent<CombatHeros>();
        }

        void OnDestroy() { if (Instance == this) { Actif = false; Instance = null; } }

        IEnumerator Start()
        {
            HUD.Fondu(2.5f);
            yield return null;   // tout le monde a fini son Start : on peut écraser les valeurs par celles de la sauvegarde
            if (Sauvegarde.Existe && Sauvegarde.Charger())
            {
                HUD.Message("PARTIE REPRISE", $"{Horloge.Texte}\nSauvegarde du {Sauvegarde.DateDerniere}.\n\nC : Statut · F5 : sauvegarder · F9 : recharger", 6f);
            }
            else
            {
                if (reveil != null) joueur.Teleporter(reveil.position, reveil.eulerAngles.y);
                var s = StatsHeros.Instance;
                yield return new WaitForSeconds(1.6f);
                Sons.Jouer("decouverte", .8f, .8f);
                HUD.Message("STATUT — " + (s != null ? s.nomHeros.ToUpper() : "ALDRIC"),
                    "Niveau 1 · Titre : Nouveau venu\n\nTu te réveilles dans une petite maison que tu ne connais pas.\nDehors, un monde entier, et rien ne t'y oblige.\n\nC : Statut   ·   F : interagir   ·   F5 : sauvegarder", 9f);
            }
            pret = true;
        }

        void Update()
        {
            if (!pret) return;
            float dt = Time.deltaTime;
            if (!HUD.MenuOuvert && !Dialogues.EnCours) Horloge.Avancer(dt);

            minuteurSauvegarde += Time.unscaledDeltaTime;
            if (minuteurSauvegarde >= intervalleSauvegarde) { minuteurSauvegarde = 0; Sauver(true); }

            var kb = Keyboard.current;
            if (kb == null || ConsoleDebug.Ouverte) return;
            if (kb.f5Key.wasPressedThisFrame) Sauver(false);
            if (kb.f9Key.wasPressedThisFrame) Recharger();
        }

        void OnApplicationQuit() { if (pret) Sauvegarde.Sauver(); }

        public static void Sauver(bool discret)
        {
            bool ok = Sauvegarde.Sauver();
            if (ok) HUD.Toast(discret ? "Sauvegarde automatique" : "Partie sauvegardée");
            else HUD.Message("SAUVEGARDE IMPOSSIBLE", Sauvegarde.DerniereErreur ?? "Erreur inconnue.", 5f);
            if (!discret && ok) Sons.Jouer("clic", .6f, 1.1f);
        }

        public static void Recharger()
        {
            if (!Sauvegarde.Existe) { HUD.Message("AUCUNE SAUVEGARDE", "Appuie sur F5 pour sauvegarder.", 3f); return; }
            HUD.Fondu(1.2f);
            if (Sauvegarde.Charger()) HUD.Message("PARTIE RECHARGÉE", Horloge.Texte, 3f);
        }

        /// <summary>Dormir : le temps passe, tout est restauré, et la partie est sauvegardée.</summary>
        public static void Dormir(float jusqua)
        {
            HUD.Fondu(3f);
            if (Instance != null) Instance.StartCoroutine(Instance.Sommeil(() => Horloge.AvancerJusqua(jusqua)));
        }

        public static void Reposer(float heures)
        {
            HUD.Fondu(2f);
            if (Instance != null) Instance.StartCoroutine(Instance.Sommeil(() => Horloge.AvancerHeures(heures)));
        }

        /// <summary>Le héros tombe : tout devient noir, il se réveille dans son lit quatre heures plus tard, soigné.</summary>
        public IEnumerator Mort()
        {
            HUD.Message("TU T'ÉCROULES", "Tout devient noir…", 3f);
            Sons.Jouer("onde", .5f, .55f);
            yield return new WaitForSecondsRealtime(1.3f);
            HUD.Fondu(3.4f);
            yield return new WaitForSecondsRealtime(1.1f);   // l'écran est noir
            if (reveil != null) joueur.Teleporter(reveil.position, reveil.eulerAngles.y);
            Horloge.AvancerHeures(4f);
            if (StatsHeros.Instance != null) StatsHeros.Instance.Soigner();
            yield return new WaitForSecondsRealtime(1.2f);
            HUD.Message("TU TE RÉVEILLES", $"{Horloge.Texte}\nQuelqu'un t'a ramené chez toi. Quatre heures ont passé.", 6f);
        }

        IEnumerator Sommeil(System.Action avancer)
        {
            yield return new WaitForSeconds(1f);   // l'écran est noir
            avancer();
            if (StatsHeros.Instance != null) StatsHeros.Instance.Soigner();
            Sauver(true);
            yield return new WaitForSeconds(.8f);
            HUD.Message("REPOSÉ", $"{Horloge.Texte}\nVie, mana et endurance restaurés. Partie sauvegardée.", 4f);
        }
    }
}
