using UnityEngine;
using GameCommon;
using Common;
using SceneInfo;
using Cysharp.Threading.Tasks;

namespace GameEventPoint
{

    public class StageSelectEventPointAttach : EventPointAbstract
    {
        public override void OnEvent()
        {
            SceneManager.Instance().LoadMainScene(new BaseHomeInfo()).Forget();
        }
    }
}