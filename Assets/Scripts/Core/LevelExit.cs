using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Trigger no fim de uma área: fade e carrega a próxima cena.
/// A cena precisa estar em File > Build Profiles > Scene List.
/// </summary>
[RequireComponent(typeof(Collider))]
public class LevelExit : MonoBehaviour
{
    public string nextScene;
    [Tooltip("SpawnPoint (id) onde a Luma aparece na próxima cena. Vazio = posição inicial da cena.")]
    public string spawnId;
    public float fadeTime = 1f;

    bool loading;

    void Reset() { GetComponent<Collider>().isTrigger = true; }

    void OnTriggerEnter(Collider other)
    {
        if (!loadOnEnter || loading || !other.CompareTag("Player") || string.IsNullOrEmpty(nextScene)) return;
        StartCoroutine(Load());
    }

    /// <summary>Carrega a próxima cena agora (para ligar em eventos: fim do pesadelo, alavanca do monta-cargas).</summary>
    public void LoadNow()
    {
        if (!loading && !string.IsNullOrEmpty(nextScene)) StartCoroutine(Load());
    }

    [Tooltip("Desligue para usar só por evento (LoadNow), sem trocar de cena ao encostar.")]
    public bool loadOnEnter = true;

    IEnumerator Load()
    {
        loading = true;
        var st = PlayerState.Instance;
        if (st != null) st.Movement.canMove = false;
        if (ScreenFader.Instance != null) yield return ScreenFader.Instance.FadeOut(fadeTime);
        SaveGame.PendingSpawn = spawnId;
        SceneManager.LoadScene(nextScene);
    }
}
