using R3.Triggers;
using UnityEngine;
using R3;
using InGame.Player;

namespace GameCommon
{
    /// <summary>
    /// ゲームプレイCommon
    /// </summary>
    public abstract class EventPointAbstract : MonoBehaviour
    {
        [SerializeField]
        private GameObject viewObject;

        public void Awake()
        {
            gameObject.OnTriggerEnter2DAsObservable()
                .Where(_ => _?.GetComponent<PlayerAttach>() != null)
                .Subscribe(_ => {
                    InPlayer();
                }).AddTo(gameObject);
            gameObject.OnTriggerExit2DAsObservable()
                .Where(_ => _?.GetComponent<PlayerAttach>() != null)
                .Subscribe(_ =>
                {
                    OutPlayer();
                }).AddTo(gameObject);
        }

        public void PlayerSearch(){}
        /// <summary>
        /// Playerが入ったとき
        /// </summary>
        public void InPlayer()
        {
            viewObject.SetActive(true);
        }
        /// <summary>
        /// Playerが出たとき
        /// </summary>
        public void OutPlayer()
        {
            viewObject.SetActive(false);
        }



        /// <summary>
        /// 何かイベントを書いていく
        /// </summary>
        public virtual void OnEvent()
        {
            
        }
    }
}
