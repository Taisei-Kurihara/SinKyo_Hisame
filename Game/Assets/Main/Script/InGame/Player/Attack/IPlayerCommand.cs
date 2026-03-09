using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InGame.Player
{
    public abstract class AbstructFirstAttack
    {
        protected PlayerControllModel playerModel;

        /// <summary>
        /// PlayerModel を注入（シーン切替対応）
        /// </summary>
        public void Inject(PlayerControllModel model)
        {
            playerModel = model;
        }

        /// <summary>
        /// 実行可能かどうかの共通チェック
        /// </summary>
        protected bool CanExecute()
        {
            if (playerModel == null) return false;
            //if (!playerModel.IsAlive()) return false;
            if (playerModel.enableAction) return false;
            return true;
        }

        /// <summary>
        /// 実際の攻撃処理
        /// </summary>
        public abstract void Act();
    }

    public abstract class AbstructSecondAttack
    {
        public virtual string Name { get; }

        public virtual InputAction action { get; set; }

        public void SetAction(InputAction _action)
        {
            action = _action;
        }
        public virtual void Act() { }
    }
    public abstract class AbstructSpecialAttack
    {
        public virtual string Name { get; }
        public virtual InputAction action { get; set; }
        public void SetAction(InputAction _action)
        {
            action = _action;
        }
        public virtual void Act() { }
    }
    public abstract class AbstructRestrainAttack
    {
        public virtual string Name { get; }
        public virtual InputAction action { get; set; }

        public void SetAction(InputAction _action)
        {
            action = _action;
        }
        public virtual void Act() { }
    }
}