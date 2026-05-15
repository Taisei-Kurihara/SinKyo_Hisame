using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>
/// AI行動決定ロジックの基底クラス.
/// RunUpdateはpublicかつ子クラスで変更不可（non-virtual）.
/// 子クラスはOnUpdateStart/OnUpdateLoop/OnUpdateEndをoverrideして行動ロジックを実装する.
/// </summary>
public abstract class EnemAIUpdater_abstract
{
    protected EnemAIModel_abstract masterAI;

    // === ライフサイクル制御 ===

    // 停止条件predicate（masterが設定）.
    private System.Func<bool> stopCondition;

    // 停止完了通知コールバック.
    private System.Action onUpdaterStopped;

    // 一時停止フラグ.
    private bool isPaused = false;

    // Updater単位のCTS（masterのtokenとリンク）.
    private CancellationTokenSource updaterCts;

    // 実行中フラグ.
    public bool IsRunning { get; private set; } = false;

    // === 怒りゲージ基盤 ===

    // 怒りゲージ現在値.
    protected float angerGauge = 0f;
    public float AngerGauge => angerGauge;

    // 怒りゲージ閾値（子クラスで設定）.
    protected float angerGaugeThreshold = 100f;
    public float AngerGaugeThreshold => angerGaugeThreshold;

    // 怒り状態フラグ.
    protected bool isAngry = false;
    public bool IsAngry => isAngry;

    public EnemAIUpdater_abstract(EnemAIModel_abstract master)
    {
        masterAI = master;
    }

    // === ライフサイクルメソッド ===

    /// <summary>
    /// 停止条件を設定. 条件がtrueを返した場合、ループ終了後にonStoppedが呼ばれる.
    /// </summary>
    public void SetStopCondition(System.Func<bool> condition, System.Action onStopped)
    {
        stopCondition = condition;
        onUpdaterStopped = onStopped;
    }

    /// <summary>一時停止.</summary>
    public void Pause()
    {
        isPaused = true;
        Debug.Log($"[EnemAIUpdater] 一時停止");
    }

    /// <summary>一時停止解除.</summary>
    public void Resume()
    {
        isPaused = false;
        Debug.Log($"[EnemAIUpdater] 一時停止解除");
    }

    /// <summary>
    /// 強制停止（OnUpdateEndを実行せずに即座にループ終了）.
    /// </summary>
    public void ForceStop()
    {
        Debug.Log($"[EnemAIUpdater] 強制停止要求");
        updaterCts?.Cancel();
    }

    // === 怒りゲージメソッド ===

    /// <summary>
    /// 怒りゲージを増加させる（外部からの呼び出し用、例: ダメージ時）.
    /// </summary>
    public virtual void IncreaseAngerGauge(float amount)
    {
        if (isAngry)
        {
            amount *= 0.5f;
        }
        angerGauge = Mathf.Clamp(angerGauge + amount, 0f, angerGaugeThreshold);

        if (!isAngry && angerGauge >= angerGaugeThreshold)
        {
            EnterAngerState();
        }
    }

    /// <summary>
    /// 怒りゲージを減衰させる（毎ループ呼ぶ想定）.
    /// </summary>
    protected virtual void DecayAngerGauge(float deltaTime)
    {
        float decayRate = isAngry ? 3f : 1f;
        angerGauge = Mathf.Max(0f, angerGauge - decayRate * deltaTime);

        if (isAngry && angerGauge <= 0f)
        {
            ExitAngerState();
        }
    }

    /// <summary>怒り状態開始（子クラスでoverride可能）.</summary>
    protected virtual void EnterAngerState()
    {
        isAngry = true;
        Debug.Log($"[EnemAIUpdater] 怒り状態開始 - AngerGauge:{angerGauge:F2}/{angerGaugeThreshold}");
    }

    /// <summary>怒り状態解除（子クラスでoverride可能）.</summary>
    protected virtual void ExitAngerState()
    {
        isAngry = false;
        angerGauge = 0f;
        Debug.Log($"[EnemAIUpdater] 怒り状態解除");
    }

    // === メインループ ===

    /// <summary>
    /// AIアップデートのメインエントリポイント（子クラスでoverride不可）.
    /// OnUpdateStart → OnUpdateLoop(loop) → OnUpdateEnd の順で呼び出す.
    /// </summary>
    public async UniTask RunUpdate(CancellationToken token)
    {
        Debug.Log($"[EnemAIUpdater] RunUpdate開始 - {GetType().Name}");

        // Updater固有のCTS生成（masterのtokenとリンク）.
        updaterCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        var linkedToken = updaterCts.Token;
        IsRunning = true;

        await OnUpdateStart(linkedToken);

        int loopCount = 0;
        while (!linkedToken.IsCancellationRequested)
        {
            if (masterAI.OwnerModel == null || masterAI.OwnerModel.Presenter == null)
            {
                Debug.LogWarning($"[EnemAIUpdater] ループ終了 - OwnerModel or Presenter が null (loopCount: {loopCount})");
                break;
            }

            // 一時停止中はYieldだけして待機.
            if (isPaused)
            {
                await UniTask.Yield(linkedToken);
                continue;
            }

            // 停止条件チェック.
            if (stopCondition != null && stopCondition())
            {
                Debug.Log($"[EnemAIUpdater] 停止条件成立 - ループ終了");
                break;
            }

            try
            {
                await OnUpdateLoop(linkedToken);
                await UniTask.Yield(linkedToken);
                loopCount++;
            }
            catch (MissingReferenceException e)
            {
                // Destroyされたオブジェクトへのアクセス → ループ終了.
                Debug.LogWarning($"[EnemAIUpdater] Destroyされたオブジェクトアクセス検出 - ループ終了: {e.Message}");
                break;
            }
            catch (System.Exception e) when (e is not System.OperationCanceledException)
            {
                Debug.LogError($"[EnemAIUpdater] 例外発生: {e.Message}");
                if (masterAI == null || masterAI.OwnerModel == null || masterAI.OwnerModel.Presenter == null) break;
                throw;
            }
        }

        // 強制停止でなければOnUpdateEndを実行.
        if (!linkedToken.IsCancellationRequested)
        {
            await OnUpdateEnd(token);
        }

        IsRunning = false;

        // CTS破棄.
        updaterCts?.Dispose();
        updaterCts = null;

        // 停止通知をmasterへ送信.
        onUpdaterStopped?.Invoke();
    }

    /// <summary>ループ開始前に1回呼ばれる（子クラスでoverride可能）.</summary>
    protected virtual async UniTask OnUpdateStart(CancellationToken token)
    {
        await UniTask.CompletedTask;
    }

    /// <summary>毎ループ呼ばれる行動決定処理（子クラスで実装必須）.</summary>
    protected abstract UniTask OnUpdateLoop(CancellationToken token);

    /// <summary>ループ終了後に1回呼ばれる（子クラスでoverride可能）.</summary>
    protected virtual async UniTask OnUpdateEnd(CancellationToken token)
    {
        await UniTask.CompletedTask;
    }
}
