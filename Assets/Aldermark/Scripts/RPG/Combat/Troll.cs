using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// Le troll des cavernes : énorme, lent, presque impossible à ébranler. Sa massue projette,
    /// son balayage fauche large, son piétinement fait une onde de choc. Chaque pas fait trembler le sol.
    /// Trop fort pour un héros de niveau 1 : on le contourne, ou on revient plus tard.
    /// </summary>
    public class Troll : MonstreBipede
    {
        float dernierPas;

        protected override void Configurer()
        {
            nom = "Troll des cavernes";
            vieBase = 230; attaqueBase = 20; defenseBase = 8; xpBase = 70;
            FixerAffinite(Element.Feu, 1.75f); FixerAffinite(Element.Terre, .5f); FixerAffinite(Element.Glace, .75f);   // les trolls craignent le feu ; la roche, c'est chez eux
            resistanceRecul = 6f; stature = 1f;
            vitesseMarche = 1.4f; vitesseCourse = 3.6f; perception = 14f; laisse = 28f; rayonRepos = 6f;
            rayonCorps = .85f; hauteurCorps = 2.8f; hauteurCentre = 1.6f; hauteurTete = 3.2f;
            rotationPreparation = 110f; croiseLeFer = true; frequencePas = 2.2f; voute = 18f;
            butin.Add(("sang_troll", 1f, 2, 3));
            butin.Add(("croc_troll", .7f, 1, 1));
            butin.Add((Objets.Pieces, 1f, 30, 60));
            attaques.Add(new AttaqueMonstre
            {
                nom = "Massue", portee = 3.7f, preparation = 1f, frappe = .22f, recuperation = 1.1f,
                mult = 1.4f, poussee = 2.4f, cone = 60f, bond = 1.5f, recharge = 2.5f, lourde = true, deuxMains = true,
                poseArmement = new Vector3(-205f, 0f, 0f), poseFrappe = new Vector3(-45f, 0f, 0f),
            });
            attaques.Add(new AttaqueMonstre
            {
                nom = "Balayage", portee = 3.5f, preparation = .7f, frappe = .25f, recuperation = .8f,
                mult = 1f, poussee = 1.7f, cone = 150f, recharge = 2f, poids = .8f,
                poseArmement = new Vector3(-90f, 75f, 0f), poseFrappe = new Vector3(-90f, -80f, 0f),
            });
            attaques.Add(new AttaqueMonstre
            {
                nom = "Piétinement", portee = 2.6f, preparation = 1.1f, frappe = .15f, recuperation = 1f,
                mult = .9f, poussee = 2.2f, rayonZone = 4.5f, recharge = 6f, poids = .6f, lourde = true,
                poseArmement = new Vector3(-150f, 10f, 0f), poseFrappe = new Vector3(-20f, 0f, 0f),
            });
        }

        protected override void Construire()
        {
            var peau = Pantin.Mat("#6f7a5c", .1f);
            var pagne = Pantin.Mat("#5a4530", .05f);
            var pieds = Pantin.Mat("#59614a", .1f);
            b = Pantin.ConstruireBipede(corps, 1.62f, peau, peau, pagne, pieds, tete: .9f, carrure: 1.45f, bras: 1.35f);
            Pantin.Bloc(b.torse, PrimitiveType.Sphere, new Vector3(0, .3f, .1f), new Vector3(.66f, .55f, .4f), peau);   // ventre
            Pantin.Bloc(b.tete, PrimitiveType.Cube, new Vector3(0, .05f, .14f), new Vector3(.3f, .1f, .08f), peau);    // mâchoire
            foreach (float x in new[] { -.08f, .08f })
                Pantin.Bloc(b.tete, PrimitiveType.Cube, new Vector3(x, .12f, .18f), new Vector3(.03f, .07f, .03f), Pantin.Mat("#e8dfc8", .3f));   // crocs
            // La massue : un tronc noueux
            var m = Pantin.Os("Massue", b.mainD, Vector3.zero, new Vector3(-20f, 0, 0));
            var bois = Pantin.Mat("#5b4029", .05f);
            Pantin.Bloc(m, PrimitiveType.Cylinder, new Vector3(0, -.35f, 0), new Vector3(.1f, .4f, .1f), bois);
            Pantin.Bloc(m, PrimitiveType.Capsule, new Vector3(0, -.95f, 0), new Vector3(.3f, .38f, .3f), bois);
            Pantin.Bloc(m, PrimitiveType.Cube, new Vector3(.13f, -1f, .05f), new Vector3(.12f, .12f, .12f), bois, new Vector3(30f, 20f, 0));
        }

        protected override void Alerter()
        {
            Sons.JouerA("rugissement", Centre, 1f, Random.Range(.9f, 1f));
            if (CombatHeros.Instance != null) CombatHeros.Instance.Secousse(.08f);
            HUD.Toast("Un troll des cavernes t'a repéré !");
        }

        protected override void SurPreparation(AttaqueMonstre a) => Sons.JouerA("rugissement", Centre, .45f, 1.3f);
        protected override void SurTouche(ICible c, AttaqueMonstre a) => Sons.JouerA("impactChair", c.Centre, 1f, .6f);
        protected override void CriDeMort() { Sons.JouerA("rugissement", Centre, 1f, .7f); HUD.Toast("Le troll des cavernes s'effondre !"); }

        protected override void Animer(float dt)
        {
            base.Animer(dt);
            // Chaque pas fait trembler le sol près de lui
            if (vitesseActuelle > .5f && Mathf.Sin(phasePas) * Mathf.Sin(dernierPas) < 0f)
            {
                var h = CombatHeros.Instance;
                if (h != null)
                {
                    float d = Distance(h);
                    if (d < 18f) h.Secousse(.035f * (1f - d / 18f));
                }
                Sons.JouerA("pasTerre", transform.position, .9f, .45f);
            }
            dernierPas = phasePas;
        }

        protected override void AnimerMort(float dt)
        {
            base.AnimerMort(dt);
            if (tEtat - Time.deltaTime < .45f && tEtat >= .45f)
            {
                Effets.Poussiere(transform.position + transform.right * 1.5f, 30);
                if (CombatHeros.Instance != null) CombatHeros.Instance.Secousse(.15f);
                Sons.JouerA("onde", transform.position, 1f, .6f);
            }
        }
    }
}
