using Common;
using Cysharp.Threading.Tasks;
using Setting;
using UnityEngine;

namespace SceneInfo
{
    /// <summary>
    /// タイトル画面シーン情報.
    /// </summary>
    public class TitleSceneInfo : ISceneInfo
    {
        string ISceneInfo.SceneName => "Title";

        UniTask ISceneInfo.End()
        {
            AudioManager.Instance()?.StopBgm();
            return UniTask.CompletedTask;
        }

        async UniTask ISceneInfo.Init()
        {
            Debug.Log("[TitleSceneInfo] Init.");

            // セーブデータ初期化（強化状態 + クリア状況ロード）.
            SaveDataManager.Instance.Initialize();

            // フェードを事前ロード（初回シーン遷移時の遅延防止）.
            // バックグラウンドで実行し、完了前にボタンが押されても StartFadeIn が通常ロードで対応する.
            SceneManager.Instance().PrewarmFadeAsync().Forget();

            // タイトルBGM再生（Addressablesキー: "BGM_タイトル"）.
            await AudioManager.Instance().LoadBgm("BGM_タイトル");
            AudioManager.Instance().SetBgmVolume(50);
        }

        void ISceneInfo.InputStart()
        {
        }

        void ISceneInfo.InputStop()
        {
        }
    }
}
