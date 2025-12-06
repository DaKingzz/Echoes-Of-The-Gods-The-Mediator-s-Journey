using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    [Header("Audio Sources")] [Tooltip("Primary music audio source (auto-created if null).")]
    public AudioSource musicSource; // will become musicA

    [Tooltip("SFX audio source.")] public AudioSource sfxSource;

    [Header("Music Clips")] public AudioClip menuMusic; // Used for Main Menu + Pause Panel
    public AudioClip gameMusic; // Used for Gameplay

    [Header("SFX Clips")] public AudioClip clickSound; // Shared for all buttons

    [Header("Crossfade Settings")] [Tooltip("Seconds of overlap between end and start of the loop.")] [Range(0.1f, 5f)]
    public float crossfadeSeconds = 1.5f;

    private Coroutine loopRoutine;

    // Internal dual sources
    private AudioSource musicA;
    private AudioSource musicB;
    private AudioSource currentSource;
    private AudioSource nextSource;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;

            EnsureMusicSources();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void EnsureMusicSources()
    {
        // Create two persistent music sources (or reuse provided ones)
        if (musicSource == null)
        {
            musicA = gameObject.AddComponent<AudioSource>();
        }
        else
        {
            musicA = musicSource;
        }

        musicB = gameObject.AddComponent<AudioSource>();

        // Common setup for both
        ConfigureMusicSource(musicA);
        ConfigureMusicSource(musicB);

        currentSource = musicA;
        nextSource = musicB;
    }

    private static void ConfigureMusicSource(AudioSource src)
    {
        src.playOnAwake = false;
        src.loop = false; // we handle loop manually
        src.spatialBlend = 0f; // 2D
        src.volume = 1f;
        src.outputAudioMixerGroup = null; // set your mixer group if you use one
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "MainMenu")
            PlayMenuMusic();
        else if (scene.name == "Game")
            PlayGameMusic();
    }

    // === Public Methods ===

    public void PlayMenuMusic()
    {
        StopLoopRoutine();

        // Single-source simple loop for menu
        currentSource.clip = menuMusic;
        nextSource.clip = null; // ensure not used
        currentSource.volume = 1f;
        currentSource.loop = true; // no crossfade needed in menu
        currentSource.Play();
    }

    public void PlayGameMusic()
    {
        StopLoopRoutine();

        if (gameMusic == null)
        {
            Debug.LogWarning("PlayGameMusic: gameMusic clip is null.");
            return;
        }

        // Prepare both sources for crossfade looping
        currentSource.clip = gameMusic;
        nextSource.clip = gameMusic;

        currentSource.loop = false;
        nextSource.loop = false;

        currentSource.volume = 1f;
        nextSource.volume = 0f;

        currentSource.time = 0f;
        nextSource.time = 0f;

        currentSource.Play();

        loopRoutine = StartCoroutine(CrossfadeLoopForever(gameMusic));
    }

    public void PlayClick()
    {
        if (clickSound != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(clickSound);
        }
    }

    public void ToggleMusic()
    {
        bool mute = !(currentSource?.mute ?? false);
        if (currentSource != null) currentSource.mute = mute;
        if (nextSource != null) nextSource.mute = mute;
    }

    public void ToggleSFX()
    {
        if (sfxSource != null) sfxSource.mute = !sfxSource.mute;
    }

    // === Internal ===

    private void StopLoopRoutine()
    {
        if (loopRoutine != null)
        {
            StopCoroutine(loopRoutine);
            loopRoutine = null;
        }

        // Stop both sources cleanly
        if (currentSource != null) currentSource.Stop();
        if (nextSource != null) nextSource.Stop();
    }

    private IEnumerator CrossfadeLoopForever(AudioClip clip)
    {
        if (clip == null) yield break;

        // clamp fade duration to sensible bounds
        float fade = Mathf.Clamp(crossfadeSeconds, 0.1f, clip.length / 2f);

        // Run forever
        while (true)
        {
            // Wait until near the end of the current clip
            // We use time-based wait rather than PlayScheduled for simplicity; good enough for most BGM
            float wait = Mathf.Max(0f, clip.length - fade);
            float elapsed = 0f;
            while (elapsed < wait)
            {
                // If music was stopped externally, exit early
                if (!currentSource.isPlaying) break;
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Start next from the beginning at zero volume
            nextSource.Stop();
            nextSource.time = 0f;
            nextSource.volume = 0f;
            nextSource.Play();

            // Equal-power crossfade over 'fade' seconds
            float t = 0f;
            while (t < fade)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / fade);

                // Equal-power curve to keep perceived loudness steady
                currentSource.volume = Mathf.Sqrt(1f - k);
                nextSource.volume = Mathf.Sqrt(k);

                // If one source stopped unexpectedly, break
                if (!currentSource.isPlaying && !nextSource.isPlaying)
                    break;

                yield return null;
            }

            // After fade: ensure volumes are at their targets
            currentSource.volume = 0f;
            nextSource.volume = 1f;

            // Swap roles: next becomes current, current becomes next
            var temp = currentSource;
            currentSource = nextSource;
            nextSource = temp;

            // Safety: ensure nextSource has the clip assigned (for the next loop)
            nextSource.clip = clip;
            nextSource.loop = false;
        }
    }
}