using System;
using UnityEngine;

namespace Aldermark
{
    public enum Camp { Heros, Allie, Neutre, Hostile }

    /// <summary>Tout ce qui peut recevoir un coup : le héros, un monstre, un PNJ, une quintaine.</summary>
    public interface ICible
    {
        Camp Camp { get; }
        bool Vivant { get; }
        Vector3 Centre { get; }
        string NomAffiche { get; }
        int NiveauAffiche { get; }
        double VieActuelle { get; }
        double VieMaximale { get; }
        Transform Corps { get; }
        void Recevoir(Coup coup);
    }

    /// <summary>Un coup porté : son attaque brute (la cible y applique sa défense), d'où il vient, et comment il frappe.</summary>
    public struct Coup
    {
        public double degats;
        public Vector3 direction;
        public float poussee;
        public bool lourd, critique, magique;
        public ICible auteur;
        public string nom;
        /// <summary>L'élément du coup (aucun pour une arme) : la cible peut y être faible ou résistante.</summary>
        public Element element;
        public Effet effet;
        public float dureeEffet;
        /// <summary>Le sort qui a porté ce coup (son identifiant), s'il y en a un : l'encaisser aide à le comprendre.</summary>
        public string sort;
    }

    public static class Degats
    {
        /// <summary>dégâts = ATQ × ATQ / (ATQ + DEF) : un ennemi de ton niveau reste un vrai combat,
        /// un ennemi bien plus faible tombe en un coup, et les chiffres peuvent exploser sans casser l'équilibre.</summary>
        public static double Calculer(double attaque, double defense)
        {
            if (attaque <= 0) return 0;
            return attaque * attaque / (attaque + Math.Max(0, defense));
        }
    }

    /// <summary>Une cible encore là : une interface ne dit pas si l'objet Unity derrière a été détruit.</summary>
    public static class Cibles
    {
        public static bool Existe(ICible c) => c is UnityEngine.Object o ? o != null : c != null;
    }
}
