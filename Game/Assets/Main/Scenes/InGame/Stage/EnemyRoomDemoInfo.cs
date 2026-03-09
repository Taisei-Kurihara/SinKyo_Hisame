using UnityEngine;

using Common;
using Cysharp.Threading.Tasks;
using InGame.Player;
using Setting;

namespace SceneInfo
{
    public class EnemyRoomDemoInfo : ISceneInfo
    {
        public string SceneName => "EnemyRoom_Demo";

        public UniTask End() => UniTask.CompletedTask;

        public async UniTask Init()
        {
            PlayerManager playerManager = PlayerManager.Instance();
            await playerManager.InstantiateCharacter("PlayerCharacter");

            // BGM再生（ビルド版ではawaitしないとAddressables読み込み完了前にフローが終了する）.
            await AudioManager.Instance().LoadBgm("BGM_EnemyRoom");
        }

        public void InputStart()
        {
        }

        public void InputStop()
        {
        }
        void Start()
        {

        }

        void Update()
        {

        }
    }
}