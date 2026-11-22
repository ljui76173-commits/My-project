using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// Posé sur le modèle humanoïde d'Aldric : quand le Sceau s'active, la main gauche
    /// se lève devant le regard pour montrer la marque d'argent (cinématique inverse).
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
            float w = joueur.SceauNiveau;
            anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, w);
            anim.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, w * .8f);
            if (w <= .001f) return;
            Transform c = joueur.cam.transform, j = joueur.transform;
            Vector3 cible = joueur.troisiemePersonne
                ? j.position + j.up * 1.35f + j.forward * .42f - j.right * .05f
                : c.position + c.forward * .45f - c.right * .12f - c.up * .14f;
            anim.SetIKPosition(AvatarIKGoal.LeftHand, cible);
            anim.SetIKHintPosition(AvatarIKHint.LeftElbow, j.position + j.up * 1.1f - j.right * .4f + j.forward * .1f);
        }
    }
}
