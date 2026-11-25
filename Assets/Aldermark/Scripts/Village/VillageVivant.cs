using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// La vie du village : les villageois suivent l'heure (champs le matin, puits et marché à midi,
    /// champs l'après-midi, auberge le soir, chez eux la nuit), et le village revit au fil du chantier :
    /// plus de monde dehors, les étals se garnissent, le moulin redémarre, les fanions sortent.
    /// </summary>
    public class VillageVivant : MonoBehaviour
    {
        [Header("Lieux")]
        public Transform marche, auberge, puits;
        [Header("Ce qui revit")]
        public Transform roueMoulin;
        public Transform[] marchandises;       // un groupe par étal, révélé au fil du chantier
        public GameObject fanions;
        public GameObject[] lumieresNuit;     // fenêtres et lanternes allumées la nuit
        public ParticleSystem fumeeForge;
        public Habitant forgeron;
        public Transform enclume;

        class Villageois { public Habitant h; public Vector2 maison, champ; public Renderer[] rendus; public bool dehors; public float hasard; }
        readonly List<Villageois> gens = new List<Villageois>();
        float vitesseRoue;

        void Start()
        {
            int i = 0;
            foreach (var h in FindObjectsByType<Habitant>(FindObjectsSortMode.None))
            {
                // Les PNJ à qui l'on parle restent libres, sauf les simples villageois du bourg en monde ouvert
                var it = h.GetComponent<Interlocuteur>();
                bool simple = MondeRPG.Actif && it != null && it.id == "villageois" && Vector2.Distance(new Vector2(h.transform.position.x, h.transform.position.z), Monde.Village) < 90f;
                if ((it != null && !simple) || h.GetComponent<Ouvrier>() != null || !h.promeneur) continue;
                var v = new Villageois { h = h, maison = new Vector2(h.transform.position.x, h.transform.position.z), hasard = Monde.HashPublic(i, 77) };
                v.champ = PointDeChamp(i);
                gens.Add(v);
                i++;
            }
        }

        static Vector2 PointDeChamp(int i)
        {
            for (int k = 0; k < 40; k++)
            {
                float x = 74f + Monde.HashPublic(i, k * 3.1f) * 64f, z = -24f + Monde.HashPublic(k * 1.7f, i) * 86f;
                if (Monde.DansChamp(x, z)) return new Vector2(x, z);
            }
            return Monde.Village + new Vector2(30, 10);
        }

        /// <summary>En monde ouvert, le bourg est déjà revenu à la vie (le scénario du pont n'a plus cours).</summary>
        static bool A(string cle) => MondeRPG.Actif || Progression.A(cle);

        /// <summary>Part du village dehors : elle grandit avec le chantier.</summary>
        static float Animation()
        {
            if (MondeRPG.Actif) return .85f;
            if (Progression.A("pont_ouvert")) return 1f;
            if (Progression.A("pont_etape2")) return .9f;
            if (Progression.A("pont_etape1")) return .75f;
            if (Progression.A("four_repare")) return .6f;
            return .45f;
        }

        void Update()
        {
            float h = MondeRPG.Actif ? Horloge.Heure : Chantier.Heure;
            bool nuit = (MondeRPG.Actif ? Horloge.EstNuit : Chantier.EstNuit) || BatailleDuGue.EnCours;   // pendant la bataille, chacun reste chez soi
            float anime = Animation();

            foreach (var v in gens)
            {
                if (v.h == null) continue;
                if (v.rendus == null || v.rendus.Length == 0) v.rendus = v.h.GetComponentsInChildren<Renderer>();
                bool sort = !nuit && v.hasard < anime;
                Vector2 lieu; float rayon;
                if (nuit || !sort) { lieu = v.maison; rayon = .5f; }
                else if (h < 11f) { lieu = v.champ; rayon = 7f; }
                else if (h < 14f) { lieu = marche != null ? new Vector2(marche.position.x, marche.position.z) : Monde.Village; rayon = 6f; }
                else if (h < 18f) { lieu = v.champ; rayon = 7f; }
                else { lieu = auberge != null ? new Vector2(auberge.position.x, auberge.position.z) : Monde.Village; rayon = 5f; }
                if ((v.h.centre - lieu).sqrMagnitude > .01f) { v.h.centre = lieu; v.h.rayon = rayon; }

                // Rentré chez soi : on ne le voit plus
                bool chezSoi = (nuit || !sort) && Vector2.Distance(new Vector2(v.h.transform.position.x, v.h.transform.position.z), v.maison) < 3f;
                if (v.dehors == chezSoi)
                {
                    v.dehors = !chezSoi;
                    foreach (var r in v.rendus) if (r != null) r.enabled = v.dehors;
                }
            }

            // Étals : se garnissent étape par étape
            if (marchandises != null)
            {
                int pleins = A("pont_ouvert") ? marchandises.Length : A("pont_etape2") ? 2 : A("pont_etape1") ? 1 : 0;
                for (int k = 0; k < marchandises.Length; k++)
                    if (marchandises[k] != null && marchandises[k].gameObject.activeSelf != k < pleins) marchandises[k].gameObject.SetActive(k < pleins);
            }

            // Le moulin redémarre à la deuxième étape du pont
            float cible = A("pont_etape2") ? 40f : 0f;
            vitesseRoue = Mathf.MoveTowards(vitesseRoue, cible, Time.deltaTime * 8f);
            if (roueMoulin != null) roueMoulin.Rotate(vitesseRoue * Time.deltaTime, 0, 0, Space.Self);

            if (fanions != null && fanions.activeSelf != Progression.A("pont_ouvert")) fanions.SetActive(Progression.A("pont_ouvert"));
            if (lumieresNuit != null)
                foreach (var l in lumieresNuit) if (l != null && l.activeSelf != (SoleilNuages.Nuit > .3f)) l.SetActive(SoleilNuages.Nuit > .3f);
            bool feu = !nuit && A("four_repare");
            if (forgeron != null && !BatailleDuGue.Mobilises.Contains(forgeron))
            {
                forgeron.geste = feu ? Geste.Marteau : Geste.Aucun;
                forgeron.regarder = enclume != null ? enclume.position : (Vector3?)null;
            }
            if (fumeeForge != null)
            {
                if (feu && !fumeeForge.isPlaying) fumeeForge.Play();
                if (!feu && fumeeForge.isPlaying) fumeeForge.Stop();
            }
        }
    }
}
