using UnityEngine;
using UnityEngine.UI;

namespace InGame.Player
{
    public interface IPlayerView
    {
        void SetHpGauge(float percent);
        void SetSkillGauge(float percent);
        /// <summary>
        /// 回復残り回数
        /// </summary>
        /// <param name="num"></param>
        void SetHealPointCount(int num);

        /// <summary>
        /// 心拍数ゲージ設定.
        /// </summary>
        /// <param name="heartRate">心拍数(0-200).</param>
        void SetHeartGauge(int heartRate);

        void SetDrainGages(float percent);

        void SetDrainUIGenerat(int num);
    }
}