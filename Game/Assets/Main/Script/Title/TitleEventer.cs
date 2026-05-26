using System.Threading;
using Common;
using Cysharp.Threading.Tasks;
using InGame.Enemy;
using SceneInfo;
using UnityEngine;
using UnityEngine.Experimental.Video;
using UnityEngine.UI;
using UnityEngine.Video;

namespace SceneEventer
{
    public class TitleEventer : ButtonEventer
    {
        [SerializeField]
        private Button gameStart;
        [SerializeField]
        private Button newGame;
        [SerializeField]
        private Button setting;
        [SerializeField]
        private Button QuitGame;

        [SerializeField]
        private VideoPlayer videoPlayer;
        [SerializeField]
        private CanvasGroup videoCanvasGroup;

        // アニメーションループ状態管理.
        private IStateAnimeloop currentAnimeState;

        // ビデオフェード制御.
        private CancellationTokenSource videoFadeCts;

        // 入力監視フラグ.
        private bool isInputMonitoring = false;
        private bool inputDetected = false;

        //ここで全ての処理のボタン押したらっていう処理を書く
        protected override void ButtonEvents(Button button)
        {
            switch (button)
            {
                case var _ when button == gameStart:
                    GameStart();
                    break;
                case var _ when button == newGame:
                    GameStart();
                    break;
                case var _ when button == setting:
                    break;
                case var _ when button == QuitGame:
                    Quit();
                    break;
            }
        }

        protected override void Init()
        {
            buttons = new Button[][]
            {
                    //new Button[]{gameStart},
                    new Button[]{newGame},
                    //new Button[]{setting},
                    new Button[]{QuitGame}
            };
        }

        private void Start()
        {
            // 初期状態: UI操作有効（ループ停止状態）.
            currentAnimeState = new stateAnimeloop();
            currentAnimeState.OnEnter(this);
        }

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
            videoPlayer.Stop();
            videoPlayer.enabled = false;
            videoPlayer.gameObject.SetActive(false);
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

        public void GameStart()
        {
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
            Debug.Log("[stateAnimeloop] OnEnter - ビデオ再生開始");
            // UI操作を停止.
            eventer.DisableEventer();
            // ビデオ再生開始.
            eventer.PlayVideo();

            // (既)修: 停止条件の監視をここで呼び出し.
            eventer.StartInputMonitoring();
        }

        public IStateAnimeloop Update(TitleEventer eventer)
        {
            // 入力監視で検出された入力を消費して遷移判定.
            if (eventer.ConsumeInputDetected())
            {
                Debug.Log("[stateAnimeloop] 入力検出 → stateAnimeloopStopへ遷移");
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
        private const float idleTimeout = 10f;

        public void OnEnter(TitleEventer eventer)
        {
            Debug.Log("[stateAnimeloopStop] OnEnter - ビデオ停止");
            // UI操作を有効化.
            eventer.EnableEventer();
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
