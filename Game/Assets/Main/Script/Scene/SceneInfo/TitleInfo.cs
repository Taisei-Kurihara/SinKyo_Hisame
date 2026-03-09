using Cysharp.Threading.Tasks;
using Setting;
using UnityEngine;
using Common;

namespace SceneInfo{
    public class TitleInfo : ISceneInfo
    {
        string ISceneInfo.SceneName => "Title";

        UniTask ISceneInfo.Init()
        {
            AudioManager.Instance();
            return UniTask.CompletedTask;
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