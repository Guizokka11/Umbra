using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Estado do mundo que sobrevive à troca de cenas e ao fechar o jogo:
/// itens que a Luma carrega ("item_fusivel") e coisas que já aconteceram
/// ("corredor2_luz_ligada", "grade_aberta"). Salvo em PlayerPrefs.
/// Convenção: itens começam com "item_".
/// </summary>
public static class GameFlags
{
    const string Key = "umbra_flags";
    static HashSet<string> flags;

    /// <summary>(flag, ligada?) sempre que algo muda.</summary>
    public static event Action<string, bool> OnChanged;

    static HashSet<string> Flags
    {
        get
        {
            if (flags == null)
            {
                flags = new HashSet<string>();
                foreach (var s in PlayerPrefs.GetString(Key, "").Split('|'))
                    if (!string.IsNullOrEmpty(s)) flags.Add(s);
            }
            return flags;
        }
    }

    public static bool Has(string flag) => !string.IsNullOrEmpty(flag) && Flags.Contains(flag);

    public static void Set(string flag, bool on = true)
    {
        if (string.IsNullOrEmpty(flag)) return;
        bool changed = on ? Flags.Add(flag) : Flags.Remove(flag);
        if (!changed) return;
        Save();
        OnChanged?.Invoke(flag, on);
    }

    public static void Clear()
    {
        Flags.Clear();
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }

    static void Save()
    {
        PlayerPrefs.SetString(Key, string.Join("|", Flags));
        PlayerPrefs.Save();
    }

    /// <summary>Nome bonito de um item para mensagens ("item_chave_grade" → "chave grade").</summary>
    public static string Pretty(string flag)
        => string.IsNullOrEmpty(flag) ? "" : flag.Replace("item_", "").Replace('_', ' ');
}
