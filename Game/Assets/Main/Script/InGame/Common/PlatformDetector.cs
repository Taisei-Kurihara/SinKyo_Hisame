using UnityEngine;
using Cysharp.Threading.Tasks;

namespace InGame.Common
{
    /// <summary>
    /// 足元のPlatformレイヤーをレイキャストで検出するユーティリティ.
    /// Player/Enemy共通で使用する.
    /// 下からは通り抜けられ、上からは着地できるワンウェイプラットフォーム対応.
    /// 上り坂・下り坂にも対応.
    /// PlatformEffector2Dと併用推奨.
    /// </summary>
    public class PlatformDetector
    {
        // Platformレイヤーマスク.
        private readonly int platformLayerMask;

        // 基本レイキャスト距離.
        private readonly float baseRayDistance;

        // レイキャスト開始位置の上方オフセット（めり込み対応）.
        private readonly float rayOriginOffset;

        // 現在足元にPlatformがあるか.
        public bool IsOnPlatform { get; private set; }

        // 検出したPlatformのCollider.
        public Collider2D CurrentPlatformCollider { get; private set; }

        // 検出したPlatformのY座標（表面位置）.
        public float PlatformY { get; private set; }

        // 検出した表面の法線（坂の傾きを表す）.
        public Vector2 SurfaceNormal { get; private set; } = Vector2.up;

        // すり抜け中フラグ.
        public bool IsDroppingThrough { get; private set; }

        // すり抜け対象のCollider.
        private Collider2D droppingCollider;

        // すり抜け中のキャラクターCollider.
        private Collider2D ownerCollider;

        // 前フレームでPlatform上にいたか（坂の継続検出用）.
        private bool wasOnPlatform = false;

        public PlatformDetector(float _baseRayDistance = 0.5f, float _rayOriginOffset = 0.15f)
        {
            int platformLayer = LayerMask.NameToLayer("Platform");
            platformLayerMask = 1 << platformLayer;
            baseRayDistance = _baseRayDistance;
            rayOriginOffset = _rayOriginOffset;
        }

        // 足裏がPlatform表面に接触したとみなす許容範囲.
        private const float surfaceTolerance = 0.05f;

        // 坂の上を歩行中に使用する拡大オフセット（レイ開始点を高くして坂面を確実に検出）.
        private const float slopeRayOriginOffset = 0.5f;

        // 坂歩行中の接触許容範囲（坂面に追従するため緩和）.
        private const float slopeSurfaceTolerance = 0.15f;

        /// <summary>
        /// 足元にPlatformがあるかレイキャストで判定する.
        /// 重力無視（IsOnPlatform=true）は以下の場合のみ:
        ///   - 足裏がPlatform表面に到達している（feetY <= platformY + tolerance）.
        ///   - または現在の落下速度で今フレーム中にPlatformを越す場合（予測スナップ）.
        /// 下からジャンプ中（中心がPlatformより下かつ前フレームで乗っていない）は常にfalse.
        /// 坂対応: 前フレームでPlatform上にいた場合、レイ開始点を高くして坂面を検出し続ける.
        /// </summary>
        /// <param name="feetPosition">足元（Collider下端）の位置.</param>
        /// <param name="centerY">キャラクター中心のY座標（上下判定に使用）.</param>
        /// <param name="velocityY">現在のY方向速度.</param>
        /// <returns>Platformが検出されたか.</returns>
        public bool CheckPlatformBelow(Vector2 feetPosition, float centerY, float velocityY = 0f)
        {
            // すり抜け中は検出しない.
            if (IsDroppingThrough)
            {
                IsOnPlatform = false;
                CurrentPlatformCollider = null;
                wasOnPlatform = false;
                return false;
            }

            // 坂対応: 前フレームでPlatform上にいた場合、レイ開始点を高くして坂面を確実に検出.
            float currentRayOffset = wasOnPlatform ? Mathf.Max(rayOriginOffset, slopeRayOriginOffset) : rayOriginOffset;
            float currentTolerance = wasOnPlatform ? slopeSurfaceTolerance : surfaceTolerance;

            // 落下速度に応じてレイキャスト距離を拡大（高速落下でもPlatformを検出する）.
            float fallDistance = velocityY < 0f ? Mathf.Abs(velocityY) * Time.fixedDeltaTime : 0f;
            float totalRayDistance = Mathf.Max(baseRayDistance, fallDistance) + currentRayOffset;

            // 足元位置から上にオフセットしてレイキャスト開始.
            Vector2 rayOrigin = feetPosition + Vector2.up * currentRayOffset;

            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, totalRayDistance, platformLayerMask);

            if (hit.collider != null)
            {
                float platformSurfaceY = hit.point.y;
                SurfaceNormal = hit.normal;

                // 下から通り抜け判定.
                // 前フレームでPlatform上にいた場合は坂歩行中の可能性があるためスキップ.
                if (!wasOnPlatform && centerY <= platformSurfaceY)
                {
                    IsOnPlatform = false;
                    CurrentPlatformCollider = null;
                    wasOnPlatform = false;
                    return false;
                }

                float feetY = feetPosition.y;

                // 条件1: 足裏がPlatform表面に到達している（接触 or 埋まっている）.
                if (feetY <= platformSurfaceY + currentTolerance)
                {
                    IsOnPlatform = true;
                    CurrentPlatformCollider = hit.collider;
                    PlatformY = platformSurfaceY;
                }
                // 条件2: 足裏はまだ上だが、今フレームの落下速度でPlatformを越す（予測スナップ）.
                else if (velocityY < 0f)
                {
                    float predictedFeetY = feetY + velocityY * Time.fixedDeltaTime;
                    if (predictedFeetY <= platformSurfaceY + currentTolerance)
                    {
                        IsOnPlatform = true;
                        CurrentPlatformCollider = hit.collider;
                        PlatformY = platformSurfaceY;
                    }
                    else
                    {
                        // まだ空中 - 通常落下を継続.
                        IsOnPlatform = false;
                        CurrentPlatformCollider = null;
                    }
                }
                else
                {
                    // 足裏がPlatformより上で落下していない - 通常状態.
                    IsOnPlatform = false;
                    CurrentPlatformCollider = null;
                }
            }
            else
            {
                IsOnPlatform = false;
                CurrentPlatformCollider = null;
            }

            wasOnPlatform = IsOnPlatform;
            return IsOnPlatform;
        }

        /// <summary>
        /// Platform上にいる場合、キャラクターの足元位置をPlatform表面にクランプする.
        /// 上り坂: 表面まで持ち上げる. 下り坂: 表面まで押し下げる.
        /// 毎フレーム呼ぶことで坂の表面に追従し、微小ドリフトも防止する.
        /// </summary>
        /// <param name="currentFeetY">現在の足元Y座標.</param>
        /// <returns>修正が必要なY方向の移動量（正=上、負=下、0=修正不要）.</returns>
        public float ClampToPlatformSurface(float currentFeetY)
        {
            if (!IsOnPlatform) return 0f;

            float diff = PlatformY - currentFeetY;
            return diff;
        }

        /// <summary>
        /// Platformをすり抜ける処理を開始する.
        /// キャラクターのColliderとPlatformのCollider間の衝突を一時的に無効化する.
        /// </summary>
        /// <param name="characterCollider">キャラクターのCollider.</param>
        /// <param name="dropDuration">すり抜け時間（秒）.</param>
        public void StartDropThrough(Collider2D characterCollider, float dropDuration = 0.35f)
        {
            if (CurrentPlatformCollider == null || IsDroppingThrough) return;

            ownerCollider = characterCollider;
            droppingCollider = CurrentPlatformCollider;
            IsDroppingThrough = true;
            IsOnPlatform = false;
            wasOnPlatform = false;

            // 衝突を無効化.
            Physics2D.IgnoreCollision(ownerCollider, droppingCollider, true);

            // 一定時間後に復帰.
            DropThroughAsync(dropDuration).Forget();
        }

        /// <summary>
        /// すり抜け処理の非同期復帰.
        /// </summary>
        private async UniTaskVoid DropThroughAsync(float duration)
        {
            await UniTask.Delay(System.TimeSpan.FromSeconds(duration));

            EndDropThrough();
        }

        /// <summary>
        /// すり抜け状態を終了し衝突を復帰する.
        /// </summary>
        public void EndDropThrough()
        {
            if (!IsDroppingThrough) return;

            if (ownerCollider != null && droppingCollider != null)
            {
                Physics2D.IgnoreCollision(ownerCollider, droppingCollider, false);
            }

            IsDroppingThrough = false;
            droppingCollider = null;
            ownerCollider = null;
        }
    }
}
