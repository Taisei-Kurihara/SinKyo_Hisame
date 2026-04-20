using Common;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace SceneInfo
{
    /// <summary>
    /// タイトル画面シーン情報.
    /// </summary>
    public class TitleSceneInfo : ISceneInfo
    {
        string ISceneInfo.SceneName => "Title";

        UniTask ISceneInfo.End() => UniTask.CompletedTask;

        UniTask ISceneInfo.Init()
        {
            Debug.Log("[TitleSceneInfo] Init.");

            // セーブデータ初期化（強化状態 + クリア状況ロード）.
            SaveDataManager.Instance.Initialize();

            return UniTask.CompletedTask;
        }

        void ISceneInfo.InputStart()
        {
        }

        void ISceneInfo.InputStop()
        {
        }
    }
}
