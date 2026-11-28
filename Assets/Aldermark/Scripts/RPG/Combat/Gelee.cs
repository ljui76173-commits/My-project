using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// La gelée : une boule molle qui avance par bonds et se jette sur toi.
    /// Une grosse gelée se divise en deux petites quand on la crève.
    /// La gelée de givre, bleue et froide, crache des éclats de glace de loin ; le feu la fait fondre deux fois plus vite.
    /// </summary>
    public class Gelee : Monstre
    {
        /// <summary>Taille de la prochaine gelée créée (1,25 : grosse ; 0,62 : petite).</summary>
        public static float Prochaine = 1.25f;

        float taille;
        Transform boule;
        Vector3 echelle = Vector3.one;

        protected override void Configurer()
        {
            taille = Prochaine;
            Prochaine = 1.25f;
            bool grosse = taille > .9f;
            nom = Lanceur ? (grosse ? "Grosse gelée de givre" : "Gelée de givre") : grosse ? "Grosse gelée" : "Gelée";
            vieBase = grosse ? 22 : 11; attaqueBase = 5; defenseBase = 1; xpBase = 10;
            FixerAffinite(Element.Feu, 2f); FixerAffinite(Element.Foudre, 1.25f); FixerAffinite(Element.Glace, .5f);   // le feu la fait fondre, le froid la fige à peine
            resistanceRecul = .7f;
            vitesseMarche = 1.3f; vitesseCourse = 3.4f; perception = 12f; laisse = 30f; rayonRepos = 6f;
            rayonCorps = .42f * taille; hauteurCorps = .8f * taille; hauteurCentre = .4f * taille; hauteurTete = .85f * taille;
            frequencePas = 5f;
            butin.Add(("gelee", .75f, 1, grosse ? 2 : 1));
            if (grosse) butin.Add(("noyau_gelee", .12f, 1, 1));
            attaques.Add(new AttaqueMonstre
            {
                nom = "Bond", portee = 3.4f, preparation = .65f, frappe = .5f, recuperation = .8f,
                mult = 1f, poussee = .45f, cone = 70f, bond = 6.5f, sautVertical = 4.2f, recharge = 1.6f,
            });
            if (Lanceur)
            {
                vieBase = grosse ? 26 : 13; xpBase = 16;
                FixerAffinite(Element.Feu, 2.5f); FixerAffinite(Element.Glace, 0f); FixerAffinite(Element.Foudre, 1f);
                perception = 16f; distanceDeTir = grosse ? 8f : 0f;
                butin.Add(("gelee", .5f, 1, 1));
                attaques.Add(new AttaqueMonstre
                {
                    nom = "Éclat de glace", sort = CatalogueCompetences.Get("glace"), niveauSort = 1,
                    porteeMin = 3.5f, portee = 15f, preparation = 1f, frappe = .2f, recuperation = .6f, recharge = grosse ? 2.6f : 4f, poids = 2f,
                });
            }
        }

        protected override void Construire()
        {
            boule = Pantin.Os("Boule", corps, Vector3.zero);
            var gel = Pantin.Mat(Lanceur ? "#9fd6ec" : "#5fb85a", .85f);
            Pantin.Bloc(boule, PrimitiveType.Sphere, new Vector3(0, .4f * taille, 0), new Vector3(.95f, .78f, .95f) * taille, gel);
            Pantin.Bloc(boule, PrimitiveType.Sphere, new Vector3(0, .32f * taille, 0), new Vector3(.55f, .4f, .55f) * taille, Lanceur ? Pantin.Lumineux("#6fc4ff") : Pantin.Mat("#3f8f3c", .6f));
            if (Lanceur)
                // Des pointes de givre sur le dessus
                for (int k = 0; k < 6; k++)
                {
                    float ang = k * 60f;
                    Vector3 dir = Quaternion.Euler(0, ang, 0) * Vector3.forward;
                    Pantin.Bloc(boule, PrimitiveType.Cube, (new Vector3(0, .7f, 0) + dir * .22f) * taille, new Vector3(.06f, .18f, .06f) * taille,
                        Pantin.Mat("#e6f6ff", .9f), new Vector3(dir.z * 30f, ang, -dir.x * 30f));
                }
            var noir = Pantin.Mat("#14140f", .9f);
            var blanc = Pantin.Lumineux("#ffffff");
            foreach (float x in new[] { -.15f, .15f })
            {
                Pantin.Bloc(boule, PrimitiveType.Sphere, new Vector3(x, .5f, .4f) * taille, new Vector3(.12f, .16f, .08f) * taille, noir);
                Pantin.Bloc(boule, PrimitiveType.Sphere, new Vector3(x + .025f, .54f, .43f) * taille, Vector3.one * .04f * taille, blanc);
            }
        }

        protected override void Alerter() => Sons.JouerA("gelee", Centre, .5f, 1.4f / taille);

        protected override void SurFrappe(AttaqueMonstre a) => Sons.JouerA("gelee", Centre, .5f, 1.7f / taille);

        protected override void SurTouche(ICible c, AttaqueMonstre a) => Sons.JouerA("gelee", Centre, .8f, 1f / taille);

        protected override void Animer(float dt)
        {
            // Molle : elle s'écrase avant de bondir, s'étire en l'air, rebondit en marchant
            Vector3 cible;
            switch (Etat)
            {
                case EtatMonstre.Preparation:
                    float u = Mathf.Clamp01(tEtat / enCours.preparation);
                    cible = new Vector3(1f + .3f * u, 1f - .35f * u, 1f + .3f * u);
                    break;
                case EtatMonstre.Frappe: cible = new Vector3(.82f, 1.3f, .82f); break;
                case EtatMonstre.Sonne: cible = new Vector3(1.2f, .75f, 1.2f) + Vector3.one * Mathf.Sin(Time.time * 14f) * .05f; break;
                default:
                    float r = Mathf.Abs(Mathf.Sin(phasePas)) * Mathf.Clamp01(vitesseActuelle);
                    cible = new Vector3(1f - .1f * r, 1f + .16f * r, 1f - .1f * r);
                    break;
            }
            echelle = Vector3.Lerp(echelle, cible, Mathf.Clamp01(dt * 14f));
            boule.localScale = echelle;
        }

        protected override void Mourir(Coup c)
        {
            base.Mourir(c);
            Effets.Eclat(Centre, Lanceur ? new Color(.7f, .9f, 1f) : new Color(.4f, .78f, .38f), 18, 3.5f, .2f, 1.6f, .6f);
            Sons.JouerA("gelee", Centre, 1f, .8f / taille);
            if (taille <= .9f) return;
            // La grosse gelée éclate en deux petites, projetées de part et d'autre (perpendiculaire au coup)
            Vector3 coup = Vector3.ProjectOnPlane(c.direction, Vector3.up);
            Vector3 cote = coup.sqrMagnitude > .01f ? Vector3.Cross(Vector3.up, coup.normalized) : transform.right;
            Effets.Eclat(Centre + Vector3.up * .2f, new Color(.45f, .85f, .42f), 30, 5f, .22f, 1.4f, .7f);
            Effets.OndeDeChoc(transform.position, 1.6f);
            HUD.ChiffreTexte(Centre + Vector3.up * .8f, "Elle se divise !", new Color(.6f, 1f, .55f));
            for (int k = -1; k <= 1; k += 2)
            {
                Prochaine = .62f;
                LanceurProchain = Lanceur;   // une gelée de givre se divise en gelées de givre
                var g = Repaire.Creer(Repaire.Espece.Gelee, transform.position + cote * k * .7f + Vector3.up * .15f, Mathf.Max(1, niveau - 1));
                g.origine = origine;
                g.Proteger(.6f);
                g.Pousser(cote * k * 5f + coup.normalized * 1.5f, 5.5f);
                g.Provoquer(c.auteur);
            }
        }

        protected override void AnimerMort(float dt)
        {
            float u = Mathf.Clamp01(tEtat / .25f);
            boule.localScale = new Vector3(1f + .7f * u, Mathf.Lerp(1f, .08f, u), 1f + .7f * u);
            if (tEtat > 2.5f) Destroy(gameObject);
        }
    }
}
