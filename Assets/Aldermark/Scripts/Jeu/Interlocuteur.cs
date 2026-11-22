using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    /// <summary>Personnage à qui Aldric peut parler (touche F).</summary>
    public class Interlocuteur : MonoBehaviour
    {
        public string id = "lise";
        public string nom = "Lise";
        public static readonly List<Interlocuteur> Tous = new List<Interlocuteur>();
        void OnEnable() => Tous.Add(this);
        void OnDisable() => Tous.Remove(this);
    }
}
