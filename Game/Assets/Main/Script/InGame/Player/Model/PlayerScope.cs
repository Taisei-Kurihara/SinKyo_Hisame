using System.Collections.Generic;
using Common;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using VContainer;
using VContainer.Unity;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using InGame.Player.Animation;
using InGame.Common;

namespace InGame.Player
{
    public class playerData
    {
        public string playerPath;
        public string playerViewPath;
    }

    /// <summary>
    /// プレイヤー:一体につきManager一体用意する（シングルトンの為今は1体想定。
    /// </summary>
    public class PlayerScope : LifetimeScope
    {
        public GameObject PlayerCharacter { get; private set; }


        public PlayerControllModel playerControllModel { get; private set; }
        public PlayerSearchModel playerSearchModel { get; private set; }
        public DrainModel drainModel { get; private set; }

        private PlayerPresenter playerPresenter;
        private PlayerAttackCommanderBase playerAttackCommander;

        //アニメーションをInterface定義で行ってしまうことで、VContainerでDIできるようにする。
        private IPlayerAnimation playerAnimation;


        // View部分は再生成が多い為.
        private GameObject playerView;
        private GameObject gameOverViewObj;

        // Addressable ハンドル保持（解放用）.
        private AsyncOperationHandle<GameObject> playerViewHandle;
        private AsyncOperationHandle<GameObject> gameOverViewHandle;

        /// <summary>
        /// GameOverViewの参照を取得.
        /// </summary>
        public InGame.Common.GameOverView GameOverView => gameOverViewObj?.GetComponent<InGame.Common.GameOverView>();

        protected override void Awake()
        {
            autoInjectGameObjects = new List<GameObject> { gameObject };

            base.Awake();
            playerControllModel = Container.Resolve<PlayerControllModel>();
            playerPresenter = Container.Resolve<PlayerPresenter>();
            playerAttackCommander = Container.Resolve<PlayerAttackCommanderBase>();
            playerSearchModel = Container.Resolve<PlayerSearchModel>();
            drainModel = Container.Resolve<DrainModel>();

            // PlayerManagerにdrainModelを設定.
            PlayerManager.Instance().drainModel = drainModel;

            // ゲージ3個分で回復ポイント1に自動変換するイベント登録.
            var statusModel = PlayerManager.Instance().playerStatusModel;
            drainModel.RegisterGageEvent(
                () => statusModel.healPoint.Value <= 2 && drainModel.CanConsumeGages(3),
                () => { drainModel.ConsumeGages(3); statusModel.healPoint.Value++; }
            );

            InitializePlayer().Forget();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            //共通Object
            builder.RegisterInstance(gameObject);
            Rigidbody2D _rb = gameObject.GetComponent<Rigidbody2D>();
            builder.RegisterInstance(_rb);
            Animator animator = gameObject.GetComponent<Animator>();
            builder.RegisterInstance(animator);
            //クラス登録
            builder.Register<DrainModel>(Lifetime.Scoped);
            builder.Register<PlayerControllModel>(Lifetime.Scoped);
            builder.Register<PlayerPresenter>(Lifetime.Scoped);
            builder.Register<PlayerAttackCommanderBase>(Lifetime.Scoped);
            builder.Register<PlayerSearchModel>(Lifetime.Scoped);
            // ガード登録.
            builder.Register<Guard_Player_Default>(Lifetime.Scoped).As<IGuard>();
        }

        /// <summary>
        /// キャラクター生成とView生成を呼び出す.
        /// View読み込みが失敗してもステータス初期化とプレイヤー有効化は必ず実行する.
        /// </summary>
        public async UniTask InitializePlayer(Vector3? spawnPos = null, string viewAddress=null)
        {
            // PlayerView、GameOverViewを同時に読み込み（失敗しても初期化は継続）.
            try
            {
                await UniTask.WhenAll(
                    InstantiateView("PlayerView"),
                    InstantiateGameOverView("GameOverView")
                );
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[PlayerScope] View読み込み中にエラーが発生しましたが、初期化を継続します: {e.Message}");
            }

            // GameOverViewの参照をPlayerPresenterに設定.
            playerPresenter.SetGameOverView(GameOverView);

            // ステータスを初期化.
            playerControllModel.Initialize();

            playerPresenter.SetPlayerEnable(true);
        }

        /// <summary>
        /// View生成
        /// </summary>
        /// <param name="viewAddress"></param>
        /// <returns></returns>
        private async UniTask InstantiateView(string viewAddress)
        {
            // 既存のハンドルを解放.
            ReleasePlayerView();

            playerViewHandle = Addressables.LoadAssetAsync<GameObject>(viewAddress);
            GameObject prefab = await playerViewHandle;

            if (playerViewHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"PlayerView '{viewAddress}' の読み込みに失敗しました.");
                return;
            }

            playerView = Object.Instantiate(prefab);

            var view = playerView.GetComponent<IPlayerView>();
            playerPresenter.GetView(view);
            playerPresenter.EventBattleView();
        }

        /// <summary>
        /// PlayerViewリソースを解放.
        /// </summary>
        private void ReleasePlayerView()
        {
            if (playerView != null)
            {
                Object.Destroy(playerView);
                playerView = null;
            }
            if (playerViewHandle.IsValid())
            {
                Addressables.Release(playerViewHandle);
                playerViewHandle = default;
            }
        }

        /// <summary>
        /// GameOverView生成.
        /// </summary>
        /// <param name="viewAddress">GameOverViewのAddressableアドレス.</param>
        private async UniTask InstantiateGameOverView(string viewAddress)
        {
            ReleaseGameOverView();

            try
            {
                gameOverViewHandle = Addressables.LoadAssetAsync<GameObject>(viewAddress);
                GameObject prefab = await gameOverViewHandle;

                if (gameOverViewHandle.Status != AsyncOperationStatus.Succeeded)
                {
                    Debug.LogWarning($"GameOverView '{viewAddress}' の読み込みに失敗しました.");
                    return;
                }

                gameOverViewObj = Object.Instantiate(prefab);
                // 初期状態は非表示（全Awake完了後に無効化）.
                gameOverViewObj.SetActive(false);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"GameOverView '{viewAddress}' の読み込み中にエラーが発生しましたが、処理を継続します: {e.Message}");
            }
        }

        /// <summary>
        /// GameOverViewリソースを解放.
        /// </summary>
        private void ReleaseGameOverView()
        {
            if (gameOverViewObj != null)
            {
                Object.Destroy(gameOverViewObj);
                gameOverViewObj = null;
            }
            if (gameOverViewHandle.IsValid())
            {
                Addressables.Release(gameOverViewHandle);
                gameOverViewHandle = default;
            }
        }

        /// <summary>
        /// 外部からダメージを受ける（Enemy等から呼び出し用）.
        /// </summary>
        /// <param name="damageData">ダメージデータ.</param>
        /// <returns>ガード状態.</returns>
        public GuardState OnReceiveAttack(DamageData damageData)
        {
            return playerPresenter.OnReceiveAttack(damageData);
        }

        /// <summary>
        /// 外部からダメージを受ける（後方互換用）.
        /// </summary>
        /// <param name="damage">ダメージ量.</param>
        /// <returns>ガード状態.</returns>
        public GuardState OnReceiveAttack(int damage)
        {
            return playerPresenter.OnReceiveAttack(damage);
        }

        /// <summary>
        /// 破棄時処理 - InputSystemリーク防止.
        /// </summary>
        protected override void OnDestroy()
        {
            playerPresenter?.Dispose();
            ReleasePlayerView();
            ReleaseGameOverView();
            base.OnDestroy();
        }
    }
}