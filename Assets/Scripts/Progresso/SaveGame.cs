using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Salvamento simples: última cena + ponto de entrada. As pistas (ClueJournal)
/// e os itens/eventos (GameFlags) já se salvam sozinhos.
/// </summary>
public static class SaveGame
{
    const string SceneKey = "umbra_scene";
    const string SpawnKey = "umbra_spawn";

    public const string MenuScene  = "00_Menu";
    public const string FirstScene = "00_Pesadelo";

    /// <summary>Ponto de entrada pedido pela porta/saída que carregou a cena atual.</summary>
    public static string PendingSpawn;

    public static bool HasSave => PlayerPrefs.HasKey(SceneKey) && !string.IsNullOrEmpty(PlayerPrefs.GetString(SceneKey));

    public static void Record(string scene, string spawnId)
    {
        if (scene == MenuScene) return;
        PlayerPrefs.SetString(SceneKey, scene);
        PlayerPrefs.SetString(SpawnKey, spawnId ?? "");
        PlayerPrefs.Save();
    }

    public static void NewGame()
    {
        GameFlags.Clear();
        ClueJournal.Clear();
        PlayerPrefs.DeleteKey(SceneKey);
        PlayerPrefs.DeleteKey(SpawnKey);
        PlayerPrefs.Save();
        PendingSpawn = null;
        Time.timeScale = 1f;
        SceneManager.LoadScene(Application.CanStreamedLevelBeLoaded(FirstScene) ? FirstScene : "01_Dormitorio1");
    }

    public static void Continue()
    {
        if (!HasSave) { NewGame(); return; }
        PendingSpawn = PlayerPrefs.GetString(SpawnKey, "");
        Time.timeScale = 1f;
        SceneManager.LoadScene(PlayerPrefs.GetString(SceneKey));
    }

    public static void ToMenu()
    {
        Time.timeScale = 1f;
        if (Application.CanStreamedLevelBeLoaded(MenuScene)) SceneManager.LoadScene(MenuScene);
    }
}
