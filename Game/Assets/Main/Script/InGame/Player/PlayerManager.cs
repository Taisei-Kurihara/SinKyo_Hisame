using UnityEngine;
using Common;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace InGame.Player
{

    /// <summary>
    /// Playerのステータス情報は、こちらで管理することで、データを管理する。
    /// Presenter-シーン依存Model-View-GameObject実体はGameObjectとして
    /// 実際にPlayerにアタッチされているイメージで設計してしまう。
    /// 参照型として認識させる。
    /// </summary>
    public class PlayerManager : SingletonMonoBase<PlayerManager>
    {
        public PlayerStatusModel playerStatusModel { get; private set; }
        public PlayerStatusInitModel playerStatusInitModel { get; private set; }
            = new PlayerStatusInitModel();
        public PlayerAttackCommmanderDataModel playerAttackCommmanderDataModel { get; private set; }
        = new PlayerAttackCommmanderDataModel();

        // 鼓動ゲージ専用モデル.
        public PulseModel pulseModel { get; private set; }

        // 吸収ゲージモデル.
        public DrainModel drainModel { get; set; }

        // Addressable ハンドル保持（解放用）.
        private AsyncOperationHandle<GameObject> characterHandle;
        private GameObject characterInstance;

        public void Awake()
        {
            pulseModel = new PulseModel();
            playerStatusModel = new PlayerStatusModel(playerStatusInitModel,pulseModel);
            pulseModel.SetBreachingPoint(100f);
        }

        /// <summary>
        /// キャラクター設定
        /// </summary>
        /// <param name="characterAddress"></param>
        /// <param name="spawnPos"></param>
        /// <param name="autoSpawn"></param>
        /// <returns></returns>
        public async UniTask InstantiateCharacter(string characterAddress, Vector3? spawnPos = null, bool autoSpawn = false)
        {
            // 既存のハンドルを解放.
            ReleaseCharacter();

            characterHandle = Addressables.LoadAssetAsync<GameObject>(characterAddress);
            GameObject prefab = await characterHandle;

            if (characterHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"キャラクター '{characterAddress}' の読み込みに失敗しました.");
                return;
            }

            if (autoSpawn)
            {
                var spawnPoint = GameObject.FindFirstObjectByType<SpawnPointAttach>();
                spawnPos = spawnPoint != null ? spawnPoint.transform.position : Vector3.zero;
            }

            if (!spawnPos.HasValue) spawnPos = Vector3.zero;
            characterInstance = Object.Instantiate(prefab, spawnPos.Value, Quaternion.identity);
            // カメラを追従させる(シーン跨ぎではない）.
            CameraManager.Instance(false).SetFollowTarget(characterInstance.transform);
        }

        /// <summary>
        /// キャラクターリソースを解放.
        /// </summary>
        public void ReleaseCharacter()
        {
            if (characterInstance != null)
            {
                Object.Destroy(characterInstance);
                characterInstance = null;
            }
            if (characterHandle.IsValid())
            {
                Addressables.Release(characterHandle);
                characterHandle = default;
            }
        }

        private void OnDestroy()
        {
            ReleaseCharacter();
        }
    }
}