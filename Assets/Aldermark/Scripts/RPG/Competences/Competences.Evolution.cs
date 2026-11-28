using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// Évolution et fusion. Un sort maîtrisé (niveau 10) peut évoluer en l'une de ses deux branches : il se transforme,
    /// garde son niveau, son expérience et ses raccourcis. Deux éléments maîtrisés (un sort de chaque au niveau 10)
    /// ouvrent une fusion : un sort nouveau, en plus des tiens.
    /// </summary>
    public partial class Competences
    {
        readonly HashSet<string> fusionsAnnoncees = new HashSet<string>();

        public bool PeutEvoluer(string id)
        {
            var m = Get(id);
            var d = m?.Def;
            return d != null && d.evolutions != null && d.evolutions.Length > 0 && m.niveau >= CatalogueCompetences.NiveauMaitrise;
        }

        /// <summary>Le sort se transforme en l'une de ses branches (niveau, expérience et raccourcis gardés).</summary>
        public bool Evoluer(string id, string vers)
        {
            var m = Get(id);
            var d = m?.Def;
            var nd = CatalogueCompetences.Get(vers);
            if (!PeutEvoluer(id) || nd == null || System.Array.IndexOf(d.evolutions, vers) < 0 || Connait(vers)) return false;
            parId.Remove(id);
            m.id = vers;
            m.pret = 0f;
            parId[vers] = m;
            for (int k = 0; k < Raccourcis; k++) if (raccourcis[k] == id) raccourcis[k] = vers;
            Trier();
            HUD.Message("ÉVOLUTION", $"{d.nom} devient <b>{nd.nom}</b> !\n\n{nd.description}\n\nNiveau {m.niveau}, et ça continue.", 7f);
            Sons.Fanfare("niveau");
            Celebrer(nd.couleur, Sorts.Ampleur(NiveauVisuel(vers)));
            Apprise?.Invoke(nd);
            Exploits.Compter("evolutions");
            VerifierFusions(true);
            return true;
        }

        /// <summary>Un élément est maîtrisé quand au moins un sort de cet élément a atteint le niveau 10.</summary>
        public bool ElementMaitrise(Element e)
        {
            foreach (var m in connues)
                if (m.Def != null && m.Def.EstSort && m.Def.element == e && m.niveau >= CatalogueCompetences.NiveauMaitrise) return true;
            return false;
        }

        public List<DefCompetence> FusionsPossibles()
        {
            var l = new List<DefCompetence>();
            foreach (var (a, b, res) in CatalogueCompetences.Fusions)
            {
                var d = CatalogueCompetences.Get(res);
                if (d != null && !Connait(res) && ElementMaitrise(a) && ElementMaitrise(b)) l.Add(d);
            }
            return l;
        }

        public bool Fusionner(string resultat)
        {
            var d = CatalogueCompetences.Get(resultat);
            if (d == null || !FusionsPossibles().Contains(d)) return false;
            Apprendre(resultat, false);
            string a = CatalogueCompetences.NomElement(d.fusionDe[0]), b = CatalogueCompetences.NomElement(d.fusionDe[1]);
            HUD.Message("FUSION", $"{a} + {b} : <b>{d.nom}</b> !\n\n{d.description}\n\nTu gardes tes autres sorts. Touche {Touche(resultat)} · fenêtre Compétences (K)", 7f);
            Sons.Fanfare("niveau");
            Celebrer(d.couleur, 1.6f);
            Exploits.Compter("fusions");
            VerifierFusions(true);
            return true;
        }

        /// <summary>Prévient (une fois) quand une nouvelle fusion devient possible.</summary>
        void VerifierFusions(bool annoncer)
        {
            foreach (var d in FusionsPossibles())
            {
                if (fusionsAnnoncees.Contains(d.id)) continue;
                fusionsAnnoncees.Add(d.id);
                if (annoncer) HUD.Toast($"Fusion possible : <color=#{ColorUtility.ToHtmlStringRGB(d.couleur)}>{d.nom}</color> (fenêtre Compétences, K)");
            }
        }

        /// <summary>Une gerbe de lumière autour du héros (évolution, fusion).</summary>
        void Celebrer(Color c, float a)
        {
            Vector3 p = transform.position + Vector3.up;
            Effets.Eclat(p, c, Mathf.RoundToInt(60 * a), 5f * a, .16f, -.4f, 1.6f);
            Effets.Eclat(p, Color.white, 30, 7f, .06f, -.2f, 1f);
            Effets.OndeDeChoc(transform.position, 4f * a, new Color(c.r, c.g, c.b, .85f));
            Sorts.Colonne(transform.position, .8f, 18f, new Color(c.r, c.g, c.b, .35f));
            Sorts.Lueur(p, c, 12f, 1f);
        }
    }
}
