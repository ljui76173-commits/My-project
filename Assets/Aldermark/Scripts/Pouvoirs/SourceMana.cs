using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// Quelque chose que le Siphon peut vider : le pendentif d'Ilse, un mage, Vesk…
    /// Le Siphon « ferme la vanne » : la réserve baisse tant qu'Aldric tire dessus.
    /// </summary>
    public class SourceMana : MonoBehaviour
    {
        public string nom = "Source";
        public float reserve = 20f;
        [Tooltip("Mana récupéré par Aldric par seconde de Siphon")] public float debit = 4f;
        [Tooltip("Hauteur du point visé")] public float hauteur = 1.3f;
        public static readonly List<SourceMana> Toutes = new List<SourceMana>();

        public System.Action<SourceMana> Videe;
        /// <summary>Résistance au Siphon (Vesk tire en sens inverse) : 0 = aucune.</summary>
        [System.NonSerialized] public float resistance;
        [System.NonSerialized] public bool siphonnee;

        void OnEnable() => Toutes.Add(this);
        void OnDisable() => Toutes.Remove(this);

        public Vector3 Point => transform.position + Vector3.up * hauteur;
        public bool Vide => reserve <= 0f;

        /// <summary>Tire du mana pendant dt secondes ; renvoie ce qu'Aldric récupère.</summary>
        public float Tirer(float dt)
        {
            if (Vide) return 0f;
            float q = Mathf.Min(reserve, debit * dt * Mathf.Max(0f, 1f - resistance));
            reserve -= q;
            if (Vide) Videe?.Invoke(this);
            return q;
        }
    }
}
