using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;

// Wendig用 突進State.
public class EnemState_Wendig_Rush : EnemState_abstract
{
    // 前進突進: 2.25倍.
    private float rushMultiplier = 2.25f;
    private int rushDamage = 113;

    // ヒット処理.
    private EnemColliderState_Wendig_Rush colliderState = new EnemColliderState_Wendig_Rush();

    private float stageMinX;
    private float stageMaxX;
    private float rushSpeed = 20f;
    private float moveToEdgeSpeed = 12f;

    // ライフサイクル間共有データ.
    private Rigidbody2D rb;
    private Collider2D mainColl;
    private Transform ownerTransform;
    private Animator animator;
    private RigidbodyConstraints2D originalConstraints;
    private bool originalIsTrigger;
    private float oppositeEdgeX;
    private float oppositeDirectionX;
    private float effectiveRushSpeed;
    private bool stateModified = false;
    private int originalLayer = -1;
    private AfterimageEffect afterimageEffect;

    public EnemState_Wendig_Rush()
    {
        postActionWaitFrames = 60;
    }

    public void SetStageEdge(float minX, float maxX)
    {
        stageMinX = minX;
        stageMaxX = maxX;
    }

    protected override async UniTask OnPreAction(EnemyModel_abstract enemyModel)
    {
        stateModified = false;

        if (!EnemNullSafetyHelper.IsValid(enemyModel)) { isAborted = true; return; }

        // 現在の攻撃力から実ダメージを計算.
        if (enemyModel is EnemyModel_Wendig wendigModel)
        {
            rushDamage = (int)(wendigModel.GetCurrentAttackPower() * rushMultiplier);
        }

        rb = enemyModel.Rigidbody;
        ownerTransform = enemyModel.Presenter.transform;
        animator = enemyModel.Animator;
        mainColl = enemyModel.Presenter.MainColl;
        float animSpeed = enemyModel.AnimSpeed;

        // 怒り時は速度2倍.
        float angerSpeedMult = animSpeed > 1f ? 2f : 1f;
        effectiveRushSpeed = rushSpeed * angerSpeedMult;
        float effectiveMoveToEdgeSpeed = moveToEdgeSpeed * angerSpeedMult;

        if (rb == null || ownerTransform == null) { isAborted = true; return; }

        // 元の状態を保存.
        originalConstraints = rb.constraints;
        originalIsTrigger = mainColl != null ? mainColl.isTrigger : false;

        // 突進中はY軸固定とコライダーをトリガーに設定（すり抜け用）.
        rb.constraints = originalConstraints | RigidbodyConstraints2D.FreezePositionY;
        if (mainColl != null)
        {
            mainColl.isTrigger = true;
        }
        stateModified = true;

        Vector2 currentPos = ownerTransform.position;

        // 近い方の端を選択.
        float distToMin = Mathf.Abs(currentPos.x - stageMinX);
        float distToMax = Mathf.Abs(currentPos.x - stageMaxX);
        float targetEdgeX = distToMin < distToMax ? stageMinX : stageMaxX;
        oppositeEdgeX = distToMin < distToMax ? stageMaxX : stageMinX;

        // === 前段階 ===.
        // 1. 端の方向を見ながら端まで移動.
        float targetEdgeDirectionX = Mathf.Sign(targetEdgeX - currentPos.x);
        EnemFacingHelper.FaceDirection(ownerTransform, targetEdgeDirectionX);

        // EnemMovementHelperで端へ移動.
        var edgeMoveResult = await EnemMovementHelper.ExecuteMove(enemyModel, new EnemMoveParams
        {
            Destination = new Vector2(targetEdgeX, currentPos.y),
            Classification = EnemMoveClassification.RushPreMove,
            SpeedMultiplier = effectiveMoveToEdgeSpeed / enemyModel.MoveSpeed.x,
            FacingMode = EnemFacingMode.FaceMovement,
            Timeout = 5f,
            ArrivalThreshold = 0.5f,
            SetMoveAnimation = true
        });

        if (edgeMoveResult.NullInterrupted) { isAborted = true; return; }

        // 2. 逆方向を見る.
        if (!EnemNullSafetyHelper.IsValid(enemyModel)) { isAborted = true; return; }
        oppositeDirectionX = Mathf.Sign(oppositeEdgeX - ownerTransform.position.x);
        EnemFacingHelper.FaceDirection(ownerTransform, oppositeDirectionX);

        // 3. Assault_Pre トリガー実行.
        if (!EnemNullSafetyHelper.IsValidWithAnimator(enemyModel)) { isAborted = true; return; }
        animator.SetTrigger("Assault_Pre");
        await UniTask.WaitUntil(() =>
        {
            if (!EnemNullSafetyHelper.IsValidWithAnimator(enemyModel)) return true;
            return animator.GetCurrentAnimatorStateInfo(0).IsName("Assault_Pre") == false ||
                   animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f;
        });

        // 攻撃通告: パリィ不可 (突進の0.3秒前).
        if (!EnemNullSafetyHelper.IsValid(enemyModel)) { isAborted = true; return; }
        enemyModel.Presenter.PlayAttackWarning(false);
        await UniTask.Delay((int)(300 / animSpeed));
    }

    protected override async UniTask OnAction(EnemyModel_abstract enemyModel)
    {
        if (!EnemNullSafetyHelper.IsValid(enemyModel)) { isAborted = true; return; }

        // 攻撃中はレイヤーをDefaultに変更（既存コライダーで当たり判定を行うため）.
        originalLayer = EnemColliderHelper.SetAttackLayer(ownerTransform);
        // mainCollが子オブジェクト上にある場合、そのレイヤーもDefaultに変更.
        int mainCollOriginalLayer = -1;
        if (mainColl != null && mainColl.gameObject != ownerTransform.gameObject)
        {
            mainCollOriginalLayer = mainColl.gameObject.layer;
            mainColl.gameObject.layer = LayerMask.NameToLayer("Default");
        }

        // 既存コライダー（mainColl）でヒット検出.
        colliderState.ClearHitTargets();
        colliderState.SetDamage(rushDamage);
        EnemyAttackHitDetector rushHitDetector = null;
        if (mainColl != null)
        {
            // HitDetectorはRigidbody2Dがある ownerTransform に設置.
            // (子オブジェクトのCollider2DのトリガーイベントはRigidbody2Dの親に送信される).
            rushHitDetector = EnemColliderHelper.AttachHitDetector(
                ownerTransform, colliderState,
                new List<Collider2D> { mainColl });
        }

        animator.SetTrigger("Assault_Assault");

        // 残像エフェクト開始.
        afterimageEffect = ownerTransform.GetComponent<AfterimageEffect>();
        if (afterimageEffect == null)
        {
            afterimageEffect = ownerTransform.gameObject.AddComponent<AfterimageEffect>();
        }
        afterimageEffect.SetColor(new Color(1f, 0.2f, 0.2f, 0.6f));
        afterimageEffect.SetFadeDuration(0.6f);
        afterimageEffect.SetSpawnInterval(0.015f);
        afterimageEffect.StartEffect();

        // 逆端まで突進.
        while (Mathf.Abs(ownerTransform.position.x - oppositeEdgeX) > 0.5f)
        {
            if (!EnemNullSafetyHelper.IsValid(enemyModel))
            {
                if (rushHitDetector != null) Object.Destroy(rushHitDetector);
                // 子オブジェクトのレイヤー復元.
                if (mainCollOriginalLayer >= 0 && mainColl != null)
                    mainColl.gameObject.layer = mainCollOriginalLayer;
                isAborted = true;
                return;
            }
            rb.linearVelocity = new Vector2(oppositeDirectionX * effectiveRushSpeed, rb.linearVelocity.y);
            await UniTask.Yield();
        }

        // 突進ヒット検出を終了.
        if (rushHitDetector != null)
        {
            Object.Destroy(rushHitDetector);
        }

        // 子オブジェクトのレイヤー復元.
        if (mainCollOriginalLayer >= 0 && mainColl != null)
        {
            mainColl.gameObject.layer = mainCollOriginalLayer;
        }

        // 残像エフェクト停止.
        afterimageEffect?.StopEffect();

        if (!EnemNullSafetyHelper.IsValid(enemyModel)) { isAborted = true; return; }
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
    }

    protected override async UniTask OnPrePostAction(EnemyModel_abstract enemyModel)
    {
        if (EnemNullSafetyHelper.IsValidWithAnimator(enemyModel))
        {
            animator.SetTrigger("Assault_End");
        }
        await UniTask.CompletedTask;
    }

    protected override async UniTask OnAfterPostAction(EnemyModel_abstract enemyModel)
    {
        if (EnemNullSafetyHelper.IsValidWithAnimator(enemyModel))
        {
            animator.SetTrigger("Assault_Finish");
        }

        // 元の状態を復元（常に実行 — クリーンアップ保証）.
        RestoreState();
        await UniTask.CompletedTask;
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
        }
        if (mainColl != null)
        {
            mainColl.isTrigger = originalIsTrigger;
        }
        stateModified = false;
    }
}
