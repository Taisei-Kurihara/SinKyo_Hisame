using Common;
using R3;
using System;
using UnityEngine;
using System.Linq;
using Cysharp.Threading.Tasks;
using InGame.Common;
using VContainer;
using GameEventPoint;

namespace InGame.Player
    {
    /// <summary>
    /// 鼓動ゲージ専用クラス.
    /// </summary>
    public class PulseModel
    {
        public ReactiveProperty<float> pulseGauge { get; private set; }
            = new ReactiveProperty<float>();

        private float minPulseGauge = 0f;
        public float maxPulseGauge { get; private set; } = 200f;
        private float basePulseGauge = 100f;

        // 減少速度設定.
        private float idleDecreasePerSec = 2f;
        private float iaiDecreasePerSec = 0.1f;

        // 動的減少量: 心拍数上昇時に0.02まで下がり、10秒かけて2まで回復.
        private float currentIdleDecrease = 2f;
        private const float minIdleDecrease = 0.02f;
        private const float maxIdleDecrease = 2f;
        private const float decreaseRecoveryDuration = 10f;
        private float decreaseRecoveryTimer = 0f;
        private bool isDecreaseRecovering = false;

        // ---- 基本アクセス ----
        public float GetPulseGauge() => pulseGauge.Value;
        public void SetPulseGauge(float v) => pulseGauge.Value = Math.Clamp(v, minPulseGauge, maxPulseGauge);

        /// <summary>
        /// 鼓動ゲージを基準値(100)にリセット.
        /// </summary>
        public void ResetToBase() => pulseGauge.Value = basePulseGauge;
        float rate = 3;

        /// <summary>
        /// 心拍数が上昇した時に減少量を0.02にリセットし、回復タイマーを開始する.
        /// </summary>
        private void OnPulseIncreased()
        {
            currentIdleDecrease = minIdleDecrease;
            decreaseRecoveryTimer = 0f;
            isDecreaseRecovering = true;
        }

        // ---- 鼓動上昇条件 ----

        public void Stun()
        {
            CoolTimeBuilder.Create().OnStart(() => { }).SetTime(TimeSpan.FromSeconds(1.5)).Run();
        }

        /// <summary>
        /// 攻撃を振る（Hit）: +0.5.
        /// </summary>
        public void OnAttackHit()
        {
            pulseGauge.Value = Math.Clamp(pulseGauge.Value + 0.5f * rate, minPulseGauge, maxPulseGauge);
            OnPulseIncreased();
        }

        /// <summary>
        /// 攻撃を振る（Miss）: +1.
        /// </summary>
        public void OnAttackMiss()
        {
            pulseGauge.Value = Math.Clamp(pulseGauge.Value + 1f * rate, minPulseGauge, maxPulseGauge);
            OnPulseIncreased();
        }

        /// <summary>
        /// 敵からの攻撃を受ける: 被弾時受けた減少HP×0.85倍.
        /// </summary>
        /// <param name="damageTaken">受けたダメージ量.</param>
        public void OnDamageTaken(float damageTaken)
        {
            float increase = damageTaken * 0.85f;
            pulseGauge.Value = Math.Clamp(pulseGauge.Value + increase, minPulseGauge, maxPulseGauge);
            OnPulseIncreased();
        }

        /// <summary>
        /// 回避: +1.5.
        /// </summary>
        public void OnDodge()
        {
            pulseGauge.Value = Math.Clamp(pulseGauge.Value + 1.5f * rate, minPulseGauge, maxPulseGauge);
            OnPulseIncreased();
        }

        /// <summary>
        /// 回復アイテム使用時: 鼓動ゲージを100にする.
        /// </summary>
        public void OnHealItemUsed()
        {
            pulseGauge.Value = basePulseGauge;
        }

        // ---- 鼓動減少条件 ----

        /// <summary>
        /// 攻撃を振らない時の減少処理.
        /// 通常時は秒間2減少。心拍数上昇時は0.02に下がり、10秒かけて2まで回復する.
        /// </summary>
        /// <param name="deltaTime">経過時間.</param>
        public void OnIdleDecrease(float deltaTime)
        {
            // 100以下の場合は何もしない（100未満を100に戻さない）.
            if (pulseGauge.Value <= basePulseGauge) return;

            // 減少量の回復処理: 0.02 → 10秒かけて 2 まで線形回復.
            if (isDecreaseRecovering)
            {
                decreaseRecoveryTimer += deltaTime;
                float t = Math.Clamp(decreaseRecoveryTimer / decreaseRecoveryDuration, 0f, 1f);
                currentIdleDecrease = Mathf.Lerp(minIdleDecrease, maxIdleDecrease, t);
                if (t >= 1f)
                {
                    isDecreaseRecovering = false;
                }
            }

            float decrease = currentIdleDecrease * deltaTime;
            // 100未満にはならない.
            pulseGauge.Value = Math.Max(pulseGauge.Value - decrease, basePulseGauge);
        }

        /// <summary>
        /// 居合ボタン長押し: 秒間0.5減少.
        /// </summary>
        /// <param name="deltaTime">経過時間.</param>
        public void OnIaiHoldDecrease(float deltaTime)
        {
            float decrease = iaiDecreasePerSec * deltaTime;
            pulseGauge.Value = Math.Clamp(pulseGauge.Value - decrease, minPulseGauge, maxPulseGauge);
        }

        // ---- 鼓動ゲージ連動倍率 ----

        /// <summary>
        /// 鼓動値に応じた入力不可時間倍率.
        /// 0～100: 2.0～1.0, 100～175: 1.0～0.5, 175～200: 0.5固定.
        /// </summary>
        public float GetActionCooldownRate()
        {
            float pulse = pulseGauge.Value;
            if (pulse <= 100f)
            {
                return Mathf.Lerp(2.0f, 1.0f, pulse / 100f);
            }
            else if (pulse <= 175f)
            {
                return Mathf.Lerp(1.0f, 0.5f, (pulse - 100f) / 75f);
            }
            else
            {
                return 0.5f;
            }
        }

        /// <summary>
        /// 鼓動値に応じたアニメーション速度倍率.
        /// 0～100: 0.667～1.0, 100～175: 1.0～2.0, 175～200: 2.0固定.
        /// </summary>
        public float GetAnimationSpeedRate()
        {
            float pulse = pulseGauge.Value;
            if (pulse <= 100f)
            {
                return Mathf.Lerp(1f / 1.5f, 1.0f, pulse / 100f);
            }
            else if (pulse <= 175f)
            {
                return Mathf.Lerp(1.0f, 2.0f, (pulse - 100f) / 75f);
            }
            else
            {
                return 2.0f;
            }
        }

        // ---- 旧互換用 ----
        public float GetBreachingPoint() => pulseGauge.Value;
        public void SetBreachingPoint(float v) => SetPulseGauge(v);
        public void AddBreachingPoint(float num)
            => pulseGauge.Value = Math.Clamp(pulseGauge.Value + num, minPulseGauge, maxPulseGauge);
        public void ReduceBreachingPoint(float num)
            => pulseGauge.Value = Math.Clamp(pulseGauge.Value - num, minPulseGauge, maxPulseGauge);
        public float GetBreachingPercent()
            => pulseGauge.Value / maxPulseGauge;
    }
}