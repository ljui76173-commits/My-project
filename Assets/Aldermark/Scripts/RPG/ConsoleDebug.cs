using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Aldermark
{
    /// <summary>
    /// Console de test (touche ² ou F1) : donner de l'XP, changer l'heure, se téléporter, sauvegarder…
    /// Tape « aide » pour la liste des commandes.
    /// </summary>
    public class ConsoleDebug : MonoBehaviour
    {
        public static bool Ouverte { get; private set; }
        /// <summary>L'image où la console a été fermée par Échap (pour ne pas ouvrir les Réglages dans la foulée).</summary>
        public static int ImageFermeture { get; private set; } = -1;
        static ConsoleDebug instance;

        string saisie = "";
        readonly List<string> historique = new List<string>();
        readonly List<string> commandesPassees = new List<string>();
        int rappel = -1;
        bool ignorerProchain;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reinitialiser() { Ouverte = false; instance = null; }

        void Awake() { instance = this; Ecrire("Console d'Aldermark. Tape « aide »."); }

        void OnEnable() { if (Keyboard.current != null) Keyboard.current.onTextInput += Caractere; }
        void OnDisable() { if (Keyboard.current != null) Keyboard.current.onTextInput -= Caractere; }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.backquoteKey.wasPressedThisFrame || kb.f1Key.wasPressedThisFrame)
            {
                Ouverte = !Ouverte;
                ignorerProchain = true;
                saisie = "";
                Cursor.lockState = Ouverte ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = Ouverte;
            }
            if (!Ouverte) { HUD.Console(null, null); return; }
            if (kb.escapeKey.wasPressedThisFrame) { Ouverte = false; ImageFermeture = Time.frameCount; HUD.Console(null, null); return; }
            if (kb.backspaceKey.wasPressedThisFrame && saisie.Length > 0) saisie = saisie.Substring(0, saisie.Length - 1);
            if (kb.upArrowKey.wasPressedThisFrame && commandesPassees.Count > 0)
            {
                rappel = rappel < 0 ? commandesPassees.Count - 1 : Mathf.Max(0, rappel - 1);
                saisie = commandesPassees[rappel];
            }
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            {
                string c = saisie.Trim();
                saisie = ""; rappel = -1;
                if (c.Length > 0) { commandesPassees.Add(c); Ecrire("> " + c); Executer(c); }
            }
            HUD.Console(string.Join("\n", historique), "> " + saisie + (Time.unscaledTime % 1f < .5f ? "_" : ""));
        }

        void Caractere(char c)
        {
            if (!Ouverte) return;
            if (ignorerProchain) { ignorerProchain = false; if (c == '²' || c == '`' || c == '~') return; }
            if (c == '\b' || c == '\r' || c == '\n' || char.IsControl(c)) return;
            if (c == '²' || c == '`') return;
            if (saisie.Length < 80) saisie += c;
        }

        void Ecrire(string ligne)
        {
            historique.Add(ligne);
            while (historique.Count > 14) historique.RemoveAt(0);
        }

        /// <summary>Le grand monde : le repaire le plus proche d'une espèce, une ville par son nom, ou un lieu de la carte.</summary>
        static bool CibleGrandMonde(string nom, Vector3 depuis, out Vector2 cible)
        {
            cible = Vector2.zero;
            string n = Simplifier(nom);
            int espece = n.StartsWith("gelee") ? 0 : n.StartsWith("loup") ? 1 : n.StartsWith("gobelin") ? 2 : n.StartsWith("troll") ? 3 : -1;
            if (espece >= 0)
            {
                Repaire meilleur = null; float md = float.MaxValue;
                foreach (var r in FindObjectsByType<Repaire>(FindObjectsSortMode.None))
                {
                    if ((int)r.espece != espece) continue;
                    float d = (r.transform.position - depuis).sqrMagnitude;
                    if (d < md) { md = d; meilleur = r; }
                }
                if (meilleur == null) return false;
                cible = new Vector2(meilleur.transform.position.x, meilleur.transform.position.z - 18f);
                return true;
            }
            foreach (var s in Geo.Sites)
                if (Simplifier(s.nom).StartsWith(n)) { cible = s.position + new Vector2(0f, -8f); return true; }
            foreach (var l in Lieu.Tous)
                if (Simplifier(l.nom).Contains(n)) { cible = new Vector2(l.Position.x, l.Position.z - 6f); return true; }
            return false;
        }

        static string Simplifier(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in s.Normalize(System.Text.NormalizationForm.FormD))
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        /// <summary>Une compétence par son identifiant ou son nom (sans accents ni espaces : « eclatdeglace », « glace »).</summary>
        static DefCompetence TrouverCompetence(string texte)
        {
            string t = Simplifier(texte);
            foreach (var d in CatalogueCompetences.Ordre)
                if (Simplifier(d.id) == t || Simplifier(d.nom) == t) return d;
            foreach (var d in CatalogueCompetences.Ordre)
                if (Simplifier(d.nom).Contains(t)) return d;
            return null;
        }

        static bool Nombre(string s, out double v) => double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v);

        void Executer(string ligne)
        {
            var m = ligne.ToLowerInvariant().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            var s = StatsHeros.Instance;
            var j = FindFirstObjectByType<JoueurAldric>();
            double v;
            switch (m[0])
            {
                case "aide":
                case "help":
                    Ecrire("xp N · niveau N · points N · stat FOR N · soigner · heure H · vitesse X");
                    Ecrire(Geo.Actif ? "tp hameau · tp quintaine · tp <ville> (varenne, aldenne, valdor…) · tp gelees|loups|gobelins|troll (le plus proche) · tp X Z"
                                     : "tp hameau · tp quintaine · tp gelees · tp loups · tp gobelins · tp troll · tp varenne · tp X Z");
                    Ecrire("creer gelee|loup|gobelin|troll [niveau] · tuer · donner <objet> [n] · objets · sauver · charger · nouvelle · infos");
                    Ecrire("sorts · apprendre <sort>|tout · competence <id>|tout <niveau> · mana");
                    break;
                case "sorts":
                case "competences":
                    {
                        var c = Competences.Instance;
                        var l = new System.Text.StringBuilder();
                        foreach (var d in CatalogueCompetences.Ordre)
                            l.Append(d.id).Append(c != null && c.Connait(d.id) ? $" (niv. {c.Niveau(d.id)})" : "").Append(" · ");
                        Ecrire(l.ToString().TrimEnd(' ', '·'));
                    }
                    break;
                case "apprendre":
                    {
                        var c = Competences.Instance;
                        if (c == null) { Ecrire("Pas de héros RPG."); break; }
                        if (m.Length < 2) { Ecrire("Usage : apprendre flamme (ou : apprendre tout) · « sorts » pour la liste"); break; }
                        if (m[1] == "tout")
                        {
                            int n = 0;
                            foreach (var d in CatalogueCompetences.Ordre) if (c.Apprendre(d.id, false)) n++;
                            Ecrire($"{n} compétence(s) apprise(s).");
                            break;
                        }
                        var def = TrouverCompetence(m[1]);
                        if (def == null) Ecrire("Inconnue. Tape « sorts » pour la liste.");
                        else if (!c.Apprendre(def.id, true)) Ecrire($"{def.nom} : déjà connue.");
                        else Ecrire($"{def.nom} apprise.");
                    }
                    break;
                case "competence":
                    {
                        var c = Competences.Instance;
                        if (c != null && m.Length > 2 && m[1] == "tout" && Nombre(m[2], out v))
                        {
                            foreach (var d in CatalogueCompetences.Ordre) if (d.EstSort && c.Connait(d.id)) c.FixerNiveau(d.id, (int)v);
                            Ecrire($"Tous tes sorts : niveau {Mathf.Clamp((int)v, 1, CatalogueCompetences.NiveauLimite)}");
                            break;
                        }
                        var def = m.Length > 1 ? TrouverCompetence(m[1]) : null;
                        if (c == null || def == null || m.Length < 3 || !Nombre(m[2], out v)) { Ecrire("Usage : competence flamme 10"); break; }
                        c.FixerNiveau(def.id, (int)v);
                        Ecrire($"{def.nom} : niveau {c.Niveau(def.id)}");
                    }
                    break;
                case "mana":
                    if (j != null) { j.FixerMana(j.manaMax); Ecrire("Mana au maximum."); }
                    break;
                case "xp":
                    if (s != null && m.Length > 1 && Nombre(m[1], out v)) { s.GagnerXP(v, "console"); Ecrire($"+{Nombres.Court(v)} XP"); }
                    else Ecrire("Usage : xp 500");
                    break;
                case "niveau":
                    if (s != null && m.Length > 1 && Nombre(m[1], out v)) { s.FixerNiveau((int)v); Ecrire($"Niveau {s.Niveau}"); }
                    else Ecrire("Usage : niveau 50");
                    break;
                case "points":
                    if (s != null && m.Length > 1 && Nombre(m[1], out v)) { s.AjouterPoints((int)v); Ecrire($"{s.PointsLibres} points à répartir"); }
                    break;
                case "stat":
                    if (s != null && m.Length > 2 && Nombre(m[2], out v))
                    {
                        int i = System.Array.IndexOf(StatsHeros.Abreges, m[1].ToUpperInvariant());
                        if (i < 0) { Ecrire("Stats : FOR AGI END INT ESP PER"); break; }
                        for (int k = 0; k < (int)v; k++) { s.AjouterPoints(1); s.Repartir((Stat)i); }
                        Ecrire($"{StatsHeros.Noms[i]} = {Nombres.Court(s.Valeur((Stat)i))}");
                    }
                    else Ecrire("Usage : stat FOR 10 (ajoute 10 points)");
                    break;
                case "soigner":
                    s?.Soigner(); Ecrire("Restauré.");
                    break;
                case "heure":
                    if (m.Length > 1 && Nombre(m[1], out v)) { Horloge.FixerHeure((float)v); Ecrire(Horloge.Texte); }
                    break;
                case "vitesse":
                    if (m.Length > 1 && Nombre(m[1], out v)) { Horloge.Vitesse = Mathf.Clamp((float)v, 0f, 500f); Ecrire($"Le temps passe ×{Horloge.Vitesse}"); }
                    break;
                case "tp":
                    if (j == null) break;
                    Vector2 cible;
                    if (m.Length > 1 && m[1] == "hameau") cible = Monde.Hameau + new Vector2(0, 12);
                    else if (m.Length > 1 && m[1] == "quintaine") cible = Monde.Quintaine + new Vector2(2.2f, 0);
                    else if (Geo.Actif && m.Length > 2 && Nombre(m[1], out var gx) && Nombre(m[2], out var gz)) cible = new Vector2((float)gx, (float)gz);
                    else if (Geo.Actif && m.Length > 1)
                    {
                        if (!CibleGrandMonde(string.Join(" ", m, 1, m.Length - 1), j.transform.position, out cible))
                        {
                            Ecrire("Lieu inconnu. Essaie : tp hameau · tp varenne · tp aldenne · tp gelees · tp loups · tp gobelins · tp troll · tp 100 -250");
                            break;
                        }
                    }
                    else if (m.Length > 1 && m[1] == "gelees") cible = Monde.RepaireGelees + new Vector2(0, -16);
                    else if (m.Length > 1 && m[1] == "loups") cible = Monde.RepaireLoups + new Vector2(0, -22);
                    else if (m.Length > 1 && m[1] == "gobelins") cible = Monde.CampGobelins + new Vector2(0, -22);
                    else if (m.Length > 1 && m[1] == "troll") cible = Monde.AntreTroll + new Vector2(0, 22);
                    else if (m.Length > 1 && m[1] == "varenne") cible = Monde.Village + new Vector2(0, -14);
                    else if (m.Length > 2 && Nombre(m[1], out var x) && Nombre(m[2], out var z)) cible = new Vector2((float)x, (float)z);
                    else { Ecrire("Usage : tp hameau · tp quintaine · tp gelees · tp loups · tp gobelins · tp troll · tp varenne · tp 10 -40"); break; }
                    j.Teleporter(new Vector3(cible.x, Mathf.Max(Monde.Hauteur(cible.x, cible.y), 0f) + .3f, cible.y), j.transform.eulerAngles.y);
                    Ecrire($"Téléporté en {cible.x:0}, {cible.y:0}");
                    break;
                case "creer":
                    {
                        if (j == null || m.Length < 2) { Ecrire("Usage : creer gelee|loup|gobelin|troll [niveau]"); break; }
                        Repaire.Espece e;
                        switch (m[1])
                        {
                            case "gelee": e = Repaire.Espece.Gelee; break;
                            case "loup": e = Repaire.Espece.Loup; break;
                            case "gobelin": e = Repaire.Espece.Gobelin; break;
                            case "troll": e = Repaire.Espece.Troll; break;
                            default: Ecrire("Espèces : gelee, loup, gobelin, troll"); e = (Repaire.Espece)(-1); break;
                        }
                        if ((int)e < 0) break;
                        int niv = m.Length > 2 && Nombre(m[2], out var vn) ? (int)vn : 1;
                        Vector3 p = j.transform.position + j.transform.forward * 7f;
                        p.y = Mathf.Max(Monde.Hauteur(p.x, p.z), p.y - 3f) + .3f;
                        var cree = Repaire.Creer(e, p, niv);
                        Ecrire($"{cree.nom} niveau {niv} créé devant toi.");
                    }
                    break;
                case "donner":
                    {
                        var inv = Inventaire.Instance;
                        if (inv == null || m.Length < 2 || Objets.Def(m[1]) == null) { Ecrire("Usage : donner <objet> [nombre]  ·  la liste : objets"); break; }
                        int nb = m.Length > 2 && Nombre(m[2], out var vq) ? (int)vq : 1;
                        inv.Ajouter(m[1], nb);
                        Ecrire($"{Objets.Def(m[1]).nom} ×{nb} ajouté.");
                    }
                    break;
                case "objets":
                    foreach (var d in Objets.Toutes) Ecrire($"{d.id}  ·  {d.nom}  ·  {Objets.NomCategorie(d.categorie)}  ·  {d.valeur} p.");
                    break;
                case "tuer":
                    {
                        int n = 0;
                        foreach (var mo in Monstre.Actifs.ToArray())
                            if (mo.camp == Camp.Hostile && mo.Vivant && j != null && (mo.transform.position - j.transform.position).sqrMagnitude < 40f * 40f)
                            { mo.Infliger(mo.VieMax * 10, null); n++; }
                        Ecrire($"{n} créature(s) abattue(s).");
                    }
                    break;
                case "sauver":
                    MondeRPG.Sauver(false); Ecrire("Sauvegardé : " + Sauvegarde.Chemin);
                    break;
                case "charger":
                    MondeRPG.Recharger(); Ecrire("Rechargé.");
                    break;
                case "nouvelle":
                    Sauvegarde.Effacer();
                    Ecrire("Sauvegarde effacée. Relance le jeu (Play) pour une nouvelle partie.");
                    break;
                case "infos":
                    Ecrire($"{Horloge.Texte} · {Registre.NombreEntites} entités · {Registre.Sauvegardables.Count} sauvegardables");
                    if (j != null) Ecrire($"Position {j.transform.position.x:0}, {j.transform.position.z:0}");
                    break;
                default:
                    Ecrire("Commande inconnue. Tape « aide ».");
                    break;
            }
        }
    }
}
