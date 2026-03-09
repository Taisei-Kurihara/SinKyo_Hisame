using System;
using Common;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using R3;
using System.Collections.Generic;

namespace Common
{
    /// <summary>
    /// PoseのButtonイベント設定。継承元（Basic）
    /// </summary>
    public abstract class ButtonEventer : MonoBehaviour
    {
        //イベンターの活性化条件
        private bool eventerEnable=true;
        /// <summary>
        /// 
        /// </summary>
        public void EnableEventer()
        {
            eventerEnable = true;
        }
        /// <summary>
        ///  
        /// </summary>
        public void DisableEventer()
        {
            eventerEnable = false;
        }

        //
        private static readonly int Highlighted = Animator.StringToHash("Highlighted");
        private static readonly int Selected = Animator.StringToHash("Selected");
        private static readonly int Normal = Animator.StringToHash("Normal");
        private static readonly int Pressed = Animator.StringToHash("Pressed");


        protected InputSystem_Actions action;
        protected virtual Button[][] buttons { get; set; }
        private Button previousButton;

        private HashSet<Button> alreadyButtonEvent;

        private int currentIndex_x = 0;
        private int currentIndex_y = 0;


        private float navigateCooldown = 0.2f;
        private float lastNavigateTime = 0f;

        private CompositeDisposable disposables = new CompositeDisposable();

        //ボタン専用の
        private CoolTimeBuilder buttonsCoolTime = new CoolTimeBuilder();


        public void Awake()
        {

            alreadyButtonEvent = new HashSet<Button>();

            action = InputSystemActionsManager.Instance().GetInputSystem_Actions();

            Init();
            //最初のボタン
            SelectButton(buttons[currentIndex_y][currentIndex_x]);
            //全ボタンをTimeScale関係無しに作動させるようにする
            foreach (var row in buttons)
            {
                foreach (var button in row)
                {
                    Animator animator = button.GetComponent<Animator>();

                    if (!alreadyButtonEvent.Contains(button))
                    {
                        alreadyButtonEvent.Add(button);
                        //自動でInitで登録したButtonに対してイベントを設定する
                        AddHoverEvents(button);
                        AddButtonEvent(button);
                        if (animator != null)
                        {
                            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                        }
                    }
                }
            }
        }

        public void Update()
        {
            CursolUpdate();
        }



        /// <summary>
        /// ボタンのイベント付けとボタンの格納を全て行う
        /// </summary>
        protected virtual void Init()
        {
            /*
            buttons = new Button[] { PlayButton, RestartButton, TitleButton };
            */
        }

        public void AddButtonEvent(Button target)
        {
            disposables.Add(
                target.OnClickAsObservable()
                .Subscribe(_ =>
                {
                    if (eventerEnable)
                    {
                        //0.1秒待機
                        buttonsCoolTime.SetTime(TimeSpan.FromSeconds(0.1f))
                        .LinkTo(gameObject)
                        .OnStart(() => ButtonEvents(target))
                        .Run();
                    }
                }));
        }
        /// <summary>
        /// 実行処理（OnClick用）
        /// </summary>
        /// <param name="button"></param>
        protected virtual void ButtonEvents(Button button)
        {
            /*
            switch (button)
            {
                case var _ when button == PlayButton:
                    OnPlay();
                    break;
                case var _ when button == RestartButton:
                    OnRestart();
                    break;
                case var _ when button == TitleButton:
                    OnTitle();
                    break;
            }*/

        }

        /// <summary>
        /// カーソルのアップデート処理(コントローラー対応）
        /// </summary>
        private void CursolUpdate()
        {
            Vector2 nav = action.UI.Navigate.ReadValue<Vector2>();
            // 時間経過チェック（入力連打防止）
            if (Time.unscaledTime - lastNavigateTime < navigateCooldown) return;

            if (nav.y > 0.5f) // 上方向
            {
                currentIndex_y = (currentIndex_y - 1 + buttons.Length) % buttons.Length;
                SelectButton(buttons[currentIndex_y][currentIndex_x]);
                lastNavigateTime = Time.unscaledTime;
            }
            else if (nav.y < -0.5f) // 下方向
            {
                currentIndex_y = (currentIndex_y + 1) % buttons.Length;
                SelectButton(buttons[currentIndex_y][currentIndex_x]);
                lastNavigateTime = Time.unscaledTime;
            }

            if (action.UI.Submit.WasPressedThisFrame())
            {
                //クールタイム処理(共通）
                buttonsCoolTime.SetTime(TimeSpan.FromSeconds(1f)).LinkTo(gameObject)
                    .OnStart(() => ButtonEvents(buttons[currentIndex_y][currentIndex_x]))
                    .Run();
            }
        }


        /// <summary>
        /// ここでボタンが選ばれた時、という処理
        /// </summary>
        /// <param name="button"></param>
        private void SelectButton(Button button)
        {
            // 前のボタンをNormalに戻す
            if (previousButton != null && previousButton != button)
            {
                Animator prevAnimator = previousButton.GetComponent<Animator>();
                if (prevAnimator != null)
                {
                    prevAnimator.SetTrigger(Normal);
                }
            }

            // 現在のボタンを選択状態に
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            button.Select();

            Animator animator = button.GetComponent<Animator>();
            if (animator != null)
            {
                animator.SetTrigger(Highlighted);
            }

            // 次回のために記録
            previousButton = button;
        }



        /// <summary>
        /// ButtonのAnimation実行（イベント付与）
        /// </summary>
        /// <param name="target"></param>
        protected void AddHoverEvents(Button target)
        {
            Animator animator = target.gameObject.GetComponent<Animator>();
            // EventTrigger がなければ追加
            EventTrigger trigger = target.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = target.gameObject.AddComponent<EventTrigger>();
            }

            //PointerEnter
            EventTrigger.Entry entryEnter = new EventTrigger.Entry();
            entryEnter.eventID = EventTriggerType.PointerEnter;
            entryEnter.callback.AddListener((eventData) => {
                //マウスで選択していた場合、リセットする。
                foreach (var row in buttons)
                {
                    foreach (var button in row)
                    {
                        if (button != target)
                        {
                            Animator animator = button.GetComponent<Animator>();
                            animator.SetTrigger(Normal);
                        }
                    }
                }
                animator.SetTrigger(Highlighted);
            });
            trigger.triggers.Add(entryEnter);

            //PointerUp
            EventTrigger.Entry entryUp = new EventTrigger.Entry();
            entryUp.eventID = EventTriggerType.PointerUp;
            entryUp.callback.AddListener((eventData) => {
                animator.SetTrigger(Highlighted);

                //ここ1012に追記
                animator.ResetTrigger(Pressed);
            });
            trigger.triggers.Add(entryUp);

            //PointerDown
            EventTrigger.Entry entryDown = new EventTrigger.Entry();
            entryDown.eventID = EventTriggerType.PointerDown;
            entryDown.callback.AddListener((eventData) => {
                animator.SetTrigger(Pressed);
            });
            trigger.triggers.Add(entryDown);

            //PointerExit
            EventTrigger.Entry entryExit = new EventTrigger.Entry();
            entryExit.eventID = EventTriggerType.PointerExit;
            entryExit.callback.AddListener((eventData) => {

                animator.ResetTrigger(Highlighted);
                animator.SetTrigger(Normal);
            });
            trigger.triggers.Add(entryExit);
        }


        private void OnDestroy()
        {
            disposables?.Dispose();
        }
    }
}
