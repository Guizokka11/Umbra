using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registro das pistas encontradas. Estático, sobrevive à troca de cenas.
/// Salva os IDs em PlayerPrefs para manter entre sessões.
/// </summary>
public static class ClueJournal
{
    const string Key = "umbra_clues";
    static HashSet<string> found;

    public static event Action<ClueData> OnClueFound;

    static HashSet<string> Found
    {
        get
        {
            if (found == null)
            {
                found = new HashSet<string>();
                string raw = PlayerPrefs.GetString(Key, "");
                foreach (var s in raw.Split('|')) if (!string.IsNullOrEmpty(s)) found.Add(s);
            }
            return found;
        }
    }

    public static bool Has(string id) => Found.Contains(id);
    public static int Count => Found.Count;

    public static void Add(ClueData clue)
    {
        if (clue == null || !Found.Add(clue.id)) return;
        PlayerPrefs.SetString(Key, string.Join("|", Found));
        PlayerPrefs.Save();
        OnClueFound?.Invoke(clue);
    }

    public static void Clear()
    {
        Found.Clear();
        PlayerPrefs.DeleteKey(Key);
    }
}
