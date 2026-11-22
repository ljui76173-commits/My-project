using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Aldermark
{
    /// <summary>
    /// Aldric à la première personne : déplacement, regard, corps visible,
    /// bâton de bois ferré, main gauche et marque d'argent, Sceau du Grand Registre.
    /// Clavier : ZQSD (positions physiques), Maj pour courir, E pour le Sceau, R pour le rendu.
    /// Manette : sticks, bouton Nord pour le Sceau, Select pour le rendu.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class JoueurAldric : MonoBehaviour
    {
        public Camera cam;
        public RenduPeint rendu;
        public float vitesseMarche = 4.5f;
        public float vitesseCourse = 8.5f;
        public float sensibiliteSouris = .08f;
        public float sensibiliteManette = 140f;
        public float hauteurYeux = 1.7f;
        public float manaMax = 35f;
        public float consommationSceau = 1.6f;
        public float regenerationMana = 2.4f;

        public float Mana { get; private set; }
        public bool SceauActif { get; private set; }
        public float SceauNiveau { get; private set; }
        public float Cap => transform.eulerAngles.y;

        CharacterController cc;
        float tangage, vitesseVerticale, balancement, tempsDepuisDebut;
        bool bienvenueAffichee, sceauDejaUtilise;
        Transform brasDroit, brasGauche, jambeGauche, jambeDroite;
        Material matMarque;
        LineRenderer[] ronds;
        float[] ageRonds;
        Vector3[] centreRonds;
        int prochainRond;
        float minuterieRonds;
        ParticleSystem poussiere;
        float minuteriePoussiere;
        static readonly Vector3 BrasGaucheRepos = new Vector3(-.5f, -1f, .5f);
        static readonly Vector3 BrasGaucheLeve = new Vector3(-.3f, -.24f, .62f);
        static readonly Vector3 BrasDroitPos = new Vector3(.56f, -.56f, .95f);

        void Start()
        {
            cc = GetComponent<CharacterController>();
            if (cam == null) cam = GetComponentInChildren<Camera>();
            Mana = manaMax;
            ConstruireCorps();
            ConstruireVue();
            CreerRonds();
            CreerPoussiere();
            Verrouiller(true);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            tempsDepuisDebut += dt;
            var kb = Keyboard.current;
            var souris = Mouse.current;
            var pad = Gamepad.current;

            if (!bienvenueAffichee && tempsDepuisDebut > 1.2f)
            {
                bienvenueAffichee = true;
                HUD.Message("BIENVENUE À ALDERMARK, ALDRIC",
                    "Niveau 1 · Classe : non classable\nMana : 35 / 35\n\nAppuie sur E pour activer le Sceau.", 6f);
            }

            // Souris : Échap libère, clic reprend
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Verrouiller(false);
            if (souris != null && souris.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked) Verrouiller(true);

            // Regard
            Vector2 regard = Vector2.zero;
            if (souris != null && Cursor.lockState == CursorLockMode.Locked) regard += souris.delta.ReadValue() * sensibiliteSouris;
            if (pad != null) regard += pad.rightStick.ReadValue() * sensibiliteManette * dt;
            transform.Rotate(0f, regard.x, 0f);
            tangage = Mathf.Clamp(tangage - regard.y, -80f, 75f);

            // Déplacement
            Vector2 dep = Vector2.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) dep.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) dep.y -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dep.x += 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) dep.x -= 1;
            }
            if (pad != null) dep += pad.leftStick.ReadValue();
            dep = Vector2.ClampMagnitude(dep, 1f);
            bool course = (kb != null && kb.leftShiftKey.isPressed) || (pad != null && pad.leftStickButton.isPressed);
            float vitesse = course ? vitesseCourse : vitesseMarche;
            Vector3 p = transform.position;
            if (Monde.Hauteur(p.x, p.z) < -.15f) vitesse *= .5f; // dans la rivière
            Vector3 mouvement = (transform.forward * dep.y + transform.right * dep.x) * vitesse;
            vitesseVerticale = cc.isGrounded ? -1f : vitesseVerticale - 20f * dt;
            mouvement.y = vitesseVerticale;
            cc.Move(mouvement * dt);
            bool bouge = dep.sqrMagnitude > .01f;
            if (bouge) balancement += dt * vitesse * 1.6f;
            MettreAJourRonds(dt, bouge && Monde.Hauteur(p.x, p.z) < -.15f, p);
            MettreAJourPoussiere(dt, bouge && cc.isGrounded, course, transform.position);

            cam.transform.localPosition = new Vector3(0f, hauteurYeux + Mathf.Sin(balancement) * .04f, 0f);
            cam.transform.localRotation = Quaternion.Euler(tangage, 0f, 0f);

            // Sceau et rendu
            if ((kb != null && kb.eKey.wasPressedThisFrame) || (pad != null && pad.buttonNorth.wasPressedThisFrame)) BasculerSceau();
            if ((kb != null && kb.rKey.wasPressedThisFrame) || (pad != null && pad.selectButton.wasPressedThisFrame)) BasculerRendu();

            if (SceauActif)
            {
                Mana -= consommationSceau * dt;
                if (Mana <= 0f)
                {
                    Mana = 0f; SceauActif = false;
                    HUD.Message("MANA ÉPUISÉ", "La vue des flux se referme.", 2.5f);
                }
            }
            else Mana = Mathf.Min(manaMax, Mana + regenerationMana * dt);
            SceauNiveau = Mathf.Lerp(SceauNiveau, SceauActif ? 1f : 0f, Mathf.Clamp01(dt * 5f));
            if (rendu != null) rendu.sceau = SceauNiveau;

            // Animation du corps et des bras
            float pas = bouge ? Mathf.Sin(balancement * .5f) * 35f : 0f;
            jambeGauche.localRotation = Quaternion.Euler(pas, 0, 0);
            jambeDroite.localRotation = Quaternion.Euler(-pas, 0, 0);
            brasDroit.localPosition = BrasDroitPos + new Vector3(Mathf.Cos(balancement * .5f) * .012f, Mathf.Sin(balancement) * .012f, 0);
            brasGauche.localPosition = Vector3.Lerp(BrasGaucheRepos, BrasGaucheLeve, SceauNiveau);
            matMarque.SetColor("_BaseColor", Color.Lerp(new Color(.78f, .8f, .85f), new Color(.95f, .98f, 1f), SceauNiveau));
        }

        // ---------- poussière sous les pas ----------

        void CreerPoussiere()
        {
            var go = new GameObject("PoussiereDesPas");
            poussiere = go.AddComponent<ParticleSystem>();
            poussiere.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = poussiere.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.6f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.3f, .9f);
            main.startSize = new ParticleSystem.MinMaxCurve(.12f, .3f);
            main.gravityModifier = -.03f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;
            var em = poussiere.emission; em.enabled = false;
            var sh = poussiere.shape; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = .25f;
            var col = poussiere.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(.6f, 0), new GradientAlphaKey(0, 1) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = ronds[0].sharedMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        void MettreAJourPoussiere(float dt, bool marche, bool course, Vector3 pied)
        {
            if (!marche) return;
            bool surTerre = Monde.DistanceRoute(pied.x, pied.z) < 2.5f || Vector2.Distance(new Vector2(pied.x, pied.z), Monde.Village) < 12f;
            minuteriePoussiere -= dt;
            if (minuteriePoussiere > 0f) return;
            minuteriePoussiere = course ? .12f : .25f;
            var e = new ParticleSystem.EmitParams
            {
                position = pied + transform.forward * -.2f + Vector3.up * .05f,
                startColor = surTerre ? new Color(.62f, .5f, .34f, .7f) : new Color(.55f, .62f, .4f, .45f),
                applyShapeToPosition = true
            };
            poussiere.Emit(e, surTerre ? (course ? 4 : 2) : 1);
        }

        // ---------- ronds dans l'eau ----------

        void CreerRonds()
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            ronds = new LineRenderer[8];
            ageRonds = new float[8];
            centreRonds = new Vector3[8];
            for (int i = 0; i < ronds.Length; i++)
            {
                var go = new GameObject("RondDansLEau");
                go.transform.rotation = Quaternion.Euler(90f, 0, 0);
                var lr = go.AddComponent<LineRenderer>();
                lr.loop = true;
                lr.positionCount = 24;
                lr.useWorldSpace = true;
                lr.widthMultiplier = .07f;
                lr.sharedMaterial = m;
                lr.shadowCastingMode = ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.alignment = LineAlignment.TransformZ;
                lr.enabled = false;
                ronds[i] = lr;
                ageRonds[i] = 1f;
            }
        }

        void MettreAJourRonds(float dt, bool emettre, Vector3 p)
        {
            if (emettre)
            {
                minuterieRonds -= dt;
                if (minuterieRonds <= 0f)
                {
                    minuterieRonds = .3f;
                    int i = prochainRond++ % ronds.Length;
                    ageRonds[i] = 0f;
                    centreRonds[i] = new Vector3(p.x, -.06f, p.z);
                }
            }
            for (int i = 0; i < ronds.Length; i++)
            {
                var lr = ronds[i];
                if (ageRonds[i] >= 1f) { lr.enabled = false; continue; }
                ageRonds[i] = Mathf.Min(1f, ageRonds[i] + dt * .75f);
                float r = .5f + ageRonds[i] * 3.2f;
                for (int k = 0; k < lr.positionCount; k++)
                {
                    float a = k * Mathf.PI * 2f / lr.positionCount;
                    lr.SetPosition(k, centreRonds[i] + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r));
                }
                var c = new Color(.92f, .95f, 1f, (1f - ageRonds[i]) * .75f);
                lr.startColor = c; lr.endColor = c;
                lr.enabled = true;
            }
        }

        void BasculerSceau()
        {
            if (!SceauActif && Mana < 3f) { HUD.Message("MANA INSUFFISANT", "Repose-toi un instant.", 2.5f); return; }
            SceauActif = !SceauActif;
            if (SceauActif && !sceauDejaUtilise)
            {
                sceauDejaUtilise = true;
                HUD.Message("SCEAU DU GRAND REGISTRE",
                    "Éveil 1 % · Vue des flux active\n\nAmbre : routes et marchandises\nRouge : flux rompu\nBleu : lignes de mana\n\nLa vue consomme du mana.", 6f);
            }
        }

        void BasculerRendu()
        {
            if (rendu == null) return;
            rendu.peint = !rendu.peint;
            HUD.Message(rendu.peint ? "RENDU PEINT" : "RENDU BRUT",
                rendu.peint ? "Aldermark." : "Le monde d'avant : sans shader, terne.", 2.2f);
        }

        static void Verrouiller(bool v)
        {
            Cursor.lockState = v ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !v;
        }

        // ---------- construction du personnage ----------

        static Material Lit(Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", .1f);
            return m;
        }

        static Transform Piece(PrimitiveType type, Transform parent, Vector3 pos, Vector3 taille, Material m, bool ombre, Vector3 rotation = default)
        {
            var g = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(rotation);
            g.transform.localScale = taille;
            var r = g.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ombre ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return g.transform;
        }

        static Transform Groupe(string nom, Transform parent, Vector3 pos, Vector3 rotation = default)
        {
            var g = new GameObject(nom).transform;
            g.SetParent(parent, false);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(rotation);
            return g;
        }

        void ConstruireCorps()
        {
            var lin = Lit(Hex("#d8cba8"));
            var corps = Groupe("Corps", transform, Vector3.zero);
            jambeGauche = Groupe("JambeG", corps, new Vector3(-.12f, .88f, .02f));
            jambeDroite = Groupe("JambeD", corps, new Vector3(.12f, .88f, .02f));
            foreach (var j in new[] { jambeGauche, jambeDroite })
            {
                Piece(PrimitiveType.Cube, j, new Vector3(0, -.31f, 0), new Vector3(.17f, .62f, .17f), Lit(Hex("#6b5240")), true);
                Piece(PrimitiveType.Cube, j, new Vector3(0, -.66f, .05f), new Vector3(.19f, .24f, .32f), Lit(Hex("#3a2a1e")), true);
            }
            Piece(PrimitiveType.Cube, corps, new Vector3(0, 1.14f, -.21f), new Vector3(.48f, .56f, .22f), lin, true);
            Piece(PrimitiveType.Cube, corps, new Vector3(0, .88f, -.19f), new Vector3(.54f, .2f, .31f), Lit(Hex("#cbbd98")), true);
            Piece(PrimitiveType.Cube, corps, new Vector3(0, 1f, -.17f), new Vector3(.53f, .08f, .31f), Lit(Hex("#4a3020")), true);
            Piece(PrimitiveType.Cube, corps, new Vector3(.2f, .93f, .02f), new Vector3(.13f, .15f, .08f), Lit(Hex("#7a5a3c")), true);
        }

        void ConstruireVue()
        {
            var peau = Lit(Hex("#e2b48a"));
            var lin = Lit(Hex("#d8cba8"));
            var fer = Lit(Hex("#7c7f8c"));
            var bois = Lit(Hex("#6b4a2e"));

            // Bras droit + bâton de bois ferré
            brasDroit = Groupe("BrasDroit", cam.transform, BrasDroitPos);
            Piece(PrimitiveType.Cube, brasDroit, Vector3.zero, new Vector3(.1f, .11f, .13f), peau, false);
            Piece(PrimitiveType.Cube, brasDroit, new Vector3(.05f, -.07f, -.3f), new Vector3(.13f, .13f, .5f), lin, false, new Vector3(-14f, 0, 0));
            var baton = Groupe("Baton", brasDroit, Vector3.zero, new Vector3(26f, 0, 2f));
            Piece(PrimitiveType.Cylinder, baton, new Vector3(0, .3f, 0), new Vector3(.044f, .85f, .044f), bois, false);
            Piece(PrimitiveType.Cylinder, baton, new Vector3(0, 1.15f, 0), new Vector3(.076f, .075f, .076f), fer, false);
            Piece(PrimitiveType.Cylinder, baton, new Vector3(0, -.55f, 0), new Vector3(.076f, .075f, .076f), fer, false);
            Piece(PrimitiveType.Cylinder, baton, new Vector3(0, .86f, 0), new Vector3(.068f, .025f, .068f), fer, false);

            // Bras gauche + marque d'argent (se lève quand le Sceau s'active)
            brasGauche = Groupe("BrasGauche", cam.transform, BrasGaucheRepos, new Vector3(-25f, 17f, 0));
            Piece(PrimitiveType.Cube, brasGauche, Vector3.zero, new Vector3(.12f, .05f, .13f), peau, false);
            Piece(PrimitiveType.Cube, brasGauche, new Vector3(0, 0, -.22f), new Vector3(.1f, .1f, .34f), peau, false);
            Piece(PrimitiveType.Cube, brasGauche, new Vector3(0, 0, -.5f), new Vector3(.13f, .13f, .3f), lin, false);

            matMarque = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            matMarque.SetColor("_BaseColor", new Color(.78f, .8f, .85f));
            var bracelet = new GameObject("Marque");
            bracelet.transform.SetParent(brasGauche, false);
            bracelet.transform.localPosition = new Vector3(0, 0, -.1f);
            bracelet.AddComponent<MeshFilter>().sharedMesh = Formes.Anneau(.062f, .012f);
            var rb = bracelet.AddComponent<MeshRenderer>();
            rb.sharedMaterial = matMarque; rb.shadowCastingMode = ShadowCastingMode.Off;

            var sigle = new GameObject("Sigle");
            sigle.transform.SetParent(brasGauche, false);
            sigle.transform.localPosition = new Vector3(0, .052f, -.14f);
            sigle.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            sigle.AddComponent<MeshFilter>().sharedMesh = Formes.Anneau(.026f, .006f, 14, 4);
            var rs = sigle.AddComponent<MeshRenderer>();
            rs.sharedMaterial = matMarque; rs.shadowCastingMode = ShadowCastingMode.Off;

            // Halo de la marque, visible seulement par la caméra du Sceau
            int couche = LayerMask.NameToLayer("Sceau");
            if (couche >= 0)
            {
                var halo = new GameObject("HaloMarque") { layer = couche };
                halo.transform.SetParent(bracelet.transform, false);
                halo.transform.localScale = Vector3.one * 1.3f;
                halo.AddComponent<MeshFilter>().sharedMesh = Formes.Anneau(.062f, .02f);
                var mh = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                mh.SetColor("_BaseColor", new Color(.85f, .93f, 1f));
                var rh = halo.AddComponent<MeshRenderer>();
                rh.sharedMaterial = mh; rh.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
    }
}
