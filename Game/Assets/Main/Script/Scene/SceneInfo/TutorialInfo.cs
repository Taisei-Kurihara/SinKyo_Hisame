using Cysharp.Threading.Tasks;
using Common;
using InGame.Player;
using UnityEngine;

namespace SceneInfo
{
    public class TutorialInfo : ISceneInfo
    {
        string ISceneInfo.SceneName => "Tutorial";

        async UniTask ISceneInfo.Init()
        {
            Debug.Log("[TutorialInfo] Init開始");
            PlayerManager playerManager = PlayerManager.Instance();
            playerManager.pulseModel.ResetToBase();
            await playerManager.InstantiateCharacter("PlayerCharacter");
            Debug.Log("[TutorialInfo] PlayerCharacter生成完了");
        }

        UniTask ISceneInfo.End() => UniTask.CompletedTask;

        void ISceneInfo.InputStart()
        {
            InputSystemActionsManager.Instance().EnableUI();
        }

        void ISceneInfo.InputStop()
        {
        }
    }
}
