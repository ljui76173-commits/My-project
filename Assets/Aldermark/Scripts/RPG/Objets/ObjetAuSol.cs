using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// Un objet posé dans le monde (butin, objet jeté) : il jaillit, retombe, puis flotte en tournant
    /// au-dessus du sol avec un fin rayon de lumière pour qu'on le retrouve dans l'herbe. E pour le ramasser ;
    /// les pièces se ramassent seules en passant dessus.
    /// </summary>
    public class ObjetAuSol : MonoBehaviour
    {
        public static readonly List<ObjetAuSol> Tous = new List<ObjetAuSol>();

        public string id;
        public int quantite = 1;

        public DefObjet Def => Objets.Def(id);

        Transform modele;
        Vector3 depart, arrivee;
        float t, sol, age;
        bool pose;
        const float DureeVol = .55f, DureeVie = 900f;
        static Material matRayon;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reinitialiser() => Tous.Clear();

        /// <summary>Fait tomber un objet : il part de « position » et retombe un peu plus loin (« elan »).</summary>
        public static ObjetAuSol Creer(string id, int n, Vector3 position, Vector3 elan)
        {
            var d = Objets.Def(id);
            if (d == null || n <= 0) return null;
            var go = new GameObject("Objet_" + id);
            go.transform.position = position;
            var o = go.AddComponent<ObjetAuSol>();
            o.id = id; o.quantite = n;
            o.depart = position;
            Vector3 cible = position + new Vector3(elan.x, 0, elan.z) * DureeVol;
            o.sol = Sol(cible);
            o.arrivee = new Vector3(cible.x, o.sol + .32f, cible.z);
            o.ConstruireVisuel(d);
            return o;
        }

        /// <summary>Le sol sous un point (plancher, rocher, terrain), sans compter les créatures ni le héros.</summary>
        static float Sol(Vector3 p)
        {
            float best = float.MaxValue, y = Monde.Hauteur(p.x, p.z);
            foreach (var hit in Physics.RaycastAll(p + Vector3.up * 1.5f, Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider is CharacterController || hit.distance >= best) continue;
                best = hit.distance;
                y = hit.point.y;
            }
            return y;
        }

        void ConstruireVisuel(DefObjet d)
        {
            modele = Objets.Modele(d, transform);
            if (d.categorie == Categorie.Arme) { modele.localRotation = Quaternion.Euler(0, 0, 90f); modele.localScale = Vector3.one * .8f; }
            if (matRayon == null) matRayon = Effets.Transparent(new Color(1f, .95f, .8f, .18f));
            var rayon = Pantin.Bloc(transform, PrimitiveType.Cylinder, new Vector3(0, .55f, 0), new Vector3(.035f, .6f, .035f), matRayon);
            rayon.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (d.valeur >= 15 && d.categorie != Categorie.Monnaie) rayon.localScale = new Vector3(.06f, 1f, .06f);
        }

        void OnEnable() => Tous.Add(this);
        void OnDisable() => Tous.Remove(this);

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            if (!pose)
            {
                t += dt;
                float u = Mathf.Clamp01(t / DureeVol);
                Vector3 p = Vector3.Lerp(depart, arrivee, u);
                p.y += Mathf.Sin(u * Mathf.PI) * 1.1f;
                transform.position = p;
                if (u >= 1f) pose = true;
            }
            else transform.position = new Vector3(transform.position.x, sol + .32f + Mathf.Sin(age * 2.2f) * .05f, transform.position.z);
            if (modele != null) modele.Rotate(0, 70f * dt, 0, Space.World);
            if (age > DureeVie) Destroy(gameObject);
        }
    }
}
