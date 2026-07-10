using Common;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Tutorial
{
    /// <summary>
    /// チュートリアルCanvas上のViewクラス.
    /// Prefab化されたCanvasのルートにアタッチし、
    /// Awake時にTutorialManagerへ自身を登録する.
    ///
    /// 階層:
    ///   Canvas (TutorialView)
    ///   ├─ Image (ウィンドウ — TutorialWindow)
    ///   │   ├─ タイトル TMP
    ///   │   ├─ 説明 TMP
    ///   │   ├─ RawImage (動画再生)
    ///   │   └─ noAnimImage (動画なし時の代替)
    ///   ├─ BackButton
    ///   └─ NextButton
    /// </summary>
    public class TutorialView : MonoBehaviour
    {
        [SerializeField] private TutorialWindow window;

        [Header("ナビゲーションボタン")]
        [SerializeField] private Button backButton;
        [SerializeField] private Button nextButton;

        [Header("タイトル戻り用ウィンドウ（通常tutorial時非表示）")]
        [SerializeField] private GameObject titleReturnWindow;
        [SerializeField] private Image titleReturnFillImage;

        [Header("完了率テキスト")]
        [SerializeField] private TextMeshProUGUI completionRateText;
        [SerializeField] private Image tutorialPercentBGImage;

        [Header("Pose/Closeキーテキスト")]
        [SerializeField] private TextMeshProUGUI poseCloseKeyText;

        /// <summary>子のTutorialWindow参照.</summary>
        public TutorialWindow Window => window;

        /// <summary>タイトル戻りウィンドウ.</summary>
        public GameObject TitleReturnWindow => titleReturnWindow;

        /// <summary>タイトル戻り長押し率表示Image.</summary>
        public Image TitleReturnFillImage => titleReturnFillImage;

        // 入力.
        private InputSystem_Actions inputActions;
        private float navigateCooldown = 0.2f;
        private float lastNavigateTime = 0f;

        // ボタン選択状態: 0=Back, 1=Next.
        private int selectedIndex = 1;
        private Button[] navButtons;

        // 出現時の入力残留無視用（一度0に戻るまで入力を受け付けない）.
        private bool ignoreNavigateUntilRelease = true;

        // Animator hash.
        private static readonly int HighlightedHash = Animator.StringToHash("Highlighted");
        private static readonly int NormalHash = Animator.StringToHash("Normal");

        // ---- クローンwindow + スライド ----
        private TutorialWindow windowA; // 原本
        private TutorialWindow windowB; // クローン
        private RectTransform windowARect;
        private RectTransform windowBRect;
        private float canvasWidth;

        // スライドアニメーション設定.
        private const float slideDuration = 0.4f;

        private void Awake()
        {
            // 自身の存在をTutorialManagerに認知させる.
            var manager = TutorialManager.Instance(false);
            if (manager != null)
            {
                manager.RegisterView(this);
            }
            else
            {
                Debug.LogWarning("[TutorialView] TutorialManager が見つかりません.");
            }

            // ボタン配列（左:Back, 右:Next）.
            navButtons = new Button[] { backButton, nextButton };

            // ボタンクリックイベント.
            if (backButton != null)
                backButton.onClick.AddListener(OnBackClicked);
            if (nextButton != null)
                nextButton.onClick.AddListener(OnNextClicked);

            // InputSystem取得.
            inputActions = InputSystemActionsManager.Instance().GetInputSystem_Actions();

            // ウィンドウは初期非表示（StartTutorialで内容設定後に表示される）.
            if (window != null)
                window.Hide();

            // タイトル戻りウィンドウは初期非表示.
            if (titleReturnWindow != null)
                titleReturnWindow.SetActive(false);

            // ---- PlayerPrefs に基づく UI 制御 ----
            bool isTutorialMode = PlayerPrefs.GetInt("TutorialMode", 0) == 1;

            // 完了率テキスト: チュートリアルシーン時のみ表示.
            if (completionRateText != null)
                completionRateText.gameObject.SetActive(isTutorialMode);
            if (tutorialPercentBGImage != null)
                tutorialPercentBGImage.gameObject.SetActive(isTutorialMode);

            // BackButton: チュートリアルシーン時は非表示.
            if (isTutorialMode && backButton != null)
                backButton.gameObject.SetActive(false);

            // ---- クローンwindow生成 ----
            windowA = window;
            windowARect = windowA != null ? windowA.GetComponent<RectTransform>() : null;

            if (window != null)
            {
                var cloneObj = Instantiate(window.gameObject, window.transform.parent);
                windowB = cloneObj.GetComponent<TutorialWindow>();
                windowBRect = cloneObj.GetComponent<RectTransform>();
                windowB.Hide();
            }

            // Canvas幅をキャッシュ.
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
        }

        private void OnEnable()
        {
            // 表示されるたびに入力残留無視をリセット.
            ignoreNavigateUntilRelease = true;
        }

        private void Update()
        {
            var manager = TutorialManager.Instance(false);
            if (manager == null) return;

            // チュートリアルシーン: ShowingExplanation以外ではナビゲーションを無効化.
            if (manager.IsTutorialScene && manager.CurrentPhase != TutorialPhase.ShowingExplanation)
                return;

            if (window == null || !window.gameObject.activeInHierarchy) return;

            NavigateUpdate();
        }

        /// <summary>
        /// 左右入力によるページ遷移 + Submit確認.
        /// 右入力 → 次に移動 (Next)  /  左入力 → 一つ戻る (Prev).
        /// </summary>
        private void NavigateUpdate()
        {
            if (inputActions == null) return;

            Vector2 nav = inputActions.UI.Navigate.ReadValue<Vector2>();

            // 出現直後の入力残留を無視（一度ニュートラルに戻るまで受け付けない）.
            if (ignoreNavigateUntilRelease)
            {
                if (Mathf.Abs(nav.x) < 0.1f)
                    ignoreNavigateUntilRelease = false;
                return;
            }

            if (Time.unscaledTime - lastNavigateTime < navigateCooldown) return;

            var manager = TutorialManager.Instance(false);
            if (manager == null) return;

            // 右入力 → 次に移動.
            if (nav.x > 0.5f)
            {
                lastNavigateTime = Time.unscaledTime;
                SelectButton(1); // Next選択.
                manager.Next();
                UpdateButtonVisuals();
            }
            // 左入力 → 一つ戻る.
            else if (nav.x < -0.5f)
            {
                lastNavigateTime = Time.unscaledTime;
                SelectButton(0); // Back選択.
                manager.Prev();
                UpdateButtonVisuals();
            }

            // BackSpace → Back.
            if (Keyboard.current != null && Keyboard.current.backspaceKey.wasPressedThisFrame)
            {
                lastNavigateTime = Time.unscaledTime;
                SelectButton(0);
                manager.Prev();
                UpdateButtonVisuals();
            }

            // Submit → 現在選択中のボタン実行.
            if (inputActions.UI.Submit.WasPressedThisFrame())
            {
                if (selectedIndex == 0)
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

            for (int i = 0; i < navButtons.Length; i++)
            {
                if (navButtons[i] == null) continue;
                Animator anim = navButtons[i].GetComponent<Animator>();
                if (anim != null)
                {
                    anim.SetTrigger(i == selectedIndex ? HighlightedHash : NormalHash);
                }
            }

            // EventSystem選択.
            if (navButtons[selectedIndex] != null)
            {
                EventSystem.current?.SetSelectedGameObject(navButtons[selectedIndex].gameObject);
            }
        }

        /// <summary>
        /// ページ位置に応じてボタンの表示状態を更新.
        /// </summary>
        private void UpdateButtonVisuals()
        {
            var manager = TutorialManager.Instance(false);
            if (manager == null) return;

            // チュートリアルシーン時は Backボタンを非表示.
            if (manager.IsTutorialScene)
            {
                if (backButton != null)
                    backButton.gameObject.SetActive(false);
            }
            else
            {
                // 最初のページ → Backを非活性.
                if (backButton != null)
                {
                    backButton.gameObject.SetActive(true);
                    backButton.interactable = !manager.IsFirst;
                }
            }

            // 最後のページ → NextのテキストをEndに切り替え等は必要に応じて.
            if (nextButton != null)
                nextButton.interactable = true;
        }

        /// <summary>
        /// スライドアニメーションで次のコンテンツへ遷移.
        /// timeScale=0中でもrealtimeSinceStartupで動作する.
        /// </summary>
        /// <param name="nextContent">次に表示するコンテンツ.</param>
        /// <param name="slideLeft">true=左方向スライド(Next), false=右方向スライド(Back).</param>
        public async UniTask SlideTransitionAsync(ITutorialContent nextContent, bool slideLeft)
        {
            if (windowA == null || windowB == null) return;
            if (windowARect == null || windowBRect == null) return;

            // windowBに次のコンテンツを設定.
            await windowB.DisplayAsync(nextContent);

            // スライド方向.
            float direction = slideLeft ? -1f : 1f;

            // windowBを画面外に配置して表示.
            windowBRect.anchoredPosition = new Vector2(-direction * canvasWidth, windowARect.anchoredPosition.y);
            windowB.Show();

            // windowAの初期位置を保存.
            Vector2 windowAStartPos = windowARect.anchoredPosition;
            Vector2 windowBStartPos = windowBRect.anchoredPosition;

            Vector2 windowAEndPos = new Vector2(direction * canvasWidth, windowAStartPos.y);
            Vector2 windowBEndPos = new Vector2(0f, windowBStartPos.y);

            // realtimeSinceStartupベースのアニメーション（timeScale=0でも動作）.
            float startTime = Time.realtimeSinceStartup;
            float elapsed = 0f;

            while (elapsed < slideDuration)
            {
                elapsed = Time.realtimeSinceStartup - startTime;
                float t = Mathf.Clamp01(elapsed / slideDuration);
                float eased = EaseInOutCubic(t);

                windowARect.anchoredPosition = Vector2.Lerp(windowAStartPos, windowAEndPos, eased);
                windowBRect.anchoredPosition = Vector2.Lerp(windowBStartPos, windowBEndPos, eased);

                await UniTask.Yield();
            }

            // 最終位置を確定.
            windowARect.anchoredPosition = windowAEndPos;
            windowBRect.anchoredPosition = windowBEndPos;

            // windowA非表示、位置リセット.
            windowA.Hide();
            windowARect.anchoredPosition = new Vector2(0f, windowAStartPos.y);

            // A⇔B入替.
            var tempWindow = windowA;
            windowA = windowB;
            windowB = tempWindow;

            var tempRect = windowARect;
            windowARect = windowBRect;
            windowBRect = tempRect;

            // window参照更新（TutorialManagerが使う参照）.
            window = windowA;

            // 入力残留無視をリセット.
            ignoreNavigateUntilRelease = true;

            // TutorialManagerにアニメーション完了を通知.
            var manager = TutorialManager.Instance(false);
            manager?.OnSlideAnimationComplete();
        }

        /// <summary>
        /// EaseInOutCubic 補間.
        /// </summary>
        private static float EaseInOutCubic(float t)
        {
            return t < 0.5f
                ? 4f * t * t * t
                : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        }

        /// <summary>
        /// タイトル戻り長押し率を更新.
        /// </summary>
        public void SetTitleReturnProgress(float progress)
        {
            if (titleReturnFillImage != null)
                titleReturnFillImage.fillAmount = progress;
        }

        /// <summary>
        /// Pose/Closeキーテキストを更新.
        /// </summary>
        /// <param name="isExplanationShowing">true=説明ウィンドウ表示中.</param>
        public void SetPoseCloseKeyText(bool isExplanationShowing)
        {
            if (poseCloseKeyText == null) return;
            poseCloseKeyText.text = isExplanationShowing
                ? "Close\n≡/esc\nPush"
                : "Pose\n≡/esc\nPush";
        }
    }
}
