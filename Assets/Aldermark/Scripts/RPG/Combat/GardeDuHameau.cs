using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// Le garde du hameau : un allié armé (épée et écu) qui patrouille et se bat contre les créatures
    /// qui approchent des maisons. Il ne meurt pas : mis à terre, il se relève quelques secondes plus tard.
    /// </summary>
    public class GardeDuHameau : MonstreBipede
    {
        bool aTerre;

        protected override void Configurer()
        {
            nom = "Garde du hameau";
            camp = Camp.Allie; increvable = true;
            vieBase = 140; attaqueBase = 10; defenseBase = 6; xpBase = 0;
            resistanceRecul = 1.4f;
            vitesseMarche = 1.5f; vitesseCourse = 5f; perception = 18f; laisse = 40f; rayonRepos = 13f;
            rayonCorps = .35f; hauteurCorps = 1.8f; hauteurCentre = 1f; hauteurTete = 1.9f; frequencePas = 4.5f;
            bouclier = true;
            attaques.Add(new AttaqueMonstre
            {
                nom = "Taille", portee = 2.3f, preparation = .4f, frappe = .18f, recuperation = .5f,
                mult = 1f, poussee = .8f, cone = 80f, bond = 2f, recharge = 1.1f,
                poseArmement = new Vector3(-190f, 35f, 0f), poseFrappe = new Vector3(-60f, -35f, 0f),
            });
            attaques.Add(new AttaqueMonstre
            {
                nom = "Coup d'écu", portee = 1.8f, preparation = .3f, frappe = .15f, recuperation = .6f,
                mult = .5f, poussee = 1.6f, cone = 60f, bond = 3f, recharge = 4f, poids = .4f,
                poseArmement = new Vector3(-30f, 0f, 0f), poseFrappe = new Vector3(-80f, 0f, 0f),
            });
        }

        protected override void Construire()
        {
            var peau = Pantin.Mat("#e2b48a", .1f);
            var tabard = Pantin.Mat("#3f5f8c", .05f);
            var chausses = Pantin.Mat("#4a4038", .05f);
            var bottes = Pantin.Mat("#2e241c", .2f);
            var fer = Pantin.Mat("#8e9098", .6f);
            b = Pantin.ConstruireBipede(corps, 1f, peau, tabard, chausses, bottes);
            Pantin.Bloc(b.torse, PrimitiveType.Cube, new Vector3(0, .3f, 0), new Vector3(.46f, .6f, .2f), fer);           // maille sous le tabard
            Pantin.Bloc(b.torse, PrimitiveType.Cube, new Vector3(0, .25f, .12f), new Vector3(.3f, .5f, .04f), tabard);
            Pantin.Bloc(b.torse, PrimitiveType.Cube, new Vector3(0, .3f, .145f), new Vector3(.08f, .3f, .02f), Pantin.Mat("#e8d48a", .3f));
            Pantin.Bloc(b.tete, PrimitiveType.Cylinder, new Vector3(0, .26f, 0), new Vector3(.31f, .1f, .31f), fer);       // casque
            Pantin.Bloc(b.tete, PrimitiveType.Sphere, new Vector3(0, .3f, 0), new Vector3(.3f, .2f, .3f), fer);
            Pantin.Bloc(b.tete, PrimitiveType.Cube, new Vector3(0, .14f, .155f), new Vector3(.035f, .12f, .02f), fer);    // nasal
            // L'épée, dans le prolongement du bras
            var e = Pantin.Os("Epee", b.mainD, Vector3.zero, new Vector3(-20f, 0, 0));
            Pantin.Bloc(e, PrimitiveType.Cube, new Vector3(0, -.08f, 0), new Vector3(.035f, .16f, .035f), Pantin.Mat("#5a3a22", .1f));
            Pantin.Bloc(e, PrimitiveType.Cube, new Vector3(0, -.17f, 0), new Vector3(.2f, .035f, .045f), fer);
            Pantin.Bloc(e, PrimitiveType.Cube, new Vector3(0, -.58f, 0), new Vector3(.05f, .8f, .012f), Pantin.Mat("#c9ccd2", .7f));
            // L'écu sur le bras gauche
            var ecu = Pantin.Os("Ecu", b.brasG, new Vector3(0, -.4f, .1f));
            Pantin.Bloc(ecu, PrimitiveType.Cube, Vector3.zero, new Vector3(.46f, .56f, .05f), Pantin.Mat("#8c3a2e", .2f), new Vector3(0, 0, 0));
            Pantin.Bloc(ecu, PrimitiveType.Cube, new Vector3(0, 0, .03f), new Vector3(.08f, .56f, .02f), Pantin.Mat("#e8d48a", .3f));
        }

        /// <summary>Il ne défend que les abords du hameau.</summary>
        protected override bool PeutCibler(ICible c) => Vector2.Distance(new Vector2(c.Centre.x, c.Centre.z), Monde.Hameau) < 32f;

        protected override void Alerter()
        {
            Sons.JouerA("parade", Centre, .35f, .7f);
            HUD.Toast("Le garde du hameau dégaine !");
        }

        protected override void SurTouche(ICible c, AttaqueMonstre a) => Sons.JouerA("impactChair", c.Centre, .8f, 1f);

        public override void Recevoir(Coup c)
        {
            base.Recevoir(c);
            if (!aTerre && Vie <= 1.01)
            {
                aTerre = true;
                Etourdir(6f);
                HUD.ChiffreTexte(Centre + Vector3.up * .9f, "À terre !", new Color(1f, .7f, .5f));
            }
        }

        protected override void Update()
        {
            base.Update();
            if (aTerre && Etourdi <= 0f) { aTerre = false; Soigner(VieMax * .5); }
            if (Etat == EtatMonstre.Repos || Etat == EtatMonstre.Retour) Soigner(VieMax * .02 * Time.deltaTime);
        }
    }
}
