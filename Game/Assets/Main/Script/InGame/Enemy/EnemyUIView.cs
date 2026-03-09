using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EnemyUIView : MonoBehaviour
{
    EnemyUI_View_Setter setter = null;
    public EnemyUI_View_Setter SetSetter { set { if (setter == null) { setter = value; } } }
    public bool IsSetterReady => setter != null;


    private void Awake()
    {
        Debug.Log(gameObject);
        if (setter != null)
        {
            setter.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] enemyUI が null です.");
        }
    }

    // 最大HPを保持.
    private float maxHp = 100f;

    /// <summary>
    /// 最大HPを設定.
    /// </summary>
    public void SetMaxHp(float max)
    {
        maxHp = max;
    }

    public void UpdateHpGauge(ReactiveProperty<int> hp)
    {
        if (setter == null)
        {
            Debug.LogWarning($"[{gameObject.name}] hpGauge が null です.");
            return;
        }
        hp.Subscribe(x =>
        {
            // HPの値を0-1の範囲に正規化してフィルアマウントに設定.
            float percent = Mathf.Clamp01((float)hp.Value / maxHp);
            setter.hpPercent = percent;
        });
    }

    public void UpdateHpGauge(ReactiveProperty<float> hp)
    {
        if (setter == null)
        {
            Debug.LogWarning($"[{gameObject.name}] hpGauge が null です.");
            return;
        }
        hp.Subscribe(x =>
        {
            // HPの値を0-1の範囲に正規化してフィルアマウントに設定.
            float percent = Mathf.Clamp01(hp.Value / maxHp);
            setter.hpPercent = percent;
        });
    }

    public void SetEnemyName(string _name)
    {
        if (setter == null)
        {
            Debug.LogWarning($"[{gameObject.name}] enemyNameText が null です.");
            return;
        }
        setter.SetName = _name;
    }

    /// <summary>
    /// UI有効化.
    /// </summary>
    public void EnableEnemyUI()
    {
        if (setter == null)
        {
            Debug.LogWarning($"[{gameObject.name}] enemyUI が null です.");
            return;
        }
        setter.gameObject.SetActive(true);
    }

    public void SetHpGauge(float percent)
    {
        if (setter == null)
        {
            Debug.LogWarning($"[{gameObject.name}] hpGauge が null です.");
            return;
        }
        setter.hpPercent = percent;
    }
}
