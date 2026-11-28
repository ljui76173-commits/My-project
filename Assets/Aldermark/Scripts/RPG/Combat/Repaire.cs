using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// Un repaire : là où vit une espèce (gelées, meute de loups, camp de gobelins, antre du troll).
    /// Il fait apparaître ses créatures au lancement, puis se repeuple avec le temps
    /// (en heures de jeu), jamais sous les yeux du héros.
    /// </summary>
    public class Repaire : MonoBehaviour
    {
        public enum Espece { Gelee, Loup, Gobelin, Troll }

        public Espece espece;
        public int nombre = 3;
        public int niveauMin = 1, niveauMax = 2;
        public float rayon = 6f;
        [Tooltip("Heures de jeu avant qu'une créature tuée soit remplacée.")]
        public float heuresRepeuplement = 6f;

        readonly List<Monstre> membres = new List<Monstre>();
        double prochainRepeuplement;
        float minuteur;

        public static readonly List<Repaire> Tous = new List<Repaire>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reinitialiser() => Tous.Clear();

        void OnEnable() => Tous.Add(this);
        void OnDisable() => Tous.Remove(this);

        void Start()
        {
            for (int i = 0; i < nombre; i++) Faire();
        }

        void Update()
        {
            minuteur -= Time.deltaTime;
            if (minuteur > 0f) return;
            minuteur = 2f;
            membres.RemoveAll(m => m == null || !m.Vivant);
            if (membres.Count >= nombre || Horloge.Temps < prochainRepeuplement) return;
            var h = CombatHeros.Instance;
            if (h != null && (h.transform.position - transform.position).sqrMagnitude < 50f * 50f) return;   // pas sous ses yeux
            Faire();
            prochainRepeuplement = Horloge.Temps + heuresRepeuplement / 24.0 * Horloge.DureeJour;
        }

        /// <summary>Une créature du repaire est tombée : le compte à rebours du repeuplement commence.</summary>
        public void Perdu(Monstre m)
        {
            membres.Remove(m);
            double apres = Horloge.Temps + heuresRepeuplement / 24.0 * Horloge.DureeJour;
            if (prochainRepeuplement < apres) prochainRepeuplement = apres;
        }

        void Faire()
        {
            Vector2 r = Random.insideUnitCircle * rayon;
            Vector3 p = transform.position + new Vector3(r.x, 0, r.y);
            p.y = Mathf.Max(Monde.Hauteur(p.x, p.z), -1f) + .2f;
            int n = Random.Range(niveauMin, niveauMax + 1);
            if (espece == Espece.Gelee) Gelee.Prochaine = Random.value < .6f ? 1.25f : .62f;
            // Des lanceurs de sorts : au moins un chaman par camp de gobelins, des gelées de givre, des trolls anciens
            switch (espece)
            {
                case Espece.Gobelin: Monstre.LanceurProchain = !membres.Exists(x => x != null && x.Vivant && x.Lanceur) || Random.value < .2f; break;
                case Espece.Gelee: Monstre.LanceurProchain = Random.value < .3f; break;
                case Espece.Troll: Monstre.LanceurProchain = Random.value < .4f; break;
            }
            var m = Creer(espece, p, n);
            m.repaire = this;
            m.origine = transform.position;
            membres.Add(m);
        }

        /// <summary>Fait apparaître une créature (repaires, console, division des gelées).</summary>
        public static Monstre Creer(Espece e, Vector3 position, int niveau)
        {
            var go = new GameObject(e.ToString());
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0, Random.Range(0f, 360f), 0));
            Monstre m;
            switch (e)
            {
                case Espece.Loup: m = go.AddComponent<Loup>(); break;
                case Espece.Gobelin: m = go.AddComponent<Gobelin>(); break;
                case Espece.Troll: m = go.AddComponent<Troll>(); break;
                default: m = go.AddComponent<Gelee>(); break;
            }
            m.FixerNiveau(niveau);
            m.origine = position;
            return m;
        }
    }
}
