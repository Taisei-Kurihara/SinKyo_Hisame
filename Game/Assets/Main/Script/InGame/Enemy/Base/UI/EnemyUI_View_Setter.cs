using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EnemyUI_View_Setter : MonoBehaviour
{
    [SerializeField]
    UnityEngine.UI.Image gage;
    [SerializeField]
    new TextMeshProUGUI name;

    public string SetName { set { name.text = value; } }

    // HPゲージアニメーション用.
    private float targetHpPercent = 1f;
    private const float hpGaugeAnimDuration = 0.5f;

    public float hpPercent { set { targetHpPercent = value; } }

    private void Update()
    {
        if (gage != null && !Mathf.Approximately(gage.fillAmount, targetHpPercent))
        {
            float maxDelta = Time.deltaTime / hpGaugeAnimDuration;
            gage.fillAmount = Mathf.MoveTowards(gage.fillAmount, targetHpPercent, maxDelta);
        }
    }
}
