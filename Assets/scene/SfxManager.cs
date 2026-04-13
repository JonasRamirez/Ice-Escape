using UnityEngine;

public class SfxManager : MonoBehaviour
{
    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Audio Clips")]
    [SerializeField] private AudioClip deathSound;
    [SerializeField] private AudioClip winSound;
    [SerializeField] private AudioClip buttonSound;
    [SerializeField] private AudioClip music;

    [Header("Volume Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float musicVolume = 0.5f;
    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 0.7f;

    // Singleton pattern (opcional, para acceder fácilmente desde cualquier script)
    public static SfxManager Instance { get; private set; }

    private void Awake()
    {
        // Singleton implementation
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Persiste entre escenas
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Configurar audio sources si no están asignados
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
        }
        
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
        }

        // Configurar música
        musicSource.loop = true;
        musicSource.volume = musicVolume;
        musicSource.playOnAwake = false;

        // Configurar SFX
        sfxSource.loop = false;
        sfxSource.volume = sfxVolume;
        sfxSource.playOnAwake = false;

        // Iniciar música automáticamente
        PlayMusic();
    }

    // =========================
    // 🎵 FUNCIONES PÚBLICAS
    // =========================

    /// <summary>
    /// Reproduce el sonido de muerte
    /// </summary>
    public void PlayDeath()
    {
        if (deathSound != null)
        {
            sfxSource.PlayOneShot(deathSound, sfxVolume);
        }
        else
        {
            Debug.LogWarning("Death sound not assigned in SfxManager");
        }
    }

    /// <summary>
    /// Reproduce el sonido de victoria
    /// </summary>
    public void PlayWin()
    {
        if (winSound != null)
        {
            sfxSource.PlayOneShot(winSound, sfxVolume);
        }
        else
        {
            Debug.LogWarning("Win sound not assigned in SfxManager");
        }
    }

    /// <summary>
    /// Reproduce el sonido de botón
    /// </summary>
    public void PlayButton()
    {
        if (buttonSound != null)
        {
            sfxSource.PlayOneShot(buttonSound, sfxVolume);
        }
        else
        {
            Debug.LogWarning("Button sound not assigned in SfxManager");
        }
    }

    /// <summary>
    /// Inicia/reproduce la música (loop automático)
    /// </summary>
    public void PlayMusic()
    {
        if (music != null)
        {
            musicSource.clip = music;
            musicSource.Play();
        }
        else
        {
            Debug.LogWarning("Music clip not assigned in SfxManager");
        }
    }

    /// <summary>
    /// Detiene la música
    /// </summary>
    public void StopMusic()
    {
        musicSource.Stop();
    }

    /// <summary>
    /// Pausa la música
    /// </summary>
    public void PauseMusic()
    {
        musicSource.Pause();
    }

    /// <summary>
    /// Reanuda la música
    /// </summary>
    public void ResumeMusic()
    {
        musicSource.UnPause();
    }

    /// <summary>
    /// Cambia el volumen de la música
    /// </summary>
    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        musicSource.volume = musicVolume;
    }

    /// <summary>
    /// Cambia el volumen de los efectos de sonido
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        sfxSource.volume = sfxVolume;
    }
}