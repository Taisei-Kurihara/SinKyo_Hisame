using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using System;

using Random = UnityEngine.Random;

namespace Common
{
    /// <summary>
    /// カメラをプレイヤーなどに追従させたり、シェイク制御を行うマネージャー。
    /// シーンをまたがない想定。
    /// </summary>
    public class CameraManager : SingletonMonoBase<CameraManager>
    {
        private Camera mainCamera;
        private Transform followerObject; // 追従対象
        private bool isFollowing = true;  // 追従オンオフ

        // カメラ追従設定
        [SerializeField]
        private Vector3 offset = new Vector3(0, 1.5f, -10f);
        [SerializeField]
        private float followSpeed = 5f;

        // カメラ境界設定.
        private bool hasBounds = false;
        private bool boundsInitialized = false;
        private float boundsMinX;
        private float boundsMaxX;
        private float boundsMinY;
        private float boundsMaxY;

        // シェイク設定
        private bool isShaking = false;
        private Vector3 initialLocalPos;




        void Awake()
        {
            mainCamera = Camera.main;

            if (mainCamera == null)
            {
                Debug.LogError("Main Camera が見つかりません。シーンに 'MainCamera' タグが付いたカメラを配置してください。");
            }
            else
            {
                initialLocalPos = mainCamera.transform.localPosition;
            }
        }

        void LateUpdate()
        {
            // マーカーが見つかるまで毎フレーム試行.
            if (!boundsInitialized)
            {
                ApplyBoundsFromMarkers();
            }

            if (mainCamera == null || followerObject == null) return;

            if (isFollowing)
            {
                Vector3 targetPos = followerObject.position + offset;

                // カメラ境界でクランプ.
                if (hasBounds)
                {
                    targetPos.x = Mathf.Clamp(targetPos.x, boundsMinX, boundsMaxX);
                    targetPos.y = Mathf.Clamp(targetPos.y, boundsMinY, boundsMaxY);
                }

                // スムーズに追従.
                mainCamera.transform.position = Vector3.Lerp(
                    mainCamera.transform.position,
                    targetPos,
                    followSpeed * Time.deltaTime
                );
            }
        }
        #region カメラ境界

        /// <summary>
        /// StageBoundsMarker と CameraBoundsUI からカメラ境界を計算して適用する.
        /// </summary>
        public void ApplyBoundsFromMarkers()
        {
            if (mainCamera == null) return;

            // Current は自動生成せずシーン配置のインスタンスのみ返す.
            var stageMarker = StageBoundsMarker.Current;
            if (stageMarker == null) return;

            // カメラ表示サイズの半分を算出.
            float halfWidth;
            float halfHeight;
            if (mainCamera.orthographic)
            {
                halfHeight = mainCamera.orthographicSize;
                halfWidth = halfHeight * mainCamera.aspect;
            }
            else
            {
                float distance = Mathf.Abs(offset.z);
                halfHeight = distance * Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                halfWidth = halfHeight * mainCamera.aspect;
            }

            // ステージ端を取得.
            float stageLeftX = stageMarker.LeftX;
            float stageRightX = stageMarker.RightX;
            float stageBottomY = stageMarker.BottomY;
            float stageTopY = stageMarker.TopY;

            // CameraBoundsUI から Viewport Inset を取得 (未配置なら inset=0).
            var boundsUI = CameraBoundsUI.Current;

            float leftInset = 0f;
            float rightInset = 0f;
            float bottomInset = 0f;
            float topInset = 0f;

            if (boundsUI != null)
            {
                // Viewport 座標からインセット計算.
                // Bottom: inner edge の viewport Y (0=画面下端).
                if (boundsUI.IsBottomEnabled)
                    bottomInset = boundsUI.GetBottomInnerViewportY();
                // Top: 1 - inner edge の viewport Y (0=画面上端).
                if (boundsUI.IsTopEnabled)
                    topInset = 1f - boundsUI.GetTopInnerViewportY();
                // Left: inner edge の viewport X (0=画面左端).
                if (boundsUI.IsLeftEnabled)
                    leftInset = boundsUI.GetLeftInnerViewportX();
                // Right: 1 - inner edge の viewport X (0=画面右端).
                if (boundsUI.IsRightEnabled)
                    rightInset = 1f - boundsUI.GetRightInnerViewportX();
            }

            // 各辺の境界を計算.
            // inset=0: カメラ端 = ステージ端.
            // inset>0: ステージ端がスクリーン内側に表示 (UIバーが外側を覆う).
            bool hasAnyBound = false;

            if (boundsUI == null || boundsUI.IsLeftEnabled)
            {
                boundsMinX = stageLeftX + halfWidth * (1f - 2f * leftInset);
                hasAnyBound = true;
            }
            else
            {
                boundsMinX = float.MinValue;
            }

            if (boundsUI == null || boundsUI.IsRightEnabled)
            {
                boundsMaxX = stageRightX - halfWidth * (1f - 2f * rightInset);
                hasAnyBound = true;
            }
            else
            {
                boundsMaxX = float.MaxValue;
            }

            if (boundsUI == null || boundsUI.IsBottomEnabled)
            {
                boundsMinY = stageBottomY + halfHeight * (1f - 2f * bottomInset);
                hasAnyBound = true;
            }
            else
            {
                boundsMinY = float.MinValue;
            }

            if (boundsUI == null || boundsUI.IsTopEnabled)
            {
                boundsMaxY = stageTopY - halfHeight * (1f - 2f * topInset);
                hasAnyBound = true;
            }
            else
            {
                boundsMaxY = float.MaxValue;
            }

            hasBounds = hasAnyBound;
            boundsInitialized = true;
        }

        /// <summary>
        /// カメラ境界を解除.
        /// </summary>
        public void ClearBounds()
        {
            hasBounds = false;
        }
        #endregion

        #region 追従対象関連
        public void SetFollowTarget(Transform target)
        {
            followerObject = target;
        }

        public void EnableFollow() => isFollowing = true;
        public void DisableFollow() => isFollowing = false;
        public void ToggleFollow() => isFollowing = !isFollowing;
        public bool IsFollowing() => isFollowing;
        #endregion

        /// <summary>
        /// オフセット
        /// </summary>
        /// <param name="newOffset"></param>
        public void SetOffset(Vector3 newOffset)
        {
            offset = newOffset;
            EnableFollow();
        }

       　/// <summary>
        /// 追従スピード
        /// </summary>
        /// <param name="speed"></param>
        public void SetFollowSpeed(float speed)
        {
            followSpeed = Mathf.Max(0f, speed);
        }

        public float GetFollowSpeed() => followSpeed;

        /// <summary>
        /// 追従スピードの変速
        /// </summary>
        /// <param name="targetSpeed"></param>
        /// <param name="lerpRate"></param>

        public void SmoothChangeFollowSpeed(float targetSpeed, float lerpRate = 2f)
        {
            followSpeed = Mathf.Lerp(followSpeed, targetSpeed, lerpRate * Time.deltaTime);
        }

        #region 揺れ関係

        // 何も指定しない場合。
        private float defaultShakeDuration = 0.3f;
        private float defaultShakeMagnitude = 0.2f;
        private Vector3 defaultShakeDirection = Vector3.one; // ← デフォルトはランダムXY

        // タスクのキャンセルに使用する
        private CancellationTokenSource shake;
        /// <summary>
        /// カメラを揺らす。
        /// </summary>
        /// <param name="duration">揺れの時間（秒）</param>
        /// <param name="magnitude">揺れの強さ</param>
        /// <param name="direction">揺れの方向（例: Vector3.up, Vector3.right など）</param>
        /// <returns></returns>
        public async UniTaskVoid ShakeCamera(float duration = -1f, float magnitude = -1f, Vector3? direction = null)
        {
            if (mainCamera == null || isShaking) return;

            // すでに揺れていたら止めてから新しく開始
            StopShake();

            // トークン生成
            shake = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            var linkedToken = shake.Token;

            isShaking = true;
            Vector3 originalPos = initialLocalPos;

            // 揺れパラメータ決定
            float d = (duration > 0f) ? duration : defaultShakeDuration;
            float m = (magnitude > 0f) ? magnitude : defaultShakeMagnitude;
            Vector3 dir = (direction ?? defaultShakeDirection).normalized;

            try
            {
                float elapsed = 0f;

                while (elapsed < d)
                {
                    float progress = elapsed / d;
                    float damping = 1f - progress; // 終盤に揺れを弱くする

                    Vector3 randomOffset;

                    // 方向指定あり → その方向ベースで揺れる
                    // Vector3.one（デフォルト）ならランダムXY方向揺れ
                    if (dir == Vector3.one)
                    {
                        float x = Random.Range(-1f, 1f) * m * damping;
                        float y = Random.Range(-1f, 1f) * m * damping;
                        randomOffset = new Vector3(x, y, 0);
                    }
                    else
                    {
                        // 指定方向 ± に沿って揺れる
                        float sign = Random.Range(0, 2) == 0 ? -1f : 1f;
                        randomOffset = dir * m * damping * sign;
                    }

                    mainCamera.transform.localPosition = originalPos + randomOffset;
                    elapsed += Time.deltaTime;

                    linkedToken.ThrowIfCancellationRequested();
                    await UniTask.Yield(PlayerLoopTiming.Update, linkedToken);
                }
            }
            catch (OperationCanceledException)
            {
                // キャンセル時は無視
            }
            finally
            {
                mainCamera.transform.localPosition = originalPos;
                isShaking = false;

                shake?.Dispose();
                shake = null;
            }
        }

        /// <summary>
        /// シェイクを停止する。
        /// </summary>
        public void StopShake()
        {
            if (mainCamera == null) return;

            if (shake != null && !shake.IsCancellationRequested)
            {
                shake.Cancel();
            }

            mainCamera.transform.localPosition = initialLocalPos;
            isShaking = false;
        }

        /// <summary>
        /// デフォルトのシェイク時間・強さ・方向を設定する。
        /// </summary>
        public void SetDefaultShakeParams(float duration, float magnitude, Vector3? direction = null)
        {
            defaultShakeDuration = Mathf.Max(0.01f, duration);
            defaultShakeMagnitude = Mathf.Max(0f, magnitude);
            if (direction.HasValue)
                defaultShakeDirection = direction.Value.normalized;
        }
        #endregion


        #region ズーム関係

        private float defaultZoom = 60f; // Perspective時: FOV
        private float defaultOrthoSize = 5f; // Orthographic時: Size
        private float zoomSpeed = 5f;

        private CancellationTokenSource zoomCTS;

        /// <summary>
        /// ズーム関連初期化（Awakeなどで呼ぶ）
        /// </summary>
        private void InitZoomSettings()
        {
            if (mainCamera == null) return;

            if (mainCamera.orthographic)
                defaultOrthoSize = mainCamera.orthographicSize;
            else
                defaultZoom = mainCamera.fieldOfView;
        }

        /// <summary>
        /// 指定ズーム値までスムーズにズームする
        /// </summary>
        /// <param name="targetZoom">FOV または OrthoSize</param>
        /// <param name="speed">ズーム速度</param>
        public async UniTask ZoomTo(float targetZoom, float speed = -1f)
        {
            if (mainCamera == null) return;

            StopZoom();

            zoomCTS = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            var token = zoomCTS.Token;

            float s = speed > 0 ? speed : zoomSpeed;

            try
            {
                float start, target;
                if (mainCamera.orthographic)
                {
                    start = mainCamera.orthographicSize;
                    target = Mathf.Max(0.01f, targetZoom);
                }
                else
                {
                    start = mainCamera.fieldOfView;
                    target = Mathf.Clamp(targetZoom, 1f, 179f);
                }

                float t = 0f;
                while (!token.IsCancellationRequested && t < 1f)
                {
                    t += Time.deltaTime * s;
                    if (mainCamera.orthographic)
                        mainCamera.orthographicSize = Mathf.Lerp(start, target, t);
                    else
                        mainCamera.fieldOfView = Mathf.Lerp(start, target, t);

                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                zoomCTS?.Dispose();
                zoomCTS = null;
            }
        }

        /// <summary>
        /// ズームを途中停止する
        /// </summary>
        public void StopZoom()
        {
            if (zoomCTS != null && !zoomCTS.IsCancellationRequested)
                zoomCTS.Cancel();
        }

        /// <summary>
        /// 初期ズーム値に戻す
        /// </summary>
        public async UniTaskVoid ResetZoom(float speed = -1f)
        {
            if (mainCamera == null) return;

            if (mainCamera.orthographic)
                await ZoomTo(defaultOrthoSize, speed);
            else
                await ZoomTo(defaultZoom, speed);
        }

        /// <summary>
        /// デフォルトズーム値と速度を設定する
        /// </summary>
        public void SetDefaultZoomParams(float zoomOrSize, float speed = 5f)
        {
            zoomSpeed = Mathf.Max(0.1f, speed);

            if (mainCamera == null) return;

            if (mainCamera.orthographic)
                defaultOrthoSize = Mathf.Max(0.01f, zoomOrSize);
            else
                defaultZoom = Mathf.Clamp(zoomOrSize, 1f, 179f);
        }

        #endregion
    }
}
