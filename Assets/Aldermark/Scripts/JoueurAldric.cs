using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Aldermark
{
    /// <summary>
    /// Aldric à la première ou à la troisième personne (V) : déplacement, saut, regard,
    /// mains nues et marque d'argent, Sceau du Grand Registre, modèle 3D facultatif.
    /// Clavier : ZQSD (positions physiques), Maj pour courir, Espace pour sauter, E pour le Sceau, R pour le rendu.
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
        [Tooltip("Modèle 3D d'Aldric (posé par le menu Aldermark). Vide = corps en cubes.")]
        public Transform modele;
        public bool troisiemePersonne;

        public float Mana { get; private set; }
        public bool SceauActif { get; private set; }
        public float SceauNiveau { get; private set; }
        public float Cap => transform.eulerAngles.y;

        /// <summary>Dépense du mana (Plan du Registre, don de mana). Renvoie faux s'il n'y en a pas assez.</summary>
        public bool Depenser(float q)
        {
            if (Mana < q) { Mana = 0f; return false; }
            Mana -= q;
            return true;
        }

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
        PointInteret cibleAnalyse;
        float tempsAnalyse;
        const float DureeAnalyse = 1.5f;
        int dernierPas;
        float fovBase = 68f;
        bool liseAInterpelle;
        Transform corps;
        Animator anim;
        Renderer[] rendusModele;
        Vector3 positionModele;
        float distanceCamera = 3.6f;
        static readonly Vector3 BrasGaucheRepos = new Vector3(-.5f, -1f, .5f);
        static readonly Vector3 BrasGaucheLeve = new Vector3(-.3f, -.24f, .62f);
        static readonly Vector3 BrasDroitPos = new Vector3(.56f, -.56f, .95f);

        void Start()
        {
            cc = GetComponent<CharacterController>();
            if (cam == null) cam = GetComponentInChildren<Camera>();
            Mana = manaMax;
            fovBase = cam.fieldOfView;
            ConstruireCorps();
            ConstruireVue();
            if (modele != null)
            {
                corps.gameObject.SetActive(false);
                anim = modele.GetComponentInChildren<Animator>();
                if (anim != null) anim.applyRootMotion = false;
                rendusModele = modele.GetComponentsInChildren<Renderer>();
                positionModele = modele.localPosition;
            }
            AppliquerVue();
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

            bool fige = Dialogues.EnCours || HUD.TableauOuvert;

            // Regard
            Vector2 regard = Vector2.zero;
            if (!fige && souris != null && Cursor.lockState == CursorLockMode.Locked) regard += souris.delta.ReadValue() * sensibiliteSouris;
            if (!fige && pad != null) regard += pad.rightStick.ReadValue() * sensibiliteManette * dt;
            transform.Rotate(0f, regard.x, 0f);
            tangage = Mathf.Clamp(tangage - regard.y, -80f, 75f);

            // Déplacement
            Vector2 dep = Vector2.zero;
            if (kb != null && !fige)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) dep.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) dep.y -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dep.x += 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) dep.x -= 1;
            }
            if (pad != null && !fige) dep += pad.leftStick.ReadValue();
            dep = Vector2.ClampMagnitude(dep, 1f);
            bool course = (kb != null && kb.leftShiftKey.isPressed) || (pad != null && pad.leftStickButton.isPressed);
            float vitesse = course ? vitesseCourse : vitesseMarche;
            Vector3 p = transform.position;
            if (Monde.Hauteur(p.x, p.z) < -.15f) vitesse *= .5f; // dans la rivière
            Vector3 mouvement = (transform.forward * dep.y + transform.right * dep.x) * vitesse;
            bool saute = !fige && ((kb != null && kb.spaceKey.wasPressedThisFrame) || (pad != null && pad.buttonSouth.wasPressedThisFrame));
            if (cc.isGrounded)
            {
                if (vitesseVerticale < -6f) Sons.Jouer("pasTerre", .5f, .7f);   // réception
                vitesseVerticale = saute ? 6.2f : -1f;
                if (saute) Sons.Jouer("pasHerbe", .35f, .8f);
            }
            else vitesseVerticale -= 18f * dt;
            mouvement.y = vitesseVerticale;
            cc.Move(mouvement * dt);
            bool bouge = dep.sqrMagnitude > .01f;
            if (bouge) balancement += dt * vitesse * 1.6f;
            int numeroPas = Mathf.FloorToInt(balancement / Mathf.PI);
            if (numeroPas != dernierPas)
            {
                dernierPas = numeroPas;
                if (cc.isGrounded && bouge)
                {
                    Vector3 pied = transform.position;
                    bool terre = Monde.DistanceRoute(pied.x, pied.z) < 2.5f || Vector2.Distance(new Vector2(pied.x, pied.z), Monde.Village) < 12f;
                    Sons.Jouer(terre ? "pasTerre" : "pasHerbe", course ? .45f : .3f, Random.Range(.85f, 1.15f));
                }
            }
            MettreAJourRonds(dt, bouge && Monde.Hauteur(p.x, p.z) < -.15f, p);
            MettreAJourPoussiere(dt, bouge && cc.isGrounded, course, transform.position);

            if (fige && Dialogues.Courant != null)
            {
                Vector3 tete = Dialogues.Courant.transform.position + Vector3.up * (1.45f * Dialogues.Courant.transform.lossyScale.y);
                Vector3 vers = tete - cam.transform.position;
                Vector3 horiz = new Vector3(vers.x, 0, vers.z);
                if (horiz.sqrMagnitude > .01f)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(horiz), Mathf.Clamp01(dt * 4f));
                    float vise = -Mathf.Atan2(vers.y, horiz.magnitude) * Mathf.Rad2Deg;
                    tangage = Mathf.Lerp(tangage, vise, Mathf.Clamp01(dt * 4f));
                }
            }
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fige ? fovBase - 16f : fovBase, Mathf.Clamp01(dt * 3f));
            if ((kb != null && kb.vKey.wasPressedThisFrame) || (pad != null && pad.dpad.up.wasPressedThisFrame))
            {
                troisiemePersonne = !troisiemePersonne;
                AppliquerVue();
            }
            PlacerCamera(dt);
            AnimerModele(dt, mouvement, bouge);

            // Sceau et rendu
            if (!fige && ((kb != null && kb.eKey.wasPressedThisFrame) || (pad != null && pad.buttonNorth.wasPressedThisFrame))) BasculerSceau();
            AnalyserAvecLeSceau(dt);
            Parler(kb, pad, fige);
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
            else if (!PlanRegistre.Actif) Mana = Mathf.Min(manaMax, Mana + regenerationMana * dt);
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

        // ---------- vue et modèle ----------

        void AppliquerVue()
        {
            // Première personne : le modèle ne fait que son ombre, on voit les mains.
            // Troisième personne : le modèle est visible, les mains de la vue disparaissent.
            if (rendusModele != null)
                foreach (var r in rendusModele)
                    r.shadowCastingMode = troisiemePersonne ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
            if (modele == null && corps != null)
                foreach (var r in corps.GetComponentsInChildren<Renderer>())
                    r.shadowCastingMode = troisiemePersonne ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
            if (brasDroit != null) brasDroit.gameObject.SetActive(!troisiemePersonne);
            if (brasGauche != null) brasGauche.gameObject.SetActive(!troisiemePersonne);
        }

        void PlacerCamera(float dt)
        {
            var rot = Quaternion.Euler(tangage, 0f, 0f);
            Vector3 tete = new Vector3(0f, hauteurYeux + Mathf.Sin(balancement) * .04f, 0f);
            if (!troisiemePersonne)
            {
                cam.transform.localPosition = tete;
                cam.transform.localRotation = rot;
                return;
            }
            // Caméra d'épaule, rapprochée si un mur ou le sol est derrière
            Vector3 pivot = new Vector3(0f, hauteurYeux + .1f, 0f);
            Vector3 voulu = pivot + rot * new Vector3(.55f, 0f, -3.6f);
            Vector3 a = transform.TransformPoint(pivot), b = transform.TransformPoint(voulu);
            float cible = 3.6f;
            if (Physics.SphereCast(a, .25f, (b - a).normalized, out var hit, (b - a).magnitude, ~0, QueryTriggerInteraction.Ignore)
                && hit.transform != transform && !hit.transform.IsChildOf(transform))
                cible = Mathf.Max(.6f, hit.distance - .1f);
            distanceCamera = cible < distanceCamera ? cible : Mathf.Lerp(distanceCamera, cible, Mathf.Clamp01(dt * 4f));
            cam.transform.localPosition = pivot + rot * new Vector3(.55f * distanceCamera / 3.6f, 0f, -distanceCamera);
            cam.transform.localRotation = rot;
        }

        void AnimerModele(float dt, Vector3 mouvement, bool bouge)
        {
            if (modele == null) return;
            float v = new Vector3(mouvement.x, 0, mouvement.z).magnitude;
            if (anim != null && anim.runtimeAnimatorController != null)
            {
                anim.SetFloat("Vitesse", v, .12f, dt);
                return;
            }
            // Pas d'animations dans le fichier : léger balancement de marche
            float b = bouge && cc.isGrounded ? Mathf.Abs(Mathf.Sin(balancement * .5f)) * .05f : 0f;
            float roulis = bouge ? Mathf.Sin(balancement * .5f) * 2.5f : 0f;
            modele.localPosition = Vector3.Lerp(modele.localPosition, positionModele + Vector3.up * b, Mathf.Clamp01(dt * 12f));
            modele.localRotation = Quaternion.Slerp(modele.localRotation, Quaternion.Euler(0, 0, roulis), Mathf.Clamp01(dt * 8f));
        }

        // ---------- indices et dialogues ----------

        void AnalyserAvecLeSceau(float dt)
        {
            PointInteret cible = null;
            if (SceauNiveau > .5f && !Dialogues.EnCours)
            {
                float meilleur = float.MaxValue;
                Vector3 oeil = cam.transform.position, regard = cam.transform.forward;
                foreach (var p in PointInteret.Tous)
                {
                    if (!p.Disponible) continue;
                    Vector3 d = p.transform.position - oeil;
                    float dist = d.magnitude;
                    if (dist > p.porteeMax) continue;
                    float angle = Vector3.Angle(regard, d);
                    float seuil = Mathf.Max(5f, Mathf.Atan2(p.rayon, dist) * Mathf.Rad2Deg);
                    if (angle < seuil && angle < meilleur) { meilleur = angle; cible = p; }
                }
            }
            if (cible != cibleAnalyse) { cibleAnalyse = cible; tempsAnalyse = 0f; }
            if (cible == null) { HUD.Analyse(-1f); Sons.Analyse(-1f); return; }
            tempsAnalyse += dt;
            HUD.Analyse(tempsAnalyse / DureeAnalyse);
            Sons.Analyse(tempsAnalyse / DureeAnalyse);
            if (tempsAnalyse >= DureeAnalyse)
            {
                cible.Decouvrir();
                cibleAnalyse = null;
                tempsAnalyse = 0f;
                HUD.Analyse(-1f);
                Sons.Analyse(-1f);
            }
        }

        void Parler(Keyboard kb, Gamepad pad, bool fige)
        {
            if (fige) { HUD.Invite(null); return; }
            Interlocuteur proche = null;
            float meilleur = float.MaxValue;
            foreach (var i in Interlocuteur.Tous)
            {
                Vector3 d = i.transform.position + Vector3.up * 1.1f - cam.transform.position;
                float dist = d.magnitude;
                if (dist > 3.5f || Vector3.Angle(cam.transform.forward, d) > 35f) continue;
                if (dist < meilleur) { meilleur = dist; proche = i; }
            }
            string invite = proche != null ? $"[F]  Parler à {proche.nom}" : null;
            if (proche == null && !Progression.A("lise"))
                foreach (var i in Interlocuteur.Tous)
                    if (i.id == "lise" && Vector3.Distance(i.transform.position, transform.position) < 16f)
                    {
                        invite = "Lise te fait signe : « Monsieur ! Monsieur ! »";
                        if (!liseAInterpelle)
                        {
                            liseAInterpelle = true;
                            for (int k = 0; k < 4; k++) Sons.Voix(1.75f);
                            var h = i.GetComponent<Habitant>();
                            if (h != null) h.parle = true;
                        }
                    }
            HUD.Invite(invite);
            if (proche != null && ((kb != null && kb.fKey.wasPressedThisFrame) || (pad != null && pad.buttonWest.wasPressedThisFrame)))
            {
                var vers = transform.position - proche.transform.position;
                vers.y = 0;
                if (vers.sqrMagnitude > .01f) proche.transform.rotation = Quaternion.LookRotation(vers);
                Dialogues.Commencer(proche);
            }
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
            Sons.Jouer(SceauActif ? "sceauOn" : "sceauOff", .7f);
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
            corps = Groupe("Corps", transform, Vector3.zero);
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

            // Bras droit, main nue (pas d'arme : Aldric arrive sans rien dans ce monde)
            brasDroit = Groupe("BrasDroit", cam.transform, BrasDroitPos);
            Piece(PrimitiveType.Cube, brasDroit, Vector3.zero, new Vector3(.1f, .11f, .13f), peau, false);
            Piece(PrimitiveType.Cube, brasDroit, new Vector3(-.035f, .03f, .03f), new Vector3(.04f, .04f, .09f), peau, false, new Vector3(0, 25f, 0));
            Piece(PrimitiveType.Cube, brasDroit, new Vector3(0, 0, -.22f), new Vector3(.1f, .1f, .34f), peau, false);
            Piece(PrimitiveType.Cube, brasDroit, new Vector3(0, 0, -.5f), new Vector3(.13f, .13f, .3f), lin, false);

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
