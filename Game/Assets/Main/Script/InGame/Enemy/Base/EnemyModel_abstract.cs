using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using InGame.Player;
using InGame.Common;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

// 敵の状態を管理するenum.
public enum EnemyState
{
    None,
    Prepare,    // ゲーム開始前の準備段階.
    Appear,     // 登場時の演出.
    Idle,
    Move,
    Attack,
    Damaged,
    Dead
}
public abstract class EnemyModel_abstract : MonoBehaviour
{
    protected EnemyPresenter_abstract presenter = null;
    public EnemyPresenter_abstract Presenter { get => presenter; set { if( presenter == null) presenter = value; } }

    protected Vector2 stageMin = new Vector2(-13f, 0f);
    protected Vector2 stageMax = new Vector2(13f, 0f);
    public Vector2 StageMin => stageMin;
    public Vector2 StageMax => stageMax;

    protected Animator animator;
    public Animator Animator => animator;
    protected Rigidbody2D rigidbody;
    public Rigidbody2D Rigidbody => rigidbody;
    protected EnemyState currentState = EnemyState.Prepare;

    protected Vector2 moveSpeed = new Vector2(2,0);
    public Vector2 MoveSpeed => moveSpeed;

    // アニメーション速度を取得.
    public float AnimSpeed => animator != null ? animator.speed : 1f;

    protected virtual void Awake()
    {
        Debug.Log($"[EnemyModel_abstract] Awake - {gameObject.name}");
        animator = GetComponent<Animator>();    
        rigidbody = GetComponent<Rigidbody2D>();
        Debug.Log($"[EnemyModel_abstract] Awake完了 - Animator: {(animator != null ? "取得" : "null")}, Rigidbody: {(rigidbody != null ? "取得" : "null")}");
    }

    protected EnemAIModel_abstract AIModel = new EnemAIModel_normal();

    // 登場時の演出開始関数（継承クラスでオーバーライド可能）.
    public virtual async UniTask StartAppearPerformance()
    {
        Debug.Log($"[EnemyModel_abstract] StartAppearPerformance - {gameObject.name}");
        await UniTask.CompletedTask;
    }

    public virtual void EnemAIStart()
    {
        Debug.Log($"[EnemyModel_abstract] EnemAIStart - {gameObject.name}");
        AIModel.SetOwnerTransform(transform);
        AIModel.SetOwnerModel(this);
        AIModel.StartLoop();
        Debug.Log($"[EnemyModel_abstract] EnemAIStart完了 - AIループ開始");
    }

    public virtual void EnemAIStop()
    {
        Debug.Log($"[EnemyModel_abstract] EnemAIStop - {gameObject.name}");
        AIModel.StopLoop();
    }

    public void TestInit()
    {
        Debug.Log($"[EnemyModel_abstract] TestInit - {gameObject.name}");
        EnemAIStart();
    }
}

// 敵AIのアクション設定クラス.
public class EnemAIActionSetting
{
    // 発動するアクションのstate.
    public EnemState_abstract actionState;

    // 繰り返し発動可能な回数（-1で無限）.
    public int repeatableCount = -1;

    // 現在の残り発動回数.
    private int currentRepeatCount;

    // 発動可能な距離（攻撃が当たる範囲）.
    public float activationDistance = 2f;

    // 移動を開始する可能性のある距離.
    public float moveStartDistance = 5f;

    // 発動可能性（重みづけで計算するための値）.
    public float activationWeight = 1f;

    // 移動開始距離が選択された時用の移動state.
    public EnemState_abstract moveState;

    // 発動するかどうか.
    public bool shouldActivate = true;

    // 初期化.
    public void Initialize()
    {
        currentRepeatCount = repeatableCount;
    }

    // 発動可能かチェック.
    public bool CanActivate()
    {
        if (!shouldActivate) return false;
        if (repeatableCount == -1) return true;
        return currentRepeatCount > 0;
    }

    // 発動回数を消費.
    public void ConsumeRepeat()
    {
        if (repeatableCount != -1 && currentRepeatCount > 0)
        {
            currentRepeatCount--;
        }
    }

    // 発動回数をリセット.
    public void ResetRepeatCount()
    {
        currentRepeatCount = repeatableCount;
    }
}

public abstract class EnemAIModel_abstract
{

    // stageの端について取得方法を考える必要あり.
    // 自身の大きさを取得する方法 => 主コライダーの大きさで取るようにする.
    private CancellationTokenSource cts;
    private bool isRunning = false;

    // アクション設定リスト.
    protected System.Collections.Generic.List<EnemAIActionSetting> actionSettings = new System.Collections.Generic.List<EnemAIActionSetting>();

    // 自身のTransform（位置取得用）.
    protected Transform ownerTransform;

    // 自身のEnemyModel（Act呼び出し用）.
    protected EnemyModel_abstract ownerModel;

    // ターゲットオブジェクト（プレイヤー）.
    protected GameObject targetObject;

    // ターゲット位置を取得するプロパティ.
    public Vector3 TargetPosition
    {
        get
        {
            // PlayerScopeからプレイヤーを検索.
            var playerScope = Object.FindFirstObjectByType<PlayerScope>();
            if (playerScope != null)
            {
                targetObject = playerScope.gameObject;
                return targetObject.transform.position;
            }

            // プレイヤーが存在しない場合.
            targetObject = null;

#if UNITY_EDITOR
            // エディタ上ではマウスポインタの位置を追跡.
            Vector3 mouseScreenPos = Input.mousePosition;
            mouseScreenPos.z = 10f; // カメラからの距離.
            if (Camera.main != null)
            {
                return Camera.main.ScreenToWorldPoint(mouseScreenPos);
            }
#endif
            // exe（ビルド）では自身の位置を返す.
            if (ownerTransform != null)
            {
                return ownerTransform.position;
            }

            return Vector3.zero;
        }
    }

    // 自身のTransformを設定.
    public void SetOwnerTransform(Transform transform)
    {
        Debug.Log($"[EnemAIModel_abstract] SetOwnerTransform - Transform: {(transform != null ? transform.name : "null")}");
        ownerTransform = transform;
    }

    // 自身のEnemyModelを設定.
    public void SetOwnerModel(EnemyModel_abstract model)
    {
        Debug.Log($"[EnemAIModel_abstract] SetOwnerModel - Model: {(model != null ? model.gameObject.name : "null")}");
        ownerModel = model;
    }

    // ループを開始.
    public void StartLoop()
    {
        Debug.Log($"[EnemAIModel_abstract] StartLoop - isRunning: {isRunning}");
        if (isRunning) return;
        cts = new CancellationTokenSource();
        isRunning = true;
        Debug.Log($"[EnemAIModel_abstract] AILoop開始");
        AILoop(cts.Token).Forget();
    }

    // ループを停止.
    public void StopLoop()
    {
        Debug.Log($"[EnemAIModel_abstract] StopLoop - isRunning: {isRunning}");
        if (!isRunning) return;
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
        isRunning = false;
        Debug.Log($"[EnemAIModel_abstract] AILoop停止完了");
    }

    public virtual async UniTask AILoop(CancellationToken token)
    {
        Debug.Log($"[EnemAIModel_abstract] AILoop開始 - ownerModel: {(ownerModel != null ? ownerModel.gameObject.name : "null")}");

        // ownerModelとPresenterが設定されるまで待機（最大60フレーム）.
        int waitCount = 0;
        const int maxWaitFrames = 60;
        while ((ownerModel == null || ownerModel.Presenter == null) && waitCount < maxWaitFrames)
        {
            if (token.IsCancellationRequested)
            {
                Debug.Log($"[EnemAIModel_abstract] AILoop - 待機中にキャンセル要求");
                return;
            }
            Debug.Log($"[EnemAIModel_abstract] AILoop - ownerModel/Presenter待機中 ({waitCount}/{maxWaitFrames})");
            await UniTask.Yield(token);
            waitCount++;
        }

        // 待機後もnullの場合は開始しない.
        if (ownerModel == null || ownerModel.Presenter == null)
        {
            Debug.LogError($"[EnemAIModel_abstract] AILoop開始失敗 - ownerModel or Presenter が {maxWaitFrames}フレーム待機後もnull");
            StopLoop();
            return;
        }

        Debug.Log($"[EnemAIModel_abstract] AILoop - 初期化完了、ループ開始");
        int loopCount = 0;
        while (!token.IsCancellationRequested)
        {
            // ownerModelまたはPresenterが破棄されている場合はループを終了.
            if (ownerModel == null || ownerModel.Presenter == null)
            {
                Debug.LogWarning($"[EnemAIModel_abstract] AILoop終了 - ownerModel or Presenter が null (loopCount: {loopCount})");
                StopLoop();
                return;
            }

            try
            {
                await OnAIUpdate(token);
                await UniTask.Yield(token);
                loopCount++;
                if (loopCount % 60 == 0) // 60フレームごとにログ
                {
                    Debug.Log($"[EnemAIModel_abstract] AILoop実行中 - loopCount: {loopCount}");
                }
            }
            catch (System.Exception e) when (e is not System.OperationCanceledException)
            {
                Debug.LogError($"[EnemAIModel_abstract] AILoop例外発生: {e.Message}");
                // MissingReferenceExceptionなどの場合はループを終了.
                if (ownerModel == null || ownerModel.Presenter == null)
                {
                    Debug.LogWarning($"[EnemAIModel_abstract] AILoop終了 - 例外後のnullチェック");
                    StopLoop();
                    return;
                }
                throw;
            }
        }
        Debug.Log($"[EnemAIModel_abstract] AILoop終了 - キャンセル要求 (loopCount: {loopCount})");
    }

    // 継承クラスでAI処理を実装.
    protected virtual async UniTask OnAIUpdate(CancellationToken token)
    {
        await UniTask.CompletedTask;
    }

    // アクション設定を追加.
    protected void AddActionSetting(EnemAIActionSetting setting)
    {
        setting.Initialize();
        actionSettings.Add(setting);
    }

    // 距離に基づいてアクションを選択（重みづけ）.
    protected EnemAIActionSetting SelectActionByDistance(float distance)
    {
        // 発動距離内のアクションをフィルタリング.
        var activatableActions = new System.Collections.Generic.List<EnemAIActionSetting>();
        float totalWeight = 0f;

        foreach (var setting in actionSettings)
        {
            if (setting.CanActivate() && distance <= setting.activationDistance)
            {
                activatableActions.Add(setting);
                totalWeight += setting.activationWeight;
            }
        }

        if (activatableActions.Count == 0) return null;

        // 重みづけでランダム選択.
        float randomValue = UnityEngine.Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        foreach (var setting in activatableActions)
        {
            currentWeight += setting.activationWeight;
            if (randomValue <= currentWeight)
            {
                return setting;
            }
        }

        return activatableActions[activatableActions.Count - 1];
    }

    // 移動開始距離内のアクションを選択.
    protected EnemAIActionSetting SelectMoveActionByDistance(float distance)
    {
        // 移動開始距離内のアクションをフィルタリング.
        var moveActions = new System.Collections.Generic.List<EnemAIActionSetting>();
        float totalWeight = 0f;

        foreach (var setting in actionSettings)
        {
            if (setting.CanActivate() &&
                distance > setting.activationDistance &&
                distance <= setting.moveStartDistance &&
                setting.moveState != null)
            {
                moveActions.Add(setting);
                totalWeight += setting.activationWeight;
            }
        }

        if (moveActions.Count == 0) return null;

        // 重みづけでランダム選択.
        float randomValue = UnityEngine.Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        foreach (var setting in moveActions)
        {
            currentWeight += setting.activationWeight;
            if (randomValue <= currentWeight)
            {
                return setting;
            }
        }

        return moveActions[moveActions.Count - 1];
    }
}

public class EnemAIModel_normal : EnemAIModel_abstract
{

}

public abstract class EnemAISetUpStatus_abstract
{

}

public abstract class EnemState_abstract
{
    protected EnemyState stateType = EnemyState.None;
    public EnemyState StateType => stateType;

    //(既)修:　EnemyModel_abstractを継承したclassを引数に設定できるようにしてください
    public virtual async UniTask Act(EnemyModel_abstract enemyModel)
    {
        Debug.Log($"[EnemState_abstract] Act - StateType: {stateType}");
        await UniTask.CompletedTask;
    }
}

/// <summary> コライダーの種類 </summary>
public enum EnemColliderType
{
    Box,
    Circle,
    Capsule
}

/// <summary> 個別コライダーの設定 </summary>
[System.Serializable]
public class EnemColliderSetting
{
    // コライダーの種類.
    public EnemColliderType colliderType = EnemColliderType.Box;

    // 位置オフセット.
    public Vector2 offset = Vector2.zero;

    // サイズ（BoxとCapsule用）.
    private Vector2 _size = new Vector2(1f, 1f);
    public Vector2 size
    {
        get => _size;
        set
        {
            Debug.Log($"[EnemColliderSetting] size設定: {value}");
            _size = value;
        }
    }

    // 半径（Circle用）.
    public float radius = 0.5f;

    // カプセルの方向（Capsule用）.
    public CapsuleDirection2D capsuleDirection = CapsuleDirection2D.Vertical;

    // 明示的にサイズを設定するコンストラクタ.
    public EnemColliderSetting() { }

    public EnemColliderSetting(EnemColliderType type, Vector2 offset, Vector2 size)
    {
        this.colliderType = type;
        this.offset = offset;
        this._size = size;
        Debug.Log($"[EnemColliderSetting] コンストラクタ - Type: {type}, Offset: {offset}, Size: {size}");
    }
}

/// <summary> 攻撃全体のコライダー設定（複数コライダーを一度のヒットとして扱う） </summary>
public class EnemColliderStatus
{
    // 親Transform（コライダーを追加する対象）.
    public Transform parentTransform;

    //(既)修:コライダーのサイズ設定が行われるようにしてください
    // 個別コライダー設定のリスト.
    public System.Collections.Generic.List<EnemColliderSetting> colliderSettings = new System.Collections.Generic.List<EnemColliderSetting>();

    // 当たり判定の持続時間.
    public float duration = 0.5f;

    // ダメージ量.
    public float damage = 10f;

    // 生成されたコライダーのリスト.
    private System.Collections.Generic.List<Collider2D> createdColliders = new System.Collections.Generic.List<Collider2D>();

    // コライダーを生成（親に直接追加）.
    public System.Collections.Generic.List<Collider2D> CreateColliders()
    {
        if (parentTransform == null)
        {
            Debug.LogWarning($"[EnemColliderStatus] CreateColliders - parentTransform が null");
            return createdColliders;
        }

        Debug.Log($"[EnemColliderStatus] CreateColliders - 設定数: {colliderSettings.Count}, Parent: {parentTransform.name}");

        foreach (var setting in colliderSettings)
        {
            Debug.Log($"[EnemColliderStatus] Setting処理 - Type: {setting.colliderType}, Size: {setting.size}, Offset: {setting.offset}");

            Collider2D collider = null;

            switch (setting.colliderType)
            {
                case EnemColliderType.Box:
                    var boxCollider = parentTransform.gameObject.AddComponent<BoxCollider2D>();
                    boxCollider.offset = setting.offset;
                    boxCollider.size = setting.size;
                    boxCollider.isTrigger = true;
                    collider = boxCollider;
                    Debug.Log($"[EnemColliderStatus] BoxCollider2D追加完了 - Size: {boxCollider.size}, Offset: {boxCollider.offset}, isTrigger: {boxCollider.isTrigger}");
                    break;

                case EnemColliderType.Circle:
                    var circleCollider = parentTransform.gameObject.AddComponent<CircleCollider2D>();
                    circleCollider.offset = setting.offset;
                    circleCollider.radius = setting.radius;
                    circleCollider.isTrigger = true;
                    collider = circleCollider;
                    Debug.Log($"[EnemColliderStatus] CircleCollider2D追加 - Offset: {setting.offset}, Radius: {setting.radius}");
                    break;

                case EnemColliderType.Capsule:
                    var capsuleCollider = parentTransform.gameObject.AddComponent<CapsuleCollider2D>();
                    capsuleCollider.offset = setting.offset;
                    capsuleCollider.size = setting.size;
                    capsuleCollider.direction = setting.capsuleDirection;
                    capsuleCollider.isTrigger = true;
                    collider = capsuleCollider;
                    Debug.Log($"[EnemColliderStatus] CapsuleCollider2D追加 - Offset: {setting.offset}, Size: {setting.size}");
                    break;
            }

            if (collider != null)
            {
                collider.hideFlags = HideFlags.HideAndDontSave;
                createdColliders.Add(collider);
                Debug.Log($"[EnemColliderStatus] コライダー追加成功 - 総数: {createdColliders.Count}");
            }
        }

        Debug.Log($"[EnemColliderStatus] CreateColliders完了 - 生成数: {createdColliders.Count}");
        return createdColliders;
    }

    // 生成したコライダーを削除.
    public void DestroyColliders()
    {
        Debug.Log($"[EnemColliderStatus] DestroyColliders - 削除数: {createdColliders.Count}");

        foreach (var collider in createdColliders)
        {
            if (collider != null)
            {
                Object.Destroy(collider);
            }
        }
        createdColliders.Clear();
    }

    // 指定されたコライダーがこのステータスで生成されたものかチェック.
    public bool ContainsCollider(Collider2D collider)
    {
        return createdColliders.Contains(collider);
    }
}

/// <summary> 命中時の処理 </summary>
public abstract class EnemColliderState_abstract
{
    // ヒット済みの対象を記録（同一対象への重複処理を防ぐ）.
    protected System.Collections.Generic.HashSet<GameObject> hitTargets = new System.Collections.Generic.HashSet<GameObject>();

    // コライダーステータス.
    protected EnemColliderStatus colliderStatus;

    // ダメージ量.
    protected int damage = 10;

    // 吹き飛ばしの力（howlingと突進は10倍、それ以外は3倍）.
    protected float knockbackForce = 3f;

    // 攻撃のPowerlevel（デフォルトは通常近接攻撃）.
    protected int powerlevel = PowerlevelConst.EnemyMeleeAttack;

    // 攻撃者のTransform（エネミーの向き判定用）.
    protected Transform attackerTransform;

    // 攻撃者のTransformを設定.
    public void SetAttackerTransform(Transform t)
    {
        attackerTransform = t;
    }

    // コライダーステータスを設定.
    public void SetColliderStatus(EnemColliderStatus status)
    {
        Debug.Log($"[EnemColliderState_abstract] SetColliderStatus - Damage: {status.damage}, ColliderCount: {status.colliderSettings.Count}");
        colliderStatus = status;
        damage = (int)status.damage;
    }

    // ダメージを設定.
    public void SetDamage(int dmg)
    {
        damage = dmg;
    }

    // ヒット対象リストをクリア（攻撃開始時に呼び出す）.
    public void ClearHitTargets()
    {
        Debug.Log($"[EnemColliderState_abstract] ClearHitTargets - 以前のヒット数: {hitTargets.Count}");
        hitTargets.Clear();
    }

    // ヒット処理（当たり判定のいずれかに当たった時に呼ばれる）.
    public bool TryProcessHit(GameObject target, Collider2D hitCollider)
    {
        if (target == null)
        {
            Debug.LogWarning($"[EnemColliderState_abstract] TryProcessHit - target が null");
            return false;
        }

        // 既にヒット済みの対象はスキップ.
        if (hitTargets.Contains(target))
        {
            Debug.Log($"[EnemColliderState_abstract] TryProcessHit - {target.name} は既にヒット済み、スキップ");
            return false;
        }

        // ヒット済みリストに追加.
        hitTargets.Add(target);
        Debug.Log($"[EnemColliderState_abstract] TryProcessHit - {target.name} をヒット済みリストに追加");

        // 実際のヒット処理を実行.
        OnHit(target, hitCollider);
        return true;
    }

    // 継承クラスで実際のヒット処理を実装.
    protected virtual void OnHit(GameObject target, Collider2D hitCollider)
    {
        Debug.Log($"[EnemColliderState_abstract] OnHit - Target: {target.name}, Collider: {hitCollider.name}");
    }

    // プレイヤーにダメージを与える共通処理.
    protected GuardState DamagePlayer(GameObject target, int attackDamage)
    {
        var playerScope = target.GetComponent<PlayerScope>();
        if (playerScope == null)
        {
            Debug.Log($"[EnemColliderState_abstract] DamagePlayer - PlayerScopeが見つからない: {target.name}");
            return GuardState.None;
        }

        Debug.Log($"[EnemColliderState_abstract] DamagePlayer - プレイヤーヒット確認 name={target.name}");

        // ガード状態を事前に確認.
        bool isGuarding = playerScope.playerControllModel != null;
        Debug.Log($"[EnemColliderState_abstract] DamagePlayer - プレイヤー状態確認 playerControllModel存在={isGuarding}");

        // エネミーが向いている方向を算出.
        float knockbackDirX = 1f;
        if (attackerTransform != null)
        {
            knockbackDirX = attackerTransform.localScale.x >= 0 ? -1f : 1f;
        }

        // ダメージ処理（DamageDataに吹き飛ばし力と方向を含めて渡す）.
        var damageData = new DamageData(attackDamage, powerlevel, knockbackForce, knockbackDirX);
        GuardState guardState = playerScope.OnReceiveAttack(damageData);
        Debug.Log($"[EnemColliderState_abstract] DamagePlayer - ダメージ処理結果 ガード状態={guardState}, 与ダメージ={attackDamage}, 吹き飛ばし力={knockbackForce}, 方向={knockbackDirX}");

        // 実際のダメージ量を計算.
        int actualDamage = guardState == GuardState.Parry ? 0 : (guardState == GuardState.Guard ? attackDamage / 2 : attackDamage);
        Debug.Log($"[EnemColliderState_abstract] DamagePlayer - 実ダメージ={actualDamage}");

        return guardState;
    }
}

// EnemColliderState_abstract を継承した None クラス.
public class EnemColliderState_None : EnemColliderState_abstract
{
    protected override void OnHit(GameObject target, Collider2D hitCollider)
    {
        // 何もしない.
        Debug.Log($"[EnemColliderState_None] OnHit - Target: {target.name} (処理なし)");
    }
}

// プレイヤーダメージ用の基本クラス.
public class EnemColliderState_PlayerDamage : EnemColliderState_abstract
{
    protected override void OnHit(GameObject target, Collider2D hitCollider)
    {
        Debug.Log($"[EnemColliderState_PlayerDamage] OnHit - Target: {target.name}, Damage: {damage}");
        DamagePlayer(target, damage);
    }
}

// 割り込みState抽象クラス（Dead, Stanなど通常のAIループを中断するState用）.
public abstract class EnemInterruptState_abstract
{
    protected EnemyState stateType = EnemyState.None;
    public EnemyState StateType => stateType;

    // 割り込み優先度（高いほど優先）.
    protected int priority = 0;
    public int Priority => priority;

    // 割り込みStateを実行.
    public virtual async UniTask Act(EnemyModel_abstract enemyModel)
    {
        Debug.Log($"[EnemInterruptState_abstract] Act - StateType: {stateType}");
        await UniTask.CompletedTask;
    }
}

// Dead割り込みState抽象クラス.
public abstract class EnemInterruptState_Dead_abstract : EnemInterruptState_abstract
{
    protected float deathAnimationDelay = 2f;

    public EnemInterruptState_Dead_abstract()
    {
        stateType = EnemyState.Dead;
        priority = 100; // Deadは最高優先度.
    }

    public override async UniTask Act(EnemyModel_abstract enemyModel)
    {
        Debug.Log($"[EnemInterruptState_Dead_abstract] Act開始");

        if (enemyModel == null || enemyModel.Animator == null)
        {
            Debug.LogWarning($"[EnemInterruptState_Dead_abstract] Act中断 - enemyModel or Animator が null");
            return;
        }

        // Deadアニメーショントリガー実行.
        enemyModel.Animator.SetTrigger("Dead");
        Debug.Log($"[EnemInterruptState_Dead_abstract] Dead トリガー実行");

        // 2秒待機.
        await UniTask.Delay((int)(deathAnimationDelay * 1000));

        // 継承クラスで実装する死亡後処理.
        await OnDeathComplete(enemyModel);

        Debug.Log($"[EnemInterruptState_Dead_abstract] Act完了");
    }

    // 死亡後の処理（継承クラスでオーバーライド）.
    protected virtual async UniTask OnDeathComplete(EnemyModel_abstract enemyModel)
    {
        await UniTask.CompletedTask;
    }
}

// Stan(スタン)割り込みState抽象クラス.
public abstract class EnemInterruptState_Stan_abstract : EnemInterruptState_abstract
{
    protected string stanBoolName = "Stan";
    protected float stanDuration = 2f;

    public EnemInterruptState_Stan_abstract()
    {
        stateType = EnemyState.Damaged;
        priority = 50; // Stanの優先度.
    }

    public override async UniTask Act(EnemyModel_abstract enemyModel)
    {
        Debug.Log($"[EnemInterruptState_Stan_abstract] Act開始");

        if (enemyModel == null || enemyModel.Animator == null)
        {
            Debug.LogWarning($"[EnemInterruptState_Stan_abstract] Act中断 - enemyModel or Animator が null");
            return;
        }

        // Stan開始：アニメーションSetBool true.
        enemyModel.Animator.SetBool(stanBoolName, true);
        Debug.Log($"[EnemInterruptState_Stan_abstract] {stanBoolName} = true 設定");

        // スタン処理（継承クラスでオーバーライド可能）.
        await OnStanProcess(enemyModel);

        // Stan終了：アニメーションSetBool false.
        if (enemyModel != null && enemyModel.Animator != null)
        {
            enemyModel.Animator.SetBool(stanBoolName, false);
            Debug.Log($"[EnemInterruptState_Stan_abstract] {stanBoolName} = false 設定");
        }

        Debug.Log($"[EnemInterruptState_Stan_abstract] Act完了");
    }

    // スタン中の処理（継承クラスでオーバーライド）.
    protected virtual async UniTask OnStanProcess(EnemyModel_abstract enemyModel)
    {
        // 未実装（協議中）.
        await UniTask.CompletedTask;
    }
}

// Howling(遠吠え)割り込みState抽象クラス.
public abstract class EnemInterruptState_Howling_abstract : EnemInterruptState_abstract
{
    public EnemInterruptState_Howling_abstract()
    {
        stateType = EnemyState.Attack;
        priority = 30; // Howlingの優先度.
    }

    public override async UniTask Act(EnemyModel_abstract enemyModel)
    {
        Debug.Log($"[EnemInterruptState_Howling_abstract] Act開始");

        if (enemyModel == null || enemyModel.Animator == null)
        {
            Debug.LogWarning($"[EnemInterruptState_Howling_abstract] Act中断 - enemyModel or Animator が null");
            return;
        }

        // Howling開始：アニメーショントリガー実行.
        enemyModel.Animator.SetTrigger("Howling");
        Debug.Log($"[EnemInterruptState_Howling_abstract] Howling トリガー実行");

        // Howling処理（継承クラスでオーバーライド可能）.
        await OnHowlingProcess(enemyModel);

        // Howling終了：アニメーショントリガー実行.
        if (enemyModel != null && enemyModel.Animator != null)
        {
            enemyModel.Animator.SetTrigger("Howling_End");
            Debug.Log($"[EnemInterruptState_Howling_abstract] Howling_End トリガー実行");
        }

        Debug.Log($"[EnemInterruptState_Howling_abstract] Act完了");
    }

    // Howling中の処理（継承クラスでオーバーライド）.
    protected virtual async UniTask OnHowlingProcess(EnemyModel_abstract enemyModel)
    {
        await UniTask.CompletedTask;
    }
}

// TripleAttack(三連撃)割り込みState抽象クラス.
public abstract class EnemInterruptState_TripleAttack_abstract : EnemInterruptState_abstract
{
    public EnemInterruptState_TripleAttack_abstract()
    {
        stateType = EnemyState.Attack;
        priority = 40; // TripleAttackの優先度.
    }

    public override async UniTask Act(EnemyModel_abstract enemyModel)
    {
        Debug.Log($"[EnemInterruptState_TripleAttack_abstract] Act開始");

        if (enemyModel == null || enemyModel.Animator == null)
        {
            Debug.LogWarning($"[EnemInterruptState_TripleAttack_abstract] Act中断 - enemyModel or Animator が null");
            return;
        }

        // TripleAttack処理（継承クラスでオーバーライド）.
        await OnTripleAttackProcess(enemyModel);

        Debug.Log($"[EnemInterruptState_TripleAttack_abstract] Act完了");
    }

    // TripleAttack処理（継承クラスでオーバーライド）.
    protected virtual async UniTask OnTripleAttackProcess(EnemyModel_abstract enemyModel)
    {
        await UniTask.CompletedTask;
    }
}
