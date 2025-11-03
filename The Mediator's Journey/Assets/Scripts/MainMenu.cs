using UnityEngine;

public class MainMenu : MonoBehaviour
{
    public GameObject mainMenuPanel;
    public GameObject settingsPanel;
    public GameObject creditsPanel;

    public void Play()
    {
        //UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene");
    }

    public void OpenSettings()
    {
        mainMenuPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }
    public void OpenCredits()
    {
        mainMenuPanel.SetActive(false);
        creditsPanel.SetActive(true);
    }

    public void BackToMenu()
    {
        settingsPanel.SetActive(false);
        creditsPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }

    public void Quit()
    {
        Application.Quit();
    }
}
