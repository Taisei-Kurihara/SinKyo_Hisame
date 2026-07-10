using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cysharp.Threading.Tasks;
using Common;

namespace InGame
{
    /// <summary>
    /// 血痕(BloodSplatter)のオブジェクトプール.
    /// Addressablesでプレハブを読み込み、プールして再利用する.
    /// </summary>
    public class BloodSplatterPool : SingletonMonoBase<BloodSplatterPool>
    {
        // プール数.
        private const int PoolSize = 16;

        // プール用リスト.
        private readonly List<GameObject> pool = new();

        // Addressableハンドル（解放用）.
        private AsyncOperationHandle<GameObject> prefabHandle;

        // 読み込み済みプレハブ.
        private GameObject loadedPrefab;

        // 初期化済みフラグ.
        private bool isInitialized = false;

        /// <summary>
        /// Addressablesからプレハブを読み込み、プールを生成.
        /// </summary>
        public async UniTask InitPool(string address)
        {
            if (isInitialized) return;

            try
            {
                prefabHandle = Addressables.LoadAssetAsync<GameObject>(address);
                loadedPrefab = await prefabHandle;

                if (prefabHandle.Status != AsyncOperationStatus.Succeeded || loadedPrefab == null)
                {
                    Debug.LogWarning($"[BloodSplatterPool] '{address}' の読み込みに失敗しました.");
                    if (prefabHandle.IsValid())
                        Addressables.Release(prefabHandle);
                    return;
                }

                for (int i = 0; i < PoolSize; i++)
                {
                    GameObject obj = Instantiate(loadedPrefab, transform);
                    obj.SetActive(false);
                    pool.Add(obj);
                }

                isInitialized = true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[BloodSplatterPool] '{address}' エラー: {e.GetType().Name}: {e.Message}");
            }
        }

        /// <summary>
        /// 血痕をスポーンする.
        /// </summary>
        /// <param name="position">表示位置 (World Space).</param>
        /// <param name="direction">DamageCounterの進行方向.</param>
        /// <param name="normal">地形法線方向.</param>
        public void Spawn(Vector3 position, Vector2 direction, Vector2 normal)
        {
            if (!isInitialized) return;

            // 非アクティブなオブジェクトを探す.
            GameObject obj = pool.Find(o => o != null && !o.activeInHierarchy);

            if (obj == null)
            {
                // プールに空きがなければ追加生成.
                obj = Instantiate(loadedPrefab, transform);
                pool.Add(obj);
            }

            obj.transform.position = new Vector3(position.x, position.y, 0f);
            obj.SetActive(true);

            var splatter = obj.GetComponent<BloodSplatter>();
            splatter?.Initialize(direction, normal);
        }

        /// <summary>
        /// プールとAddressableリソースを解放.
        /// </summary>
        public void ReleasePool()
        {
            foreach (var obj in pool)
            {
                if (obj != null) Destroy(obj);
            }
            pool.Clear();

            if (prefabHandle.IsValid())
            {
                Addressables.Release(prefabHandle);
                prefabHandle = default;
            }

            loadedPrefab = null;
            isInitialized = false;
        }

        private void OnDestroy()
        {
            ReleasePool();
        }
    }
}
