using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    /// <summary>Personnage à qui Aldric peut parler (touche F).</summary>
    public class Interlocuteur : MonoBehaviour
    {
        public string id = "lise";
        public string nom = "Lise";
        [Tooltip("Verbe de l'invite : « Parler à Lise », « Se coucher dans la paillasse »…")]
        public string verbe = "Parler à";
        public static readonly List<Interlocuteur> Tous = new List<Interlocuteur>();
        void OnEnable() => Tous.Add(this);
        void OnDisable() => Tous.Remove(this);
    }
}
