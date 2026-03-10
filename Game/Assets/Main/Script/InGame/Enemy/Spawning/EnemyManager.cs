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

        // EnemyView関連.
        private AsyncOperationHandle<GameObject> enemyViewHandle;
        private GameObject enemyViewInstance;

        // EnemyUIView参照（EnemyPresenterから取得可能）.
        private EnemyUIView enemyUIView;
        public EnemyUIView EnemyUIView => enemyUIView;

        // 現在の敵インスタンス.
        public GameObject EnemyInstance => enemyInstance;

        /// <summary>
        /// PlayerPrefsからEnemyNameを読み取って敵を生成.
        /// </summary>
        public async UniTask InstantiateEnemyFromPrefs(Vector3? spawnPos = null, bool autoSpawn = false)
        {
            int enemyId = PlayerPrefs.GetInt("EnemyName", 0);
            EnemyName enemyName = (EnemyName)enemyId;

            // 取得後に破棄.
            PlayerPrefs.DeleteKey("EnemyName");
            PlayerPrefs.Save();

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

            // EnemyViewを先にロードしてUIを準備.
            await SetupEnemyView();

            enemyHandle = Addressables.LoadAssetAsync<GameObject>(enemyAddress);
            GameObject prefab = await enemyHandle;

            if (enemyHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"[EnemyManager] Enemy '{enemyAddress}' の読み込みに失敗しました.");
                return;
            }

            if (autoSpawn)
            {
                spawnPos = EnemySpawnPointAttach.Instance().SpawnPosition;
            }

            if (!spawnPos.HasValue) spawnPos = Vector3.zero;
            enemyInstance = Object.Instantiate(prefab, spawnPos.Value, Quaternion.identity);
            Debug.Log($"[EnemyManager] Enemy '{enemyAddress}' 生成完了 - EnemyUIView: {(enemyUIView != null ? "Ready" : "null")}");
        }

        /// <summary>
        /// EnemyView（HP・名称のUIキャンバス）をロードしてEnemyUIViewを準備.
        /// </summary>
        private async UniTask SetupEnemyView()
        {
            // 既存のEnemyViewを解放.
            ReleaseEnemyView();

            enemyViewHandle = Addressables.LoadAssetAsync<GameObject>("EnemyView");
            GameObject prefab = await enemyViewHandle;

            if (enemyViewHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError("[EnemyManager] EnemyView の読み込みに失敗しました.");
                return;
            }

            enemyViewInstance = Object.Instantiate(prefab);

            // EnemyUI_View_SetterをEnemyUIViewにセット.
            var setter = enemyViewInstance.GetComponent<EnemyUI_View_Setter>();
            if (setter == null)
            {
                setter = enemyViewInstance.GetComponentInChildren<EnemyUI_View_Setter>();
            }

            if (setter != null)
            {
                // EnemyUIViewがシーンになければ、EnemyView上に追加.
                enemyUIView = Object.FindFirstObjectByType<EnemyUIView>();
                if (enemyUIView == null)
                {
                    enemyUIView = enemyViewInstance.AddComponent<EnemyUIView>();
                }
                enemyUIView.SetSetter = setter;
                Debug.Log($"[EnemyManager] EnemyUIView準備完了 - IsSetterReady: {enemyUIView.IsSetterReady}");
            }
            else
            {
                Debug.LogError("[EnemyManager] EnemyUI_View_Setter が EnemyView上に見つかりません.");
            }
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
            ReleaseEnemyView();
        }

        /// <summary>
        /// EnemyViewリソースを解放.
        /// </summary>
        private void ReleaseEnemyView()
        {
            if (enemyViewInstance != null)
            {
                Object.Destroy(enemyViewInstance);
                enemyViewInstance = null;
            }
            if (enemyViewHandle.IsValid())
            {
                Addressables.Release(enemyViewHandle);
                enemyViewHandle = default;
            }
            enemyUIView = null;
        }

        private void OnDestroy()
        {
            ReleaseEnemy();
        }
    }
}