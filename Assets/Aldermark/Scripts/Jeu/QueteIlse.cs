using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// Ilse à l'auberge (scène 10) : au matin qui suit la nuit des sonnailles, une Lame grise arrive à l'auberge.
    /// Son pendentif cherche le porteur du Sceau : il le trouve si la Présence est haute, il hésite si le Voile tient.
    /// Le Voile du Registre se débloque à ce moment-là.
    /// </summary>
    public class QueteIlse : MonoBehaviour
    {
        public GameObject ilse;
        public Transform pendentif;
        float minuteur = -1f;
        Material matPendentif;
        Transform joueur;

        void Start()
        {
            if (ilse != null) ilse.SetActive(Progression.A("ilse_arrivee"));
            if (ilse != null) Lieux.Declarer("ilse", ilse.transform);
            var j = FindFirstObjectByType<JoueurAldric>();
            if (j != null) joueur = j.transform;
            if (pendentif != null)
            {
                matPendentif = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                pendentif.GetComponent<MeshRenderer>().sharedMaterial = matPendentif;
                var s = pendentif.GetComponent<SourceMana>();
                if (s != null) s.Videe += _ => { Progression.Mettre("pendentif_vide"); HUD.Message("PENDENTIF VIDÉ", "Le cristal d'Ilse s'éteint. La Tour ne pourra plus la suivre à la trace…\nmais elle, elle a senti quelque chose.", 5f); };
            }
        }

        void Update()
        {
            if (!Progression.A("ilse_arrivee") && Progression.A("sonnailles_resolues") && !Chantier.EstNuit)
            {
                Progression.Mettre("ilse_arrivee");
                if (ilse != null) ilse.SetActive(true);
                HUD.Message("LISE ACCOURT",
                    "« Monsieur ! Une dame en gris est arrivée à l'auberge. Elle a une pierre qui pend à son cou,\net elle demande s'il y a un étranger avec une marque qui brille… »", 8f);
                Sons.Voix(1.75f);
                minuteur = 8.5f;
            }
            if (minuteur > 0)
            {
                minuteur -= Time.deltaTime;
                if (minuteur <= 0)
                {
                    Progression.Mettre("voile");
                    Sons.Jouer("decouverte", .8f, 1.1f);
                    HUD.Message("NOUVELLE FONCTION DU SCEAU — VOILE DU REGISTRE",
                        "Le Sceau peut masquer ton propre flux.\nC : activer ou lever le Voile · 1 mana par heure · recharge ralentie.\nTant qu'il tient, ta Présence détectable tombe à « faible ».", 10f);
                }
            }

            // Le pendentif cherche le porteur : il brille et se tourne vers lui quand la Présence est haute
            if (pendentif != null && matPendentif != null && joueur != null && ilse != null && ilse.activeSelf)
            {
                var src = pendentif.GetComponent<SourceMana>();
                bool vide = src != null && src.Vide;
                float d = Vector3.Distance(joueur.position, pendentif.position);
                float trouve = vide ? 0f : Mathf.Clamp01((Pouvoirs.Presence - .3f) / .5f) * Mathf.Clamp01((40f - d) / 30f);
                matPendentif.SetColor("_BaseColor", vide ? new Color(.25f, .25f, .3f) : Color.Lerp(new Color(.35f, .45f, .7f), new Color(.6f, .9f, 1f), trouve + .2f * Mathf.Sin(Time.time * 6f) * trouve));
                Vector3 vers = trouve > .4f ? (joueur.position - pendentif.position) : new Vector3(Mathf.Sin(Time.time * 1.7f), 0, Mathf.Cos(Time.time * 1.3f));
                vers.y = 0;
                if (vers.sqrMagnitude > .01f) pendentif.rotation = Quaternion.Slerp(pendentif.rotation, Quaternion.LookRotation(vers), Time.deltaTime * 4f);
            }
        }
    }
}
