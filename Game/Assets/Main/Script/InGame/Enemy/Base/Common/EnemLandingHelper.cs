using UnityEngine;
using Cysharp.Threading.Tasks;

/// <summary>
/// Enemy 着地検出ヘルパー.
/// 空中にいる敵が着地するまで待機し、地面にスナップする共通処理.
/// </summary>
public static class EnemLandingHelper
{
    // デフォルトの地面レイヤーマスク（Platform + Default）.
    private static readonly int defaultGroundLayerMask = LayerMask.GetMask("Platform", "Default");

    /// <summary>
    /// 着地を待機する.
    /// 上昇中はスキップし、落下中にレイキャストで地面を検出する.
    /// </summary>
    /// <param name="enemyModel">敵モデル.</param>
    /// <param name="groundCheckDistance">着地判定レイキャスト距離.</param>
    /// <param name="landingAnimTrigger">着地時に再生するアニメーショントリガー（nullなら再生しない）.</param>
    /// <param name="snapRayDistance">地面スナップ用レイキャスト距離.</param>
    /// <param name="timeoutSec">タイムアウト秒（0以下で無制限）.</param>
    /// <returns>着地に成功したか.</returns>
    public static async UniTask<bool> WaitForLanding(
        EnemyModel_abstract enemyModel,
        float groundCheckDistance = 0.3f,
        string landingAnimTrigger = null,
        float snapRayDistance = 1.5f,
        float timeoutSec = 5f)
    {
        if (!EnemNullSafetyHelper.IsValid(enemyModel)) return false;

        Rigidbody2D rb = enemyModel.Rigidbody;
        if (rb == null) return false;

        float elapsed = 0f;
        float airTimeElapsed = 0f;

        while (true)
        {
            if (!EnemNullSafetyHelper.IsValid(enemyModel)) return false;

            elapsed += Time.deltaTime;
            airTimeElapsed += Time.deltaTime;

            // タイムアウトチェック.
            if (timeoutSec > 0f && elapsed >= timeoutSec)
            {
                Debug.LogWarning("[EnemLandingHelper] 着地タイムアウト.");
                return false;
            }

            // 落下中かつ最低滞空時間経過後に着地判定.
            if (rb.linearVelocity.y <= 0.1f && airTimeElapsed > 0.2f)
            {
                if (CheckGrounded(enemyModel, groundCheckDistance))
                {
                    // 地面にスナップ.
                    SnapToGround(enemyModel, snapRayDistance);

                    // 速度リセット.
                    rb.linearVelocity = Vector2.zero;

                    // 着地アニメーション再生.
                    if (!string.IsNullOrEmpty(landingAnimTrigger) && EnemNullSafetyHelper.IsValidWithAnimator(enemyModel))
                    {
                        enemyModel.Animator.SetTrigger(landingAnimTrigger);
                    }

                    return true;
                }
            }

            await UniTask.Yield();
        }
    }

    /// <summary>
    /// 足元からレイキャストで地面を検出する.
    /// </summary>
    public static bool CheckGrounded(EnemyModel_abstract enemyModel, float distance = 0.3f)
    {
        if (!EnemNullSafetyHelper.IsValid(enemyModel)) return false;

        Transform ownerTransform = enemyModel.Presenter.transform;
        Vector2 feetPos = (Vector2)ownerTransform.position;

        RaycastHit2D hit = Physics2D.Raycast(feetPos, Vector2.down, distance, defaultGroundLayerMask);
        return hit.collider != null;
    }

    /// <summary>
    /// 地面に正確にスナップする.
    /// 少し上からレイキャストを飛ばして地表面の位置を取得し、Y座標を合わせる.
    /// </summary>
    public static void SnapToGround(EnemyModel_abstract enemyModel, float rayDistance = 1.5f)
    {
        if (!EnemNullSafetyHelper.IsValid(enemyModel)) return;

        Transform ownerTransform = enemyModel.Presenter.transform;
        Vector2 rayOrigin = (Vector2)ownerTransform.position + Vector2.up * 0.5f;

        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, rayDistance, defaultGroundLayerMask);
        if (hit.collider != null)
        {
            Vector3 pos = ownerTransform.position;
            pos.y = hit.point.y;
            ownerTransform.position = pos;
        }
    }
}
