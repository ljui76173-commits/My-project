using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    public enum TypeLieu { Maison, Hameau, Village, Auberge, Forge, Marche, Moulin, Chapelle, Lavoir, Puits, Quintaine, Gelees, Loups, Gobelins, Troll }
    public enum OngletCarte { Tout, Lieux, Commerces, Services, Dangers, Reperes }

    /// <summary>
    /// Un lieu qui figure sur la carte : une icône par type, rangée dans un onglet de la légende
    /// (Lieux, Commerces, Services, Dangers). Il apparaît quand on l'a découvert (ou s'il est connu d'emblée).
    /// </summary>
    public class Lieu : MonoBehaviour
    {
        public static readonly List<Lieu> Tous = new List<Lieu>();

        public string nom = "Lieu";
        public TypeLieu type;
        [Tooltip("Connu dès le début (ta maison, ton hameau, le bourg voisin).")]
        public bool connu;

        public string Id
        {
            get
            {
                var e = GetComponent<Entite>();
                return e != null && !string.IsNullOrEmpty(e.id) ? e.id : "lieu-" + nom;
            }
        }

        public OngletCarte Onglet => OngletDe(type);
        public bool Decouvert => connu || (CarteMonde.Instance != null && CarteMonde.Instance.EstDecouvert(Id));
        public Vector3 Position => transform.position;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reinitialiser() => Tous.Clear();

        void OnEnable() => Tous.Add(this);
        void OnDisable() => Tous.Remove(this);

        public static OngletCarte OngletDe(TypeLieu t)
        {
            switch (t)
            {
                case TypeLieu.Auberge: case TypeLieu.Forge: case TypeLieu.Marche: case TypeLieu.Moulin: return OngletCarte.Commerces;
                case TypeLieu.Chapelle: case TypeLieu.Lavoir: case TypeLieu.Puits: case TypeLieu.Quintaine: return OngletCarte.Services;
                case TypeLieu.Gelees: case TypeLieu.Loups: case TypeLieu.Gobelins: case TypeLieu.Troll: return OngletCarte.Dangers;
                default: return OngletCarte.Lieux;
            }
        }

        public static string NomType(TypeLieu t)
        {
            switch (t)
            {
                case TypeLieu.Maison: return "Ta maison";
                case TypeLieu.Hameau: return "Hameau";
                case TypeLieu.Village: return "Bourg";
                case TypeLieu.Auberge: return "Auberge";
                case TypeLieu.Forge: return "Forge";
                case TypeLieu.Marche: return "Marché";
                case TypeLieu.Moulin: return "Moulin";
                case TypeLieu.Chapelle: return "Chapelle";
                case TypeLieu.Lavoir: return "Lavoir";
                case TypeLieu.Puits: return "Puits";
                case TypeLieu.Quintaine: return "Entraînement";
                case TypeLieu.Gelees: return "Gelées";
                case TypeLieu.Loups: return "Loups";
                case TypeLieu.Gobelins: return "Gobelins";
                default: return "Troll";
            }
        }

        public static string NomOnglet(OngletCarte o)
        {
            switch (o)
            {
                case OngletCarte.Lieux: return "Lieux";
                case OngletCarte.Commerces: return "Commerces";
                case OngletCarte.Services: return "Services";
                case OngletCarte.Dangers: return "Dangers";
                case OngletCarte.Reperes: return "Mes repères";
                default: return "Tout";
            }
        }
    }
}
