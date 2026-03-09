using Common;
using InGame;
using UnityEngine;
using R3;
using Cysharp.Threading.Tasks;

namespace InGame.Enemy
{

    public class EnemyTemporayPresenter : MonoBehaviour
    {

        private Animator animator;

        private EnemyModelTemporary model;

        private EnemyStatus_Temporay status;

        private EnemyUIView view;

        void Start()
        {
            // アニメーター取得.
            animator = GetComponent<Animator>();

            model = gameObject.AddComponent<EnemyModelTemporary>();
            EnemyStart().Forget();
            //Observable.EveryUpdate()
            //    .Where(_ => Input.GetKeyDown(KeyCode.P))
            //    .Take(1)
            //    .Subscribe(_ =>
            //    {
            //        EnemyStart().Forget();
            //    }).AddTo(this);

            status = gameObject.AddComponent<EnemyStatus_Temporay>();
        }

        private UniTask EnemyStart()
        {
            //EnemyUIInstance.Instance().EnemyUIStartAnim(status);
            model.Init();

            Observable.EveryUpdate()
                .Where(_ => Input.GetKeyDown(KeyCode.P))
                .Subscribe(_ =>
                {
                    status.OnDamaged(10).Forget();

                }).AddTo(this);

            return UniTask.CompletedTask;
        }
    }
}