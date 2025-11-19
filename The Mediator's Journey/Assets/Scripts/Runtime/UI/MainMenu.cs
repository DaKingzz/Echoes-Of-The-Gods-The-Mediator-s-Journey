using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

public class MainMenu : MonoBehaviour
{
    [Header("Panels")]
    public GameObject mainMenuPanel;
    public GameObject settingsPanel;
    public GameObject creditsPanel;

    public void Play()
    {
        SoundManager.Instance.PlayClick();
        //GameManager.Instance.StartNewGame();
        SceneManager.LoadScene("Game");
    }

    public void OpenSettings()
    {
        SoundManager.Instance.PlayClick();

        if (mainMenuPanel) mainMenuPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(true);

        VideoPlayer vp = settingsPanel.GetComponent<VideoPlayer>();
        if (vp)
        {
            vp.Play(); 
        }
    }

    public void OpenCredits()
    {
        SoundManager.Instance.PlayClick();

        if (mainMenuPanel) mainMenuPanel.SetActive(false);
        if (creditsPanel) creditsPanel.SetActive(true);
    }

    public void BackToMenu()
    {
        SoundManager.Instance.PlayClick();

        if (settingsPanel) settingsPanel.SetActive(false);
        if (creditsPanel) creditsPanel.SetActive(false);
        if (mainMenuPanel) mainMenuPanel.SetActive(true);
    }

    public void Quit()
    {
        SoundManager.Instance.PlayClick();
        GameManager.Instance.Quit();
    }
}
