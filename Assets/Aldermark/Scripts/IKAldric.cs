using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// Posé sur le modèle humanoïde d'Aldric (cinématique inverse) : la main droite tient l'épée là où
    /// le combat la place ; quand le Sceau s'active, la main gauche se lève pour montrer la marque d'argent.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class IKAldric : MonoBehaviour
    {
        public JoueurAldric joueur;
        Animator anim;

        void Awake() => anim = GetComponent<Animator>();

        void OnAnimatorIK(int couche)
        {
            if (joueur == null || joueur.cam == null) return;
            Transform c = joueur.cam.transform, j = joueur.transform;

            // Main droite : elle tient l'épée (le combat place la poignée)
            float wd = joueur.poidsMainDroite;
            anim.SetIKPositionWeight(AvatarIKGoal.RightHand, wd);
            anim.SetIKHintPositionWeight(AvatarIKHint.RightElbow, wd * .6f);
            if (wd > .001f)
            {
                anim.SetIKPosition(AvatarIKGoal.RightHand, joueur.cibleMainDroite);
                anim.SetIKHintPosition(AvatarIKHint.RightElbow, j.position + j.up * 1.05f + j.right * .45f - j.forward * .1f);
            }

            // Pied droit : le coup de pied
            float wp = joueur.poidsPiedDroit;
            anim.SetIKPositionWeight(AvatarIKGoal.RightFoot, wp);
            if (wp > .001f) anim.SetIKPosition(AvatarIKGoal.RightFoot, joueur.ciblePiedDroit);

            // Main gauche : le poing gauche à mains nues
            if (joueur.poidsMainGauche > joueur.SceauNiveau)
            {
                anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, joueur.poidsMainGauche);
                anim.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, joueur.poidsMainGauche * .6f);
                anim.SetIKPosition(AvatarIKGoal.LeftHand, joueur.cibleMainGauche);
                anim.SetIKHintPosition(AvatarIKHint.LeftElbow, j.position + j.up * 1.05f - j.right * .45f - j.forward * .1f);
                return;
            }

            // Main gauche : montre la marque d'argent quand le Sceau s'active
            float w = joueur.SceauNiveau;
            anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, w);
            anim.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, w * .8f);
            if (w <= .001f) return;
            Vector3 cible = joueur.troisiemePersonne
                ? j.position + j.up * 1.35f + j.forward * .42f - j.right * .05f
                : c.position + c.forward * .45f - c.right * .12f - c.up * .14f;
            anim.SetIKPosition(AvatarIKGoal.LeftHand, cible);
            anim.SetIKHintPosition(AvatarIKHint.LeftElbow, j.position + j.up * 1.1f - j.right * .4f + j.forward * .1f);
        }
    }
}
