using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// Fabrique de corps en volumes simples pour les créatures et les PNJ armés (même direction artistique
    /// que les villageois) : matériaux partagés, os (pivots), blocs sans collision, et un bipède articulé
    /// (bassin, torse, tête, bras, jambes) qu'on anime en code.
    /// </summary>
    public static class Pantin
    {
        static readonly Dictionary<string, Material> materiaux = new Dictionary<string, Material>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reinitialiser() => materiaux.Clear();

        public static Color Couleur(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        /// <summary>Un matériau éclairé partagé (une couleur, un lissé).</summary>
        public static Material Mat(string hex, float lisse = .1f)
        {
            string cle = hex + "/" + lisse;
            if (materiaux.TryGetValue(cle, out var m) && m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", Couleur(hex));
            m.SetFloat("_Smoothness", lisse);
            materiaux[cle] = m;
            return m;
        }

        /// <summary>Un matériau qui ne prend pas la lumière (yeux qui brillent, signes au-dessus des têtes).</summary>
        public static Material Lumineux(string hex)
        {
            string cle = "lum/" + hex;
            if (materiaux.TryGetValue(cle, out var m) && m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", Couleur(hex));
            materiaux[cle] = m;
            return m;
        }

        public static Transform Os(string nom, Transform parent, Vector3 pos, Vector3 rot = default)
        {
            var t = new GameObject(nom).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localRotation = Quaternion.Euler(rot);
            return t;
        }

        public static Transform Bloc(Transform parent, PrimitiveType type, Vector3 pos, Vector3 taille, Material m, Vector3 rot = default)
        {
            var g = GameObject.CreatePrimitive(type);
            var col = g.GetComponent<Collider>();
            if (col != null) { col.enabled = false; Object.Destroy(col); }
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(rot);
            g.transform.localScale = taille;
            g.GetComponent<MeshRenderer>().sharedMaterial = m;
            return g.transform;
        }

        // ---------- bipède ----------

        public class Bipede
        {
            public Transform squelette, bassin, torse, tete, brasG, brasD, jambeG, jambeD, mainG, mainD;
            public float longueurBras;

            /// <summary>Marche : jambes en ciseaux, bras opposés (si libres).</summary>
            public void Marcher(float phase, float amplitude, bool brasGLibre)
            {
                float s = Mathf.Sin(phase);
                jambeG.localRotation = Quaternion.Euler(s * 34f * amplitude, 0, 0);
                jambeD.localRotation = Quaternion.Euler(-s * 34f * amplitude, 0, 0);
                if (brasGLibre) brasG.localRotation = Quaternion.Euler(-s * 24f * amplitude - 6f, 0, -6f);
            }
        }

        /// <summary>
        /// Un bipède d'environ 1,75 m × echelle : chaque membre est un os qu'on fait pivoter.
        /// Les bras pendent vers le bas (−Y) ; une arme se pose sous la main (mainD), le long du bras.
        /// </summary>
        public static Bipede ConstruireBipede(Transform corps, float echelle, Material peau, Material haut, Material bas, Material pieds,
                                              float tete = 1f, float carrure = 1f, float bras = 1f)
        {
            var b = new Bipede { longueurBras = .6f * bras };
            b.squelette = Os("Squelette", corps, Vector3.zero);
            b.squelette.localScale = Vector3.one * echelle;

            b.bassin = Os("Bassin", b.squelette, new Vector3(0, .95f, 0));
            Bloc(b.bassin, PrimitiveType.Cube, Vector3.zero, new Vector3(.36f * carrure, .2f, .23f), bas);
            b.jambeG = Os("JambeG", b.bassin, new Vector3(-.11f * carrure, -.04f, 0));
            b.jambeD = Os("JambeD", b.bassin, new Vector3(.11f * carrure, -.04f, 0));
            foreach (var j in new[] { b.jambeG, b.jambeD })
            {
                Bloc(j, PrimitiveType.Cube, new Vector3(0, -.44f, 0), new Vector3(.15f * carrure, .86f, .15f), bas);
                Bloc(j, PrimitiveType.Cube, new Vector3(0, -.87f, .05f), new Vector3(.17f * carrure, .1f, .27f), pieds);
            }

            b.torse = Os("Torse", b.bassin, new Vector3(0, .08f, 0));
            Bloc(b.torse, PrimitiveType.Cube, new Vector3(0, .3f, 0), new Vector3(.44f * carrure, .58f, .26f), haut);

            b.tete = Os("Tete", b.torse, new Vector3(0, .62f, 0));
            float t = .28f * tete;
            Bloc(b.tete, PrimitiveType.Cube, new Vector3(0, t * .55f, .01f), new Vector3(t, t * 1.08f, t), peau);
            var oeil = Mat("#1a1410", .6f);
            Bloc(b.tete, PrimitiveType.Cube, new Vector3(-t * .22f, t * .62f, t * .5f), new Vector3(t * .14f, t * .12f, .02f), oeil);
            Bloc(b.tete, PrimitiveType.Cube, new Vector3(t * .22f, t * .62f, t * .5f), new Vector3(t * .14f, t * .12f, .02f), oeil);

            float l = b.longueurBras;
            b.brasG = Os("BrasG", b.torse, new Vector3(-.28f * carrure, .54f, 0));
            b.brasD = Os("BrasD", b.torse, new Vector3(.28f * carrure, .54f, 0));
            foreach (var a in new[] { b.brasG, b.brasD })
            {
                Bloc(a, PrimitiveType.Cube, new Vector3(0, -l / 2f, 0), new Vector3(.12f * carrure, l, .12f * carrure), haut);
                Bloc(a, PrimitiveType.Sphere, new Vector3(0, -l - .04f, 0), Vector3.one * .13f * carrure, peau);
            }
            b.mainG = Os("MainG", b.brasG, new Vector3(0, -l - .04f, 0));
            b.mainD = Os("MainD", b.brasD, new Vector3(0, -l - .04f, 0));
            return b;
        }
    }
}
