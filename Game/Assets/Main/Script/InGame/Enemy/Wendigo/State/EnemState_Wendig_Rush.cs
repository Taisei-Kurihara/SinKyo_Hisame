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
    private int waitFrames = 40;

    public void SetStageEdge(float minX, float maxX)
    {
        stageMinX = minX;
        stageMaxX = maxX;
    }

    public override async UniTask Act(EnemyModel_abstract enemyModel)
    {
        if (!EnemNullSafetyHelper.IsValid(enemyModel)) return;

        // 現在の攻撃力から実ダメージを計算.
        if (enemyModel is EnemyModel_Wendig wendigModel)
        {
            rushDamage = (int)(wendigModel.GetCurrentAttackPower() * rushMultiplier);
        }

        Rigidbody2D rb = enemyModel.Rigidbody;
        Transform ownerTransform = enemyModel.Presenter.transform;
        Animator animator = enemyModel.Animator;
        Collider2D mainColl = enemyModel.Presenter.MainColl;
        float animSpeed = enemyModel.AnimSpeed;

        // 怒り時は速度2倍.
        float angerSpeedMult = animSpeed > 1f ? 2f : 1f;
        float effectiveRushSpeed = rushSpeed * angerSpeedMult;
        float effectiveMoveToEdgeSpeed = moveToEdgeSpeed * angerSpeedMult;

        if (rb == null || ownerTransform == null) return;

        // 元の状態を保存.
        RigidbodyConstraints2D originalConstraints = rb.constraints;
        bool originalIsTrigger = mainColl != null ? mainColl.isTrigger : false;

        // 突進中はY軸固定とコライダーをトリガーに設定.
        rb.constraints = originalConstraints | RigidbodyConstraints2D.FreezePositionY;
        if (mainColl != null)
        {
            mainColl.isTrigger = true;
        }

        Vector2 currentPos = ownerTransform.position;

        // 近い方の端を選択.
        float distToMin = Mathf.Abs(currentPos.x - stageMinX);
        float distToMax = Mathf.Abs(currentPos.x - stageMaxX);
        float targetEdgeX = distToMin < distToMax ? stageMinX : stageMaxX;
        float oppositeEdgeX = distToMin < distToMax ? stageMaxX : stageMinX;

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

        if (edgeMoveResult.NullInterrupted)
        {
            RestoreState(rb, mainColl, originalConstraints, originalIsTrigger);
            return;
        }

        // 2. 逆方向を見る.
        if (!EnemNullSafetyHelper.IsValid(enemyModel))
        {
            RestoreState(rb, mainColl, originalConstraints, originalIsTrigger);
            return;
        }
        float oppositeDirectionX = Mathf.Sign(oppositeEdgeX - ownerTransform.position.x);
        EnemFacingHelper.FaceDirection(ownerTransform, oppositeDirectionX);

        // 3. Assault_Pre トリガー実行.
        if (!EnemNullSafetyHelper.IsValidWithAnimator(enemyModel))
        {
            RestoreState(rb, mainColl, originalConstraints, originalIsTrigger);
            return;
        }
        animator.SetTrigger("Assault_Pre");
        await UniTask.WaitUntil(() =>
        {
            if (!EnemNullSafetyHelper.IsValidWithAnimator(enemyModel)) return true;
            return animator.GetCurrentAnimatorStateInfo(0).IsName("Assault_Pre") == false ||
                   animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f;
        });

        // 攻撃通告: パリィ不可 (突進の0.3秒前).
        if (!EnemNullSafetyHelper.IsValid(enemyModel))
        {
            RestoreState(rb, mainColl, originalConstraints, originalIsTrigger);
            return;
        }
        enemyModel.Presenter.PlayAttackWarning(false);
        await UniTask.Delay((int)(300 / animSpeed));

        // === 攻撃中 ===.
        if (!EnemNullSafetyHelper.IsValid(enemyModel))
        {
            RestoreState(rb, mainColl, originalConstraints, originalIsTrigger);
            return;
        }

        // 突進中のヒット検出を設定（既存コライダー使用）.
        colliderState.ClearHitTargets();
        colliderState.SetDamage(rushDamage);
        EnemyAttackHitDetector rushHitDetector = null;
        if (mainColl != null)
        {
            rushHitDetector = EnemColliderHelper.AttachHitDetector(
                ownerTransform, colliderState,
                new List<Collider2D> { mainColl });
        }

        animator.SetTrigger("Assault_Assault");

        // 逆端まで突進.
        while (Mathf.Abs(ownerTransform.position.x - oppositeEdgeX) > 0.5f)
        {
            if (!EnemNullSafetyHelper.IsValid(enemyModel))
            {
                if (rushHitDetector != null) Object.Destroy(rushHitDetector);
                RestoreState(rb, mainColl, originalConstraints, originalIsTrigger);
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

        if (!EnemNullSafetyHelper.IsValid(enemyModel))
        {
            RestoreState(rb, mainColl, originalConstraints, originalIsTrigger);
            return;
        }
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);

        // === 攻撃後 ===.
        animator.SetTrigger("Assault_End");

        // フレーム待機.
        if (!await EnemAttackPhaseHelper.WaitPostAttackFrames(enemyModel, waitFrames, animSpeed))
        {
            RestoreState(rb, mainColl, originalConstraints, originalIsTrigger);
            return;
        }

        if (EnemNullSafetyHelper.IsValidWithAnimator(enemyModel))
        {
            animator.SetTrigger("Assault_Finish");
        }

        // 元の状態を復元.
        RestoreState(rb, mainColl, originalConstraints, originalIsTrigger);
    }

    // 元の状態を復元するヘルパーメソッド.
    private void RestoreState(Rigidbody2D rb, Collider2D mainColl, RigidbodyConstraints2D originalConstraints, bool originalIsTrigger)
    {
        if (rb != null)
        {
            rb.constraints = originalConstraints;
        }
        if (mainColl != null)
        {
            mainColl.isTrigger = originalIsTrigger;
        }
    }
}
