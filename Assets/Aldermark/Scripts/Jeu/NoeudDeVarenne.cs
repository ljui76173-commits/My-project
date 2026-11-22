using UnityEngine;
using UnityEngine.InputSystem;

namespace Aldermark
{
    /// <summary>
    /// Le nœud de mana sous l'ancien pont (scène 11) : visible avec le Sceau.
    /// F maintenu : Aldric tire le nœud en lui ; le mana dépasse le maximum, l'écran brûle.
    /// Lâcher dans la bonne fenêtre (1,6 à 2,2 fois le maximum) donne +10 de mana maximum pour toujours ;
    /// trop tard, la rupture : −20 % de mana maximum jusqu'à l'aube. Dans tous les cas, le Siphon s'éveille
    /// et le nœud s'affaiblit (Vesk y puisera moins).
    /// </summary>
    public class NoeudDeVarenne : MonoBehaviour
    {
        public static float Force { get; private set; } = 1f;
        public Transform sphere;
        public LineRenderer[] veines;
        public ParticleSystem vapeur;

        JoueurAldric j;
        bool absorbe;
        float battement, malus;
        Material matSphere;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reinitialiser() => Force = 1f;

        void Start()
        {
            j = FindFirstObjectByType<JoueurAldric>();
            Lieux.Declarer("noeud", transform);
            if (sphere != null)
            {
                matSphere = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                sphere.GetComponent<MeshRenderer>().sharedMaterial = matSphere;
            }
            Chantier.NouveauJour += Aube;
        }

        void OnDestroy() => Chantier.NouveauJour -= Aube;

        void Aube()
        {
            if (malus <= 0 || j == null) return;
            j.manaMax += malus; malus = 0;
            HUD.Message("CHOC DE SURCHARGE DISSIPÉ", $"Mana maximum : {j.manaMax:0}.", 3f);
        }

        void Update()
        {
            float pulse = .5f + .5f * Mathf.Sin(Time.time * 2.2f);
            if (sphere != null) sphere.localScale = Vector3.one * (10f + pulse * .8f) * Mathf.Lerp(.7f, 1f, Force);
            if (matSphere != null) matSphere.SetColor("_BaseColor", Color.Lerp(new Color(.25f, .45f, 1f), new Color(.7f, .85f, 1f), absorbe ? 1f : pulse * .4f) * Mathf.Lerp(.6f, 1f, Force));
            if (j == null) return;

            var kb = Keyboard.current; var pad = Gamepad.current;
            Vector3 d = transform.position - j.cam.transform.position;
            bool vise = j.SceauNiveau > .5f && d.magnitude < 22f && Vector3.Angle(j.cam.transform.forward, d) < 28f && !Dialogues.EnCours && !HUD.TableauOuvert;
            bool tenu = (kb != null && kb.fKey.isPressed) || (pad != null && pad.buttonWest.isPressed);

            if (!absorbe)
            {
                if (!vise) return;
                if (Progression.A("noeud_touche")) { HUD.Invite($"Nœud de Varenne — {Force * 100f:0} %. Il pulse plus faiblement depuis que tu y as touché."); return; }
                if (!Progression.A("info_noeud")) { HUD.Invite("Un nœud de mana, énorme. Mieux vaut comprendre ce que c'est avant d'y toucher."); return; }
                HUD.Invite("[F maintenu]  Tirer le nœud en toi — lâche avant la rupture");
                if (tenu && ((kb != null && kb.fKey.wasPressedThisFrame) || (pad != null && pad.buttonWest.wasPressedThisFrame)))
                {
                    absorbe = true; Pouvoirs.Absorbe = true;
                    Sons.Jouer("sceauOn", 1f, .5f);
                }
                return;
            }

            // Absorption : le mana monte vite au-delà du maximum
            j.FixerMana(j.Mana + j.manaMax * .38f * Time.deltaTime);
            float r = j.Mana / j.manaMax;
            HUD.Surcharge(r);
            HUD.Invite(null);
            battement -= Time.deltaTime;
            if (battement <= 0) { battement = Mathf.Lerp(.7f, .18f, Mathf.Clamp01((r - 1f) / 1.2f)); Sons.Jouer("coup", .7f, .35f); }
            if (r >= 2.24f) { Lacher(r, true); return; }
            if (!tenu) Lacher(r, false);
        }

        void Lacher(float r, bool rupture)
        {
            absorbe = false; Pouvoirs.Absorbe = false;
            HUD.Surcharge(-1f);
            Force = 1f - Mathf.Clamp((r - 1f) * .2f, .05f, .3f);
            Progression.Mettre("noeud_touche");
            Progression.Mettre("siphon");
            Progression.AjouterEveil(6f);
            Pouvoirs.Dechirer(1.2f);
            Progression.Mettre("ilse_a_vu");
            if (vapeur != null) vapeur.Play();
            Sons.Jouer("decouverte", 1f, .6f);

            string bilan;
            if (rupture)
            {
                malus = j.manaMax * .2f;
                j.manaMax -= malus;
                HUD.Flash(new Color(.9f, .1f, .05f), 2.2f);
                bilan = $"RUPTURE. Le Réceptacle hurle, tu tombes à genoux dans la boue, du sang au nez.\nChoc de surcharge : mana maximum {j.manaMax:0} jusqu'à l'aube.";
            }
            else if (r >= 1.6f)
            {
                j.manaMax += 10f;
                bilan = $"Tu lâches juste à temps. Le Réceptacle s'est élargi : mana maximum {j.manaMax:0}, pour toujours.";
            }
            else bilan = "Tu lâches trop tôt : le nœud t'a à peine effleuré.";
            j.FixerMana(j.manaMax);
            HUD.Message("✦ LE NŒUD DE VARENNE ✦",
                $"{bilan}\nNœud de Varenne : 100 % → {Force * 100f:0} %. Vesk y puisera moins.\nÉveil du Sceau +6 %. Ton Voile s'est rompu une seconde : une colonne d'argent dans le ciel.\n\nNOUVELLE FONCTION : SIPHON (R maintenu) — vider une source de mana à moins de 30 m.", 12f);
        }
    }
}
