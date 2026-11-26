using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    /// <summary>Tout ce qui s'enregistre dans la sauvegarde : un identifiant stable, son état en JSON.</summary>
    public interface ISauvegardable
    {
        string IdSauvegarde { get; }
        string Ecrire();
        void Lire(string json);
    }

    /// <summary>
    /// Le registre du monde : un identifiant unique pour chaque PNJ, objet, bâtiment, contrat.
    /// Les relations, les souvenirs et la sauvegarde s'y réfèrent.
    /// </summary>
    public static class Registre
    {
        static readonly Dictionary<string, Entite> entites = new Dictionary<string, Entite>();
        static readonly List<ISauvegardable> sauvegardables = new List<ISauvegardable>();

        /// <summary>Compteur des identifiants créés en jeu (sauvegardé).</summary>
        public static long ProchainId = 1;
        public static IReadOnlyList<ISauvegardable> Sauvegardables => sauvegardables;
        public static int NombreEntites => entites.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reinitialiser() { entites.Clear(); sauvegardables.Clear(); ProchainId = 1; }

        public static string NouvelId(string prefixe) => $"{prefixe}-{ProchainId++}";

        public static void Inscrire(Entite e)
        {
            if (string.IsNullOrEmpty(e.id)) e.id = NouvelId(e.prefixe);
            if (entites.TryGetValue(e.id, out var autre) && autre != null && autre != e)
                Debug.LogWarning($"Aldermark : identifiant en double dans le registre : {e.id} ({autre.name} et {e.name}).");
            entites[e.id] = e;
        }

        public static void Retirer(Entite e)
        {
            if (!string.IsNullOrEmpty(e.id) && entites.TryGetValue(e.id, out var x) && x == e) entites.Remove(e.id);
        }

        public static Entite Trouver(string id) => !string.IsNullOrEmpty(id) && entites.TryGetValue(id, out var e) ? e : null;

        public static void InscrireSauvegarde(ISauvegardable s) { if (!sauvegardables.Contains(s)) sauvegardables.Add(s); }
        public static void RetirerSauvegarde(ISauvegardable s) => sauvegardables.Remove(s);
    }

    /// <summary>Une chose du monde qui porte un identifiant stable (posé par l'éditeur, ou créé en jeu).</summary>
    public class Entite : MonoBehaviour
    {
        public string id;
        public string prefixe = "obj";

        void Awake() => Registre.Inscrire(this);
        void OnDestroy() => Registre.Retirer(this);
    }
}
