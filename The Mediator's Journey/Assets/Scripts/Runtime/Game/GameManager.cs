using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Scene Names")]
    [SerializeField] private string mainMenuScene = "MainMenu";
    [SerializeField] private string startWorldScene = "Game";

    [Header("Fade Away")]
    [SerializeField] private CanvasGroup fadeCanvas;
    [SerializeField] private float fadeDuration = 0.25f;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    IEnumerator Fade(float target, float duration)
    {
        if (!fadeCanvas) yield break;
        fadeCanvas.blocksRaycasts = true;
        float start = fadeCanvas.alpha;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            fadeCanvas.alpha = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }
        fadeCanvas.alpha = target;
        fadeCanvas.blocksRaycasts = target > 0.01f;
    }

    IEnumerator LoadSingle(string sceneName)
    {
        yield return Fade(1f, fadeDuration);
        var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        while (!op.isDone) yield return null;
        yield return Fade(0f, fadeDuration);
    }

    // Buttons will call these:
    public void LoadMainMenu() => StartCoroutine(LoadSingle(mainMenuScene));
    public void StartNewGame() => StartCoroutine(LoadSingle(startWorldScene));
    public void LoadScene(string name) => StartCoroutine(LoadSingle(name));

    public void Quit()
    {

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}