using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Aldermark
{
    /// <summary>
    /// La sauvegarde du monde entier (pas seulement du héros) : l'horloge, le registre,
    /// et l'état de chaque chose sauvegardable, dans un fichier JSON lisible.
    /// </summary>
    public static class Sauvegarde
    {
        [Serializable] class Bloc { public string id; public string json; }

        [Serializable]
        class Fichier
        {
            public int version = 1;
            public string date;
            public double temps;
            public long prochainId;
            public List<Bloc> blocs = new List<Bloc>();
        }

        public static string Dossier => Path.Combine(Application.persistentDataPath, "Sauvegardes");
        public static string Chemin => Path.Combine(Dossier, "partie.json");
        public static bool Existe => File.Exists(Chemin);
        public static string DerniereErreur { get; private set; }
        public static string DateDerniere { get; private set; }

        public static bool Sauver()
        {
            try
            {
                var f = new Fichier { date = DateTime.Now.ToString("yyyy-MM-dd HH:mm"), temps = Horloge.Temps, prochainId = Registre.ProchainId };
                foreach (var s in Registre.Sauvegardables)
                    if (s != null) f.blocs.Add(new Bloc { id = s.IdSauvegarde, json = s.Ecrire() });
                Directory.CreateDirectory(Dossier);
                string tmp = Chemin + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(f, true));
                if (File.Exists(Chemin))
                {
                    File.Copy(Chemin, Chemin + ".bak", true);   // la précédente, au cas où
                    File.Delete(Chemin);
                }
                File.Move(tmp, Chemin);
                DateDerniere = f.date;
                return true;
            }
            catch (Exception e)
            {
                DerniereErreur = e.Message;
                Debug.LogWarning("Aldermark : sauvegarde impossible : " + e.Message);
                return false;
            }
        }

        public static bool Charger()
        {
            try
            {
                if (!Existe) return false;
                var f = JsonUtility.FromJson<Fichier>(File.ReadAllText(Chemin));
                if (f == null) return false;
                Horloge.Fixer(f.temps);
                Registre.ProchainId = Math.Max(Registre.ProchainId, f.prochainId);
                var parId = new Dictionary<string, ISauvegardable>();
                foreach (var s in Registre.Sauvegardables) if (s != null) parId[s.IdSauvegarde] = s;
                foreach (var b in f.blocs)
                    if (b != null && parId.TryGetValue(b.id, out var s))
                    {
                        try { s.Lire(b.json); }
                        catch (Exception e) { Debug.LogWarning($"Aldermark : bloc « {b.id} » illisible : {e.Message}"); }
                    }
                DateDerniere = f.date;
                return true;
            }
            catch (Exception e)
            {
                DerniereErreur = e.Message;
                Debug.LogWarning("Aldermark : chargement impossible : " + e.Message);
                return false;
            }
        }

        public static void Effacer()
        {
            foreach (var c in new[] { Chemin, Chemin + ".bak", Chemin + ".tmp" })
                if (File.Exists(c)) File.Delete(c);
        }
    }
}
