using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Backend LOCAL: guarda la tabla como JSON en Application.persistentDataPath, la ruta que Unity
// garantiza escribible en cada plataforma (junto al .exe en Windows es un directorio de usuario,
// no la carpeta de instalación). Legible y respaldable a mano.
//
// JsonUtility no serializa una List<> en la raíz, así que se envuelve en una clase 'Wrapper'.
// Robusto por diseño: cualquier fallo de IO o JSON corrupto se traga y devuelve lista vacía; el
// llamador (la fachada Leaderboard) nunca ve una excepción ni un null.
public class JsonLeaderboardStore : ILeaderboardStore
{
    // Envoltorio serializable: JsonUtility necesita un objeto raíz con un campo lista.
    [Serializable]
    private class Wrapper
    {
        public List<LeaderboardEntry> entries = new List<LeaderboardEntry>();
    }

    private const string FileName = "leaderboard.json";

    private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    public List<LeaderboardEntry> Load()
    {
        try
        {
            string path = FilePath;
            if (!File.Exists(path)) return new List<LeaderboardEntry>();

            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return new List<LeaderboardEntry>();

            Wrapper wrapper = JsonUtility.FromJson<Wrapper>(json);
            if (wrapper?.entries == null) return new List<LeaderboardEntry>();

            // Filtra entradas nulas/corruptas por seguridad (JSON manipulado a mano).
            wrapper.entries.RemoveAll(e => e == null);
            return wrapper.entries;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Leaderboard] No se pudo leer '{FilePath}': {e.Message}. Se usa una tabla vacía.");
            return new List<LeaderboardEntry>();
        }
    }

    public void Save(List<LeaderboardEntry> entries)
    {
        try
        {
            var wrapper = new Wrapper { entries = entries ?? new List<LeaderboardEntry>() };
            string json = JsonUtility.ToJson(wrapper, prettyPrint: true);
            File.WriteAllText(FilePath, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Leaderboard] No se pudo escribir '{FilePath}': {e.Message}");
        }
    }
}
