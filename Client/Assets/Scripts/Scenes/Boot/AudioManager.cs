using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip loginClip;
    [SerializeField] private AudioClip lobbyClip;
    [SerializeField] private AudioClip battleClip;

    private void Awake()
    {
        // 중복 방지
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void PlayBGM(AudioClip clip, bool loop = true)
    {
        bgmSource.clip = clip;
        bgmSource.loop = loop;
        bgmSource.Play();
    }

    public void PlaySFX(AudioClip clip)
    {
        sfxSource.PlayOneShot(clip);
    }

    public void StopBGM()
    {
        bgmSource.Stop();
    }

    public void SetVolume(float volume)
    {
        bgmSource.volume = volume;
        sfxSource.volume = volume;
    }
}
