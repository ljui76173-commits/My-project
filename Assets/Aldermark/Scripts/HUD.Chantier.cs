using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Aldermark
{
    /// <summary>
    /// Partie « chantier » de l'interface : le tableau (Tab) pour affecter les villageois,
    /// et les panneaux au-dessus de chaque poste (stock, ouvriers, alerte rouge).
    /// </summary>
    public partial class HUD
    {
        public static bool TableauOuvert { get; private set; }

        GameObject panneauPlan;
        UnityEngine.UI.Text textePlan;
        CarteChantier carteChantier;
        UnityEngine.UI.Image voileNoir;
        float fondu, fonduDuree;

        /// <summary>Fondu au noir puis retour (sommeil, nuit des sonnailles).</summary>
        public static void Fondu(float duree)
        {
            if (instance == null) return;
            instance.fondu = duree; instance.fonduDuree = duree;
        }
        readonly Dictionary<Poste, UnityEngine.UI.Text> etiquettesPostes = new Dictionary<Poste, UnityEngine.UI.Text>();

        const string Ambre = "#ffd27a", Rouge = "#ff7a7a", Gris = "#9a98b0", Vert = "#a8e6a0";

        void ConstruireChantier()
        {
            carteChantier = gameObject.AddComponent<CarteChantier>();
            carteChantier.Construire(toile, police);

            var v = Panneau("Voile", toile, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0));
            v.offsetMin = v.offsetMax = Vector2.zero;
            voileNoir = v.GetComponent<UnityEngine.UI.Image>();

            // Panneau du Plan du Registre (à gauche)
            var pl = Panneau("PlanRegistre", toile, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(24, 40), new Vector2(660, 420), new Color(.04f, .07f, .16f, .86f));
            panneauPlan = pl.gameObject;
            textePlan = Texte("Texte", pl, "", 19, Color.white, TextAnchor.UpperLeft);
            textePlan.horizontalOverflow = HorizontalWrapMode.Wrap;
            Etirer((RectTransform)textePlan.transform, new Vector2(24, 18), new Vector2(-24, -18));
            panneauPlan.SetActive(false);
        }

        /// <summary>Affiche le panneau du Plan du Registre (null pour le cacher).</summary>
        public static void AfficherPlan(string texte)
        {
            if (instance == null || instance.panneauPlan == null) return;
            instance.panneauPlan.SetActive(texte != null && !TableauOuvert);
            if (texte != null) instance.textePlan.text = texte;
        }

        void MettreAJourChantier(Vector2 taille)
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool basculer = (kb != null && kb.tabKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame);
            if (basculer && Chantier.Actif && !Dialogues.EnCours) DefinirTableau(!TableauOuvert);
            if (TableauOuvert && kb != null && kb.escapeKey.wasPressedThisFrame) DefinirTableau(false, false);
            if (TableauOuvert && !Chantier.Actif) DefinirTableau(false);

            MettreAJourEtiquettesPostes(taille);
            if (fondu > 0) fondu -= Time.unscaledDeltaTime;
            float tf = fonduDuree > 0 ? 1f - Mathf.Clamp01(fondu / fonduDuree) : 1f;   // 0 → 1
            voileNoir.color = new Color(0, 0, 0, fondu > 0 ? (tf < .25f ? tf / .25f : tf > .7f ? (1f - tf) / .3f : 1f) : 0f);
            voileNoir.transform.SetAsLastSibling();
        }

        void DefinirTableau(bool ouvert, bool verrouiller = true)
        {
            TableauOuvert = ouvert;
            if (ouvert) carteChantier.Ouvrir(); else carteChantier.Fermer(verrouiller);
            if (panneauPlan != null && ouvert) panneauPlan.SetActive(false);
            if (panneauJournal != null && ouvert) panneauJournal.SetActive(false);
            Sons.Jouer("clic", .6f, ouvert ? 1.1f : .9f);
        }

        /// <summary>État du pont provisoire : étapes, matériaux requis / au chantier / en route, blocage en rouge.</summary>
        public static string TextePont()
        {
            var p = new StringBuilder();
            if (!Progression.A("plan_bois"))
            {
                p.AppendLine($"<color={Ambre}><b>PONT DE VARENNE</b></color>");
                p.AppendLine($"<color={Gris}>Pas encore de plan. Projette le Plan du Registre (P) sur les ruines du pont.</color>");
                return p.ToString();
            }
            p.AppendLine($"<color={Ambre}><b>PONT PROVISOIRE EN BOIS</b></color>");
            p.AppendLine($"<size=15><color={Gris}>Blocs et chaux (le mortier) pour les piles, puis le bois pour les poutres et le tablier.</color></size>");
            var c = Poste.Trouver("chantier");
            int e = Chantier.Etape;
            for (int k = 0; k < 3; k++)
            {
                var b = Chantier.Etapes[k];
                if (k < e) { p.AppendLine($"<color={Vert}>{k + 1}. {b.nom} — terminé</color>"); continue; }
                if (k > e) { p.AppendLine($"<color={Gris}>{k + 1}. {b.nom}</color>"); continue; }
                p.AppendLine($"<b>{k + 1}. {b.nom}</b>  {Barre(Chantier.Avancement[k] / b.jours)}  {Chantier.Avancement[k]:0.#}/{b.jours:0} jours-personne");
                if (c != null)
                {
                    var besoins = new List<string>();
                    if (b.pierre > 0) besoins.Add($"blocs {Mathf.Min(Mathf.Floor(c.Stock(Ressource.Pierre)), b.pierre):0}/{b.pierre:0}{EnRoute(c, Ressource.Pierre)}");
                    if (b.chaux > 0) besoins.Add($"chaux (mortier) {Mathf.Min(Mathf.Floor(c.Stock(Ressource.Chaux)), b.chaux):0}/{b.chaux:0}{EnRoute(c, Ressource.Chaux)}");
                    if (b.bois > 0) besoins.Add($"poutres {Mathf.Min(Mathf.Floor(c.Stock(Ressource.Bois)), b.bois):0}/{b.bois:0}{EnRoute(c, Ressource.Bois)}");
                    p.AppendLine("   " + string.Join("   ·   ", besoins) + $"   ·   bâtisseurs {c.personnes:0.#}");
                    if (Chantier.BlocageChantier != "") p.AppendLine($"   <color={Rouge}>⚠ {Chantier.BlocageChantier}</color>");
                }
            }
            if (e >= 3) p.AppendLine($"<color={Vert}>Le pont est ouvert !</color>");
            return p.ToString();
        }

        static string Horloge()
        {
            float h = 6f + Chantier.HeureDuJour * 14f;   // de 6 h à 20 h
            return $"{(int)h:00} h {(int)((h % 1f) * 60f):00}";
        }

        static string Barre(float f)
        {
            int n = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(f) * 10f), 0, 10);
            return $"<color={Ambre}>{new string('■', n)}</color><color={Gris}>{new string('□', 10 - n)}</color>";
        }

        /// <summary>« (+3 en route) » : ce que les porteurs sont en train d'apporter.</summary>
        static string EnRoute(Poste destination, Ressource r)
        {
            float q = 0;
            foreach (var o in Ouvrier.Tous) if (o.Livraison == destination && o.charge == r) q += o.quantite;
            return q > 0 ? $" <color={Gris}>(+{q:0} en route)</color>" : "";
        }

        /// <summary>Panneau au-dessus de chaque poste : stock, ouvriers, alerte rouge.</summary>
        void MettreAJourEtiquettesPostes(Vector2 taille)
        {
            foreach (var poste in Poste.Tous)
            {
                if (!etiquettesPostes.TryGetValue(poste, out var t))
                {
                    var r = Panneau("Poste_" + poste.id, toile, Vector2.zero, Vector2.zero, new Vector2(.5f, 0), Vector2.zero, new Vector2(260, 40), new Color(0, 0, 0, 0));
                    r.SetAsFirstSibling();
                    t = Texte("Texte", r, "", 18, Color.white, TextAnchor.LowerCenter);
                    Etirer((RectTransform)t.transform, Vector2.zero, Vector2.zero);
                    etiquettesPostes[poste] = t;
                }
                var rt = (RectTransform)t.transform.parent;
                bool vis = poste.Disponible && cam != null && !Dialogues.EnCours && !TableauOuvert;
                Vector3 vp = Vector3.zero;
                if (vis)
                {
                    vp = cam.WorldToViewportPoint(poste.transform.position + Vector3.up * 3.2f);
                    vis = vp.z > 2f && vp.z < 110f && vp.x > 0 && vp.x < 1 && vp.y > 0 && vp.y < 1;
                }
                rt.gameObject.SetActive(vis);
                if (!vis) continue;
                rt.anchoredPosition = new Vector2(vp.x * taille.x, vp.y * taille.y);
                string contenu;
                if (poste.EstChantier) contenu = Chantier.Etape >= 3 ? "Pont ouvert" : $"{Chantier.Etapes[Chantier.Etape].nom} · {Barre(Chantier.Avancement[Chantier.Etape] / Chantier.Etapes[Chantier.Etape].jours)}";
                else if (poste.EstFour && !Chantier.FourRepare) contenu = $"Réparation {Barre(Chantier.ReparationFour / Chantier.FourJours)}";
                else if (poste.EstFour) contenu = $"{Mathf.Floor(poste.Stock(Ressource.Chaux)):0} sacs de chaux";
                else contenu = $"{Mathf.Floor(poste.Stock(poste.produit)):0} {Ouvrier.NomRessource(poste.produit, 2)}";
                string alerte = poste.blocage != "" && poste.blocage != "Épuisé" ? $"\n<color={Rouge}>⚠ {poste.blocage}</color>" : "";
                t.text = $"<b>{poste.nom}</b>\n<size=15>{contenu} · {poste.personnes:0.#} au travail</size><size=15>{alerte}</size>";
                float d = vp.z;
                t.color = new Color(1, 1, 1, Mathf.Clamp01((110f - d) / 30f));
            }
        }
    }
}
