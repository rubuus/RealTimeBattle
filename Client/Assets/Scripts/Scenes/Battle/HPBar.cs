using UnityEngine;
using UnityEngine.UI;

/*
 * HPBar.cs
 * 
 * 역할 :
 * - HP 업데이트 시, UI 표시
 * 
 */

public class HPBar : MonoBehaviour
{
    [SerializeField] private Image fill;

    public void SetValue(float normalized)
    {
        fill.fillAmount = Mathf.Clamp01(normalized);
    }
}
