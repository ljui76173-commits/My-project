using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    /// <summary>Un sort sur le point de partir : qui le lance, avec quelle force, d'où, vers où.</summary>
    public struct Incantation
    {
        public DefCompetence def;
        public ICible auteur;
        public Camp camp;
        /// <summary>Attaque magique brute (ou quantité soignée pour un soin).</summary>
        public double attaque;
        public int niveau;
        /// <summary>Le niveau qui règle l'ASPECT du sort (taille, paliers) : celui du sort, plus ce qu'apporte le niveau du héros.
        /// 0 : le niveau du sort (les monstres).</summary>
        public int niveauVisuel;
        public int Visuel => niveauVisuel > 0 ? niveauVisuel : niveau;
        public Vector3 origine, direction;
        /// <summary>La cible verrouillée, s'il y en a une : le projectile s'incurve vers elle, la foudre la frappe.</summary>
        public ICible cible;
        public float rayon, poussee;
        /// <summary>Appelé à chaque cible touchée (vrai si le coup l'a achevée).</summary>
        public Action<ICible, bool> touche;
    }

    /// <summary>
    /// Ce que font les sorts, pour tout le monde (le héros comme les monstres) :
    /// projectiles, traits de foudre, cônes, zones, soins.
    /// Le niveau du sort se voit : chaque niveau grandit et éclaire le sort (Ampleur),
    /// et aux niveaux 4, 7 et 10 (les paliers) il gagne une nouvelle forme visible.
    /// </summary>
    public static partial class Sorts
    {
        /// <summary>Quelqu'un vient de lancer un sort (pour apprendre en regardant).</summary>
        public static event Action<Incantation> Lance;

        static readonly Dictionary<Color, Material> materiaux = new Dictionary<Color, Material>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reinitialiser() { Lance = null; materiaux.Clear(); }

        /// <summary>
        /// La taille et l'éclat d'un sort selon son niveau : 1 au niveau 1, 1,9 au niveau 10, puis ça continue sans plafond
        /// (plus lentement : 2,9 au niveau 20, 3,9 au 50, 4,6 au 100), pour que l'écran reste lisible.
        /// </summary>
        public static float Ampleur(int niveau)
        {
            niveau = Mathf.Max(1, niveau);
            if (niveau <= 10) return 1f + .1f * (niveau - 1);
            return 1.9f + .9f * Mathf.Log(1f + (niveau - 10) / 5f);
        }

        /// <summary>
        /// Une progression qui suit le niveau jusqu'à 10 puis plafonne à 9 : pour le son et les petits réglages,
        /// qui ne doivent pas déraper quand l'aspect d'un sort monte à 100 ou 200.
        /// </summary>
        public static float Prog(int niveau) => Mathf.Min(9f, (Ampleur(niveau) - 1f) * 10f);

        /// <summary>Les niveaux (d'aspect) où un sort change : 4, 7, 10 (maîtrisé), puis 15, 25, 40, 60, 100, 160, 250.</summary>
        static readonly int[] SeuilsPaliers = { 1, 4, 7, 10, 15, 25, 40, 60, 100, 160, 250 };

        /// <summary>1 à 11 : le palier atteint (4 : maîtrisé ; 5 : transcendé ; 6 : céleste ; 7 : cataclysme ; 8 : légendaire ; 9 : divin ; 10 : mythique ; 11 : absolu).</summary>
        public static int Palier(int niveau)
        {
            int p = 1;
            for (int k = 1; k < SeuilsPaliers.Length; k++) if (niveau >= SeuilsPaliers[k]) p = k + 1;
            return p;
        }

        /// <summary>Le niveau où commence un palier (0 s'il n'y en a plus).</summary>
        public static int DebutPalier(int palier) => palier >= 1 && palier <= SeuilsPaliers.Length ? SeuilsPaliers[palier - 1] : 0;

        public static string NomPalier(int palier)
        {
            switch (palier)
            {
                case 4: return "maîtrisé";
                case 5: return "transcendé";
                case 6: return "céleste";
                case 7: return "cataclysme";
                case 8: return "légendaire";
                case 9: return "divin";
                case 10: return "mythique";
                case 11: return "absolu";
                default: return "";
            }
        }

        /// <summary>Ce qui change à l'œil à ce palier : propre au sort jusqu'au niveau 10, puis commun à tous les sorts.</summary>
        public static string DescriptionPalier(DefCompetence d, int palier)
        {
            if (palier < 2) return "";
            if (d.paliers != null && palier - 2 < d.paliers.Length) return d.paliers[palier - 2];
            switch (palier)
            {
                case 5: return "chaque impact creuse un cratère, et une colonne d'énergie tombe du ciel";
                case 6: return "le jour s'assombrit pendant l'incantation, et un cercle immense s'ouvre dans le ciel";
                case 7: return "le temps s'arrête au lancer ; triple onde de choc, cratère géant";
                case 8: return "tout est plus grand : cercles, explosions, cratères";
                case 9: return "la puissance d'un dieu : l'écran blanchit au lancer";
                case 10: return "les cercles emplissent le ciel, les cratères deviennent des gouffres";
                case 11: return "chaque sort fait trembler le monde";
                default: return "";
            }
        }

        /// <summary>Un matériau translucide et lumineux, partagé par couleur.</summary>
        public static Material Materiau(Color c)
        {
            if (materiaux.TryGetValue(c, out var m) && m != null) return m;
            m = Effets.Transparent(c);
            materiaux[c] = m;
            return m;
        }

        public static bool Hostile(Camp auteur, Camp cible) =>
            auteur == Camp.Heros || auteur == Camp.Allie ? cible != Camp.Heros && cible != Camp.Allie : Monstre.Ennemis(auteur, cible);

        public static void Lancer(Incantation s)
        {
            if (s.def == null) return;
            if (s.direction.sqrMagnitude < .001f) s.direction = Vector3.forward;
            s.direction.Normalize();
            if (s.niveau < 1) s.niveau = 1;
            Lance?.Invoke(s);
            switch (s.def.forme)
            {
                case FormeSort.Projectile: ProjectileSort.Creer(s); break;
                case FormeSort.Trait: Trait(s); break;
                case FormeSort.Cone: Cone(s); break;
                case FormeSort.Zone: Zone(s); break;
                case FormeSort.Soin: Soin(s); break;
            }
        }

        /// <summary>Un sort atteint une cible : le coup magique, son effet (selon sa chance), l'éclat à l'impact.</summary>
        public static void Toucher(Incantation s, ICible c, Vector3 direction, double mult = 1)
        {
            if (!Cibles.Existe(c) || !c.Vivant) return;
            var d = s.def;
            bool effet = d.effet != Effet.Aucun && UnityEngine.Random.value < d.chanceEffet;
            bool vivant = c.Vivant;
            Vector3 centre = c.Centre;
            c.Recevoir(new Coup
            {
                degats = s.attaque * mult, direction = direction, poussee = s.poussee, lourd = d.lourd, magique = true,
                auteur = s.auteur, nom = d.nom, element = d.element, effet = effet ? d.effet : Effet.Aucun, dureeEffet = d.dureeEffet,
                sort = d.id,
            });
            s.touche?.Invoke(c, vivant && Cibles.Existe(c) && !c.Vivant);
            Impact(d.element, centre, direction, s.Visuel);
        }

        /// <summary>L'éclat d'un sort qui frappe (une cible, un mur, le sol), d'autant plus grand que le sort est haut.</summary>
        public static void Impact(Element e, Vector3 p, Vector3 direction, int niveau = 1)
        {
            float a = Ampleur(niveau);
            int pal = Palier(niveau);
            float kn = 1f + Prog(niveau);   // 1 à 10, pour le son et les lueurs
            Color c = CatalogueCompetences.CouleurElement(e);
            Vector3 sol = new Vector3(p.x, Monde.Hauteur(p.x, p.z), p.z);
            bool presDuSol = p.y - sol.y < 3f;
            switch (e)
            {
                case Element.Feu:
                    Effets.Eclat(p, new Color(1f, .55f, .15f), Mathf.RoundToInt(22 * a), 4.5f * Mathf.Sqrt(a), .2f * a, -.4f, .6f);
                    Effets.Eclat(p, new Color(1f, .9f, .5f), Mathf.RoundToInt(10 * a), 7f * a, .07f, .8f, .4f);
                    Effets.Eclat(p, new Color(.25f, .2f, .18f, .5f), Mathf.RoundToInt(6 * a), 1.2f, .45f * a, -.25f, 1.1f);   // fumée
                    Sons.JouerA("sortFeu", p, .7f + .03f * kn, .78f - .02f * kn);
                    Lueur(p, c, 5f * a, .25f + .02f * kn);
                    if (pal >= 2) Effets.Anneau(p, -direction, .9f * a, new Color(1f, .6f, .2f, .8f));
                    if (pal >= 3 && presDuSol) Brasier(sol, .9f * a, 2.6f);
                    if (pal >= 4)
                    {
                        Effets.Eclat(p, new Color(.65f, .82f, 1f), 24, 6f, .16f, -.5f, .5f);
                        if (presDuSol) Effets.OndeDeChoc(sol, 2.6f, new Color(1f, .55f, .2f, .85f));
                    }
                    break;
                case Element.Glace:
                    Effets.Eclat(p, new Color(.85f, .96f, 1f), Mathf.RoundToInt(18 * a), 5f * Mathf.Sqrt(a), .09f * a, 1.6f, .55f, -direction);
                    Effets.Eclat(p, new Color(.6f, .85f, 1f, .6f), Mathf.RoundToInt(8 * a), 1.5f, .3f * a, -.05f, .5f + .05f * kn);
                    Sons.JouerA("sortGlace", p, .7f + .03f * kn, 1.15f - .02f * kn);
                    if (pal >= 2) Effets.Anneau(p, -direction, .8f * a, new Color(.75f, .93f, 1f, .75f), .45f);
                    if (pal >= 3 && presDuSol) Pics(sol, pal >= 4 ? 9 : 5, .5f * a, .55f * a, new Color(.7f, .9f, 1f, .7f), true, 1.4f);
                    break;
                case Element.Foudre:
                    Effets.Eclat(p, new Color(.9f, .92f, 1f), Mathf.RoundToInt(16 * a), 6f * a, .06f, .5f, .3f);
                    Lueur(p, c, 8f * a, .15f + .02f * kn);
                    if (pal >= 3 && presDuSol)
                    {
                        Effets.OndeDeChoc(sol, 1.2f * a, new Color(.8f, .85f, 1f, .8f));
                        Effets.Sillon(sol, direction);
                    }
                    break;
                case Element.Terre:
                    for (int k = 0; k < Mathf.CeilToInt(a); k++) Effets.Terre(p, Vector3.up);
                    break;
                case Element.Lumiere:
                    Effets.Eclat(p, c, Mathf.RoundToInt(12 * a), 1.5f, .12f, -.4f, .8f);
                    break;
                default:
                    Effets.Eclat(p, c, Mathf.RoundToInt(10 * a), 3f, .12f, .4f, .4f);
                    break;
            }
        }

        // ---------- les formes ----------

        /// <summary>La foudre : instantanée, elle frappe la cible verrouillée ou ce qu'on vise ; au niveau 5, elle rebondit.</summary>
        static void Trait(Incantation s)
        {
            var d = s.def;
            Vector3 fin = s.origine + s.direction * d.portee;
            ICible touchee = null;
            if (Valide(s, s.cible) && Vector3.Distance(s.origine, s.cible.Centre) <= d.portee + 2f)
            {
                touchee = s.cible;
                fin = s.cible.Centre;
            }
            else if (Premier(s, s.origine, s.direction, d.portee, .35f, out var c, out var point))
            {
                touchee = c;
                fin = c != null ? c.Centre : point;
            }
            float kt = Prog(s.Visuel);
            Eclair(s.origine, fin, d.couleur, s.Visuel);
            Sons.JouerA("sortFoudre", fin, .92f + .02f * kt, Mathf.Max(.72f, UnityEngine.Random.Range(.92f, 1.08f) - .03f * kt), 90f + 6f * kt);
            Deflagration(d.element, fin, s.direction, s.Visuel, s.camp);
            if (touchee == null) { Impact(d.element, fin, s.direction, s.Visuel); return; }
            Toucher(s, touchee, (fin - s.origine).normalized);
            if (s.niveau < 5) return;
            // Le rebond : sur l'ennemi le plus proche de la première cible
            Combattant suivant = null;
            float best = 7f;
            foreach (var cb in Combattant.Tous)
            {
                if (ReferenceEquals(cb, touchee) || !Valide(s, cb)) continue;
                float dist = Vector3.Distance(cb.Centre, fin);
                if (dist < best) { best = dist; suivant = cb; }
            }
            if (suivant == null) return;
            Eclair(fin, suivant.Centre, d.couleur, s.Visuel);
            Toucher(s, suivant, (suivant.Centre - fin).normalized, .6);
        }

        /// <summary>Le vent : tout ce qui est devant, dans le cône, est frappé et repoussé.</summary>
        static void Cone(Incantation s)
        {
            var d = s.def;
            float a = Ampleur(s.Visuel);
            int pal = Palier(s.Visuel);
            Vector3 avant = Vector3.ProjectOnPlane(s.direction, Vector3.up).normalized;
            if (avant.sqrMagnitude < .01f) avant = s.direction;
            Vector3 pied = s.auteur != null && Cibles.Existe(s.auteur) ? s.auteur.Corps.position : s.origine;
            Effets.Eclat(s.origine, new Color(.9f, .97f, .93f, .55f), Mathf.RoundToInt(46 * a), 15f * Mathf.Sqrt(a), .3f * a, 0f, .45f, s.direction);
            Effets.Eclat(s.origine, new Color(1f, 1f, 1f, .8f), Mathf.RoundToInt(20 * a), 22f * Mathf.Sqrt(a), .06f, 0f, .3f, s.direction);
            int lignes = 3 + pal;
            for (int k = 1; k <= lignes; k++)
            {
                Vector3 p = pied + avant * k * d.portee / lignes;
                Effets.Poussiere(p, Mathf.RoundToInt(5 * a));
                if (pal >= 2) Effets.Eclat(p + Vector3.up * .2f, new Color(.45f, .6f, .25f), 4, 5f, .09f, .4f, .8f, avant + Vector3.up * .5f);   // herbes et feuilles
            }
            if (pal >= 3)
            {
                // Des tourbillons au bout du souffle
                Vector3 droite = Vector3.Cross(Vector3.up, avant);
                foreach (float cote in pal >= 4 ? new[] { -1f, 0f, 1f } : new[] { -1f, 1f })
                    Tourbillon(pied + avant * d.portee * .9f + droite * cote * d.portee * .35f, 1.8f, 1.1f * a);
            }
            if (pal >= 5)
            {
                // Une tempête : des tourbillons tout le long du souffle, et la caméra qui tremble
                Vector3 droite = Vector3.Cross(Vector3.up, avant);
                for (int k = 0; k < 4 + pal; k++)
                    Tourbillon(pied + avant * d.portee * UnityEngine.Random.Range(.3f, 1.2f) + droite * UnityEngine.Random.Range(-1f, 1f) * d.portee * .5f, 2.5f * a, 1.3f * a);
                var hh = CombatHeros.Instance;
                if (hh != null) hh.Secousse(.05f * a);
            }
            if (pal >= 4)
            {
                Vector3 o = s.origine;
                Effets.Anneau(o + avant * 1.5f, avant, 3.5f, new Color(.92f, 1f, .96f, .55f), .4f);
                Retard.Lancer(.12f, () => Effets.Anneau(o + avant * 3.5f, avant, 5f, new Color(.92f, 1f, .96f, .4f), .4f));
            }
            Sons.JouerA("sortVent", s.origine, .93f + .03f * Prog(s.Visuel), UnityEngine.Random.Range(.92f, 1.08f) - .02f * Prog(s.Visuel));
            foreach (var c in Autour(s, pied, d.portee))
            {
                Vector3 vers = Vector3.ProjectOnPlane(c.Centre - pied, Vector3.up);
                if (vers.magnitude > d.portee || (vers.magnitude > .8f && Vector3.Angle(avant, vers) > d.angle * .5f)) continue;
                Toucher(s, c, vers.sqrMagnitude > .01f ? vers.normalized : avant);
            }
        }

        /// <summary>La terre : une onde autour du lanceur qui frappe, soulève et étourdit.</summary>
        static void Zone(Incantation s)
        {
            float a = Ampleur(s.Visuel);
            int pal = Palier(s.Visuel);
            Vector3 centre = s.auteur != null && Cibles.Existe(s.auteur) ? s.auteur.Corps.position : s.origine;
            centre.y = Monde.Hauteur(centre.x, centre.z);
            float rayon = s.rayon > 0f ? s.rayon : s.def.rayon;
            Effets.OndeDeChoc(centre, rayon, new Color(.85f, .72f, .5f, .85f));
            Deflagration(Element.Terre, centre + Vector3.up * .4f, Vector3.up, s.Visuel, s.camp);
            int gerbes = Mathf.Min(40, 10 + Mathf.RoundToInt(2 * (a - 1f) * 10f));
            for (int k = 0; k < gerbes; k++)
            {
                float ang = k * Mathf.PI * 2f / gerbes;
                Vector3 p = centre + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * rayon * UnityEngine.Random.Range(.45f, .9f);
                p.y = Monde.Hauteur(p.x, p.z);
                Effets.Terre(p, Vector3.up * 2f * a + (p - centre).normalized);
                Effets.Sillon(p, p - centre);
            }
            if (pal >= 2) Pics(centre, pal >= 4 ? 16 : pal >= 3 ? 11 : 7, rayon * .75f, 1.1f * a, new Color(.48f, .4f, .32f), false, 1.1f);
            if (pal >= 3)
                Retard.Lancer(.3f, () =>
                {
                    Effets.OndeDeChoc(centre, rayon * 1.35f, new Color(.85f, .72f, .5f, .6f));
                    for (int k = 0; k < 8; k++)
                    {
                        float ang = k * Mathf.PI * .25f + .4f;
                        Vector3 p = centre + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * rayon * 1.2f;
                        p.y = Monde.Hauteur(p.x, p.z);
                        Effets.Terre(p, Vector3.up * 2.5f);
                    }
                });
            if (pal >= 4)
                for (int k = 0; k < 12; k++)
                {
                    float ang = k * Mathf.PI / 6f;
                    var dir = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
                    Effets.Sillon(centre + dir * 1.2f, dir);
                }
            Sons.JouerA("sortTerre", centre, .93f + .03f * Prog(s.Visuel), UnityEngine.Random.Range(.9f, 1.05f) - .025f * Prog(s.Visuel), 80f + 6f * Prog(s.Visuel));
            var h = CombatHeros.Instance;
            if (h != null) h.Secousse(Mathf.Clamp01(1f - Vector3.Distance(h.transform.position, centre) / 25f) * .14f * a);
            foreach (var c in Autour(s, centre, rayon))
            {
                Vector3 vers = Vector3.ProjectOnPlane(c.Centre - centre, Vector3.up);
                if (vers.magnitude > rayon) continue;
                Vector3 dir = vers.sqrMagnitude > .01f ? vers.normalized : Vector3.forward;
                Toucher(s, c, dir);
                if (c is Monstre m && m.Vivant && m.stature < 1f) m.Pousser(dir * 2.5f, 5.5f);
            }
        }

        /// <summary>Le soin : sur le lanceur lui-même.</summary>
        static void Soin(Incantation s)
        {
            var c = s.auteur;
            if (!Cibles.Existe(c) || !c.Vivant) return;
            float a = Ampleur(s.Visuel);
            int pal = Palier(s.Visuel);
            double avant = c.VieActuelle;
            if (c is CombatHeros && StatsHeros.Instance != null) StatsHeros.Instance.Rendre(s.attaque);
            else if (c is Combattant cb) cb.Rendre(s.attaque);
            double rendu = c.VieActuelle - avant;
            Vector3 p = c.Corps.position;
            int n = 6 + 2 * (pal - 1);
            for (int k = 0; k < n; k++)
            {
                float ang = k * Mathf.PI * 2f / n;
                Effets.Eclat(p + new Vector3(Mathf.Cos(ang) * .6f * a, .2f, Mathf.Sin(ang) * .6f * a), s.def.couleur, Mathf.RoundToInt(6 * a), .8f, .14f, -.9f, 1.1f);
            }
            Effets.Eclat(c.Centre, new Color(1f, 1f, .85f), Mathf.RoundToInt(14 * a), 1.2f, .1f, -.6f, 1f);
            Lueur(c.Centre, s.def.couleur, 6f * a, .6f);
            if (pal >= 2) Effets.OndeDeChoc(p, 1.6f * a, new Color(.6f, 1f, .55f, .8f));
            if (pal >= 3) Colonne(p, .55f * a, 5f, new Color(.75f, 1f, .7f, .35f));
            if (pal >= 4)
                for (int k = 0; k < 5; k++)
                    Effets.Eclat(p + Vector3.up * 3.5f + UnityEngine.Random.insideUnitSphere * .8f, new Color(1f, .9f, .5f), 8, .6f, .1f, .35f, 2f);
            Sons.JouerA("sortSoin", c.Centre, .9f, 1f - .015f * Prog(s.Visuel));
            Vector3 ou = c is CombatHeros ? c.Centre + Vector3.up * .6f : c.Centre + Vector3.up * .9f;
            HUD.ChiffreTexte(ou, "+" + Nombres.Court(Math.Max(0, rendu)), new Color(.55f, 1f, .55f));
        }

        // ---------- utilitaires ----------

        public static bool Valide(Incantation s, ICible c) =>
            Cibles.Existe(c) && c.Vivant && !ReferenceEquals(c, s.auteur) && Hostile(s.camp, c.Camp);

        /// <summary>Les cibles hostiles autour d'un point (le héros compris, si c'est un monstre qui lance).</summary>
        static List<ICible> Autour(Incantation s, Vector3 centre, float rayon)
        {
            var l = new List<ICible>();
            var h = CombatHeros.Instance;
            if (h != null && Valide(s, h) && Vector3.Distance(h.Centre, centre) <= rayon + 1.5f) l.Add(h);
            foreach (var c in Combattant.Tous.ToArray())
                if (Valide(s, c) && Vector3.Distance(c.Centre, centre) <= rayon + 1.5f) l.Add(c);
            return l;
        }

        /// <summary>
        /// Ce qu'un sort rencontre en premier sur sa trajectoire : une cible hostile (c), ou un obstacle (c nul, point d'impact).
        /// Les alliés, le lanceur et les zones invisibles sont traversés.
        /// </summary>
        public static bool Premier(Incantation s, Vector3 depuis, Vector3 dir, float distance, float rayon, out ICible c, out Vector3 point)
        {
            c = null;
            point = depuis + dir * distance;
            var hits = Physics.SphereCastAll(depuis, rayon, dir, distance, ~0, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
            foreach (var hit in hits)
            {
                var cible = hit.collider.GetComponentInParent<ICible>();
                if (cible != null)
                {
                    if (ReferenceEquals(cible, s.auteur) || !cible.Vivant) continue;
                    if (!Hostile(s.camp, cible.Camp)) continue;
                    c = cible;
                    point = hit.distance > 0f ? hit.point : depuis;
                    return true;
                }
                if (hit.collider.isTrigger) continue;
                point = hit.distance > 0f ? hit.point : depuis;
                return true;
            }
            // Le relief (le terrain a son collider, mais au cas où)
            Vector3 bout = depuis + dir * distance;
            if (bout.y < Monde.Hauteur(bout.x, bout.z))
            {
                for (int k = 1; k <= 8; k++)
                {
                    Vector3 p = depuis + dir * distance * k / 8f;
                    if (p.y < Monde.Hauteur(p.x, p.z)) { point = p; return true; }
                }
            }
            return false;
        }
    }
}
