using System;
using System.Globalization;

namespace Aldermark
{
    /// <summary>Les grands nombres à la française : 9 999, 12,3 k, 4,5 M, 6,7 Md, puis Bn, Bd, Tn…</summary>
    public static class Nombres
    {
        static readonly string[] Suffixes = { "", " k", " M", " Md", " Bn", " Bd", " Tn", " Td", " Qn" };

        public static string Court(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "∞";
            double a = Math.Abs(v);
            if (a < 100) return v.ToString("0.#", CultureInfo.InvariantCulture).Replace('.', ',');
            if (a < 10000) return Entier(v);
            int rang = (int)Math.Floor(Math.Log10(a) / 3);
            if (rang >= Suffixes.Length) return v.ToString("0.0e0", CultureInfo.InvariantCulture).Replace('.', ',');
            double m = v / Math.Pow(1000, rang);
            string f = Math.Abs(m) < 100 ? "0.#" : "0";
            return m.ToString(f, CultureInfo.InvariantCulture).Replace('.', ',') + Suffixes[rang];
        }

        /// <summary>Un entier avec des espaces entre les milliers (12 345).</summary>
        public static string Entier(double v) => Math.Floor(v).ToString("#,0", CultureInfo.InvariantCulture).Replace(',', ' ');

        /// <summary>Un multiplicateur : ×1,09.</summary>
        public static string Facteur(double v) => "×" + v.ToString(v < 10 ? "0.00" : "0.#", CultureInfo.InvariantCulture).Replace('.', ',');
    }
}
