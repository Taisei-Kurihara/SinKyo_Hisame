using Common;
using Cysharp.Threading.Tasks;
using InGame.Player;
using Tutorial;
using UnityEngine;

namespace SceneInfo
{
    /// <summary>
    /// チュートリアルステージシーン用 ISceneInfo.
    /// SceneManager.LoadMainScene(new TutorialStageInfo()) で読み込む.
    /// シーン名: "TutorialStage"
    /// </summary>
    public class TutorialStageInfo : ISceneInfo
    {
        string ISceneInfo.SceneName => "TutorialStage";

        async UniTask ISceneInfo.Init()
        {
            Debug.Log("[TutorialStageInfo] Init開始");

            // プレイヤー生成・心拍数リセット.
            PlayerManager playerManager = PlayerManager.Instance();
            playerManager.pulseModel.ResetToBase();
            await playerManager.InstantiateCharacter("PlayerCharacter");

            // チュートリアルを最初のページから開始（シーン遷移モード）.
            TutorialManager.Instance().StartTutorial(withSceneTransition: true);

            Debug.Log("[TutorialStageInfo] Init完了");
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
