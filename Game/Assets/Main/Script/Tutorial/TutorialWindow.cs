using System.Threading;
using Common;
using Cysharp.Threading.Tasks;
using InGame.Enemy;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Tutorial
{
    /// <summary>
    /// チュートリアル表示・進行 UI.
    /// Prefab化されたCanvasのルートにアタッチし、
    /// Awake時にTutorialManagerへ自身を登録する.
    ///
    /// 階層:
    ///   Canvas (TutorialWindow)
    ///   ├─ PanelA (TutorialWindowPanel — Inspector で panelA に設定)
    ///   │   ├─ タイトル TMP
    ///   │   ├─ 説明 TMP
    ///   │   ├─ RawImage (動画再生)
    ///   │   └─ noAnimImage (動画なし時の代替)
    ///   ├─ BackButton
    ///   └─ NextButton
    ///
    /// PanelB は Awake 時に PanelA を Instantiate して自動生成される.
    /// Inspector で設定するのは panelA のみ.
    /// </summary>
    public class TutorialWindow : MonoBehaviour
    {
        // ============================
        // === Inspector 設定 ===
        // ============================

        [Header("ナビゲーションボタン")]
        [SerializeField] private Button backButton;
        [SerializeField] private Button nextButton;

        [Header("ナビゲーションボタンラベル（()内の操作キー表記はそのまま、語句のみ翻訳）")]
        [SerializeField] private TextMeshProUGUI backButtonText;
        [SerializeField] private TextMeshProUGUI nextButtonText;

        [Header("完了率テキスト")]
        [SerializeField] private TextMeshProUGUI completionRateText;
        [SerializeField] private Image tutorialPercentBGImage;

        [Header("Pose/Closeキーテキスト")]
        [SerializeField] private TextMeshProUGUI poseCloseKeyText;

        // ============================
        // === 変数 ===
        // ============================

        // 入力.
        private InputSystem_Actions inputActions;
        private float navigateCooldown = 0.2f;
        private float lastNavigateTime = 0f;

        // ボタン選択状態: 0=Back, 1=Next.
        private int selectedIndex = 1;
        private Button[] navButtons;

        // 出現時の入力残留無視用（一度0に戻るまで入力を受け付けない）.
        private bool ignoreNavigateUntilRelease = true;

        // ナビゲーションボタンラベル翻訳 { JP, EN }.
        // ()内の操作キー表記はそのまま、語句部分のみ翻訳.
        private static readonly string[] backLabels = { "戻る ( ← / BackSpace )", "Back ( ← / BackSpace )" };
        private static readonly string[] nextLabels = { "( → / Enter) 進む",      "( → / Enter) Next"      };

        // ボタン色定数.
        private static readonly Color colorDeselected = new Color(0.8f, 0.8f, 0.8f, 0.8f);
        private const float blinkHalfDuration = 0.5f;

        // 非同期ブリンク制御.
        private CancellationTokenSource blinkCts;

        // ナビゲーションボタン（進む/戻る）表示フラグ.
        // ShowContentAsync で true、HidePanel で false に切り替える.
        // パネル非表示中（入力監視・初期待機）にボタンが露出するのを防ぐ.
        private bool _navButtonsEnabled = false;

        // panelA は子から自動取得、panelB はランタイム複製.
        private TutorialWindowPanel panelA;

        // スライドアニメーション: active=表示中, inactive=スライド用.
        private TutorialWindowPanel activePanel;
        private TutorialWindowPanel inactivePanel;

        // スライドアニメーション設定.
        private float canvasWidth;
        private const float slideDuration = 0.4f;

        // ============================
        // === 初期化 ===
        // ============================

        private void Awake()
        {
            // TutorialManagerへ自身を登録.
            var manager = TutorialManager.Instance(false);
            if (manager != null)
                manager.RegisterView(this);
            else
                Debug.LogWarning("[TutorialWindow] TutorialManager が見つかりません.");

            // ボタン配列（左:Back, 右:Next）.
            navButtons = new Button[] { backButton, nextButton };

            if (backButton != null)
                backButton.onClick.AddListener(OnBackClicked);
            if (nextButton != null)
                nextButton.onClick.AddListener(OnNextClicked);

            inputActions = InputSystemActionsManager.Instance().GetInputSystem_Actions();

            // 子オブジェクトから TutorialWindowPanel を自動取得.
            panelA = GetComponentInChildren<TutorialWindowPanel>();

            // panelB を panelA からランタイム複製.
            TutorialWindowPanel panelB = null;
            if (panelA != null)
            {
                var cloneObj = Instantiate(panelA.gameObject, panelA.transform.parent);
                panelB = cloneObj.GetComponent<TutorialWindowPanel>();
            }

            // パネル初期化（両パネルとも非表示）.
            activePanel   = panelA;
            inactivePanel = panelB;
            activePanel?.Hide();
            inactivePanel?.Hide();


            // PlayerPrefs に基づく UI 制御.
            // チュートリアルモード or イージーモード(操作一覧表示)の場合に表示.
            bool isTutorialMode = PlayerPrefs.GetInt("TutorialMode", 0) == 1;
            bool showControls = isTutorialMode || IsEasyMode();

            if (completionRateText != null)
                completionRateText.gameObject.SetActive(showControls);
            if (tutorialPercentBGImage != null)
                tutorialPercentBGImage.gameObject.SetActive(showControls);

            // ナビゲーションボタンは初期非表示（ShowContentAsync / SlideTransitionAsync で有効化）.
            if (backButton != null) backButton.gameObject.SetActive(false);
            if (nextButton != null) nextButton.gameObject.SetActive(false);

            // Canvas幅をキャッシュ（スライドアニメーション用）.
            var canvasRect = GetComponent<RectTransform>();
            if (canvasRect != null)
                canvasWidth = canvasRect.rect.width;

            // 完了率テキスト・BG画像を登録.
            if (manager != null)
            {
                if (completionRateText != null)
                    manager.RegisterCompletionRateText(completionRateText);
                if (tutorialPercentBGImage != null)
                    manager.RegisterPercentBGImage(tutorialPercentBGImage);
            }

            // 言語切り替えイベント購読 + 起動時適用.
            var lm = LanguageManager.Instance(false);
            if (lm != null)
            {
                lm.OnLanguageChanged += OnLanguageChanged;
                OnLanguageChanged(lm.CurrentLanguage);
            }
        }

        private void OnEnable()
        {
            // 表示されるたびに入力残留無視をリセット.
            ignoreNavigateUntilRelease = true;
        }

        private void OnDestroy()
        {
            blinkCts?.Cancel();
            blinkCts?.Dispose();
            blinkCts = null;
            // 各パネルの OnDestroy で ReleaseVideo が呼ばれる.

            var lm = LanguageManager.Instance(false);
            if (lm != null)
                lm.OnLanguageChanged -= OnLanguageChanged;
        }

        // ============================
        // === 表示制御（TutorialManagerから呼ばれる） ===
        // ============================

        /// <summary>
        /// コンテンツをアクティブパネルに表示する.
        /// </summary>
        public async UniTask ShowContentAsync(ITutorialContent content)
        {
            // await の前に同期的に実行し、currentIndex が正しい状態で更新する.
            // await より後に置くと並行呼び出し時に完了順が前後してボタン表示が交互になるバグが発生する.
            _navButtonsEnabled = true;
            UpdateButtonVisuals();
            activePanel?.Show();
            if (activePanel != null)
                await activePanel.DisplayAsync(content);
        }

        /// <summary>
        /// アクティブパネルを非表示にする.
        /// </summary>
        public void HidePanel()
        {
            activePanel?.Hide();
            _navButtonsEnabled = false;
            UpdateButtonVisuals();
        }

        // ============================
        // === Update ===
        // ============================

        private void Update()
        {
            var manager = TutorialManager.Instance(false);
            if (manager == null) return;

            // チュートリアルシーン: ShowingExplanation以外ではナビゲーションを無効化.
            if (manager.IsTutorialScene && manager.CurrentPhase != TutorialPhase.ShowingExplanation)
                return;

            if (activePanel == null || !activePanel.IsVisible) return;

            NavigateUpdate();
        }

        // ============================
        // === ナビゲーション入力 ===
        // ============================

        /// <summary>
        /// 左右入力によるページ遷移 + Submit確認.
        /// 右入力 → 次に移動 (Next)  /  左入力 → 一つ戻る (Back).
        /// </summary>
        private void NavigateUpdate()
        {
            if (inputActions == null) return;

            // ゲームパッドは十字キー(D-pad)、キーボードは矢印キー/WASDで操作.
            Vector2 nav = Vector2.zero;
            if (Gamepad.current != null)
                nav = Gamepad.current.dpad.ReadValue();
            if (Keyboard.current != null)
            {
                if (Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed) nav.x = 1f;
                else if (Keyboard.current.leftArrowKey.isPressed || Keyboard.current.aKey.isPressed) nav.x = -1f;
            }

            // 出現直後の入力残留を無視（一度ニュートラルに戻るまで受け付けない）.
            // スティックが押されていれば早期リターン。ニュートラル時はフラグを解除して
            // そのまま処理を継続する（同フレームの Submit を取りこぼさないため）.
            if (ignoreNavigateUntilRelease)
            {
                if (Mathf.Abs(nav.x) < 0.1f)
                    ignoreNavigateUntilRelease = false;
                else
                    return;
            }

            if (Time.unscaledTime - lastNavigateTime < navigateCooldown) return;

            var manager = TutorialManager.Instance(false);
            if (manager == null) return;

            if (nav.x > 0.5f)
            {
                lastNavigateTime = Time.unscaledTime;
                SelectButton(1);
                manager.Next();
                UpdateButtonVisuals();
            }
            else if (nav.x < -0.5f)
            {
                // Back が無効（役割なし）の場合は左入力を無視.
                if (backButton != null && !backButton.interactable) { /* ignore */ }
                else
                {
                    lastNavigateTime = Time.unscaledTime;
                    SelectButton(0);
                    manager.Prev();
                    UpdateButtonVisuals();
                }
            }

            // BackSpace → Back（Back が無効の場合は無視）.
            if (Keyboard.current != null && Keyboard.current.backspaceKey.wasPressedThisFrame
                && (backButton == null || backButton.interactable))
            {
                lastNavigateTime = Time.unscaledTime;
                SelectButton(0);
                manager.Prev();
                UpdateButtonVisuals();
            }

            // Submit → 現在選択中のボタン実行.
            // Back が無効のときに selectedIndex==0 のままになっていても Next を実行.
            if (inputActions.UI.Submit.WasPressedThisFrame())
            {
                bool backEnabled = backButton != null && backButton.interactable;
                if (selectedIndex == 0 && backEnabled)
                    OnBackClicked();
                else
                    OnNextClicked();
            }
        }

        private void OnBackClicked()
        {
            var manager = TutorialManager.Instance(false);
            if (manager == null) return;
            manager.Prev();
            UpdateButtonVisuals();
        }

        private void OnNextClicked()
        {
            var manager = TutorialManager.Instance(false);
            if (manager == null) return;
            manager.Next();
            UpdateButtonVisuals();
        }

        private void SelectButton(int index)
        {
            selectedIndex = index;
            if (navButtons == null) return;

            blinkCts?.Cancel();
            blinkCts?.Dispose();
            blinkCts = null;

            for (int i = 0; i < navButtons.Length; i++)
            {
                if (navButtons[i] == null) continue;
                var img = navButtons[i].GetComponent<Image>();
                if (img == null) continue;

                if (i == selectedIndex)
                {
                    blinkCts = new CancellationTokenSource();
                    ImageColorAnimator.BlinkLoopAsync(img, Color.white, colorDeselected, blinkHalfDuration, blinkCts.Token).Forget();
                }
                else
                {
                    img.color = colorDeselected;
                }
            }

            if (navButtons[selectedIndex] != null)
                EventSystem.current?.SetSelectedGameObject(navButtons[selectedIndex].gameObject);
        }

        /// <summary>
        /// ページ位置に応じてボタンの表示状態を更新.
        /// 役割なし（チュートリアルシーン or 先頭ページ）: 文字を灰色にして背景を非表示.
        /// 役割あり: 通常表示.
        /// </summary>
        private void UpdateButtonVisuals()
        {
            var manager = TutorialManager.Instance(false);

            // manager 未取得時はボタンを強制非表示にして終了.
            if (manager == null)
            {
                if (backButton != null) backButton.gameObject.SetActive(false);
                if (nextButton != null) nextButton.gameObject.SetActive(false);
                return;
            }

            // バックボタンの役割判定.
            // チュートリアルシーンでは同一チュートリアルの分割ページ内のみ戻れる.
            bool backEnabled = manager.IsTutorialScene
                ? manager.CanGoBackInScene()
                : !manager.IsFirst;

            if (backButton != null)
            {
                backButton.interactable = backEnabled;
                // _navButtonsEnabled かつ役割ありのときのみ表示.
                backButton.gameObject.SetActive(_navButtonsEnabled && backEnabled);

                // 背景画像（豆腐）: 常に非表示（文字のみで表示）.
                var backImg = backButton.GetComponent<Image>();
                if (backImg != null)
                    backImg.enabled = false;

                if (backButtonText != null)
                    backButtonText.color = Color.white;
            }

            if (nextButton != null)
            {
                nextButton.interactable = true;
                nextButton.gameObject.SetActive(_navButtonsEnabled);
            }
        }

        // ============================
        // === スライドアニメーション ===
        // ============================

        /// <summary>
        /// スライドアニメーションで次のコンテンツへ遷移.
        /// timeScale=0中でもrealtimeSinceStartupで動作する.
        /// </summary>
        /// <param name="nextContent">次に表示するコンテンツ.</param>
        /// <param name="slideLeft">true=左方向スライド(Next), false=右方向スライド(Back).</param>
        public async UniTask SlideTransitionAsync(ITutorialContent nextContent, bool slideLeft)
        {
            if (activePanel == null || inactivePanel == null) return;

            var activeRect   = activePanel.PanelRect;
            var inactiveRect = inactivePanel.PanelRect;
            if (activeRect == null || inactiveRect == null) return;

            // inactivePanelに次のコンテンツを設定.
            await inactivePanel.DisplayAsync(nextContent);

            // スライド方向.
            float direction = slideLeft ? -1f : 1f;

            // inactivePanelを画面外に配置して表示.
            inactiveRect.anchoredPosition = new Vector2(-direction * canvasWidth, activeRect.anchoredPosition.y);
            inactivePanel.Show();

            Vector2 activePanelStartPos   = activeRect.anchoredPosition;
            Vector2 inactivePanelStartPos = inactiveRect.anchoredPosition;
            Vector2 activePanelEndPos     = new Vector2(direction * canvasWidth, activePanelStartPos.y);
            Vector2 inactivePanelEndPos   = new Vector2(0f, inactivePanelStartPos.y);

            // realtimeSinceStartupベースのアニメーション（timeScale=0でも動作）.
            float startTime = Time.realtimeSinceStartup;
            float elapsed   = 0f;

            while (elapsed < slideDuration)
            {
                elapsed = Time.realtimeSinceStartup - startTime;
                float t     = Mathf.Clamp01(elapsed / slideDuration);
                float eased = EaseInOutCubic(t);

                activeRect.anchoredPosition   = Vector2.Lerp(activePanelStartPos, activePanelEndPos, eased);
                inactiveRect.anchoredPosition = Vector2.Lerp(inactivePanelStartPos, inactivePanelEndPos, eased);

                await UniTask.Yield();
            }

            // 最終位置を確定.
            activeRect.anchoredPosition   = activePanelEndPos;
            inactiveRect.anchoredPosition = inactivePanelEndPos;

            // activePanel非表示、位置リセット.
            activePanel.Hide();
            activeRect.anchoredPosition = new Vector2(0f, activePanelStartPos.y);

            // active ⇔ inactive 入替.
            var temp      = activePanel;
            activePanel   = inactivePanel;
            inactivePanel = temp;

            // 入力残留無視をリセット.
            ignoreNavigateUntilRelease = true;

            // TutorialManagerにアニメーション完了を通知.
            var manager = TutorialManager.Instance(false);
            manager?.OnSlideAnimationComplete();

            // スライド後もボタン表示状態を更新（ShowContentAsync を経由しないため明示的に呼ぶ）.
            _navButtonsEnabled = true;
            UpdateButtonVisuals();
        }

        private static bool IsEasyMode()
        {
            int tags = PlayerPrefs.GetInt("MissionTags", 0);
            return ((MissionTag)tags & MissionTag.Difficulty_Easy) != 0;
        }

        private static float EaseInOutCubic(float t)
        {
            return t < 0.5f
                ? 4f * t * t * t
                : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        }

        // ============================
        // === 外部から呼ばれる UI 更新 ===
        // ============================

        /// <summary>
        /// 言語変更時にナビゲーションボタンラベルを更新.
        /// ()内の操作キー表記はそのまま、語句部分のみ翻訳.
        /// </summary>
        private void OnLanguageChanged(GameLanguage lang)
        {
            int li = lang == GameLanguage.English ? 1 : 0;
            if (backButtonText != null)
                backButtonText.text = backLabels[li];
            if (nextButtonText != null)
                nextButtonText.text = nextLabels[li];
        }

        /// <summary>
        /// Pose/Closeキーテキストを更新.
        /// </summary>
        /// <param name="isExplanationShowing">true=説明ウィンドウ表示中.</param>
        public void SetPoseCloseKeyText(bool isExplanationShowing)
        {
            if (poseCloseKeyText == null) return;
            poseCloseKeyText.text = isExplanationShowing ? "≡/esc" : "≡/esc";
        }
    }
}
