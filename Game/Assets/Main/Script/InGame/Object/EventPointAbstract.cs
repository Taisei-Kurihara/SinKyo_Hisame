using R3.Triggers;
using UnityEngine;
using R3;
using InGame.Player;
using Common;

namespace GameCommon
{
    /// <summary>
    /// ゲームプレイCommon
    /// </summary>
    public abstract class EventPointAbstract : MonoBehaviour
    {
        [SerializeField]
        private GameObject viewObject;

        [Header("長押し発動設定")]
        [SerializeField] private float holdMaxValue = 100f;
        [SerializeField] private float holdSpeed = 50f;
        [SerializeField] private bool resetOnRelease = true;

        protected float holdValue = 0f;
        protected bool isPlayerInside = false;
        protected bool isEventInvoked = false;

        private InputSystem_Actions inputActions;
        private CompositeDisposable disposables = new CompositeDisposable();

        public void Awake()
        {
            inputActions = InputSystemActionsManager.Instance().GetInputSystem_Actions();

            gameObject.OnTriggerEnter2DAsObservable()
                .Where(col => col != null && col.GetComponent<PlayerAttach>() != null)
                .Subscribe(_ =>
                {
                    InPlayer();
                })
                .AddTo(disposables);

            gameObject.OnTriggerExit2DAsObservable()
                .Where(col => col != null && col.GetComponent<PlayerAttach>() != null)
                .Subscribe(_ =>
                {
                    OutPlayer();
                })
                .AddTo(disposables);

            Observable.EveryUpdate()
                .Subscribe(_ =>
                {
                    UpdateHeartResistHold();
                })
                .AddTo(disposables);
        }

        /// <summary>
        /// HeartResist長押し進行処理
        /// </summary>
        private void UpdateHeartResistHold()
        {
            if (!isPlayerInside || isEventInvoked || inputActions == null)
            {
                return;
            }

            if (inputActions.CharacterController.HeartResist.IsPressed())
            {
                holdValue += holdSpeed * Time.deltaTime;
                holdValue = Mathf.Clamp(holdValue, 0f, holdMaxValue);

                // 必要ならここでUI更新
                // OnHoldProgress(holdValue / holdMaxValue);

                if (holdValue >= holdMaxValue)
                {
                    isEventInvoked = true;
                    OnEvent();
                }
            }
            else
            {
                if (resetOnRelease)
                {
                    holdValue = 0f;
                    // 必要ならここでUI更新
                    // OnHoldProgress(0f);
                }
            }
        }


        public void PlayerSearch(){}
        /// <summary>
        /// Playerが入ったとき
        /// </summary>
        public void InPlayer()
        {
            isPlayerInside = true;
            holdValue = 0f;
            isEventInvoked = false;

            if (viewObject != null)
            {
                viewObject.SetActive(true);
            }
        }
        /// <summary>
        /// Playerが出たとき
        /// </summary>
        public void OutPlayer()
        {
            isPlayerInside = false;
            holdValue = 0f;
            isEventInvoked = false;

            if (viewObject != null)
            {
                viewObject.SetActive(false);
            }
        }



        /// <summary>
        /// 何かイベントを書いていく
        /// </summary>
        public virtual void OnEvent()
        {
            
        }

        public virtual void OnDestroy()
        {
            disposables?.Dispose();
        }
    }
}
