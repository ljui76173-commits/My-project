using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    /// <summary>Un volume de collision (boîte) : centre dans le monde, taille, lacet.</summary>
    public struct Volume
    {
        public Vector3 centre, taille;
        public float lacet;
        public Volume(Repere r, float x0, float y0, float z0, float x1, float y1, float z1)
        {
            centre = r.P((x0 + x1) * .5f, (y0 + y1) * .5f, (z0 + z1) * .5f);
            taille = new Vector3(Math.Abs(x1 - x0), Math.Abs(y1 - y0), Math.Abs(z1 - z0));
            lacet = r.LacetMonde;
        }
    }

    /// <summary>Un tirage au sort reproductible (même graine, même maison).</summary>
    public class Tirage
    {
        uint e;
        public Tirage(int graine) { e = (uint)graine * 2654435761u + 1013904223u; if (e == 0) e = 1; Valeur(); Valeur(); }
        public float Valeur() { e ^= e << 13; e ^= e >> 17; e ^= e << 5; return (e & 0xFFFFFF) / 16777216f; }
        public float Entre(float a, float b) => a + (b - a) * Valeur();
        public int Entier(int a, int bExclu) => a + (int)(Valeur() * (bExclu - a) * .9999f);
        public bool Chance(float p) => Valeur() < p;
        public T Parmi<T>(T[] t) => t[Entier(0, t.Length)];
    }

    public enum StyleMur { Colombage, Pierre, Enduit, Planches }
    public enum Toiture { Tuiles, Ardoise, Chaume }

    /// <summary>Tout ce qui décrit une maison. Les couleurs sont tirées dans les palettes par <see cref="KitBatiment.Varier"/>.</summary>
    [Serializable]
    public class OptionsMaison
    {
        public float largeur = 7f, profondeur = 6f, hauteurEtage = 2.6f, pente = 1.1f, socle = 1.6f;
        public int etages = 2, graine;
        public StyleMur style;
        public Toiture toiture;
        public Color enduit, bois, pierre, toit, volets, porte;
        public bool encorbellement, porteSurPignon, lucarne, cheminee = true, auvent, enseigne, rezOuvert;
        /// <summary>Pas d'ouvertures du tout (grange) ou pas au rez-de-chaussée (entrepôt : portes de charge ajoutées à part).</summary>
        public bool aveugle, aveugleRez;
        public Color auventCouleur, enseigneCouleur;
    }

    /// <summary>
    /// Le kit de construction : des maisons à colombages, en pierre ou enduites, avec encorbellements, volets,
    /// jardinières, lucarnes et cheminées ; et les grands bâtiments (église, halle, forge, auberge, moulin,
    /// remparts, tours, portes, château, quais). Tout est fait de boîtes, de pans et de cônes, à la main.
    /// La façade principale (la porte) est du côté +z du repère, ou +x si la maison a pignon sur rue.
    /// </summary>
    public static partial class KitBatiment
    {
        public static Color C(string hex)
        {
            int v = Convert.ToInt32(hex.TrimStart('#'), 16);
            return new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f, 1f);
        }

        static Color[] P(params string[] h) { var t = new Color[h.Length]; for (int i = 0; i < h.Length; i++) t[i] = C(h[i]); return t; }

        public static readonly Color[] Enduits = P("#e8dcc0", "#efe3c8", "#e6d2b0", "#dcc9a6", "#f0e6d2", "#e3cfa8", "#e9c9a2", "#d8c4a8", "#eadcc8");
        public static readonly Color[] Bois = P("#5a3d2b", "#4a3426", "#6b4a33", "#3f2e24", "#5e4434");
        public static readonly Color[] Pierres = P("#a59d8f", "#9a9284", "#b0a898", "#8f8a80", "#aaa292", "#9c9488");
        public static readonly Color[] Tuiles = P("#a6533a", "#b5603e", "#9c4b36", "#b86a45", "#8d4535", "#a85d40");
        public static readonly Color[] Ardoises = P("#5b6274", "#4f5668", "#646b7c", "#566070");
        public static readonly Color[] Chaumes = P("#b59a5a", "#a88d50", "#bfa463");
        public static readonly Color[] Volets = P("#4f6f8f", "#5d7f4e", "#8f3f3a", "#6a8a8a", "#7a5a3a", "#3f5f6f", "#9a7a3a");
        public static readonly Color[] Portes = P("#5a3a26", "#6b4430", "#4a5f7a", "#6a3a30", "#3f5040");
        public static readonly Color[] Toiles = P("#b84a3a", "#3f6fa0", "#d0a040", "#4f8a50", "#a04a7a", "#e0d8c0", "#c8703a");
        public static readonly Color[] Fleurs = P("#e0403a", "#f080a0", "#f0d040", "#d060d0", "#ffffff");
        static readonly Color Vitre = C("#2a2f3e"), Ombre = C("#1c1a20"), Fer = C("#3a3a40"), Braise = C("#ff8a3a");

        /// <summary>Des couleurs et des détails tirés au sort (selon le style voulu).</summary>
        public static void Varier(OptionsMaison o, Tirage t, bool riche)
        {
            o.enduit = t.Parmi(Enduits);
            o.bois = t.Parmi(Bois);
            o.pierre = t.Parmi(Pierres);
            o.volets = t.Parmi(Volets);
            o.porte = t.Parmi(Portes);
            o.toit = o.toiture == Toiture.Ardoise ? t.Parmi(Ardoises) : o.toiture == Toiture.Chaume ? t.Parmi(Chaumes) : t.Parmi(Tuiles);
            o.auventCouleur = t.Parmi(Toiles);
            o.enseigneCouleur = t.Parmi(Toiles);
            o.pente = o.toiture == Toiture.Chaume ? t.Entre(1.15f, 1.4f) : o.toiture == Toiture.Ardoise ? t.Entre(1.2f, 1.6f) : t.Entre(.85f, 1.2f);
            o.lucarne = riche ? t.Chance(.5f) : t.Chance(.15f);
        }

        // ---------- d'une commande du plan de ville à la géométrie ----------

        /// <summary>Construit une commande de <see cref="PlanVille"/> dans le maillage (le sol du maillage est réglé ici).</summary>
        public static void Construire(Maillage m, Commande k, List<Volume> vols)
        {
            var b = Repere.Lacet(k.position, k.lacet);
            m.sol = k.position.y;
            switch (k.type)
            {
                case Gabarit.Maison:
                    var o = k.maison ?? new OptionsMaison { graine = k.graine };
                    Maison(m, o.porteSurPignon ? Repere.Lacet(k.position, k.lacet - 90f) : b, o, vols);
                    break;
                case Gabarit.Auberge: Auberge(m, b, k.graine, vols); break;
                case Gabarit.Forge: Forge(m, b, k.graine, vols); break;
                case Gabarit.Eglise: Eglise(m, b, k.a, k.b, k.graine, vols); break;
                case Gabarit.Chapelle: Chapelle(m, b, k.graine, vols); break;
                case Gabarit.Halle: Halle(m, b, k.a, k.b, k.graine, vols); break;
                case Gabarit.Grange: Grange(m, b, k.graine, vols); break;
                case Gabarit.MoulinAVent: MoulinAVent(m, b, k.graine, vols); break;
                case Gabarit.Puits: Puits(m, b, k.graine, vols); break;
                case Gabarit.Fontaine: Fontaine(m, b, k.graine, vols); break;
                case Gabarit.Entrepot: Entrepot(m, b, k.graine, vols); break;
                case Gabarit.Etal: Etal(m, b, k.graine, vols); break;
                case Gabarit.Chateau: Chateau(m, b, k.a, k.graine, vols); break;
                case Gabarit.Ponton:
                    float eau = Geo.NiveauEau(k.position.x, k.position.z);
                    Ponton(m, b, k.a, k.b, eau > -9000f ? eau : 0f, vols);
                    break;
                case Gabarit.Barque: Barque(m, b, k.graine); break;
                case Gabarit.Tour: Tour(m, b, k.a, k.b, k.graine == 1, PierreRempart, Ardoises[0], vols); break;
                case Gabarit.PorteDeVille: PorteDeVille(m, b, k.a, k.b, PierreRempart, Ardoises[(k.graine & 0x7fffffff) % Ardoises.Length], vols); break;
                case Gabarit.Rempart: Rempart(m, k.position, k.fin, k.a, k.b, PierreRempart, vols); break;
            }
            m.sol = -10000f;
        }

        // ---------- la maison ----------

        public static void Maison(Maillage m, Repere b, OptionsMaison o, List<Volume> vols)
        {
            var sauve = m.r;
            var t = new Tirage(o.graine);
            m.r = b;
            float W = o.largeur, D = o.profondeur, Hf = o.hauteurEtage;
            m.Boite(-W / 2 - .12f, -o.socle, -D / 2 - .12f, W / 2 + .12f, .45f, D / 2 + .12f, o.pierre, Maillage.Faces.SansDessous);
            float y = .45f, ex = 0f, ez = 0f;
            bool rezPierre = o.style == StyleMur.Pierre || (o.style == StyleMur.Colombage && t.Chance(.45f));
            for (int k = 0; k < o.etages; k++)
            {
                if (k > 0 && o.encorbellement) { if (o.porteSurPignon) ex += .32f; else ez += .32f; }
                float w = W + ex * 2f, d = D + ez * 2f;
                bool colomb = o.style == StyleMur.Colombage && (k > 0 || !rezPierre);
                Color mur = o.style == StyleMur.Planches ? o.bois * 1.25f : (k == 0 && rezPierre) || o.style == StyleMur.Pierre ? o.pierre : o.enduit;
                m.Boite(-w / 2, y, -d / 2, w / 2, y + Hf, d / 2, mur, k > 0 && (ex > 0f || ez > 0f) ? Maillage.Faces.Cotes | Maillage.Faces.Dessous : Maillage.Faces.Cotes);
                if (k > 0) m.Boite(-w / 2 - .05f, y - .14f, -d / 2 - .05f, w / 2 + .05f, y + .08f, d / 2 + .05f, o.bois, Maillage.Faces.Cotes | Maillage.Faces.Dessous);
                for (int f = 0; f < 4; f++)
                {
                    float L = f % 2 == 0 ? w : d;
                    var fr = FrameFacade(b, f, w, d);
                    bool cotePorte = o.porteSurPignon ? f == 1 : f == 0;
                    Facade(m, fr, L, y, Hf, o, t, colomb, mur, cotePorte && k == 0, k == 0, cotePorte || f == 2);
                }
                y += Hf;
            }
            float wr = W + ex * 2f, dr = D + ez * 2f;
            Toit(m, b, wr, dr, y, o, t, o.style == StyleMur.Colombage);
            m.r = b;
            vols.Add(new Volume(b, -W / 2 - .12f, -o.socle, -D / 2 - .12f, W / 2 + .12f, y, D / 2 + .12f));
            m.r = sauve;
        }

        /// <summary>Le repère d'une façade : 0 avant (+z), 1 droite (+x), 2 arrière, 3 gauche ; origine au pied du mur, au milieu.</summary>
        public static Repere FrameFacade(Repere b, int f, float w, float d)
        {
            switch (f)
            {
                case 0: return b.Sous(0f, 0f, d / 2, 0f);
                case 1: return b.Sous(w / 2, 0f, 0f, 90f);
                case 2: return b.Sous(0f, 0f, -d / 2, 180f);
                default: return b.Sous(-w / 2, 0f, 0f, -90f);
            }
        }

        /// <summary>
        /// Une pièce de bois (ou de pierre) posée sur la façade. Mince (moins de 9 cm) : la seule face avant suffit,
        /// les côtés ne se voient pas ; épaisse (appui, linteau saillant) : avant, côtés et dessus.
        /// </summary>
        static void Piece(Maillage m, float u0, float v0, float u1, float v1, float ep, Color col, float z0 = 0f)
        {
            if (m.detail) m.Boite(Math.Min(u0, u1), Math.Min(v0, v1), z0, Math.Max(u0, u1), Math.Max(v0, v1), z0 + ep, col,
                       ep < .09f ? Maillage.Faces.Avant : Maillage.Faces.Avant | Maillage.Faces.Droite | Maillage.Faces.Gauche | Maillage.Faces.Dessus);
        }

        static void Facade(Maillage m, Repere fr, float L, float y0, float H, OptionsMaison o, Tirage t, bool colombage, Color mur, bool porte, bool rez, bool principale)
        {
            var sauve = m.r;
            m.r = fr;
            int nf = Math.Max(1, (int)Math.Floor((L - .4f) / 2.2f));
            float pas = L / nf;
            int iPorte = porte ? (nf == 1 ? 0 : t.Entier(0, nf)) : -1;
            float hf = rez ? 1.05f : 1.15f, vf = y0 + (rez ? .95f : .75f);
            bool fenetres = (principale || t.Chance(.75f)) && !o.aveugle && !(rez && o.aveugleRez);
            if (o.aveugle || (rez && o.aveugleRez)) iPorte = -1;
            var avecFenetre = new bool[nf];
            for (int i = 0; i < nf; i++)
            {
                float u = -L / 2 + pas * (i + .5f);
                if (i == iPorte)
                {
                    if (o.rezOuvert) Echoppe(m, u, y0, Math.Min(pas - .4f, 2.6f), o, t);
                    else Porte(m, u, y0, o);
                }
                else if (fenetres && (principale || t.Chance(.7f)))
                {
                    Fenetre(m, u, vf, rez ? .8f : .85f, hf, o, t, principale);
                    avecFenetre[i] = true;
                }
            }
            if (colombage)
            {
                Color bo = o.bois;
                Piece(m, -L / 2, y0, L / 2, y0 + .16f, .07f, bo);
                Piece(m, -L / 2, y0 + H - .16f, L / 2, y0 + H, .07f, bo);
                for (int i = 0; i <= nf; i++)
                {
                    float u = -L / 2 + pas * i;
                    Piece(m, u - (i == 0 ? 0f : .08f), y0, u + (i == nf ? 0f : .08f), y0 + H, .07f, bo);
                }
                for (int i = 0; i < nf; i++)
                {
                    float ua = -L / 2 + pas * i + .08f, ub = -L / 2 + pas * (i + 1) - .08f;
                    if (avecFenetre[i] || i == iPorte)
                    {
                        // Les poteaux qui encadrent la fenêtre et l'appui
                        float uc = -L / 2 + pas * (i + .5f);
                        if (i != iPorte)
                        {
                            Piece(m, ua, vf - .3f, ub, vf - .18f, .06f, bo);
                            Bande(m, ua, y0 + .16f, uc - .5f, vf - .3f, .13f, .065f, bo);
                            Bande(m, ub, y0 + .16f, uc + .5f, vf - .3f, .13f, .065f, bo);
                        }
                    }
                    else
                    {
                        // Panneau plein : croix de Saint-André ou décharge simple
                        float v0 = y0 + .16f, v1 = y0 + H - .16f;
                        if (t.Chance(.5f)) { Bande(m, ua, v0, ub, v1, .14f, .065f, bo); Bande(m, ub, v0, ua, v1, .14f, .065f, bo); }
                        else { float vm = (v0 + v1) * .5f; Piece(m, ua, vm - .07f, ub, vm + .07f, .065f, bo); Bande(m, ua, v0, ub, vm, .13f, .066f, bo); Bande(m, ub, vm, ua, v1, .13f, .066f, bo); }
                    }
                }
            }
            else if (o.style == StyleMur.Pierre || (rez && mur.r == o.pierre.r && mur.g == o.pierre.g))
            {
                // Chaînes d'angle : de gros blocs alternés
                for (float v = y0 + .05f; v < y0 + H - .2f; v += .34f)
                {
                    bool large = ((int)((v - y0) / .34f)) % 2 == 0;
                    Piece(m, L / 2 - (large ? .55f : .35f), v, L / 2, v + .3f, .035f, o.pierre * 1.12f);
                    Piece(m, -L / 2, v, -L / 2 + (large ? .35f : .55f), v + .3f, .035f, o.pierre * 1.12f);
                }
            }
            else if (o.style == StyleMur.Planches)
            {
                for (float u = -L / 2 + .3f; u < L / 2; u += .45f) Piece(m, u - .02f, y0, u + .02f, y0 + H, .02f, o.bois * .8f);
            }
            m.r = sauve;
        }

        static void Bande(Maillage m, float u0, float v0, float u1, float v1, float largeur, float z, Color col) { if (m.detail) m.Bande(u0, v0, u1, v1, largeur, z, col); }

        static void Fenetre(Maillage m, float u, float v0, float w, float h, OptionsMaison o, Tirage t, bool soignee)
        {
            Color cadre = o.style == StyleMur.Pierre ? o.pierre * 1.15f : o.bois;
            m.Boite(u - w / 2, v0, 0f, u + w / 2, v0 + h, .012f, Vitre, Maillage.Faces.Avant);
            Piece(m, u - w / 2 - .09f, v0 + h, u + w / 2 + .09f, v0 + h + .12f, .07f, cadre);                     // linteau
            Piece(m, u - w / 2 - .12f, v0 - .1f, u + w / 2 + .12f, v0, .13f, o.pierre * 1.1f);                      // appui
            Piece(m, u - w / 2 - .07f, v0, u - w / 2, v0 + h, .06f, cadre);
            Piece(m, u + w / 2, v0, u + w / 2 + .07f, v0 + h, .06f, cadre);
            Piece(m, u - .03f, v0, u + .03f, v0 + h, .035f, cadre * 1.1f);                                          // meneau
            Piece(m, u - w / 2, v0 + h * .6f, u + w / 2, v0 + h * .6f + .05f, .035f, cadre * 1.1f);                // traverse
            if (t.Chance(soignee ? .6f : .4f))
            {
                // Volets ouverts contre le mur, à lames
                Piece(m, u + w / 2 + .07f, v0, u + w / 2 + .07f + w * .5f, v0 + h, .04f, o.volets);
                Piece(m, u - w / 2 - .07f - w * .5f, v0, u - w / 2 - .07f, v0 + h, .04f, o.volets);
                Piece(m, u + w / 2 + .07f, v0 + h * .3f, u + w / 2 + .07f + w * .5f, v0 + h * .3f + .06f, .055f, o.volets * .8f);
                Piece(m, u - w / 2 - .07f - w * .5f, v0 + h * .3f, u - w / 2 - .07f, v0 + h * .3f + .06f, .055f, o.volets * .8f);
            }
            if (t.Chance(.22f))
            {
                // Une jardinière fleurie
                Piece(m, u - w / 2, v0 - .28f, u + w / 2, v0 - .1f, .22f, o.bois * 1.1f);
                var fl = t.Parmi(Fleurs);
                if (m.detail) m.Boite(u - w / 2 + .05f, v0 - .1f, .04f, u + w / 2 - .05f, v0 + .02f, .2f, C("#4f7a34"), Maillage.Faces.Avant | Maillage.Faces.Dessus);
                for (int k = 0; k < 3; k++)
                {
                    float uu = u - w / 2 + .18f + k * (w - .36f) / 2f;
                    if (m.detail) m.Boite(uu - .08f, v0 - .02f, .08f, uu + .08f, v0 + .1f, .2f, fl, Maillage.Faces.Avant | Maillage.Faces.Dessus);
                }
            }
        }

        static void Porte(Maillage m, float u, float y0, OptionsMaison o)
        {
            const float w = 1.1f, h = 2.15f;
            Color cadre = o.style == StyleMur.Colombage || o.style == StyleMur.Planches ? o.bois : o.pierre * 1.15f;
            m.Boite(u - w / 2, y0, 0f, u + w / 2, y0 + h, .03f, o.porte, Maillage.Faces.Avant);
            for (int k = 1; k < 4; k++) Piece(m, u - w / 2 + k * w / 4 - .015f, y0, u - w / 2 + k * w / 4 + .015f, y0 + h, .045f, o.porte * .75f);
            Piece(m, u - w / 2 - .16f, y0 + h, u + w / 2 + .16f, y0 + h + .2f, .09f, cadre);
            Piece(m, u - w / 2 - .14f, y0, u - w / 2, y0 + h, .08f, cadre);
            Piece(m, u + w / 2, y0, u + w / 2 + .14f, y0 + h, .08f, cadre);
            m.Boite(u + w / 2 - .25f, y0 + 1.0f, .03f, u + w / 2 - .17f, y0 + 1.08f, .09f, Fer);                           // poignée
            m.Boite(u - w / 2 - .25f, y0 - .5f, 0f, u + w / 2 + .25f, y0 - .22f, .7f, o.pierre * 1.05f);                   // marche
        }

        /// <summary>Une boutique ouverte sur la rue : large baie, comptoir, marchandises, auvent de toile.</summary>
        static void Echoppe(Maillage m, float u, float y0, float w, OptionsMaison o, Tirage t)
        {
            const float h = 2.3f;
            m.Boite(u - w / 2, y0, 0f, u + w / 2, y0 + h, .012f, Ombre, Maillage.Faces.Avant);
            Piece(m, u - w / 2 - .15f, y0 + h, u + w / 2 + .15f, y0 + h + .22f, .1f, o.bois);
            Piece(m, u - w / 2 - .15f, y0, u - w / 2, y0 + h, .1f, o.bois);
            Piece(m, u + w / 2, y0, u + w / 2 + .15f, y0 + h, .1f, o.bois);
            // Le comptoir et ses marchandises
            m.Boite(u - w / 2, y0, .0f, u + w / 2, y0 + .9f, .55f, o.bois * 1.2f);
            for (int k = 0; k < 5; k++)
            {
                float uu = u - w / 2 + .25f + k * (w - .5f) / 4f;
                m.Boite(uu - .12f, y0 + .9f, .12f, uu + .12f, y0 + .9f + t.Entre(.12f, .3f), .42f, t.Parmi(Fleurs) * .85f);
            }
            // L'auvent
            Color toile = o.auventCouleur;
            m.Dalle(new Vector3(u + w / 2 + .3f, y0 + h + .15f, .05f), new Vector3(u - w / 2 - .3f, y0 + h + .15f, .05f),
                    new Vector3(u - w / 2 - .3f, y0 + h - .45f, 1.45f), new Vector3(u + w / 2 + .3f, y0 + h - .45f, 1.45f), .05f, toile, toile * .85f);
            for (int k = 0; k < 6; k += 2)
            {
                float a = u - w / 2 - .3f + k * (w + .6f) / 6f, b2 = a + (w + .6f) / 6f;
                m.Boite(a, y0 + h - .65f, 1.38f, b2, y0 + h - .42f, 1.46f, toile * .7f, Maillage.Faces.Avant | Maillage.Faces.Dessous);
            }
        }

        /// <summary>Le toit à deux pans (faîtage selon x), les pignons, la cheminée, la lucarne.</summary>
        static void Toit(Maillage m, Repere b, float W, float D, float ye, OptionsMaison o, Tirage t, bool colombage)
        {
            m.r = b;
            bool chaume = o.toiture == Toiture.Chaume;
            float ov = chaume ? .55f : .42f, og = chaume ? .45f : .32f, ep = chaume ? .42f : .18f;
            float yr = ye + D / 2 * o.pente;
            ToitDeuxPans(m, W, D, ye, o.pente, ov, og, ep, o.toit, chaume ? o.toit * .85f : o.bois, !chaume);
            // Pignons
            Color mur = o.style == StyleMur.Pierre ? o.pierre : o.style == StyleMur.Planches ? o.bois * 1.25f : o.enduit;
            m.Tri(new Vector3(W / 2, yr - .05f, 0f), new Vector3(W / 2, ye, D / 2), new Vector3(W / 2, ye, -D / 2), mur);
            m.Tri(new Vector3(-W / 2, yr - .05f, 0f), new Vector3(-W / 2, ye, -D / 2), new Vector3(-W / 2, ye, D / 2), mur);
            for (int f = 1; f <= 3; f += 2)
            {
                var fr = FrameFacade(b, f, W, D);
                m.r = fr;
                float h = yr - ye;
                m.Boite(-.3f, ye + h * .28f, 0f, .3f, ye + h * .28f + .55f, .012f, Vitre, Maillage.Faces.Avant);
                Piece(m, -.38f, ye + h * .28f - .08f, .38f, ye + h * .28f, .08f, o.pierre * 1.1f);
                if (colombage)
                {
                    Piece(m, -D / 2, ye, D / 2, ye + .15f, .07f, o.bois);
                    Piece(m, -.07f, ye + .15f, .07f, ye + h * .28f - .08f, .07f, o.bois);
                    Piece(m, -.07f, ye + h * .28f + .55f, .07f, yr - .2f, .07f, o.bois);
                    Bande(m, -D / 2 + .3f, ye + .15f, -.15f, ye + h * .7f, .13f, .065f, o.bois);
                    Bande(m, D / 2 - .3f, ye + .15f, .15f, ye + h * .7f, .13f, .065f, o.bois);
                }
                m.r = b;
            }
            // Cheminée
            if (o.cheminee)
            {
                float cx = (t.Chance(.5f) ? 1f : -1f) * (W / 2 - .75f), cz = t.Entre(-.6f, .6f);
                float haut = yr - Math.Abs(cz) * o.pente + t.Entre(.7f, 1.2f);
                m.Boite(cx - .34f, ye - .2f, cz - .34f, cx + .34f, haut, cz + .34f, o.pierre * .95f);
                m.Boite(cx - .42f, haut, cz - .42f, cx + .42f, haut + .14f, cz + .42f, o.pierre * .75f);
                m.Boite(cx - .2f, haut + .14f, cz - .2f, cx + .2f, haut + .15f, cz + .2f, Ombre, Maillage.Faces.Dessus);
            }
            // Lucarne sur le pan avant
            if (o.lucarne && W > 5.5f && !chaume)
            {
                float z0 = D / 4f, yb = yr - z0 * o.pente;
                var lr = b.Sous(t.Entre(-W * .2f, W * .2f), 0f, 0f, 0f);
                m.r = lr;
                m.Boite(-.6f, yb - .7f, z0 - .5f, .6f, yb + 1.1f, z0 + .35f, o.enduit, Maillage.Faces.Avant | Maillage.Faces.Droite | Maillage.Faces.Gauche);
                m.Boite(-.32f, yb + .1f, z0 + .35f, .32f, yb + .85f, z0 + .36f, Vitre, Maillage.Faces.Avant);
                Piece(m, -.4f, yb + .85f, .4f, yb + .95f, .06f, o.bois, z0 + .35f);
                var toitL = lr.Sous(0f, 0f, z0 - .1f, 90f);
                m.r = toitL;
                ToitDeuxPans(m, 1.0f, 1.2f, yb + 1.1f, 1.2f, .25f, .15f, .1f, o.toit, o.bois);
                m.Tri(new Vector3(-.5f, yb + 1.1f + .6f * 1.2f - .05f, 0f), new Vector3(-.5f, yb + 1.1f, -.6f), new Vector3(-.5f, yb + 1.1f, .6f), o.enduit);
            }
            m.r = b;
        }

        /// <summary>Deux pans épais, faîtage selon x, rives débordantes, faîtière.</summary>
        public static void ToitDeuxPans(Maillage m, float W, float D, float ye, float pente, float ov, float og, float ep, Color toit, Color rive, bool rangs = true)
        {
            float yr = ye + D / 2 * pente, x = W / 2 + og, z = D / 2 + ov, yE = ye - ov * pente;
            var a = new Vector3(x, yr, 0f); var b = new Vector3(-x, yr, 0f); var c = new Vector3(-x, yE, z); var d = new Vector3(x, yE, z);
            m.Dalle(a, b, c, d, ep, toit, rive);
            if (rangs) Rangs(m, a, b, c, d, toit * .8f);
            a = new Vector3(-x, yr, 0f); b = new Vector3(x, yr, 0f); c = new Vector3(x, yE, -z); d = new Vector3(-x, yE, -z);
            m.Dalle(a, b, c, d, ep, toit * .94f, rive);
            if (rangs) Rangs(m, a, b, c, d, toit * .76f);
            m.Boite(-x - .02f, yr - .1f, -.14f, x + .02f, yr + .09f, .14f, toit * .72f, Maillage.Faces.SansDessous);
        }

        /// <summary>Les rangs de tuiles ou d'ardoises : de fines bandes plus sombres, parallèles au faîtage.</summary>
        static void Rangs(Maillage m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
        {
            if (!m.detail) return;
            var u = b - a; var w = d - a;
            var n = new Vector3(u.y * w.z - u.z * w.y, u.z * w.x - u.x * w.z, u.x * w.y - u.y * w.x);
            float l = n.magnitude;
            if (l < 1e-5f) return;
            n = n * (.014f / l);
            float pan = w.magnitude;
            int k = Math.Max(3, (int)(pan / .42f));
            for (int i = 1; i < k; i++)
            {
                float f0 = (float)i / k, f1 = f0 + .045f / pan;
                Vector3 A = a + (d - a) * f0 + n, B = b + (c - b) * f0 + n, C2 = b + (c - b) * f1 + n, D2 = a + (d - a) * f1 + n;
                m.Quad(A, B, C2, D2, col);
            }
        }

        /// <summary>Un toit en pavillon (quatre pans) sur un rectangle W × D.</summary>
        public static void ToitPavillon(Maillage m, float W, float D, float ye, float hauteur, float ov, Color toit)
        {
            float x = W / 2 + ov, z = D / 2 + ov, yE = ye - ov * hauteur / Math.Max(.1f, Math.Min(W, D) / 2);
            float faite = Math.Max(0f, (W - D) / 2);
            float yr = ye + hauteur;
            var A = new Vector3(x, yE, z); var B = new Vector3(-x, yE, z); var Cc = new Vector3(-x, yE, -z); var Dd = new Vector3(x, yE, -z);
            var R1 = new Vector3(faite, yr, 0f); var R2 = new Vector3(-faite, yr, 0f);
            m.Quad(R1, R2, B, A, toit);                  // avant
            m.Quad(R2, R1, Dd, Cc, toit * .93f);         // arrière
            m.Tri(R1, A, Dd, toit * .97f);               // droite
            m.Tri(R2, Cc, B, toit * .9f);                // gauche
            m.Quad(A, B, Cc, Dd, toit * .5f);            // dessous (vu d'en bas)
        }

        // ---------- les grands bâtiments ----------

        /// <summary>L'église : nef, bas-côtés à contreforts, clocher à flèche, abside, rosace.</summary>
        public static void Eglise(Maillage m, Repere b, float L, float W, int graine, List<Volume> vols)
        {
            var sauve = m.r; m.r = b;
            var t = new Tirage(graine);
            Color pierre = t.Parmi(Pierres) * 1.05f, ardoise = t.Parmi(Ardoises), bois = Bois[0];
            float H = 9f;
            // La nef (longueur selon x, façade et clocher côté +x, abside côté −x)
            m.Boite(-L / 2, -2f, -W / 2, L / 2, H, W / 2, pierre);
            ToitDeuxPans(m, L, W, H, 1.25f, .5f, .2f, .25f, ardoise, pierre * .8f);
            m.Tri(new Vector3(L / 2, H + W / 2 * 1.25f - .05f, 0f), new Vector3(L / 2, H, W / 2), new Vector3(L / 2, H, -W / 2), pierre);
            m.Tri(new Vector3(-L / 2, H + W / 2 * 1.25f - .05f, 0f), new Vector3(-L / 2, H, -W / 2), new Vector3(-L / 2, H, W / 2), pierre);
            // Contreforts et grandes baies
            for (int i = 0; i < 5; i++)
            {
                float x = -L / 2 + 2f + i * (L - 4f) / 4f;
                foreach (float s in new[] { -1f, 1f })
                {
                    float z0 = s * W / 2;
                    m.Boite(x - .45f, -1f, Math.Min(z0, z0 + s * 1.3f), x + .45f, H * .75f, Math.Max(z0, z0 + s * 1.3f), pierre * .95f);
                    m.Boite(x - .55f, H * .75f, Math.Min(z0, z0 + s * 1.4f), x + .55f, H * .75f + .3f, Math.Max(z0, z0 + s * 1.4f), pierre * .85f);
                    if (i < 4)
                    {
                        var fr = FrameFacade(b, s > 0 ? 0 : 2, L, W);
                        m.r = fr;
                        float u = (s > 0 ? 1f : -1f) * (x + (L - 4f) / 8f);
                        Lancette(m, u, 2.6f, .9f, 4.2f, pierre);
                        m.r = b;
                    }
                }
            }
            // Façade : portail, rosace
            var fa = FrameFacade(b, 1, L, W);
            m.r = fa;
            m.Boite(-1.1f, 0f, 0f, 1.1f, 3.4f, .03f, Portes[0], Maillage.Faces.Avant);
            for (int k = 0; k < 3; k++) Piece(m, -1.4f - k * .25f, 0f, -1.1f - k * .25f, 3.6f + k * .3f, .15f + k * .12f, pierre * 1.08f);
            for (int k = 0; k < 3; k++) Piece(m, 1.1f + k * .25f, 0f, 1.4f + k * .25f, 3.6f + k * .3f, .15f + k * .12f, pierre * 1.08f);
            Piece(m, -2f, 3.6f, 2f, 4.4f, .5f, pierre * 1.08f);
            m.Disque(0f, 6.4f, .03f, 1.5f, 16, pierre * 1.12f);
            m.Disque(0f, 6.4f, .05f, 1.2f, 16, Vitre);
            m.r = b;
            // Le clocher, côté façade
            float tx = L / 2 - 2.6f, cote = 5.2f, ht = 21f;
            m.Boite(tx - cote / 2, -2f, -W / 2 - cote * .6f - .2f, tx + cote / 2, ht, -W / 2 - .2f + cote * .4f, pierre * 1.02f);
            var cl = b.Sous(tx, 0f, -W / 2 - cote * .1f - .2f, 0f);
            m.r = cl;
            for (int f = 0; f < 4; f++)
            {
                var fr = FrameFacade(cl, f, cote, cote);
                m.r = fr;
                m.Boite(-.9f, ht - 5f, 0f, .9f, ht - 1.4f, .02f, Ombre, Maillage.Faces.Avant);
                for (int k = -1; k <= 1; k++) Piece(m, k * .6f - .06f, ht - 5f, k * .6f + .06f, ht - 1.4f, .06f, bois);
                Piece(m, -cote / 2, ht - .6f, cote / 2, ht, .2f, pierre * .9f);
                m.r = cl;
            }
            m.Pyramide(0f, ht, 0f, cote / 2 + .3f, cote / 2 + .3f, 12f, ardoise * .95f);
            m.Boite(-.06f, ht + 12f, -.06f, .06f, ht + 13.6f, .06f, Fer);
            m.Boite(-.45f, ht + 12.9f, -.06f, .45f, ht + 13.02f, .06f, Fer);
            m.r = b;
            // L'abside
            m.r = b.Sous(-L / 2, 0f, 0f, 0f);
            m.Cylindre(0f, -2f, 0f, W / 2 - .2f, H - 1f + 2f, 10, pierre, false);
            m.Cone(0f, H - 1f, 0f, W / 2 + .3f, 4.2f, 10, ardoise * .92f);
            m.r = b;
            vols.Add(new Volume(b, -L / 2 - W / 2, -2f, -W / 2, L / 2, H, W / 2));
            vols.Add(new Volume(b, tx - cote / 2, -2f, -W / 2 - cote * .6f - .2f, tx + cote / 2, ht, -W / 2 - .2f + cote * .4f));
            m.r = sauve;
        }

        /// <summary>Une fenêtre haute à arc brisé (vue de face sur le repère courant).</summary>
        static void Lancette(Maillage m, float u, float v0, float w, float h, Color pierre)
        {
            m.Boite(u - w / 2, v0, 0f, u + w / 2, v0 + h, .015f, Vitre, Maillage.Faces.Avant);
            m.Tri(new Vector3(u + w / 2, v0 + h, .015f), new Vector3(u, v0 + h + w * .7f, .015f), new Vector3(u - w / 2, v0 + h, .015f), Vitre);
            Piece(m, u - w / 2 - .15f, v0 - .15f, u + w / 2 + .15f, v0, .12f, pierre * 1.1f);
            Piece(m, u - w / 2 - .12f, v0, u - w / 2, v0 + h, .08f, pierre * 1.1f);
            Piece(m, u + w / 2, v0, u + w / 2 + .12f, v0 + h, .08f, pierre * 1.1f);
        }

        /// <summary>Une petite chapelle : nef, clocher-mur avec sa cloche, porte, deux baies.</summary>
        public static void Chapelle(Maillage m, Repere b, int graine, List<Volume> vols)
        {
            var sauve = m.r; m.r = b;
            var t = new Tirage(graine);
            Color pierre = t.Parmi(Pierres) * 1.08f, toit = t.Chance(.5f) ? t.Parmi(Ardoises) : t.Parmi(Tuiles);
            const float L = 11f, W = 6.4f, H = 5.5f;
            m.Boite(-L / 2, -2f, -W / 2, L / 2, H, W / 2, pierre);
            ToitDeuxPans(m, L, W, H, 1.1f, .45f, .25f, .2f, toit, pierre * .8f);
            m.Tri(new Vector3(-L / 2, H + W / 2 * 1.1f - .05f, 0f), new Vector3(-L / 2, H, -W / 2), new Vector3(-L / 2, H, W / 2), pierre);
            // Façade (+x) avec le clocher-mur
            var fa = FrameFacade(b, 1, L, W);
            m.r = fa;
            float yp = H + W / 2 * 1.1f;
            m.Tri(new Vector3(0f, yp - .05f, 0f), new Vector3(-W / 2, H, 0f), new Vector3(W / 2, H, 0f), pierre);
            m.Boite(-1.2f, H - .5f, -.3f, 1.2f, yp + 2.6f, .3f, pierre);
            m.Pyramide(0f, yp + 2.6f, 0f, 1.35f, .45f, .9f, pierre * .85f);
            m.Boite(-.55f, yp + .5f, .3f, .55f, yp + 1.9f, .31f, Ombre, Maillage.Faces.Avant);
            m.r = fa.Sous(0f, yp + .9f, .1f, 0f);
            m.Cone(0f, 0f, 0f, .38f, .7f, 8, C("#b08a3a"));
            m.r = fa;
            m.Boite(-.75f, 0f, 0f, .75f, 2.6f, .03f, Portes[0], Maillage.Faces.Avant);
            Piece(m, -1f, 2.6f, 1f, 2.95f, .14f, pierre * 1.1f);
            Piece(m, -1f, 0f, -.75f, 2.6f, .12f, pierre * 1.1f);
            Piece(m, .75f, 0f, 1f, 2.6f, .12f, pierre * 1.1f);
            m.r = b;
            foreach (int f in new[] { 0, 2 })
            {
                m.r = FrameFacade(b, f, L, W);
                Lancette(m, -2f, 1.8f, .7f, 2.2f, pierre);
                Lancette(m, 2f, 1.8f, .7f, 2.2f, pierre);
            }
            m.r = b;
            vols.Add(new Volume(b, -L / 2, -2f, -W / 2, L / 2, H, W / 2));
            m.r = sauve;
        }

        /// <summary>La halle du marché : un grand toit sur des piliers de bois, des étals dessous.</summary>
        public static void Halle(Maillage m, Repere b, float L, float W, int graine, List<Volume> vols)
        {
            var sauve = m.r; m.r = b;
            var t = new Tirage(graine);
            Color bois = t.Parmi(Bois), toit = t.Parmi(Tuiles), pierre = t.Parmi(Pierres);
            m.Boite(-L / 2 - .3f, -1.5f, -W / 2 - .3f, L / 2 + .3f, .25f, W / 2 + .3f, pierre);
            int nx = Math.Max(2, (int)(L / 4f)), nz = Math.Max(2, (int)(W / 4f));
            for (int i = 0; i <= nx; i++)
                for (int k = 0; k <= nz; k++)
                {
                    if (i != 0 && i != nx && k != 0 && k != nz && (i + k) % 2 == 1) continue;
                    float x = -L / 2 + i * L / nx, z = -W / 2 + k * W / nz;
                    m.Boite(x - .4f, .25f, z - .4f, x + .4f, .7f, z + .4f, pierre * 1.1f);
                    m.Boite(x - .17f, .7f, z - .17f, x + .17f, 3.4f, z + .17f, bois);
                    m.Boite(x - .17f, 2.9f, z - .6f, x + .17f, 3.2f, z + .6f, bois);
                    vols.Add(new Volume(b, x - .2f, 0f, z - .2f, x + .2f, 3.4f, z + .2f));
                }
            for (int k = 0; k <= nz; k++)
            {
                float z = -W / 2 + k * W / nz;
                m.Boite(-L / 2 - .1f, 3.4f, z - .2f, L / 2 + .1f, 3.75f, z + .2f, bois * 1.1f);
            }
            ToitPavillon(m, L, W, 3.75f, Math.Min(L, W) * .45f, .9f, toit);
            // Les étals
            for (int i = 0; i < nx; i++)
                foreach (float z in new[] { -W / 4, W / 4 })
                {
                    float x = -L / 2 + (i + .5f) * L / nx;
                    m.Boite(x - 1.1f, .25f, z - .5f, x + 1.1f, 1.05f, z + .5f, bois * 1.25f);
                    for (int k = 0; k < 4; k++)
                        m.Boite(x - .9f + k * .55f, 1.05f, z - .3f, x - .6f + k * .55f, 1.05f + t.Entre(.1f, .35f), z + .3f, t.Parmi(Fleurs) * .8f);
                }
            m.r = sauve;
        }

        /// <summary>Un étal en plein air : tréteaux, marchandises, toile tendue sur quatre perches.</summary>
        public static void Etal(Maillage m, Repere b, int graine, List<Volume> vols)
        {
            var sauve = m.r; m.r = b;
            var t = new Tirage(graine);
            Color bois = t.Parmi(Bois) * 1.2f, toile = t.Parmi(Toiles);
            m.Boite(-1.2f, 0f, -.55f, 1.2f, .9f, .55f, bois);
            for (int k = 0; k < 5; k++) m.Boite(-1f + k * .45f, .9f, -.35f, -.75f + k * .45f, .9f + t.Entre(.08f, .32f), .35f, t.Parmi(Fleurs) * .8f);
            foreach (float x in new[] { -1.3f, 1.3f })
                foreach (float z in new[] { -.8f, .8f })
                    m.Boite(x - .05f, 0f, z - .05f, x + .05f, z > 0 ? 2.2f : 2.5f, z + .05f, bois * .7f);
            m.Dalle(new Vector3(1.45f, 2.5f, -.95f), new Vector3(-1.45f, 2.5f, -.95f), new Vector3(-1.45f, 2.2f, .95f), new Vector3(1.45f, 2.2f, .95f), .04f, toile, toile * .8f);
            vols.Add(new Volume(b, -1.2f, 0f, -.55f, 1.2f, .9f, .55f));
            m.r = sauve;
        }

        /// <summary>La forge : bâtiment de pierre, appentis ouvert sur la rue, foyer rougeoyant, enclume, grosse cheminée.</summary>
        public static void Forge(Maillage m, Repere b, int graine, List<Volume> vols)
        {
            var sauve = m.r;
            var t = new Tirage(graine);
            var o = new OptionsMaison { largeur = 6.5f, profondeur = 6f, etages = 1, hauteurEtage = 3.2f, style = StyleMur.Pierre, toiture = Toiture.Tuiles, graine = graine, cheminee = false };
            Varier(o, t, false);
            Maison(m, b.Sous(-2f, 0f, -1f, 0f), o, vols);
            m.r = b;
            // L'appentis ouvert, côté rue (+z)
            Color bois = o.bois;
            m.Boite(-5.2f, -1f, .8f, 1.2f, .25f, 5.2f, o.pierre * .9f);
            foreach (float x in new[] { -4.9f, 1f })
            {
                m.Boite(x - .15f, .25f, 4.75f, x + .15f, 2.9f, 5.05f, bois);
                vols.Add(new Volume(b, x - .15f, 0f, 4.75f, x + .15f, 2.9f, 5.05f));
            }
            m.Dalle(new Vector3(1.6f, 3.6f, 1.9f), new Vector3(-5.6f, 3.6f, 1.9f), new Vector3(-5.6f, 2.85f, 5.6f), new Vector3(1.6f, 2.85f, 5.6f), .15f, o.toit, bois);
            // Le foyer et la cheminée
            m.Boite(-4.6f, .25f, 2.2f, -2.8f, 1.2f, 3.6f, o.pierre * .8f);
            m.Boite(-4.3f, 1.2f, 2.5f, -3.1f, 1.32f, 3.3f, Braise);
            m.Boite(-4.4f, 1.2f, 2.2f, -3f, 7.4f, 2.9f, o.pierre * .85f);
            m.Boite(-4.55f, 7.4f, 2.05f, -2.85f, 7.6f, 3.05f, o.pierre * .7f);
            // L'enclume, le baquet, la meule
            m.Boite(-1.5f, .25f, 3.3f, -1f, .85f, 3.8f, Bois[3]);
            m.Boite(-1.75f, .85f, 3.35f, -.75f, 1.12f, 3.75f, Fer);
            m.r = b.Sous(-.2f, .25f, 4.3f, 0f);
            m.Cylindre(0f, 0f, 0f, .42f, .55f, 10, bois, true);
            m.r = b;
            vols.Add(new Volume(b, -4.6f, 0f, 2.2f, -2.8f, 1.3f, 3.6f));
            m.r = sauve;
        }

        /// <summary>L'auberge : grande maison à trois niveaux, enseigne pendue à une potence, bancs et tonneaux devant.</summary>
        public static void Auberge(Maillage m, Repere b, int graine, List<Volume> vols)
        {
            var t = new Tirage(graine);
            var o = new OptionsMaison { largeur = 11f, profondeur = 8f, etages = 3, hauteurEtage = 2.7f, style = StyleMur.Colombage, toiture = Toiture.Tuiles, graine = graine, encorbellement = true };
            Varier(o, t, true);
            o.lucarne = true;
            Maison(m, b, o, vols);
            var sauve = m.r;
            var fa = FrameFacade(b, 0, o.largeur, o.profondeur);
            m.r = fa;
            // Potence et enseigne
            float u = o.largeur / 2 - .6f;
            m.Boite(u - .06f, 3.4f, 0f, u + .06f, 3.52f, 1.5f, Fer);
            m.Boite(u - .4f, 2.4f, 1.0f, u + .4f, 3.3f, 1.08f, o.enseigneCouleur, Maillage.Faces.Toutes);
            m.Boite(u - .3f, 2.55f, 1.08f, u + .3f, 3.15f, 1.1f, C("#e8c050"), Maillage.Faces.Avant);
            // Bancs et tonneaux
            foreach (float x in new[] { -3f, 2.2f })
            {
                m.Boite(x - .9f, -.4f, .9f, x + .9f, .45f, 1.25f, o.bois * 1.2f);
                vols.Add(new Volume(fa, x - .9f, -.4f, .9f, x + .9f, .45f, 1.25f));
            }
            m.r = fa.Sous(-o.largeur / 2 + .6f, -.3f, .8f, 0f);
            m.Cylindre(0f, 0f, 0f, .38f, 1.0f, 10, o.bois * 1.3f, true);
            m.Cylindre(.85f, 0f, .1f, .38f, 1.0f, 10, o.bois * 1.3f, true);
            m.r = sauve;
        }

        /// <summary>Une grange : murs de planches, grand toit qui descend bas, grande porte.</summary>
        public static void Grange(Maillage m, Repere b, int graine, List<Volume> vols)
        {
            var t = new Tirage(graine);
            var o = new OptionsMaison { largeur = t.Entre(10f, 14f), profondeur = t.Entre(7f, 9f), etages = 1, hauteurEtage = 3.4f, style = StyleMur.Planches, toiture = t.Chance(.5f) ? Toiture.Chaume : Toiture.Tuiles, graine = graine, cheminee = false, aveugle = true };
            Varier(o, t, false);
            o.lucarne = false;
            o.pente = t.Entre(.9f, 1.15f);
            Maison(m, b, o, vols);
            var sauve = m.r;
            m.r = FrameFacade(b, 0, o.largeur, o.profondeur);
            m.Boite(-1.6f, .45f, 0f, 1.6f, 3.4f, .03f, o.bois * .7f, Maillage.Faces.Avant);
            Piece(m, -1.6f, 3.4f, 1.6f, 3.6f, .08f, o.bois * .6f);
            Bande(m, -1.5f, .5f, 1.5f, 3.3f, .14f, .05f, o.bois * .6f);
            m.r = sauve;
        }

        /// <summary>Un moulin à vent : tour ronde de pierre, toit conique, quatre ailes entoilées.</summary>
        public static void MoulinAVent(Maillage m, Repere b, int graine, List<Volume> vols)
        {
            var sauve = m.r; m.r = b;
            var t = new Tirage(graine);
            Color pierre = t.Parmi(Pierres) * 1.1f, toit = t.Parmi(Chaumes), bois = t.Parmi(Bois);
            m.Cylindre(0f, -1.5f, 0f, 3.2f, 10f, 14, pierre, false, 2.6f);
            m.Cone(0f, 8.5f, 0f, 3.1f, 3.8f, 14, toit);
            m.r = FrameFacade(b, 0, 5.2f, 5.2f);
            m.Boite(-.55f, 0f, .35f, .55f, 2.1f, .38f, Portes[0], Maillage.Faces.Avant);
            m.r = b.Sous(0f, 8.6f, 2.9f, 0f);
            m.Boite(-.3f, -.3f, 0f, .3f, .3f, 1.1f, bois);
            for (int k = 0; k < 4; k++)
            {
                float a = (k * 90f + 20f) * Mathf.Deg2Rad, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                // L'aile : une perche et sa toile, dans le plan z = 1
                var p0 = new Vector3(ca * .4f, sa * .4f, 1f); var p1 = new Vector3(ca * 7.5f, sa * 7.5f, 1f);
                m.Bande(p0.x, p0.y, p1.x, p1.y, .22f, 1.0f, bois);
                var q0 = new Vector3(ca * 1.6f - sa * .2f, sa * 1.6f + ca * .2f, 1.04f);
                var q1 = new Vector3(ca * 7.4f - sa * .2f, sa * 7.4f + ca * .2f, 1.04f);
                var q2 = new Vector3(ca * 7.4f - sa * 1.5f, sa * 7.4f + ca * 1.5f, 1.04f);
                var q3 = new Vector3(ca * 1.6f - sa * 1.5f, sa * 1.6f + ca * 1.5f, 1.04f);
                m.Quad(q1, q0, q3, q2, C("#e8e0cc"));
                m.Quad(q2, q3, q0, q1, C("#d8d0bc"));
            }
            m.r = b;
            vols.Add(new Volume(b, -3f, -1.5f, -3f, 3f, 8.5f, 3f));
            m.r = sauve;
        }

        /// <summary>Un puits : margelle ronde, deux poteaux, un petit toit, le seau.</summary>
        public static void Puits(Maillage m, Repere b, int graine, List<Volume> vols)
        {
            var sauve = m.r; m.r = b;
            var t = new Tirage(graine);
            Color pierre = t.Parmi(Pierres) * 1.1f, bois = t.Parmi(Bois), toit = t.Parmi(Tuiles);
            m.Cylindre(0f, -.5f, 0f, 1.05f, 1.35f, 12, pierre, false);
            m.Cylindre(0f, .7f, 0f, .8f, .02f, 12, Ombre, true);
            foreach (float x in new[] { -.95f, .95f }) m.Boite(x - .09f, .8f, -.09f, x + .09f, 2.6f, .09f, bois);
            m.Boite(-1f, 1.9f, -.06f, 1f, 2.02f, .06f, bois);
            ToitDeuxPans(m, 2.2f, 1.6f, 2.55f, .9f, .2f, .15f, .08f, toit, bois);
            m.Boite(-.15f, 1.3f, -.15f, .15f, 1.6f, .15f, Bois[0] * 1.3f);
            vols.Add(new Volume(b, -1.05f, -.5f, -1.05f, 1.05f, .85f, 1.05f));
            m.r = sauve;
        }

        /// <summary>Une fontaine : bassin octogonal, colonne, vasque.</summary>
        public static void Fontaine(Maillage m, Repere b, int graine, List<Volume> vols)
        {
            var sauve = m.r; m.r = b;
            var t = new Tirage(graine);
            Color pierre = t.Parmi(Pierres) * 1.15f, eau = C("#6f9ac8");
            m.Cylindre(0f, -.5f, 0f, 2.6f, 1.15f, 8, pierre, false);
            m.Cylindre(0f, .45f, 0f, 2.35f, .02f, 8, eau, true);
            m.Cylindre(0f, .4f, 0f, .35f, 1.9f, 8, pierre, false);
            m.Cylindre(0f, 2.3f, 0f, .9f, .35f, 8, pierre, true, 1.05f);
            m.Cylindre(0f, 2.65f, 0f, .2f, .7f, 8, pierre, true);
            vols.Add(new Volume(b, -2.5f, -.5f, -2.5f, 2.5f, .7f, 2.5f));
            m.r = sauve;
        }

        /// <summary>Un entrepôt de port : grande halle de pierre, portes de charge, toit d'ardoise, caisses.</summary>
        public static void Entrepot(Maillage m, Repere b, int graine, List<Volume> vols)
        {
            var t = new Tirage(graine);
            var o = new OptionsMaison { largeur = t.Entre(13f, 17f), profondeur = t.Entre(8f, 10f), etages = 2, hauteurEtage = 3f, style = StyleMur.Pierre, toiture = Toiture.Ardoise, graine = graine, cheminee = false, aveugleRez = true };
            Varier(o, t, false);
            o.lucarne = true;
            Maison(m, b, o, vols);
            var sauve = m.r;
            var fa = FrameFacade(b, 0, o.largeur, o.profondeur);
            m.r = fa;
            foreach (float u in new[] { -o.largeur / 4, o.largeur / 4 })
            {
                m.Boite(u - 1.3f, .45f, 0f, u + 1.3f, 3.2f, .03f, o.bois * .75f, Maillage.Faces.Avant);
                Piece(m, u - 1.45f, 3.2f, u + 1.45f, 3.45f, .1f, o.pierre * 1.15f);
            }
            for (int k = 0; k < 5; k++)
            {
                float u = t.Entre(-o.largeur / 2 + .6f, o.largeur / 2 - .6f), s = t.Entre(.6f, .9f);
                m.Boite(u - s / 2, -.3f, .4f, u + s / 2, s - .3f, .4f + s, o.bois * 1.3f);
            }
            m.r = sauve;
        }

        // ---------- défenses ----------

        public static readonly Color PierreRempart = C("#9d978c");

        /// <summary>Un tronçon de rempart entre deux points (au sol) : mur épais, chemin de ronde, créneaux côté extérieur (−z local).</summary>
        public static void Rempart(Maillage m, Vector3 a, Vector3 bb, float hauteur, float epaisseur, Color pierre, List<Volume> vols)
        {
            var sauve = m.r;
            var d = new Vector2(bb.x - a.x, bb.z - a.z);
            float L = d.magnitude;
            if (L < .5f) return;
            float lacet = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg - 90f;   // x local le long du mur
            float y0 = Math.Min(a.y, bb.y);
            var r = Repere.Lacet(new Vector3((a.x + bb.x) * .5f, y0, (a.z + bb.z) * .5f), lacet);
            m.r = r;
            float h = hauteur + Math.Abs(a.y - bb.y);
            m.Boite(-L / 2 - .05f, -2.5f, -epaisseur / 2, L / 2 + .05f, h, epaisseur / 2, pierre, Maillage.Faces.SansDessous);
            m.Boite(-L / 2 - .05f, -2.5f, -epaisseur / 2 - .35f, L / 2 + .05f, 1.2f, -epaisseur / 2, pierre * .9f, Maillage.Faces.Arriere | Maillage.Faces.Dessus);
            int n = Math.Max(1, (int)(L / 1.7f));
            for (int i = 0; i < n; i++)
            {
                float x0 = -L / 2 + i * L / n, x1 = x0 + L / n * .55f;
                m.Boite(x0, h, -epaisseur / 2, x1, h + 1.1f, -epaisseur / 2 + .5f, pierre * 1.04f);
            }
            m.Boite(-L / 2, h, epaisseur / 2 - .35f, L / 2, h + .9f, epaisseur / 2, pierre * 1.02f);
            vols.Add(new Volume(r, -L / 2, -2.5f, -epaisseur / 2, L / 2, h + 1.1f, epaisseur / 2));
            m.r = sauve;
        }

        /// <summary>Une tour ronde, crénelée, coiffée ou non d'une poivrière.</summary>
        public static void Tour(Maillage m, Repere b, float rayon, float hauteur, bool poivriere, Color pierre, Color toit, List<Volume> vols)
        {
            var sauve = m.r; m.r = b;
            m.Cylindre(0f, -2.5f, 0f, rayon + .4f, 3.5f, 14, pierre * .92f, false, rayon);
            m.Cylindre(0f, 1f, 0f, rayon, hauteur - 1f, 14, pierre, true);
            m.Cylindre(0f, hauteur - .6f, 0f, rayon + .35f, .6f, 14, pierre * .9f, true);
            for (int k = 0; k < 4; k++)
            {
                var fr = b.Sous(0f, 0f, 0f, k * 90f + 45f);
                m.r = fr;
                m.Boite(-.12f, hauteur * .45f, rayon - .05f, .12f, hauteur * .45f + 1f, rayon + .01f, Ombre, Maillage.Faces.Avant);
            }
            m.r = b;
            if (poivriere) m.Cone(0f, hauteur, 0f, rayon + .6f, rayon * 2.3f, 14, toit);
            else m.CreneauxRonds(0f, hauteur, 0f, rayon + .35f, .5f, 1.1f, pierre * 1.03f);
            vols.Add(new Volume(b, -rayon, -2.5f, -rayon, rayon, hauteur, rayon));
            m.r = sauve;
        }

        /// <summary>Une porte de ville : deux tours, un passage voûté (ouvert), une salle au-dessus, des créneaux.</summary>
        public static void PorteDeVille(Maillage m, Repere b, float largeurPassage, float hauteur, Color pierre, Color toit, List<Volume> vols)
        {
            var sauve = m.r; m.r = b;
            float w = largeurPassage, ep = 7f, hp = 4.8f;
            // Le corps au-dessus du passage
            m.Boite(-w / 2 - .1f, hp, -ep / 2, w / 2 + .1f, hauteur, ep / 2, pierre);
            m.Boite(-w / 2, hp - .01f, -ep / 2, w / 2, hp, ep / 2, Ombre, Maillage.Faces.Dessous);
            foreach (float s in new[] { -1f, 1f })
            {
                m.Boite(s > 0 ? w / 2 : -w / 2 - 1.2f, -2.5f, -ep / 2, s > 0 ? w / 2 + 1.2f : -w / 2, hp, ep / 2, pierre);
                vols.Add(new Volume(b, s > 0 ? w / 2 : -w / 2 - 1.2f, -2.5f, -ep / 2, s > 0 ? w / 2 + 1.2f : -w / 2, hp, ep / 2));
                Tour(m, b.Sous(s * (w / 2 + 3f), 0f, -ep / 2 + 1f, 0f), 3.2f, hauteur + 3f, true, pierre, toit, vols);
            }
            // La herse levée et les créneaux
            m.Boite(-w / 2, hp - .7f, -ep / 2 - .05f, w / 2, hp, -ep / 2 + .1f, Fer, Maillage.Faces.Avant | Maillage.Faces.Arriere | Maillage.Faces.Dessous);
            for (int i = 0; i < 5; i++)
            {
                float x0 = -w / 2 - .1f + i * (w + .2f) / 5f;
                m.Boite(x0, hauteur, -ep / 2, x0 + (w + .2f) / 10f, hauteur + 1.1f, -ep / 2 + .5f, pierre);
                m.Boite(x0, hauteur, ep / 2 - .5f, x0 + (w + .2f) / 10f, hauteur + 1.1f, ep / 2, pierre);
            }
            var fa = FrameFacade(b, 0, w, ep);
            m.r = fa;
            m.Boite(-.5f, hauteur - 2.6f, 0f, .5f, hauteur - 1.2f, .02f, Ombre, Maillage.Faces.Avant);
            m.r = FrameFacade(b, 2, w, ep);
            m.Boite(-.5f, hauteur - 2.6f, 0f, .5f, hauteur - 1.2f, .02f, Ombre, Maillage.Faces.Avant);
            vols.Add(new Volume(b, -w / 2, hp, -ep / 2, w / 2, hauteur, ep / 2));
            m.r = sauve;
        }

        /// <summary>
        /// Le château : enceinte carrée flanquée de quatre tours, porte fortifiée face à la ville, grand donjon carré
        /// à tourelles, logis seigneurial. Le repère regarde la ville (+z).
        /// </summary>
        public static void Chateau(Maillage m, Repere b, float cote, int graine, List<Volume> vols)
        {
            var sauve = m.r;
            var t = new Tirage(graine);
            Color pierre = C("#a7a196"), toit = t.Parmi(Ardoises);
            float c = cote / 2;
            var coins = new[] { new Vector3(c, 0f, c), new Vector3(-c, 0f, c), new Vector3(-c, 0f, -c), new Vector3(c, 0f, -c) };
            for (int k = 0; k < 4; k++)
            {
                Vector3 a = coins[k], z = coins[(k + 1) % 4];
                if (k == 0)
                {
                    // Le côté de la porte : deux tronçons autour du passage
                    Rempart(m, b.P(a.x, 0f, a.z), b.P(5.5f, 0f, a.z), 11f, 3f, pierre, vols);
                    Rempart(m, b.P(-5.5f, 0f, a.z), b.P(z.x, 0f, z.z), 11f, 3f, pierre, vols);
                }
                else Rempart(m, b.P(a.x, 0f, a.z), b.P(z.x, 0f, z.z), 11f, 3f, pierre, vols);
                Tour(m, b.Sous(a.x, 0f, a.z, 0f), 4.5f, 16f, true, pierre, toit, vols);
            }
            PorteDeVille(m, b.Sous(0f, 0f, c, 0f), 5f, 12f, pierre, toit, vols);
            // Le donjon
            float dz = -c * .3f, dc = cote * .32f, dh = 30f;
            m.r = b;
            m.Boite(-dc / 2, -3f, dz - dc / 2, dc / 2, dh, dz + dc / 2, pierre * 1.02f);
            for (int f = 0; f < 4; f++)
            {
                var fr = FrameFacade(b.Sous(0f, 0f, dz, 0f), f, dc, dc);
                m.r = fr;
                for (int e = 0; e < 3; e++)
                    for (int i = -1; i <= 1; i += 2)
                        m.Boite(i * dc * .22f - .35f, 8f + e * 7f, 0f, i * dc * .22f + .35f, 9.6f + e * 7f, .02f, Ombre, Maillage.Faces.Avant);
                int n = (int)(dc / 1.6f);
                for (int i = 0; i < n; i += 2) m.Boite(-dc / 2 + i * dc / n, dh, -.5f, -dc / 2 + (i + 1) * dc / n, dh + 1.2f, .3f, pierre);
            }
            m.r = b;
            foreach (var p in new[] { new Vector2(dc / 2, dz + dc / 2), new Vector2(-dc / 2, dz + dc / 2), new Vector2(-dc / 2, dz - dc / 2), new Vector2(dc / 2, dz - dc / 2) })
            {
                m.r = b.Sous(p.x, 0f, p.y, 0f);
                m.Cylindre(0f, dh - 4f, 0f, 1.6f, 6f, 10, pierre, true);
                m.Cone(0f, dh + 2f, 0f, 2f, 4.5f, 10, toit);
            }
            m.r = b;
            vols.Add(new Volume(b, -dc / 2, -3f, dz - dc / 2, dc / 2, dh, dz + dc / 2));
            // Le logis
            var o = new OptionsMaison { largeur = cote * .5f, profondeur = 8f, etages = 3, hauteurEtage = 3f, style = StyleMur.Pierre, toiture = Toiture.Ardoise, graine = graine + 1 };
            Varier(o, t, true);
            o.pierre = pierre;
            o.lucarne = true;
            Maison(m, b.Sous(0f, 0f, -c + 6f, 180f), o, vols);
            m.r = sauve;
        }

        // ---------- le port ----------

        /// <summary>Un ponton de bois sur pilotis, de longueur L selon +z, avec des bittes d'amarrage.</summary>
        public static void Ponton(Maillage m, Repere b, float L, float largeur, float niveauEau, List<Volume> vols)
        {
            var sauve = m.r; m.r = b;
            Color bois = Bois[2] * 1.2f;
            float y = niveauEau - b.o.y + 1.1f;
            m.Boite(-largeur / 2, y - .25f, 0f, largeur / 2, y, L, bois, Maillage.Faces.Toutes);
            for (float z = 1f; z < L; z += 3f)
                foreach (float x in new[] { -largeur / 2 + .2f, largeur / 2 - .2f })
                {
                    m.Boite(x - .14f, y - 8f, z - .14f, x + .14f, y + (z % 6f < 3f ? .5f : -.25f), z + .14f, Bois[0]);
                }
            for (float z = .6f; z < L; z += .9f) m.Boite(-largeur / 2, y, z, largeur / 2, y + .02f, z + .05f, bois * .75f, Maillage.Faces.Dessus);
            vols.Add(new Volume(b, -largeur / 2, y - .3f, 0f, largeur / 2, y, L));
            m.r = sauve;
        }

        /// <summary>Une barque (coque, banc), posée sur l'eau.</summary>
        public static void Barque(Maillage m, Repere b, int graine)
        {
            var sauve = m.r; m.r = b;
            var t = new Tirage(graine);
            Color coque = t.Parmi(Bois) * 1.3f, bande = t.Parmi(Volets);
            const float L = 2.6f, W = .75f, H = .55f;
            var avant = new Vector3(0f, H, L); var arriere = new Vector3(0f, H, -L);
            var g = new Vector3(W, H, 0f); var d = new Vector3(-W, H, 0f); var fond = new Vector3(0f, 0f, 0f);
            var fa = new Vector3(0f, .1f, L * .7f); var fr = new Vector3(0f, .1f, -L * .7f);
            m.Quad(avant, g, fond, fa, coque); m.Quad(d, avant, fa, fond, coque * .95f);
            m.Quad(g, arriere, fr, fond, coque * .9f); m.Quad(arriere, d, fond, fr, coque * .92f);
            m.Tri(avant, d, g, C("#3a2a20"));
            m.Tri(arriere, g, d, C("#3a2a20"));
            m.Boite(-W * .7f, H - .2f, -.15f, W * .7f, H - .1f, .15f, bande, Maillage.Faces.Toutes);
            m.r = sauve;
        }
    }
}
