using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;

// Wendig用 近接アプローチState（移動のみ、コライダーなし）.
public class EnemState_Wendig_MeleeApproach : EnemState_abstract
{
    private Vector3 targetPosition;
    private float speedMultiplier = 3f;

    // アプローチの設定.
    private float approachDuration = 1.5f;
    private float arrivalThreshold = 1.5f;

    // プレイヤー近接検知用.
    private float playerProximityRange = 2f;
    public bool StoppedByPlayerProximity { get; private set; } = false;

    public void SetTargetPosition(Vector3 pos)
    {
        targetPosition = pos;
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

        // プレイヤー位置取得用.
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");

        // EnemMovementHelperに委譲.
        var result = await EnemMovementHelper.ExecuteMove(enemyModel, new EnemMoveParams
        {
            Destination = (Vector2)targetPosition,
            Classification = EnemMoveClassification.ApproachTarget,
            SpeedMultiplier = speedMultiplier,
            FacingMode = EnemFacingMode.FaceMovement,
            PlayerPosition = targetPosition,
            Timeout = approachDuration,
            ArrivalThreshold = arrivalThreshold,
            SetMoveAnimation = true,
            CancelConditions = new List<EnemMoveCancelCondition>
            {
                new EnemMoveCancelCondition
                {
                    CancelReason = "PlayerProximity",
                    ShouldCancel = (model, pos) =>
                    {
                        // プレイヤーとの実際の距離を直接チェック.
                        if (playerObj == null) return false;
                        return Vector2.Distance(pos, (Vector2)playerObj.transform.position) <= playerProximityRange;
                    }
                }
            }
        });

        // プレイヤー近接検知結果を反映.
        StoppedByPlayerProximity = result.Cancelled && result.CancelReason == "PlayerProximity";
    }
}
