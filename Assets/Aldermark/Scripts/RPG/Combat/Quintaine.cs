using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// La quintaine du hameau : un poteau, un bras qui pivote, un écu d'un côté, un sac de sable de l'autre.
    /// Frappe-la : le bras tourne (bien plus vite sur un coup lourd) et le sac revient te cogner
    /// si tu restes dans sa course. Pare-le, esquive-le, ou encaisse et glisse en arrière.
    /// Elle ne casse jamais : elle compte les dégâts.
    /// </summary>
    public class Quintaine : Combattant
    {
        const float HauteurBras = 1.85f, RayonSac = 1.75f, RayonEcu = .95f;

        Transform bras, corde, sac;
        float omega;                 // vitesse du bras, en degrés par seconde
        float dernierCoupDonne = -9f;

        protected override void Awake()
        {
            nom = "Quintaine"; camp = Camp.Neutre; invincible = true; hauteurCentre = 1.35f;
            vieBase = 1; attaqueBase = 0; defenseBase = 1; resistanceRecul = 99f; xpBase = 0;
            base.Awake();
            Construire();
        }

        protected override void Update()
        {
            base.Update();
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // Frottement : le bras ralentit puis s'arrête
            float v = Mathf.Max(0f, Mathf.Abs(omega) * Mathf.Exp(-.45f * dt) - 50f * dt);
            omega = Mathf.Sign(omega) * v;
            bras.localRotation *= Quaternion.Euler(0, omega * dt, 0);

            // Le sac s'écarte en tournant (force centrifuge)
            float w = omega * Mathf.Deg2Rad;
            float angle = Mathf.Min(65f, Mathf.Atan2(w * w * RayonSac, 9.81f) * Mathf.Rad2Deg);
            corde.localRotation = Quaternion.Slerp(corde.localRotation, Quaternion.Euler(0, 0, -angle), Mathf.Clamp01(dt * 8f));

            Cogner();
        }

        /// <summary>Un coup fait tourner le bras : d'autant plus vite qu'il est lourd et qu'il porte loin du poteau.</summary>
        public override void Recevoir(Coup c)
        {
            base.Recevoir(c);
            Vector3 pivot = bras.position, axe = bras.right;   // axe : du sac (−) vers l'écu (+)
            Vector3 dir = Vector3.ProjectOnPlane(c.direction, Vector3.up).normalized;
            Vector3 depuis = c.auteur != null ? c.auteur.Centre : pivot - dir * 2f;
            float s = Mathf.Clamp(Vector3.Dot(Vector3.ProjectOnPlane(depuis - pivot, Vector3.up), axe), -RayonSac, RayonEcu);
            float couple = Vector3.Cross(axe * s, dir).y;
            float levier = Mathf.Lerp(.6f, 1f, Mathf.Clamp01(Mathf.Abs(s) / RayonEcu));
            float elan = (c.lourd ? 640f : 300f) * (c.critique ? 1.25f : 1f) * levier;
            float signe = Mathf.Abs(couple) > .05f ? Mathf.Sign(couple) : omega != 0f ? Mathf.Sign(omega) : 1f;
            omega = Mathf.Clamp(omega + signe * elan, -1100f, 1100f);
        }

        /// <summary>Le sac frappe le héros s'il passe sur lui en venant vers lui.</summary>
        void Cogner()
        {
            var h = CombatHeros.Instance;
            if (h == null || !h.Vivant || Mathf.Abs(omega) < 110f || Time.time - dernierCoupDonne < .35f) return;
            Vector3 pSac = sac.position, cible = h.transform.position;
            float dy = pSac.y - cible.y;
            Vector3 ecart = Vector3.ProjectOnPlane(cible - pSac, Vector3.up);
            if (ecart.magnitude > .8f || dy < .2f || dy > 2.2f) return;
            Vector3 pivot = bras.position;
            Vector3 vitesse = Vector3.Cross(Vector3.up * (omega * Mathf.Deg2Rad), Vector3.ProjectOnPlane(pSac - pivot, Vector3.up));
            if (Vector3.Dot(vitesse, ecart) < 0f) return;   // il s'éloigne déjà

            dernierCoupDonne = Time.time;
            float force = Mathf.Abs(omega) / 360f;
            double vieMax = StatsHeros.Instance != null ? StatsHeros.Instance.VieMax : 70;
            Vector3 radial = Vector3.ProjectOnPlane(cible - pivot, Vector3.up).normalized;
            var coup = new Coup
            {
                degats = (4 + vieMax * .05) * Mathf.Clamp(force, .5f, 2.2f),
                direction = (vitesse.normalized * .6f + radial * .4f).normalized,
                poussee = .95f * Mathf.Pow(force, 1.3f),
                lourd = force > 1.4f,
                auteur = this,
                nom = "Sac de sable",
            };
            bool esquive = h.Invulnerable;
            float etourdiAvant = Etourdi;
            h.Recevoir(coup);
            if (esquive) return;   // le sac passe dans le vide
            Sons.JouerA("impactChair", pSac, .6f, .75f);
            if (Etourdi > etourdiAvant) omega *= -.25f;   // parade parfaite : le sac rebondit
            else if (h.EnGarde) omega *= -.4f;
            else omega *= .55f;
        }

        // ---------- construction ----------

        void Construire()
        {
            var bois = Mat("#7a5a3c", .15f);
            var sombre = Mat("#5a4230", .1f);
            var fer = Mat("#4a4a52", .55f);
            var toile = Mat("#c8b48a", .05f);
            var rouge = Mat("#8c3a2e", .25f);
            var chanvre = Mat("#b49a6a", .05f);

            Bloc(transform, PrimitiveType.Cube, new Vector3(0, .11f, 0), new Vector3(1.6f, .22f, .26f), sombre);
            Bloc(transform, PrimitiveType.Cube, new Vector3(0, .11f, 0), new Vector3(.26f, .22f, 1.6f), sombre);
            Bloc(transform, PrimitiveType.Cylinder, new Vector3(0, 1f, 0), new Vector3(.22f, 1f, .22f), bois);
            Bloc(transform, PrimitiveType.Cylinder, new Vector3(0, 2.02f, 0), new Vector3(.3f, .04f, .3f), fer);

            bras = new GameObject("Bras").transform;
            bras.SetParent(transform, false);
            bras.localPosition = new Vector3(0, HauteurBras, 0);
            bras.localRotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            Bloc(bras, PrimitiveType.Cube, new Vector3((RayonEcu - RayonSac) / 2f, 0, 0), new Vector3(RayonSac + RayonEcu + .2f, .12f, .12f), bois);
            Bloc(bras, PrimitiveType.Cylinder, Vector3.zero, new Vector3(.3f, .09f, .3f), fer);

            // L'écu, face au cavalier
            Bloc(bras, PrimitiveType.Cube, new Vector3(RayonEcu, -.08f, 0), new Vector3(.56f, .72f, .07f), rouge);
            Bloc(bras, PrimitiveType.Cube, new Vector3(RayonEcu, -.08f, 0), new Vector3(.08f, .74f, .09f), fer);
            Bloc(bras, PrimitiveType.Cube, new Vector3(RayonEcu, .05f, 0), new Vector3(.58f, .08f, .09f), fer);
            foreach (float z in new[] { -.06f, .06f })
                Bloc(bras, PrimitiveType.Sphere, new Vector3(RayonEcu, -.02f, z), Vector3.one * .14f, fer);

            // Le sac de sable au bout de sa corde
            corde = new GameObject("Corde").transform;
            corde.SetParent(bras, false);
            corde.localPosition = new Vector3(-RayonSac, 0, 0);
            Bloc(corde, PrimitiveType.Cube, new Vector3(0, -.24f, 0), new Vector3(.035f, .48f, .035f), chanvre);
            sac = Bloc(corde, PrimitiveType.Sphere, new Vector3(0, -.74f, 0), new Vector3(.44f, .56f, .44f), toile);
            Bloc(corde, PrimitiveType.Cube, new Vector3(0, -.48f, 0), new Vector3(.12f, .07f, .12f), chanvre);

            var col = gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 1.1f, 0); col.radius = .38f; col.height = 2.3f;
        }

        static Material Mat(string hex, float lisse)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", lisse);
            return m;
        }

        static Transform Bloc(Transform parent, PrimitiveType type, Vector3 pos, Vector3 taille, Material m)
        {
            var g = GameObject.CreatePrimitive(type);
            var col = g.GetComponent<Collider>();
            if (col != null) { col.enabled = false; Destroy(col); }
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = taille;
            g.GetComponent<MeshRenderer>().sharedMaterial = m;
            return g.transform;
        }
    }
}
