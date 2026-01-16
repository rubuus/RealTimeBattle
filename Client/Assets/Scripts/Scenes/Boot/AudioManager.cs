using UnityEngine;

/*
 * AudioManager.cs
 * 
 * 역할 :
 * - 게임 Audio 관리
 * 
 */

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

    // BGM 시작
    public void PlayBGM(AudioClip clip, bool loop = true)
    {
        bgmSource.clip = clip;
        bgmSource.loop = loop;
        bgmSource.Play();
    }

    // BGM 멈춤
    public void StopBGM()
    {
        bgmSource.Stop();
    }

    // 이벤트 발생 시, SFX 한번
    public void PlaySFX(AudioClip clip)
    {
        sfxSource.PlayOneShot(clip);
    }

    // Volume 세팅
    public void SetVolume(float volume)
    {
        bgmSource.volume = volume;
        sfxSource.volume = volume;
    }
}
