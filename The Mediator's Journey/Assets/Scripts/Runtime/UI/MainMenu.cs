using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

public class MainMenu : MonoBehaviour
{
    [Header("Panels")]
    public GameObject mainMenuPanel;
    public GameObject settingsPanel;
    public GameObject creditsPanel;

    [SerializeField] private Button musicButton;
    [SerializeField] private Button sfxButton;

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

    //Setting panel
    public void ToggleMusicButton()
    {
        SoundManager.Instance.ToggleMusic();

        Image img = musicButton.image;
        img.color = SoundManager.Instance.musicSource.mute
            ? new Color(0.6f, 0.6f, 0.6f)  
            : Color.white;
    }

    public void ToggleSFXButton()
    {
        SoundManager.Instance.ToggleSFX();

        Image img = sfxButton.image;
        img.color = SoundManager.Instance.sfxSource.mute
            ? new Color(0.6f, 0.6f, 0.6f)  
            : Color.white;
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

        #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}
