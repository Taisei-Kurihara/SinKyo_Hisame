using UnityEngine;

namespace InGame.Player
{
    /// <summary>
    /// 通常攻撃
    /// </summary>
    public class FirstDefault : AbstructAttackBase
    {
        public override void Act()
        {
            if (!CanExecute()) return;

            // 攻撃処理
            //Debug.Log("FirstAttack executed");
            // ここで PlayerModel のメソッド呼び出し等

        }
    }
}