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
        public float porteeMax = 50f;
        public float rayon = 3f;
        public bool decouvert;

        public static readonly List<PointInteret> Tous = new List<PointInteret>();
        EtiquetteSceau etiq;

        public bool Disponible => !decouvert && (string.IsNullOrEmpty(requis) || Progression.A(requis));

        void Awake() => etiq = GetComponent<EtiquetteSceau>();
        void OnEnable() => Tous.Add(this);
        void OnDisable() => Tous.Remove(this);

        void Update()
        {
            etiq.texte = decouvert ? etiquette : (Disponible ? "? — à analyser" : "");
        }

        public void Decouvrir()
        {
            if (decouvert) return;
            decouvert = true;
            if (eveil > 0) Progression.AjouterEveil(eveil);
            Progression.AjouterIndice(titre);
            if (confiance != 0) Progression.AjouterConfiance(confiance);
            Progression.Mettre(drapeau);
            HUD.Message(titre.ToUpperInvariant(),
                description + (eveil > 0 ? $"\n\nÉveil du Sceau : {Progression.Eveil:0.#} %" : ""), 7f);
        }
    }
}
