using Audio;
using Cysharp.Threading.Tasks;
using InGame;
using R3;
using System.Threading;
using UnityEngine;


/// <summary>
/// 攻撃の種別タグ.
/// 回避パリィ時にどのスタン処理を適用するかの判定に使用する.
/// </summary>
public enum EnemyAttackTag
{
    Normal,    // 通常攻撃（パリィ可能/不可問わず、パリィスタン対象）
    BigAttack,  // 大技（隕石落下等）: 5secスタン + チャンス状態対象
}

public abstract class EnemyPresenter_abstract : MonoBehaviour
{
    /// <summary>
    /// 必殺技の軌道中心として使う Transform（Inspector で設定）.
    /// 未設定の場合は PlayerPresenter がフォールバック値を使用する.
    /// 敵 GameObject の子オブジェクト（例: 胸部ボーン、専用 Empty）を割り当てること.
    ///
    /// ※ X は PlayerPresenter が Collider2D.bounds.center.x（ワールド空間）で自動補正するため,
    ///   この Transform は Y 高さ調整にのみ使われる.
    /// </summary>
    [SerializeField] private Transform chanceAttackOrbitCenter;
    public Transform ChanceAttackOrbitCenter => chanceAttackOrbitCenter;

    /// <summary>
    /// 必殺技軌道中心に加算するワールド空間オフセット（Inspector で微調整用）.
    /// X: 左右のずれ補正、Y: 上下のずれ補正.
    /// </summary>
    [SerializeField] private Vector2 chanceAttackOrbitOffset = Vector2.zero;
    public Vector2 ChanceAttackOrbitOffset => chanceAttackOrbitOffset;

    protected Animator animator;

    protected EnemyModel_abstract model;
    public EnemyModel_abstract Model => model;

    protected EnemyStatus_abstract status;
    public EnemyStatus_abstract Status => status;

    protected EnemyUIView view;

    protected abstract string EnemyName { get; }

    private CancellationTokenSource hpDrainCts;

    /// <summary>
    /// test用:HPが1秒ごとに1%減少する.
    /// </summary>
    public async UniTask TestHpDrain()
    {
        if (status == null)
        {
            Debug.LogWarning($"[{gameObject.name}] status が null のためHPドレイン開始不可.");
            return;
        }

        hpDrainCts?.Cancel();
        hpDrainCts = new CancellationTokenSource();
        var token = hpDrainCts.Token;

        Debug.Log($"[EnemyPresenter_abstract] TestHpDrain開始 - {gameObject.name}");

        while (!token.IsCancellationRequested && status.hp.Value > 0)
        {
            float drainAmount = status.maxhp * 0.01f;
            await status.OnDamaged(drainAmount);
            await UniTask.Delay(1000, cancellationToken: token);
        }

        Debug.Log($"[EnemyPresenter_abstract] TestHpDrain終了 - {gameObject.name}");
    }

    /// <summary>
    /// test用:HPドレインを停止.
    /// </summary>
    public void StopTestHpDrain()
    {
        hpDrainCts?.Cancel();
        hpDrainCts = null;
        Debug.Log($"[EnemyPresenter_abstract] TestHpDrain停止 - {gameObject.name}");
    }

    [SerializeField]
    protected Collider2D mainColl;
    public Collider2D MainColl => mainColl;

    [SerializeField]
    protected Animator attackWarningAnimator;

    // 被弾バウンスエフェクト.
    protected EnemyHitBounce hitBounce;

    // 画面外インジケーター.
    protected EnemyOffScreenIndicator offScreenIndicator;
    public EnemyOffScreenIndicator OffScreenIndicator => offScreenIndicator;

    // SE再生用.
    protected SEClipRegistry seRegistry;
    protected SEPlayer sePlayer;
    /// <summary>SE初期化完了フラグ.</summary>
    public bool IsSEReady { get; private set; }

    /// <summary>
    /// SE初期化（派生クラスでオーバーライド）.
    /// </summary>
    protected virtual async UniTask InitializeSE()
    {
        seRegistry = new SEClipRegistry();
        sePlayer = SEPlayer.Create($"{gameObject.name}_SE");
        await UniTask.CompletedTask;
    }

    /// <summary>
    /// SE初期化を実行し、完了フラグをセット.
    /// </summary>
    private async UniTaskVoid InitializeSEAndMarkReady()
    {
        await InitializeSE();
        IsSEReady = true;
    }

    /// <summary>
    /// SE再生.
    /// </summary>
    public void PlaySE(string actionName)
    {
        if (sePlayer != null && seRegistry != null)
        {
            sePlayer.PlayByAction(seRegistry, actionName);
        }
    }

    // ---- Animation Event コールバック（攻撃判定同期用） ----
    // State側からコールバックを登録し、アニメーションクリップの
    // AnimEvent_HitStart / AnimEvent_HitEnd イベントで判定開始/終了を通知する.
    private System.Action onAnimHitStart;
    private System.Action onAnimHitEnd;

    /// <summary>
    /// Animation Event: 攻撃ヒット判定の開始タイミング.
    /// アニメーションクリップのキーフレームから呼ばれる.
    ///
    /// 【Unity Editor での設定方法】
    /// 1. Project ウィンドウで対象のアニメーションクリップ(.anim)を選択
    ///    例: TripleAttack_0.anim, TripleAttack_1.anim, TripleAttack_2.anim
    /// 2. Animation ウィンドウを開く（Window > Animation > Animation）
    /// 3. タイムラインで「攻撃判定を開始したいフレーム」にシークバーを移動
    ///    （振り始め等、実際に武器が当たり始めるフレーム）
    /// 4. タイムライン上部の「Add Event」ボタン（▼マーク）をクリック
    ///    → イベントマーカーが追加される
    /// 5. Inspector に表示される Function ドロップダウンから
    ///    「AnimEvent_HitStart」を選択
    /// 6. 同様に「攻撃判定を終了したいフレーム」に移動し、
    ///    「AnimEvent_HitEnd」を追加
    /// 7. 各 TripleAttack_0 / _1 / _2 の3クリップすべてに設定
    ///
    /// ※ Animation Event は、Animator が存在する GameObject 上の
    ///    MonoBehaviour の public メソッドを呼び出す.
    ///    EnemyPresenter_abstract (MonoBehaviour) が Animator と同じ
    ///    GameObject にあるため、自動的に呼び出される.
    /// </summary>
    public void AnimEvent_HitStart() => onAnimHitStart?.Invoke();

    /// <summary>
    /// Animation Event: 攻撃ヒット判定の終了タイミング.
    /// 設定方法は AnimEvent_HitStart と同様.
    /// </summary>
    public void AnimEvent_HitEnd() => onAnimHitEnd?.Invoke();

    /// <summary>ヒット判定コールバックを登録.</summary>
    public void RegisterHitDetectionCallbacks(System.Action onStart, System.Action onEnd)
    {
        onAnimHitStart = onStart;
        onAnimHitEnd = onEnd;
    }

    /// <summary>コールバックをクリア.</summary>
    public void ClearHitDetectionCallbacks()
    {
        onAnimHitStart = null;
        onAnimHitEnd = null;
    }

    // ---- 攻撃タイミング公開（回避居合い判定用） ----
    /// <summary>攻撃が間近かどうか（PlayAttackWarning〜攻撃終了の間true）.</summary>
    public bool IsAttackImminent { get; private set; }
    /// <summary>攻撃通告が再生された時刻.</summary>
    public float AttackWarningTime { get; private set; }
    /// <summary>現在の攻撃がパリィ可能か.</summary>
    public bool IsCurrentAttackParryable { get; private set; }
    /// <summary>現在の攻撃の種別タグ（回避パリ���処理の分岐に使用）.</summary>
    public EnemyAttackTag CurrentAttackTag { get; private set; } = EnemyAttackTag.Normal;

    /// <summary>
    /// 攻撃タグをセットする.
    /// PlayAttackWarning と同タイミングか直前に呼ぶこと.
    /// </summary>
    public void SetAttackTag(EnemyAttackTag tag)
    {
        CurrentAttackTag = tag;
    }
    /// <summary>突進中フラグ（Rush stateからセット）.</summary>
    public bool IsRushing { get; set; }
    /// <summary>怒り時専用行動中フラグ（AIUpdaterからセット）.</summary>
    public bool IsAngerAction { get; set; }
    /// <summary>MeteorDrop（大技）実行中フラグ.</summary>
    public bool IsMeteorDropActive { get; set; }

    // ---- InGamePresenter 連携 ----
    /// <summary>ゲームプレイ上の戦闘状態（InGamePresenter に通知済みの最新値）.</summary>
    public EnemyBattleState BattleState { get; private set; } = EnemyBattleState.None;

    /// <summary>
    /// 戦闘状態を更新し InGamePresenter へ通知する.
    /// 各 State クラスから呼ぶこと（直接 BattleState を変更しない）.
    /// </summary>
    public void SetBattleState(EnemyBattleState state)
    {
        BattleState = state;
        InGamePresenter.Instance.SetEnemyState(state);
    }

    /// <summary>攻撃タイミングフラグをリセット.</summary>
    public void ClearAttackImminent()
    {
        IsAttackImminent = false;
        IsRushing = false;
        CurrentAttackTag = EnemyAttackTag.Normal;
    }

    /// <summary>
    /// 攻撃通告を再生.
    /// </summary>
    /// <param name="isParryable">パリィ可能な攻撃ならtrue.</param>
    public void PlayAttackWarning(bool isParryable)
    {
        IsAttackImminent = true;
        AttackWarningTime = Time.time;
        IsCurrentAttackParryable = isParryable;

        if (attackWarningAnimator == null) return;
        attackWarningAnimator.SetTrigger(isParryable ? "Yellow" : "Red");
        // 攻撃前SE再生.
        PlaySE(isParryable ? "AttackPre" : "AttackPreUnparryable");
    }


    private void Awake()
    {
        Debug.Log($"[EnemyPresenter_abstract] Awake開始 - {gameObject.name}");
        animator = GetComponent<Animator>();
        Debug.Log($"[EnemyPresenter_abstract] Animator: {(animator != null ? "取得" : "null")}");

        InitComponents();

        // 被弾バウンスコンポーネント追加.
        hitBounce = gameObject.GetComponent<EnemyHitBounce>();
        if (hitBounce == null)
        {
            hitBounce = gameObject.AddComponent<EnemyHitBounce>();
        }

        Debug.Log($"[EnemyPresenter_abstract] InitComponents完了 - model: {(model != null ? "生成" : "null")}, status: {(status != null ? "生成" : "null")}");

        if (model != null)
        {
            Debug.Log($"[EnemyPresenter_abstract] model.Presenter設定中");
            model.Presenter = this;
            Debug.Log($"[EnemyPresenter_abstract] model.TestInit呼び出し");
            model.TestInit();
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] model が null です.");
        }

        if (status != null)
        {
            Debug.Log($"[EnemyPresenter_abstract] status.Presenter設定中");
            status.Presenter = this;
            status.Init();
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] status が null です.");
        }

        Debug.Log($"[EnemyPresenter_abstract] Awake完了 - {gameObject.name}");

        // SE初期化（完了後にIsSEReady=trueになる）.
        InitializeSEAndMarkReady().Forget();

        // EnemyUIViewのsetterがnullでなくなったらEnemyNameをセット.
        WaitAndSetEnemyName().Forget();

        // 画面外インジケーター初期化.
        InitializeOffScreenIndicator().Forget();

        // 必殺技軌道中心 Transform + オフセットを InGamePresenter に登録.
        InGamePresenter.Instance.SetEnemyOrbitCenter(chanceAttackOrbitCenter, chanceAttackOrbitOffset);
    }

    /// <summary>
    /// 画面外インジケーターを初期化.
    /// Addressablesから"EnemyOffScreenIndicator"プレハブを読み込み、Canvas下にインスタンス化する.
    /// プレハブが未登録の場合はスキップする.
    /// </summary>
    private async UniTaskVoid InitializeOffScreenIndicator()
    {
        try
        {
            // Canvas を検索.
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[EnemyPresenter_abstract] Canvas が見つかりません。画面外インジケーターをスキップ.");
                return;
            }

            // Addressableキーの存在チェック（InvalidKeyExceptionのログ出力を回避）.
            var locHandle = UnityEngine.AddressableAssets.Addressables.LoadResourceLocationsAsync("EnemyOffScreenIndicator");
            var locations = await locHandle;
            UnityEngine.AddressableAssets.Addressables.Release(locHandle);
            if (locations == null || locations.Count == 0)
            {
                return;
            }

            var handle = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("EnemyOffScreenIndicator");
            GameObject prefab = await handle;

            if (handle.Status != UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
            {
                UnityEngine.AddressableAssets.Addressables.Release(handle);
                return;
            }

            GameObject indicatorObj = Instantiate(prefab, canvas.transform);
            offScreenIndicator = indicatorObj.GetComponent<EnemyOffScreenIndicator>();
            if (offScreenIndicator != null)
            {
                offScreenIndicator.Initialize(transform, canvas);
            }

            UnityEngine.AddressableAssets.Addressables.Release(handle);
        }
        catch (System.Exception)
        {
            // Addressable未登録の場合はエラーを出さずにスキップ.
        }
    }

    /// <summary>
    /// EnemyUIViewのsetterが準備できたらEnemyNameをセット.
    /// </summary>
    private async UniTaskVoid WaitAndSetEnemyName()
    {
        // EnemyManagerが先にEnemyUIViewを準備しているので、直接取得を試みる.
        view = InGame.Enemy.EnemyManager.Instance().EnemyUIView;

        // 万が一まだ準備されていない場合はシーン探索 + 待機.
        if (view == null)
        {
            await UniTask.WaitUntil(() =>
            {
                view = Object.FindFirstObjectByType<EnemyUIView>();
                return view != null;
            });
        }

        if (!view.IsSetterReady)
        {
            await UniTask.WaitUntil(() => view.IsSetterReady);
        }

        view.SetEnemyName(EnemyName);
        view.EnableEnemyUI();
        Debug.Log($"[EnemyPresenter_abstract] EnemyName設定完了 - {EnemyName}");

        // HP変化時にhpPercentを更新.
        if (status != null)
        {
            status.hp
                .Subscribe(hp =>
                {
                    float percent = CalculateHpPercent(hp, status.maxhp);
                    view.SetHpGauge(percent);
                })
                .AddTo(this);
            Debug.Log($"[EnemyPresenter_abstract] HP購読設定完了");
        }
    }

    /// <summary>HP変化時のバーパーセント計算（子クラスでoverride可能）.</summary>
    protected virtual float CalculateHpPercent(float currentHp, float maxHp)
    {
        return currentHp / maxHp;
    }

    // 派生クラスでmodel/statusのAddComponentを行う.
    protected abstract void InitComponents();

    private void OnDestroy()
    {
        // SEリソース解放.
        if (sePlayer != null)
        {
            sePlayer.ReleaseAll();
            Destroy(sePlayer.gameObject);
            sePlayer = null;
        }
        seRegistry?.Clear();
        seRegistry = null;

        // 画面外インジケーター破棄.
        if (offScreenIndicator != null)
        {
            Destroy(offScreenIndicator.gameObject);
            offScreenIndicator = null;
        }
    }
}
