using UnityEngine;
using UnityEngine.Rendering;

namespace Aldermark
{
    /// <summary>
    /// Villageois low-poly construit en code. S'il est « promeneur », il marche
    /// d'un point à l'autre du village et s'arrête un moment entre deux trajets.
    /// </summary>
    /// <summary>Geste de travail d'un villageois.</summary>
    public enum Geste { Aucun, Hache, Pioche, Marteau, Ramasser }

    public class Habitant : MonoBehaviour
    {
        public Color tunique = new Color(.55f, .35f, .24f);
        public Color cheveux = new Color(.29f, .19f, .13f);
        public float taille = 1f;
        public bool promeneur = true;
        public Vector2 centre = new Vector2(55, 10);
        public float rayon = 28f;
        public bool panier;
        [System.NonSerialized] public bool parle;
        /// <summary>Geste de travail en cours (hache, pioche, marteau, ramasser).</summary>
        [System.NonSerialized] public Geste geste;
        /// <summary>Point que le villageois regarde pendant qu'il travaille.</summary>
        [System.NonSerialized] public Vector3? regarder;
        Geste outilAffiche = Geste.Aucun;
        Transform outil;
        float phaseTravail;
        bool porteDevant;
        Vector3? destination;
        float vitesseDestination = 3.2f;
        Transform charge;
        /// <summary>Vrai tant que le personnage marche vers un point donné.</summary>
        public bool EnRoute => destination.HasValue;

        Transform jambeG, jambeD, brasG, brasD;
        Vector3 cible;
        float attente, phase;
        bool enMarche;

        void Start()
        {
            transform.localScale = Vector3.one * taille;
            var lin = Lit(tunique);
            jambeG = Groupe("JambeG", new Vector3(-.1f, .52f, 0));
            jambeD = Groupe("JambeD", new Vector3(.1f, .52f, 0));
            foreach (var j in new[] { jambeG, jambeD }) Piece(j, new Vector3(0, -.25f, 0), new Vector3(.15f, .5f, .15f), Lit(new Color(.29f, .23f, .18f)));
            Piece(transform, new Vector3(0, .8f, 0), new Vector3(.44f, .58f, .26f), lin);
            Piece(transform, new Vector3(0, 1.24f, 0), new Vector3(.25f, .27f, .25f), Lit(new Color(.88f, .7f, .55f)));
            Piece(transform, new Vector3(0, 1.39f, 0), new Vector3(.27f, .09f, .27f), Lit(cheveux));
            brasG = Groupe("BrasG", new Vector3(-.28f, 1.05f, 0));
            brasD = Groupe("BrasD", new Vector3(.28f, 1.05f, 0));
            foreach (var b in new[] { brasG, brasD }) Piece(b, new Vector3(0, -.22f, 0), new Vector3(.11f, .48f, .12f), lin);
            if (panier)
            {
                var p = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.Destroy(p.GetComponent<Collider>());
                p.transform.SetParent(brasD, false);
                p.transform.localPosition = new Vector3(0, -.45f, .08f);
                p.transform.localScale = new Vector3(.3f, .08f, .3f);
                p.GetComponent<MeshRenderer>().sharedMaterial = Lit(new Color(.54f, .42f, .24f));
                brasD.localRotation = Quaternion.Euler(-15f, 0, 0);
            }
            if (promeneur) { NouvelleCible(); attente = Random.Range(0f, 4f); }
            PoserAuSol();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (destination.HasValue)
            {
                Vector3 p = transform.position, c = destination.Value;
                Vector3 d = new Vector3(c.x - p.x, 0, c.z - p.z);
                if (d.magnitude < .3f) { destination = null; enMarche = false; }
                else
                {
                    transform.position += d.normalized * Mathf.Min(vitesseDestination, d.magnitude * 2f) * dt;
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), dt * 6f);
                    enMarche = true;
                    PoserAuSol();
                }
            }
            else if (promeneur)
            {
                Vector3 p = transform.position;
                Vector3 d = new Vector3(cible.x - p.x, 0, cible.z - p.z);
                if (attente > 0) { attente -= dt; enMarche = false; }
                else if (d.magnitude < .3f) { attente = Random.Range(2f, 7f); NouvelleCible(); }
                else
                {
                    transform.position += d.normalized * 1.3f * dt;
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), dt * 6f);
                    enMarche = true;
                }
                PoserAuSol();
            }
            if (enMarche) phase += dt * 7f;
            float b = enMarche ? Mathf.Sin(phase) * 28f : 0f;
            jambeG.localRotation = Quaternion.Euler(b, 0, 0);
            jambeD.localRotation = Quaternion.Euler(-b, 0, 0);
            brasG.localRotation = Quaternion.Euler(-b * .7f, 0, 0);
            if (!panier) brasD.localRotation = Quaternion.Euler(b * .7f, 0, 0);
            // Charge portée à deux bras, devant soi
            if (porteDevant)
            {
                brasG.localRotation = Quaternion.Euler(-68f, 0, 8f);
                brasD.localRotation = Quaternion.Euler(-68f, 0, -8f);
            }
            Travailler(dt);
            if (parle && geste == Geste.Aucun && !porteDevant)
            {
                // Gestes quand il parle
                float g = Mathf.Sin(Time.time * 7f);
                brasG.localRotation = Quaternion.Euler(-35f + g * 15f, 0, -10f);
                if (!panier) brasD.localRotation = Quaternion.Euler(-20f - g * 10f, 0, 10f);
            }
        }

        /// <summary>Fait marcher le personnage jusqu'à un point, puis il s'arrête.</summary>
        public void AllerVers(Vector2 pos, float vitesse = 3.2f)
        {
            promeneur = false;
            vitesseDestination = vitesse;
            destination = new Vector3(pos.x, 0, pos.y);
        }

        /// <summary>Animation du geste de travail, avec l'outil en main et le bruit du coup.</summary>
        void Travailler(float dt)
        {
            Geste g = enMarche || porteDevant ? Geste.Aucun : geste;
            if (g != outilAffiche) ChangerOutil(g);
            if (g == Geste.Aucun) return;

            if (regarder.HasValue)
            {
                Vector3 d = regarder.Value - transform.position; d.y = 0;
                if (d.sqrMagnitude > .01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), dt * 4f);
            }

            float rythme = g == Geste.Marteau ? 1.7f : g == Geste.Ramasser ? .8f : 1.05f;
            float avant = phaseTravail;
            phaseTravail += dt * rythme;
            float t = Mathf.Repeat(phaseTravail, 1f);
            float angle;
            if (g == Geste.Ramasser)
            {
                // Se penche, ramasse, se relève
                angle = -20f - 55f * Mathf.Sin(t * Mathf.PI);
                brasG.localRotation = Quaternion.Euler(angle, 0, 6f);
                brasD.localRotation = Quaternion.Euler(angle, 0, -6f);
                return;
            }
            // Armer lentement, frapper vite
            angle = t < .7f ? Mathf.Lerp(-35f, -165f, Mathf.SmoothStep(0, 1, t / .7f)) : Mathf.Lerp(-165f, -35f, (t - .7f) / .3f);
            brasD.localRotation = Quaternion.Euler(angle, 0, -6f);
            if (g != Geste.Marteau) brasG.localRotation = Quaternion.Euler(angle + 8f, 0, 14f);   // outils à deux mains
            else brasG.localRotation = Quaternion.Euler(-40f, 0, 10f);                           // l'autre main tient la pièce

            // Le coup : un « toc » dans le monde (hache sourde, pioche et marteau plus secs)
            if (Mathf.Floor(avant) != Mathf.Floor(phaseTravail))
                Sons.JouerA(g == Geste.Hache ? "pasTerre" : "clic", transform.position + transform.forward * .8f + Vector3.up, g == Geste.Hache ? .5f : .35f,
                    g == Geste.Hache ? .55f : g == Geste.Pioche ? .75f : 1.1f);
        }

        void ChangerOutil(Geste g)
        {
            outilAffiche = g;
            if (outil != null) Destroy(outil.gameObject);
            outil = null;
            if (g == Geste.Aucun || g == Geste.Ramasser) return;
            outil = new GameObject("Outil").transform;
            outil.SetParent(brasD, false);
            outil.localPosition = new Vector3(0, -.46f, 0);
            var bois = Lit(new Color(.45f, .3f, .18f));
            var fer = Lit(new Color(.55f, .56f, .6f));
            float l = g == Geste.Marteau ? .38f : .78f;
            // Manche vers l'avant de la main, tête au bout
            PieceOutil(new Vector3(0, 0, l / 2f - .08f), new Vector3(.045f, .045f, l), bois);
            if (g == Geste.Hache) PieceOutil(new Vector3(0, -.07f, l - .1f), new Vector3(.03f, .2f, .16f), fer);
            else if (g == Geste.Pioche) PieceOutil(new Vector3(0, 0, l - .08f), new Vector3(.05f, .5f, .06f), fer);
            else PieceOutil(new Vector3(0, 0, l - .06f), new Vector3(.09f, .16f, .09f), fer);
        }

        void PieceOutil(Vector3 pos, Vector3 echelle, Material m)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(outil, false);
            g.transform.localPosition = pos;
            g.transform.localScale = echelle;
            g.GetComponent<MeshRenderer>().sharedMaterial = m;
        }

        /// <summary>Porte une charge à deux bras devant soi (couleur de la ressource), ou rien.</summary>
        public void Porter(Color? couleur)
        {
            porteDevant = couleur != null;
            if (couleur == null) { if (charge != null) charge.gameObject.SetActive(false); return; }
            if (charge == null)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(g.GetComponent<Collider>());
                g.name = "Charge";
                charge = g.transform;
                charge.SetParent(transform, false);
                charge.localPosition = new Vector3(0, .9f, .32f);
                charge.localScale = new Vector3(.46f, .3f, .34f);
            }
            charge.gameObject.SetActive(true);
            charge.GetComponent<MeshRenderer>().sharedMaterial = Lit(couleur.Value);
        }

        void PoserAuSol()
        {
            var p = transform.position;
            p.y = Mathf.Max(Monde.Hauteur(p.x, p.z), -1.2f);
            transform.position = p;
        }

        void NouvelleCible()
        {
            for (int k = 0; k < 15; k++)
            {
                Vector2 c = centre + Random.insideUnitCircle * rayon;
                var t = new Vector3(c.x, Monde.Hauteur(c.x, c.y) + 1f, c.y);
                if (!Physics.CheckSphere(t, .8f, ~0, QueryTriggerInteraction.Ignore)) { cible = t; return; }
            }
            cible = transform.position;
        }

        Transform Groupe(string nom, Vector3 pos)
        {
            var g = new GameObject(nom).transform;
            g.SetParent(transform, false);
            g.localPosition = pos;
            return g;
        }

        static void Piece(Transform parent, Vector3 pos, Vector3 echelle, Material m)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = echelle;
            var r = g.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.On;
        }

        static Material Lit(Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", .1f);
            return m;
        }
    }
}
