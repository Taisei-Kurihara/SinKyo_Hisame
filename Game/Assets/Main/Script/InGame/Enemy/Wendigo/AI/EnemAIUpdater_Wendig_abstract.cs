using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>
/// Wendigo用AIアップデーターの基底クラス.
/// 自身の主たるMasterAIクラスをEnemAIModel_Wendig_Normalに決定する.
/// Wendigo共通の怒りゲージ動作と速度修正基盤を提供する.
/// </summary>
public abstract class EnemAIUpdater_Wendig_abstract : EnemAIUpdater_abstract
{
    /// <summary>Wendigo用MasterAIへの型付き参照.</summary>
    protected EnemAIModel_Wendig_Normal WendigMasterAI { get; private set; }

    // --- Wendigo怒りゲージ設定 ---

    // 怒り閾値 = currentMaxHp / divisor（設定可能）.
    protected float angerThresholdDivisor = 3f;

    // 減衰速度（通常/怒り中）.
    protected float normalDecayPerSec = 1f;
    protected float angryDecayPerSec = 3f;

    // --- 速度修正 ---

    // 現在の速度修正.
    protected WendigSpeedModifier currentSpeedModifier = WendigSpeedModifier.Default;

    public EnemAIUpdater_Wendig_abstract(EnemAIModel_Wendig_Normal master) : base(master)
    {
        WendigMasterAI = master;
    }

    // === 怒りゲージ ===

    /// <summary>
    /// 怒りゲージ閾値を初期化（HPフェーズ情報に基づく）.
    /// </summary>
    protected void InitAngerThreshold(float currentMaxHp)
    {
        angerGaugeThreshold = currentMaxHp / angerThresholdDivisor;
        Debug.Log($"[WendigUpdater] 怒り閾値設定: {angerGaugeThreshold:F1} (maxHP:{currentMaxHp} / {angerThresholdDivisor})");
    }

    /// <summary>
    /// HP減少 × 2 → 怒りゲージ増加（怒り中は×0.5）.
    /// amountは呼び出し元で既にdamage*2済み.
    /// </summary>
    public override void IncreaseAngerGauge(float amount)
    {
        if (isAngry)
        {
            amount *= 0.5f;
        }
        float prev = angerGauge;
        angerGauge = Mathf.Clamp(angerGauge + amount, 0f, angerGaugeThreshold);

        Debug.Log($"[WendigUpdater] 怒りゲージ増加: {prev:F2} → {angerGauge:F2} / {angerGaugeThreshold:F1} (amount:{amount:F2} isAngry:{isAngry})");

        if (!isAngry && angerGauge >= angerGaugeThreshold)
        {
            EnterAngerState();
        }
    }

    /// <summary>減衰: 通常1/sec、怒り中3/sec.</summary>
    protected override void DecayAngerGauge(float deltaTime)
    {
        float rate = isAngry ? angryDecayPerSec : normalDecayPerSec;
        angerGauge = Mathf.Max(0f, angerGauge - rate * deltaTime);

        if (isAngry && angerGauge <= 0f)
        {
            ExitAngerState();
        }
    }

    protected override void EnterAngerState()
    {
        base.EnterAngerState();
        OnEnterAnger();
    }

    protected override void ExitAngerState()
    {
        base.ExitAngerState();
        OnExitAnger();
    }

    /// <summary>怒り開始時の追加処理（子クラスでoverride）.</summary>
    protected virtual void OnEnterAnger() { }

    /// <summary>怒り解除時の追加処理（子クラスでoverride）.</summary>
    protected virtual void OnExitAnger() { }

    // === 速度修正 ===

    /// <summary>速度修正を適用（Animator.speed等）.</summary>
    protected void ApplySpeedModifier(WendigSpeedModifier modifier)
    {
        currentSpeedModifier = modifier;
        if (masterAI.OwnerModel?.Animator != null)
        {
            masterAI.OwnerModel.Animator.speed = modifier.speedMultiplier;
        }
    }

    /// <summary>速度修正適用時の移動速度を取得.</summary>
    protected float GetMoveSpeed(float baseSpeed)
    {
        return baseSpeed * currentSpeedModifier.speedMultiplier;
    }

    /// <summary>速度修正適用時の接近速度を取得.</summary>
    protected float GetApproachSpeed(float baseSpeed)
    {
        return baseSpeed * currentSpeedModifier.speedMultiplier;
    }
}
