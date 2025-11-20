using UnityEngine;

public class ViewCanvas : MonoBehaviour
{
    void Start()
    {
        GetComponent<CanvasGroup>().alpha = 0f;
        StartCoroutine(SceneLoader.Instance.ViewSceneCoroutine(0.5f));
    }
}
