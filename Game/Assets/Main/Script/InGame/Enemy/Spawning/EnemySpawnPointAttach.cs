using UnityEngine;

namespace InGame.Enemy
{
    /// <summary>
    /// 敵キャラクタースポーン地点シングルトン.
    /// シーンに配置済みならその位置を使用、なければ原点(0,0,0)で自動生成.
    /// </summary>
    public class EnemySpawnPointAttach : MonoBehaviour
    {
        private static EnemySpawnPointAttach instance;

        public static EnemySpawnPointAttach Instance()
        {
            if (instance == null)
            {
                // シーン上の既存インスタンスを探す.
                instance = FindFirstObjectByType<EnemySpawnPointAttach>();
            }
            if (instance == null)
            {
                // 見つからなければ原点に生成.
                var go = new GameObject(nameof(EnemySpawnPointAttach));
                instance = go.AddComponent<EnemySpawnPointAttach>();
            }
            return instance;
        }

        /// <summary>
        /// スポーン位置を取得.
        /// </summary>
        public Vector3 SpawnPosition => transform.position;

        private void Awake()
        {
            // シーンに配置済みの場合、instanceを自身に設定.
            if (instance == null)
            {
                instance = this;
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}