using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// Le gobelin : petit, vicieux, armé d'un gourdin. Il ricane, frappe à deux contre toi au plus,
    /// lève son gourdin à deux mains pour un coup lourd, et s'enfuit quand il est blessé.
    /// Son gourdin peut se bloquer contre ton épée (choc des lames).
    /// Le chaman (un par camp au moins) : bâton surmonté d'une flamme, il garde ses distances,
    /// lance Flamme et se soigne quand il est blessé. On peut apprendre ses sorts en le regardant.
    /// </summary>
    public class Gobelin : MonstreBipede
    {
        protected override void Configurer()
        {
            if (Lanceur) { ConfigurerChaman(); return; }
            nom = "Gobelin";
            vieBase = 40; attaqueBase = 8; defenseBase = 3; xpBase = 20;
            FixerAffinite(Element.Foudre, 1.25f);
            resistanceRecul = 1f;
            vitesseMarche = 1.9f; vitesseCourse = 5.2f; perception = 16f; laisse = 34f; rayonRepos = 8f;
            rayonCorps = .32f; hauteurCorps = 1.3f; hauteurCentre = .75f; hauteurTete = 1.45f;
            maxAttaquants = 2; fuyard = true; croiseLeFer = true; frequencePas = 5.5f;
            butin.Add((Objets.Pieces, .9f, 2, 9));
            butin.Add(("oreille_gobelin", .6f, 1, 1));
            butin.Add(("gourdin_gobelin", .22f, 1, 1));
            butin.Add(("pain", .2f, 1, 1));
            attaques.Add(new AttaqueMonstre
            {
                nom = "Coup de gourdin", portee = 2.1f, preparation = .5f, frappe = .18f, recuperation = .6f,
                mult = 1f, poussee = .6f, cone = 80f, bond = 2.5f, recharge = 1.4f,
                poseArmement = new Vector3(-200f, 20f, 0f), poseFrappe = new Vector3(-50f, -10f, 0f),
            });
            attaques.Add(new AttaqueMonstre
            {
                nom = "Gourdin à deux mains", portee = 2.3f, preparation = .9f, frappe = .2f, recuperation = .9f,
                mult = 1.6f, poussee = 1.2f, cone = 70f, bond = 3f, recharge = 4f, poids = .5f, lourde = true, deuxMains = true,
                poseArmement = new Vector3(-215f, 0f, 0f), poseFrappe = new Vector3(-40f, 0f, 0f),
            });
        }

        void ConfigurerChaman()
        {
            nom = "Gobelin chaman";
            vieBase = 32; attaqueBase = 9; defenseBase = 2; xpBase = 32;
            FixerAffinite(Element.Foudre, 1.25f); FixerAffinite(Element.Feu, .6f);
            resistanceRecul = .9f;
            vitesseMarche = 1.7f; vitesseCourse = 4.4f; perception = 22f; laisse = 34f; rayonRepos = 6f;
            rayonCorps = .32f; hauteurCorps = 1.3f; hauteurCentre = .75f; hauteurTete = 1.45f;
            maxAttaquants = 2; distanceDeTir = 11f; frequencePas = 5f;
            butin.Add((Objets.Pieces, .95f, 4, 14));
            butin.Add(("oreille_gobelin", .6f, 1, 1));
            butin.Add(("pain", .25f, 1, 1));
            attaques.Add(new AttaqueMonstre
            {
                nom = "Coup de bâton", portee = 2f, preparation = .55f, frappe = .18f, recuperation = .7f,
                mult = .7f, poussee = .5f, cone = 80f, bond = 1.5f, recharge = 1.6f, poids = 1f,
                poseArmement = new Vector3(-190f, 20f, 0f), poseFrappe = new Vector3(-55f, -10f, 0f),
            });
            attaques.Add(new AttaqueMonstre
            {
                nom = "Flamme", sort = CatalogueCompetences.Get("flamme"), niveauSort = 1,
                porteeMin = 3.5f, portee = 22f, preparation = 1.15f, frappe = .2f, recuperation = .7f, recharge = 3.4f, poids = 3f,
                poseArmement = new Vector3(-125f, 0f, 0f), poseFrappe = new Vector3(-85f, 0f, 0f),
            });
            attaques.Add(new AttaqueMonstre
            {
                nom = "Soin", sort = CatalogueCompetences.Get("soin"), niveauSort = 1,
                porteeMin = 0f, portee = 99f, preparation = 1.7f, frappe = .2f, recuperation = .6f, recharge = 13f, poids = 6f,
                condition = () => Vie < VieMax * .5,
                poseArmement = new Vector3(-160f, 0f, 0f), poseFrappe = new Vector3(-120f, 0f, 0f),
            });
        }

        protected override void Construire()
        {
            if (Lanceur) { ConstruireChaman(); return; }
            var peau = Pantin.Mat("#7f9a4c", .15f);
            var haillons = Pantin.Mat("#6b5238", .05f);
            var cuir = Pantin.Mat("#4a3524", .1f);
            var corpsGob = Pantin.Os("Gabarit", corps, Vector3.zero);
            b = Pantin.ConstruireBipede(corpsGob, .74f, peau, haillons, cuir, cuir, tete: 1.35f, carrure: .95f, bras: 1.15f);
            // Oreilles pointues, nez, yeux jaunes
            foreach (float x in new[] { -1f, 1f })
                Pantin.Bloc(b.tete, PrimitiveType.Cube, new Vector3(x * .26f, .26f, 0), new Vector3(.2f, .06f, .09f), peau, new Vector3(0, x * 15f, x * -25f));
            Pantin.Bloc(b.tete, PrimitiveType.Cube, new Vector3(0, .17f, .22f), new Vector3(.08f, .1f, .12f), peau, new Vector3(20f, 0, 0));
            foreach (float x in new[] { -.08f, .08f })
                Pantin.Bloc(b.tete, PrimitiveType.Cube, new Vector3(x, .25f, .19f), new Vector3(.06f, .04f, .02f), Pantin.Lumineux("#ffd84a"));
            // Le gourdin, dans le prolongement du bras
            var g = Pantin.Os("Gourdin", b.mainD, Vector3.zero, new Vector3(-25f, 0, 0));
            var bois = Pantin.Mat("#6e4b2e", .1f);
            Pantin.Bloc(g, PrimitiveType.Cylinder, new Vector3(0, -.22f, 0), new Vector3(.06f, .22f, .06f), bois);
            Pantin.Bloc(g, PrimitiveType.Capsule, new Vector3(0, -.55f, 0), new Vector3(.17f, .2f, .17f), bois);
            Pantin.Bloc(g, PrimitiveType.Cube, new Vector3(.06f, -.6f, 0), new Vector3(.05f, .05f, .05f), Pantin.Mat("#5a5a60", .5f), new Vector3(0, 0, 45f));
        }

        void ConstruireChaman()
        {
            var peau = Pantin.Mat("#6f8a46", .15f);
            var robe = Pantin.Mat("#4b2f5e", .05f);
            var cuir = Pantin.Mat("#3a2a1e", .1f);
            var corpsGob = Pantin.Os("Gabarit", corps, Vector3.zero);
            b = Pantin.ConstruireBipede(corpsGob, .74f, peau, robe, robe, cuir, tete: 1.35f, carrure: .9f, bras: 1.15f);
            foreach (float x in new[] { -1f, 1f })
                Pantin.Bloc(b.tete, PrimitiveType.Cube, new Vector3(x * .26f, .26f, 0), new Vector3(.2f, .06f, .09f), peau, new Vector3(0, x * 15f, x * -25f));
            Pantin.Bloc(b.tete, PrimitiveType.Cube, new Vector3(0, .17f, .22f), new Vector3(.08f, .1f, .12f), peau, new Vector3(20f, 0, 0));
            // Des yeux qui brûlent, une coiffe de plumes, un collier d'os
            foreach (float x in new[] { -.08f, .08f })
                Pantin.Bloc(b.tete, PrimitiveType.Cube, new Vector3(x, .25f, .19f), new Vector3(.06f, .04f, .02f), Pantin.Lumineux("#ff8a2a"));
            var plume = Pantin.Mat("#c0402a", .1f);
            for (int k = -2; k <= 2; k++)
                Pantin.Bloc(b.tete, PrimitiveType.Cube, new Vector3(k * .06f, .5f, -.05f), new Vector3(.035f, .26f, .015f), k % 2 == 0 ? plume : Pantin.Mat("#e8d6a8", .1f), new Vector3(-15f, 0, k * -14f));
            var os = Pantin.Mat("#e8dfc8", .3f);
            for (int k = -2; k <= 2; k++)
                Pantin.Bloc(b.torse, PrimitiveType.Cube, new Vector3(k * .07f, .28f - Mathf.Abs(k) * .02f, .17f), new Vector3(.035f, .06f, .03f), os);
            // Le bâton, et la flamme qui danse au bout
            var baton = Pantin.Os("Baton", b.mainD, Vector3.zero, new Vector3(-25f, 0, 0));
            var bois = Pantin.Mat("#5b3e26", .1f);
            Pantin.Bloc(baton, PrimitiveType.Cylinder, new Vector3(0, -.15f, 0), new Vector3(.045f, .62f, .045f), bois);
            Pantin.Bloc(baton, PrimitiveType.Cube, new Vector3(0, -.74f, 0), new Vector3(.12f, .06f, .12f), os, new Vector3(0, 45f, 0));
            bout = Pantin.Bloc(baton, PrimitiveType.Sphere, new Vector3(0, -.86f, 0), Vector3.one * .13f, Pantin.Lumineux("#ff9a3a"));
        }

        protected override void Alerter()
        {
            Sons.JouerA("ricanement", Centre, .8f, Random.Range(.95f, 1.15f));
            foreach (var m in Actifs)
                if (m is Gobelin g && g != this && g.Cible == null && (g.transform.position - transform.position).sqrMagnitude < 30f * 30f)
                    g.Provoquer(cible);
        }

        protected override void SurPreparation(AttaqueMonstre a) { if (a.lourde) Sons.JouerA("ricanement", Centre, .6f, .8f); }
        protected override void SurFuite() { Sons.JouerA("ricanement", Centre, .9f, 1.5f); HUD.ChiffreTexte(Centre + Vector3.up * .9f, "S'enfuit !", new Color(.85f, .9f, .6f)); }
        protected override void SurTouche(ICible c, AttaqueMonstre a) => Sons.JouerA("impactChair", c.Centre, .8f, .8f);
        protected override void CriDeMort() => Sons.JouerA("ricanement", Centre, .8f, .6f);
    }
}
