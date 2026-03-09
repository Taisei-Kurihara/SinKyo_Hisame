using Cysharp.Threading.Tasks;
using UnityEngine;
using Common;
using System;
using R3;

namespace InGame.Player
{
    public class PlayerHealModel
    {
        PlayerHealModel(GameObject _obje,PlayerStatusModel _playerStatusModel,PlayerStatusInitModel initModel,PlayerActivator _activator)
        {
            parent = _obje;
            playerStatusModel = _playerStatusModel;
            activator = _activator;
        }
        GameObject parent;
        PlayerActivator activator;

        //回数
        public ReactiveProperty<int> healPoint { get; private set; }
            = new ReactiveProperty<int>(3);

        PlayerStatusModel playerStatusModel;

        float speed=0.5f;
        CoolTimeBuilder coolTime = new CoolTimeBuilder();

        int healPower = 30;

        /// <summary>
        /// 回復実行（ただし、アニメーションはない為クールタイム処理のみ
        /// </summary>
        public void OnHeal()
        {
            if (healPoint.Value <= 0) return;
            healPoint.Value--;

            coolTime.LinkTo(parent)
                .SetTime(TimeSpan.FromSeconds(speed))
                .OnStart(() => {
                    activator.EnableBattle();
                    

                }).OnComplete(() => {
                    activator.OffDisableMove();  
                    playerStatusModel.IncrementHp(healPower); })
                .Run();
        }
    }
}