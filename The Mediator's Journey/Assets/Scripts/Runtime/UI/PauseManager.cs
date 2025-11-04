using UnityEngine;

public class PauseManager : MonoBehaviour
{
    [SerializeField] private GameObject pauseUI;

    public void Pause()
    {
        if (pauseUI) pauseUI.SetActive(true);
        Time.timeScale = 0f;
    }
    public void Resume()
    {
        if (pauseUI) pauseUI.SetActive(false);
        Time.timeScale = 1f;
    }
    public void GoToMenu()
    {
        Time.timeScale = 1f;
        GameManager.Instance.LoadMainMenu();
    }
}
