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

    // ライフサイクル間共有データ.
    private EnemMoveResult lastMoveResult;
    private GameObject playerObj;

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
            Destination = moveTargetPos,
            Classification = EnemMoveClassification.RandomMove,
            SpeedMultiplier = speedMultiplier,
            FacingMode = EnemFacingMode.FacePlayer,
            PlayerPosition = lookAtPos,
            Timeout = moveTimeout,
            ArrivalThreshold = 0.5f,
            SetMoveAnimation = true,
            // プレイヤー位置の動的取得（振り向き + 近接持続チェック用）.
            GetLivePlayerPosition = () => playerObj != null ? playerObj.transform.position : lookAtPos,
            SustainedProximityDuration = 0.5f,
            SustainedProximityRange = 2f,
            CancelConditions = new List<EnemMoveCancelCondition>
            {
                new EnemMoveCancelCondition
                {
                    CancelReason = "PlayerProximity",
                    ShouldCancel = (model, pos) =>
                    {
                        // プレイヤーとの距離が近接範囲内ならキャンセル.
                        if (playerObj == null) return false;
                        return Vector2.Distance(pos, (Vector2)playerObj.transform.position) <= meleeAttackRange;
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
