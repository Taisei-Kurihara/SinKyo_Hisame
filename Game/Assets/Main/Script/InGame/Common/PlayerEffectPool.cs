using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cysharp.Threading.Tasks;
using Common;

namespace InGame
{
    /// <summary>
    /// プレイヤー状態エフェクトのオブジェクトプール.
    /// Addressablesでプレハブを読み込み、種類別にプールして再利用する.
    /// </summary>
    public class PlayerEffectPool : SingletonMonoBase<PlayerEffectPool>
    {
        // エフェクト種別ごとのプールデータ.
        private class EffectPoolData
        {
            public readonly List<GameObject> pool = new();
            public AsyncOperationHandle<GameObject> prefabHandle;
            public GameObject loadedPrefab;
            public bool isInitialized;
        }

        // 種別ごとのプール.
        private readonly Dictionary<string, EffectPoolData> pools = new();

        /// <summary>
        /// 指定アドレスのエフェクトプールを初期化.
        /// </summary>
        /// <param name="effectAddress">Addressablesアドレス.</param>
        /// <param name="poolSize">プール数.</param>
        public async UniTask InitPool(string effectAddress, int poolSize)
        {
            if (pools.ContainsKey(effectAddress) && pools[effectAddress].isInitialized)
            {
                Debug.Log($"[PlayerEffectPool] '{effectAddress}' は既に初期化済みのためスキップ.");
                return;
            }

            var data = new EffectPoolData();
            pools[effectAddress] = data;

            try
            {
                data.prefabHandle = Addressables.LoadAssetAsync<GameObject>(effectAddress);
                data.loadedPrefab = await data.prefabHandle;

                if (data.prefabHandle.Status != AsyncOperationStatus.Succeeded || data.loadedPrefab == null)
                {
                    Debug.LogWarning($"[PlayerEffectPool] エフェクト '{effectAddress}' の読み込みに失敗しました.");
                    if (data.prefabHandle.IsValid())
                    {
                        Addressables.Release(data.prefabHandle);
                    }
                    pools.Remove(effectAddress);
                    return;
                }

                // プール生成.
                for (int i = 0; i < poolSize; i++)
                {
                    GameObject obj = Instantiate(data.loadedPrefab, transform);
                    var renderer = obj.GetComponentInChildren<ParticleSystemRenderer>();
                    if (renderer != null)
                    {
                        renderer.sortingOrder = 100;
                    }
                    obj.SetActive(false);
                    data.pool.Add(obj);
                }

                data.isInitialized = true;
                Debug.Log($"[PlayerEffectPool] プール初期化完了 - '{effectAddress}' x{poolSize}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[PlayerEffectPool] エフェクト '{effectAddress}' の読み込み中にエラー: {e.GetType().Name}: {e.Message}");
            }
        }

        /// <summary>
        /// プールからエフェクトを取得してプレイヤー位置に再生.
        /// </summary>
        /// <param name="effectAddress">エフェクトのAddressablesアドレス.</param>
        /// <param name="position">表示位置.</param>
        /// <param name="followTarget">追従するTransform（nullなら追従しない）.</param>
        /// <summary>
        /// プールからエフェクトを取得してプレイヤー位置に再生.
        /// </summary>
        /// <param name="effectAddress">エフェクトのAddressablesアドレス.</param>
        /// <param name="position">表示位置.</param>
        /// <param name="followTarget">追従するTransform（nullなら追従しない）.</param>
        /// <param name="loop">trueの場合StopAllまで繰り返し再生する.</param>
        public void Spawn(string effectAddress, Vector3 position, Transform followTarget = null, bool loop = false)
        {
            if (!pools.TryGetValue(effectAddress, out var data) || !data.isInitialized)
            {
                Debug.LogWarning($"[PlayerEffectPool] Spawn失敗 - '{effectAddress}' プール未初期化.");
                return;
            }

            // 非アクティブなオブジェクトを探す.
            GameObject obj = data.pool.Find(o => o != null && !o.activeInHierarchy);

            if (obj == null)
            {
                // プールに空きがなければ追加生成.
                obj = Instantiate(data.loadedPrefab, transform);
                data.pool.Add(obj);
            }

            obj.transform.position = new Vector3(position.x, position.y, 0f);
            obj.SetActive(true);

            // パーティクルを停止→クリア→再生して確実にリスタート.
            var ps = obj.GetComponentInChildren<ParticleSystem>();
            if (ps != null)
            {
                // ループ設定を反映.
                var main = ps.main;
                main.loop = loop;

                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Clear(true);
                ps.Play(true);
            }

            if (loop)
            {
                // ループ時はStopAllまで追従のみ行い、自動返却しない.
                FollowUntilStopped(obj, followTarget).Forget();
            }
            else
            {
                // パーティクル終了後に自動返却（追従あり）.
                ReturnAfterEffect(obj, ps, followTarget).Forget();
            }
        }

        /// <summary>
        /// パーティクルシステムの再生完了後にプールへ返却.
        /// 追従先がある場合は毎フレーム位置を更新する.
        /// </summary>
        private async UniTaskVoid ReturnAfterEffect(GameObject obj, ParticleSystem ps, Transform followTarget)
        {
            float waitTime = 1f;
            if (ps != null)
            {
                waitTime = ps.main.duration + ps.main.startLifetime.constantMax;
            }

            float elapsed = 0f;
            while (elapsed < waitTime)
            {
                if (obj == null) return;

                // 追従対象がある場合は位置を更新.
                if (followTarget != null)
                {
                    obj.transform.position = new Vector3(followTarget.position.x, followTarget.position.y, 0f);
                }

                elapsed += Time.deltaTime;
                await UniTask.Yield(cancellationToken: this.destroyCancellationToken);
            }

            if (obj != null)
            {
                obj.SetActive(false);
            }
        }

        /// <summary>
        /// ループエフェクト用: StopAllで非アクティブにされるまで追従のみ行う.
        /// </summary>
        private async UniTaskVoid FollowUntilStopped(GameObject obj, Transform followTarget)
        {
            while (obj != null && obj.activeInHierarchy)
            {
                if (followTarget != null)
                {
                    obj.transform.position = new Vector3(followTarget.position.x, followTarget.position.y, 0f);
                }
                await UniTask.Yield(cancellationToken: this.destroyCancellationToken);
            }
        }

        /// <summary>
        /// エフェクトを即座に停止して返却.
        /// </summary>
        /// <param name="effectAddress">エフェクトのAddressablesアドレス.</param>
        public void StopAll(string effectAddress)
        {
            if (!pools.TryGetValue(effectAddress, out var data)) return;

            foreach (var obj in data.pool)
            {
                if (obj != null && obj.activeInHierarchy)
                {
                    var ps = obj.GetComponentInChildren<ParticleSystem>();
                    if (ps != null)
                    {
                        // ループ設定を解除してから停止.
                        var main = ps.main;
                        main.loop = false;
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }
                    obj.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 全プールとAddressableリソースを解放.
        /// </summary>
        public void ReleaseAll()
        {
            foreach (var kvp in pools)
            {
                var data = kvp.Value;
                foreach (var obj in data.pool)
                {
                    if (obj != null) Destroy(obj);
                }
                data.pool.Clear();

                if (data.prefabHandle.IsValid())
                {
                    Addressables.Release(data.prefabHandle);
                    data.prefabHandle = default;
                }
                data.loadedPrefab = null;
                data.isInitialized = false;
            }
            pools.Clear();
        }

        private void OnDestroy()
        {
            ReleaseAll();
        }
    }
}
