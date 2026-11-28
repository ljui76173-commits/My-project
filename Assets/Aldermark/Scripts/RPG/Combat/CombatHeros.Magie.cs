using UnityEngine;
using UnityEngine.InputSystem;

namespace Aldermark
{
    /// <summary>
    /// La magie du héros : touches 1 à 4 (manette : croix gauche et droite) pour lancer le sort du raccourci.
    /// La main gauche se lève, l'énergie s'y rassemble (une sphère qui grossit), puis le sort part
    /// vers la cible verrouillée, sinon vers le centre de l'écran. Un coup reçu pendant l'incantation l'interrompt.
    /// Et les altérations qui touchent le héros : brûlure, gel, choc.
    /// </summary>
    public partial class CombatHeros
    {
        static readonly Vector3 MainSort = new Vector3(-.24f, -.15f, .62f);

        Competences comp;
        DefCompetence sortEnCours;
        CercleMagique cercleMain, cercleSol, cercleCiel, cercleCible;
        float prochaineRune, prochaineConvergence, prochainArretTemps;
        Transform orbe;
        Light lumiereOrbe;
        float prochainAvertissement, prochaineEtincelleSort, prochaineFlammeHeros;

        public Alterations Alterations { get; } = new Alterations();
        /// <summary>Le sort en cours d'incantation (null : aucun).</summary>
        public DefCompetence SortEnCours => etat == Etat.Incantation ? sortEnCours : null;
        public float ProgresIncantation => etat == Etat.Incantation && sortEnCours != null ? Mathf.Clamp01(tEtat / Mathf.Max(.01f, sortEnCours.incantation)) : 0f;

        void GererSorts(Keyboard kb, Gamepad pad, bool libre)
        {
            if (comp == null || !libre) return;
            int k = -1;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) k = 0;
                else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) k = 1;
                else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) k = 2;
                else if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) k = 3;
            }
            if (k < 0 && pad != null)
            {
                if (pad.dpad.left.wasPressedThisFrame) k = 0;
                else if (pad.dpad.right.wasPressedThisFrame) k = 1;
            }
            if (k < 0) return;
            var d = CatalogueCompetences.Get(comp.Raccourci(k));
            if (d == null) { Avertir(comp.ConnaitUnSort ? "Raccourci vide : place un sort dans tes Compétences (K)" : "Tu ne connais encore aucun sort"); return; }
            if (etat != Etat.Libre && etat != Etat.Recuperation) return;
            if (garde || tRangement >= 0f) return;
            float attente = comp.Attente(d.id);
            if (attente > 0f) { Avertir($"{d.nom} : encore {attente:0.0} s"); return; }
            if (j.Mana < comp.Cout(d)) { Avertir("Pas assez de mana"); return; }

            sortEnCours = d;
            etat = Etat.Incantation; tEtat = 0f;
            courante = null; appui = -1f; comboDemande = false;
            Sons.Jouer("incantation", .5f, HauteurElement(d.element) / Mathf.Max(.35f, d.incantation / .45f));
            CreerOrbe(d);
            CreerCercles(d);
        }

        static float HauteurElement(Element e)
        {
            switch (e)
            {
                case Element.Feu: return .9f;
                case Element.Glace: return 1.25f;
                case Element.Foudre: return 1.1f;
                case Element.Terre: return .7f;
                case Element.Lumiere: return 1.15f;
                default: return 1f;
            }
        }

        void Avertir(string texte)
        {
            if (Time.unscaledTime < prochainAvertissement) return;
            prochainAvertissement = Time.unscaledTime + .9f;
            HUD.Toast(texte);
            Sons.Jouer("clic", .45f, .6f);
        }

        /// <summary>L'incantation est finie : le sort part.</summary>
        void LibererSort()
        {
            var d = sortEnCours;
            sortEnCours = null;
            if (d == null || comp == null) { DetruireOrbe(); return; }
            if (!j.Depenser(comp.Cout(d))) { DetruireOrbe(); Avertir("Pas assez de mana"); return; }   // dépenser du mana entraîne l'Esprit
            // Les cercles éclatent au lancer
            if (cercleMain != null) cercleMain.Liberer();
            if (cercleSol != null) cercleSol.Liberer();
            if (cercleCiel != null) cercleCiel.Liberer();
            if (cercleCible != null) cercleCible.Liberer();
            cercleMain = cercleSol = cercleCiel = cercleCible = null;
            DetruireOrbe();
            comp.Declencher(d);
            int niveau = comp.Niveau(d.id);
            int visuel = comp.NiveauVisuel(d.id);
            Vector3 origine = VersMonde(MainSort);
            ICible cible = Cibles.Existe(verrou) && verrou.Vivant ? verrou : null;
            Vector3 vise = cible != null ? cible.Centre : PointVise();
            string id = d.id;
            Sorts.Lancer(new Incantation
            {
                def = d, auteur = this, camp = Camp.Heros, niveau = niveau, niveauVisuel = visuel,
                attaque = d.forme == FormeSort.Soin ? comp.Soin(d) : comp.AttaqueMagique(d),
                soin = d.forme == FormeSort.Sanctuaire && d.element == Element.Lumiere ? comp.Soin(d) * .12 : 0,
                origine = origine, direction = vise - origine, cible = cible,
                point = AVise(d) ? PointViseSol(d) : vise,
                rayon = comp.Rayon(d),
                poussee = d.forme == FormeSort.Cone ? d.poussee * (float)Competences.Progression(niveau) : d.poussee * Sorts.Ampleur(niveau),
                touche = (c, tue) =>
                {
                    if (comp != null) comp.Exercer(id, tue ? 3 : 1);
                    if (stats != null) stats.Exercer(Stat.INT, .3);
                },
            });
            comp.Exercer(d.id, d.forme == FormeSort.Soin ? 2 : 1);
            stats.Exercer(Stat.INT, .5);
            Decharge(d, niveau, visuel, origine, vise);
        }

        /// <summary>
        /// Le moment où le sort part : éclair dans la main, souffle sur la caméra (le champ de vision s'ouvre d'un coup),
        /// onde au sol, et le recul : plus le sort est puissant, plus il te repousse (un pas en arrière,
        /// puis une vraie glissade, les pieds labourant le sol). Un sort maîtrisé ralentit le temps un instant.
        /// </summary>
        void Decharge(DefCompetence d, int niveau, int visuel, Vector3 origine, Vector3 vise)
        {
            // L'aspect suit le niveau visuel (sort + héros) ; le recul, lui, suit la vraie force du sort
            float a = Sorts.Ampleur(visuel);
            int pal = Sorts.Palier(visuel);
            float puissance = Mathf.Clamp(d.cout / 10f * Sorts.Ampleur(niveau), .3f, 2.5f);
            Color c = d.couleur;
            Vector3 vers = (vise - origine).sqrMagnitude > .01f ? (vise - origine).normalized : transform.forward;
            Vector3 avant = Vector3.ProjectOnPlane(vers, Vector3.up);
            avant = avant.sqrMagnitude > .01f ? avant.normalized : transform.forward;

            // La décharge dans la main
            Effets.Eclat(origine, c, Mathf.RoundToInt(14 * a), 4f * a, .1f, 0f, .35f, vers);
            Effets.Eclat(origine, Color.white, Mathf.RoundToInt(6 * a), 6f * a, .05f, 0f, .2f, vers);
            Sorts.Lueur(origine, c, 5f * a, .22f);
            if (d.forme == FormeSort.Projectile || d.forme == FormeSort.Trait || d.forme == FormeSort.Cone)
            {
                Effets.Anneau(origine + vers * .3f, vers, .45f * a, new Color(c.r, c.g, c.b, .85f), .3f);
                if (pal >= 3) Effets.Anneau(origine + vers * .9f, vers, .8f * a, new Color(c.r, c.g, c.b, .5f), .35f);
            }

            // Le souffle sur la caméra et l'écran
            j.CoupFov(Mathf.Min(18f, 3f + 4.5f * puissance + 1.5f * (pal - 1)));
            j.Secouer(.03f + .05f * puissance);
            HUD.EclairCombat(new Color(c.r, c.g, c.b, .05f + .07f * puissance), .22f, false);
            if (puissance >= 1.1f) Sons.Jouer("onde", Mathf.Min(.6f, .25f * puissance), .65f);

            // Au sol, sous les pieds
            if (puissance >= .9f && j.AuSol)
            {
                Effets.OndeDeChoc(transform.position, 1f + .7f * puissance, new Color(c.r, c.g, c.b, .7f));
                Effets.Poussiere(transform.position, Mathf.RoundToInt(10 * puissance));
            }

            // Le recul
            if (d.forme == FormeSort.Projectile || d.forme == FormeSort.Trait || d.forme == FormeSort.Cone || d.forme == FormeSort.Tornade || d.forme == FormeSort.Ligne)
            {
                if (puissance >= 1.25f && j.AuSol) CommencerGlissade(-avant, puissance * 1.3f, false, true);
                else if (puissance >= .5f) j.Elan(-avant * puissance * 4f, .2f, puissance * 20f);
            }
            if (pal >= 7 && Time.unscaledTime >= prochainArretTemps && d.incantation >= .3f)
            {
                // Le temps s'arrête, puis repart au ralenti (pas à chaque sort : ce serait pénible)
                prochainArretTemps = Time.unscaledTime + 10f;
                Figer(.14f);
                Ralentir(.6f);
                HUD.EclairCombat(new Color(1f, 1f, 1f, pal >= 9 ? .45f : .28f), .4f, false);
            }
            else if (pal >= 4) Ralentir(Mathf.Min(.4f, .22f + .04f * (pal - 4)));
        }

        // ---------- les cercles d'incantation ----------

        void CreerCercles(DefCompetence d)
        {
            AnnulerCercles();
            int niveau = comp != null ? comp.NiveauVisuel(d.id) : 1;
            float a = Sorts.Ampleur(niveau);
            int pal = Sorts.Palier(niveau);
            bool devant = d.forme == FormeSort.Projectile || d.forme == FormeSort.Trait || d.forme == FormeSort.Cone || d.forme == FormeSort.Tornade || d.forme == FormeSort.Ligne;
            // Là où le sort va tomber (pluie, éclair du ciel, prison, magma) : un cercle au sol qui suit la visée
            if (AVise(d))
                cercleCible = CercleMagique.Creer(PointViseSol(d) + Vector3.up * .06f, Quaternion.LookRotation(Vector3.down, Vector3.forward),
                    (d.vagues > 1 ? d.rayon * .6f : d.rayon) * (1f + .3f * (a - 1f)), d.couleur, d.element, pal, false);
            // Devant la main (face à la visée) pour ce qui part au loin
            if (devant)
                cercleMain = CercleMagique.Creer(VersMonde(mainG), Quaternion.LookRotation(DirectionVisee()),
                    Mathf.Min(j.troisiemePersonne ? .42f * a : .16f * a, j.troisiemePersonne ? 1.6f : .3f), d.couleur, d.element, pal, true);
            // Au sol : toujours pour la terre et le soin ; à partir du niveau 4 pour les autres
            if (!devant || pal >= 2)
            {
                float r = d.forme == FormeSort.Zone ? comp.Rayon(d) * .8f : d.forme == FormeSort.Soin ? 1.3f * a : 1.1f * a;
                cercleSol = CercleMagique.Creer(PiedAuSol(), Quaternion.LookRotation(Vector3.down, transform.forward), r, d.couleur, d.element, pal, !devant);
            }
            // Céleste : un cercle immense s'ouvre dans le ciel, au-dessus de ce que tu vises
            if (pal >= 6)
                cercleCiel = CercleMagique.Creer(PositionCiel(), Quaternion.LookRotation(Vector3.down, transform.forward), 6f * a, d.couleur, d.element, pal, true);
        }

        /// <summary>Les sorts qui tombent là où l'on vise (et non devant ou autour de soi).</summary>
        static bool AVise(DefCompetence d) => d.aDistance || d.forme == FormeSort.Pluie || d.forme == FormeSort.Ciel;

        /// <summary>Le point visé au sol, ramené à la portée du sort.</summary>
        Vector3 PointViseSol(DefCompetence d)
        {
            Vector3 p = Cibles.Existe(verrou) && verrou.Vivant ? verrou.Centre : PointVise();
            Vector3 v = Vector3.ProjectOnPlane(p - transform.position, Vector3.up);
            if (v.magnitude > d.portee) p = transform.position + v.normalized * d.portee;
            return new Vector3(p.x, Monde.Hauteur(p.x, p.z), p.z);
        }

        Vector3 PositionCiel()
        {
            Vector3 vers = Cibles.Existe(verrou) && verrou.Vivant ? verrou.Centre : transform.position + Vector3.ProjectOnPlane(j.cam.transform.forward, Vector3.up).normalized * 12f;
            return new Vector3(vers.x, Monde.Hauteur(vers.x, vers.z) + 24f, vers.z);
        }

        Vector3 PiedAuSol()
        {
            Vector3 p = transform.position;
            return new Vector3(p.x, Mathf.Max(p.y, Monde.Hauteur(p.x, p.z)) + .05f, p.z);
        }

        Vector3 DirectionVisee()
        {
            if (Cibles.Existe(verrou) && verrou.Vivant)
            {
                Vector3 v = verrou.Centre - VersMonde(mainG);
                if (v.sqrMagnitude > .01f) return v.normalized;
            }
            return j.cam.transform.forward;
        }

        void AnnulerCercles()
        {
            if (cercleMain != null) cercleMain.Annuler();
            if (cercleSol != null) cercleSol.Annuler();
            if (cercleCiel != null) cercleCiel.Annuler();
            if (cercleCible != null) cercleCible.Annuler();
            cercleMain = cercleSol = cercleCiel = cercleCible = null;
        }

        /// <summary>Ce que vise le centre de l'écran (on traverse son propre corps et les zones invisibles).</summary>
        Vector3 PointVise()
        {
            var cam = j.cam.transform;
            Vector3 loin = cam.position + cam.forward * 60f;
            var hits = Physics.RaycastAll(cam.position, cam.forward, 60f, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (h.collider.GetComponentInParent<CombatHeros>() == this) continue;
                var c = h.collider.GetComponentInParent<ICible>();
                if (h.collider.isTrigger && c == null) continue;
                if (h.distance < 1.2f) continue;   // trop près de la caméra (troisième personne)
                return h.point;
            }
            return loin;
        }

        void AnnulerSort()
        {
            if (sortEnCours == null && orbe == null) return;
            if (sortEnCours != null) HUD.ChiffreTexte(SurSoi(), "Sort interrompu", new Color(.75f, .75f, .8f));
            sortEnCours = null;
            DetruireOrbe();
            if (etat == Etat.Incantation) etat = Etat.Libre;
        }

        // ---------- la sphère d'énergie dans la main ----------

        void CreerOrbe(DefCompetence d)
        {
            DetruireOrbe();
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(go.GetComponent<Collider>());
            go.name = "Incantation";
            go.GetComponent<MeshRenderer>().sharedMaterial = Sorts.Materiau(new Color(d.couleur.r, d.couleur.g, d.couleur.b, .75f));
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            orbe = go.transform;
            orbe.localScale = Vector3.zero;
            var noyau = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(noyau.GetComponent<Collider>());
            noyau.transform.SetParent(orbe, false);
            noyau.transform.localScale = Vector3.one * .5f;
            noyau.GetComponent<MeshRenderer>().sharedMaterial = Sorts.Materiau(new Color(1f, .97f, .9f, .9f));
            noyau.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lumiereOrbe = go.AddComponent<Light>();
            lumiereOrbe.type = LightType.Point; lumiereOrbe.color = d.couleur; lumiereOrbe.range = 3f; lumiereOrbe.intensity = 0f; lumiereOrbe.shadows = LightShadows.None;
            PlacerOrbe();
        }

        void PlacerOrbe()
        {
            if (orbe == null) return;
            if (etat != Etat.Incantation || sortEnCours == null) { DetruireOrbe(); return; }
            float u = ProgresIncantation;
            int niveau = comp != null ? comp.NiveauVisuel(sortEnCours.id) : 1;
            // Les cercles suivent la main et les pieds, et s'avivent à mesure que l'énergie monte
            if (cercleMain != null)
            {
                Vector3 dir = DirectionVisee();
                cercleMain.transform.SetPositionAndRotation(VersMonde(mainG) + dir * (j.troisiemePersonne ? .25f : .12f), Quaternion.LookRotation(dir));
                cercleMain.Progres = u;
            }
            if (cercleCible != null)
            {
                cercleCible.transform.position = Vector3.Lerp(cercleCible.transform.position, PointViseSol(sortEnCours) + Vector3.up * .06f, 1f - Mathf.Exp(-Time.deltaTime * 12f));
                cercleCible.Progres = u;
            }
            if (cercleCiel != null)
            {
                cercleCiel.transform.position = Vector3.Lerp(cercleCiel.transform.position, PositionCiel(), 1f - Mathf.Exp(-Time.deltaTime * 3f));
                cercleCiel.Progres = u;
            }
            // L'énergie qui afflue vers la main, la lueur sur les bords de l'écran, le jour qui s'assombrit (céleste)
            {
                int palC = Sorts.Palier(niveau);
                Color ce = sortEnCours.couleur;
                Vector3 main = VersMonde(mainG);
                if (Time.time >= prochaineConvergence)
                {
                    prochaineConvergence = Time.time + .07f / palC;
                    Vector3 depuis = main + Random.onUnitSphere * (j.troisiemePersonne ? 1.6f : .7f) * Sorts.Ampleur(niveau);
                    Effets.Eclat(depuis, ce, 1, Vector3.Distance(depuis, main) / .22f, (j.troisiemePersonne ? .07f : .035f), 0f, .22f, main - depuis);
                }
                if (palC >= 2) HUD.Voile(new Color(ce.r, ce.g, ce.b, Mathf.Min(.5f, .07f + .05f * palC) * u));
                if (palC >= 6) HUD.Assombrir(u);
            }
            if (cercleSol != null)
            {
                cercleSol.transform.SetPositionAndRotation(PiedAuSol(), Quaternion.LookRotation(Vector3.down, transform.forward));
                cercleSol.Progres = u;
                // Des runes de lumière montent du bord du cercle
                if (Time.time >= prochaineRune)
                {
                    prochaineRune = Time.time + .09f / Sorts.Palier(niveau);
                    float ang = Random.value * Mathf.PI * 2f;
                    Vector3 bord = cercleSol.transform.position + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * cercleSol.Rayon * Random.Range(.6f, 1f);
                    Effets.Eclat(bord, sortEnCours.couleur, 2, .3f, .07f, -.9f, .9f);
                }
            }
            float a = Sorts.Ampleur(niveau);
            int pal = Sorts.Palier(niveau);
            Vector3 p = VersMonde(mainG) + DirMonde(new Vector3(.04f, .05f, .09f));
            // Plus le sort est haut, plus l'énergie dans la main est grosse, vive et agitée
            float taille = (j.troisiemePersonne ? .26f : .1f) * (.25f + .75f * u) * (1f + .08f * (niveau - 1)) * (1f + .08f * Mathf.Sin(Time.time * 30f));
            orbe.position = p;
            orbe.localScale = Vector3.one * taille;
            lumiereOrbe.intensity = 2.5f * u * a;
            lumiereOrbe.range = 3f * a;
            if (Time.time >= prochaineEtincelleSort)
            {
                prochaineEtincelleSort = Time.time + .07f / pal;
                Vector3 autour = p + Random.onUnitSphere * taille * 2.5f;
                Effets.Eclat(autour, sortEnCours.couleur, pal, .4f + .1f * pal, taille * .35f, 0f, .25f, p - autour);
            }
        }

        void DetruireOrbe()
        {
            AnnulerCercles();
            if (orbe != null) Destroy(orbe.gameObject);
            orbe = null;
            lumiereOrbe = null;
        }

        // ---------- les altérations du héros ----------

        void SubirEffet(Coup c)
        {
            switch (c.effet)
            {
                case Effet.Choc:
                    if (etat != Etat.Projete && etat != Etat.Glissade) Sonner(c.dureeEffet * .7f);
                    HUD.ChiffreTexte(SurSoi() + Vector3.up * .2f, "Étourdi", new Color(1f, .95f, .5f));
                    break;
                case Effet.GelNet:
                    Alterations.GelerNet(c.dureeEffet * .6f);
                    if (etat != Etat.Projete && etat != Etat.Glissade) Sonner(c.dureeEffet * .6f);
                    HUD.ChiffreTexte(SurSoi() + Vector3.up * .2f, "Gelé !", new Color(.65f, .9f, 1f));
                    HUD.EclairCombat(new Color(.6f, .85f, 1f, .4f), .6f, true);
                    break;
                case Effet.Brulure:
                case Effet.Gel:
                    if (Alterations.Appliquer(c.effet, c.degats * .5, c.dureeEffet, c.auteur))
                    {
                        if (etat != Etat.Projete && etat != Etat.Glissade) Sonner(Alterations.Gele * .7f);
                        HUD.ChiffreTexte(SurSoi() + Vector3.up * .2f, "Gelé !", new Color(.65f, .9f, 1f));
                        HUD.EclairCombat(new Color(.6f, .85f, 1f, .4f), .6f, true);
                    }
                    else HUD.ChiffreTexte(SurSoi() + Vector3.up * .2f, c.effet == Effet.Brulure ? "En feu ! (Alt : roule)" : "Ralenti", c.effet == Effet.Brulure ? new Color(1f, .6f, .3f) : new Color(.65f, .9f, 1f));
                    break;
            }
        }

        // ---------- la régénération ----------
        float regenParSeconde, finRegen, prochaineLueurRegen;
        Color couleurRegen = Color.green;

        /// <summary>La vie remonte toute seule pendant un moment (sort de Régénération).</summary>
        public void Regenerer(double total, float duree, Color couleur)
        {
            float reste = Mathf.Max(0f, finRegen - Time.time) * regenParSeconde;
            finRegen = Time.time + duree;
            regenParSeconde = (reste + (float)total) / duree;
            couleurRegen = couleur;
        }

        void MettreAJourRegeneration(float dt)
        {
            if (finRegen <= 0f || stats == null) return;
            if (Time.time >= finRegen || mort) { finRegen = 0f; regenParSeconde = 0f; return; }
            stats.Rendre(regenParSeconde * dt);
            if (Time.time >= prochaineLueurRegen)
            {
                prochaineLueurRegen = Time.time + .3f;
                Vector3 p = j.troisiemePersonne ? transform.position + Vector3.up * Random.Range(.2f, 1.6f) + Random.insideUnitSphere * .35f
                                                 : j.cam.transform.TransformPoint(new Vector3(Random.Range(-.5f, .5f), -.5f, .7f));
                Effets.Eclat(p, couleurRegen, 2, .5f, j.troisiemePersonne ? .1f : .05f, -.8f, .9f);
            }
        }

        void MettreAJourAlterations(float dt)
        {
            MettreAJourRegeneration(dt);
            if (!Alterations.Actif) return;
            if (j.Nage && Alterations.Brulure > 0f) Alterations.Eteindre();
            double brule = Alterations.Avancer(dt);
            if (brule > 0 && Vivant)
            {
                stats.Blesser(brule);
                HUD.ChiffreDegats(SurSoi(), brule, false, true);
                if (stats.Vie <= 0 && !mort) { StartCoroutine(Mourir()); return; }
            }
            if (Time.time < prochaineFlammeHeros) return;
            if (Alterations.Brulure > 0f)
            {
                prochaineFlammeHeros = Time.time + .1f;
                Vector3 p = j.troisiemePersonne ? transform.position + Vector3.up * Random.Range(.3f, 1.6f) + Random.insideUnitSphere * .3f
                                                 : j.cam.transform.TransformPoint(new Vector3(Random.Range(-.5f, .5f), -.55f, .6f));
                Effets.Eclat(p, new Color(1f, Random.Range(.35f, .65f), .1f, .9f), 2, 1.2f, j.troisiemePersonne ? .16f : .08f, -.8f, .4f);
                HUD.EclairCombat(new Color(1f, .45f, .1f, .12f), .25f, true);
            }
            else if (Alterations.Gel > 0f)
            {
                prochaineFlammeHeros = Time.time + .35f;
                HUD.EclairCombat(new Color(.6f, .85f, 1f, .14f), .5f, true);
            }
        }
    }
}
