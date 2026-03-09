using UnityEngine;
using Common;
using Cysharp.Threading.Tasks;

namespace SceneInfo
{
    public class PlayerUI : ISceneInfo
    {
        public string SceneName => "";

        public UniTask End() => UniTask.CompletedTask;

        public UniTask Init() => UniTask.CompletedTask;

        public void InputStart()
        {
        }

        public void InputStop()
        {
        }
    }
}