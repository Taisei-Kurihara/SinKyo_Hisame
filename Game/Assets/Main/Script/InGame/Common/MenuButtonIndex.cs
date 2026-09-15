using System;
using TMPro;
using UnityEngine.UI;

namespace InGame.Common
{
    /// <summary>
    /// ポーズメニューのボタン種別.
    /// </summary>
    public enum MenuButtonIndex
    {
        None = -1,  // 未設定・無効スロット用センチネル値（負値の追加はここから）.

        Resume = 0,
        Tutorial = 1,
        Setting = 2,
        Quit = 3,
        Start = 4,
        Credit = 5,
        Language = 6,

        // ---- タイトル画面専用 ----
        TitleTutorial  = 7,  // チュートリアルボタン
        TitleGameStart = 8,  // ゲーム開始ボタン
        TitleGameQuit  = 9,  // ゲーム終了ボタン

    }

    /// <summary>
    /// Inspector で MenuButton の縦一列（Y軸）を定義するラッパー.
    /// MenuButtonEventer の buttons[x] に対応する.
    /// </summary>
    [Serializable]
    public class MenuButtonRow
    {
        /// <summary>
        /// 2D 配列内の X 軸インデックス（左から 0 始まり・列内一意）.
        /// 辞書キーとして使用するため他の列と被らないように設定する.
        /// </summary>
        public int xIndex;
        public MenuButton[] buttons;
    }

    /// <summary>
    /// Unity.UI.Button と MenuButtonIndex を紐づけるデータクラス.
    /// PauseMenuEventer の Inspector で設定する.
    /// </summary>
    [Serializable]
    public class MenuButton
    {
        public Button button;
        public MenuButtonIndex index;
        /// <summary>
        /// 行内での X 軸位置（左から 0 始まり・行内一意）.
        /// 辞書キーとして使用するため同一行内で被らないように設定する.
        /// </summary>
        public int xIndex;
        /// <summary>
        /// ボタン内のラベルテキスト（言語切り替え時に書き換える）.
        /// Inspector で設定するか、InitMenuButtonLabel() で自動取得する.
        /// </summary>
        public TextMeshProUGUI labelText;

        /// <summary>
        /// labelText が未設定の場合、button 子オブジェクトから TextMeshProUGUI を自動取得する.
        /// </summary>
        public void InitMenuButtonLabel()
        {
            if (labelText == null && button != null)
                // includeInactive=true: 親(menuWindow等)が非アクティブでも子を検索する.
                labelText = button.GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }
}
