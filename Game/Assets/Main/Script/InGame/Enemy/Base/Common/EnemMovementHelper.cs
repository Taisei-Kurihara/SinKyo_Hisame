using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;

// 移動種別を表すenum.
public enum EnemMoveClassification
{
    RandomMove,         // ランダム移動.
    ApproachTarget,     // ターゲットへの接近.
    ReturnToCamera,     // カメラ範囲内への復帰.
    RushPreMove,        // 突進前の端への移動.
    Custom              // カスタム移動.
}

// 向く方向の設定.
public enum EnemFacingMode
{
    FacePlayer,         // 常にプレイヤーの方を見る.
    FaceMovement        // 移動方向を見る.
}

// キャンセル条件の定義.
public class EnemMoveCancelCondition
{
    // キャンセル判定関数.
    public System.Func<EnemyModel_abstract, Vector2, bool> ShouldCancel;

    // キャンセル時に実行するアクション（nullならそのまま終了）.
    public System.Func<EnemyModel_abstract, UniTask> OnCancel;

    // キャンセル理由を示す識別子.
    public string CancelReason;
}

// 移動結果.
public class EnemMoveResult
{
    // 移動が正常に完了したか.
    public bool Completed;

    // キャンセルされたか.
    public bool Cancelled;

    // キャンセル理由.
    public string CancelReason;

    // null化で中断されたか.
    public bool NullInterrupted;

    // プレイヤー近接持続で中断されたか.
    public bool StoppedBySustainedProximity;
}

// 移動パラメータの設定.
public class EnemMoveParams
{
    // 移動場所.
    public Vector2 Destination;

    // 移動後の行動（移動完了後に呼ばれるコールバック、nullなら何もしない）.
    public System.Func<EnemyModel_abstract, UniTask> PostMoveAction;

    // 何に分類される移動か.
    public EnemMoveClassification Classification = EnemMoveClassification.RandomMove;

    // 移動速度倍率.
    public float SpeedMultiplier = 1f;

    // 向く方向.
    public EnemFacingMode FacingMode = EnemFacingMode.FacePlayer;

    // プレイヤー位置（FacePlayer時に使用）.
    public Vector3 PlayerPosition;

    // キャンセル条件リスト.
    public List<EnemMoveCancelCondition> CancelConditions;

    // 移動タイムアウト（秒）.
    public float Timeout = 2f;

    // 到達判定の閾値.
    public float ArrivalThreshold = 0.5f;

    // 移動開始時にMoveアニメーションを設定するか.
    public bool SetMoveAnimation = true;

    // プレイヤー位置を動的に取得する関数（nullならPlayerPosition固定使用）.
    public System.Func<Vector3> GetLivePlayerPosition;

    // プレイヤー近接持続で移動中断するまでの時間（秒、0以下で無効）.
    public float SustainedProximityDuration = 0f;

    // プレイヤー近接範囲.
    public float SustainedProximityRange = 2f;
}

// 移動処理の共通ヘルパークラス.
// 移動場所・移動後の行動・移動分類・速度倍率・向く方向・キャンセル条件を一元管理する.
public static class EnemMovementHelper
{
    /// <summary>
    /// 共通移動処理を実行する.
    /// </summary>
    /// <param name="enemyModel">敵モデル.</param>
    /// <param name="moveParams">移動パラメータ.</param>
    /// <returns>移動結果.</returns>
    public static async UniTask<EnemMoveResult> ExecuteMove(
        EnemyModel_abstract enemyModel,
        EnemMoveParams moveParams)
    {
        var result = new EnemMoveResult();

        if (!EnemNullSafetyHelper.IsValid(enemyModel))
        {
            result.NullInterrupted = true;
            return result;
        }

        Transform ownerTransform = enemyModel.Presenter.transform;
        Rigidbody2D rb = enemyModel.Rigidbody;

        if (rb == null || ownerTransform == null)
        {
            result.NullInterrupted = true;
            return result;
        }

        // 移動アニメーション設定.
        if (moveParams.SetMoveAnimation && EnemNullSafetyHelper.IsValidWithAnimator(enemyModel))
        {
            enemyModel.Animator.SetInteger("Move", 1);
        }

        float elapsedTime = 0f;

        // プレイヤー位置監視用タイマー.
        float playerMonitorTimer = 0f;
        const float playerMonitorInterval = 1f;

        // プレイヤー近接持続チェック用.
        float sustainedProximityTimer = 0f;
        bool sustainedProximityEnabled = moveParams.SustainedProximityDuration > 0f;

        while (elapsedTime < moveParams.Timeout)
        {
            if (!EnemNullSafetyHelper.IsValid(enemyModel))
            {
                result.NullInterrupted = true;
                break;
            }

            Vector2 currentPos = ownerTransform.position;
            float distanceToTarget = Vector2.Distance(currentPos, moveParams.Destination);

            // プレイヤー位置の動的更新.
            if (moveParams.GetLivePlayerPosition != null)
            {
                Vector3 livePlayerPos = moveParams.GetLivePlayerPosition();
                playerMonitorTimer += Time.deltaTime;

                // FacePlayer: 毎フレームPlayerPositionを動的更新.
                if (moveParams.FacingMode == EnemFacingMode.FacePlayer)
                {
                    moveParams.PlayerPosition = livePlayerPos;
                }
                // FaceMovement: 1秒ごとにプレイヤー方向へ振り向く.
                else if (moveParams.FacingMode == EnemFacingMode.FaceMovement && playerMonitorTimer >= playerMonitorInterval)
                {
                    EnemFacingHelper.FaceToward(ownerTransform, livePlayerPos);
                    playerMonitorTimer = 0f;
                }

                // プレイヤー近接持続チェック.
                if (sustainedProximityEnabled)
                {
                    float distToPlayer = Vector2.Distance(currentPos, (Vector2)livePlayerPos);
                    if (distToPlayer <= moveParams.SustainedProximityRange)
                    {
                        sustainedProximityTimer += Time.deltaTime;
                        if (sustainedProximityTimer >= moveParams.SustainedProximityDuration)
                        {
                            result.Cancelled = true;
                            result.CancelReason = "SustainedProximity";
                            result.StoppedBySustainedProximity = true;
                            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                            break;
                        }
                    }
                    else
                    {
                        sustainedProximityTimer = 0f;
                    }
                }
            }

            // 到達判定.
            if (distanceToTarget <= moveParams.ArrivalThreshold)
            {
                result.Completed = true;
                break;
            }

            // キャンセル条件チェック.
            if (moveParams.CancelConditions != null)
            {
                bool cancelled = false;
                foreach (var condition in moveParams.CancelConditions)
                {
                    if (condition.ShouldCancel(enemyModel, currentPos))
                    {
                        result.Cancelled = true;
                        result.CancelReason = condition.CancelReason;

                        // 移動停止.
                        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);

                        // キャンセル時アクション実行.
                        if (condition.OnCancel != null)
                        {
                            await condition.OnCancel(enemyModel);
                        }

                        cancelled = true;
                        break;
                    }
                }
                if (cancelled) break;
            }

            // 移動方向計算.
            Vector2 direction = (moveParams.Destination - currentPos).normalized;
            rb.linearVelocity = new Vector2(
                direction.x * enemyModel.MoveSpeed.x * moveParams.SpeedMultiplier,
                rb.linearVelocity.y);

            // 向き制御.
            switch (moveParams.FacingMode)
            {
                case EnemFacingMode.FacePlayer:
                    EnemFacingHelper.FaceToward(ownerTransform, moveParams.PlayerPosition);
                    break;
                case EnemFacingMode.FaceMovement:
                    EnemFacingHelper.FaceDirection(ownerTransform, direction.x);
                    break;
            }

            elapsedTime += Time.deltaTime;
            await UniTask.Yield();
        }

        // 速度リセット.
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        }

        // 移動アニメーション解除.
        if (moveParams.SetMoveAnimation && EnemNullSafetyHelper.IsValidWithAnimator(enemyModel))
        {
            enemyModel.Animator.SetInteger("Move", 0);
        }

        // 移動後の行動を実行.
        if (result.Completed && moveParams.PostMoveAction != null && EnemNullSafetyHelper.IsValid(enemyModel))
        {
            await moveParams.PostMoveAction(enemyModel);
        }

        return result;
    }
}
