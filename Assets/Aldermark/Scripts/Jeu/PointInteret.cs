using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// Point d'intérêt du Sceau : en le fixant avec le Sceau actif pendant 1,5 s,
    /// à portée, Aldric le découvre (fenêtre Système, Éveil, drapeau d'histoire).
    /// </summary>
    [RequireComponent(typeof(EtiquetteSceau))]
    public class PointInteret : MonoBehaviour
    {
        public string titre = "Indice";
        [TextArea(3, 8)] public string description = "";
        public string etiquette = "Indice";
        public string drapeau = "";
        public string requis = "";
        public float eveil;
        public int confiance;
        public string cible = "";
        public float porteeMax = 18f;
        public float rayon = 3f;
        public bool decouvert;

        public static readonly List<PointInteret> Tous = new List<PointInteret>();
        EtiquetteSceau etiq;
        Transform repere;
        LineRenderer onde;
        float ageOnde = 1f;
        Material matRepere;

        public bool Disponible => !decouvert && (string.IsNullOrEmpty(requis) || Progression.A(requis));

        void Awake() => etiq = GetComponent<EtiquetteSceau>();

        void Start()
        {
            // Repère lumineux visible seulement dans la vue du Sceau (calque « Sceau »)
            int couche = LayerMask.NameToLayer("Sceau");
            if (couche < 0) return;
            matRepere = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            matRepere.SetColor("_BaseColor", etiq.couleur);
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(g.GetComponent<Collider>());
            g.name = "RepereSceau"; g.layer = couche;
            g.transform.SetParent(transform, false);
            g.transform.localRotation = Quaternion.Euler(45f, 0, 45f);
            g.GetComponent<MeshRenderer>().sharedMaterial = matRepere;
            repere = g.transform;

            var o = new GameObject("Onde") { layer = couche };
            o.transform.SetParent(transform, false);
            onde = o.AddComponent<LineRenderer>();
            onde.loop = true; onde.positionCount = 32; onde.useWorldSpace = true; onde.widthMultiplier = .25f;
            onde.sharedMaterial = new Material(matRepere);
            onde.enabled = false;
        }
        void OnEnable() => Tous.Add(this);
        void OnDisable() => Tous.Remove(this);

        void Update()
        {
            etiq.texte = decouvert ? etiquette : (Disponible ? "? — à analyser" : "");
            if (repere != null)
            {
                repere.gameObject.SetActive(Disponible);
                repere.localScale = Vector3.one * (.5f + .15f * Mathf.Sin(Time.time * 3f + transform.position.x));
                repere.Rotate(0, 60f * Time.deltaTime, 0, Space.World);
            }
            if (onde != null && ageOnde < 1f)
            {
                ageOnde = Mathf.Min(1f, ageOnde + Time.deltaTime * .8f);
                float r = .5f + ageOnde * 12f;
                for (int k = 0; k < onde.positionCount; k++)
                {
                    float a = k * Mathf.PI * 2f / onde.positionCount;
                    onde.SetPosition(k, transform.position + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r));
                }
                onde.sharedMaterial.SetColor("_BaseColor", etiq.couleur * (1f - ageOnde));
                onde.enabled = ageOnde < 1f;
            }
        }

        public void Decouvrir()
        {
            if (decouvert) return;
            decouvert = true;
            if (eveil > 0) Progression.AjouterEveil(eveil);
            Progression.AjouterIndice(titre);
            if (confiance != 0) Progression.AjouterConfiance(confiance);
            Progression.Mettre(drapeau);
            ageOnde = 0f;
            Sons.Jouer("decouverte", .8f);
            HUD.Impulsion();
            HUD.Message(titre.ToUpperInvariant(),
                description + (eveil > 0 ? $"\n\nÉveil du Sceau : {Progression.Eveil:0.#} %" : ""), 7f);
        }
    }
}
