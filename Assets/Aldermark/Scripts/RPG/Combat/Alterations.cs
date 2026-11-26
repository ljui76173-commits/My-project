using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// Les altérations d'un combattant (monstre ou héros) : brûlure (des dégâts toutes les demi-secondes),
    /// gel (ralenti de moitié ; trois gels en 4 secondes : gelé net, immobile), et qui l'a causé
    /// (pour l'expérience si la brûlure achève la cible).
    /// </summary>
    public class Alterations
    {
        public float Brulure { get; private set; }
        public float Gel { get; private set; }
        public float Gele { get; private set; }
        public ICible Auteur { get; private set; }

        double dps, reserve;
        float tick, finCumuls;
        int cumulsGel;

        /// <summary>1 : normal ; 0,5 : ralenti par le gel ; 0 : gelé net.</summary>
        public float Lenteur => Gele > 0f ? 0f : Gel > 0f ? .5f : 1f;
        public bool Actif => Brulure > 0f || Gel > 0f || Gele > 0f;

        /// <summary>Applique un effet. Renvoie vrai si la cible vient d'être gelée net (troisième gel).</summary>
        public bool Appliquer(Effet e, double attaque, float duree, ICible auteur)
        {
            switch (e)
            {
                case Effet.Brulure:
                    Brulure = Mathf.Max(Brulure, duree);
                    dps = System.Math.Max(dps, attaque * .15);   // la plus forte brûlure l'emporte
                    Auteur = auteur;
                    return false;
                case Effet.Gel:
                    Gel = Mathf.Max(Gel, duree);
                    Brulure = 0f;   // le froid éteint le feu
                    if (Gele > 0f) return false;   // déjà gelé : pas de cumul (sinon on enchaînerait les gels)
                    if (Time.time > finCumuls) cumulsGel = 0;
                    cumulsGel++;
                    finCumuls = Time.time + 4f;
                    if (cumulsGel >= 3)
                    {
                        cumulsGel = 0;
                        Gele = 1.6f;
                        return true;
                    }
                    return false;
            }
            return false;
        }

        /// <summary>Le feu s'éteint (une roulade, de l'eau, le gel).</summary>
        public void Eteindre() { Brulure = 0f; reserve = 0; dps = 0; }

        public void Effacer() { Eteindre(); Gel = 0f; Gele = 0f; cumulsGel = 0; }

        /// <summary>Fait passer le temps ; renvoie les dégâts de brûlure à infliger maintenant (par paquets de 0,5 s).</summary>
        public double Avancer(float dt)
        {
            if (Gel > 0f) Gel -= dt;
            if (Gele > 0f) Gele -= dt;
            if (Brulure <= 0f) { reserve = 0; dps = 0; return 0; }
            Brulure -= dt;
            reserve += dps * dt;
            tick -= dt;
            if (tick > 0f && Brulure > 0f) return 0;
            tick = .5f;
            double r = reserve;
            reserve = 0;
            return r;
        }
    }
}
