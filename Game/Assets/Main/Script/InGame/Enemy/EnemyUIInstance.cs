using Common;
using Cysharp.Threading.Tasks;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System;

namespace InGame.Enemy
{

    public class EnemyUIInstance
    {
        EnemyUIInstance ui = null;
        public EnemyUIInstance UI { get { return ui; } set { ui = (ui == null) ? value : ui; } }

        public async UniTask EnemyUIStartAnim(EnemyStatus_abstract status)
        {
            await UniTask.WaitUntil(() => ui != null);

            //ui.name.text = status.name;

            // 初期値を設定.
            //ui.hp.fillAmount = (float)status.hp.Value / (float)status.maxhp;

            // hp変更時の購読.
            status.hp
                .Subscribe(_ =>
                {
                    //ui.hp.fillAmount = (float)_ / (float)status.maxhp;
                    //Debug.Log($"HP fillAmount updated: {ui.hp.fillAmount} (HP: {_}/{status.maxhp})");
                });
                //.AddTo(this);


        }

    }
}