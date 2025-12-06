using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    [Header("Audio Sources")] public AudioSource musicSource;
    public AudioSource sfxSource;

    [Header("Music Clips")] public AudioClip menuMusic; // Used for Main Menu + Pause Panel
    public AudioClip gameMusic; // Used for Gameplay

    [Header("SFX Clips")] public AudioClip clickSound; // Shared for all buttons

    [Header("Crossfade Settings")] [Tooltip("Seconds of overlap between end and start of the loop.")] [Range(0.1f, 5f)]
    public float crossfadeSeconds = 1.5f;

    private Coroutine loopRoutine;

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

    // === Public Methods (unchanged) ===

    public void PlayMenuMusic()
    {
        StopLoopRoutine();
        if (musicSource.clip == menuMusic) return;
        musicSource.clip = menuMusic;
        musicSource.loop = true;
        musicSource.volume = 1f;
        musicSource.Play();
    }

    public void PlayGameMusic()
    {
        StopLoopRoutine();
        if (musicSource.clip == gameMusic) return;
        musicSource.clip = gameMusic;
        musicSource.loop = false; // We'll handle looping manually
        musicSource.volume = 1f;
        musicSource.Play();

        // Start crossfade loop
        loopRoutine = StartCoroutine(CrossfadeLoop());
    }

    public void PlayClick()
    {
        sfxSource.PlayOneShot(clickSound);
    }

    public void ToggleMusic()
    {
        musicSource.mute = !musicSource.mute;
    }

    public void ToggleSFX()
    {
        sfxSource.mute = !sfxSource.mute;
    }

    // === Internal ===

    private void StopLoopRoutine()
    {
        if (loopRoutine != null)
        {
            StopCoroutine(loopRoutine);
            loopRoutine = null;
        }
    }


    private IEnumerator CrossfadeLoop()
    {
        AudioClip clip = gameMusic;
        if (clip == null) yield break;

        while (true)
        {
            float fade = Mathf.Clamp(crossfadeSeconds, 0.1f, clip.length / 2f);
            yield return new WaitForSeconds(clip.length - fade);

            // Prepare second source
            AudioSource tempSource = gameObject.AddComponent<AudioSource>();
            tempSource.clip = clip;
            tempSource.volume = 0f;
            tempSource.Play();

            float t = 0f;
            while (t < fade)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / fade);
                musicSource.volume = Mathf.Sqrt(1f - k);
                tempSource.volume = Mathf.Sqrt(k);
                yield return null;
            }

            // Instead of destroying, swap references
            musicSource.Stop();
            Destroy(musicSource);
            musicSource = tempSource;
        }
    }
}