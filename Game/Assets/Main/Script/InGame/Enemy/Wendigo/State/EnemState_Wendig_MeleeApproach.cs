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

    // ライフサイクル間共有データ.
    private EnemMoveResult lastMoveResult;
    private GameObject playerObj;

    public void SetTargetPosition(Vector3 pos)
    {
        targetPosition = pos;
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = multiplier;
    }

    protected override async UniTask OnPreAction(EnemyModel_abstract enemyModel)
    {
        if (!EnemNullSafetyHelper.IsValid(enemyModel)) { isAborted = true; return; }

        // フラグリセット.
        StoppedByPlayerProximity = false;
        lastMoveResult = default;

        // プレイヤー位置取得用.
        playerObj = GameObject.FindGameObjectWithTag("Player");

        await UniTask.CompletedTask;
    }

    protected override async UniTask OnAction(EnemyModel_abstract enemyModel)
    {
        // EnemMovementHelperに委譲.
        lastMoveResult = await EnemMovementHelper.ExecuteMove(enemyModel, new EnemMoveParams
        {
            Destination = (Vector2)targetPosition,
            Classification = EnemMoveClassification.ApproachTarget,
            SpeedMultiplier = speedMultiplier,
            FacingMode = EnemFacingMode.FaceMovement,
            PlayerPosition = targetPosition,
            Timeout = approachDuration,
            ArrivalThreshold = arrivalThreshold,
            SetMoveAnimation = true,
            // プレイヤー位置の動的取得（振り向き + 近接持続チェック用）.
            GetLivePlayerPosition = () => playerObj != null ? playerObj.transform.position : targetPosition,
            SustainedProximityDuration = 0.5f,
            SustainedProximityRange = 2f,
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
    }

    protected override async UniTask OnAfterPostAction(EnemyModel_abstract enemyModel)
    {
        // プレイヤー近接検知結果を反映.
        StoppedByPlayerProximity = lastMoveResult.Cancelled && lastMoveResult.CancelReason == "PlayerProximity";
        // 持続近接で中断した場合も反映.
        if (lastMoveResult.StoppedBySustainedProximity)
        {
            StoppedByPlayerProximity = true;
        }
        await UniTask.CompletedTask;
    }
}
