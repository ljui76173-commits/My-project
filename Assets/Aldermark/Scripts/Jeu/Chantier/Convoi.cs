using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// La fin de la boucle du pont : une fois le pont provisoire ouvert, les charrettes du Comte,
    /// tirées par leurs bœufs, traversent la rivière et montent jusqu'au marché de Varenne.
    /// L'arrivée du convoi donne la Dévotion du village et le titre de « Bâtisseur ».
    /// </summary>
    public class Convoi : MonoBehaviour
    {
        public Transform[] charrettes;
        public Transform[] attelages;
        public Habitant[] charretiers;
        public Chantier chantier;
        public Transform arrivee;

        class Voiture { public Transform c, a; public Vector3 relPos; public Quaternion relRot; public List<Vector3> chemin; public int etape; public float retard; public Transform[] roues; public bool arrivee; }
        readonly List<Voiture> voitures = new List<Voiture>();
        bool parti, fini;
        float attente = 4f;
        const float Vitesse = 2.3f;

        void Start()
        {
            if (charrettes == null) return;
            for (int i = 0; i < charrettes.Length; i++)
            {
                var c = charrettes[i];
                if (c == null) continue;
                var v = new Voiture { c = c, a = attelages != null && i < attelages.Length ? attelages[i] : null, retard = i * 5f };
                if (v.a != null) { v.relPos = c.InverseTransformPoint(v.a.position); v.relRot = Quaternion.Inverse(c.rotation) * v.a.rotation; }
                var roues = new List<Transform>();
                foreach (Transform t in c) if (t.name.StartsWith("Roue")) roues.Add(t);
                v.roues = roues.ToArray();
                voitures.Add(v);
            }
        }

        List<Vector3> Chemin(Voiture v, int rang)
        {
            Vector3 p = chantier != null ? chantier.centrePont : new Vector3(Monde.PontX, 1.7f, -9f);
            float L = chantier != null ? chantier.longueurPont : 32f;
            Vector2 fin = arrivee != null ? new Vector2(arrivee.position.x, arrivee.position.z) : Monde.Village;
            var l = new List<Vector3>
            {
                v.c.position,
                new Vector3(p.x - L / 2f - 9f, 0, p.z),
                new Vector3(p.x - L / 2f - 1f, 0, p.z),
                new Vector3(p.x + L / 2f + 1f, 0, p.z),
                new Vector3(p.x + L / 2f + 9f, 0, p.z),
                new Vector3((p.x + L / 2f + fin.x) / 2f, 0, (p.z + fin.y) / 2f - 4f),
                new Vector3(fin.x - 6f - rang * 6f, 0, fin.y - 8f - rang * 2f),
            };
            return l;
        }

        float Hauteur(Vector3 q)
        {
            if (chantier != null)
            {
                var p = chantier.centrePont;
                if (Mathf.Abs(q.x - p.x) < chantier.longueurPont / 2f + .5f && Mathf.Abs(q.z - p.z) < 2.6f) return p.y + .15f;
            }
            return Mathf.Max(Monde.Hauteur(q.x, q.z), -.1f);
        }

        void Update()
        {
            if (fini || !Progression.A("pont_ouvert")) return;
            if (!parti)
            {
                attente -= Time.deltaTime;
                if (attente > 0) return;
                parti = true;
                for (int i = 0; i < voitures.Count; i++) voitures[i].chemin = Chemin(voitures[i], i);
                Sons.Jouer("quete", .8f, .9f);
                HUD.Message("LE CONVOI DU COMTE", "De l'autre côté de la rivière, les charretiers lancent leurs bœufs.\nPour la première fois depuis trois lunes, des charrettes vont passer.", 7f);
            }

            float dt = Time.deltaTime;
            int arrivees = 0;
            for (int i = 0; i < voitures.Count; i++)
            {
                var v = voitures[i];
                if (v.arrivee) { arrivees++; continue; }
                if (v.retard > 0) { v.retard -= dt; continue; }
                if (v.etape >= v.chemin.Count - 1) { v.arrivee = true; continue; }
                Vector3 pos = v.c.position, but = v.chemin[v.etape + 1];
                Vector3 d = new Vector3(but.x - pos.x, 0, but.z - pos.z);
                if (d.magnitude < .4f) { v.etape++; continue; }
                Vector3 dir = d.normalized;
                pos += dir * Vitesse * dt;
                pos.y = Hauteur(pos);
                v.c.position = pos;
                var rot = Quaternion.LookRotation(dir) * Quaternion.Euler(0, -90f, 0);   // la charrette avance vers son +x
                v.c.rotation = Quaternion.Slerp(v.c.rotation, rot, dt * 2.5f);
                foreach (var r in v.roues) r.Rotate(0, Vitesse * dt / .75f * Mathf.Rad2Deg, 0, Space.Self);
                if (v.a != null)
                {
                    Vector3 pa = v.c.TransformPoint(v.relPos);
                    pa.y = Hauteur(pa);
                    v.a.SetPositionAndRotation(pa, v.c.rotation * v.relRot);
                }
                if (charretiers != null && i < charretiers.Length && charretiers[i] != null)
                {
                    Vector3 cote = v.c.TransformPoint(new Vector3(1.5f, 0, 1.8f));
                    charretiers[i].AllerVers(new Vector2(cote.x, cote.z), 2.6f);
                }
            }

            if (arrivees > 0 && !Progression.A("convoi_arrive"))
            {
                Progression.Mettre("convoi_arrive");
                while (Progression.Palier < 2) Progression.AjouterConfiance(1);
                Sons.Jouer("decouverte", 1f, .8f);
                HUD.Message("TITRE OBTENU — LE BÂTISSEUR",
                    $"Les charrettes du Comte sont arrivées au marché de Varenne, au jour {Chantier.Jour}.\nLe village t'appelle désormais « le Bâtisseur ».\n\nCe soir, Varenne mange du pain.", 10f);
            }
            if (arrivees == voitures.Count && voitures.Count > 0) fini = true;
        }
    }
}
