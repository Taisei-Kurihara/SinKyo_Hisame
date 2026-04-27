using Audio;
using Cysharp.Threading.Tasks;
using InGame;
using R3;
using System.Threading;
using UnityEngine;


public abstract class EnemyPresenter_abstract : MonoBehaviour
{
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

    // SE再生用.
    protected SEClipRegistry seRegistry;
    protected SEPlayer sePlayer;

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
    /// SE再生.
    /// </summary>
    public void PlaySE(string actionName)
    {
        if (sePlayer != null && seRegistry != null)
        {
            sePlayer.PlayByAction(seRegistry, actionName);
        }
    }

    /// <summary>
    /// 攻撃通告を再生.
    /// </summary>
    /// <param name="isParryable">パリィ可能な攻撃ならtrue.</param>
    public void PlayAttackWarning(bool isParryable)
    {
        if (attackWarningAnimator == null) return;
        attackWarningAnimator.SetTrigger(isParryable ? "Yellow" : "Red");
        // 攻撃前SE再生.
        PlaySE("AttackPre");
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

        // SE初期化.
        InitializeSE().Forget();

        // EnemyUIViewのsetterがnullでなくなったらEnemyNameをセット.
        WaitAndSetEnemyName().Forget();

        // 画面外インジケーター初期化.
        InitializeOffScreenIndicator().Forget();
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
