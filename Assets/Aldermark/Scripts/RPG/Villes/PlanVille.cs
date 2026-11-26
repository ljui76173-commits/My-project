using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    public enum Gabarit { Maison, Auberge, Forge, Eglise, Chapelle, Halle, Grange, MoulinAVent, Puits, Fontaine, Entrepot, Etal, Chateau, Ponton, Barque, Tour, PorteDeVille, Rempart }

    /// <summary>Un bâtiment à construire : son gabarit, sa place (y = sol), son orientation, ses réglages.</summary>
    [Serializable]
    public class Commande
    {
        public Gabarit type;
        public Vector3 position, fin;          // fin : l'autre bout d'un rempart
        public float lacet, a, b;              // a, b : tailles (église, halle, tour, ponton…)
        public int graine;
        public OptionsMaison maison;
        public string nom;                     // pour la carte (auberge, forge, église…)
        public bool lieu;                      // figure sur la carte
    }

    /// <summary>Une emprise au sol (rectangle tourné) : pour que rien ne se chevauche, ni les arbres.</summary>
    public struct Emprise
    {
        public Vector2 c, u, w;   // centre, axes local x et local z
        public float hx, hz;
        public Emprise(Vector2 centre, float lacet, float demiX, float demiZ)
        {
            float a = lacet * Mathf.Deg2Rad;
            c = centre; u = new Vector2(Mathf.Cos(a), -Mathf.Sin(a)); w = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
            hx = demiX; hz = demiZ;
        }
        public Vector2 Coin(int k) => c + u * (k == 0 || k == 3 ? hx : -hx) + w * (k < 2 ? hz : -hz);
        float Rayon(Vector2 axe) => hx * Math.Abs(Vector2.Dot(u, axe)) + hz * Math.Abs(Vector2.Dot(w, axe));
        public bool Chevauche(Emprise o, float marge)
        {
            var d = o.c - c;
            foreach (var axe in new[] { u, w, o.u, o.w })
                if (Math.Abs(Vector2.Dot(d, axe)) >= Rayon(axe) + o.Rayon(axe) + marge) return false;
            return true;
        }
        public bool Contient(Vector2 p, float marge)
        {
            var d = p - c;
            return Math.Abs(Vector2.Dot(d, u)) < hx + marge && Math.Abs(Vector2.Dot(d, w)) < hz + marge;
        }
    }

    /// <summary>Une ville prête à construire : son nom, son centre, ses bâtiments (enregistré en JSON à la génération).</summary>
    [Serializable]
    public class VilleCuite
    {
        public string nom;
        public Vector3 centre;
        public List<Commande> commandes = new List<Commande>();
    }

    [Serializable]
    public class DonneesVilles
    {
        public List<VilleCuite> villes = new List<VilleCuite>();
    }

    /// <summary>
    /// Le plan d'une ville, d'un port, d'un village ou d'un hameau : les rues (les routes qui arrivent, des boulevards
    /// en anneau, des ruelles), la place et ses monuments, puis des maisons serrées le long des rues, porte sur la rue ;
    /// les remparts, leurs tours et leurs portes là où arrivent les routes ; le château sur sa butte ;
    /// les faubourgs et les fermes au-delà. Rien n'est posé dans l'eau, sur une route, ou sur une pente trop forte.
    /// </summary>
    public class PlanVille
    {
        public readonly Site site;
        public readonly List<Commande> commandes = new List<Commande>();
        public readonly List<Emprise> emprises = new List<Emprise>();

        class Rue { public List<Vector2> pts = new List<Vector2>(); public float demi; public int rang; }
        readonly List<Rue> rues = new List<Rue>();
        readonly Tirage t;
        readonly Vector2 c;
        readonly float R;
        float rayonPlace, limite;           // limite : pas de maison au-delà (dans les remparts)
        readonly List<float> entrees = new List<float>();
        Emprise? zoneChateau;
        int graines;

        static readonly string[] NomsAuberges = { "Le Sanglier d'Or", "La Chope Fêlée", "Le Lys Blanc", "Le Cerf Agile", "La Marmite", "Le Repos du Voyageur", "Les Trois Clés", "La Lanterne", "Le Bouc Têtu", "L'Écu de Bronze", "La Belle Étoile", "Le Chaudron" };
        static readonly string[] NomsPort = { "L'Ancre Rouillée", "La Mouette Grise", "Le Filet d'Argent" };
        static readonly string[] Forgerons = { "Hadrien", "Bertrand", "Gautier", "Odon", "Mathis", "Roland", "Anselme", "Thierry" };
        static readonly string[] Saints = { "Saint-Martin", "Saint-Rémi", "Sainte-Anne", "Saint-Jacques", "Sainte-Claire", "Saint-Léger", "Saint-Aubin", "Sainte-Odile" };

        PlanVille(Site s, int graine)
        {
            site = s;
            c = s.position;
            R = s.rayon;
            t = new Tirage(graine);
            graines = graine * 1000;
        }

        public static PlanVille Tracer(Site s, int graine)
        {
            var p = new PlanVille(s, graine);
            switch (s.type)
            {
                case TypeSite.Capitale: p.Ville(true, false); break;
                case TypeSite.Ville: p.Ville(false, false); break;
                case TypeSite.Port: p.Ville(false, true); break;
                case TypeSite.Village: p.Village(); break;
                case TypeSite.Hameau: p.Hameau(); break;
            }
            return p;
        }

        // ---------- outils ----------

        static float Sol(float x, float z) => Geo.Hauteur(x, z);
        static bool Eau(float x, float z) => Geo.NiveauEau(x, z) > Geo.Hauteur(x, z) - .2f;
        Vector2 Dir(float deg) => new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));
        /// <summary>
        /// Le lacet qui tourne l'avant (+z) d'un bâtiment vers une direction. Pour une maison « pignon sur rue »,
        /// le constructeur la tourne lui-même d'un quart de tour : sa porte (+x) regarde aussi par là.
        /// </summary>
        static float LacetVers(Vector2 d) => Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;

        static float DistSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float l2 = ab.sqrMagnitude;
            float u = l2 > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2) : 0f;
            return (p - (a + ab * u)).magnitude;
        }

        float DistanceRues(Vector2 p, out Rue proche)
        {
            float m = float.MaxValue; proche = null;
            foreach (var r in rues)
                for (int k = 0; k < r.pts.Count - 1; k++)
                {
                    float d = DistSegment(p, r.pts[k], r.pts[k + 1]) - r.demi;
                    if (d < m) { m = d; proche = r; }
                }
            return m;
        }

        /// <summary>Peut-on bâtir cette emprise ? (rien dessous, pas sur une rue, pas dans l'eau, pas trop en pente)</summary>
        bool Libre(Emprise e, float marge, float margeRue, out float ySol, float penteMax = 2.6f)
        {
            ySol = 0f;
            foreach (var o in emprises) if ((o.c - e.c).sqrMagnitude < 1600f + (e.hx + e.hz + o.hx + o.hz) * (e.hx + e.hz + o.hx + o.hz) && o.Chevauche(e, marge)) return false;
            if (zoneChateau.HasValue && zoneChateau.Value.Chevauche(e, 2f)) return false;
            float mn = float.MaxValue, mx = float.MinValue;
            for (int k = 0; k < 5; k++)
            {
                Vector2 p = k < 4 ? e.Coin(k) : e.c;
                if (DistanceRues(p, out _) < margeRue) return false;
                if (Geo.DistanceRoute(p.x, p.y) < margeRue) return false;
                if (Eau(p.x, p.y)) return false;
                float h = Sol(p.x, p.y);
                mn = Math.Min(mn, h); mx = Math.Max(mx, h);
            }
            if (mx - mn > penteMax) return false;
            ySol = mn;
            return true;
        }

        Commande Ajouter(Gabarit g, Vector2 p, float y, float lacet, Emprise e, float a = 0f, float b = 0f)
        {
            var k = new Commande { type = g, position = new Vector3(p.x, y, p.y), lacet = lacet, a = a, b = b, graine = ++graines };
            commandes.Add(k);
            emprises.Add(e);
            return k;
        }

        // ---------- les rues ----------

        void EntreesEtRoutes(float rayonEntree)
        {
            foreach (var r in Geo.Routes)
            {
                var rue = new Rue { demi = (r.largeurs.Length > 0 ? r.largeurs[0] : 5f) * .5f + 1.2f, rang = 0 };
                for (int k = 0; k < r.points.Length; k++)
                {
                    var p = r.points[k];
                    float d = (p - c).magnitude;
                    if (d < R * 1.3f + 60f) rue.pts.Add(p);
                    else if (rue.pts.Count > 1) { rues.Add(rue); rue = new Rue { demi = rue.demi, rang = 0 }; }
                    else rue.pts.Clear();
                    if (k > 0)
                    {
                        float d0 = (r.points[k - 1] - c).magnitude;
                        if ((d0 - rayonEntree) * (d - rayonEntree) <= 0f)
                        {
                            var q = (r.points[k - 1] + p) * .5f - c;
                            float ang = Mathf.Atan2(q.y, q.x) * Mathf.Rad2Deg;
                            bool connu = false;
                            foreach (var e in entrees) if (Math.Abs(Mathf.DeltaAngle(e, ang)) < 14f) connu = true;
                            if (!connu) entrees.Add(ang);
                        }
                    }
                }
                if (rue.pts.Count > 1) rues.Add(rue);
            }
        }

        void Anneau(float rayon, float demi, int rang)
        {
            int n = Math.Max(16, (int)(rayon * Mathf.PI * 2f / 8f));
            var rue = new Rue { demi = demi, rang = rang };
            for (int i = 0; i <= n; i++)
            {
                var p = c + Dir(i * 360f / n) * rayon;
                if (zoneChateau.HasValue && zoneChateau.Value.Contient(p, 8f))
                {
                    if (rue.pts.Count > 1) rues.Add(rue);
                    rue = new Rue { demi = demi, rang = rang };
                    continue;
                }
                rue.pts.Add(p);
            }
            if (rue.pts.Count > 1) rues.Add(rue);
        }

        void Radiales(int n, float r0, float r1, float demi)
        {
            float decalage = t.Entre(0f, 360f / n);
            for (int i = 0; i < n; i++)
            {
                float a = decalage + i * 360f / n;
                bool proche = false;
                foreach (var e in entrees) if (Math.Abs(Mathf.DeltaAngle(e, a)) < 360f / n * .45f) proche = true;
                if (proche) continue;
                var rue = new Rue { demi = demi, rang = 2 };
                for (float r = r0; r <= r1; r += 8f)
                {
                    var p = c + Dir(a) * r;
                    if (zoneChateau.HasValue && zoneChateau.Value.Contient(p, 8f)) break;
                    rue.pts.Add(p);
                }
                if (rue.pts.Count > 1) rues.Add(rue);
            }
        }

        // ---------- les maisons le long des rues ----------

        /// <summary>Longe une rue et pose des maisons de part et d'autre, porte sur la rue.</summary>
        void Border(Rue rue, float rMin, float rMax, float espaceMin, float espaceMax, int etagesMin, int etagesMax, float profMin, float profMax, float pOuvert, bool rural)
        {
            for (int cote = -1; cote <= 1; cote += 2)
            {
                float s = t.Entre(0f, 3f);
                float longueur = 0f;
                var cumul = new List<float> { 0f };
                for (int k = 1; k < rue.pts.Count; k++) { longueur += (rue.pts[k] - rue.pts[k - 1]).magnitude; cumul.Add(longueur); }
                int seg = 0;
                while (s < longueur - 3f)
                {
                    float largeur = t.Entre(5.2f, rural ? 8f : 8.8f);
                    float milieu = s + largeur * .5f;
                    while (seg < cumul.Count - 2 && cumul[seg + 1] < milieu) seg++;
                    Vector2 a = rue.pts[seg], b = rue.pts[seg + 1];
                    float u = (milieu - cumul[seg]) / Math.Max(.01f, cumul[seg + 1] - cumul[seg]);
                    Vector2 p = Vector2.Lerp(a, b, u), dir = (b - a).normalized;
                    var n = new Vector2(-dir.y, dir.x) * cote;
                    float prof = t.Entre(profMin, profMax);
                    float retrait = rural ? t.Entre(1.5f, 5f) : t.Entre(.4f, 1f);
                    Vector2 centre = p + n * (rue.demi + retrait + prof * .5f);
                    float d = (centre - c).magnitude;
                    if (d < rMin || d > rMax) { s += largeur * .5f + 1f; continue; }
                    float lacet = LacetVers(-n);
                    var e = new Emprise(centre, lacet, largeur * .5f + .15f, prof * .5f + .15f);
                    if (Libre(e, rural ? 1.5f : .05f, .3f, out float y))
                    {
                        var o = NouvelleMaison(largeur, prof, t.Entier(etagesMin, etagesMax + 1), rural, d);
                        o.rezOuvert = !rural && t.Chance(pOuvert);
                        var k = Ajouter(Gabarit.Maison, centre, y, lacet, e);
                        k.maison = o;
                        s += largeur + t.Entre(espaceMin, espaceMax);
                    }
                    else s += 1.5f;
                }
            }
        }

        OptionsMaison NouvelleMaison(float largeur, float prof, int etages, bool rural, float distCentre)
        {
            var o = new OptionsMaison { largeur = largeur, profondeur = prof, etages = Math.Max(1, etages), graine = ++graines };
            float r = t.Valeur();
            if (rural) o.style = r < .45f ? StyleMur.Colombage : r < .8f ? StyleMur.Pierre : StyleMur.Enduit;
            else o.style = r < .62f ? StyleMur.Colombage : r < .85f ? StyleMur.Pierre : StyleMur.Enduit;
            float rt = t.Valeur();
            o.toiture = rural ? (rt < .4f ? Toiture.Chaume : rt < .85f ? Toiture.Tuiles : Toiture.Ardoise)
                              : (site.royaume == 0 ? (rt < .7f ? Toiture.Tuiles : Toiture.Ardoise) : (rt < .65f ? Toiture.Ardoise : Toiture.Tuiles));
            o.encorbellement = !rural && o.etages > 1 && t.Chance(.6f);
            o.porteSurPignon = largeur < 6.8f && t.Chance(.55f);
            if (o.porteSurPignon) { o.largeur = prof; o.profondeur = largeur; }
            KitBatiment.Varier(o, new Tirage(o.graine), distCentre < R * .5f);
            return o;
        }

        // ---------- poser un monument près d'un point ----------

        Commande Poser(Gabarit g, Vector2 voulu, Vector2 regard, float demiX, float demiZ, float rayonRecherche, float margeRue = .5f, float a = 0f, float b = 0f, float lacetFixe = float.NaN)
        {
            for (int essai = 0; essai < 40; essai++)
            {
                Vector2 p = essai == 0 ? voulu : voulu + Dir(t.Entre(0f, 360f)) * t.Entre(1f, rayonRecherche);
                float lacet = float.IsNaN(lacetFixe) ? LacetVers((regard - p).sqrMagnitude > .01f ? (regard - p).normalized : Vector2.up) : lacetFixe;
                var e = new Emprise(p, lacet, demiX, demiZ);
                if (Libre(e, 1f, margeRue, out float y, 3.5f)) return Ajouter(g, p, y, lacet, e, a, b);
            }
            return null;
        }

        // ---------- les villes ----------

        void Ville(bool capitale, bool port)
        {
            rayonPlace = capitale ? 30f : 21f;
            bool remparts = !port;
            float rw = R - 6f;
            limite = remparts ? rw - 7f : R + 10f;
            EntreesEtRoutes(remparts ? rw : R * .9f);

            // Le château, sur sa butte, la porte vers la ville
            if (capitale)
            {
                var f = site.fort;
                var vers = (c - f).normalized;
                float lacetC = LacetVers(vers);
                zoneChateau = new Emprise(f, lacetC, 33f, 33f);
                commandes.Add(new Commande { type = Gabarit.Chateau, position = new Vector3(f.x, Sol(f.x, f.y), f.y), lacet = lacetC, a = 54f, graine = ++graines, nom = "Château de " + site.nom, lieu = false });
                emprises.Add(zoneChateau.Value);
            }

            // Des boulevards en anneau tous les ~30 m (des îlots serrés, dos à dos), et des ruelles qui les relient
            float[] anneaux = capitale ? new[] { .3f, .44f, .58f, .72f, .86f } : port ? new[] { .45f, .7f } : new[] { .42f, .63f, .84f };
            for (int i = 0; i < anneaux.Length; i++) Anneau(R * anneaux[i], i % 2 == 0 ? 2.8f : 2.4f, 1);
            Radiales(capitale ? 14 : 8, rayonPlace + 6f, (remparts ? rw - 9f : R * .9f), 2f);

            // La place : fontaine, église, halle, étals
            Vector2 cote = Dir(t.Entre(0f, 360f));
            Poser(Gabarit.Fontaine, c + new Vector2(cote.y, -cote.x) * 9f, c, 2.7f, 2.7f, 4f, 0f);
            // La façade de l'église (+x local) regarde la place
            var eglise = Poser(Gabarit.Eglise, c + cote * (rayonPlace + 15f), c, capitale ? 18f : 14f, capitale ? 9.5f : 8f, 14f, .5f, capitale ? 28f : 22f, capitale ? 12f : 10f, LacetVers(-cote) - 90f);
            if (eglise != null)
            {
                eglise.nom = (capitale ? "Cathédrale " : "Église ") + t.Parmi(Saints);
                eglise.lieu = true;
            }
            var halle = Poser(Gabarit.Halle, c - cote * (rayonPlace + 4f), c, capitale ? 10f : 8f, capitale ? 7f : 5.5f, 14f, .5f, capitale ? 18f : 14f, capitale ? 12f : 9f);
            if (halle != null) { halle.nom = "Halle de " + site.nom; halle.lieu = true; }
            for (int i = 0; i < (capitale ? 8 : 4); i++)
            {
                var p = c + Dir(t.Entre(0f, 360f)) * t.Entre(8f, rayonPlace - 2f);
                Poser(Gabarit.Etal, p, c, 1.5f, 1f, 3f, .8f);
            }

            // Auberges et forges près des portes, sur les grandes rues
            int nAub = capitale ? 3 : port ? 2 : 1;
            for (int i = 0; i < nAub && i < Math.Max(1, entrees.Count); i++)
            {
                float a = entrees.Count > 0 ? entrees[i % entrees.Count] + (i >= entrees.Count ? 40f : 0f) : t.Entre(0f, 360f);
                var p = c + Dir(a + t.Entre(-12f, 12f)) * (limite - 22f);
                var k = Poser(Gabarit.Auberge, p, c, 6f, 4.5f, 18f, .5f);
                if (k != null) { k.nom = port && i == 0 ? t.Parmi(NomsPort) : t.Parmi(NomsAuberges); k.lieu = true; }
            }
            for (int i = 0; i < (capitale ? 2 : 1); i++)
            {
                float a = entrees.Count > 0 ? entrees[(i + 1) % entrees.Count] : t.Entre(0f, 360f);
                var p = c + Dir(a + t.Entre(-15f, 15f)) * (limite - 16f);
                var k = Poser(Gabarit.Forge, p, c, 6f, 6f, 18f, .5f);
                if (k != null) { k.nom = "Forge de " + t.Parmi(Forgerons); k.lieu = true; }
            }
            if (capitale)
            {
                // L'hôtel de ville sur la place
                var p = c + Dir(Mathf.Atan2(cote.y, cote.x) * Mathf.Rad2Deg + 90f) * (rayonPlace + 8f);
                var o = new OptionsMaison { largeur = 15f, profondeur = 10f, etages = 3, hauteurEtage = 3.2f, style = StyleMur.Pierre, toiture = Toiture.Ardoise, graine = ++graines, lucarne = true };
                KitBatiment.Varier(o, new Tirage(o.graine), true);
                o.lucarne = true;
                var k = Poser(Gabarit.Maison, p, c, 7.8f, 5.3f, 10f, .5f);
                if (k != null) k.maison = o;
            }
            if (port) Port();

            // Les maisons : grandes rues, boulevards, ruelles
            float rMax = limite;
            foreach (var r in rues.ToArray())
                if (r.rang == 0) Border(r, rayonPlace + 4f, rMax, 0f, .4f, capitale ? 2 : 1, capitale ? 4 : 3, 7f, 10f, .3f, false);
            foreach (var r in rues.ToArray())
                if (r.rang == 1) Border(r, rayonPlace + 4f, rMax, 0f, .8f, capitale ? 2 : 1, 3, 6.5f, 9.5f, .12f, false);
            foreach (var r in rues.ToArray())
                if (r.rang == 2) Border(r, rayonPlace + 4f, rMax, .2f, 1.5f, 1, capitale ? 3 : 2, 6f, 9f, .05f, false);

            if (remparts) Remparts(rw, capitale);

            // Les faubourgs le long des routes, hors les murs, puis les fermes
            foreach (var r in rues.ToArray())
                if (r.rang == 0) Border(r, R + 6f, R + 90f, 3f, 9f, 1, 2, 6f, 8.5f, 0f, true);
            Fermes(R + 30f, R + 200f, capitale ? 8 : 5);
        }

        void Remparts(float rw, bool capitale)
        {
            Color pierre = KitBatiment.PierreRempart, toit = KitBatiment.Ardoises[t.Entier(0, KitBatiment.Ardoises.Length)];
            const float demiPorte = 6f;
            int n = Math.Max(24, (int)(rw * Mathf.PI * 2f / 9f));
            float pasTour = capitale ? 48f : 40f, depuisTour = 0f;
            Vector3? prec = null;
            for (int i = 0; i <= n; i++)
            {
                float a = i * 360f / n;
                var p2 = c + Dir(a) * rw;
                bool porte = false;
                foreach (var e in entrees) if (Math.Abs(Mathf.DeltaAngle(e, a)) * Mathf.Deg2Rad * rw < demiPorte + 3f) porte = true;
                bool chateau = zoneChateau.HasValue && zoneChateau.Value.Contient(p2, 4f);
                var p = new Vector3(p2.x, Sol(p2.x, p2.y), p2.y);
                if (porte || chateau || Eau(p2.x, p2.y)) { prec = null; continue; }
                if (prec.HasValue)
                {
                    commandes.Add(new Commande { type = Gabarit.Rempart, position = prec.Value, fin = p, a = capitale ? 9f : 7f, b = capitale ? 3f : 2.4f });
                    emprises.Add(new Emprise((new Vector2(prec.Value.x, prec.Value.z) + p2) * .5f, LacetVers(new Vector2(-Dir(a).y, Dir(a).x)) , 1.6f, (p2 - new Vector2(prec.Value.x, prec.Value.z)).magnitude * .5f));
                    depuisTour += (p2 - new Vector2(prec.Value.x, prec.Value.z)).magnitude;
                    if (depuisTour > pasTour)
                    {
                        depuisTour = 0f;
                        commandes.Add(new Commande { type = Gabarit.Tour, position = p, lacet = 0f, a = capitale ? 4.2f : 3.4f, b = capitale ? 14f : 11f, graine = t.Chance(.4f) ? 1 : 0 });
                    }
                }
                prec = p;
            }
            foreach (var e in entrees)
            {
                var p2 = c + Dir(e) * rw;
                if (Eau(p2.x, p2.y)) continue;
                var tang = new Vector2(-Dir(e).y, Dir(e).x);
                float lacet = LacetVers(Dir(e)) ;   // le passage (z local) suit la route
                commandes.Add(new Commande { type = Gabarit.PorteDeVille, position = new Vector3(p2.x, Sol(p2.x, p2.y), p2.y), lacet = lacet, a = 5.5f, b = capitale ? 12f : 10f, graine = ++graines });
                emprises.Add(new Emprise(p2, lacet, 9f, 4f));
            }
        }

        void Port()
        {
            // La mer : la direction où l'on trouve de l'eau le plus près
            float meilleur = float.MaxValue; float angle = 0f;
            for (int i = 0; i < 36; i++)
                for (float r = R * .5f; r < R + 160f; r += 8f)
                {
                    var p = c + Dir(i * 10f) * r;
                    if (Eau(p.x, p.y)) { if (r < meilleur) { meilleur = r; angle = i * 10f; } break; }
                }
            if (meilleur == float.MaxValue) return;
            var d = Dir(angle);
            var rivage = c + d * meilleur;
            var tang = new Vector2(-d.y, d.x);
            // Pontons et barques
            for (int k = -1; k <= 1; k++)
            {
                var depart = rivage + tang * (k * 18f) - d * 4f;
                var e = new Emprise(depart + d * 15f, LacetVers(d), 2f, 16f);
                commandes.Add(new Commande { type = Gabarit.Ponton, position = new Vector3(depart.x, Sol(depart.x, depart.y), depart.y), lacet = LacetVers(d), a = 30f, b = 3f, graine = ++graines });
                emprises.Add(e);
                for (int i = 0; i < 2; i++)
                {
                    var pb = depart + d * t.Entre(10f, 26f) + tang * (i == 0 ? 3.5f : -3.5f);
                    commandes.Add(new Commande { type = Gabarit.Barque, position = new Vector3(pb.x, Geo.NiveauEau(pb.x, pb.y) - .35f, pb.y), lacet = LacetVers(d) + t.Entre(-20f, 20f), graine = ++graines });
                }
            }
            // Les entrepôts le long du quai
            for (int k = 0; k < 4; k++)
            {
                var p = rivage - d * 14f + tang * ((k - 1.5f) * 19f);
                Poser(Gabarit.Entrepot, p, p + d, 8.5f, 5f, 10f, .5f);
            }
        }

        // ---------- villages, hameaux, fermes ----------

        void Village()
        {
            rayonPlace = 11f;
            limite = R + 40f;
            EntreesEtRoutes(R);
            Anneau(R * .62f, 2f, 2);
            var cote = Dir(t.Entre(0f, 360f));
            Poser(Gabarit.Puits, c + new Vector2(cote.y, -cote.x) * 6f, c, 1.2f, 1.2f, 4f, .2f);
            var ch = Poser(Gabarit.Chapelle, c + cote * (rayonPlace + 7f), c, 6f, 3.6f, 10f, .5f, 0f, 0f, LacetVers(-cote) - 90f);
            if (ch != null)
            {
                ch.nom = "Chapelle " + t.Parmi(Saints); ch.lieu = true;
            }
            var aub = Poser(Gabarit.Auberge, c - cote * (rayonPlace + 9f), c, 6f, 4.5f, 12f);
            if (aub != null) { aub.nom = t.Parmi(NomsAuberges); aub.lieu = true; }
            var fo = Poser(Gabarit.Forge, c + new Vector2(-cote.y, cote.x) * (rayonPlace + 10f), c, 6f, 6f, 12f);
            if (fo != null) { fo.nom = "Forge de " + t.Parmi(Forgerons); fo.lieu = true; }
            for (int i = 0; i < 2; i++) Poser(Gabarit.Etal, c + Dir(t.Entre(0f, 360f)) * t.Entre(4f, rayonPlace), c, 1.5f, 1f, 3f, .6f);
            foreach (var r in rues.ToArray())
                if (r.rang == 0) Border(r, rayonPlace + 3f, R + 35f, 1.5f, 5f, 1, 2, 6f, 8.5f, .1f, true);
            foreach (var r in rues.ToArray())
                if (r.rang == 2) Border(r, rayonPlace + 3f, R + 10f, 2f, 6f, 1, 2, 6f, 8f, 0f, true);
            Fermes(R + 15f, R + 140f, t.Entier(3, 6));
            if (t.Chance(.6f))
            {
                for (int essai = 0; essai < 20; essai++)
                {
                    var p = c + Dir(t.Entre(0f, 360f)) * t.Entre(R + 40f, R + 110f);
                    var k = Poser(Gabarit.MoulinAVent, p, c, 3.4f, 3.4f, 6f, 2f);
                    if (k != null) { k.nom = "Moulin de " + site.nom; k.lieu = true; break; }
                }
            }
        }

        void Hameau()
        {
            rayonPlace = 6f;
            limite = R + 20f;
            EntreesEtRoutes(R);
            Poser(Gabarit.Puits, c + Dir(t.Entre(0f, 360f)) * 4f, c, 1.2f, 1.2f, 3f, .2f);
            int n = t.Entier(4, 7);
            for (int i = 0; i < n; i++)
            {
                float a = i * 360f / n + t.Entre(-15f, 15f);
                var p = c + Dir(a) * t.Entre(12f, R - 4f);
                float l = t.Entre(6f, 8.5f), pr = t.Entre(5.5f, 7f);
                var k = Poser(Gabarit.Maison, p, c, l * .5f + .2f, pr * .5f + .2f, 6f, .4f);
                if (k != null) k.maison = NouvelleMaison(l, pr, t.Entier(1, 3), true, 999f);
            }
            Fermes(R, R + 70f, t.Entier(1, 3));
        }

        /// <summary>Des fermes dans la campagne : une grange et un corps de logis, souvent près des champs.</summary>
        void Fermes(float r0, float r1, int nombre)
        {
            int faites = 0;
            for (int essai = 0; essai < nombre * 12 && faites < nombre; essai++)
            {
                var p = c + Dir(t.Entre(0f, 360f)) * t.Entre(r0, r1);
                if (Geo.DistanceRoute(p.x, p.y) > 60f) continue;
                float lacet = t.Entre(0f, 360f);
                var e = new Emprise(p, lacet, 7.5f, 5f);
                if (!Libre(e, 3f, 2f, out float y, 2.2f)) continue;
                Ajouter(Gabarit.Grange, p, y, lacet, e);
                var p2 = p + Dir(-lacet + t.Entre(-30f, 30f)) * 14f;
                float l2 = t.Entre(6.5f, 8.5f), pr2 = t.Entre(5.5f, 7f);
                var o = NouvelleMaison(l2, pr2, t.Entier(1, 3), true, 999f);
                var e2 = new Emprise(p2, lacet + 90f, l2 * .5f + .2f, pr2 * .5f + .2f);
                if (Libre(e2, 2f, 2f, out float y2, 2.2f)) Ajouter(Gabarit.Maison, p2, y2, lacet + 90f, e2).maison = o;
                faites++;
            }
        }
    }
}
