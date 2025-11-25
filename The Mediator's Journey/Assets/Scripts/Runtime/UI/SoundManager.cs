using UnityEngine;
using UnityEngine.SceneManagement;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    [Header("Audio Sources")]
    public AudioSource musicSource;
    public AudioSource sfxSource;

    [Header("Music Clips")]
    public AudioClip menuMusic;   // Used for Main Menu + Pause Panel
    public AudioClip gameMusic;   // Used for Gameplay

    [Header("SFX Clips")]
    public AudioClip clickSound;  // Shared for all buttons

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "MainMenu")
            PlayMenuMusic();
        else if (scene.name == "Game")
            PlayGameMusic();
    }

    // Play background music
    public void PlayMenuMusic()
    {
        if (musicSource.clip == menuMusic) return;
        musicSource.clip = menuMusic;
        musicSource.loop = true;
        musicSource.Play();
    }

    public void PlayGameMusic()
    {
        if (musicSource.clip == gameMusic) return;
        musicSource.clip = gameMusic;
        musicSource.loop = true;
        musicSource.Play();
    }

    // Play UI click sound
    public void PlayClick()
    {
        sfxSource.PlayOneShot(clickSound);
    }
}
