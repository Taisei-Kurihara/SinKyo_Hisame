using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using Cysharp.Threading.Tasks;
using InGame.Common;

namespace InGame.Player
{
    /// <summary>
    /// ガード状態管理enum.
    /// </summary>
    public enum GuardState
    {
        None = -1,      // ガードなし.
        Startup = 0,    // 発生.
        Parry = 1,      // パリィ状態.
        Guard = 2,      // ガード.
        Stun = 3        // 硬直.
    }

    /// <summary>
    /// デフォルトガード実装.
    /// </summary>
    public class Guard_Player_Default : IGuard
    {
        /// <summary>
        /// フレームレート(1/30F).
        /// </summary>
        protected static readonly float FrameRate = 1f / 30f;

        /// <summary>
        /// 各ガード状態のフレーム数.
        /// </summary>
        protected static readonly Dictionary<GuardState, int> GuardStateDuration = new Dictionary<GuardState, int>
        {
            { GuardState.Startup, 1 },   // 発生: 3F.
            { GuardState.Parry, 15 },     // パリィ: 15F (0.5秒).
            { GuardState.Guard, 11 },     // ガード: 継続(0は無制限).
            { GuardState.Stun, 10 }      // 硬直: 10F.
        };

        protected PlayerControllModel playerModel;
        protected InputAction action;
        protected bool isGuarding;
        protected float guardStartTime;
        protected int currentGuardStateInt;
        protected CancellationTokenSource guardCts;

        // 防御解除後のクールダウン.
        protected float guardEndCooldown = 0.5f;
        protected float lastGuardEndTime = -1f;

        /// <summary>
        /// ガード中かどうか.
        /// </summary>
        public bool IsGuarding => isGuarding;

        /// <summary>
        /// 現在のガード状態(int).
        /// </summary>
        public int CurrentGuardStateInt => currentGuardStateInt;

        /// <summary>
        /// 現在のガード状態(enum).
        /// </summary>
        public GuardState CurrentGuardState => (GuardState)currentGuardStateInt;

        /// <summary>
        /// PlayerModel を注入.
        /// </summary>
        public void Inject(PlayerControllModel model)
        {
            playerModel = model;
        }

        /// <summary>
        /// InputAction を設定.
        /// </summary>
        public void SetAction(InputAction _action)
        {
            action = _action;
        }

        /// <summary>
        /// ガード実行可能か判定.
        /// </summary>
        public virtual bool CanExecute()
        {
            if (playerModel == null) return false;
            // 攻撃ステート中は入力不可.
            if (playerModel.enableAction) return false;
            // 防御解除後0.5秒間は入力不可.
            if (lastGuardEndTime >= 0 && Time.time - lastGuardEndTime < guardEndCooldown) return false;
            return true;
        }

        /// <summary>
        /// 攻撃を受けたことを受け取る.
        /// </summary>
        /// <returns>攻撃を受けた時点のガード状態.</returns>
        public virtual GuardState OnReceiveAttack()
        {
            // 派生クラスでオーバーライド可能.
            GuardState state = CurrentGuardState;
            GuardEnd();
            return state;
        }

        /// <summary>
        /// ガード開始.
        /// </summary>
        public virtual void GuardStart()
        {
            if (!CanExecute())
            {
                //Debug.Log("[Guard] GuardStart: CanExecute failed");
                return;
            }
            isGuarding = true;
            guardStartTime = Time.time;
            //Debug.Log($"[Guard] GuardStart: isGuarding={isGuarding}");

            // 既存のガード処理をキャンセル.
            guardCts?.Cancel();
            guardCts?.Dispose();
            guardCts = new CancellationTokenSource();

            // ガード状態更新処理を開始.
            UpdateGuardStateAsync(guardCts.Token).Forget();
        }

        /// <summary>
        /// ガード状態を更新する非同期処理.
        /// </summary>
        protected virtual async UniTaskVoid UpdateGuardStateAsync(CancellationToken token)
        {
            try
            {
                // Startup -> Parry -> Guard の順に遷移(Stunは攻撃受け時のみ).
                for (int i = (int)GuardState.Startup; i <= (int)GuardState.Guard; i++)
                {
                    currentGuardStateInt = i;
                    GuardState state = (GuardState)i;
                    int frames = GuardStateDuration[state];
                    //Debug.Log($"[Guard] State changed to: {state} (frames={frames})");
                    await UniTask.Delay(TimeSpan.FromSeconds(frames * FrameRate), cancellationToken: token);
                }
                //Debug.Log("[Guard] Guard state sequence completed");
            }
            catch (OperationCanceledException)
            {
                //Debug.Log("[Guard] Guard cancelled");
            }
        }

        /// <summary>
        /// ガード終了.
        /// </summary>
        public virtual void GuardEnd()
        {
            //Debug.Log($"[Guard] GuardEnd called. isGuarding was: {isGuarding}");

            // ガード状態更新処理をキャンセル.
            guardCts?.Cancel();
            guardCts?.Dispose();
            guardCts = null;

            isGuarding = false;
            currentGuardStateInt = (int)GuardState.None;

            // 防御解除時刻を記録（0.5秒クールダウン用）.
            lastGuardEndTime = Time.time;
        }

        /// <summary>
        /// 現在のガードPowerlevelを取得.
        /// </summary>
        public virtual int GetGuardPowerlevel()
        {
            if (!isGuarding) return 0;

            // パリィ状態ならパリィのPowerlevel、それ以外はガードのPowerlevel.
            if (CurrentGuardState == GuardState.Parry)
            {
                return PowerlevelConst.PlayerParry;
            }
            return PowerlevelConst.PlayerGuard;
        }

        /// <summary>
        /// Powerlevelで上回られているか判定.
        /// </summary>
        /// <param name="attackPowerlevel">攻撃側のPowerlevel.</param>
        /// <returns>上回られている場合true.</returns>
        public virtual bool IsOverpowered(int attackPowerlevel)
        {
            return attackPowerlevel > GetGuardPowerlevel();
        }
    }
}
