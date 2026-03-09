using UnityEngine;
using Common;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace InGame.Enemy
{
    /// <summary>
    /// Enemyの生成・管理を行うシングルトン（PlayerManagerと同パターン）.
    /// </summary>
    public class EnemyManager : SingletonMonoBase<EnemyManager>
    {
        // Addressable ハンドル保持（解放用）.
        private AsyncOperationHandle<GameObject> enemyHandle;
        private GameObject enemyInstance;

        // 現在の敵インスタンス.
        public GameObject EnemyInstance => enemyInstance;

        /// <summary>
        /// PlayerPrefsからEnemyNameを読み取って敵を生成.
        /// </summary>
        public async UniTask InstantiateEnemyFromPrefs(Vector3? spawnPos = null, bool autoSpawn = false)
        {
            int enemyId = PlayerPrefs.GetInt("EnemyName", 0);
            EnemyName enemyName = (EnemyName)enemyId;

            if (enemyName == EnemyName.None)
            {
                Debug.LogWarning("[EnemyManager] EnemyName is None - 敵を生成しません.");
                return;
            }

            string address = enemyName.ToString(); // e.g. "Wendigo"
            await InstantiateEnemy(address, spawnPos, autoSpawn);
        }

        /// <summary>
        /// 指定アドレスで敵を生成（PlayerManager.InstantiateCharacterと同パターン）.
        /// </summary>
        public async UniTask InstantiateEnemy(string enemyAddress, Vector3? spawnPos = null, bool autoSpawn = false)
        {
            // 既存のハンドルを解放.
            ReleaseEnemy();

            enemyHandle = Addressables.LoadAssetAsync<GameObject>(enemyAddress);
            GameObject prefab = await enemyHandle;

            if (enemyHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"[EnemyManager] Enemy '{enemyAddress}' の読み込みに失敗しました.");
                return;
            }

            if (autoSpawn)
            {
                var spawnPoint = GameObject.FindFirstObjectByType<EnemySpawnPointAttach>();
                spawnPos = spawnPoint != null ? spawnPoint.transform.position : Vector3.zero;
            }

            if (!spawnPos.HasValue) spawnPos = Vector3.zero;
            enemyInstance = Object.Instantiate(prefab, spawnPos.Value, Quaternion.identity);
        }

        /// <summary>
        /// 敵リソースを解放.
        /// </summary>
        public void ReleaseEnemy()
        {
            if (enemyInstance != null)
            {
                Object.Destroy(enemyInstance);
                enemyInstance = null;
            }
            if (enemyHandle.IsValid())
            {
                Addressables.Release(enemyHandle);
                enemyHandle = default;
            }
        }

        private void OnDestroy()
        {
            ReleaseEnemy();
        }
    }
}
