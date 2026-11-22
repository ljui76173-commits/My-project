using UnityEngine;

namespace Aldermark
{
    /// <summary>Mouton ou poule : erre doucement autour de son enclos, s'arrête pour brouter ou picorer.</summary>
    public class Animal : MonoBehaviour
    {
        public Vector2 centre;
        public float rayon = 5f;
        public float vitesse = .7f;
        public bool poule;
        public Transform tete;

        Vector2 cible;
        float attente, phase;

        void Start() { cible = centre; attente = Random.Range(0f, 3f); phase = Random.value * 10f; }

        void Update()
        {
            float dt = Time.deltaTime;
            Vector3 p = transform.position;
            Vector2 ici = new Vector2(p.x, p.z), d = cible - ici;
            if (attente > 0)
            {
                attente -= dt;
                // Brouter ou picorer
                if (tete != null)
                {
                    float b = poule ? Mathf.Abs(Mathf.Sin(Time.time * 9f + phase)) * 40f : (Mathf.Sin(Time.time * 1.5f + phase) * .5f + .5f) * 30f;
                    tete.localRotation = Quaternion.Euler(b, 0, 0);
                }
            }
            else if (d.magnitude < .2f)
            {
                attente = Random.Range(1.5f, poule ? 3f : 6f);
                cible = centre + Random.insideUnitCircle * rayon;
            }
            else
            {
                Vector2 pas = d.normalized * vitesse * dt;
                p.x += pas.x; p.z += pas.y;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(new Vector3(d.x, 0, d.y)), dt * 4f);
                if (tete != null) tete.localRotation = Quaternion.identity;
            }
            p.y = Mathf.Max(Monde.Hauteur(p.x, p.z), -1f) + (attente <= 0 && poule ? Mathf.Abs(Mathf.Sin(Time.time * 14f)) * .04f : 0f);
            transform.position = p;
        }
    }
}
