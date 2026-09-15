using UnityEngine;
using Cysharp.Threading.Tasks;

public abstract class EnemState_abstract
{
    protected EnemyState stateType = EnemyState.None;
    public EnemyState StateType => stateType;

    // 行動後の待機フレーム数（子クラスがコンストラクタ等で設定）.
    protected int postActionWaitFrames = 0;

    /// <summary>行動後の待機フレーム数（外部から一時変更可能）.</summary>
    public int PostActionWaitFrames
    {
        get => postActionWaitFrames;
        set => postActionWaitFrames = value;
    }

    // 中断フラグ: 子がtrueにすると OnAction〜OnPostAction をスキップ.
    // OnAfterPostAction は常に実行される（クリーンアップ保証）.
    protected bool isAborted = false;

    // trueの場合、Act()終了時のIdol状態復帰（Move=0）をスキップする.
    // 独自のアニメーション遷移を管理するState向け.
    protected bool skipPostIdolTransition = false;

    // Template Method（子クラスでoverrideしない）.
    public async UniTask Act(EnemyModel_abstract enemyModel)
    {
        isAborted = false;

        // 1. 行動前.
        await OnPreAction(enemyModel);

        // 2. 行動中.
        if (!isAborted)
            await OnAction(enemyModel);

        // 3. 行動後待機前の処理.
        if (!isAborted)
            await OnPrePostAction(enemyModel);

        // 4. 行動後待機（子override不可）.
        if (!isAborted)
            await OnPostAction(enemyModel);

        // 5. 行動後待機後の処理（常に実行 — クリーンアップ保証）.
        await OnAfterPostAction(enemyModel);

        // Idol状態に復帰（skipPostIdolTransition=trueの場合はスキップ）.
        // 空中状態（IsJumping）ではIdol遷移をスキップ（ジャンプアニメーションを上書きしないため）.
        if (!skipPostIdolTransition && EnemNullSafetyHelper.IsValidWithAnimator(enemyModel))
        {
            if (!enemyModel.IsJumping)
            {
                enemyModel.Animator.SetInteger("Move", 0);
            }
        }

        // 安全策: Act終了時にIsJumpingフラグをリセット（前のStateで未クリアの場合の対策）.
        if (enemyModel != null)
        {
            enemyModel.IsJumping = false;
        }
    }

    // --- ライフサイクルメソッド（子クラスでoverride可） ---

    protected virtual async UniTask OnPreAction(EnemyModel_abstract enemyModel)
    {
        await UniTask.CompletedTask;
    }

    protected virtual async UniTask OnAction(EnemyModel_abstract enemyModel)
    {
        await UniTask.CompletedTask;
    }

    protected virtual async UniTask OnPrePostAction(EnemyModel_abstract enemyModel)
    {
        await UniTask.CompletedTask;
    }

    // 行動後待機（子override不可）.
    private async UniTask OnPostAction(EnemyModel_abstract enemyModel)
    {
        if (postActionWaitFrames <= 0) return;
        if (!EnemNullSafetyHelper.IsValid(enemyModel)) return;

        float animSpeed = enemyModel.AnimSpeed;
        await EnemAttackPhaseHelper.WaitPostAttackFrames(enemyModel, postActionWaitFrames, animSpeed);
    }

    protected virtual async UniTask OnAfterPostAction(EnemyModel_abstract enemyModel)
    {
        await UniTask.CompletedTask;
    }

    // ============================
    // === 着地判定ユーティリティ ===
    // ============================

    // レイキャスト距離設定.
    // 静止時の基本レイキャスト距離（WaitForLandingAsync / IsOnGround 共通）.
    private static float s_groundCheckBaseDistance = 1.5f;
    // 落下速度に乗算する係数（速度 × deltaTime × この値 で動的距離を算出）.
    private static float s_groundCheckSpeedMultiplier = 1.5f;
    // 動的距離に加算するベース値.
    private static float s_groundCheckSpeedBase = 0.5f;

    /// <summary>接地判定の基本レイキャスト距離.</summary>
    public static float GroundCheckBaseDistance
    {
        get => s_groundCheckBaseDistance;
        set => s_groundCheckBaseDistance = value;
    }
    /// <summary>落下速度に乗算する係数.</summary>
    public static float GroundCheckSpeedMultiplier
    {
        get => s_groundCheckSpeedMultiplier;
        set => s_groundCheckSpeedMultiplier = value;
    }
    /// <summary>動的距離に加算するベース値.</summary>
    public static float GroundCheckSpeedBase
    {
        get => s_groundCheckSpeedBase;
        set => s_groundCheckSpeedBase = value;
    }

    // Default レイヤーのみ（Platform 除外）のキャッシュ.
    private static int s_groundOnlyLayerMask = -1;

    /// <summary>
    /// Default レイヤーのみの地面レイヤーマスクを取得（キャッシュ）.
    /// Platform は除外する.
    /// </summary>
    public static int GroundOnlyLayerMask
    {
        get
        {
            if (s_groundOnlyLayerMask == -1)
                s_groundOnlyLayerMask = 1 << LayerMask.NameToLayer("Default");
            return s_groundOnlyLayerMask;
        }
    }

    /// <summary>
    /// 着地待機: 毎フレームレイキャストで地面（Default レイヤー）を検知し、着地したら返す.
    /// - レイキャスト発射位置: transform.position（中心）
    /// - 対象: Default レイヤーのみ（Platform 除外）
    /// - 落下速度に応じた動的レイキャスト距離
    /// - isTrigger を変更しない前提のため位置補正は物理衝突に任せ、速度ゼロ化のみ行う.
    /// </summary>
    /// <returns>true=着地検知、false=タイムアウト.</returns>
    public static async UniTask<bool> WaitForLandingAsync(
        EnemyModel_abstract enemyModel,
        Transform ownerTransform,
        Rigidbody2D rb,
        float timeoutSec = 5f)
    {
        float elapsed = 0f;
        while (elapsed < timeoutSec)
        {
            if (enemyModel == null || ownerTransform == null) return false;

            // 中心から下方向にレイキャスト.
            Vector2 rayOrigin = (Vector2)ownerTransform.position;
            float fallSpeed = rb != null ? Mathf.Abs(rb.linearVelocity.y) : 0f;
            float checkDist = Mathf.Max(s_groundCheckBaseDistance, fallSpeed * Time.deltaTime * s_groundCheckSpeedMultiplier + s_groundCheckSpeedBase);

            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, checkDist, GroundOnlyLayerMask);
            if (hit.collider != null)
            {
                // 着地検知: 速度ゼロ化のみ（位置補正は物理衝突に任せる）.
                if (rb != null) rb.linearVelocity = Vector2.zero;
                return true;
            }

            await UniTask.Yield();
            elapsed += Time.deltaTime;
        }

        // タイムアウト: 速度ゼロ化のみ.
        if (rb != null) rb.linearVelocity = Vector2.zero;
        return false;
    }

    /// <summary>
    /// 現在地面にいるか判定（即時、待機なし）.
    /// Default レイヤーのみ対象（Platform 除外）.
    /// </summary>
    public static bool IsOnGround(Transform ownerTransform)
    {
        if (ownerTransform == null) return true;
        Vector2 rayOrigin = (Vector2)ownerTransform.position;
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, s_groundCheckBaseDistance, GroundOnlyLayerMask);
        return hit.collider != null;
    }
}
