using UnityEngine;
using Cysharp.Threading.Tasks;

// Wendig用 とびかかり切りState.
// プレイヤーが高い位置にいる場合にジャンプで飛びかかり、着地時に攻撃する.
// 空中でプレイヤーが近くに来たら近接攻撃も行う.
public class EnemState_Wendig_JumpSlash : EnemState_abstract
{
    // とびかかり切り: 1.5倍.
    private float attackMultiplier = 1.5f;
    private int jumpSlashDamage = 75;

    // ヒット処理.
    private EnemColliderState_Wendig_JumpSlash colliderState = new EnemColliderState_Wendig_JumpSlash();

    // ジャンプ設定.
    private float jumpHeight = 2.5f;            // ジャンプの高さ（放物線軌道用に低め）.
    private float landingOffsetX = 0.5f;        // プレイヤー横のオフセット（近くに着地）.
    private float airMeleeRange = 2.5f;         // 空中近接攻撃の射程.
    private float groundCheckDistance = 0.3f;    // 着地判定レイキャスト距離.

    // 後退設定（X軸が近い場合）.
    private float retreatThresholdX = 3f;       // 後退を行うX距離の閾値.
    private float retreatDistance = 3f;          // 後退距離.
    private float retreatDuration = 0.4f;       // 後退にかける時間（秒）.

    // ライフサイクル間共有データ.
    private Rigidbody2D rb;
    private Transform ownerTransform;
    private Animator animator;
    private Collider2D mainColl;
    private RigidbodyConstraints2D originalConstraints;
    private bool originalIsTrigger;
    private bool stateModified = false;
    private int originalLayer = -1;

    // ターゲット位置（AI側から設定）.
    private Vector3 targetPosition;

    // 着地判定用レイヤーマスク.
    private int groundLayerMask = -1;

    public EnemState_Wendig_JumpSlash()
    {
        postActionWaitFrames = 90; // 1.5秒クールダウン.
    }

    // ターゲット位置を設定（AI側から呼び出し）.
    public void SetTargetPosition(Vector3 pos)
    {
        targetPosition = pos;
    }

    protected override async UniTask OnPreAction(EnemyModel_abstract enemyModel)
    {
        stateModified = false;

        if (!EnemNullSafetyHelper.IsValidWithAnimator(enemyModel)) { isAborted = true; return; }

        // 現在の攻撃力から実ダメージを計算.
        if (enemyModel is EnemyModel_Wendig wendigModel)
        {
            jumpSlashDamage = (int)(wendigModel.GetCurrentAttackPower() * attackMultiplier);
        }

        rb = enemyModel.Rigidbody;
        ownerTransform = enemyModel.Presenter.transform;
        animator = enemyModel.Animator;
        mainColl = enemyModel.Presenter.MainColl;
        float animSpeed = enemyModel.AnimSpeed;

        if (rb == null || ownerTransform == null) { isAborted = true; return; }

        // レイヤーマスク初期化（Ground + Platform）.
        if (groundLayerMask == -1)
        {
            int platformLayer = LayerMask.NameToLayer("Platform");
            groundLayerMask = (1 << platformLayer) | (1 << LayerMask.NameToLayer("Default"));
        }

        // 元の状態を保存.
        originalConstraints = rb.constraints;
        originalIsTrigger = mainColl != null ? mainColl.isTrigger : false;

        // プレイヤーの方を向く.
        EnemFacingHelper.FaceToward(ownerTransform, targetPosition);

        // X軸が近い場合は後退してからジャンプ.
        float absDistX = Mathf.Abs(ownerTransform.position.x - targetPosition.x);
        if (absDistX < retreatThresholdX)
        {
            // プレイヤーと反対方向に後退.
            float retreatDirX = ownerTransform.position.x < targetPosition.x ? -1f : 1f;
            float retreatSpeed = retreatDistance / retreatDuration;
            float retreatElapsed = 0f;

            while (retreatElapsed < retreatDuration)
            {
                if (!EnemNullSafetyHelper.IsValid(enemyModel)) { isAborted = true; return; }
                rb.linearVelocity = new Vector2(retreatDirX * retreatSpeed, rb.linearVelocity.y);
                retreatElapsed += Time.deltaTime;
                await UniTask.Yield();
            }
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

            // 後退後、再度プレイヤーの方を向く.
            EnemFacingHelper.FaceToward(ownerTransform, targetPosition);
        }

        // ヒット対象リストをクリア.
        colliderState.ClearHitTargets();
        colliderState.SetDamage(jumpSlashDamage);

        // アニメーション: "Jump"トリガー.
        animator.ResetTrigger("EndJump");
        animator.SetTrigger("Jump");

        // 攻撃通告: パリィ不可.
        if (!await EnemAttackPhaseHelper.PlayAttackPremonition(
            enemyModel, 400f, false, 300f, animSpeed)) { isAborted = true; return; }

        if (!EnemNullSafetyHelper.IsValid(enemyModel)) { isAborted = true; return; }

        // ジャンプ中はコライダーをトリガーに設定（Platformをすり抜けるため）.
        if (mainColl != null)
        {
            mainColl.isTrigger = true;
        }
        // FixedUpdateのPlatform落下制御をスキップ（isTrigger中でも安全にジャンプする）.
        enemyModel.IsJumping = true;
        stateModified = true;

        // ジャンプ速度を計算.
        // ターゲット: プレイヤーの少し横.
        float dirSign = ownerTransform.position.x < targetPosition.x ? 1f : -1f;
        float targetX = targetPosition.x - dirSign * landingOffsetX; // プレイヤーの手前側に着地.
        float targetY = targetPosition.y;

        float deltaX = targetX - ownerTransform.position.x;
        float deltaY = targetY - ownerTransform.position.y;

        // 重力加速度（Unity 2Dのデフォルト: Physics2D.gravity.y * rb.gravityScale）.
        float gravity = Mathf.Abs(Physics2D.gravity.y * rb.gravityScale);
        if (gravity < 0.1f) gravity = 9.81f; // フォールバック.

        // ジャンプの頂点を計算: 目標高さ + jumpHeight.
        float peakY = Mathf.Max(deltaY, 0f) + jumpHeight;

        // 上昇初速度: v_y = sqrt(2 * g * peakY).
        float vy = Mathf.Sqrt(2f * gravity * peakY);

        // 空中時間: 上昇 + 下降.
        // 上昇時間: t_up = v_y / g.
        float tUp = vy / gravity;
        // 下降高さ: peakY - deltaY（頂点から着地位置までの高さ）.
        float fallHeight = peakY - deltaY;
        // 下降時間: t_down = sqrt(2 * fallHeight / g).
        float tDown = Mathf.Sqrt(2f * Mathf.Max(fallHeight, 0f) / gravity);
        float totalAirTime = tUp + tDown;

        // 水平速度: v_x = deltaX / totalAirTime.
        float vx = totalAirTime > 0.01f ? deltaX / totalAirTime : 0f;

        // Y軸フリーズを解除（ジャンプ中は重力が必要）.
        rb.constraints = originalConstraints & ~RigidbodyConstraints2D.FreezePositionY;

        // 初速度設定.
        rb.linearVelocity = new Vector2(vx, vy);
    }

    protected override async UniTask OnAction(EnemyModel_abstract enemyModel)
    {
        if (!EnemNullSafetyHelper.IsValid(enemyModel)) { isAborted = true; return; }

        bool airMeleeUsed = false;
        bool hasLanded = false;

        // ジャンプ中はEnemyレイヤーを維持（PlatformBreakableが検出できるように）.
        // SetAttackLayerは攻撃コライダーフェーズのみで使用する.

        // 空中フェーズ: 着地するまでループ.
        float airTimeElapsed = 0f;
        float maxAirTime = 5f; // 安全タイムアウト.

        while (!hasLanded && airTimeElapsed < maxAirTime)
        {
            if (!EnemNullSafetyHelper.IsValid(enemyModel)) { isAborted = true; return; }

            airTimeElapsed += Time.deltaTime;

            // 空中近接攻撃チェック（1回のみ）.
            if (!airMeleeUsed)
            {
                // プレイヤーとの距離を毎フレームチェック.
                var playerScope = Object.FindFirstObjectByType<InGame.Player.PlayerScope>();
                if (playerScope != null)
                {
                    float distToPlayer = Vector2.Distance(ownerTransform.position, playerScope.transform.position);
                    if (distToPlayer <= airMeleeRange)
                    {
                        airMeleeUsed = true;

                        // プレイヤーの方を向く.
                        EnemFacingHelper.FaceToward(ownerTransform, playerScope.transform.position);

                        // 攻撃アニメーション再生.
                        animator.ResetTrigger("Attack_End");
                        animator.SetTrigger("Attack");

                        // 空中攻撃中のみレイヤーを変更.
                        originalLayer = EnemColliderHelper.SetAttackLayer(ownerTransform);

                        // 空中近接攻撃コライダー発動（短時間）.
                        colliderState.ClearHitTargets();
                        colliderState.SetDamage(jumpSlashDamage);
                        await EnemColliderHelper.ExecuteColliderPhase(
                            enemyModel,
                            new EnemColliderHelper.ColliderPhaseConfig
                            {
                                colliderType = EnemColliderType.Box,
                                offset = new Vector2(-0.1f, 0f),
                                size = new Vector2(0.5f, 2f),
                                damage = jumpSlashDamage,
                                duration = 0.3f,
                                colliderState = colliderState
                            },
                            300f,
                            enemyModel.AnimSpeed);

                        // 空中攻撃終了: レイヤーを復元.
                        EnemColliderHelper.RestoreLayer(ownerTransform, originalLayer);
                        originalLayer = -1;

                        // 攻撃アニメーション終了.
                        animator.ResetTrigger("Attack");
                        animator.SetTrigger("Attack_End");
                    }
                }
            }

            // 着地判定.
            if (rb.linearVelocity.y <= 0.1f && airTimeElapsed > 0.2f)
            {
                hasLanded = CheckGrounded(enemyModel);
            }

            await UniTask.Yield();
        }

        if (!EnemNullSafetyHelper.IsValid(enemyModel)) { isAborted = true; return; }

        // 着地処理.
        Debug.Log($"[JumpSlash] 着地検出 - hasLanded: {hasLanded}, airTime: {airTimeElapsed:F2}s, pos: {ownerTransform.position}");
        rb.linearVelocity = Vector2.zero;

        // 着地アニメーション終了トリガー.
        if (EnemNullSafetyHelper.IsValidWithAnimator(enemyModel))
        {
            Debug.Log("[JumpSlash] EndJump トリガー発火");
            animator.SetTrigger("EndJump");
        }

        // 着地位置をスナップ（isTrigger中は物理衝突しないため手動で地面に合わせる）.
        SnapToGround(enemyModel);

        // ジャンプ終了: FixedUpdateのPlatform制御を復帰.
        enemyModel.IsJumping = false;

        // コライダーのトリガーを解除（着地したので）.
        if (mainColl != null)
        {
            mainColl.isTrigger = originalIsTrigger;
        }

        // 着地攻撃: レイヤーを変更してコライダー発動.
        originalLayer = EnemColliderHelper.SetAttackLayer(ownerTransform);
        colliderState.ClearHitTargets();
        colliderState.SetDamage(jumpSlashDamage);

        await EnemColliderHelper.ExecuteColliderPhaseUntil(
            enemyModel,
            new EnemColliderHelper.ColliderPhaseConfig
            {
                colliderType = EnemColliderType.Circle,
                offset = Vector2.zero,
                radius = 1.5f,
                damage = jumpSlashDamage,
                duration = 0.5f,
                colliderState = colliderState
            },
            () =>
            {
                if (!EnemNullSafetyHelper.IsValidWithAnimator(enemyModel)) return true;
                var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                // Jumpアニメーションが終了したら終了.
                return !stateInfo.IsName("Jump") || stateInfo.normalizedTime >= 1f;
            });
    }

    protected override async UniTask OnAfterPostAction(EnemyModel_abstract enemyModel)
    {
        // 元の状態を復元（常に実行 — クリーンアップ保証）.
        ClearJumpingFlag(enemyModel);
        RestoreState();

        if (EnemNullSafetyHelper.IsValidWithAnimator(enemyModel))
        {
            animator.ResetTrigger("Jump");
            animator.SetTrigger("EndJump");
        }
        await UniTask.CompletedTask;
    }

    // 着地判定: Collider下端から短距離レイキャストで地面を検出.
    private bool CheckGrounded(EnemyModel_abstract enemyModel)
    {
        if (rb == null || ownerTransform == null) return false;

        Vector2 feetPos = GetFeetPosition(enemyModel);

        // Platform/Defaultレイヤーに対して下方向レイキャスト.
        RaycastHit2D hit = Physics2D.Raycast(feetPos, Vector2.down, groundCheckDistance, groundLayerMask);
        return hit.collider != null;
    }

    // 着地時に地面表面へ位置をスナップ（isTrigger中は物理衝突しないため手動補正）.
    private void SnapToGround(EnemyModel_abstract enemyModel)
    {
        if (rb == null || ownerTransform == null) return;

        Vector2 feetPos = GetFeetPosition(enemyModel);

        // やや長めのレイキャストで地面を検出.
        RaycastHit2D hit = Physics2D.Raycast(feetPos + Vector2.up * 0.5f, Vector2.down, 1.5f, groundLayerMask);
        if (hit.collider != null)
        {
            // 足元を地面表面に合わせる.
            float feetOffset = feetPos.y - ownerTransform.position.y;
            ownerTransform.position = new Vector3(ownerTransform.position.x, hit.point.y - feetOffset, ownerTransform.position.z);
        }
    }

    // 足元位置を取得.
    private Vector2 GetFeetPosition(EnemyModel_abstract enemyModel)
    {
        Collider2D col = enemyModel.GetComponent<Collider2D>();
        if (col != null)
        {
            return new Vector2(col.bounds.center.x, col.bounds.min.y);
        }
        return (Vector2)ownerTransform.position;
    }

    // 元の状態を復元するヘルパーメソッド.
    private void RestoreState()
    {
        // レイヤー復元.
        if (originalLayer >= 0)
        {
            EnemColliderHelper.RestoreLayer(ownerTransform, originalLayer);
            originalLayer = -1;
        }
        if (!stateModified) return;
        if (rb != null)
        {
            rb.constraints = originalConstraints;
            rb.linearVelocity = Vector2.zero;
        }
        if (mainColl != null)
        {
            mainColl.isTrigger = originalIsTrigger;
        }
        stateModified = false;
    }

    // EnemyModelのIsJumpingフラグを確実にクリアするヘルパー.
    private void ClearJumpingFlag(EnemyModel_abstract enemyModel)
    {
        if (enemyModel != null)
        {
            enemyModel.IsJumping = false;
        }
    }
}
