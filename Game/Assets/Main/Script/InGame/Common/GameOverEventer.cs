using Common;
using Cysharp.Threading.Tasks;
using SceneInfo;
using UnityEngine;
using UnityEngine.UI;

namespace InGame.Common
{
    /// <summary>
    /// ゲームオーバー画面のボタン/表示管理.
    /// ButtonEventerを継承し、Inspectorで設定した子Objを管理する.
    /// </summary>
    public class GameOverEventer : ButtonEventer
    {
        // Inspectorで設定する子Obj.
        [SerializeField] private GameObject winObj;
        [SerializeField] private GameObject loseObj;
        [SerializeField] private Button returnTitleButton;

        protected override void Init()
        {
            // 初期状態は非表示.
            if (winObj != null) winObj.SetActive(false);
            if (loseObj != null) loseObj.SetActive(false);
            if (returnTitleButton != null) returnTitleButton.gameObject.SetActive(false);

            // ボタン配列登録.
            if (returnTitleButton != null)
            {
                buttons = new Button[][]
                {
                    new Button[] { returnTitleButton }
                };
            }
            else
            {
                Debug.LogWarning("[GameOverEventer] ReturnTitleボタンが見つかりません.");
            }
        }

        protected override void ButtonEvents(Button button)
        {
            if (button == returnTitleButton)
            {
                ReturnToTitle();
            }
        }

        /// <summary>
        /// 勝敗結果を表示.
        /// </summary>
        /// <param name="isVictory">勝利の場合true.</param>
        public void ShowResult(bool isVictory)
        {
            if (isVictory)
            {
                if (winObj != null) winObj.SetActive(true);
                Debug.Log("[GameOverEventer] Win表示");
            }
            else
            {
                if (loseObj != null) loseObj.SetActive(true);
                Debug.Log("[GameOverEventer] Lose表示");
            }

            // ボタン表示.
            if (returnTitleButton != null) returnTitleButton.gameObject.SetActive(true);
        }

        /// <summary>
        /// タイトルへ戻る.
        /// </summary>
        public void ReturnToTitle()
        {
            Debug.Log("[GameOverEventer] タイトルへ戻る");
            SceneManager.Instance().LoadMainScene(new TitleSceneInfo()).Forget();
        }

    }
}
