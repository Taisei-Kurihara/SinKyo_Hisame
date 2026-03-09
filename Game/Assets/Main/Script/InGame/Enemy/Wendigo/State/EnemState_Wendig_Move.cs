using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;

// Wendig用 移動State.
public class EnemState_Wendig_Move : EnemState_abstract
{
    private Vector2 moveTargetPos;
    private Vector3 lookAtPos;
    private float moveTimeout = 2f;
    private float speedMultiplier = 2f;

    // 移動中のプレイヤー近接検知用.
    private float meleeAttackRange = 2f;

    // プレイヤー近接で移動中断したかどうか（AIループ側で参照）.
    public bool StoppedByPlayerProximity { get; private set; } = false;

    public void SetMovePos(Vector2 targetPos)
    {
        moveTargetPos = targetPos;
    }

    public void SetLookAtPos(Vector3 targetPos)
    {
        lookAtPos = targetPos;
    }

    public void SetMoveTimeout(float timeout)
    {
        moveTimeout = timeout;
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = multiplier;
    }

    public override async UniTask Act(EnemyModel_abstract enemyModel)
    {
        if (!EnemNullSafetyHelper.IsValid(enemyModel)) return;

        // フラグリセット.
        StoppedByPlayerProximity = false;

        // EnemMovementHelperに委譲.
        var result = await EnemMovementHelper.ExecuteMove(enemyModel, new EnemMoveParams
        {
            Destination = moveTargetPos,
            Classification = EnemMoveClassification.RandomMove,
            SpeedMultiplier = speedMultiplier,
            FacingMode = EnemFacingMode.FacePlayer,
            PlayerPosition = lookAtPos,
            Timeout = moveTimeout,
            ArrivalThreshold = 0.5f,
            SetMoveAnimation = true,
            CancelConditions = new List<EnemMoveCancelCondition>
            {
                new EnemMoveCancelCondition
                {
                    CancelReason = "PlayerProximity",
                    ShouldCancel = (model, pos) =>
                    {
                        // プレイヤーとの距離が近接範囲内ならキャンセル.
                        return Vector2.Distance(pos, (Vector2)lookAtPos) <= meleeAttackRange;
                    }
                }
            }
        });

        // プレイヤー近接検知結果を反映.
        StoppedByPlayerProximity = result.Cancelled && result.CancelReason == "PlayerProximity";
    }
}
