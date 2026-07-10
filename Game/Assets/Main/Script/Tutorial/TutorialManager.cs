using System.Collections.Generic;
using UnityEngine;
using Common;
using Cysharp.Threading.Tasks;
using InGame.Enemy;
using InGame.Player;
using SceneInfo;
using TMPro;
using UnityEngine.UI;

namespace Tutorial
{
    /// <summary>
    /// チュートリアル進行フェーズ.
    /// </summary>
    public enum TutorialPhase
    {
        Idle,                // 非チュートリアル
        WaitingInitialDelay, // 2秒待機中
        ShowingExplanation,  // 説明window表示中(timeScale=0)
        MonitoringInput,     // 入力監視中(timeScale=1)
        Animating,           // スライドアニメーション中
    }

    /// <summary>
    /// チュートリアル進行管理シングルトン.
    /// TutorialWindow を参照して各ページを表示・切り替える.
    ///
    /// 使い方:
    ///   1. TutorialManager.Instance() でシングルトンを取得.
    ///   2. Initialize(window) で TutorialWindow を登録.
    ///   3. StartTutorial() でチュートリアル開始（最初のページを表示）.
    ///   4. Next() / Prev() でページを切り替え.
    ///   5. EndTutorial() でウィンドウを閉じる.
    /// </summary>
    public class TutorialManager : SingletonMonoBase<TutorialManager>
    {
        // ---- チュートリアル内容リスト ----
        private readonly List<ITutorialContent> tutorials = new List<ITutorialContent>
        {
            new TutorialContent_Controls(),
            new TutorialContent_Move(),
            new TutorialContent_WeakAttack(),
            new TutorialContent_StrongAttack(),
            new TutorialContent_Dodge(),
            new TutorialContent_Recovery(),
            new TutorialContent_Parry(),
            new TutorialContent_HeartRateRise(),
            new TutorialContent_HeartRate200(),
            new TutorialContent_HeartResist(),
            new TutorialContent_HeartResistStrong(),
            new TutorialContent_Iai(),
            new TutorialContent_AbsorbGauge(),
            new TutorialContent_HeartRateZero(),
        };

        private TutorialView view;
        private int currentIndex = 0;

        // チュートリアルシーンモード.
        private bool sceneTransitionOnEnd = false;

        // ---- 説明文分割 ----
        private const int maxLinesPerPage = 12;
        private bool tutorialsExpanded = false;

        // ---- ステートマシン ----
        private TutorialPhase currentPhase = TutorialPhase.Idle;
        private bool isTutorialScene = false;
        // チュートリアルシーンの入力監視中にポーズ表示しているかどうか.
        private bool isPausedDuringMonitoring = false;
        private int completedCount = 0;
        private InputSystem_Actions inputActions;
        private TextMeshProUGUI completionRateText;
        private Image percentBGImage;
        private RectTransform percentBGRect;
        private float initialDelayTimer = 0f;
        private const float initialDelayDuration = 0.75f;
        // 入力完了後の余韻（(完)表示 → 1sec後にスライド遷移）.
        private bool inputCompleted = false;
        private float afterglowTimer = 0f;
        private const float afterglowDuration = 1f;

        /// <summary>現在表示中のページインデックス（0始まり）.</summary>
        public int CurrentIndex => currentIndex;

        /// <summary>全ページ数.</summary>
        public int TotalCount => tutorials.Count;

        /// <summary>最初のページかどうか.</summary>
        public bool IsFirst => currentIndex <= 0;

        /// <summary>最後のページかどうか.</summary>
        public bool IsLast => currentIndex >= tutorials.Count - 1;

        /// <summary>登録済みの TutorialView 参照.</summary>
        public TutorialView View => view;

        /// <summary>現在のフェーズ.</summary>
        public TutorialPhase CurrentPhase => currentPhase;

        /// <summary>チュートリアルシーンかどうか.</summary>
        public bool IsTutorialScene => isTutorialScene;

        // ---- 公開 API ----

        /// <summary>
        /// TutorialView を登録する（TutorialView.Awake から呼ばれる）.
        /// </summary>
        public void RegisterView(TutorialView tutorialView)
        {
            view = tutorialView;
            Debug.Log("[TutorialManager] View登録完了");
        }

        /// <summary>
        /// View が登録されるまで待機する.
        /// </summary>
        public async UniTask WaitForView()
        {
            while (view == null)
            {
                await UniTask.Yield();
            }
        }

        /// <summary>
        /// 完了率テキストを登録する.
        /// </summary>
        public void RegisterCompletionRateText(TextMeshProUGUI text)
        {
            completionRateText = text;
            UpdateCompletionRateText();
        }

        /// <summary>
        /// 完了率BG画像を登録する.
        /// </summary>
        public void RegisterPercentBGImage(Image image)
        {
            percentBGImage = image;
            percentBGRect = image != null ? image.GetComponent<RectTransform>() : null;
            UpdatePercentBGSize();
        }

        /// <summary>
        /// チュートリアルを最初から開始する.
        /// </summary>
        /// <param name="withSceneTransition">true=チュートリアルシーン（最終ページでゲームシーンへ遷移）.</param>
        public void StartTutorial(bool withSceneTransition = false)
        {
            ExpandTutorials();
            currentIndex = 0;
            completedCount = 0;
            isTutorialScene = withSceneTransition;
            sceneTransitionOnEnd = isTutorialScene;

            // チュートリアルシーンでは移動説明から開始.
            if (isTutorialScene)
            {
                for (int i = 0; i < tutorials.Count; i++)
                {
                    if (tutorials[i].Title.StartsWith("移動"))
                    {
                        currentIndex = i;
                        break;
                    }
                }
            }

            // InputSystem取得.
            inputActions = InputSystemActionsManager.Instance().GetInputSystem_Actions();

            if (isTutorialScene)
            {
                // チュートリアルシーン: 2秒間シーンを見せてからwindow表示.
                Time.timeScale = 1f;
                currentPhase = TutorialPhase.WaitingInitialDelay;
                initialDelayTimer = 0f;

                // 初期待機中はすべての入力を無効化.
                inputActions?.CharacterController.Disable();
                inputActions?.UI.Disable();

                SetCompletionRateVisible(false);
                UpdateCompletionRateText();
            }
            else
            {
                // ゲームシーン: 従来通り即表示.
                currentPhase = TutorialPhase.ShowingExplanation;
                Time.timeScale = 0f;

                // コードレベルでプレイヤーアクション無効化（ESCは維持）.
                PlayerManager.Instance(false)?.SetPlayerActionEnable(false);

                // UI操作を有効化（Navigate/Submitでウィンドウ操作）.
                inputActions?.UI.Enable();

                // Pose→Closeテキスト切替.
                view?.SetPoseCloseKeyText(true);

                ShowCurrentAsync().Forget();
            }
        }

        /// <summary>
        /// 次のページへ進む.
        /// </summary>
        public void Next()
        {
            if (isTutorialScene)
            {
                NextTutorialScene();
                return;
            }

            // ゲームシーン: 最後のページではnextで閉じない.
            if (IsLast) return;
            currentIndex++;
            ShowCurrentAsync().Forget();
        }

        /// <summary>
        /// 前のページへ戻る.
        /// </summary>
        public void Prev()
        {
            // チュートリアルシーンでは無効.
            if (isTutorialScene) return;

            if (IsFirst) return;
            currentIndex--;
            ShowCurrentAsync().Forget();
        }

        /// <summary>
        /// 指定インデックスのページへジャンプする.
        /// </summary>
        public void JumpTo(int index)
        {
            if (index < 0 || index >= tutorials.Count) return;
            currentIndex = index;
            ShowCurrentAsync().Forget();
        }

        /// <summary>
        /// チュートリアルウィンドウを非表示にする（ゲーム中のトグル用）.
        /// </summary>
        public void HideTutorial()
        {
            view?.Window?.Hide();
            Time.timeScale = 1f;
            currentPhase = TutorialPhase.Idle;

            // コードレベルでプレイヤーアクション再有効化.
            PlayerManager.Instance(false)?.SetPlayerActionEnable(true);

            // UI操作を無効化（ゲームプレイに戻す）.
            inputActions?.UI.Disable();

            // Close→Poseテキスト切替.
            view?.SetPoseCloseKeyText(false);
        }

        /// <summary>
        /// チュートリアルシーンの入力監視中にポーズ表示する.
        /// StartTutorialと異なり、進行状況をリセットしない.
        /// </summary>
        public void PauseTutorialScene()
        {
            if (!isTutorialScene || currentPhase != TutorialPhase.MonitoringInput) return;

            isPausedDuringMonitoring = true;
            currentPhase = TutorialPhase.ShowingExplanation;
            Time.timeScale = 0f;

            // 説明window表示中: player操作無効、UI操作有効.
            inputActions?.CharacterController.Disable();
            inputActions?.UI.Enable();

            view?.SetPoseCloseKeyText(true);
            SetCompletionRateVisible(false);

            ShowCurrentAsync().Forget();
        }

        /// <summary>
        /// チュートリアルシーンのポーズ解除（入力監視に復帰）.
        /// 入力監視の進行状況はリセットしない.
        /// </summary>
        public void ResumeTutorialScene()
        {
            if (!isTutorialScene || !isPausedDuringMonitoring) return;

            isPausedDuringMonitoring = false;
            currentPhase = TutorialPhase.MonitoringInput;
            view?.Window?.Hide();
            Time.timeScale = 1f;

            // 監視中: player操作有効、UI操作無効.
            inputActions?.CharacterController.Enable();
            inputActions?.UI.Disable();

            view?.SetPoseCloseKeyText(false);
            SetCompletionRateVisible(true);
            UpdateCompletionRateText();
        }

        /// <summary>
        /// チュートリアルを終了し、MainSceneInfo へ遷移する.
        /// </summary>
        public void EndTutorial()
        {
            view?.Window?.Hide();
            Time.timeScale = 1f;
            currentPhase = TutorialPhase.Idle;
            isTutorialScene = false;
            isPausedDuringMonitoring = false;

            // チュートリアルモード解除.
            PlayerPrefs.SetInt("TutorialMode", 0);

            // PlayerPrefs設定（TitleEventer.GameStartと同じ）.
            PlayerPrefs.SetInt("EnemyName", (int)EnemyName.Wendigo);
            MissionTag tags = MissionTag.Difficulty_Normal
                            | MissionTag.Condition_BossNormal
                            | MissionTag.Enemy_Wendigo;
            PlayerPrefs.SetInt("MissionTags", (int)tags);
            PlayerPrefs.Save();

            Debug.Log("[TutorialManager] EndTutorial → MainSceneInfo へ遷移");
            SceneManager.Instance().LoadMainScene(new MainSceneInfo()).Forget();
        }

        // ---- Update (ステートマシン) ----

        private void Update()
        {
            if (!isTutorialScene) return;

            switch (currentPhase)
            {
                case TutorialPhase.WaitingInitialDelay:
                    UpdateWaitingInitialDelay();
                    break;
                case TutorialPhase.MonitoringInput:
                    UpdateMonitoringInput();
                    break;
            }
        }

        private void UpdateWaitingInitialDelay()
        {
            initialDelayTimer += Time.unscaledDeltaTime;
            if (initialDelayTimer >= initialDelayDuration)
            {
                // 2秒経過 → 最初のwindow表示.
                currentPhase = TutorialPhase.ShowingExplanation;
                Time.timeScale = 0f;

                // 説明window表示中: player操作無効、UI操作有効.
                inputActions?.CharacterController.Disable();
                inputActions?.UI.Enable();

                // Pose→Closeテキスト切替.
                view?.SetPoseCloseKeyText(true);

                // 説明ウィンドウ表示中は完了率UI非表示.
                SetCompletionRateVisible(false);

                ShowCurrentAsync().Forget();
            }
        }

        private void UpdateMonitoringInput()
        {
            if (inputActions == null) return;
            if (currentIndex < 0 || currentIndex >= tutorials.Count) return;

            var content = tutorials[currentIndex];
            if (content.IsInformational) return;

            if (!inputCompleted)
            {
                // 入力監視中 - プログレス更新.
                bool completed = content.CheckCompletion(inputActions);
                UpdateCompletionRateText();

                if (completed)
                {
                    // 入力条件達成 → 余韻状態へ.
                    inputCompleted = true;
                    afterglowTimer = 0f;
                    UpdateCompletionRateText();
                }
            }
            else
            {
                // 余韻: (完)表示中 → 1sec後に完了処理.
                afterglowTimer += Time.deltaTime;
                if (afterglowTimer >= afterglowDuration)
                {
                    inputCompleted = false;
                    OnInputCompleted();
                }
            }
        }

        // ---- チュートリアルシーン用 Next ----

        private void NextTutorialScene()
        {
            if (currentPhase != TutorialPhase.ShowingExplanation) return;
            // ポーズ中のNextは無視（Poseキーで復帰する前提）.
            if (isPausedDuringMonitoring) return;

            var content = tutorials[currentIndex];

            if (content.IsInformational)
            {
                // 最後のページ: チュートリアルシーンでは終了.
                if (IsLast)
                {
                    EndTutorial();
                    return;
                }

                // 説明のみ → 即完了してスライドで次へ.
                if (content.CountsForCompletion)
                {
                    completedCount++;
                    UpdateCompletionRateText();
                }
                currentIndex++;
                currentPhase = TutorialPhase.Animating;
                view.SlideTransitionAsync(tutorials[currentIndex], true).Forget();
            }
            else
            {
                // 入力が必要 → 監視フェーズへ.
                currentPhase = TutorialPhase.MonitoringInput;
                inputCompleted = false;
                content.ResetMonitoring();
                view?.Window?.Hide();
                Time.timeScale = 1f;

                // 監視中: player操作有効、UI操作無効.
                inputActions?.CharacterController.Enable();
                inputActions?.UI.Disable();

                // Close→Poseテキスト切替.
                view?.SetPoseCloseKeyText(false);

                // 入力監視中は完了率UI表示.
                SetCompletionRateVisible(true);
                UpdateCompletionRateText();
            }
        }

        /// <summary>
        /// 入力完了時の処理.
        /// 完了window表示をスキップし、直接スライド遷移へ進む.
        /// </summary>
        private void OnInputCompleted()
        {
            var content = tutorials[currentIndex];
            if (content.CountsForCompletion)
                completedCount++;
            UpdateCompletionRateText();

            // 全操作を無効化.
            inputActions?.CharacterController.Disable();
            inputActions?.UI.Disable();

            // フェーズ切替 + timeScale=0 を維持.
            currentPhase = TutorialPhase.Animating;
            Time.timeScale = 0f;

            // 完了率UI非表示.
            SetCompletionRateVisible(false);

            // 直接次のページへスライド遷移.
            AdvanceToNextPageAsync().Forget();
        }

        /// <summary>
        /// 完了表示後、次のページへスライド遷移.
        /// </summary>
        private async UniTaskVoid AdvanceToNextPageAsync()
        {
            if (IsLast)
            {
                EndTutorial();
                return;
            }

            currentIndex++;
            await view.SlideTransitionAsync(tutorials[currentIndex], true);
        }

        /// <summary>
        /// スライドアニメーション完了時にViewから呼ばれる.
        /// </summary>
        public void OnSlideAnimationComplete()
        {
            currentPhase = TutorialPhase.ShowingExplanation;
            Time.timeScale = 0f;

            // 説明window表示中: player操作無効、UI操作有効.
            inputActions?.CharacterController.Disable();
            inputActions?.UI.Enable();

            // Pose→Closeテキスト切替.
            view?.SetPoseCloseKeyText(true);

            // 説明ウィンドウ表示中は完了率UI非表示.
            SetCompletionRateVisible(false);

            UpdateCompletionRateText();
        }

        // ---- 内部処理 ----

        /// <summary>
        /// 説明文が maxLinesPerPage を超える ITutorialContent を複数ページに分割.
        /// StartTutorial() 初回呼び出し時に1度だけ実行.
        /// </summary>
        private void ExpandTutorials()
        {
            if (tutorialsExpanded) return;
            tutorialsExpanded = true;

            var expanded = new List<ITutorialContent>();
            foreach (var content in tutorials)
            {
                string[] lines = content.Description.Split('\n');
                if (lines.Length <= maxLinesPerPage)
                {
                    expanded.Add(content);
                    continue;
                }

                int totalPages = Mathf.CeilToInt((float)lines.Length / maxLinesPerPage);
                for (int page = 0; page < totalPages; page++)
                {
                    int start = page * maxLinesPerPage;
                    int count = Mathf.Min(maxLinesPerPage, lines.Length - start);
                    string pageDesc = string.Join("\n", lines, start, count);
                    expanded.Add(new SplitTutorialContent(content, pageDesc, page + 1, totalPages));
                }
            }

            tutorials.Clear();
            tutorials.AddRange(expanded);
        }

        private async UniTaskVoid ShowCurrentAsync()
        {
            var w = view?.Window;
            if (w == null)
            {
                Debug.LogWarning("[TutorialManager] TutorialWindow が登録されていません。");
                return;
            }

            w.Show();
            await w.DisplayAsync(tutorials[currentIndex]);
        }

        private void UpdateCompletionRateText()
        {
            if (completionRateText == null) return;

            int countableTotal = 0;
            foreach (var t in tutorials) { if (t.CountsForCompletion) countableTotal++; }
            var text = $"チュートリアルを完了する ({completedCount}/{countableTotal})";

            if (currentIndex >= 0 && currentIndex < tutorials.Count)
            {
                var content = tutorials[currentIndex];
                string suffix = inputCompleted ? "(完)" : "";

                if (!string.IsNullOrEmpty(content.OperationName))
                    text += $"\n{content.OperationName}{suffix}";
                if (!string.IsNullOrEmpty(content.OperationKey))
                    text += $"\n{content.OperationKey}{suffix}";
                if (!string.IsNullOrEmpty(content.Supplement))
                    text += $"\n{content.Supplement}{suffix}";

                // プログレスバー.
                string progressBar = content.GetProgressBarText();
                if (!string.IsNullOrEmpty(progressBar))
                    text += $"\n{progressBar}";
            }

            completionRateText.text = text;
            UpdatePercentBGSize();
        }

        /// <summary>
        /// 完了率UI（テキスト+BG画像）の表示/非表示を設定.
        /// 説明ウィンドウ表示中は非表示にする.
        /// </summary>
        private void SetCompletionRateVisible(bool visible)
        {
            if (completionRateText != null)
                completionRateText.gameObject.SetActive(visible);
            if (percentBGImage != null)
                percentBGImage.gameObject.SetActive(visible);
        }

        /// <summary>
        /// 完了率BG画像のサイズをテキストに合わせて更新.
        /// ピボットは左上前提のため位置調整不要.
        /// </summary>
        private void UpdatePercentBGSize()
        {
            if (percentBGRect == null || completionRateText == null) return;

            // テキストのpreferredサイズに合わせてBGをリサイズ.
            float w = completionRateText.preferredWidth;
            float h = completionRateText.preferredHeight;
            percentBGRect.sizeDelta = new Vector2(w, h);
        }
    }
}
