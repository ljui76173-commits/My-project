using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// Un coffre où l'on range des objets : E pour l'ouvrir (le couvercle se lève), puis on fait passer
    /// les objets du coffre au sac et inversement. Son contenu est sauvegardé.
    /// </summary>
    public class Coffre : MonoBehaviour, ISauvegardable
    {
        public static readonly List<Coffre> Tous = new List<Coffre>();
        /// <summary>Le coffre ouvert en ce moment (sa fenêtre est affichée), sinon null.</summary>
        public static Coffre Ouvert { get; private set; }

        public string nom = "Coffre";
        public string idSauvegarde = "coffre";
        public Transform couvercle;
        public List<Pile> contenu = new List<Pile>();

        public string IdSauvegarde => idSauvegarde;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reinitialiser() { Tous.Clear(); Ouvert = null; }

        void Awake() => Registre.InscrireSauvegarde(this);
        void OnDestroy() { Registre.RetirerSauvegarde(this); if (Ouvert == this) Ouvert = null; }
        void OnEnable() => Tous.Add(this);
        void OnDisable() => Tous.Remove(this);

        public void Ouvrir()
        {
            Ouvert = this;
            Sons.JouerA("impactBois", transform.position, .5f, 1.4f);
            Sons.Jouer("clic", .5f, 1.1f);
        }

        public static void Fermer()
        {
            if (Ouvert == null) return;
            Sons.JouerA("impactBois", Ouvert.transform.position, .45f, 1.2f);
            Ouvert = null;
        }

        void Update()
        {
            if (couvercle == null) return;
            float voulu = Ouvert == this ? -105f : 0f;
            couvercle.localRotation = Quaternion.Slerp(couvercle.localRotation, Quaternion.Euler(voulu, 0, 0), Mathf.Clamp01(Time.unscaledDeltaTime * 8f));
        }

        public int Combien(string id)
        {
            int n = 0;
            foreach (var p in contenu) if (p.id == id) n += p.n;
            return n;
        }

        public void Deposer(string id, int n)
        {
            var d = Objets.Def(id);
            if (d == null || n <= 0) return;
            foreach (var p in contenu)
                if (p.id == id && p.n < d.pileMax) { int k = Mathf.Min(n, d.pileMax - p.n); p.n += k; n -= k; if (n <= 0) return; }
            while (n > 0) { int k = Mathf.Min(n, d.pileMax); contenu.Add(new Pile(id, k)); n -= k; }
        }

        public bool Prendre(int index, out Pile pile)
        {
            pile = null;
            if (index < 0 || index >= contenu.Count) return false;
            pile = contenu[index];
            contenu.RemoveAt(index);
            return true;
        }

        [System.Serializable] class Etat { public List<Pile> contenu; }

        public string Ecrire() => JsonUtility.ToJson(new Etat { contenu = contenu });

        public void Lire(string json)
        {
            var e = JsonUtility.FromJson<Etat>(json);
            if (e?.contenu != null) contenu = e.contenu;
        }
    }
}
