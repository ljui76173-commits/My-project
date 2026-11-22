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

        GameObject panneauTableau;
        UnityEngine.UI.Text texteTitreTableau, texteVillageois, textePostes, texteAideTableau;
        int selection;
        readonly Dictionary<Poste, UnityEngine.UI.Text> etiquettesPostes = new Dictionary<Poste, UnityEngine.UI.Text>();

        const string Ambre = "#ffd27a", Rouge = "#ff7a7a", Gris = "#9a98b0", Vert = "#a8e6a0";

        void ConstruireChantier()
        {
            var t = Panneau("TableauChantier", toile, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(1500, 760), new Color(.055f, .047f, .12f, .94f));
            panneauTableau = t.gameObject;
            texteTitreTableau = Texte("Titre", t, "", 26, new Color(1f, .82f, .45f), TextAnchor.UpperLeft);
            texteTitreTableau.fontStyle = FontStyle.Bold;
            Etirer((RectTransform)texteTitreTableau.transform, new Vector2(36, 24), new Vector2(-36, -24));

            texteVillageois = Texte("Villageois", t, "", 20, Color.white, TextAnchor.UpperLeft);
            texteVillageois.horizontalOverflow = HorizontalWrapMode.Wrap;
            var rv = (RectTransform)texteVillageois.transform;
            rv.anchorMin = new Vector2(0, 0); rv.anchorMax = new Vector2(.55f, 1);
            rv.offsetMin = new Vector2(36, 70); rv.offsetMax = new Vector2(-12, -78);

            textePostes = Texte("Postes", t, "", 19, Color.white, TextAnchor.UpperLeft);
            textePostes.horizontalOverflow = HorizontalWrapMode.Wrap;
            var rp = (RectTransform)textePostes.transform;
            rp.anchorMin = new Vector2(.55f, 0); rp.anchorMax = new Vector2(1, 1);
            rp.offsetMin = new Vector2(12, 70); rp.offsetMax = new Vector2(-36, -78);

            texteAideTableau = Texte("Aide", t, "↑ ↓  choisir un villageois     ← →  changer son affectation     Tab  fermer     (manette : croix, Start)", 18, new Color(.8f, .8f, .95f), TextAnchor.LowerCenter);
            Etirer((RectTransform)texteAideTableau.transform, new Vector2(36, 22), new Vector2(-36, -22));
            panneauTableau.SetActive(false);
        }

        void MettreAJourChantier(Vector2 taille)
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool basculer = (kb != null && kb.tabKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame);
            if (basculer && Chantier.Actif && !Dialogues.EnCours) DefinirTableau(!TableauOuvert);
            if (TableauOuvert && kb != null && kb.escapeKey.wasPressedThisFrame) DefinirTableau(false);
            if (TableauOuvert && !Chantier.Actif) DefinirTableau(false);

            if (TableauOuvert) MettreAJourTableau(kb, pad);
            MettreAJourEtiquettesPostes(taille);
        }

        void DefinirTableau(bool ouvert)
        {
            TableauOuvert = ouvert;
            panneauTableau.SetActive(ouvert);
            if (panneauJournal != null && ouvert) panneauJournal.SetActive(false);
            Sons.Jouer("clic", .6f, ouvert ? 1.1f : .9f);
        }

        static bool Appui(Keyboard kb, Gamepad pad, Key a, Key b, System.Func<Gamepad, UnityEngine.InputSystem.Controls.ButtonControl> bouton) =>
            (kb != null && (kb[a].wasPressedThisFrame || kb[b].wasPressedThisFrame)) || (pad != null && bouton(pad).wasPressedThisFrame);

        void MettreAJourTableau(Keyboard kb, Gamepad pad)
        {
            var ouvriers = new List<Ouvrier>();
            foreach (var o in Ouvrier.Tous) if (o.Disponible) ouvriers.Add(o);
            if (ouvriers.Count == 0) return;

            // Navigation
            if (Appui(kb, pad, Key.UpArrow, Key.W, g => g.dpad.up)) { selection--; Sons.Jouer("clic", .35f, 1.3f); }
            if (Appui(kb, pad, Key.DownArrow, Key.S, g => g.dpad.down)) { selection++; Sons.Jouer("clic", .35f, 1.3f); }
            selection = (selection % ouvriers.Count + ouvriers.Count) % ouvriers.Count;
            var choisi = ouvriers[selection];
            int sens = 0;
            if (Appui(kb, pad, Key.LeftArrow, Key.A, g => g.dpad.left)) sens = -1;
            if (Appui(kb, pad, Key.RightArrow, Key.D, g => g.dpad.right)) sens = 1;
            if (sens != 0)
            {
                var options = choisi.Options();
                int i = options.FindIndex(o => o.Egale(choisi.affectation));
                i = ((i < 0 ? 0 : i) + sens + options.Count) % options.Count;
                choisi.Affecter(options[i]);
                Sons.Jouer("clic", .55f, 1f);
            }

            texteTitreTableau.text = $"TABLEAU DU CHANTIER   ·   Jour {Chantier.Jour}   ·   {Horloge()}";

            // Colonne de gauche : les villageois
            var v = new StringBuilder();
            v.AppendLine($"<color={Ambre}><b>VILLAGEOIS</b></color>  ({ouvriers.Count})").AppendLine();
            for (int k = 0; k < ouvriers.Count; k++)
            {
                var o = ouvriers[k];
                bool sel = k == selection;
                string nom = o.nom + (o.force > 1 ? " (bâtit pour deux)" : "") + (!o.porte ? " · ne porte pas" : "");
                string ligne = sel ? $"<color={Ambre}>▶ <b>{nom}</b>   ◀  {o.affectation.Nom}  ▶</color>" : $"   <b>{nom}</b>   —   {o.affectation.Nom}";
                v.AppendLine(ligne);
                string etat = o.Etat;
                bool attente = etat.StartsWith("attend") || etat.StartsWith("rien") || etat.StartsWith("il manque") || etat.StartsWith("stock");
                v.AppendLine($"     <size=16><color={(attente ? Rouge : Gris)}>{etat}</color></size>");
            }
            texteVillageois.text = v.ToString();

            // Colonne de droite : postes et pont
            var p = new StringBuilder();
            p.AppendLine($"<color={Ambre}><b>POSTES</b></color>");
            foreach (var poste in Poste.Tous)
            {
                if (poste.EstChantier) continue;
                if (!poste.Disponible) { p.AppendLine($"<color={Gris}>{poste.nom} : à découvrir avec le Sceau</color>"); continue; }
                string ligne;
                if (poste.EstFour && !Chantier.FourRepare)
                    ligne = $"<b>Four</b> — réparation {Barre(Chantier.ReparationFour / Chantier.FourJours)}  blocs {Mathf.Floor(poste.Stock(Ressource.Pierre)):0}/{Chantier.FourBlocs:0}{EnRoute(poste, Ressource.Pierre)}";
                else if (poste.EstFour)
                    ligne = $"<b>Four</b> — chaux {Mathf.Floor(poste.Stock(Ressource.Chaux)):0} · calcaire {Mathf.Floor(poste.Stock(Ressource.Calcaire)):0}{EnRoute(poste, Ressource.Calcaire)} · bois {Mathf.Floor(poste.Stock(Ressource.Bois)):0}{EnRoute(poste, Ressource.Bois)}";
                else
                {
                    string reste = poste.reserve >= 0 ? $" · reste {Mathf.Max(0, poste.reserve - poste.extrait):0}" : "";
                    ligne = $"<b>{poste.nom}</b> — {Mathf.Floor(poste.Stock(poste.produit)):0} {Ouvrier.NomRessource(poste.produit, 2)}{reste}";
                }
                ligne += $"  <color={Gris}>({poste.personnes:0.#} au travail)</color>";
                p.AppendLine(ligne);
                if (poste.blocage != "") p.AppendLine($"   <color={Rouge}>⚠ {poste.blocage}</color>");
            }

            p.AppendLine().AppendLine($"<color={Ambre}><b>PONT PROVISOIRE EN BOIS</b></color>");
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
                    if (b.chaux > 0) besoins.Add($"chaux {Mathf.Min(Mathf.Floor(c.Stock(Ressource.Chaux)), b.chaux):0}/{b.chaux:0}{EnRoute(c, Ressource.Chaux)}");
                    if (b.bois > 0) besoins.Add($"poutres {Mathf.Min(Mathf.Floor(c.Stock(Ressource.Bois)), b.bois):0}/{b.bois:0}{EnRoute(c, Ressource.Bois)}");
                    p.AppendLine("   " + string.Join("   ·   ", besoins) + $"   ·   bâtisseurs {c.personnes:0.#}");
                    if (Chantier.BlocageChantier != "") p.AppendLine($"   <color={Rouge}>⚠ {Chantier.BlocageChantier}</color>");
                }
            }
            if (e >= 3) p.AppendLine($"<color={Vert}>Le pont est ouvert !</color>");
            textePostes.text = p.ToString();
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
