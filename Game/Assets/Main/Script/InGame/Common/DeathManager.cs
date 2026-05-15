using Common;
using Cysharp.Threading.Tasks;
using InGame.Enemy;
using InGame.Player;
using SceneInfo;
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

            var playerView = UnityEngine.Object.FindFirstObjectByType<PlayerView>();
            var cam = CameraManager.Instance();
            float originalFollowSpeed = cam.GetFollowSpeed();
            Transform playerTransform = cam.GetFollowTarget();
            Transform enemyTransform = enemyDeathHandler?.EnemyModel?.Presenter?.transform;

            // 現在のズーム値を保存（復帰用）.
            var mainCam = Camera.main;
            float originalZoom = mainCam != null
                ? (mainCam.orthographic ? mainCam.orthographicSize : mainCam.fieldOfView)
                : 60f;

            // === Step1: timeScale=0 + FullscreenBlackEffect ON + statusUI.alpha=0 ===.
            Time.timeScale = 0f;
            FullscreenBlackEffectFeature.IsEnabled = true;
            FullscreenBlackEffectFeature.Blend = 1f;
            if (playerView != null) playerView.SetStatusUIAlpha(0f);

            // === Step1.5: 完全黒のまま1sec待機 (realtime) ===.
            float startTime = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startTime < 1f)
            {
                await UniTask.Yield();
            }

            // === Step2: エフェクトのみフェードアウト (1.5s realtime / timeScale=0のまま) ===.
            cam.UseUnscaledTime = true;
            startTime = Time.realtimeSinceStartup;
            float duration = 1.5f;
            while (Time.realtimeSinceStartup - startTime < duration)
            {
                float t = Mathf.Clamp01((Time.realtimeSinceStartup - startTime) / duration);
                FullscreenBlackEffectFeature.Blend = 1f - t;
                if (playerView != null) playerView.SetStatusUIAlpha(t);
                await UniTask.Yield();
            }
            FullscreenBlackEffectFeature.IsEnabled = false;
            FullscreenBlackEffectFeature.Blend = 0f;
            if (playerView != null) playerView.SetStatusUIAlpha(1f);

            // === Step3: Enemy中心にズーム (timeScale=0のまま / speed=0.5 → 約2s) ===.
            if (enemyTransform != null)
            {
                cam.SetFollowTarget(enemyTransform);
                cam.SetFollowSpeed(30f);
            }
            float zoomTarget = mainCam != null
                ? (mainCam.orthographic ? mainCam.orthographicSize * 0.5f : mainCam.fieldOfView * 0.5f)
                : 30f;
            await cam.ZoomTo(zoomTarget, 0.5f);

            // === Step3.5: ズーム完了後Enemyを見せる待機 (1.0s realtime) ===.
            startTime = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startTime < 1f)
            {
                await UniTask.Yield();
            }

            // === Step4: プレイヤー中心にカメラ移動 (timeScale=0のまま / 1.5s realtime) ===.
            if (playerTransform != null)
            {
                cam.SetFollowTarget(playerTransform);
                cam.SetFollowSpeed(3f);
            }
            startTime = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startTime < 1.5f)
            {
                await UniTask.Yield();
            }

            // === Step5: ズームリセット + timeScale 0→1 (1.0s realtime) ===.
            var zoomTask = cam.ZoomTo(originalZoom, 1f);
            startTime = Time.realtimeSinceStartup;
            duration = 1f;
            while (Time.realtimeSinceStartup - startTime < duration)
            {
                float t = Mathf.Clamp01((Time.realtimeSinceStartup - startTime) / duration);
                Time.timeScale = t;
                await UniTask.Yield();
            }
            Time.timeScale = 1f;
            await zoomTask;
            cam.UseUnscaledTime = false;
            cam.SetFollowSpeed(originalFollowSpeed);

            // === Step6: Win.alpha → 1 (0.5s) + Step7: 3sec待機 (or 3回入力でスキップ) ===.
            skipInputCount = 0;

            // Win alpha フェードイン (0.5s).
            startTime = Time.realtimeSinceStartup;
            duration = 0.5f;
            while (Time.realtimeSinceStartup - startTime < duration)
            {
                float t = Mathf.Clamp01((Time.realtimeSinceStartup - startTime) / duration);
                if (playerView != null) playerView.SetWinAlpha(t);
                if (CheckAnyInputDown()) skipInputCount++;
                if (skipInputCount >= 3) { TransitionToTitle(); return; }
                await UniTask.Yield();
            }
            if (playerView != null) playerView.SetWinAlpha(1f);

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

            // === Step8: Scene遷移 ===.
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

            // === Step1: timeScale=0 (0.5s realtime) ===.
            Time.timeScale = 0f;
            float startTime = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startTime < 0.5f)
            {
                await UniTask.Yield();
            }

            // === Step2: timeScale→1, プレイヤーvelocity<=歩行速度 or 3sec経過待ち ===.
            Time.timeScale = 1f;
            startTime = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startTime < 3f)
            {
                if (playerRb != null && playerRb.linearVelocity.magnitude <= playerWalkSpeed)
                {
                    break;
                }
                await UniTask.Yield();
            }

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
            var cam = CameraManager.Instance();
            if (cam != null) cam.UseUnscaledTime = false;

            Debug.Log("[DeathManager] シーン遷移開始");
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
            playerDeathHandler?.Reset();
            enemyDeathHandler = null;
            playerDeathHandler = null;
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
        }

        // シングルトン破棄（シーン遷移時等）.
        public static void DisposeInstance()
        {
            instance?.Reset();
            instance = null;
        }
    }
}
