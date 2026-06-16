using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cysharp.Threading.Tasks;
using Common;
using InGame.Player;

namespace InGame
{
    /// <summary>
    /// ダメージカウンターのオブジェクトプール.
    /// Addressablesでプレハブを読み込み、プールして再利用する.
    /// </summary>
    public class DamageCounterPool : SingletonMonoBase<DamageCounterPool>
    {
        // プール数.
        private const int PoolSize = 24;

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
                    Debug.LogWarning($"[DamageCounterPool] '{address}' の読み込みに失敗しました.");
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
                Debug.LogWarning($"[DamageCounterPool] '{address}' エラー: {e.GetType().Name}: {e.Message}");
            }
        }

        /// <summary>
        /// ダメージカウンターをスポーンする.
        /// </summary>
        // --- スポーン位置ランダムオフセット ---
        [Header("Spawn Offset")]
        [Tooltip("ランダムオフセットの最小距離")]
        [SerializeField] private float spawnOffsetMin = 0.1f;
        [Tooltip("ランダムオフセットの最大距離")]
        [SerializeField] private float spawnOffsetMax = 0.5f;

        public void Spawn(Vector3 position, float damage, PlayerAttackType attackType,
                          bool facingRight = true, float additionalAngle = 0f)
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

            // 親子がterrain等に変わっている場合、Pool配下に戻す.
            if (obj.transform.parent != transform)
                obj.transform.SetParent(transform);

            // ランダム角 + ランダム距離でオフセット.
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float dist = Random.Range(spawnOffsetMin, spawnOffsetMax);
            Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;

            obj.transform.position = new Vector3(position.x + offset.x, position.y + offset.y, 0f);
            obj.SetActive(true);

            var counter = obj.GetComponent<DamageCounter>();
            counter?.Initialize(damage, attackType, facingRight, additionalAngle);
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
