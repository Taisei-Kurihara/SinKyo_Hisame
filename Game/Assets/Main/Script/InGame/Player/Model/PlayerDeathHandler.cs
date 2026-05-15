using Cysharp.Threading.Tasks;
using UnityEngine;

namespace InGame.Player
{
    // プレイヤー死亡時の処理を担当するクラス（非MonoBehaviour）.
    // 死亡アニメーション・入力停止等の後処理を行う.
    public class PlayerDeathHandler
    {
        private bool isDead = false;

        public bool IsDead => isDead;

        // プレイヤー死亡時に呼ばれる処理.
        // 既存のHP<=0 Subscribe内の処理相当（入力停止・アニメーション・吹き飛ばし）.
        public void OnDeath()
        {
            if (isDead) return;
            isDead = true;

            Debug.Log("[PlayerDeathHandler] OnDeath");
        }

        // 状態リセット（再戦時等）.
        public void Reset()
        {
            isDead = false;
        }
    }
}
