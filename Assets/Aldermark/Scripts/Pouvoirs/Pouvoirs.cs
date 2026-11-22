using UnityEngine;
using UnityEngine.InputSystem;

namespace Aldermark
{
    /// <summary>
    /// Pouvoirs d'Aldric au-delà du Sceau :
    /// - Présence détectable (faible, moyenne, élevée) : le Sceau la fait monter, le Voile la fait baisser ;
    /// - Voile du Registre (C) : masque son propre flux, 1 mana par heure de jeu, ralentit la recharge ;
    /// - Siphon (R maintenu) : vide une source de mana visée à moins de 30 m, comme on ferme une vanne.
    /// </summary>
    [RequireComponent(typeof(JoueurAldric))]
    public class Pouvoirs : MonoBehaviour
    {
        public static float Presence { get; private set; } = .5f;
        public static bool VoileActif { get; private set; }
        public static bool SiphonActif { get; private set; }
        /// <summary>Vrai pendant l'absorption du nœud : le mana ne se recharge pas normalement.</summary>
        public static bool Absorbe { get; set; }
        public static SourceMana Cible { get; private set; }
        public const float PorteeSiphon = 30f;

        public static string NomPresence => Presence < .34f ? "faible" : Presence < .67f ? "moyenne" : "élevée";
        public static bool VoileDebloque => Progression.A("voile");
        public static bool SiphonDebloque => Progression.A("siphon");

        JoueurAldric j;
        LineRenderer fil;
        float rupture;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reinitialiser() { Presence = .5f; VoileActif = false; SiphonActif = false; Absorbe = false; Cible = null; }

        /// <summary>Le Voile se déchire un instant (Surcharge) : un pic de présence visible de loin.</summary>
        public static void Dechirer(float secondes)
        {
            var p = FindFirstObjectByType<Pouvoirs>();
            if (p != null) p.rupture = secondes;
            Presence = 1f;
        }

        void Start()
        {
            j = GetComponent<JoueurAldric>();
            int couche = LayerMask.NameToLayer("Sceau");
            var go = new GameObject("FilSiphon") { layer = couche < 0 ? 0 : couche };
            go.transform.SetParent(transform, false);
            fil = go.AddComponent<LineRenderer>();
            fil.positionCount = 12; fil.useWorldSpace = true; fil.widthMultiplier = .12f;
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", new Color(.45f, .7f, 1f));
            fil.sharedMaterial = m;
            fil.enabled = false;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var kb = Keyboard.current; var pad = Gamepad.current;
            bool fige = Dialogues.EnCours || HUD.TableauOuvert || Prologue.EnCours;

            // Voile (C)
            if (VoileDebloque && !fige && ((kb != null && kb.cKey.wasPressedThisFrame) || (pad != null && pad.leftStickButton.wasPressedThisFrame && pad.rightStickButton.isPressed)))
            {
                VoileActif = !VoileActif;
                Sons.Jouer(VoileActif ? "sceauOff" : "sceauOn", .5f, .7f);
                HUD.Message(VoileActif ? "VOILE DU REGISTRE" : "VOILE LEVÉ", VoileActif ? "Ton propre flux s'efface. Ceux qui cherchent le Sceau ne te sentent plus." : "Ta présence redevient perceptible.", 2.5f);
            }
            if (VoileActif)
            {
                // 1 mana par heure de jeu (une journée de 14 h dure 3 minutes)
                if (!j.Depenser(dt * 14f / Chantier.JourDuree)) { VoileActif = false; HUD.Message("VOILE ROMPU", "Plus assez de mana pour te cacher.", 2.5f); }
            }
            if (rupture > 0) rupture -= dt;

            // Présence : le Sceau la fait monter, le Voile la fait chuter, sinon elle revient à « moyenne »
            float cible = rupture > 0 ? 1f : VoileActif ? .12f : j.SceauActif ? .85f : .5f;
            Presence = Mathf.MoveTowards(Presence, cible, dt * (VoileActif ? .5f : .08f));

            // Siphon (R maintenu)
            bool tenir = SiphonDebloque && !fige && ((kb != null && kb.rKey.isPressed) || (pad != null && pad.rightTrigger.isPressed));
            Cible = tenir ? Viser() : null;
            SiphonActif = Cible != null;
            if (SiphonActif)
            {
                float gain = Cible.Tirer(dt);
                j.FixerMana(Mathf.Min(j.manaMax, j.Mana + gain) - .4f * dt);
                if (Cible.Vide) HUD.Message("SIPHON", $"{Cible.nom} : vidé.", 2.5f);
            }
            fil.enabled = SiphonActif;
            if (SiphonActif)
            {
                Vector3 a = Cible.Point, b = j.cam.transform.position + j.cam.transform.forward * .6f - j.cam.transform.up * .3f;
                for (int i = 0; i < fil.positionCount; i++)
                {
                    float t = i / (fil.positionCount - 1f);
                    fil.SetPosition(i, Vector3.Lerp(a, b, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * .6f + Random.insideUnitSphere * .05f);
                }
            }
            else if (tenir && kb != null && kb.rKey.wasPressedThisFrame) HUD.Invite("Siphon : vise une source de mana à moins de 30 m (le Sceau les montre)");
        }

        SourceMana Viser()
        {
            SourceMana meilleur = null;
            float best = 9f;
            var cam = j.cam.transform;
            foreach (var s in SourceMana.Toutes)
            {
                if (s.Vide && !s.siphonnee) continue;
                Vector3 d = s.Point - cam.position;
                if (d.magnitude > PorteeSiphon) continue;
                float a = Vector3.Angle(cam.forward, d);
                if (a < best) { best = a; meilleur = s; }
            }
            if (Cible != null && meilleur != Cible && Cible.enabled)
            {
                // on garde la prise tant qu'on vise à peu près la même cible
                Vector3 d = Cible.Point - cam.position;
                if (d.magnitude < PorteeSiphon && Vector3.Angle(cam.forward, d) < 16f) return Cible;
            }
            return meilleur;
        }
    }
}
