using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// Le loup gris chasse en meute : le premier qui te voit hurle et appelle les autres ;
    /// un seul mord à la fois pendant que les autres tournent autour de toi ;
    /// de loin, il bondit (lueur rouge : coup lourd).
    /// Le loup d'orage, des montagnes : pelage bleu sombre strié de lumière, il garde ses distances et crache la foudre.
    /// </summary>
    public class Loup : Monstre
    {
        Transform cou, queue;
        readonly Transform[] pattes = new Transform[4];
        float hurle = -99f;

        protected override void Configurer()
        {
            nom = Lanceur ? "Loup d'orage" : "Loup gris";
            vieBase = Lanceur ? 34 : 30; attaqueBase = 7; defenseBase = 2; xpBase = Lanceur ? 26 : 16;
            FixerAffinite(Element.Feu, 1.5f); FixerAffinite(Element.Glace, .75f);   // le feu leur fait peur, la fourrure tient chaud
            resistanceRecul = .8f;
            vitesseMarche = 2.2f; vitesseCourse = 7.5f; perception = 20f; laisse = 45f; rayonRepos = 9f;
            rayonCorps = .38f; hauteurCorps = .95f; hauteurCentre = .62f; hauteurTete = 1f;
            rotationPreparation = 420f; maxAttaquants = 1; frequencePas = 2.6f;
            butin.Add(("peau_loup", .8f, 1, 1));
            butin.Add(("croc_loup", .5f, 1, 2));
            butin.Add(("viande_crue", .5f, 1, 1));
            attaques.Add(new AttaqueMonstre
            {
                nom = "Morsure", portee = 2.6f, preparation = .45f, frappe = .22f, recuperation = .55f,
                mult = 1f, poussee = .5f, cone = 70f, bond = 8.5f, recharge = 1.2f,
            });
            attaques.Add(new AttaqueMonstre
            {
                nom = "Bond", porteeMin = 3.5f, portee = 6.5f, preparation = .7f, frappe = .45f, recuperation = .9f,
                mult = 1.5f, poussee = 1.3f, cone = 50f, bond = 11f, sautVertical = 4f, recharge = 5f, poids = .6f, lourde = true,
            });
            if (Lanceur)
            {
                FixerAffinite(Element.Foudre, 0f); FixerAffinite(Element.Terre, 1.25f); FixerAffinite(Element.Glace, 1f);
                distanceDeTir = 9f; perception = 24f;
                butin.Add(("croc_loup", .6f, 1, 2));
                attaques.Add(new AttaqueMonstre
                {
                    nom = "Trait de foudre", sort = CatalogueCompetences.Get("foudre"), niveauSort = 1,
                    porteeMin = 4f, portee = 19f, preparation = 1.05f, frappe = .2f, recuperation = .6f, recharge = 4.2f, poids = 3f,
                });
            }
        }

        /// <summary>La foudre part de la gueule.</summary>
        public override Vector3 MainSort => cou != null ? cou.position + transform.forward * .5f : base.MainSort;

        protected override void Construire()
        {
            var poil = Pantin.Mat(Lanceur ? "#4f5866" : "#77716a", .05f);
            var dos = Pantin.Mat(Lanceur ? "#3a414d" : "#5a554f", .05f);
            var clair = Pantin.Mat(Lanceur ? "#8c96a8" : "#a49c90", .05f);
            var nez = Pantin.Mat("#1b1714", .6f);
            Pantin.Bloc(corps, PrimitiveType.Cube, new Vector3(0, .62f, 0), new Vector3(.34f, .34f, .95f), poil);
            Pantin.Bloc(corps, PrimitiveType.Cube, new Vector3(0, .76f, -.05f), new Vector3(.3f, .1f, .8f), dos);
            Pantin.Bloc(corps, PrimitiveType.Cube, new Vector3(0, .62f, .42f), new Vector3(.38f, .38f, .26f), poil);
            cou = Pantin.Os("Cou", corps, new Vector3(0, .74f, .5f));
            Pantin.Bloc(cou, PrimitiveType.Cube, new Vector3(0, .04f, .14f), new Vector3(.26f, .26f, .3f), poil);
            Pantin.Bloc(cou, PrimitiveType.Cube, new Vector3(0, -.03f, .36f), new Vector3(.15f, .13f, .22f), clair);
            Pantin.Bloc(cou, PrimitiveType.Cube, new Vector3(0, .01f, .48f), new Vector3(.06f, .05f, .04f), nez);
            foreach (float x in new[] { -.08f, .08f })
            {
                Pantin.Bloc(cou, PrimitiveType.Cube, new Vector3(x, .22f, .08f), new Vector3(.07f, .14f, .05f), dos, new Vector3(-10f, 0, x > 0 ? -12f : 12f));
                Pantin.Bloc(cou, PrimitiveType.Cube, new Vector3(x, .08f, .29f), new Vector3(.04f, .03f, .02f), Pantin.Lumineux(Lanceur ? "#d8e6ff" : "#f2c45a"));
            }
            if (Lanceur)
                // Des stries de lumière le long du dos, comme des éclairs figés
                for (int k = 0; k < 4; k++)
                    Pantin.Bloc(corps, PrimitiveType.Cube, new Vector3(k % 2 == 0 ? .06f : -.06f, .82f, .3f - k * .2f), new Vector3(.03f, .02f, .16f), Pantin.Lumineux("#bcd4ff"), new Vector3(0, k % 2 == 0 ? 25f : -25f, 0));
            var pos = new[] { new Vector3(-.12f, .5f, .34f), new Vector3(.12f, .5f, .34f), new Vector3(-.12f, .5f, -.34f), new Vector3(.12f, .5f, -.34f) };
            for (int k = 0; k < 4; k++)
            {
                pattes[k] = Pantin.Os("Patte" + k, corps, pos[k]);
                Pantin.Bloc(pattes[k], PrimitiveType.Cube, new Vector3(0, -.25f, 0), new Vector3(.1f, .5f, .1f), k < 2 ? poil : dos);
            }
            queue = Pantin.Os("Queue", corps, new Vector3(0, .72f, -.48f), new Vector3(-30f, 0, 0));
            Pantin.Bloc(queue, PrimitiveType.Cube, new Vector3(0, 0, -.22f), new Vector3(.09f, .09f, .44f), dos);
        }

        protected override void Alerter()
        {
            if (Time.time - hurle < 6f) return;
            hurle = Time.time;
            Sons.JouerA("hurlement", Centre, 1f, Random.Range(.92f, 1.08f));
            // Toute la meute accourt
            foreach (var m in Actifs)
                if (m is Loup l && l != this && l.Cible == null && (l.repaire == repaire || (l.transform.position - transform.position).sqrMagnitude < 40f * 40f))
                    l.Provoquer(cible);
        }

        protected override void SurPreparation(AttaqueMonstre a) => Sons.JouerA("grognement", Centre, .7f, a.lourde ? .85f : 1.1f);
        protected override void SurFrappe(AttaqueMonstre a) => Sons.JouerA("grognement", Centre, .5f, 1.4f);
        protected override void CriDeMort() => Sons.JouerA("grognement", Centre, .8f, 1.8f);

        protected override void Animer(float dt)
        {
            float amp = Mathf.Clamp01(vitesseActuelle / 3f);
            float s = Mathf.Sin(phasePas);
            // Pattes en diagonale
            pattes[0].localRotation = pattes[3].localRotation = Quaternion.Euler(s * 38f * amp, 0, 0);
            pattes[1].localRotation = pattes[2].localRotation = Quaternion.Euler(-s * 38f * amp, 0, 0);
            float basse = 0f, tete = 0f;
            switch (Etat)
            {
                case EtatMonstre.Preparation: float u = Mathf.Clamp01(tEtat / enCours.preparation); basse = -.14f * u; tete = 18f * u; break;
                case EtatMonstre.Frappe: tete = -22f; break;
                case EtatMonstre.Encerclement: basse = -.08f; tete = 12f; break;
                case EtatMonstre.Sonne: tete = 25f + Mathf.Sin(Time.time * 7f) * 10f; basse = -.05f; break;
            }
            corps.localPosition = Vector3.Lerp(corps.localPosition, new Vector3(0, basse + Mathf.Abs(s) * .03f * amp, 0), Mathf.Clamp01(dt * 12f));
            cou.localRotation = Quaternion.Slerp(cou.localRotation, Quaternion.Euler(tete, 0, 0), Mathf.Clamp01(dt * 14f));
            queue.localRotation = Quaternion.Euler(Etat == EtatMonstre.Fuite ? 20f : -30f + amp * 15f, Mathf.Sin(Time.time * 9f) * 18f, 0);
        }
    }
}
