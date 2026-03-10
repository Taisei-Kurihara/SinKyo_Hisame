using UnityEngine;
using GameCommon;
using Common;
using InGame.Enemy;
using SceneInfo;
using Cysharp.Threading.Tasks;

namespace GameEventPoint
{

    public class StageSelectEventPointAttach : EventPointAbstract
    {
        public override void OnEvent()
        {
            // 敵をWindigoに設定（とりあえず固定）.
            PlayerPrefs.SetInt("EnemyName", (int)EnemyName.Wendigo);
            PlayerPrefs.Save();

            // MainSceneInfo で敵生成を含むシーンをロード.
            SceneManager.Instance().LoadMainScene(new MainSceneInfo()).Forget();
        }
    }
}