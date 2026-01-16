using System.IO;
using UnityEngine;

/*
 * SettingData.cs
 * 
 * 역할 :
 * - 클라이언트 데이터 저장 및 불러오기
 * 
 */

[System.Serializable]
public class SettingsData
{
    public float BGM_Volume = 1f;
    public float SFX_Volume = 1f;
    public string language = "en";
    public string resolution = "1920x1080";
    public bool fullscreen = true;

    private static string filePath => Path.Combine(Application.persistentDataPath, "settings.json");

    private static SettingsData _current;
    public static SettingsData Current
    {
        get
        {
            if (_current == null)
                Load(); // 처음 접근 시 자동 로드
            return _current;
        }
    }

    // JSON으로 저장
    public static void Save()
    {
        string json = JsonUtility.ToJson(Current, true);
        File.WriteAllText(filePath, json);
        Debug.Log($"Settings saved to: {filePath}");
    }

    // 모든 설정 초기화
    public static void Reset()
    {
        _current = new SettingsData();
        Save();
        Debug.Log("Settings reset to default");
    }

    // JSON에서 불러오기
    public static void Load()
    {
        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);
            _current = JsonUtility.FromJson<SettingsData>(json);
            Debug.Log("Settings loaded");
        }
        else
        {
            _current = new SettingsData(); // 기본값 생성
            Save(); // 파일 없으면 새로 저장
            Debug.Log("Settings file not found. Created new one.");
        }
    }

    
}
