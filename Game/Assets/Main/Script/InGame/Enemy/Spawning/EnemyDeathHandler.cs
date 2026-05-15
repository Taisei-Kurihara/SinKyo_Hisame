using Cysharp.Threading.Tasks;
using UnityEngine;

namespace InGame.Enemy
{
    // Enemy死亡時の処理を担当するクラス（非MonoBehaviour）.
    // 死亡アニメーション後のAI停止・通知等を行う.
    // GameObjectの破棄はDeathManagerが管理する.
    public class EnemyDeathHandler
    {
        private EnemyModel_abstract enemyModel;
        private bool isDead = false;

        public bool IsDead => isDead;
        public EnemyModel_abstract EnemyModel => enemyModel;

        public EnemyDeathHandler(EnemyModel_abstract model)
        {
            enemyModel = model;
        }

        // Enemy死亡時に呼ばれる処理.
        // 既存のOnDeathComplete相当の処理（Destroyを除く）.
        public async UniTask OnDeath()
        {
            if (isDead) return;
            isDead = true;

            Debug.Log($"[EnemyDeathHandler] OnDeath開始");

            if (enemyModel == null)
            {
                Debug.LogWarning("[EnemyDeathHandler] enemyModelがnull");
                return;
            }

            // AIループを停止.
            enemyModel.EnemAIStop();
            Debug.Log($"[EnemyDeathHandler] AIループ停止");

            await UniTask.CompletedTask;
        }
    }
}
