using UnityEngine;

/*
 * ViewCanvas.cs
 * 
 * 역할 :
 * - 로드되는 씬의 alpha 값 초기화 후, Fade In
 * 
 */

public class ViewCanvas : MonoBehaviour
{
    void Start()
    {
        GetComponent<CanvasGroup>().alpha = 0f;
        StartCoroutine(SceneLoader.Instance.ViewSceneCoroutine(0.5f));
    }
}
