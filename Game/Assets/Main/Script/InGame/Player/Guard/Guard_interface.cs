using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InGame.Player
{
    /// <summary>
    /// ガード機能のインターフェース.
    /// </summary>
    public interface IGuard
    {
        /// <summary>
        /// PlayerModel を注入.
        /// </summary>
        void Inject(PlayerControllModel model);

        /// <summary>
        /// InputAction を設定.
        /// </summary>
        void SetAction(InputAction action);

        /// <summary>
        /// ガード実行可能か判定.
        /// </summary>
        bool CanExecute();

        /// <summary>
        /// 攻撃を受けたことを受け取る.
        /// </summary>
        /// <returns>攻撃を受けた時点のガード状態.</returns>
        GuardState OnReceiveAttack();

        /// <summary>
        /// ガード開始.
        /// </summary>
        void GuardStart();

        /// <summary>
        /// ガード終了.
        /// </summary>
        void GuardEnd();

        /// <summary>
        /// ガード中かどうか.
        /// </summary>
        bool IsGuarding { get; }

        /// <summary>
        /// 現在のガード状態を取得.
        /// </summary>
        GuardState CurrentGuardState { get; }

        /// <summary>
        /// 現在のガードPowerlevelを取得.
        /// </summary>
        int GetGuardPowerlevel();

        /// <summary>
        /// Powerlevelで上回られているか判定.
        /// </summary>
        /// <param name="attackPowerlevel">攻撃側のPowerlevel.</param>
        /// <returns>上回られている場合true.</returns>
        bool IsOverpowered(int attackPowerlevel);
    }
}
