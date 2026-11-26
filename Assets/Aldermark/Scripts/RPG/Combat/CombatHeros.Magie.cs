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
            DetruireOrbe();
            if (d == null || comp == null) return;
            if (!j.Depenser(comp.Cout(d))) { Avertir("Pas assez de mana"); return; }   // dépenser du mana entraîne l'Esprit
            comp.Declencher(d);
            int niveau = comp.Niveau(d.id);
            Vector3 origine = VersMonde(MainSort);
            ICible cible = Cibles.Existe(verrou) && verrou.Vivant ? verrou : null;
            Vector3 vise = cible != null ? cible.Centre : PointVise();
            string id = d.id;
            Sorts.Lancer(new Incantation
            {
                def = d, auteur = this, camp = Camp.Heros, niveau = niveau,
                attaque = d.forme == FormeSort.Soin ? comp.Soin(d) : comp.AttaqueMagique(d),
                origine = origine, direction = vise - origine, cible = cible,
                rayon = comp.Rayon(d),
                poussee = d.forme == FormeSort.Cone ? d.poussee * (float)Competences.Progression(niveau) : d.poussee,
                touche = (c, tue) =>
                {
                    if (comp != null) comp.Exercer(id, tue ? 3 : 1);
                    if (stats != null) stats.Exercer(Stat.INT, .3);
                },
            });
            comp.Exercer(d.id, d.forme == FormeSort.Soin ? 2 : 1);
            stats.Exercer(Stat.INT, .5);
            j.Secouer(d.forme == FormeSort.Zone ? .1f : .03f);
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
            int niveau = comp != null ? comp.Niveau(sortEnCours.id) : 1;
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

        void MettreAJourAlterations(float dt)
        {
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
