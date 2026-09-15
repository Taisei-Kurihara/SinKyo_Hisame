using Common;
using Cysharp.Threading.Tasks;
using SceneInfo;
using System.Collections.Generic;
using Tutorial;
using UnityEngine;
using UnityEngine.UI;

namespace InGame.Common
{
    public class PauseMenuEventer : MenuButtonEventer
    {

        [Header("メニューボタン（Inspector設定）")]
        [SerializeField] private MenuButton[] menuButtons;

        [Header("メニューウィンドウ（表示/非表示対象）")]
        [SerializeField] private GameObject menuWindow;

        [Header("タイトルテキスト")]
        [SerializeField] private TMPro.TextMeshProUGUI titleText;

        // メニュー表示状態.
        private bool isMenuVisible = false;

        // メニューからチュートリアルを開いた状態.
        private bool isTutorialFromMenu = false;

        // ShowMenu()を呼んだフレームでLateUpdateのCancel処理をスキップするためのフラグ.
        private int showMenuFrame = -1;

        // 参照.
        private TutorialManager tutorialManager;
        private InputSystem_Actions inputActions;

        // buttonsSlot 実装（abstract 強制）.
        private ButtonSlotDictionary _buttonsSlot;
        protected override ButtonSlotDictionary buttonsSlot => _buttonsSlot;

        // タイトルテキスト翻訳 { JP, EN }.
        private static readonly string[] titleLabels = { "ポーズ", "Pause" };

        // ボタンラベル辞書（MenuButtonIndex → { JP文字列, EN文字列 }）.
        private static readonly Dictionary<MenuButtonIndex, string[]> pauseLabelMap =
            new Dictionary<MenuButtonIndex, string[]>
        {
            { MenuButtonIndex.Resume,   new[] { "再開",           "Resume"       } },
            { MenuButtonIndex.Tutorial, new[] { "チュートリアル", "Tutorial"     } },
            { MenuButtonIndex.Setting,  new[] { "設定",           "Setting"      } },
            { MenuButtonIndex.Quit,     new[] { "タイトルへ",     "Back to Title" } },
        };

        // ラベルキャッシュ: (TextMeshProUGUI, string[JP,EN]) のフラットリスト.
        private List<(TMPro.TextMeshProUGUI label, string[] texts)> _labelCache;


        protected override void Init()
        {

            // スロット定義: { MenuButtonIndex, 縦Yインデックス, 発火アクション, 発火モード }
            _buttonsSlot = new ButtonSlotDictionary()
            {
                { MenuButtonIndex.Resume,   0, ResumeGame,    ButtonFireMode.Immediate },
                { MenuButtonIndex.Tutorial, 1, OpenTutorial,  ButtonFireMode.Immediate },
                { MenuButtonIndex.Setting,  2, OpenSetting,   ButtonFireMode.Immediate },
                { MenuButtonIndex.Quit,     3, ReturnToTitle, ButtonFireMode.EarlyFire },
            };

            // ButtonEventer用のボタン配列を構築（縦一列: buttons[0][y]）.
            // Y インデックスでスロット指定するため、先に4要素nullで初期化.
            var col = new MenuButton[4];

            // MenuButton配列からenum別にボタン参照を取得.
            if (menuButtons != null)
            {
                foreach (var mb in menuButtons)
                {
                    if (mb == null || mb.button == null) continue;
                    col[buttonsSlot[mb.index]] = mb;
                }
            }

            // nullスロットを前に詰める（非nullを前方にシフト）.
            int nullCount = 0;
            for (int i = 0; i < 4; i++)
            {
                if (col[i] == null)
                {
                    nullCount++;
                    // null のスロットを BiDictionary から削除.
                    buttonsSlot.RemoveByValue(i);
                }
                else if (nullCount > 0)
                {
                    col[i - nullCount] = col[i];
                    col[i] = null;
                    // 詰めたことで Y インデックスが変わるため BiDictionary の値を更新.
                    buttonsSlot.UpdateValueByKey(buttonsSlot.GetByValue(i), i - nullCount);
                }
            }

            var finalLen = 4 - nullCount;
            if (finalLen == 0)
            {
                Debug.LogWarning("[PauseMenuEventer] メニューボタンが設定されていません. InspectorでmenuButtonsを設定してください.");
                // ButtonEventer.Awake()のクラッシュ防止用ダミー.
                buttons = new MenuButton[][] { new MenuButton[] { new MenuButton { button = gameObject.AddComponent<Button>(), index = (MenuButtonIndex)0 } } };
            }
            else
            {
                var finalCol = new MenuButton[finalLen];
                for (int i = 0; i < finalLen; i++)
                    finalCol[i] = col[i];
                // 縦一列（X=0固定、Y方向に上下ナビゲート）.
                buttons = new MenuButton[][] { finalCol };
            }

            // 参照取得.
            tutorialManager = TutorialManager.Instance(false);
            inputActions = InputSystemActionsManager.Instance().GetInputSystem_Actions();

            // 初期状態は非表示.
            if (menuWindow != null) menuWindow.SetActive(false);
            DisableEventer();
        }

        protected override void OnLanguageChanged(GameLanguage lang)
        {
            // タイトルテキスト更新.
            if (titleText != null)
                titleText.text = titleLabels[lang == GameLanguage.English ? 1 : 0];

            if (buttons == null) return;

            // 初回呼び出し時にキャッシュを構築（Awake の InitMenuButtonLabel() 完了後に実行される）.
            if (_labelCache == null)
            {
                _labelCache = new List<(TMPro.TextMeshProUGUI, string[])>();
                foreach (var c in buttons)
                {
                    if (c == null) continue;
                    foreach (var mb in c)
                    {
                        if (mb == null || mb.labelText == null) continue;
                        if (pauseLabelMap.TryGetValue(mb.index, out var texts))
                            _labelCache.Add((mb.labelText, texts));
                    }
                }
            }

            int li = lang == GameLanguage.English ? 1 : 0;
            foreach (var (label, texts) in _labelCache)
                label.text = texts[li];
        }

        // ============================
        // === Cancel(ESC) 入力監視 ===
        // ============================

        /// <summary>
        /// LateUpdate で Cancel(ESC) を監視.
        /// ButtonEventer の Update(CursolUpdate) とは別に、
        /// メニュー/チュートリアル状態に応じた ESC 処理を行う.
        /// </summary>
        private void LateUpdate()
        {
            // 安全策: メニュー表示中にtimeScaleが0でない場合は強制的に0に戻す.
            // Addressables読み込み後の初回シーン進入時等、外部処理がtimeScaleを
            // リセットしてしまうケースへの対策.
            if (isMenuVisible && Time.timeScale != 0f)
            {
                Time.timeScale = 0f;
            }

            if (inputActions == null) return;
            if (!inputActions.UI.Cancel.WasPressedThisFrame()) return;

            // ShowMenu()と同フレームのCancel検知をスキップ.
            // Update(ShowMenu) → LateUpdate(Cancel) が同フレームで発火し、
            // メニューが即座に閉じるのを防ぐ.
            if (Time.frameCount == showMenuFrame) return;

            if (isTutorialFromMenu)
            {
                // チュートリアル表示中(メニューから開いた) → メニューに戻る.
                ReturnFromTutorialToMenu();
            }
            else if (isMenuVisible)
            {
                // メニュー表示中 → ゲーム再開.
                ResumeGame();
            }
        }

        // ============================
        // === メニュー表示/非表示 ===
        // ============================

        /// <summary>
        /// ポーズメニューを表示する.
        /// timeScale=0 に設定し、UI入力に切り替える.
        /// PlayerPresenter から呼ばれる.
        /// </summary>
        public void ShowMenu()
        {
            if (menuWindow != null) menuWindow.SetActive(true);
            isMenuVisible = true;
            showMenuFrame = Time.frameCount;
            Time.timeScale = 0f;

            // UI入力に切替（Navigate/Submit/Cancel）.
            InputSystemActionsManager.Instance()?.EnableUI();
            EnableEventer();

            // Player入力を無効化（UI入力と同時に）.
            inputActions?.CharacterController.Disable();
            InGame.Player.PlayerManager.Instance(false)?.SetPlayerActionEnable(false);

            // ボタン選択を先頭(Resume)にリセット.
            // menuWindow再表示時にAnimatorがリセットされるため、
            // Highlightedトリガーを再発火させる.
            ResetSelection();
        }

        /// <summary>
        /// ポーズメニューを非表示にする（内部用）.
        /// </summary>
        private void HideMenu()
        {
            if (menuWindow != null) menuWindow.SetActive(false);
            isMenuVisible = false;
            DisableEventer();
        }

        // ============================
        // === 各ボタンの処理 ===
        // ============================

        /// <summary>
        /// ゲームを再開する.
        /// timeScale=1 に戻し、Player/CharacterController 入力を復帰.
        /// </summary>
        private void ResumeGame()
        {
            HideMenu();
            Time.timeScale = 1f;

            // チュートリアルシーンの場合、チュートリアルを再開.
            if (tutorialManager != null && tutorialManager.IsTutorialScene)
            {
                tutorialManager.ResumeTutorialScene();
            }

            // Player入力に戻す.
            InputSystemActionsManager.Instance()?.EnablePlayer();
            inputActions?.CharacterController.Enable();

            // コードレベルでプレイヤーアクション再有効化.
            InGame.Player.PlayerManager.Instance(false)?.SetPlayerActionEnable(true);
        }

        /// <summary>
        /// メニューからチュートリアルを開く.
        /// メニューを非表示にし、TutorialManager でチュートリアルを表示.
        /// timeScale は 0 のまま.
        /// </summary>
        private void OpenTutorial()
        {
            HideMenu();
            isTutorialFromMenu = true;

            if (tutorialManager != null)
            {
                tutorialManager.StartTutorial();
                // StartTutorial() が timeScale=0 を設定するのでそのまま.
            }
        }

        /// <summary>
        /// チュートリアルを閉じてメニューに戻る.
        /// </summary>
        private void ReturnFromTutorialToMenu()
        {
            isTutorialFromMenu = false;

            // チュートリアルを非表示（HideTutorial は timeScale=1 に戻すが、
            // 直後の ShowMenu で timeScale=0 に再設定される）.
            tutorialManager?.HideTutorial();

            // メニューを再表示.
            ShowMenu();
        }

        /// <summary>
        /// 設定画面を開く.
        /// </summary>
        private void OpenSetting()
        {
            // TODO: 設定画面の実装.
        }

        /// <summary>
        /// タイトルへ戻る.
        /// </summary>
        private void ReturnToTitle()
        {
            HideMenu();
            Time.timeScale = 1f;
            SceneManager.Instance().LoadMainScene(new TitleSceneInfo()).Forget();
        }

        // ============================
        // === 外部参照用プロパティ ===
        // ============================

        /// <summary>
        /// メニューが表示中か.
        /// </summary>
        public bool IsMenuVisible => isMenuVisible;

        /// <summary>
        /// メニューからチュートリアルを開いた状態か.
        /// </summary>
        public bool IsTutorialFromMenu => isTutorialFromMenu;
    }
}

