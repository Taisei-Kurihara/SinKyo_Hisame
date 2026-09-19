using Cysharp.Threading.Tasks;
using Common;
using InGame.Player;
using Setting;
using Tutorial;
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

            // View登録完了を明示的に待機.
            var tutorialManager = TutorialManager.Instance();
            await tutorialManager.WaitForView();

            // チュートリアルシーンとして開始（2秒待機→window表示）.
            tutorialManager.StartTutorial(withSceneTransition: true);

            // チュートリアルBGM再生.
            await AudioManager.Instance().LoadBgm("BGM_チュートリアル");
            AudioManager.Instance().SetBgmVolume(50);

            Debug.Log("[TutorialInfo] Init完了");
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
