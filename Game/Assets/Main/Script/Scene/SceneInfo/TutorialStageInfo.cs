using Common;
using Cysharp.Threading.Tasks;
using InGame.Player;
using Setting;
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

            // チュートリアルBGM再生.
            await AudioManager.Instance().LoadBgm("BGM_チュートリアル");
            AudioManager.Instance().SetBgmVolume(50);

            Debug.Log("[TutorialStageInfo] Init完了");
        }

        UniTask ISceneInfo.End()
        {
            AudioManager.Instance()?.StopBgm();
            return UniTask.CompletedTask;
        }

        void ISceneInfo.InputStart()
        {
            InputSystemActionsManager.Instance().EnableUI();
        }

        void ISceneInfo.InputStop()
        {
        }
    }
}
