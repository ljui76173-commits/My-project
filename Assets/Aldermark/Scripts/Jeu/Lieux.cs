using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    /// <summary>Lieux nommés que la quête peut désigner (repère doré) : le nœud, le gué, Ilse…</summary>
    public static class Lieux
    {
        public static readonly Dictionary<string, Transform> Tous = new Dictionary<string, Transform>();
        public static void Declarer(string id, Transform t) => Tous[id] = t;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reinitialiser() => Tous.Clear();
    }
}
