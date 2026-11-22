using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Aldermark
{
    /// <summary>
    /// Plan du Registre (première compétence créée avec la Genèse) : touche P.
    /// Projette un pont fantôme bleu sur la rivière et liste ce qui lui manque.
    /// Le Plan propose d'abord le pont de pierre (beaucoup trop long), puis le pont provisoire en bois.
    /// Coûte 2 mana par minute de projection ; le mana ne remonte pas pendant ce temps.
    /// </summary>
    public class PlanRegistre : MonoBehaviour
    {
        public Chantier chantier;
        public const float CoutParMinute = 2f;
        public static bool Actif { get; private set; }

        Material fantome;
        readonly List<(GameObject vrai, GameObject image)> paires = new List<(GameObject, GameObject)>();
        Transform racineBois, racinePierre;
        JoueurAldric joueur;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reinitialiser() => Actif = false;

        void Start()
        {
            joueur = FindFirstObjectByType<JoueurAldric>();
            if (chantier == null) chantier = GetComponent<Chantier>();
            fantome = MateriauFantome();

            // Pont en bois : une image fantôme de chaque pièce, visible tant que la vraie n'est pas posée
            racineBois = new GameObject("FantomeBois").transform;
            racineBois.SetParent(transform, false);
            foreach (var groupe in new[] { chantier.etape1, chantier.etape2, chantier.etape3 })
            {
                if (groupe == null) continue;
                foreach (Transform piece in groupe)
                {
                    var mf = piece.GetComponent<MeshFilter>();
                    if (mf == null) continue;
                    var g = new GameObject("Fantome_" + piece.name);
                    g.transform.SetParent(racineBois, false);
                    g.transform.SetPositionAndRotation(piece.position, piece.rotation);
                    g.transform.localScale = piece.lossyScale * 1.02f;
                    g.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                    var r = g.AddComponent<MeshRenderer>();
                    r.sharedMaterial = fantome;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    paires.Add((piece.gameObject, g));
                }
            }

            // Pont de pierre : l'arche manquante au milieu des ruines
            racinePierre = new GameObject("FantomePierre").transform;
            racinePierre.SetParent(transform, false);
            float BX = Monde.PontX;
            Cube(racinePierre, new Vector3(BX, 4.6f, 0), new Vector3(9f, 1.3f, 5f));
            Cube(racinePierre, new Vector3(BX, 5.7f, -2.3f), new Vector3(9f, .9f, .4f));
            Cube(racinePierre, new Vector3(BX, 5.7f, 2.3f), new Vector3(9f, .9f, .4f));
            Cube(racinePierre, new Vector3(BX, -.2f, 0), new Vector3(3.2f, 8.6f, 4.6f));
            racineBois.gameObject.SetActive(false);
            racinePierre.gameObject.SetActive(false);
        }

        void Cube(Transform parent, Vector3 pos, Vector3 taille)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.position = pos;
            g.transform.localScale = taille;
            var r = g.GetComponent<MeshRenderer>();
            r.sharedMaterial = fantome;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static Material MateriauFantome()
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetColor("_BaseColor", new Color(.45f, .7f, 1f, .3f));
            return m;
        }

        void Update()
        {
            if (!Chantier.Actif) { Fermer(); return; }
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool occupe = Dialogues.EnCours || HUD.TableauOuvert;
            bool basculer = !occupe && ((kb != null && kb.pKey.wasPressedThisFrame) || (pad != null && pad.leftShoulder.wasPressedThisFrame));
            if (basculer)
            {
                if (Actif) Fermer();
                else Ouvrir();
            }
            if (!Actif) return;

            // Coût : 2 mana par minute
            if (joueur != null && !joueur.Depenser(CoutParMinute / 60f * Time.deltaTime))
            {
                Fermer();
                HUD.Message("MANA ÉPUISÉ", "Le Plan du Registre se dissipe.", 2.5f);
                return;
            }

            float a = .22f + .1f * Mathf.Sin(Time.time * 2.5f);
            fantome.SetColor("_BaseColor", new Color(.45f, .7f, 1f, a));

            if (!Progression.A("plan_bois"))
            {
                racinePierre.gameObject.SetActive(true);
                racineBois.gameObject.SetActive(false);
                HUD.AfficherPlan(
                    "<color=#9fd0ff><b>PLAN DU REGISTRE — PONT DE PIERRE</b></color>\n\n" +
                    "Matériaux : 46 blocs · 12 sacs de chaux · 18 poutres\n" +
                    "Main-d'œuvre : 8 personnes pendant 6 jours\n\n" +
                    "<color=#ff7a7a>Six jours. Le village mange des champignons depuis trois lunes.</color>\n\n" +
                    "<color=#ffd27a>[1]  Trop long : concevoir un pont provisoire en bois</color>\n" +
                    "<color=#9a98b0>P : fermer le plan · 2 mana par minute</color>");
                if ((kb != null && (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame)) || (pad != null && pad.buttonSouth.wasPressedThisFrame))
                {
                    Progression.Mettre("plan_bois");
                    Sons.Jouer("decouverte", .7f, 1.1f);
                    HUD.Message("PLAN MODIFIÉ",
                        "Pont provisoire en bois, posé sur les piles renforcées.\n12 blocs · 4 sacs de chaux · 54 poutres · 15 jours-personne.\nBien organisé, trois jours suffisent.", 7f);
                }
                return;
            }

            racinePierre.gameObject.SetActive(false);
            racineBois.gameObject.SetActive(true);
            foreach (var (vrai, image) in paires)
            {
                bool montrer = !vrai.activeInHierarchy;
                if (image.activeSelf != montrer) image.SetActive(montrer);
            }
            HUD.AfficherPlan("<color=#9fd0ff><b>PLAN DU REGISTRE</b></color>\n\n" + HUD.TextePont() +
                             "\n<color=#9a98b0>P : fermer le plan · 2 mana par minute</color>");
        }

        void Ouvrir()
        {
            Actif = true;
            Sons.Jouer("sceauOn", .6f, 1.3f);
            if (!Progression.A("plan_cree"))
            {
                Progression.Mettre("plan_cree");
                HUD.Message("COMPÉTENCE CRÉÉE — GENÈSE",
                    "PLAN DU REGISTRE\nProjette un ouvrage sur le monde et liste ce qui lui manque.\nCoût : 2 mana par minute de projection.", 7f);
            }
        }

        void Fermer()
        {
            if (!Actif) return;
            Actif = false;
            Sons.Jouer("sceauOff", .5f, 1.3f);
            if (racineBois != null) racineBois.gameObject.SetActive(false);
            if (racinePierre != null) racinePierre.gameObject.SetActive(false);
            HUD.AfficherPlan(null);
        }
    }
}
