using UnityEngine;
using UnityEngine.UI;

public class HPBar : MonoBehaviour
{
    [SerializeField] private Image fill;

    public void SetValue(float normalized)
    {
        fill.fillAmount = Mathf.Clamp01(normalized);
    }
}
