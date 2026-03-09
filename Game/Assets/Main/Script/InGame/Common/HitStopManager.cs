using UnityEngine;
using Common;
using Cysharp.Threading.Tasks;
using System;
using InGame.Player;

namespace InGame.Common
{
    /// <summary>
    /// ヒットストップ管理クラス.
    /// 遅延→完全停止→徐々に復帰.
    /// </summary>
    public class HitStopManager : SingletonMonoBase<HitStopManager>
    {
        // ヒットストップ中フラグ.
        private bool isHitStopping = false;

        // 遅延時間（リアルタイム秒、全攻撃共通）.
        private const float hitStopDelay = 0.25f;

        /// <summary>
        /// 攻撃種別に応じたヒットストップを再生.
        /// </summary>
        public void PlayHitStop(PlayerAttackType attackType)
        {
            float freeze;
            float recovery;
            switch (attackType)
            {
                case PlayerAttackType.Iai:
                    freeze = 0.1f;
                    recovery = 0.075f;
                    break;
                case PlayerAttackType.Normal:
                    freeze = 0.075f;
                    recovery = 0.075f;
                    break;
                default: // Weak など.
                    freeze = 0.075f;
                    recovery = 0.075f;
                    break;
            }
            PlayHitStopAsync(freeze, recovery).Forget();
        }

        /// <summary>
        /// 停止/復帰時間を直接指定してヒットストップを実行.
        /// </summary>
        public void PlayHitStop(float freeze, float recovery)
        {
            PlayHitStopAsync(freeze, recovery).Forget();
        }

        private async UniTaskVoid PlayHitStopAsync(float freezeDuration, float recoveryDuration)
        {
            // 既にヒットストップ中の場合は無視.
            if (isHitStopping) return;

            isHitStopping = true;

            // 遅延（リアルタイム）.
            float delayStart = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - delayStart < hitStopDelay)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            // 完全停止.
            Time.timeScale = 0f;
            float freezeStart = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - freezeStart < freezeDuration)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            // 徐々に元の速度に復帰.
            float recoveryStart = Time.realtimeSinceStartup;
            while (true)
            {
                float elapsed = Time.realtimeSinceStartup - recoveryStart;
                if (elapsed >= recoveryDuration) break;
                float t = Mathf.Clamp01(elapsed / recoveryDuration);
                Time.timeScale = Mathf.Lerp(0f, 1f, t);
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            Time.timeScale = 1f;
            isHitStopping = false;
        }
    }
}
