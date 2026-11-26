using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// Le gobelin : petit, vicieux, armé d'un gourdin. Il ricane, frappe à deux contre toi au plus,
    /// lève son gourdin à deux mains pour un coup lourd, et s'enfuit quand il est blessé.
    /// Son gourdin peut se bloquer contre ton épée (choc des lames).
    /// </summary>
    public class Gobelin : MonstreBipede
    {
        protected override void Configurer()
        {
            nom = "Gobelin";
            vieBase = 40; attaqueBase = 8; defenseBase = 3; xpBase = 20;
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

        protected override void Construire()
        {
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
