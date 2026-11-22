using UnityEngine;

namespace Aldermark
{
    /// <summary>Un objet qui flotte et tourne doucement (les orbes d'Ysolde).</summary>
    public class Flotter : MonoBehaviour
    {
        public float amplitude = .08f, vitesse = 1.4f, rotation = 25f;
        Vector3 depart;
        float phase;
        void Start() { depart = transform.localPosition; phase = Random.value * 6f; }
        void Update()
        {
            transform.localPosition = depart + Vector3.up * Mathf.Sin(Time.time * vitesse + phase) * amplitude;
            transform.Rotate(0, rotation * Time.deltaTime, 0, Space.World);
        }
    }
}
