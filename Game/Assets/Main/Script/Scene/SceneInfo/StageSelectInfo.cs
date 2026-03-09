using UnityEngine;
using Common;
using Cysharp.Threading.Tasks;

namespace SceneInfo {
    /// <summary>
    /// ステージ選択画面
    /// </summary>
    public class StageSelectInfo : ISceneInfo
    {
        string ISceneInfo.SceneName => "StageSelect";

        UniTask ISceneInfo.End() => UniTask.CompletedTask;

        UniTask ISceneInfo.Init() => UniTask.CompletedTask;

        void ISceneInfo.InputStart()
        {
            InputSystemActionsManager manager=InputSystemActionsManager.Instance();
            //ステージセレクト画面の時はUIをOnに。
            manager.EnableUI();
        }

        void ISceneInfo.InputStop()
        {
            InputSystemActionsManager manager = InputSystemActionsManager.Instance();
            manager.UIDisable();
        }
    }
}
