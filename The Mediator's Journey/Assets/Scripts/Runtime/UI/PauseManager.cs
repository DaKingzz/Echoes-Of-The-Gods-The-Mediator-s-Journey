using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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
        SoundManager.Instance.PlayClick();

        isPaused = !isPaused;

        if (pausePanel) pausePanel.SetActive(isPaused);
        if (pauseButton) pauseButton.SetActive(!isPaused);

        Time.timeScale = isPaused ? 0f : 1f;

        // switch music when paused
        if (isPaused)
            SoundManager.Instance.PlayMenuMusic();
        else
            SoundManager.Instance.PlayGameMusic();
    }

    public void Resume()
    {
        SoundManager.Instance.PlayClick();
        if (!isPaused) return;
        TogglePause();
    }

    public void QuitToMenu()
    {
        SoundManager.Instance.PlayClick();
        Time.timeScale = 1f;
        //GameManager.Instance.LoadMainMenu();
        SceneManager.LoadScene("MainMenu");
    }
}
