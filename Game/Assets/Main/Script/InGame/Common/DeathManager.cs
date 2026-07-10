using Common;
using Cysharp.Threading.Tasks;
using InGame.Enemy;
using InGame.Player;
using InGame.Player.Animation;
using R3;
using SceneInfo;
using System;
using System.Collections.Generic;
using System.Threading;
using Tutorial;
using UnityEngine;

namespace InGame.Common
{
    // 死亡処理を一元管理するクラス（非MonoBehaviour）.
    // EnemyDeathHandler / PlayerDeathHandler を保持し、
    // 勝利・敗北の判定と後処理の呼び出しを行う.
    public class DeathManager
    {
        private static DeathManager instance;
        public static DeathManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new DeathManager();
                }
                return instance;
            }
        }

        // ドメインリロード無効時の静的フィールドリセット.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            Time.timeScale = 1f;
        }

        private EnemyDeathHandler enemyDeathHandler;
        private PlayerDeathHandler playerDeathHandler;

        // 勝利・敗北の結果.
        public bool IsVictory { get; private set; } = false;
        public bool IsDefeat { get; private set; } = false;
        public bool IsGameEnd => IsVictory || IsDefeat;

        // 入力スキップ用カウンタ.
        private int skipInputCount = 0;

        // プレイヤー歩行速度（速度判定用）.
        private const float playerWalkSpeed = 7f;

        // HP監視サブスクリプション.
        private IDisposable playerHpSubscription;
        private List<IDisposable> enemyHpSubscriptions = new List<IDisposable>();

        // ---- HP監視（旧DeathConditionの機能を統合） ----

        /// <summary>
        /// プレイヤーHPを監視し、HP <= 0 で敗北演出を開始する.
        /// </summary>
        public void MonitorPlayerHP(ReactiveProperty<int> playerHp)
        {
            playerHpSubscription?.Dispose();
            playerHpSubscription = playerHp
                .Where(hp => hp <= 0)
                .Take(1)
                .Subscribe(_ =>
                {
                    Debug.Log("[DeathManager] プレイヤーHP <= 0 検知");
                    NotifyPlayerDeath().Forget();
                });
        }

        /// <summary>
        /// EnemyのHPを監視し、HP <= 0 で勝利演出を開始する.
        /// </summary>
        public void MonitorEnemyHP(ReactiveProperty<float> enemyHp)
        {
            var subscription = enemyHp
                .Where(hp => hp <= 0)
                .Take(1)
                .Subscribe(_ =>
                {
                    Debug.Log("[DeathManager] EnemyHP <= 0 検知");
                    NotifyEnemyDeath().Forget();
                });
            enemyHpSubscriptions.Add(subscription);
        }

        /// <summary>
        /// SceneChangeStand互換のゲーム終了条件を生成する.
        /// </summary>
        public IGameEndCondition CreateGameEndCondition()
        {
            return new DeathGameEndCondition();
        }

        /// <summary>
        /// SceneChangeStand互換のゲーム終了条件.
        /// DeathManagerのIsGameEnd状態を監視する.
        /// </summary>
        private class DeathGameEndCondition : IGameEndCondition
        {
            public async UniTask<bool> WaitForConditionAsync(CancellationToken token)
            {
                try
                {
                    while (!Instance.IsGameEnd)
                    {
                        await UniTask.Yield(cancellationToken: token);
                    }
                    return true;
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
            }

            public void Dispose() { }
        }

        // ---- 登録 ----

        // EnemyDeathHandlerを登録.
        public void RegisterEnemy(EnemyDeathHandler handler)
        {
            enemyDeathHandler = handler;
            Debug.Log("[DeathManager] EnemyDeathHandler登録");
        }

        // PlayerDeathHandlerを登録.
        public void RegisterPlayer(PlayerDeathHandler handler)
        {
            playerDeathHandler = handler;
            Debug.Log("[DeathManager] PlayerDeathHandler登録");
        }

        // ---- 死亡通知 ----

        // Enemy死亡時に呼ばれる（勝利演出シーケンス）.
        // 流れ: timeScale=0 → 白黒化 → timeScale徐々に1 + 白黒率徐々に0
        //       → 白黒解除 → 待機 → ズーム → win文字出現.
        public async UniTask NotifyEnemyDeath()
        {
            if (IsGameEnd) return;

            Debug.Log("[DeathManager] Enemy死亡通知 - 勝利演出開始");
            IsVictory = true;

            if (enemyDeathHandler != null)
            {
                await enemyDeathHandler.OnDeath();
            }

            StopAllEnemyAI();

            // プレイヤー操作を無効化.
            var playerScope = UnityEngine.Object.FindFirstObjectByType<PlayerScope>();
            if (playerScope != null) playerScope.SetPlayerEnable(false);

            var playerView = UnityEngine.Object.FindFirstObjectByType<PlayerView>();
            var cam = CameraManager.Instance();

            // 現在のズーム値を保存（復帰用）.
            var mainCam = Camera.main;
            float originalZoom = mainCam != null
                ? (mainCam.orthographic ? mainCam.orthographicSize : mainCam.fieldOfView)
                : 60f;

            // チュートリアルCanvas非表示.
            var tutorialView = UnityEngine.Object.FindFirstObjectByType<TutorialView>();
            if (tutorialView != null) tutorialView.gameObject.SetActive(false);

            // UI非表示.
            if (playerView != null) playerView.SetStatusUIAlpha(0f);
            var enemyUISetters = UnityEngine.Object.FindObjectsByType<EnemyUI_View_Setter>(FindObjectsSortMode.None);
            foreach (var setter in enemyUISetters)
            {
                if (setter != null) setter.gameObject.SetActive(false);
            }
            var offScreenIndicators = UnityEngine.Object.FindObjectsByType<EnemyOffScreenIndicator>(FindObjectsSortMode.None);
            foreach (var indicator in offScreenIndicators)
            {
                if (indicator != null) indicator.gameObject.SetActive(false);
            }

            // === Step1: timeScale=0 + シルエット化 (Fillモード: 背景白/オブジェクト黒) ===.
            Time.timeScale = 0f;
            FullscreenBlackEffectFeature.IsEnabled = true;
            FullscreenBlackEffectFeature.FillEnabled = true;       // Fillモード（alpha閾値で白黒分離）.
            FullscreenBlackEffectFeature.Blend = 1f;               // 完全シルエット.
            FullscreenBlackEffectFeature.GrayscaleRatio = 0f;      // グレースケール不使用.

            cam.UseUnscaledTime = true;
            // ApplyHeartRateZoomによるズーム上書きを防止.
            cam.SetZoomLock(true);

            // === Step1.5: 白黒中に振動 (1sec realtime) + 1sec待機 ===.
            cam.SnapToFollowTarget();
            cam.ShakeCamera(1f, 0.2f).Forget();
            float shakeStart = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - shakeStart < 1f)
            {
                await UniTask.Yield();
            }
            cam.StopShake();

            // 振動後 1sec待機.
            float waitStart = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - waitStart < 1f)
            {
                await UniTask.Yield();
            }

            // === Step2: timeScale 0→1 + シルエット解除 (同時進行, 2sec realtime) ===.
            float transitionDuration = 2f;
            float startTime = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startTime < transitionDuration)
            {
                float t = Mathf.Clamp01((Time.realtimeSinceStartup - startTime) / transitionDuration);
                Time.timeScale = Mathf.Lerp(0f, 1f, t);
                FullscreenBlackEffectFeature.Blend = Mathf.Lerp(1f, 0f, t);
                await UniTask.Yield();
            }

            // === Step3: timeScale=1, エフェクト解除 ===.
            Time.timeScale = 1f;
            FullscreenBlackEffectFeature.Blend = 0f;
            FullscreenBlackEffectFeature.IsEnabled = false;
            cam.UseUnscaledTime = false;

            // === Step4: 待機 + 納刀アニメーション ===.
            var playerAnim = UnityEngine.Object.FindFirstObjectByType<PlayerAnimationController>();
            if (playerAnim != null) playerAnim.PlayTrigger("sheathing_of_sword");

            // 待機 (0.5sec).
            await UniTask.Delay(500);

            // === Step5: ズーム (player中心) ===.
            cam.ClearBounds();
            cam.SetFollowSpeed(30f);
            float zoomTarget = originalZoom * 0.5f;
            await cam.ZoomTo(zoomTarget, 2.0f);

            // ズーム完了後もズーム値を維持（ApplyHeartRateZoomによる復帰を防止）.
            cam.SetZoomLock(true);

            // ズーム完了後: 納刀アニメーション2.
            if (playerAnim != null) playerAnim.PlayTrigger("sheathing_of_sword_2");

            // === Step6: Win.alpha → 1 (0.5s realtime) ===.
            skipInputCount = 0;
            startTime = Time.realtimeSinceStartup;
            float duration = 0.5f;
            while (Time.realtimeSinceStartup - startTime < duration)
            {
                float t = Mathf.Clamp01((Time.realtimeSinceStartup - startTime) / duration);
                if (playerView != null) playerView.SetWinAlpha(t);
                if (CheckAnyInputDown()) skipInputCount++;
                if (skipInputCount >= 3) { TransitionToTitle(); return; }
                await UniTask.Yield();
            }
            if (playerView != null) playerView.SetWinAlpha(1f);

            // === Step6.5: Win表示後 1.5sec待機 (realtime / 入力スキップ可能) ===.
            startTime = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startTime < 1.5f)
            {
                if (CheckAnyInputDown()) skipInputCount++;
                if (skipInputCount >= 3) { TransitionToTitle(); return; }
                await UniTask.Yield();
            }

            // GameOverView表示.
            var gameOverView = UnityEngine.Object.FindFirstObjectByType<GameOverView>();
            if (gameOverView != null) gameOverView.Show(true);

            // 3sec待機（入力スキップ可能）.
            startTime = Time.realtimeSinceStartup;
            duration = 3f;
            while (Time.realtimeSinceStartup - startTime < duration)
            {
                if (CheckAnyInputDown()) skipInputCount++;
                if (skipInputCount >= 3) break;
                await UniTask.Yield();
            }

            // === Scene遷移 ===.
            TransitionToTitle();
        }

        // プレイヤー死亡時に呼ばれる（敗北演出シーケンス）.
        public async UniTask NotifyPlayerDeath()
        {
            if (IsGameEnd) return;

            Debug.Log("[DeathManager] プレイヤー死亡通知 - 敗北演出開始");
            IsDefeat = true;

            playerDeathHandler?.OnDeath();
            StopAllEnemyAI();

            var playerView = UnityEngine.Object.FindFirstObjectByType<PlayerView>();
            var cam = CameraManager.Instance();
            Transform playerTransform = cam.GetFollowTarget();
            Rigidbody2D playerRb = playerTransform != null
                ? playerTransform.GetComponent<Rigidbody2D>()
                : null;

            // === Step1: timeScale=0 + 左右カメラ振動 (0.75s realtime) ===.
            Time.timeScale = 0f;
            cam.UseUnscaledTime = true;
            cam.SnapToFollowTarget();
            // 死亡SE再生（設定のみ、再生内容未設定）.
            // AudioManager.Instance()?.PlaySE("SE_PlayerDead");
            cam.ShakeCamera(0.75f, 0.2f).Forget();
            float startTime = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startTime < 0.75f)
            {
                await UniTask.Yield();
            }

            // === Step1.5: timeScale=0のまま 0.25s待機後、Deadアニメーショントリガー発火 ===.
            cam.StopShake();
            startTime = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startTime < 0.25f)
            {
                await UniTask.Yield();
            }

            // Deadアニメーショントリガー発火.
            var playerAnim = UnityEngine.Object.FindFirstObjectByType<PlayerAnimationController>();
            if (playerAnim != null)
            {
                playerAnim.NotifyDead();
                playerAnim.PlayDead();
            }

            // === Step2: timeScaleを徐々に0→1へ復帰（3secで強制1.0） ===.
            cam.UseUnscaledTime = true;
            float timeScaleRestoreDuration = 3f;
            startTime = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startTime < timeScaleRestoreDuration)
            {
                float elapsed = Time.realtimeSinceStartup - startTime;
                float t = Mathf.Clamp01(elapsed / timeScaleRestoreDuration);
                Time.timeScale = Mathf.Lerp(0f, 1f, t);

                // 速度が歩行速度以下 かつ timeScale >= 0.8 なら早期完了.
                if (Time.timeScale >= 0.8f
                    && playerRb != null && playerRb.linearVelocity.magnitude <= playerWalkSpeed)
                {
                    break;
                }
                await UniTask.Yield();
            }
            Time.timeScale = 1f;
            cam.UseUnscaledTime = false;

            // === Step3: Lose.alpha → 1 (0.5s) + Step4: 3sec待機 (or 3回入力でスキップ) ===.
            skipInputCount = 0;

            // Lose alpha フェードイン (0.5s).
            startTime = Time.realtimeSinceStartup;
            float duration = 0.5f;
            while (Time.realtimeSinceStartup - startTime < duration)
            {
                float t = Mathf.Clamp01((Time.realtimeSinceStartup - startTime) / duration);
                if (playerView != null) playerView.SetLoseAlpha(t);
                if (CheckAnyInputDown()) skipInputCount++;
                if (skipInputCount >= 3) { TransitionToTitle(); return; }
                await UniTask.Yield();
            }
            if (playerView != null) playerView.SetLoseAlpha(1f);

            // GameOverView表示.
            var gameOverView = UnityEngine.Object.FindFirstObjectByType<GameOverView>();
            if (gameOverView != null) gameOverView.Show(false);

            // 3sec待機（入力スキップ可能）.
            startTime = Time.realtimeSinceStartup;
            duration = 3f;
            while (Time.realtimeSinceStartup - startTime < duration)
            {
                if (CheckAnyInputDown()) skipInputCount++;
                if (skipInputCount >= 3) break;
                await UniTask.Yield();
            }

            // === Step5: Scene遷移 ===.
            TransitionToTitle();
        }

        // ---- ヘルパー ----

        // 入力検出（キーボードanyKey + マウス左/中/右クリック）.
        private bool CheckAnyInputDown()
        {
            return Input.anyKeyDown;
        }

        // タイトルへ遷移.
        private void TransitionToTitle()
        {
            // timeScaleとカメラを確実に復元.
            Time.timeScale = 1f;
            FullscreenBlackEffectFeature.IsEnabled = false;
            FullscreenBlackEffectFeature.GrayscaleRatio = 0f;
            FullscreenBlackEffectFeature.Blend = 0f;
            FullscreenBlackEffectFeature.FillEnabled = true;
            var cam = CameraManager.Instance();
            if (cam != null) cam.UseUnscaledTime = false;

            // プレイヤーUI状態を復元（DontDestroyOnLoadで永続する場合対策）.
            var playerView = UnityEngine.Object.FindFirstObjectByType<PlayerView>();
            if (playerView != null)
            {
                playerView.SetStatusUIAlpha(1f);
                playerView.SetWinAlpha(0f);
                playerView.SetLoseAlpha(0f);
            }

            Debug.Log("[DeathManager] シーン遷移開始");

            // シングルトン破棄（次回ゲーム開始時に新規インスタンス生成）.
            DisposeInstance();

            SceneManager.Instance().LoadMainScene(new TitleSceneInfo()).Forget();
        }

        // シーン上の全EnemyのAIループを停止.
        private void StopAllEnemyAI()
        {
            var enemyModels = UnityEngine.Object.FindObjectsByType<EnemyModel_abstract>(FindObjectsSortMode.None);
            foreach (var model in enemyModels)
            {
                if (model != null)
                {
                    model.EnemAIStop();
                }
            }
            Debug.Log($"[DeathManager] 全EnemyAI停止 - 対象数: {enemyModels.Length}");
        }

        // ---- リセット ----

        // ゲーム再開時等にリセット.
        public void Reset()
        {
            IsVictory = false;
            IsDefeat = false;
            skipInputCount = 0;
            Time.timeScale = 1f;
            FullscreenBlackEffectFeature.IsEnabled = false;
            FullscreenBlackEffectFeature.Blend = 0f;
            FullscreenBlackEffectFeature.GrayscaleRatio = 0f;
            FullscreenBlackEffectFeature.FillEnabled = true;
            playerDeathHandler?.Reset();
            enemyDeathHandler = null;
            playerDeathHandler = null;

            // HP監視サブスクリプション破棄.
            playerHpSubscription?.Dispose();
            playerHpSubscription = null;
            foreach (var sub in enemyHpSubscriptions) sub?.Dispose();
            enemyHpSubscriptions.Clear();

            Debug.Log("[DeathManager] リセット");
        }

        // デバッグ用: フラグとtimeScaleのみリセット（Handler登録は維持）.
        public void DebugReset()
        {
            IsVictory = false;
            IsDefeat = false;
            skipInputCount = 0;
            Time.timeScale = 1f;
            FullscreenBlackEffectFeature.IsEnabled = false;
            FullscreenBlackEffectFeature.Blend = 0f;
            FullscreenBlackEffectFeature.GrayscaleRatio = 0f;
            FullscreenBlackEffectFeature.FillEnabled = true;
        }

        // シングルトン破棄（シーン遷移時等）.
        public static void DisposeInstance()
        {
            instance?.Reset();
            instance = null;
        }
    }
}
