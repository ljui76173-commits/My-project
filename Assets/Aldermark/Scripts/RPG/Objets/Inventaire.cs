using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Aldermark
{
    /// <summary>
    /// Le sac du héros : ses objets, sa bourse, l'arme équipée (dégainée ou rangée), le poids porté.
    /// Au-delà de la charge maximale (20 kg + 2 par point de FOR), le héros ralentit.
    /// E : ramasser un objet au sol, ouvrir un coffre. Les pièces se ramassent seules.
    /// </summary>
    [RequireComponent(typeof(StatsHeros))]
    public class Inventaire : MonoBehaviour, ISauvegardable
    {
        public static Inventaire Instance { get; private set; }

        public readonly List<Pile> sac = new List<Pile>();
        public int Argent { get; private set; }
        public string ArmeId { get; private set; }
        public bool Degainee { get; private set; }

        public DefObjet Arme => Objets.Def(ArmeId);
        /// <summary>L'arme en main (null : rangée, ou aucune : mains nues).</summary>
        public DefObjet ArmeEnMain => Degainee ? Arme : null;
        public float Poids
        {
            get { float p = 0; foreach (var x in sac) { var d = Objets.Def(x.id); if (d != null) p += d.poids * x.n; } return p; }
        }
        public double ChargeMax => stats != null ? stats.ChargeMax : 30;
        public bool Surcharge => Poids > ChargeMax;

        public string IdSauvegarde => "inventaire";

        StatsHeros stats;
        JoueurAldric joueur;
        bool surchargeAnnoncee;

        void Awake()
        {
            Instance = this;
            stats = GetComponent<StatsHeros>();
            joueur = GetComponent<JoueurAldric>();
            Registre.InscrireSauvegarde(this);
        }

        void OnDestroy()
        {
            Registre.RetirerSauvegarde(this);
            if (Instance == this) Instance = null;
        }

        // ---------- contenu ----------

        public int Combien(string id)
        {
            int n = 0;
            foreach (var p in sac) if (p.id == id) n += p.n;
            return n;
        }

        /// <summary>Met des objets dans le sac (les pièces vont dans la bourse).</summary>
        public void Ajouter(string id, int n, bool annoncer = true)
        {
            var d = Objets.Def(id);
            if (d == null || n <= 0) return;
            if (d.categorie == Categorie.Monnaie)
            {
                Argent += n;
                if (annoncer) HUD.Toast($"+{n} pièce{(n > 1 ? "s" : "")}");
                Sons.Jouer("clic", .45f, 2f);
                return;
            }
            int reste = n;
            foreach (var p in sac)
                if (p.id == id && p.n < d.pileMax) { int k = Mathf.Min(reste, d.pileMax - p.n); p.n += k; reste -= k; if (reste <= 0) break; }
            while (reste > 0) { int k = Mathf.Min(reste, d.pileMax); sac.Add(new Pile(id, k)); reste -= k; }
            if (annoncer) HUD.Toast(n > 1 ? $"{d.nom} ×{n}" : d.nom);
            Sons.Jouer("clic", .5f, 1.3f);
        }

        public bool Retirer(string id, int n)
        {
            if (Combien(id) < n) return false;
            for (int i = sac.Count - 1; i >= 0 && n > 0; i--)
            {
                if (sac[i].id != id) continue;
                int k = Mathf.Min(n, sac[i].n);
                sac[i].n -= k; n -= k;
                if (sac[i].n <= 0) sac.RemoveAt(i);
            }
            if (ArmeId != null && Combien(ArmeId) == 0) { ArmeId = null; Degainee = false; }
            return true;
        }

        public void RetirerArgent(int n) => Argent = Mathf.Max(0, Argent - n);

        // ---------- actions ----------

        /// <summary>Équipe une arme du sac (et la met en main) ; sur l'arme déjà équipée, la retire.</summary>
        public void Equiper(string id)
        {
            var d = Objets.Def(id);
            if (d == null || d.categorie != Categorie.Arme || Combien(id) == 0) return;
            if (ArmeId == id) { ArmeId = null; Degainee = false; HUD.Toast($"{d.nom} retirée : mains nues"); Sons.Jouer("clic", .5f, .9f); return; }
            ArmeId = id;
            Degainee = true;
            HUD.Toast($"{d.nom} équipée · H pour la ranger");
            Sons.Jouer("lame", .5f, 1.3f);
        }

        /// <summary>Range l'arme ou la dégaine (H).</summary>
        public bool BasculerDegaine()
        {
            if (Arme == null) return false;
            Degainee = !Degainee;
            Sons.Jouer(Degainee ? "lame" : "clic", .5f, Degainee ? 1.4f : .8f);
            return true;
        }

        /// <summary>Utilise un objet : manger rend de la vie ; une arme s'équipe.</summary>
        public void Utiliser(string id)
        {
            var d = Objets.Def(id);
            if (d == null || Combien(id) == 0) return;
            switch (d.categorie)
            {
                case Categorie.Arme: Equiper(id); break;
                case Categorie.Nourriture:
                    Retirer(id, 1);
                    stats.Rendre(stats.VieMax * d.soin);
                    HUD.Toast($"{d.nom} mangé · +{Nombres.Court(stats.VieMax * d.soin)} vie");
                    Sons.Jouer("pasHerbe", .5f, 1.6f);
                    break;
                default: HUD.Toast($"{d.nom} : rien à en faire pour l'instant (artisanat à venir)"); break;
            }
        }

        /// <summary>Jette des objets devant soi.</summary>
        public void Jeter(string id, int n)
        {
            if (!Retirer(id, n)) return;
            Vector3 avant = Vector3.ProjectOnPlane(joueur.cam.transform.forward, Vector3.up).normalized;
            ObjetAuSol.Creer(id, n, transform.position + Vector3.up * 1.2f + avant * .5f, avant * 2.2f);
            Sons.Jouer("pasHerbe", .4f, 1.2f);
        }

        // ---------- ramasser, ouvrir ----------

        void Update()
        {
            if (!MondeRPG.Actif) return;
            if (Surcharge && !surchargeAnnoncee) { surchargeAnnoncee = true; HUD.Toast("Surchargé : tu avances lentement"); }
            if (!Surcharge) surchargeAnnoncee = false;
            if (HUD.MenuOuvert || Dialogues.EnCours || joueur == null || joueur.cam == null) return;

            // Les pièces viennent seules
            for (int i = ObjetAuSol.Tous.Count - 1; i >= 0; i--)
            {
                var o = ObjetAuSol.Tous[i];
                if (o.id != Objets.Pieces) continue;
                Vector3 d = o.transform.position - transform.position;
                if (d.y > -1f && d.y < 2f && new Vector2(d.x, d.z).magnitude < 1.6f) { Ajouter(o.id, o.quantite); Destroy(o.gameObject); }
            }

            // L'objet ou le coffre le plus proche devant soi
            Vector3 oeil = joueur.cam.transform.position, regard = Vector3.ProjectOnPlane(joueur.cam.transform.forward, Vector3.up).normalized;
            ObjetAuSol objet = null;
            Coffre coffre = null;
            float best = float.MaxValue;
            foreach (var o in ObjetAuSol.Tous)
            {
                float s = Score(o.transform.position, 2.6f, regard);
                if (s < best) { best = s; objet = o; }
            }
            foreach (var c in Coffre.Tous)
            {
                float s = Score(c.transform.position, 2.4f, regard);
                if (s < best) { best = s; coffre = c; objet = null; }
            }
            if (objet == null && coffre == null) return;
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool e = (kb != null && kb.eKey.wasPressedThisFrame) || (pad != null && pad.buttonNorth.wasPressedThisFrame);
            if (coffre != null)
            {
                HUD.InviteForte($"[E]  Ouvrir : {coffre.nom}");
                if (e) { coffre.Ouvrir(); HUD.OuvrirCoffre(); }
                return;
            }
            var def = objet.Def;
            HUD.InviteForte($"[E]  Ramasser : {def.nom}" + (objet.quantite > 1 ? $" ×{objet.quantite}" : "") + $"   ({def.poids * objet.quantite:0.#} kg)");
            if (e)
            {
                Ajouter(objet.id, objet.quantite);
                if (def.categorie == Categorie.Arme && Arme == null) Equiper(objet.id);
                Destroy(objet.gameObject);
            }
        }

        /// <summary>Plus c'est proche et en face, plus le score est bas ; MaxValue : hors de portée.</summary>
        float Score(Vector3 p, float portee, Vector3 regard)
        {
            Vector3 d = p - transform.position;
            if (d.y < -1.2f || d.y > 2.2f) return float.MaxValue;
            d.y = 0;
            float dist = d.magnitude;
            if (dist > portee) return float.MaxValue;
            float angle = dist < .9f ? 0f : Vector3.Angle(regard, d);
            if (angle > 65f) return float.MaxValue;
            return dist + angle * .02f;
        }

        // ---------- sauvegarde ----------

        [System.Serializable]
        class Etat
        {
            public List<Pile> sac;
            public int argent;
            public string arme;
            public bool degainee;
        }

        public string Ecrire() => JsonUtility.ToJson(new Etat { sac = sac, argent = Argent, arme = ArmeId ?? "", degainee = Degainee });

        public void Lire(string json)
        {
            var e = JsonUtility.FromJson<Etat>(json);
            if (e == null) return;
            sac.Clear();
            if (e.sac != null) sac.AddRange(e.sac);
            Argent = e.argent;
            ArmeId = string.IsNullOrEmpty(e.arme) || Combien(e.arme) == 0 ? null : e.arme;
            Degainee = ArmeId != null && e.degainee;
        }
    }
}
