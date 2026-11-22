using System.Collections.Generic;
using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// La bataille du Gué, à l'aube, dans la brume :
    /// 1. la barge noire descend la rivière et bute sur la chaîne d'Hadrien (ou accoste, sans chaîne) ;
    /// 2. le bras de fer : Siphon (R maintenu) sur Vesk pour lui couper le nœud, pendant qu'il force par vagues ;
    ///    à la fin, il lance une boule de flammes noires sur Aldric, et Ilse la pare ;
    /// 3. la rive : les Cendrés nagent jusqu'au quai ou prennent leur barque jusqu'à la grève, et tombent dans les pièges ;
    ///    ceux qui passent heurtent la ligne de Garr. Si Aldric lâche le Siphon, la main noire de Vesk se rallume ;
    /// 4. Vesk saute à l'eau ; Ilse le prend à revers (Pas d'Ombre) et lui tranche le jarret ;
    /// 5. le verdict : l'épargner (et sceller son lien au mana) ou laisser Ilse le tuer. Puis le bilan et la fin de l'arc 1.
    /// </summary>
    public partial class BatailleDuGue
    {
        enum Phase { Aucune, Arrivee, BrasDeFer, Rive, Duel, Verdict, Fin }
        enum EtatC { Pont, Nage, Barque, Marche, Pris, Ligne, Maitrise, Fuite, Enfui, Rattrape }

        class Cendre
        {
            public Habitant h;
            public Vector2[] chemin;
            public int etape;
            public EtatC etat;
            public float combat, enfonce, attente;
            public bool deserteur;
        }

        class Projectile { public Transform t; public Vector3 a, b; public float duree, u, arc; public System.Action fin; }

        Phase phase;
        readonly List<Cendre> cendres = new List<Cendre>();
        readonly List<Projectile> projectiles = new List<Projectile>();
        readonly List<(float quand, System.Action quoi)> plusTard = new List<(float, System.Action)>();
        readonly List<(Transform filet, Vector3 de, Vector3 a, float u)> chutes = new List<(Transform, Vector3, Vector3, float)>();
        Transform barge, barque;
        Habitant vesk, ilse, garr, hadrien, bram, marthe, pell, lise, colin;
        SourceMana sourceVesk;
        Interlocuteur parleVesk;
        LineRenderer fluxVesk;
        Material matBrasVesk, matParticules;
        Emplacement chaine, clochesFuite;
        Vector2 versVillage, cote, cibleBarque, arretVesk;
        Vector3 positionAldric;
        float tPhase, prise, vague = 5f, sansSiphon, zBarge = 120f, duelFin, angleDuel, fumee, brouillardDebut, brouillardFin;
        bool vagueActive, alerte, fuiteGardee, puise, premierContact, manaVideDit, lignePliee, debarquementQuai, barqueEnRoute, nettoye, ligneDite, boueDite, trapesDite;
        int blesses, echappes, filetsTombes, ilseBlessures, prochainDebarque, etapeDuel;
        float minuteurDebarque;
        bool garrBlesse, pellSonne, fioleUtilisee;
        readonly Dictionary<Habitant, Vector3> postesInitiaux = new Dictionary<Habitant, Vector3>();

        bool Tient => Pouvoirs.Cible != null && Pouvoirs.Cible == sourceVesk && j.Mana > .3f;
        static Vector2 P(Transform t) => new Vector2(t.position.x, t.position.z);
        bool SurQuai(Vector2 p) => p.x > quaiBout.x - .6f && p.x < quaiRacine.x + .6f && Mathf.Abs(p.y - 14f) < 1.4f;
        bool Nage(Vector2 p) => !SurQuai(p) && Monde.Hauteur(p.x, p.y) < -.75f;
        Vector3 MainVesk => vesk.transform.position + Vector3.up * 1.15f * vesk.taille + vesk.transform.right * .4f;

        // ------------------------------------------------------------------
        // Lancement : l'aube, la brume, chacun à son poste
        // ------------------------------------------------------------------

        void Lancer()
        {
            EnCours = true;
            Progression.Mettre("bataille_lancee");
            phase = Phase.Arrivee; tPhase = 0;
            Chantier.FixerHeure(6.3f);
            chaine = null; clochesFuite = null;
            foreach (var e in emplacements)
            {
                if (e.type == TypePiege.Chaine && e.trace && e.bon) chaine = e;
                if (e.nom.StartsWith("Sentier du bois")) clochesFuite = e;
            }
            alerte = Trace("Rive en amont — l'alerte");
            fuiteGardee = clochesFuite != null && clochesFuite.trace;
            brouillardDebut = RenderSettings.fogStartDistance; brouillardFin = RenderSettings.fogEndDistance;
            RenderSettings.fogStartDistance = 6f; RenderSettings.fogEndDistance = 120f;
            HUD.Fondu(2.5f);
            HUD.Invite(null);

            // Aldric derrière les arbres, au bout du chemin du quai
            positionAldric = new Vector3(quaiRacine.x + 1f, 0, 12.6f);
            positionAldric.y = Monde.Hauteur(positionAldric.x, positionAldric.z) + .1f;
            Vector3 versBarge = new Vector3(R(37f), 0, 37f) - positionAldric;
            j.Teleporter(positionAldric, Mathf.Atan2(versBarge.x, versBarge.z) * Mathf.Rad2Deg);
            j.Regarder(4f);
            j.FixerMana(j.manaMax);
            if (!Pouvoirs.SiphonDebloque)
            {
                Progression.Mettre("siphon");
                PlusTard(9f, () => HUD.Message("LE SCEAU S'ÉVEILLE — SIPHON", "Face à Vesk, le Sceau trouve d'instinct comment fermer une vanne.\nR maintenu : vider une source de mana visée, à moins de 30 m.", 7f));
            }

            Mobiliser();
            ConstruireFlotte();
            HUD.Message("⚔ LE GUÉ DE VARENNE — AUBE DU JOUR " + Chantier.Jour,
                (alerte ? "La brume couvre la rivière. Dans le silence, une sonnaille tinte, côté amont : la milice est déjà en position.\n"
                        : "La brume couvre la rivière. Personne n'a rien entendu venir : la milice court encore à ses postes.\n")
                + (chaine != null ? "La chaîne est tendue sous l'eau ; Hadrien a les mains posées dessus, et l'eau commence à fumer.\n" : "Il n'y a pas de chaîne dans la rivière.\n")
                + "\nUne longue barge noire sort de la brume. À la proue, un homme immense, une main gantée de cuir,\nl'autre entièrement noire, comme du charbon. Vesk.", 11f);
        }

        Habitant Trouver(string id)
        {
            foreach (var i in Interlocuteur.Tous) if (i.id == id) return i.GetComponent<Habitant>();
            return null;
        }

        void Poster(Habitant h, Vector2 p, Vector2 regard)
        {
            if (h == null) return;
            Mobilises.Add(h);
            if (!postesInitiaux.ContainsKey(h)) postesInitiaux[h] = h.transform.position;
            foreach (var r in h.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
            h.geste = Geste.Aucun; h.regarder = null; h.Porter(null);
            h.transform.position = new Vector3(p.x, Sol(p), p.y);
            if ((regard - p).sqrMagnitude > .01f) h.transform.rotation = Quaternion.LookRotation(new Vector3(regard.x - p.x, 0, regard.y - p.y));
            h.AllerVers(p, 1f);
        }

        /// <summary>Un poste sur la ligne de Garr : k de côté, « recul » vers le village.</summary>
        Vector2 PosteLigne(float k, float recul) => ligne + cote * k + versVillage * recul;

        void Mobiliser()
        {
            versVillage = (Monde.Village - ligne).normalized;
            cote = new Vector2(-versVillage.y, versVillage.x);
            garr.gameObject.SetActive(true);
            hadrien = Trouver("forgeron"); bram = Trouver("bram"); marthe = Trouver("marthe"); pell = Trouver("pell"); lise = Trouver("lise"); colin = Trouver("colin");
            var q = FindFirstObjectByType<QueteIlse>();
            ilse = q != null && q.ilse != null ? q.ilse.GetComponent<Habitant>() : null;

            Poster(garr, PosteLigne(0, 0), jonction);
            Poster(bram, PosteLigne(1.3f, 0), jonction);
            Poster(colin, PosteLigne(-1.3f, 0), jonction);
            Poster(Trouver("firmin"), PosteLigne(.6f, 1.6f), jonction);
            Poster(Trouver("odon"), PosteLigne(-.7f, 3.2f), jonction);
            Poster(Trouver("agathe"), PosteLigne(0, 7f), jonction);
            Poster(hadrien, chaine != null ? new Vector2(chaine.pos.x + 10.2f, chaine.pos.y) : PosteLigne(-2.4f, .4f), chaine != null ? chaine.pos : jonction);
            if (hadrien != null && chaine != null) hadrien.regarder = chaine.racine.position;
            Poster(Trouver("tobin"), alerte ? Emp("Rive en amont — l'alerte").pos + new Vector2(2f, 0) : PosteLigne(2.4f, 1f), quaiBout);
            Poster(Trouver("joss"), Emp("Saule du quai").pos + new Vector2(2.6f, 1.6f), quaiBout);
            Poster(pell, Emp("Saule de la grève").pos + new Vector2(2.6f, 1.6f), plage);
            Poster(marthe, Libre(new Vector2(31f, 16f)), plage);
            if (lise != null)
            {
                Poster(lise, Monde.Village + new Vector2(-6f, -3f), Monde.Village);
                foreach (var r in lise.GetComponentsInChildren<Renderer>(true)) r.enabled = false;   // à l'abri chez Marthe
            }
            if (ilse != null) ilse.gameObject.SetActive(false);   // quelque part dans l'ombre d'un saule
        }

        Emplacement Emp(string nom)
        {
            foreach (var e in emplacements) if (e.nom == nom) return e;
            return emplacements[0];
        }

        void ConstruireFlotte()
        {
            barge = new GameObject("BargeNoire").transform;
            barge.SetParent(transform, false);
            PlacerBarge(zBarge);
            var coque = new Color(.1f, .09f, .09f);
            Local(barge, new Vector3(0, .2f, 0), new Vector3(3.4f, 1.3f, 12f), coque);
            Local(barge, new Vector3(0, .25f, 6.2f), new Vector3(2.4f, 1.15f, 2.4f), coque, new Vector3(0, 45f, 0));
            Local(barge, new Vector3(0, .88f, 0), new Vector3(3f, .08f, 11.6f), new Color(.17f, .14f, .12f));
            foreach (float x in new[] { -1.6f, 1.6f }) Local(barge, new Vector3(x, 1.2f, 0), new Vector3(.12f, .6f, 11.6f), coque);
            Local(barge, new Vector3(0, 4.2f, -1f), new Vector3(.22f, 6.8f, .22f), new Color(.2f, .15f, .1f));
            Local(barge, new Vector3(-.3f, 4.8f, -1.12f), new Vector3(2.6f, 3.6f, .05f), new Color(.17f, .16f, .16f), new Vector3(0, 0, 4f));
            Local(barge, new Vector3(1.1f, 3.4f, -1.12f), new Vector3(1.3f, 2f, .05f), new Color(.14f, .13f, .13f), new Vector3(0, 0, -9f));

            vesk = Soldat(barge, "Vesk", new Vector3(0, .92f, 4.6f), 1.3f, new Color(.2f, .12f, .12f), new Color(.24f, .2f, .2f));
            sourceVesk = vesk.gameObject.AddComponent<SourceMana>();
            sourceVesk.nom = "Vesk"; sourceVesk.reserve = 1000f; sourceVesk.debit = 0f; sourceVesk.hauteur = 1.7f;
            for (int i = 0; i < 12; i++)
            {
                var h = Soldat(barge, "Cendre", new Vector3(i % 2 == 0 ? -.75f : .75f, .92f, 3f - (i / 2) * 1.15f), 1f, new Color(.11f, .11f, .13f), new Color(.07f, .07f, .07f));
                cendres.Add(new Cendre { h = h, etat = EtatC.Pont, deserteur = i == 11 });
            }

            var go = new GameObject("FluxDuNoeud");
            go.transform.SetParent(transform, false);
            fluxVesk = go.AddComponent<LineRenderer>();
            fluxVesk.useWorldSpace = true; fluxVesk.positionCount = 24; fluxVesk.widthMultiplier = .3f;
            fluxVesk.sharedMaterial = Unlit(new Color(.4f, .65f, 1f));
            fluxVesk.enabled = false;

            barque = new GameObject("BarqueDesCendres").transform;
            barque.SetParent(transform, false);
            Local(barque, new Vector3(0, .1f, 0), new Vector3(1.7f, .55f, 4.4f), new Color(.16f, .12f, .1f));
            barque.gameObject.SetActive(false);

            matParticules = MateriauParticules();
        }

        Habitant Soldat(Transform parent, string nom, Vector3 local, float taille, Color tunique, Color cheveux)
        {
            var go = new GameObject(nom);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.identity;
            var h = go.AddComponent<Habitant>();
            h.tunique = tunique; h.cheveux = cheveux; h.taille = taille; h.promeneur = false; h.hauteurLibre = true;
            return h;
        }

        /// <summary>La barge suit le milieu de la rivière vers le sud ; sans chaîne, elle vient se ranger le long du quai.</summary>
        void PlacerBarge(float z)
        {
            float X(float zz) => chaine != null ? R(zz) : Mathf.Lerp(R(zz), quaiBout.x - 2.7f, Mathf.InverseLerp(36f, 16f, zz));
            float x = X(z);
            barge.position = new Vector3(x, -.2f, z);
            barge.rotation = Quaternion.LookRotation(new Vector3(X(z - 1.5f) - x, 0, -1.5f));
        }

        // ------------------------------------------------------------------
        // Le déroulement, image par image
        // ------------------------------------------------------------------

        void Combat()
        {
            float dt = Time.deltaTime;
            tPhase += dt;
            for (int i = plusTard.Count - 1; i >= 0; i--)
                if (Time.time >= plusTard[i].quand) { var a = plusTard[i].quoi; plusTard.RemoveAt(i); a(); }
            Projectiles(dt);
            Chutes(dt);
            if (phase == Phase.Fin) return;

            switch (phase)
            {
                case Phase.Arrivee: Arrivee(dt); break;
                case Phase.BrasDeFer: BrasDeFer(dt); break;
                case Phase.Rive: Rive(dt); break;
                case Phase.Duel: Duel(dt); break;
            }
            if (phase >= Phase.BrasDeFer && phase <= Phase.Duel)
            {
                foreach (var c in cendres) Avancer(c, dt);
                Debarquer(dt);
                Barque(dt);
                Melee();
            }
            j.entrave = phase >= Phase.BrasDeFer && phase <= Phase.Duel && Tient ? .3f : 1f;
            Decor(dt);
            int pris = 0;
            foreach (var c in cendres) if (c.etat == EtatC.Pris || c.etat == EtatC.Maitrise || c.etat == EtatC.Rattrape) pris++;
            HUD.Bilan(phase == Phase.Verdict ? null : $"Cendrés hors de combat : {pris} / 12\nPassés jusqu'à la ligne : {echappes}\nVillageois blessés : {blesses}");
        }

        void PlusTard(float s, System.Action a) => plusTard.Add((Time.time + s, a));

        void Arrivee(float dt)
        {
            float zArret = chaine != null ? 40f : 14f;
            zBarge = Mathf.MoveTowards(zBarge, zArret, 3.4f * dt);
            PlacerBarge(zBarge);
            puise = zBarge < 90f;
            HUD.Jauge(null, -1f);
            if (zBarge > zArret + .01f) return;

            phase = Phase.BrasDeFer; tPhase = 0;
            if (chaine != null)
            {
                Sons.JouerA("coup", barge.position, 1f, .22f);
                Sons.JouerA("coup", barge.position, .9f, .32f);
                barge.rotation *= Quaternion.Euler(-4f, 0, 0);
                for (int k = 0; k < 3; k++) Plonger(cendres[k], k * .25f);
                HUD.Message("⛓ CRAC — LA BARGE BUTE SUR LA CHAÎNE",
                    "Le choc fait tomber trois hommes à l'eau. La coque se bloque, le nez soulevé, à 20 m du quai.\nUn Cendré qui tente de saisir la chaîne hurle et lâche prise : Hadrien l'a chauffée au rouge.\n\nVesk lève sa main noire : des veines bleues s'allument sous l'eau. Il puise dans le nœud.\nSIPHON (R maintenu) sur Vesk : coupe-lui la ligne, comme on ferme une vanne.", 10f);
            }
            else
            {
                debarquementQuai = true;
                HUD.Message("LA BARGE ACCOSTE AU QUAI",
                    "Rien n'arrête la barge : elle glisse jusqu'au quai, et les Cendrés sautent sur les planches.\n\nVesk lève sa main noire : il puise dans le nœud.\nSIPHON (R maintenu) sur Vesk : coupe-lui la ligne !", 9f);
            }
        }

        void BrasDeFer(float dt)
        {
            float force = NoeudDeVarenne.Force;
            vague -= dt;
            if (vague <= 0f)
            {
                vagueActive = !vagueActive;
                vague = vagueActive ? 2.4f : 5.5f;
                if (vagueActive && Tient)
                {
                    HUD.SousTitre("Vesk arrache son gant et pose les deux mains sur le bastingage. Il force. Le flux revient vers lui comme une marée."
                        + (Pouvoirs.VoileActif ? "\n⚠ Le Voile vacille. Le Réceptacle chauffe, du sang perle à ton nez." : "\nLe Réceptacle chauffe, du sang perle à ton nez."), 3f);
                }
            }
            if (Tient)
            {
                if (!premierContact)
                {
                    premierContact = true;
                    Sons.Voix(.5f);
                    HUD.SousTitre("Vesk se fige. Sa main noire s'éteint d'un coup. Il tourne lentement la tête… « Toi. Le porteur. »", 5f);
                }
                prise += dt * (vagueActive ? -.11f * force : .095f * (1.3f - .5f * force));
                j.Depenser(dt * j.manaMax * (vagueActive ? .04f : .004f));
                sansSiphon = 0f;
                puise = false;
            }
            else
            {
                prise -= dt * .03f;
                sansSiphon += dt;
                puise = true;
                if (sansSiphon > 6f) { sansSiphon = 0f; FeuNoir(); }
            }
            if (j.Mana <= .3f && Pouvoirs.Cible == sourceVesk && !manaVideDit)
            {
                manaVideDit = true;
                HUD.Message("PLUS DE MANA", "Le Réceptacle est vide : ta prise glisse. Attends que le mana revienne un peu, et reprends-le.", 4f);
            }
            prise = Mathf.Clamp01(prise);
            HUD.Jauge(vagueActive && Tient ? "BRAS DE FER — Vesk force !" : "BRAS DE FER — tiens le Siphon (R) sur Vesk", prise);
            if (prise >= 1f) Parade();
        }

        /// <summary>Vesk lâche le nœud et jette ce qui lui reste sur Aldric : Ilse surgit et pare.</summary>
        void Parade()
        {
            phase = Phase.Rive; tPhase = 0;
            HUD.Jauge(null, -1f);
            HUD.SousTitre("Avec un cri de rage, Vesk lâche le nœud et lance ce qui lui reste sur toi : une boule de flammes noires.", 3f);
            Vector3 cam = j.cam.transform.position;
            Vector3 versVesk = vesk.transform.position - cam; versVesk.y = 0; versVesk.Normalize();
            Vector3 garde = cam + versVesk * 1.7f;
            garde.y = Monde.Hauteur(garde.x, garde.z);
            PlusTard(.75f, () => Apparaitre(ilse, garde, versVesk));
            Feu(MainVesk, garde + Vector3.up * 1.2f, 1.3f, () =>
            {
                HUD.Flash(new Color(1f, .45f, .1f), .5f);
                Sons.Jouer("coup", 1f, .3f);
                Eclat(garde + Vector3.up * 1.2f, new Color(1f, .4f, .1f), 40);
                ilseBlessures++;
                HUD.SousTitre("Ilse surgit de l'ombre d'un saule et pare avec sa lame. Le feu lui brûle l'avant-bras. Elle ne crie pas.\n« Garde ta vanne fermée. Je m'occupe du reste. »", 6f);
                if (ilse != null) { ilse.geste = Geste.Aucun; Sons.Voix(1.2f); }
                DebutRive();
            });
        }

        void Apparaitre(Habitant h, Vector3 p, Vector3 regard)
        {
            if (h == null) return;
            Eclat(p + Vector3.up, new Color(.05f, .05f, .08f), 30);
            h.gameObject.SetActive(true);
            Mobilises.Add(h);
            h.transform.SetPositionAndRotation(p, Quaternion.LookRotation(regard));
            h.AllerVers(new Vector2(p.x, p.z), 1f);
        }

        void DebutRive()
        {
            HUD.Message("🕸 LA RIVE",
                "Sans magie, Vesk ordonne l'assaut à l'ancienne : ses hommes sautent à l'eau et nagent vers le quai, d'autres descendent dans la barque de bord.\n\nGarde le Siphon (R) sur Vesk : si tu lâches, sa main noire se rallume.", 8f);
            if (chaine != null)
            {
                for (int k = 3; k < 6; k++) Plonger(cendres[k], (k - 3) * .9f);
                PlusTard(1.5f, LancerBarque);
            }
            if (hadrien != null) { hadrien.regarder = null; hadrien.AllerVers(PosteLigne(-2.4f, .4f), 4.5f); }
        }

        void Rive(float dt)
        {
            Garder(dt);
            HUD.Jauge(Tient ? "VANNE FERMÉE — Vesk ne puise plus" : "VANNE OUVERTE — reprends Vesk (R) !", Tient ? 1f : 0f);
            if (tPhase > 14f && Resolus()) DebutDuel();
        }

        /// <summary>Tenir le Siphon pendant l'assaut : si Aldric lâche, Vesk se rallume et tire.</summary>
        void Garder(float dt)
        {
            if (Tient)
            {
                sansSiphon = 0f; puise = false;
                j.FixerMana(j.Mana + .2f * dt);   // la vanne est fermée : tenir coûte moins que lutter
            }
            else
            {
                sansSiphon += dt; puise = true;
                if (sansSiphon > 3.5f) { sansSiphon = 0f; FeuNoir(); }
            }
        }

        bool Resolus()
        {
            foreach (var c in cendres)
                if (c.etat != EtatC.Pris && c.etat != EtatC.Maitrise && c.etat != EtatC.Enfui && c.etat != EtatC.Rattrape) return false;
            return true;
        }

        // ---------- les Cendrés ----------

        void Plonger(Cendre c, float attente)
        {
            Vector3 p = barge.TransformPoint(new Vector3(-2.1f, 0, c.h.transform.localPosition.z));
            c.h.transform.SetParent(transform, true);
            c.h.transform.position = new Vector3(p.x, -.95f, p.z);
            c.etat = EtatC.Nage; c.chemin = cheminA; c.etape = 0; c.attente = attente;
            Eclat(new Vector3(p.x, 0, p.z), new Color(.85f, .9f, 1f), 14);
        }

        void Debarquer(float dt)
        {
            if (!debarquementQuai || prochainDebarque >= cendres.Count) return;
            minuteurDebarque -= dt;
            if (minuteurDebarque > 0) return;
            minuteurDebarque = .6f;
            var c = cendres[prochainDebarque++];
            c.h.transform.SetParent(transform, true);
            c.h.transform.position = new Vector3(quaiBout.x + .3f, QuaiY, 14f + Random.Range(-.6f, .6f));
            c.etape = 1;
            if (c.deserteur) { c.etat = EtatC.Fuite; c.chemin = fuiteQuai; }
            else { c.etat = EtatC.Marche; c.chemin = cheminA; }
        }

        void LancerBarque()
        {
            barque.gameObject.SetActive(true);
            barque.position = barge.TransformPoint(new Vector3(-2.8f, -.25f, -1f));
            cibleBarque = new Vector2(plage.x - 2.6f, plage.y);
            barque.rotation = Quaternion.LookRotation(new Vector3(cibleBarque.x - barque.position.x, 0, cibleBarque.y - barque.position.z));
            for (int k = 6; k < 12; k++)
            {
                var c = cendres[k];
                c.h.transform.SetParent(barque, false);
                c.h.transform.localPosition = new Vector3((k % 2 == 0 ? -.4f : .4f), .38f, -1.4f + (k - 6) / 2 * 1.2f);
                c.h.transform.localRotation = Quaternion.identity;
                c.etat = EtatC.Barque;
            }
            barqueEnRoute = true;
            HUD.SousTitre("Six Cendrés descendent dans la barque de bord et rament vers la grève.", 3f);
        }

        void Barque(float dt)
        {
            if (!barqueEnRoute) return;
            Vector2 p = P(barque);
            Vector2 d = cibleBarque - p;
            if (d.magnitude > .3f)
            {
                p += d.normalized * Mathf.Min(2.2f * dt, d.magnitude);
                barque.position = new Vector3(p.x, -.2f, p.y);
                return;
            }
            barqueEnRoute = false;
            for (int k = 6; k < 12; k++)
            {
                var c = cendres[k];
                c.h.transform.SetParent(transform, true);
                c.h.transform.position = new Vector3(plage.x - 1f + (k % 2) * .6f, Sol(plage), plage.y + (k - 6) * .3f);
                c.attente = (k - 6) * .55f;
                c.etape = 1;
                if (c.deserteur) { c.etat = EtatC.Fuite; c.chemin = fuite; }
                else { c.etat = EtatC.Marche; c.chemin = cheminB; }
            }
        }

        void Avancer(Cendre c, float dt)
        {
            if (c.etat == EtatC.Ligne) { Combattre(c, dt); return; }
            if (c.etat != EtatC.Nage && c.etat != EtatC.Marche && c.etat != EtatC.Fuite) return;
            if (c.attente > 0) { c.attente -= dt; return; }
            if (c.etape >= c.chemin.Length) { Arriver(c); return; }
            Vector2 p = P(c.h.transform);
            Vector2 but = c.chemin[c.etape];
            if (Vector2.Distance(p, but) < .6f) { c.etape++; return; }
            bool nage = Nage(p);
            c.h.AllerVers(but, nage ? 1.35f : c.etat == EtatC.Fuite ? 3.4f : 2.5f);
            if (c.etat == EtatC.Nage && !nage) c.etat = EtatC.Marche;
            if (c.etat == EtatC.Marche) Pieges(c, p);
            else if (c.etat == EtatC.Fuite) Cloches(c, p);
        }

        void Arreter(Cendre c, EtatC etat, float enfonce)
        {
            c.etat = etat; c.enfonce = enfonce;
            c.h.AllerVers(P(c.h.transform), 1f);
            c.h.geste = Geste.Aucun;
        }

        void Pieges(Cendre c, Vector2 p)
        {
            foreach (var e in emplacements)
            {
                if (!e.trace || e.detruit) continue;
                float d = Vector2.Distance(p, e.pos);
                if (e.type == TypePiege.Fosse && d < 2f && e.captures < 2)
                {
                    Prendre(c, e, .6f);
                    if (!boueDite) { boueDite = true; HUD.SousTitre("Les premiers s'enfoncent dans la fosse à boue jusqu'aux cuisses. Figés.", 4f); }
                    Sons.JouerA("pasTerre", e.racine.position, .8f, .5f);
                }
                else if (e.type == TypePiege.Filet && !e.tombe && d < 1.6f) LacherFilet(e);
                else if (e.type == TypePiege.ChausseTrape && d < 2.2f && e.captures < 2)
                {
                    Prendre(c, e, .3f);
                    if (!trapesDite) { trapesDite = true; HUD.SousTitre("Les chausse-trapes font tomber ceux qui coupaient par l'herbe. Marthe tire : une épaule, puis une cuisse.", 4f); }
                    Fleche(c);
                }
                if (c.etat != EtatC.Marche) return;
            }
        }

        void Prendre(Cendre c, Emplacement e, float enfonce)
        {
            Arreter(c, EtatC.Pris, enfonce);
            if (e == null) return;
            e.captures++;
            e.pris.Add(c);
        }

        void LacherFilet(Emplacement e)
        {
            e.tombe = true;
            filetsTombes++;
            Vector3 sol = new Vector3(e.pos.x, Sol(e.pos) + .9f, e.pos.y);
            if (e.filet != null) chutes.Add((e.filet, e.filet.position, sol, 0f));
            Sons.JouerA("coup", e.racine.position, .7f, .6f);
            foreach (var c in cendres)
                if (c.etat == EtatC.Marche && e.captures < 3 && Vector2.Distance(P(c.h.transform), e.pos) < 3.6f) Prendre(c, e, .15f);
            HUD.SousTitre(e.nom.Contains("grève") ? "Pell coupe la corde : le filet lesté tombe sur les hommes de la barque." : "Joss coupe la corde : le filet lesté tombe sur ceux qui montent du quai.", 3.5f);
            // Un filet plein : l'un d'eux a une fiole à la ceinture
            if (e.captures >= 3 && !fioleUtilisee) { fioleUtilisee = true; PlusTard(1.3f, () => Fiole(e)); }
        }

        void Fiole(Emplacement e)
        {
            Sons.JouerA("coup", e.racine.position, 1f, .25f);
            Eclat(e.racine.position + Vector3.up, new Color(1f, .6f, .2f), 50);
            var libres = Liberer(e);
            if (pell != null && !pellSonne) { pellSonne = true; blesses++; }
            HUD.SousTitre("Un Cendré arrache une fiole de sa ceinture et la lance sur le filet. Explosion ! Le filet part en lambeaux" + (pellSonne ? ", Pell est projeté au sol, sonné." : "."), 5f);
            if (libres.Count > 0) PlusTard(2f, () => { if (libres[0].etat == EtatC.Marche) { Fleche(libres[0]); Arreter(libres[0], EtatC.Pris, .2f); } });
        }

        List<Cendre> Liberer(Emplacement e)
        {
            var libres = new List<Cendre>(e.pris);
            foreach (var c in libres) { c.etat = EtatC.Marche; c.enfonce = 0; c.attente = .8f; }
            e.pris.Clear(); e.captures = 0; e.detruit = true;
            if (e.filet != null) { Destroy(e.filet.gameObject); e.filet = null; }
            return libres;
        }

        void Cloches(Cendre c, Vector2 p)
        {
            if (!fuiteGardee || clochesFuite == null || Vector2.Distance(p, clochesFuite.pos) > 3.5f) return;
            Arreter(c, EtatC.Rattrape, .3f);
            Sons.JouerA("decouverte", clochesFuite.racine.position, 1f, 1.9f);
            Sons.JouerA("decouverte", clochesFuite.racine.position, 1f, 2.3f);
            HUD.SousTitre("Les fils à cloches du bois tintent : le douzième Cendré, qui filait vers les bois, est rattrapé.", 4f);
        }

        void Arriver(Cendre c)
        {
            if (c.etat == EtatC.Fuite) { c.etat = EtatC.Enfui; c.h.gameObject.SetActive(false); return; }
            c.etat = EtatC.Ligne;
            c.combat = 3.5f;
            if (!ligneDite) { ligneDite = true; HUD.SousTitre("Ils tombent sur le mur de la milice. Garr est au centre, Hadrien et Bram de chaque côté. « TENEZ ! »", 4f); Sons.Voix(.65f); }
        }

        void Combattre(Cendre c, float dt)
        {
            c.h.geste = Geste.Epee;
            if (garr != null) c.h.regarder = garr.transform.position;
            c.combat -= dt;
            if (c.combat > 0) return;
            Arreter(c, EtatC.Maitrise, .35f);
            echappes++;
            int francs = (alerte ? 1 : 0) + (Moral >= 2 ? 1 : 0);
            if (echappes > francs) blesses++;
            if (echappes == 2 && !garrBlesse)
            {
                garrBlesse = true;
                HUD.SousTitre("Un Cendré passe sous la garde de Garr et lui ouvre le flanc. Garr grogne, reste debout et l'assomme d'un coup de pommeau.\n« Ça t'en fait cinq d'argent de plus, patron ! »", 6f);
            }
            else if (echappes == 3)
            {
                HUD.SousTitre("Hadrien saisit un Cendré par le col et le jette dans la boue, en contrebas.", 4f);
                foreach (var e in emplacements)
                    if (e.type == TypePiege.Fosse && e.trace) { c.h.transform.position = new Vector3(e.pos.x, c.h.transform.position.y, e.pos.y); c.enfonce = .6f; break; }
            }
            if (echappes >= 6 && !lignePliee)
            {
                lignePliee = true; blesses += 2;
                Incendie();
                HUD.Message("LA LIGNE PLIE", "Trop d'hommes sont passés : la milice recule jusqu'aux premières maisons.\nUne torche vole, un toit prend feu. Le nombre finit par l'emporter, mais Varenne paie le prix.", 6f);
            }
        }

        void Melee()
        {
            Cendre adversaire = null;
            foreach (var c in cendres) if (c.etat == EtatC.Ligne) { adversaire = c; break; }
            foreach (var (h, g) in new[] { (garr, Geste.Epee), (hadrien, Geste.Marteau), (bram, Geste.Hache), (colin, Geste.Pioche) })
            {
                if (h == null) continue;
                bool seBat = adversaire != null && Vector2.Distance(P(h.transform), ligne) < 4f;
                h.geste = seBat ? g : Geste.Aucun;
                h.regarder = seBat ? adversaire.h.transform.position : (Vector3?)null;
            }
        }

        void Incendie()
        {
            Transform cible = null; float best = 1e9f;
            var village = GameObject.Find("Village");
            if (village != null)
                foreach (Transform t in village.transform)
                    if (t.name.StartsWith("Maison") && t.name != "MaisonCorvin")
                    {
                        float d = Vector2.Distance(P(t), ligne);
                        if (d < best) { best = d; cible = t; }
                    }
            Vector3 p = cible != null ? cible.position + Vector3.up * 5f : new Vector3(ligne.x + 8f, 6f, ligne.y + 4f);
            var ps = Particules("Incendie", p, new Color(1f, .5f, .15f), true);
            var m = ps.main; m.startSize = 1.6f; m.startLifetime = 2.2f; m.startSpeed = 1.5f;
            var em = ps.emission; em.rateOverTime = 30f;
            ps.Play();
        }

        // ---------- les flammes noires de Vesk ----------

        void FeuNoir()
        {
            Emplacement filet = null;
            foreach (var e in emplacements) if (e.type == TypePiege.Filet && e.tombe && !e.detruit && e.captures > 0) { filet = e; break; }
            Vector3 cible = filet != null ? filet.racine.position + Vector3.up : new Vector3(ligne.x, Sol(ligne) + 1.2f, ligne.y);
            HUD.SousTitre(filet != null ? "La main noire de Vesk se rallume : une boule de flammes noires file vers un filet !" : "La main noire de Vesk se rallume : une boule de flammes noires file vers la milice !", 3f);
            Feu(MainVesk, cible, 1.2f, () =>
            {
                Eclat(cible, new Color(1f, .4f, .1f), 40);
                Sons.JouerA("coup", cible, 1f, .3f);
                if (filet != null && !filet.detruit)
                {
                    Liberer(filet);
                    HUD.SousTitre("Le filet s'embrase : les hommes pris dessous se dégagent.", 3f);
                }
                else { blesses++; HUD.SousTitre("Un milicien est brûlé à l'épaule.", 3f); }
            });
        }

        void Feu(Vector3 de, Vector3 a, float duree, System.Action fin)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(b.GetComponent<Collider>());
            b.transform.localScale = Vector3.one * .55f;
            b.GetComponent<MeshRenderer>().sharedMaterial = Unlit(new Color(.12f, .03f, .06f));
            var coeur = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(coeur.GetComponent<Collider>());
            coeur.transform.SetParent(b.transform, false);
            coeur.transform.localScale = Vector3.one * .6f;
            coeur.GetComponent<MeshRenderer>().sharedMaterial = Unlit(new Color(1f, .35f, .08f));
            projectiles.Add(new Projectile { t = b.transform, a = de, b = a, duree = duree, arc = 1.5f, fin = fin });
            Sons.JouerA("sceauOn", de, .7f, .4f);
        }

        void Fleche(Cendre c)
        {
            Vector3 de = marthe != null ? marthe.transform.position + Vector3.up * 1.4f : new Vector3(31f, 4f, 16f);
            var f = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(f.GetComponent<Collider>());
            f.transform.localScale = new Vector3(.04f, .04f, .8f);
            f.GetComponent<MeshRenderer>().sharedMaterial = Lit(new Color(.45f, .32f, .2f));
            projectiles.Add(new Projectile { t = f.transform, a = de, b = c.h.transform.position + Vector3.up, duree = .55f, arc = .6f });
            if (marthe != null) { marthe.regarder = c.h.transform.position; marthe.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(c.h.transform.position - marthe.transform.position, Vector3.up)); }
        }

        void Projectiles(float dt)
        {
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                var p = projectiles[i];
                p.u += dt / p.duree;
                float u = Mathf.Clamp01(p.u);
                Vector3 pos = Vector3.Lerp(p.a, p.b, u) + Vector3.up * Mathf.Sin(u * Mathf.PI) * p.arc;
                Vector3 avant = p.t.position;
                p.t.position = pos;
                if ((pos - avant).sqrMagnitude > 1e-5f) p.t.rotation = Quaternion.LookRotation(pos - avant);
                if (p.u < 1f) continue;
                projectiles.RemoveAt(i);
                Destroy(p.t.gameObject);
                p.fin?.Invoke();
            }
        }

        void Chutes(float dt)
        {
            for (int i = chutes.Count - 1; i >= 0; i--)
            {
                var c = chutes[i];
                if (c.filet == null) { chutes.RemoveAt(i); continue; }
                c.u = Mathf.Min(1f, c.u + dt * 2.2f);
                c.filet.position = Vector3.Lerp(c.de, c.a, c.u * c.u);
                c.filet.localScale = Vector3.Lerp(new Vector3(.25f, 1f, .25f), Vector3.one, c.u);
                chutes[i] = c;
                if (c.u >= 1f) chutes.RemoveAt(i);
            }
        }

        // ---------- Vesk : le saut, le duel avec Ilse, le genou à terre ----------

        void DebutDuel()
        {
            phase = Phase.Duel; tPhase = 0; etapeDuel = 0; duelFin = 999f;
            vesk.transform.SetParent(transform, true);
            Vector2 aldric = P(j.transform);
            float dist = Vector2.Distance(quaiRacine, aldric);
            arretVesk = Vector2.Lerp(quaiRacine, aldric, dist > 6f ? 1f - 6f / dist : 0f);
            if (debarquementQuai) vesk.transform.position = new Vector3(quaiBout.x + .5f, QuaiY, 14f);
            else { var p = barge.TransformPoint(new Vector3(-2.1f, 0, 4.5f)); vesk.transform.position = new Vector3(p.x, -.95f, p.z); Eclat(new Vector3(p.x, 0, p.z), Color.white, 20); }
            vesk.geste = Geste.Aucun;
            HUD.SousTitre("Sur la barge, Vesk comprend qu'il a perdu sa source. Il dégaine une longue épée et saute à l'eau. Il nage avec une force terrifiante.", 5f);
        }

        void Duel(float dt)
        {
            Garder(dt);
            HUD.Jauge(Tient ? "VANNE FERMÉE — Vesk n'a que son épée" : "VANNE OUVERTE — reprends Vesk (R) !", Tient ? 1f : 0f);
            Vector2 pv = P(vesk.transform);
            if (etapeDuel == 0)
            {
                Vector2 but = SurQuai(pv) || !Nage(pv) ? arretVesk : quaiBout;
                vesk.AllerVers(but, Nage(pv) ? 1.8f : 2.6f);
                if (Vector2.Distance(pv, arretVesk) < .8f || tPhase > 22f)
                {
                    etapeDuel = 1;
                    vesk.geste = Geste.Epee;
                    vesk.regarder = j.transform.position;
                    HUD.SousTitre("Il prend pied sur la rive, à quelques pas de toi. Sa main noire fume : privée de mana, la malédiction remonte le long de son cou.", 4f);
                    PlusTard(1.6f, () =>
                    {
                        if (ilse != null) { Eclat(ilse.transform.position + Vector3.up, new Color(.05f, .05f, .08f), 30); ilse.gameObject.SetActive(false); }
                        PlusTard(.5f, () =>
                        {
                            var dos = vesk.transform.position - vesk.transform.forward * 1.5f;
                            dos.y = Monde.Hauteur(dos.x, dos.z);
                            Apparaitre(ilse, dos, vesk.transform.position - dos);
                            HUD.SousTitre("Ilse disparaît dans l'ombre d'un arbre… et réapparaît dans son dos.", 3.5f);
                            etapeDuel = 2; duelFin = tPhase + 12f;
                        });
                    });
                }
                return;
            }
            if (etapeDuel < 2 || ilse == null) { if (ilse == null && tPhase > 30f) Genou(); return; }

            // Le duel : lui plus fort, elle plus rapide
            angleDuel += dt * 1.3f;
            Vector3 c = vesk.transform.position;
            Vector2 orbite = new Vector2(c.x, c.z) + new Vector2(Mathf.Cos(angleDuel), Mathf.Sin(angleDuel)) * 1.7f;
            ilse.AllerVers(orbite, 4f);
            ilse.geste = Geste.Epee; ilse.regarder = c;
            vesk.regarder = ilse.transform.position;
            float restant = duelFin - tPhase;
            if (etapeDuel == 2 && restant < 9f) { etapeDuel = 3; ilseBlessures++; HUD.SousTitre("Le duel est rapide, violent. Il la touche à l'épaule. Elle lui entaille la cuisse.", 3.5f); }
            if (etapeDuel == 3 && restant < 5f) { etapeDuel = 4; HUD.SousTitre("Il la repousse d'un revers. Elle roule, se relève et repart.", 3f); }
            if (!Tient && sansSiphon > 3f)
            {
                sansSiphon = 0f; ilseBlessures++; duelFin += 3f;
                Feu(MainVesk, ilse.transform.position + Vector3.up * 1.2f, .5f, () => { Eclat(ilse.transform.position + Vector3.up, new Color(1f, .4f, .1f), 30); HUD.SousTitre("Le bras noir de Vesk crache une gerbe de flammes : Ilse est brûlée, elle recule. Tiens le Siphon !", 3f); });
            }
            if (restant <= 0f) Genou();
        }

        void Genou()
        {
            phase = Phase.Verdict;
            vesk.geste = Geste.Aucun; vesk.regarder = j.transform.position;
            vesk.AllerVers(P(vesk.transform), 1f);
            veskEnfonce = .45f;
            if (ilse != null)
            {
                ilse.geste = Geste.Aucun;
                var devant = vesk.transform.position + (j.transform.position - vesk.transform.position).normalized * 1.3f;
                ilse.AllerVers(new Vector2(devant.x, devant.z), 2f);
                ilse.regarder = vesk.transform.position;
            }
            HUD.Jauge(null, -1f);
            fluxVesk.enabled = false; puise = false;
            HUD.SousTitre("Ilse feinte, glisse dans une ombre une dernière fois, réapparaît au ras du sol et lui tranche le jarret.\nVesk tombe à genoux dans la boue. La pointe de l'épée d'Ilse se pose sous sa gorge.", 7f);
            PlusTard(4f, () => HUD.SousTitre("Le silence retombe sur la rive. Les Cendrés pris dans la boue et les filets lâchent leurs armes, un par un.", 4f));
            parleVesk = vesk.gameObject.AddComponent<Interlocuteur>();
            parleVesk.id = "vesk"; parleVesk.nom = "Vesk";
            Lieux.Declarer("vesk", vesk.transform);
            Progression.Mettre("vesk_a_genoux");
            PlusTard(8.5f, () => { if (!Dialogues.EnCours && phase == Phase.Verdict) Dialogues.Commencer(parleVesk); });
        }

        float veskEnfonce;

        // ---------- le verdict, le bilan, la fin de l'arc ----------

        public static bool ManaPour(float q) => instance != null && instance.j != null && instance.j.Mana >= q;

        public static void Conclure(bool epargne, bool scelle) { if (instance != null && instance.phase == Phase.Verdict) instance.Conclusion(epargne, scelle); }

        void Conclusion(bool epargne, bool scelle)
        {
            phase = Phase.Fin;
            Progression.Mettre(epargne ? "vesk_epargne" : "vesk_mort");
            if (scelle)
            {
                Progression.Mettre("vesk_scelle");
                j.FixerMana(Mathf.Max(0f, j.Mana - j.manaMax * .15f));
                if (matBrasVesk != null) matBrasVesk.SetColor("_BaseColor", new Color(.45f, .45f, .47f));
                Eclat(vesk.transform.position + Vector3.up * 1.2f, new Color(.4f, .6f, 1f), 30);
            }
            if (!epargne)
            {
                veskEnfonce = .7f;
                vesk.transform.rotation *= Quaternion.Euler(70f, 0, 0);
            }
            if (garrBlesse) Progression.Mettre("garr_blesse");
            Progression.Mettre("bataille_finie");
            Progression.AjouterEveil(scelle ? 8f : 5f);
            Progression.AjouterConfiance(2);
            parleVesk.nom = epargne ? "Vesk, prisonnier" : "Le corps de Vesk";

            int pris = 0, enfuis = 0;
            foreach (var c in cendres)
            {
                if (c.etat == EtatC.Pris || c.etat == EtatC.Maitrise || c.etat == EtatC.Rattrape) pris++;
                if (c.etat == EtatC.Enfui) enfuis++;
            }
            if (enfuis > 0) Progression.Mettre("cendre_enfui");
            EnCours = false;
            Mobilises.Clear();
            j.entrave = 1f;
            HUD.Jauge(null, -1f);
            HUD.Bilan(null);
            RenderSettings.fogStartDistance = brouillardDebut; RenderSettings.fogEndDistance = brouillardFin;
            if (chaine != null && chaine.matChaine != null) chaine.matChaine.SetColor("_BaseColor", new Color(.2f, .2f, .22f));
            fluxVesk.enabled = false;
            Sons.Jouer("decouverte", 1f, .8f);
            HUD.Message("✦ VICTOIRE AU GUÉ DE VARENNE ✦",
                $"Cendrés capturés : {pris} / 12" + (enfuis > 0 ? " — le douzième s'est enfui vers les bois de l'est" : "") + "\n"
                + $"Villageois blessés : {blesses} (aucun mort)" + (lignePliee ? " · une maison brûlée" : "") + "\n"
                + (garrBlesse ? "Garr : blessé au flanc, tient debout\n" : "Garr : indemne, et déçu de l'être\n")
                + $"Ilse : {(ilseBlessures >= 3 ? "brûlée, blessée à l'épaule, épuisée" : "brûlée à l'avant-bras, blessée à l'épaule")}\n"
                + (epargne ? (scelle ? "Vesk : prisonnier · lien SCELLÉ, son bras est de pierre\n" : "Vesk : prisonnier, enchaîné\n") : "Vesk : mort de la main d'Ilse\n")
                + $"Mana : {j.Mana:0} / {j.manaMax:0}\n"
                + (scelle ? "\nNOUVELLE FONCTION DU SIPHON : SCELLEMENT — couper pour toujours le lien d'un mage au mana.\n" : "\n")
                + "TITRE OBTENU : « Vainqueur du Gué »", 16f);

            // Le soleil perce la brume ; Lise sort de chez Marthe en courant, malgré l'interdiction
            if (lise != null)
            {
                foreach (var r in lise.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
                PlusTard(5f, () => { if (lise != null) lise.AllerVers(new Vector2(j.transform.position.x - 1f, j.transform.position.z - 1f), 4.5f); });
            }
            // Chacun rentre à ses affaires (le forgeron à sa forge…) ; Garr et Ilse restent au Gué
            PlusTard(12f, () =>
            {
                foreach (var kv in postesInitiaux)
                    if (kv.Key != null && kv.Key != garr && kv.Key != ilse && kv.Key != lise) kv.Key.AllerVers(new Vector2(kv.Value.x, kv.Value.z), 2.2f);
            });
            PlusTard(17f, () =>
            {
                Sons.Jouer("quete", 1f, .8f);
                HUD.Message("✦ FIN DE L'ARC 1 — LE PONT ET LE GUÉ ✦",
                    (epargne
                        ? "Dans le cellier, enchaîné, Vesk finira par parler. Un nom : Vaëlor, le Dieu des Chemins Brisés.\nEt une raison : le dieu a besoin d'un corps. Le tien."
                        : "Vesk a emporté ses secrets dans la boue du Gué. Mais Ilse, elle, est restée.\nEt quelque part, la Tour d'obsidienne a senti la colonne d'argent.")
                    + "\n\nVarenne a un pont, du pain, et un Bâtisseur.\nLa suite du chapitre 1 arrive bientôt. Merci d'avoir joué !", 16f);
            });
        }

        /// <summary>Le lendemain, on a emmené les prisonniers ; la barge reste plantée sur la chaîne.</summary>
        void Nettoyer()
        {
            if (nettoye) return;
            nettoye = true;
            foreach (var c in cendres) if (c.h != null) Destroy(c.h.gameObject);
            cendres.Clear();
            if (barque != null) Destroy(barque.gameObject);
            if (vesk != null) Destroy(vesk.gameObject);
        }

        // ---------- décor : flux du nœud, chaîne brûlante, hauteurs ----------

        void Decor(float dt)
        {
            if (vesk == null) return;
            if (matBrasVesk == null)
            {
                var bras = vesk.transform.Find("BrasD");
                if (bras != null && bras.childCount > 0)
                {
                    matBrasVesk = Unlit(new Color(.04f, .04f, .05f));
                    bras.GetChild(0).GetComponent<MeshRenderer>().sharedMaterial = matBrasVesk;
                }
            }
            else if (!Progression.A("vesk_scelle"))
                matBrasVesk.SetColor("_BaseColor", puise ? Color.Lerp(new Color(.25f, .02f, .02f), new Color(.9f, .2f, .05f), .5f + .5f * Mathf.Sin(Time.time * 9f)) : new Color(.04f, .04f, .05f));

            // Le flux du nœud jusqu'à la main de Vesk
            fluxVesk.enabled = puise && phase < Phase.Verdict;
            if (fluxVesk.enabled)
            {
                Vector3 a = new Vector3(Monde.PontX, -2.5f, 0), b = MainVesk;
                for (int i = 0; i < fluxVesk.positionCount; i++)
                {
                    float t = i / (fluxVesk.positionCount - 1f);
                    Vector3 p = Vector3.Lerp(a, b, t);
                    p.y = Mathf.Lerp(-2.5f, -1.2f, t) + (b.y + 1.2f) * Mathf.Pow(t, 6f);
                    fluxVesk.SetPosition(i, p + Random.insideUnitSphere * .06f);
                }
            }

            // La chaîne chauffe au rouge, l'eau fume autour
            if (chaine != null && chaine.matChaine != null)
            {
                float chaud = (phase == Phase.Arrivee ? Mathf.Clamp01(tPhase / 25f) : 1f) * (phase <= Phase.Rive ? 1f : .4f);
                chaine.matChaine.SetColor("_BaseColor", Color.Lerp(new Color(.2f, .2f, .22f), new Color(1f, .35f, .08f), chaud * (.8f + .2f * Mathf.Sin(Time.time * 3f))));
                fumee -= dt;
                if (fumee <= 0f && chaud > .3f)
                {
                    fumee = .35f;
                    Eclat(chaine.racine.position + new Vector3(Random.Range(-8f, 8f), .1f, 0), new Color(.92f, .94f, .97f), 6, .35f);
                }
            }

            // La brume se lève pendant le duel
            if (phase >= Phase.Duel)
            {
                RenderSettings.fogStartDistance = Mathf.MoveTowards(RenderSettings.fogStartDistance, brouillardDebut, dt * 4f);
                RenderSettings.fogEndDistance = Mathf.MoveTowards(RenderSettings.fogEndDistance, brouillardFin, dt * 60f);
            }
        }

        void LateUpdate()
        {
            if (vesk == null) return;
            foreach (var c in cendres)
            {
                if (c.h == null || c.etat == EtatC.Pont || c.etat == EtatC.Barque || c.h.transform.parent != transform) continue;
                Hauteur(c.h.transform, c.enfonce);
            }
            if (vesk.transform.parent == transform) Hauteur(vesk.transform, veskEnfonce);
        }

        void Hauteur(Transform t, float enfonce)
        {
            Vector2 p = P(t);
            float y = SurQuai(p) ? Mathf.Max(QuaiY, Monde.Hauteur(p.x, p.y)) : Nage(p) ? -.95f : Monde.Hauteur(p.x, p.y);
            var pos = t.position;
            pos.y = y - enfonce;
            t.position = pos;
        }

        // ---------- particules ----------

        ParticleSystem Particules(string nom, Vector3 p, Color c, bool boucle)
        {
            var go = new GameObject(nom);
            go.transform.SetParent(transform, false);
            go.transform.position = p;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main;
            m.loop = boucle; m.duration = boucle ? 5f : .3f; m.playOnAwake = false;
            m.startColor = c; m.startLifetime = .9f; m.startSpeed = 3f; m.startSize = .45f;
            m.gravityModifier = boucle ? -.15f : .3f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = .4f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            if (matParticules == null) matParticules = MateriauParticules();
            r.sharedMaterial = matParticules;
            return ps;
        }

        static Material MateriauParticules()
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetColor("_BaseColor", Color.white);
            return m;
        }

        void Eclat(Vector3 p, Color c, int nombre, float vitesse = 3f)
        {
            var ps = Particules("Eclat", p, c, false);
            var m = ps.main; m.startSpeed = vitesse;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)nombre) });
            ps.Play();
            Destroy(ps.gameObject, 2.5f);
        }
    }
}
