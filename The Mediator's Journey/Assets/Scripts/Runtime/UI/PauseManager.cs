using UnityEngine;
using UnityEngine.InputSystem;

public class PauseMenu : MonoBehaviour
{
    [Header("UI")]
    public GameObject pausePanel;
    public GameObject pauseButton; 

    bool isPaused;

    void Start()
    {
        if (pausePanel) pausePanel.SetActive(false);
        if (pauseButton) pauseButton.SetActive(true);
        Time.timeScale = 1f;
        isPaused = false;
    }

    void Update()
    {
        // Letter P toggles pause/unpause
        if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        isPaused = !isPaused;

        if (pausePanel) pausePanel.SetActive(isPaused);
        if (pauseButton) pauseButton.SetActive(!isPaused);

        Time.timeScale = isPaused ? 0f : 1f;
    }

    public void Resume()
    {
        if (!isPaused) return;
        TogglePause();
    }

    public void QuitToMenu()
    {
        Time.timeScale = 1f;
        GameManager.Instance.LoadMainMenu();
    }
}
