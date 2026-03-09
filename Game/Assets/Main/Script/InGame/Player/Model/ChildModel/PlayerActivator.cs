using UnityEngine;

namespace InGame.Player
{
    /// <summary>
    /// ActivatorModel。ここで可能かどうかを司る
    /// </summary>
    public class PlayerActivator
    {
        PlayerActivator()
        {
            disableMove = false;
            disableHeal = false;
            enableBattleFlag = false;
        }

        //移動
        public bool disableMove { get; private set; } = false;
        //回復不可
        public bool disableHeal { get; private set; } = false;
        //戦闘開始時(UIや、戦闘ステータスの初期化:ステージ開始時のEnable）
        public bool enableBattleFlag { get; private set; } = false;

        //プラクティスモードとか作った方がいいのかね。
        public bool practiceMode { get; private set; } = false;
        
        // 有効化
        public void EnableBattle()
        {
            enableBattleFlag = true;
        }
        /// <summary>
        /// 移動不可を起動
        /// </summary>
        public void OnDisableMove(){disableMove = true;}
        /// <summary>
        /// 移動不可を解除
        /// </summary>
        public void OffDisableMove(){ disableMove = false;}
    }
}