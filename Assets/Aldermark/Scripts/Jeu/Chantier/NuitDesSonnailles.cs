using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Aldermark
{
    /// <summary>
    /// La nuit des sonnailles : une fois le four relancé, le soir venu, Odon donne 6 sonnailles.
    /// Le joueur les pose (touche T) sur le chemin des saboteurs, que le Sceau montre (de chez Corvin au four).
    /// La nuit, trois saboteurs viennent : une sonnaille sur leur chemin, et ils sont pris (dont Tobin) ;
    /// sinon, ils volent 10 poutres. Pas de combat : un défi de placement.
    /// </summary>
    public class NuitDesSonnailles : MonoBehaviour
    {
        public const int Nombre = 6;
        public static int EnMain { get; private set; }
        const float Portee = 3.5f;

        readonly List<Vector3> posees = new List<Vector3>();
        readonly List<Habitant> saboteurs = new List<Habitant>();
        readonly List<int> etat = new List<int>();   // 0 vers le four, 1 retour, 2 pris
        LineRenderer piste;
        bool nuitLancee, pris;
        JoueurAldric joueur;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reinitialiser() => EnMain = 0;

        static Vector2 Depart => Monde.Corvin + new Vector2(-2f, -4f);
        static Vector2 Arrivee => Monde.Four + (Monde.Village - Monde.Four).normalized * 5f;

        void Start()
        {
            joueur = FindFirstObjectByType<JoueurAldric>();
            Chantier.NouveauJour += Aube;
            // Le chemin des saboteurs, visible dans la vue du Sceau pendant cette quête
            int couche = LayerMask.NameToLayer("Sceau");
            var go = new GameObject("PisteSaboteurs") { layer = couche < 0 ? 0 : couche };
            go.transform.SetParent(transform, false);
            piste = go.AddComponent<LineRenderer>();
            piste.useWorldSpace = true; piste.positionCount = 30; piste.widthMultiplier = .5f;
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", new Color(1f, .95f, .75f));
            piste.sharedMaterial = m;
            for (int i = 0; i < 30; i++)
            {
                var p = Vector2.Lerp(Depart, Arrivee, i / 29f);
                piste.SetPosition(i, new Vector3(p.x, Mathf.Max(Monde.Hauteur(p.x, p.y), -.1f) + .4f, p.y));
            }
            piste.enabled = false;
        }

        void OnDestroy() => Chantier.NouveauJour -= Aube;

        bool Phase => Progression.A("sonnailles_donnees") && !Progression.A("sonnailles_resolues");

        void Update()
        {
            // Le soir, une fois le four relancé : Odon donne les sonnailles
            if (Chantier.Actif && Chantier.FourRepare && !Progression.A("sonnailles_donnees") && Chantier.Heure >= 16.5f && !Chantier.EstNuit)
            {
                EnMain = Nombre;
                Progression.Mettre("sonnailles_donnees");
                HUD.Message("ODON — LES SONNAILLES",
                    "« Le four refume : ils vont revenir cette nuit, j'en suis sûr. Voilà six sonnailles, des cloches et du fil.\nPose-les là où ils passeront. Ton œil d'argent doit voir leur chemin. »\n\nT : poser une sonnaille · E : le Sceau montre leur passage", 10f);
            }
            piste.enabled = Phase;
            if (!Phase) return;

            // Poser une sonnaille devant soi
            var kb = Keyboard.current; var pad = Gamepad.current;
            bool poser = (kb != null && kb.tKey.wasPressedThisFrame) || (pad != null && pad.rightShoulder.wasPressedThisFrame);
            if (poser && EnMain > 0 && joueur != null && !Dialogues.EnCours && !HUD.TableauOuvert) Poser();

            // La nuit, si le joueur veille, les saboteurs viennent vraiment
            if (Chantier.EstNuit && !nuitLancee) LancerNuit();
            if (nuitLancee) Saboteurs();
        }

        void Poser()
        {
            var cam = joueur.cam.transform;
            Vector3 point = joueur.transform.position + joueur.transform.forward * 2.5f;
            if (Physics.Raycast(cam.position, cam.forward, out var hit, 12f, ~0, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(joueur.transform))
                point = hit.point;
            point.y = Mathf.Max(Monde.Hauteur(point.x, point.z), -.1f);
            Vector3 cote = joueur.transform.right;
            var s = new GameObject("Sonnaille").transform;
            s.SetParent(transform, false);
            s.position = point;
            Matiere(s, PrimitiveType.Cube, point - cote * 1.5f + Vector3.up * .45f, new Vector3(.08f, .9f, .08f), new Color(.4f, .28f, .17f));
            Matiere(s, PrimitiveType.Cube, point + cote * 1.5f + Vector3.up * .45f, new Vector3(.08f, .9f, .08f), new Color(.4f, .28f, .17f));
            var fil = Matiere(s, PrimitiveType.Cube, point + Vector3.up * .5f, new Vector3(.02f, .02f, 3f), new Color(.85f, .82f, .7f));
            fil.rotation = Quaternion.LookRotation(cote);
            for (int k = -1; k <= 1; k++)
                Matiere(s, PrimitiveType.Cube, point + cote * (k * .8f) + Vector3.up * .38f, Vector3.one * .14f, new Color(.85f, .7f, .25f));
            posees.Add(point);
            EnMain--;
            Sons.JouerA("decouverte", point, .4f, 2f);
            bool bon = SurLeChemin(point);
            HUD.Message("SONNAILLE POSÉE", $"Il en reste {EnMain}." + (EnMain == 0 ? "\nVa dormir chez Odon… ou veille pour voir." : ""), 2.5f);
            if (bon && !Progression.A("sonnaille_bien_placee")) Progression.Mettre("sonnaille_bien_placee");
        }

        static Transform Matiere(Transform parent, PrimitiveType t, Vector3 pos, Vector3 taille, Color c)
        {
            var g = GameObject.CreatePrimitive(t);
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, true);
            g.transform.position = pos;
            g.transform.localScale = taille;
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", 0f);
            g.GetComponent<MeshRenderer>().sharedMaterial = m;
            return g.transform;
        }

        static bool SurLeChemin(Vector3 p)
        {
            Vector2 q = new Vector2(p.x, p.z), a = Depart, b = Arrivee, ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(q, a + t * ab) < Portee;
        }

        bool PiegeTendu()
        {
            foreach (var p in posees) if (SurLeChemin(p)) return true;
            return false;
        }

        // ---------- la nuit, si le joueur veille ----------

        void LancerNuit()
        {
            nuitLancee = true;
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject(i == 0 ? "Saboteur_Tobin" : "Saboteur");
                go.transform.position = new Vector3(Depart.x + i * 1.2f, Monde.Hauteur(Depart.x, Depart.y), Depart.y - i);
                var h = go.AddComponent<Habitant>();
                h.tunique = new Color(.12f, .12f, .14f); h.cheveux = new Color(.08f, .08f, .08f); h.promeneur = false;
                saboteurs.Add(h); etat.Add(0);
            }
        }

        void Saboteurs()
        {
            for (int i = 0; i < saboteurs.Count; i++)
            {
                var h = saboteurs[i];
                if (h == null || etat[i] == 2) continue;
                Vector3 p = h.transform.position;
                // Une sonnaille tout près : elle sonne, ils sont pris
                foreach (var s in posees)
                    if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(s.x, s.z)) < Portee)
                    {
                        if (!pris)
                        {
                            pris = true;
                            Sons.JouerA("decouverte", s, 1f, 1.9f);
                            Sons.JouerA("decouverte", s, 1f, 2.3f);
                            HUD.Message("LES SONNAILLES SONNENT !", "Des cris dans la nuit, des torches qui s'allument : le village leur tombe dessus.", 5f);
                        }
                    }
                if (pris) { etat[i] = 2; h.parle = true; continue; }
                Vector2 but = etat[i] == 0 ? Arrivee + new Vector2(i, 0) : Depart;
                h.AllerVers(but, 1.7f);
                if (Vector2.Distance(new Vector2(p.x, p.z), but) < .8f)
                {
                    if (etat[i] == 0) etat[i] = 1;
                    else { Destroy(h.gameObject); saboteurs[i] = null; }
                }
            }
        }

        // ---------- à l'aube ----------

        void Aube()
        {
            if (!Progression.A("sonnailles_donnees") || Progression.A("sonnailles_resolues")) return;
            bool reussi = pris || PiegeTendu();
            foreach (var h in saboteurs) if (h != null) Destroy(h.gameObject);
            saboteurs.Clear(); etat.Clear();
            EnMain = 0;
            Progression.Mettre("sonnailles_resolues");
            if (reussi)
            {
                Progression.Mettre("saboteurs_captures");
                Progression.AjouterConfiance(1);
                Prisonniers();
                HUD.Message("LA NUIT DES SONNAILLES",
                    "Les sonnailles ont tinté au cœur de la nuit. Trois hommes ont été pris près du four.\nParmi eux : Tobin, le frère de Bram. Ils attendent, ligotés, devant le four.", 8f);
            }
            else
            {
                var c = Poste.Trouver("chantier");
                float vole = 0;
                foreach (var p in new[] { c, Poste.Trouver("foret"), Poste.Trouver("four") })
                    if (p != null && vole < 10) vole += p.Prendre(Ressource.Bois, 10 - vole);
                HUD.Message("LA NUIT DES SONNAILLES",
                    $"Aucune sonnaille n'a tinté. Au matin, {vole:0} poutres ont disparu, et des traces de bottes filent vers chez Corvin.\nLe pont prend du retard.", 8f);
            }
        }

        void Prisonniers()
        {
            Vector2 c = Arrivee + new Vector2(2f, 3f);
            string[] noms = { "Tobin", "Homme de Corvin", "Homme de Corvin" };
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject(i == 0 ? "Tobin" : "Prisonnier");
                go.transform.position = new Vector3(c.x + i * 1.4f, Monde.Hauteur(c.x + i * 1.4f, c.y), c.y);
                go.transform.rotation = Quaternion.LookRotation(new Vector3(Monde.Village.x - c.x, 0, Monde.Village.y - c.y));
                var h = go.AddComponent<Habitant>();
                h.tunique = i == 0 ? new Color(.35f, .3f, .25f) : new Color(.15f, .15f, .17f);
                h.cheveux = new Color(.25f, .18f, .12f); h.promeneur = false;
                var it = go.AddComponent<Interlocuteur>();
                it.id = i == 0 ? "tobin" : "prisonnier"; it.nom = noms[i];
                if (i == 0)
                {
                    var o = go.AddComponent<Ouvrier>();
                    o.nom = "Tobin"; o.requis = "tobin_rallie"; o.porte = true; o.force = 1f;
                }
                // La corde
                Matiere(go.transform, PrimitiveType.Cube, go.transform.position + Vector3.up * .85f, new Vector3(.5f, .08f, .32f), new Color(.6f, .5f, .3f)).name = "Corde";
            }
        }
    }
}
