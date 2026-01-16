using System.Collections.Generic;
using UnityEngine;

/*
 * UserData.cs
 * 
 * 역할 :
 * - 로컬에 저장된 User 세팅 불러오기
 * 
 */

public class UserData : MonoBehaviour
{
    public static UserData Instance { get; private set; }
    public List<Sprite> images = new List<Sprite>();

    private void Awake()
    {
        QualitySettings.vSyncCount = 0; // 프레임 제한 X
        Application.targetFrameRate = 120; // 120fps 고정

        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
