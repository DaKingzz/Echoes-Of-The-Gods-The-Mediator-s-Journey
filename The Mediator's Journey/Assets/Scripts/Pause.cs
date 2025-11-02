using UnityEngine;

public class PauseMenu : MonoBehaviour
{
    public GameObject pausePanel;    
    public GameObject pauseButton;   

    private bool isPaused = false;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))  
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        isPaused = !isPaused;
        pausePanel.SetActive(isPaused);
        pauseButton.SetActive(!isPaused);
    }

    public void Resume()
    {
        TogglePause();
    }

    public void QuitToMenu()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }
}
