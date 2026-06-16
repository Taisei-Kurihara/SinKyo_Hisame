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

    // --- 怒りビジュアル ---
    private SpriteRenderer[] cachedRenderers = null;
    private bool isAngerBlinking = false;
    private CancellationTokenSource angerVisualCts = null;

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

    /// <summary>減衰: 通常1/sec、怒り中3/sec. 怒り中はビジュアル更新も行う.</summary>
    protected override void DecayAngerGauge(float deltaTime)
    {
        float rate = isAngry ? angryDecayPerSec : normalDecayPerSec;
        angerGauge = Mathf.Max(0f, angerGauge - rate * deltaTime);

        // 怒り中かつ点滅中でなければ、ゲージ残量に応じた赤色を適用.
        if (isAngry && !isAngerBlinking)
        {
            float t = angerGaugeThreshold > 0f ? angerGauge / angerGaugeThreshold : 0f;
            UpdateAngerTint(t);
        }

        if (isAngry && angerGauge <= 0f)
        {
            ExitAngerState();
        }
    }

    protected override void EnterAngerState()
    {
        // 前回のビジュアル処理をキャンセル.
        CancelAngerVisual();
        angerVisualCts = new CancellationTokenSource();

        base.EnterAngerState();
        PlayAngerStartBlink(angerVisualCts.Token).Forget();
        OnEnterAnger();
    }

    protected override void ExitAngerState()
    {
        // 点滅/ビジュアルをキャンセルしてからフェードリセット.
        CancelAngerVisual();
        angerVisualCts = new CancellationTokenSource();
        PlayAngerExitFade(angerVisualCts.Token).Forget();

        base.ExitAngerState();
        OnExitAnger();
    }

    /// <summary>怒りビジュアル用CTSをキャンセル・破棄.</summary>
    private void CancelAngerVisual()
    {
        if (angerVisualCts != null)
        {
            angerVisualCts.Cancel();
            angerVisualCts.Dispose();
            angerVisualCts = null;
        }
        isAngerBlinking = false;
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

    // === 怒りビジュアルエフェクト ===

    /// <summary>SpriteRendererをキャッシュ取得.</summary>
    private SpriteRenderer[] GetRenderers()
    {
        if (cachedRenderers == null && masterAI.OwnerModel?.Presenter != null)
        {
            cachedRenderers = masterAI.OwnerModel.Presenter.GetComponentsInChildren<SpriteRenderer>();
        }
        return cachedRenderers;
    }

    /// <summary>怒り開始時の赤点滅（3回 / 1.3秒）.</summary>
    private async UniTaskVoid PlayAngerStartBlink(CancellationToken ct)
    {
        var renderers = GetRenderers();
        if (renderers == null || renderers.Length == 0) return;

        isAngerBlinking = true;
        float cycleDuration = 1.3f / 3f;
        int halfCycleMs = (int)(cycleDuration * 0.5f * 1000f);

        try
        {
            for (int i = 0; i < 3; i++)
            {
                SetRenderersColor(renderers, Color.red);
                await UniTask.Delay(halfCycleMs, cancellationToken: ct);
                SetRenderersColor(renderers, Color.white);
                await UniTask.Delay(halfCycleMs, cancellationToken: ct);
            }
        }
        catch (System.OperationCanceledException) { }
        finally
        {
            isAngerBlinking = false;
        }
    }

    /// <summary>怒り解除時の滑らかな色フェード（現在色→白）.</summary>
    private async UniTaskVoid PlayAngerExitFade(CancellationToken ct)
    {
        var renderers = GetRenderers();
        if (renderers == null || renderers.Length == 0) return;

        // 現在の色を取得（最初のRendererから）.
        Color startColor = Color.white;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                startColor = renderers[i].color;
                break;
            }
        }

        // 既に白ならスキップ.
        if (startColor == Color.white) return;

        float fadeDuration = 0.4f;
        float elapsed = 0f;

        try
        {
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                Color current = Color.Lerp(startColor, Color.white, t);
                SetRenderersColor(renderers, current);
                await UniTask.Yield(ct);
            }
            SetRenderersColor(renderers, Color.white);
        }
        catch (System.OperationCanceledException) { }
    }

    /// <summary>怒りゲージ残量に応じた赤色を適用.</summary>
    private void UpdateAngerTint(float intensity)
    {
        var renderers = GetRenderers();
        if (renderers == null) return;
        Color tint = Color.Lerp(Color.white, new Color(1f, 0.4f, 0.4f), intensity);
        SetRenderersColor(renderers, tint);
    }

    /// <summary>怒りビジュアルをリセット（白に戻す）.</summary>
    private void ClearAngerTint()
    {
        var renderers = GetRenderers();
        if (renderers == null) return;
        SetRenderersColor(renderers, Color.white);
    }

    /// <summary>全SpriteRendererの色を設定.</summary>
    private void SetRenderersColor(SpriteRenderer[] renderers, Color color)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].color = color;
            }
        }
    }
}
