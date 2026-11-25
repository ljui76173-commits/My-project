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
            if (kb.escapeKey.wasPressedThisFrame) { Ouverte = false; HUD.Console(null, null); return; }
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
                    Ecrire("tp hameau · tp quintaine · tp varenne · tp X Z · sauver · charger · nouvelle · infos");
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
                    else if (m.Length > 1 && m[1] == "varenne") cible = Monde.Village + new Vector2(0, -14);
                    else if (m.Length > 2 && Nombre(m[1], out var x) && Nombre(m[2], out var z)) cible = new Vector2((float)x, (float)z);
                    else { Ecrire("Usage : tp hameau · tp quintaine · tp varenne · tp 10 -40"); break; }
                    j.Teleporter(new Vector3(cible.x, Mathf.Max(Monde.Hauteur(cible.x, cible.y), 0f) + .3f, cible.y), j.transform.eulerAngles.y);
                    Ecrire($"Téléporté en {cible.x:0}, {cible.y:0}");
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
