using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cysharp.Threading.Tasks;
using Common;

namespace InGame
{
    /// <summary>
    /// ヒットエフェクトのオブジェクトプール.
    /// Addressablesでプレハブを読み込み、指定数をプールして再利用する.
    /// </summary>
    public class HitEffectPool : SingletonMonoBase<HitEffectPool>
    {
        // プール数.
        private const int PoolSize = 5;

        // プール用リスト.
        private readonly List<GameObject> pool = new();

        // Addressableハンドル（解放用）.
        private AsyncOperationHandle<GameObject> prefabHandle;

        // 読み込み済みプレハブ.
        private GameObject loadedPrefab;

        // 初期化済みフラグ.
        private bool isInitialized = false;

        /// <summary>
        /// Addressablesからエフェクトプレハブを読み込み、プールを生成.
        /// </summary>
        public async UniTask InitPool(string effectAddress)
        {
            if (isInitialized)
            {
                Debug.Log($"[HitEffectPool] 既に初期化済みのためスキップ.");
                return;
            }

            Debug.Log($"[HitEffectPool] InitPool開始 - アドレス: '{effectAddress}'");

            try
            {
                Debug.Log($"[HitEffectPool] Addressables読み込み開始 - '{effectAddress}'");
                prefabHandle = Addressables.LoadAssetAsync<GameObject>(effectAddress);
                loadedPrefab = await prefabHandle;

                if (prefabHandle.Status != AsyncOperationStatus.Succeeded || loadedPrefab == null)
                {
                    Debug.LogWarning($"[HitEffectPool] エフェクト '{effectAddress}' の読み込みに失敗しました. Status: {prefabHandle.Status}");
                    if (prefabHandle.IsValid())
                    {
                        Addressables.Release(prefabHandle);
                    }
                    return;
                }

                Debug.Log($"[HitEffectPool] Addressables読み込み成功 - '{effectAddress}', Prefab: {loadedPrefab.name}");

                // プール生成.
                for (int i = 0; i < PoolSize; i++)
                {
                    GameObject obj = Instantiate(loadedPrefab, transform);
                    // ソート順を事前設定.
                    var renderer = obj.GetComponentInChildren<ParticleSystemRenderer>();
                    if (renderer != null)
                    {
                        renderer.sortingOrder = 100;
                    }
                    obj.SetActive(false);
                    pool.Add(obj);
                    Debug.Log($"[HitEffectPool] プールオブジェクト生成 [{i + 1}/{PoolSize}] - {obj.name}");
                }

                isInitialized = true;
                Debug.Log($"[HitEffectPool] プール初期化完了 - '{effectAddress}' x{PoolSize}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[HitEffectPool] エフェクト '{effectAddress}' の読み込み中にエラー: {e.GetType().Name}: {e.Message}");
            }
        }

        /// <summary>
        /// プールからエフェクトを取得して指定位置・方向にパーティクルを再生.
        /// </summary>
        /// <param name="position">エフェクト表示位置.</param>
        /// <param name="facingRight">攻撃方向が右ならtrue（エフェクト初期方向が右）.</param>
        public void Spawn(Vector3 position, bool facingRight = true, float additionalAngle = 0f)
        {
            if (!isInitialized)
            {
                Debug.LogWarning($"[HitEffectPool] Spawn失敗 - プール未初期化.");
                return;
            }

            // 非アクティブなオブジェクトを探す.
            GameObject obj = pool.Find(o => o != null && !o.activeInHierarchy);

            if (obj == null)
            {
                // プールに空きがなければ追加生成.
                obj = Instantiate(loadedPrefab, transform);
                pool.Add(obj);
                Debug.Log($"[HitEffectPool] プール不足のため追加生成 - 現在プール数: {pool.Count}");
            }

            // 2Dゲーム用にZ座標を0に固定.
            obj.transform.position = new Vector3(position.x, position.y, 0f);

            // 攻撃方向に合わせてZ軸回転で反転（初期方向は右）+ 時計回りに45度 + 追加角度.
            float zRotation = facingRight ? -45f + additionalAngle : 180f - 45f - additionalAngle;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, zRotation);

            obj.SetActive(true);

            // パーティクルを停止→クリア→再生して確実にリスタート.
            var ps = obj.GetComponentInChildren<ParticleSystem>();
            if (ps != null)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Clear(true);
                ps.Play(true);

                // ソート順をスプライトの手前に設定.
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                {
                    renderer.sortingOrder = 100;
                }
            }

            // パーティクル終了後に自動返却.
            ReturnAfterEffect(obj, ps).Forget();
        }

        /// <summary>
        /// パーティクルシステムの再生完了後にプールへ返却.
        /// </summary>
        private async UniTaskVoid ReturnAfterEffect(GameObject obj, ParticleSystem ps)
        {
            if (ps != null)
            {
                // メインモジュールのdurationとstartLifetimeから待機時間を算出.
                float waitTime = ps.main.duration + ps.main.startLifetime.constantMax;
                await UniTask.Delay((int)(waitTime * 1000), cancellationToken: this.destroyCancellationToken);
            }
            else
            {
                // ParticleSystemがない場合は1秒後に返却.
                await UniTask.Delay(1000, cancellationToken: this.destroyCancellationToken);
            }

            if (obj != null)
            {
                obj.SetActive(false);
            }
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
