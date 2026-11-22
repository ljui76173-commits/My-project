using UnityEditor;
using UnityEngine;

namespace Aldermark.Editeur
{
    /// <summary>
    /// Suite du chapitre 1 : Ilse à l'auberge (scène 10) et le nœud sous l'ancien pont (scène 11).
    /// </summary>
    public static partial class GameplayPont
    {
        static void ConstruireIlseEtNoeud(Transform racine)
        {
            // Les pouvoirs d'Aldric (Voile, Siphon, Présence)
            var aldric = Object.FindFirstObjectByType<JoueurAldric>();
            if (aldric != null && aldric.GetComponent<Pouvoirs>() == null) aldric.gameObject.AddComponent<Pouvoirs>();

            // Ilse, devant l'auberge, avec son pendentif
            var arc = new GameObject("Arc1").transform;
            arc.SetParent(racine, false);
            var auberge = racine.Find("VillageVivant/Auberge");
            Vector2 pi = auberge != null
                ? new Vector2(auberge.position.x, auberge.position.z) + new Vector2(auberge.forward.x, auberge.forward.z).normalized * 6.5f + new Vector2(auberge.right.x, auberge.right.z).normalized * 2.5f
                : Monde.Village + new Vector2(12, -6);
            pi = PlaceLibre(pi, 1f);
            var ilse = Personnage(arc, "Ilse", "ilse", pi, "#6a6a72", "#2a2228", 1.04f);
            ilse.GetComponent<Interlocuteur>().nom = "Femme en gris";
            if (auberge != null) ilse.transform.rotation = Quaternion.LookRotation(new Vector3(Monde.Village.x - pi.x, 0, Monde.Village.y - pi.y));
            var pend = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(pend.GetComponent<Collider>());
            pend.name = "Pendentif";
            pend.transform.SetParent(ilse.transform, false);
            pend.transform.localPosition = new Vector3(0, 1.05f, .16f);
            pend.transform.localScale = new Vector3(.07f, .12f, .05f);
            var src = pend.AddComponent<SourceMana>();
            src.nom = "Pendentif d'Ilse"; src.reserve = 10f; src.debit = 5f; src.hauteur = 0f;
            var q = arc.gameObject.AddComponent<QueteIlse>();
            q.ilse = ilse; q.pendentif = pend.transform;

            // Le nœud de mana sous les piles du vieux pont (visible seulement avec le Sceau)
            int couche = LayerMask.NameToLayer("Sceau");
            var n = new GameObject("NoeudDeVarenne").transform;
            n.SetParent(arc, false);
            n.position = new Vector3(Monde.PontX, -2.5f, 0);
            var noeud = n.gameObject.AddComponent<NoeudDeVarenne>();
            var sph = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(sph.GetComponent<Collider>());
            sph.name = "Sphere"; sph.layer = couche < 0 ? 0 : couche;
            sph.transform.SetParent(n, false);
            sph.transform.localScale = Vector3.one * 10f;
            noeud.sphere = sph.transform;
            // Trois lignes : nord (Sainte-Aube), sud, est (la Tour)
            var directions = new[] { new Vector3(.15f, 0, 1f), new Vector3(-.1f, 0, -1f), new Vector3(1f, 0, -.25f) };
            noeud.veines = new LineRenderer[3];
            for (int i = 0; i < 3; i++)
            {
                var v = new GameObject("Veine" + i) { layer = couche < 0 ? 0 : couche };
                v.transform.SetParent(n, false);
                var lr = v.AddComponent<LineRenderer>();
                lr.useWorldSpace = true; lr.positionCount = 16; lr.widthMultiplier = 1.4f;
                lr.sharedMaterial = Eclairant("VeineMana", new Color(.35f, .55f, 1f));
                for (int k = 0; k < 16; k++)
                {
                    Vector3 p = n.position + directions[i].normalized * k * 9f;
                    p.y = Mathf.Min(Monde.Hauteur(p.x, p.z) - 1.5f, -1.5f) + Mathf.Sin(k * .7f) * .5f;
                    lr.SetPosition(k, p);
                }
                noeud.veines[i] = lr;
            }
            // La rivière fume après la Surcharge
            var vap = new GameObject("Vapeur").AddComponent<ParticleSystem>();
            vap.transform.SetParent(n, false);
            vap.transform.position = new Vector3(Monde.PontX, -.1f, 0);
            vap.transform.rotation = Quaternion.Euler(-90f, 0, 0);
            var m = vap.main; m.duration = 6f; m.loop = false; m.startLifetime = 4f; m.startSpeed = 1.2f; m.startSize = 3f; m.playOnAwake = false;
            m.startColor = new Color(.9f, .92f, .95f, .35f);
            var em = vap.emission; em.rateOverTime = 25f;
            var sh = vap.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 9f;
            vap.GetComponent<ParticleSystemRenderer>().sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-ParticleSystem.mat");
            noeud.vapeur = vap;
        }
    }
}
