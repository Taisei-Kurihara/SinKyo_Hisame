using System.Collections.Generic;
using System.Threading;
using Audio;
using Common;
using Cysharp.Threading.Tasks;
using InGame.Common;
using InGame.Enemy;
using SceneInfo;
using UnityEngine;
using UnityEngine.Experimental.Video;
using UnityEngine.UI;
using UnityEngine.Video;

namespace SceneEventer
{
    public class TitleEventer : MenuButtonEventer
    {
        // ============================
        // === 変数 ===
        // ============================

        /// <summary>
        /// Inspector で設定するボタン列配列（2次元配列の列ラッパー）.
        /// buttons[x][y]: x=横ナビ列, y=縦ナビ行.
        /// 例: Column0=[チュートリアル, スタート, 終了], Column1=[言語切替]
        /// </summary>
        [SerializeField]
        private MenuButtonRow[] buttonRows;

        [Header("タイトルアニメーション")]
        [SerializeField] private Animator Hisame;

        [SerializeField]
        private VideoPlayer videoPlayer;
        [SerializeField]
        private bool isVideoPlay = true;
        [SerializeField]
        private CanvasGroup videoCanvasGroup;

        // アニメーションループ状態管理.
        private IStateAnimeloop currentAnimeState;

        // ビデオフェード制御.
        private CancellationTokenSource videoFadeCts;

        // 入力監視フラグ.
        private bool isInputMonitoring = false;
        private bool inputDetected = false;

        // ビデオ自然終了フラグ.
        private bool videoFinished = false;

        // タイトルSE.
        private SEPlayer titleSEPlayer;

        // ボタンラベル辞書（MenuButtonIndex → { JP文字列, EN文字列 }）.
        // タイトル画面では全言語のデータを常時メモリに保持し、言語切り替えを即時反映する.
        private static readonly Dictionary<MenuButtonIndex, string[]> buttonLabelMap =
            new Dictionary<MenuButtonIndex, string[]>
        {
            { MenuButtonIndex.TitleTutorial,        new[] { "修練所",      "Tutorial"          } },
            { MenuButtonIndex.TitleGameStartNormal, new[] { "討伐",        "Game Start"        } },
            { MenuButtonIndex.TitleGameStartEasy,   new[] { "瞑想",        "Game Start (Easy)" } },
            { MenuButtonIndex.TitleGameQuit,        new[] { "終了",        "Quit"              } },
            { MenuButtonIndex.Language,             new[] { "(日)/ EN",    "日 /(EN)"          } },
        };

        // ラベルキャッシュ: (TextMeshProUGUI, string[JP,EN]) のフラットリスト.
        // InitMenuButtonLabel() 完了後に OnLanguageChanged 初回呼び出しで構築される.
        private List<(TMPro.TextMeshProUGUI label, string[] texts)> _labelCache;

        // buttonsSlot 実装（abstract 強制）.
        private ButtonSlotDictionary _buttonsSlot;
        protected override ButtonSlotDictionary buttonsSlot => _buttonsSlot;

        [Header("ミッション設定")]
        [Tooltip("難易度")]
        [SerializeField] private MissionTag difficulty = MissionTag.Difficulty_Normal;

        [Tooltip("条件")]
        [SerializeField] private MissionTag condition = MissionTag.Condition_BossNormal;

        [Tooltip("Enemy名")]
        [SerializeField] private MissionTag enemyName = MissionTag.Enemy_Wendigo;

        public const string MissionTagsPrefsKey = "MissionTags";

        /// <summary>選択中のミッションタグ（全カテゴリ合成）.</summary>
        public MissionTag SelectedTags => difficulty | condition | enemyName;

        // ============================
        // === 初期化するための関数 ===
        // ============================

        protected override void Init()
        {
            // タイトル画面はテキストスライドアニメーションを使用.
            animatorType = MenuButtonAnimatorType.TextSlide;

            // スロット定義: { MenuButtonIndex, 一意のslotID, 発火アクション }
            _buttonsSlot = new ButtonSlotDictionary()
            {
                { MenuButtonIndex.TitleTutorial,        0, TutorialStart,  ButtonFireMode.EarlyFire },
                { MenuButtonIndex.TitleGameStartNormal, 1, GameStart,      ButtonFireMode.EarlyFire },
                { MenuButtonIndex.TitleGameStartEasy,   2, GameStartEasy,  ButtonFireMode.EarlyFire },
                { MenuButtonIndex.TitleGameQuit,        3, Quit,           ButtonFireMode.EarlyFire },
                { MenuButtonIndex.Language,             4, ToggleLanguage, ButtonFireMode.Immediate },
            };

            // Inspector の buttonRows から buttons 2次元配列を構築.
            // 未設定（Inspector設定待ち）の場合は空配列で初期化してクラッシュを防ぐ.
            if (buttonRows != null && buttonRows.Length > 0)
            {
                buttons = new MenuButton[buttonRows.Length][];
                for (int i = 0; i < buttonRows.Length; i++)
                    buttons[i] = buttonRows[i]?.buttons ?? new MenuButton[0];
            }
            else
            {
                buttons = new MenuButton[0][];
            }

            if (!isVideoPlay)
            {
                videoPlayer?.gameObject.SetActive(false);
                videoPlayer = null;
            }
        }

        private void ToggleLanguage()
        {
            Common.LanguageManager.Instance()?.ToggleLanguage();
        }

        private void Start()
        {
            // Hisame: 時間停止中でも再生できるよう UnscaledTime に設定 + タイトル開幕アニメーション発火.
            if (Hisame != null)
            {
                Hisame.updateMode = AnimatorUpdateMode.UnscaledTime;
                Hisame.SetTrigger("Title");
            }

            // 初期状態: ビデオ未再生・UI操作有効.
            currentAnimeState = new stateAnimeloopStop();
            currentAnimeState.OnEnter(this);

            // SE初期化（SEファイルが存在しなくてもエラーにならない）.
            InitializeTitleSE().Forget();

            // 起動時に現在の言語でラベルを適用.
            var lang = Common.LanguageManager.Instance(false)?.CurrentLanguage ?? GameLanguage.Japanese;
            OnLanguageChanged(lang);
        }

        protected override void OnLanguageChanged(GameLanguage lang)
        {
            if (buttons == null) return;

            // 初回呼び出し時にキャッシュを構築（Awake の InitMenuButtonLabel() 完了後に実行される）.
            if (_labelCache == null)
            {
                _labelCache = new List<(TMPro.TextMeshProUGUI, string[])>();
                foreach (var col in buttons)
                {
                    if (col == null) continue;
                    foreach (var mb in col)
                    {
                        if (mb == null || mb.labelText == null) continue;
                        if (buttonLabelMap.TryGetValue(mb.index, out var texts))
                            _labelCache.Add((mb.labelText, texts));
                    }
                }
            }

            int li = lang == GameLanguage.English ? 1 : 0;
            foreach (var (label, texts) in _labelCache)
                label.text = texts[li];
        }

        private async UniTaskVoid InitializeTitleSE()
        {
            titleSEPlayer = SEPlayer.Create("TitleSE");
            await titleSEPlayer.LoadClipsAsync("SE_Title_Select", "SE_Title_Submit");
        }

        protected override void OnButtonSelected(MenuButton button)
        {
            titleSEPlayer?.Play("SE_Title_Select");
        }

        protected override void OnButtonSubmitted(MenuButton button)
        {
            titleSEPlayer?.Play("SE_Title_Submit");
            // Language は buttonsSlot に Immediate で登録済みのため、ここでは処理しない.
        }

        public void GameStart()
        {
            // チュートリアルモード解除（残留防止）.
            PlayerPrefs.SetInt("TutorialMode", 0);

            // Enemy名 → EnemyName enumへの変換.
            EnemyName enemy = MissionTagToEnemyName(enemyName);
            PlayerPrefs.SetInt("EnemyName", (int)enemy);

            // MissionTags（難易度+条件+Enemy名の合成）を保存.
            PlayerPrefs.SetInt(MissionTagsPrefsKey, (int)SelectedTags);
            PlayerPrefs.Save();

            Debug.Log($"[StageSelect] 難易度:{difficulty} 条件:{condition} Enemy:{enemyName} → Tags:{SelectedTags} ({(int)SelectedTags})");

            // MainSceneInfo で敵生成を含むシーンをロード.
            SceneManager.Instance().LoadMainScene(new MainSceneInfo()).Forget();
        }

        public void GameStartEasy()
        {
            // チュートリアルシーンを経由しない（直接ゲームシーンへ）.
            PlayerPrefs.SetInt("TutorialMode", 0);

            // Enemy名 → EnemyName enumへの変換.
            EnemyName enemy = MissionTagToEnemyName(enemyName);
            PlayerPrefs.SetInt("EnemyName", (int)enemy);

            // 難易度を Difficulty_Easy に固定して MissionTags を保存.
            MissionTag easyTags = MissionTag.Difficulty_Easy | condition | enemyName;
            PlayerPrefs.SetInt(MissionTagsPrefsKey, (int)easyTags);
            PlayerPrefs.Save();

            Debug.Log($"[StageSelect] 難易度:Easy 条件:{condition} Enemy:{enemyName} → Tags:{easyTags} ({(int)easyTags})");

            // ゲームシーンへ直接遷移（EnemyHP=1万、左側に操作一覧表示）.
            SceneManager.Instance().LoadMainScene(new MainSceneInfo()).Forget();
        }

        public void TutorialStart()
        {
            // チュートリアルモード PlayerPrefs 設定.
            PlayerPrefs.SetInt("TutorialMode", 1);
            PlayerPrefs.Save();

            SceneManager.Instance().LoadMainScene(new TutorialInfo()).Forget();
        }

        public void NewGame()
        {
            SceneManager.Instance().LoadMainScene(new StageSelectInfo()).Forget();
        }

        /// <summary>
        /// MissionTag(Enemy_*) → EnemyName enum変換.
        /// </summary>
        private static EnemyName MissionTagToEnemyName(MissionTag tag)
        {
            if ((tag & MissionTag.Enemy_Wendigo) != 0) return EnemyName.Wendigo;
            return EnemyName.None;
        }

        /// <summary>
        /// Game終了処理。
        /// </summary>
        public void Quit()
        {
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }

        // ============================
        // === 更新処理 ===
        // ============================

        private void LateUpdate()
        {
            // 入力監視中に入力があればフラグを立てる.
            if (isInputMonitoring && HasAnyInput())
            {
                inputDetected = true;
            }

            if (currentAnimeState != null)
            {
                currentAnimeState = currentAnimeState.Update(this);
            }
        }

        // ============================
        // === 更新処理が使用する関数 ===
        // ============================

        /// <summary>
        /// ビデオ再生開始.
        /// </summary>
        public void PlayVideo()
        {
            // 進行中のフェードをキャンセル.
            videoFadeCts?.Cancel();
            videoFadeCts?.Dispose();
            videoFadeCts = null;

            if (videoPlayer == null) return;
            if (videoCanvasGroup != null) videoCanvasGroup.alpha = 1f;
            videoPlayer.gameObject.SetActive(true);
            videoPlayer.enabled = true;
            videoPlayer.isLooping = false;
            videoFinished = false;
            // 二重登録防止のため先に解除してから登録.
            videoPlayer.loopPointReached -= OnVideoLoopPointReached;
            videoPlayer.loopPointReached += OnVideoLoopPointReached;
            videoPlayer.Play();
        }

        /// <summary>
        /// ビデオ停止（CanvasGroupフェードアウト後に非表示）.
        /// </summary>
        public void StopVideo()
        {
            // 進行中のフェードをキャンセル.
            videoFadeCts?.Cancel();
            videoFadeCts?.Dispose();
            videoFadeCts = new CancellationTokenSource();
            StopVideoAsync(videoFadeCts.Token).Forget();
        }

        private async UniTask StopVideoAsync(CancellationToken token)
        {
            if (videoPlayer == null) return;

            // CanvasGroupフェードアウト (15f/30f = 0.5秒).
            if (videoCanvasGroup != null)
            {
                float duration = 15f / 30f;
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    videoCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
                    await UniTask.Yield(token);
                }
                videoCanvasGroup.alpha = 0f;
            }

            // フェード完了後にビデオ停止 + 非表示.
            videoPlayer.loopPointReached -= OnVideoLoopPointReached;
            videoPlayer.Stop();
            videoPlayer.enabled = false;
            videoPlayer.gameObject.SetActive(false);
        }

        // --- input があった時の関数 ---

        /// <summary>
        /// 入力監視を開始.
        /// </summary>
        public void StartInputMonitoring()
        {
            isInputMonitoring = true;
            inputDetected = false;
        }

        /// <summary>
        /// 入力監視を停止.
        /// </summary>
        public void StopInputMonitoring()
        {
            isInputMonitoring = false;
            inputDetected = false;
        }

        /// <summary>
        /// 入力検出フラグを消費して返す.
        /// </summary>
        public bool ConsumeInputDetected()
        {
            if (inputDetected)
            {
                inputDetected = false;
                return true;
            }
            return false;
        }

        /// <summary>
        /// ビデオ自然終了フラグを消費して返す.
        /// </summary>
        public bool ConsumeVideoFinished()
        {
            if (videoFinished)
            {
                videoFinished = false;
                return true;
            }
            return false;
        }

        private void OnVideoLoopPointReached(VideoPlayer source)
        {
            videoFinished = true;
        }

        /// <summary>
        /// 何かしらの入力があるか判定.
        /// </summary>
        public bool HasAnyInput()
        {
            // キーボード + マウスボタン（旧InputSystem）.
            if (Input.anyKey) return true;
            // マウスクリック（InputSystem）.
            if (action.UI.Click.IsPressed()) return true;
            if (action.UI.RightClick.IsPressed()) return true;
            if (action.UI.MiddleClick.IsPressed()) return true;
            // マウススクロール.
            Vector2 scroll = action.UI.ScrollWheel.ReadValue<Vector2>();
            if (scroll.sqrMagnitude > 0.01f) return true;
            // コントローラーのスティック入力検出.
            Vector2 nav = action.UI.Navigate.ReadValue<Vector2>();
            if (nav.sqrMagnitude > 0.25f) return true;
            // コントローラーのボタン入力検出（Submit/Cancel）.
            if (action.UI.Submit.IsPressed()) return true;
            if (action.UI.Cancel.IsPressed()) return true;
            return false;
        }
    }

    public interface IStateAnimeloop
    {
        /// <summary>
        /// 状態更新。次の状態を返す.
        /// </summary>
        IStateAnimeloop Update(TitleEventer eventer);

        /// <summary>
        /// 状態開始時の処理.
        /// </summary>
        void OnEnter(TitleEventer eventer);
    }

    public class stateAnimeloop : IStateAnimeloop
    {

        public void OnEnter(TitleEventer eventer)
        {
            // UI操作を停止.
            eventer.DisableEventer();
            // ビデオ再生開始.
            eventer.PlayVideo();

            eventer.StartInputMonitoring();
        }

        public IStateAnimeloop Update(TitleEventer eventer)
        {
            // 入力監視で検出された入力を消費して遷移判定.
            if (eventer.ConsumeInputDetected())
            {
                eventer.StopInputMonitoring();
                var next = new stateAnimeloopStop();
                next.OnEnter(eventer);
                return next;
            }
            // 動画が自然終了したらタイトルに戻る.
            if (eventer.ConsumeVideoFinished())
            {
                eventer.StopInputMonitoring();
                var next = new stateAnimeloopStop();
                next.OnEnter(eventer);
                return next;
            }
            return this;
        }
    }

    public class stateAnimeloopStop : IStateAnimeloop
    {
        private float idleTimer = 0f;
        // 無入力タイムアウト秒数.
        private const float idleTimeout = 20f;

        public void OnEnter(TitleEventer eventer)
        {
            // UI操作を有効化 + ボタン色を暗い状態にリセットしてカーソルを先頭へ.
            eventer.EnableEventer();
            eventer.ResetSelection();
            // ビデオ停止.
            eventer.StopVideo();
            idleTimer = 0f;
        }

        public IStateAnimeloop Update(TitleEventer eventer)
        {
            // 入力があればタイマーリセット.
            if (eventer.HasAnyInput())
            {
                idleTimer = 0f;
                return this;
            }

            // 無入力時間を計測.
            idleTimer += Time.unscaledDeltaTime;
            if (idleTimer >= idleTimeout)
            {
                var next = new stateAnimeloop();
                next.OnEnter(eventer);
                return next;
            }
            return this;
        }
    }
}