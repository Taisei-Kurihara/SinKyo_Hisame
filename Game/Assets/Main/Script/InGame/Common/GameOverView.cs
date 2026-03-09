using UnityEngine;

namespace InGame.Common
{
    /// <summary>
    /// ゲームオーバー画面表示用View.
    /// ゲーム開始時にAddressablesで読み込み、非表示で保持.
    /// ゲーム終了時にGameOverEventerを通じて結果表示とボタン操作を行う.
    /// </summary>
    public class GameOverView : MonoBehaviour
    {
        // GameOverEventer参照.
        private GameOverEventer eventer;

        // ゲームごとに一回のみ実行するためのフラグ.
        private bool hasShown = false;

        private void Awake()
        {
            eventer = GetComponent<GameOverEventer>();
            if (eventer == null)
            {
                Debug.LogWarning("[GameOverView] GameOverEventerが見つかりません.");
            }
        }

        /// <summary>
        /// ゲームオーバー画面を表示.
        /// </summary>
        /// <param name="isVictory">勝利の場合true.</param>
        public void Show(bool isVictory)
        {
            // ゲームごとに一回のみ.
            if (hasShown) return;
            hasShown = true;

            gameObject.SetActive(true);

            if (eventer != null)
            {
                eventer.ShowResult(isVictory);
            }
        }

        /// <summary>
        /// ゲームオーバー画面を非表示.
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
