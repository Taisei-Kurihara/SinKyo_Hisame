using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace InGame.Player.Animation
{
    /// <summary>
    /// キャラクターアニメーションのコントローラー/Prefab依存
    /// </summary>
    public class PlayerAnimationController : MonoBehaviour, IPlayerAnimation
    {
        private static readonly int IsMoveHash = Animator.StringToHash("Move");//移動中.
        private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");//接地判定.
        private static readonly int AttackHash = Animator.StringToHash("NormalAttackDefault");//通常攻撃.
        private static readonly int DodgeHash = Animator.StringToHash("Dodge");//回避.
        private static readonly int HurtHash = Animator.StringToHash("Hurt");//被ダメージ.
        private static readonly int DeadHash = Animator.StringToHash("Dead");//死亡.
        private static readonly int GuardHash = Animator.StringToHash("Guard");//ガード.
        private static readonly int JumpHash = Animator.StringToHash("Jump");//ジャンプ.
        private static readonly int JumpOnGroundHash = Animator.StringToHash("JumpOnGround");//着地.

        private Animator animator;
        private Rigidbody2D rb;

        // 入力ベースの向き（1f or -1f）. 物理反動では変化しない.
        private float facingSign = 1f;

        // アクション（攻撃等）がanimator.speedを制御中かどうか.
        private bool actionSpeedOverride = false;

        // 攻撃中やHeartResist強中に移動アニメーション遷移を抑制するフラグ.
        private bool suppressMovementAnim = false;

        // 基準歩行速度（PlayerStatusInitModel.speed）. 移動アニメ速度の正規化に使用.
        private const float baseWalkSpeed = 7f;


        public void Awake()
        {
            animator=gameObject.GetComponent<Animator>();
            rb = gameObject.GetComponent<Rigidbody2D>();
        }

        private bool isDead = false;

        /// <summary>
        /// LateUpdate: 他スクリプトの方向変更より後に実行し、方向の最終決定権を保証.
        /// git merge等で旧コードが復活しても、ここが最後に上書きするため二重反転を防止.
        /// </summary>
        public void LateUpdate()
        {
            if (isDead) return;
            SetMove(rb.linearVelocityX);
        }

        /// <summary>
        /// ガード状態設定.
        /// </summary>
        /// <param name="_isGuarding">ガード中かどうか.</param>
        public void SetGuard(bool _isGuarding)
        {
            animator.SetBool(GuardHash, _isGuarding);
        }

        /// <summary>
        /// 移動と左右反転
        /// </summary>
        /// <param name="_move"></param>
        // 速度のデッドゾーン（微小な速度でアニメーションがちらつくのを防止）.
        private const float moveDeadZone = 0.05f;

        public void SetMove(float _move)
        {
            // 歩行アニメーション（物理速度ベース）.
            int moveState = 0;
            if (_move > moveDeadZone) { moveState = 1; }
            else if (_move < -moveDeadZone) { moveState = -1; }

            // 移動アニメ抑制中は常にIdle.
            if (suppressMovementAnim) moveState = 0;
            animator.SetInteger(IsMoveHash, moveState);

            // 移動速度に応じてアニメーション速度を変更（アクション中は除外）.
            if (!actionSpeedOverride)
            {
                if (moveState != 0)
                {
                    float speedRatio = Mathf.Abs(_move) / baseWalkSpeed + 0.5f;
                    animator.speed = Mathf.Clamp(speedRatio, 0.5f, 3.0f);
                }
                else
                {
                    animator.speed = 1.0f;
                }
            }

            // 向き反転は入力ベース（facingSign）で適用. 物理反動では変化しない.
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * facingSign;
            transform.localScale = scale;
        }

        /// <summary>
        /// 入力方向から向きを更新. プレイヤー入力時のみ呼び出す.
        /// 攻撃中は呼ばれないため、物理反動による反転を防止.
        /// </summary>
        public void SetInputDirection(float inputX)
        {
            if (Mathf.Abs(inputX) > moveDeadZone)
            {
                facingSign = inputX > 0 ? -1f : 1f;
            }
        }

        /// <summary>
        /// 接地判定
        /// </summary>
        public void SetGrounded(bool _isGrounded)
        {
            animator.SetBool(IsGroundedHash, _isGrounded);
        }

        /// <summary>
        /// 通常攻撃
        /// </summary>
        public void PlayAttack()
        {
            animator.SetTrigger(AttackHash);
        }

        /// <summary>
        /// 回避
        /// </summary>
        public void PlayDodge()
        {
            animator.SetTrigger(DodgeHash);
        }

        /// <summary>
        /// 被ダメージ
        /// </summary>
        public void PlayHurt()
        {
            animator.SetTrigger(HurtHash);
        }
        /// <summary>
        /// 死亡
        /// </summary>
        public void PlayDead()
        {
            animator.SetTrigger(DeadHash);
        }

        /// <summary>
        /// ジャンプ
        /// </summary>
        public void PlayJump()
        {
            animator.SetTrigger(JumpHash);
        }

        /// <summary>
        /// 着地
        /// </summary>
        public void PlayJumpOnGround()
        {
            animator.SetTrigger(JumpOnGroundHash);
        }

        /// <summary>
        /// 汎用トリガー実行.
        /// </summary>
        /// <param name="triggerName">トリガー名.</param>
        public void PlayTrigger(string triggerName)
        {
            animator.SetTrigger(triggerName);
        }

        // Animation Event コールバック.
        private Action onHitDetectionStart;
        private Action onHitDetectionEnd;

        /// <summary>
        /// Animation Event: 攻撃のhit判定開始タイミング.
        /// アニメーションクリップのキーフレームから呼ばれる.
        /// </summary>
        public void AnimEvent_HitStart() => onHitDetectionStart?.Invoke();

        /// <summary>
        /// Animation Event: 攻撃のhit判定終了タイミング.
        /// アニメーションクリップのキーフレームから呼ばれる.
        /// </summary>
        public void AnimEvent_HitEnd() => onHitDetectionEnd?.Invoke();

        /// <summary>
        /// hit判定コールバックを登録.
        /// </summary>
        public void RegisterHitDetectionCallbacks(Action onStart, Action onEnd)
        {
            onHitDetectionStart = onStart;
            onHitDetectionEnd = onEnd;
        }

        /// <summary>
        /// hit判定コールバックをクリア.
        /// </summary>
        public void ClearHitDetectionCallbacks()
        {
            onHitDetectionStart = null;
            onHitDetectionEnd = null;
        }

        /// <summary>
        /// 移動アニメーション抑制を設定.
        /// 攻撃中やHeartResist強中にMoveパラメータを強制0にする.
        /// </summary>
        public void SetSuppressMovement(bool suppress)
        {
            suppressMovementAnim = suppress;
            if (suppress && animator != null)
                animator.SetInteger(IsMoveHash, 0);
        }

        /// <summary>
        /// suppressMovementAnim有効中、毎フレームアニメーターの状態を確認し、
        /// 移動系に遷移していたら攻撃トリガーを再発火する.
        /// 攻撃アニメーション確認後は再発火せず、完了時に監視終了.
        /// </summary>
        public async UniTask EnsureAttackAnimation(string triggerName)
        {
            // 最大監視フレーム数（無限ループ防止）.
            int maxFrames = 120;
            bool animationConfirmed = false;
            for (int i = 0; i < maxFrames; i++)
            {
                await UniTask.Yield();
                if (animator == null) return;
                // suppress解除されたら監視終了.
                if (!suppressMovementAnim) return;
                var info = animator.GetCurrentAnimatorStateInfo(0);
                bool isInMovementState = info.IsName("Idle") || info.IsName("Walk") || info.IsName("Run");

                if (!animationConfirmed)
                {
                    // 攻撃アニメーション未確認: 移動系なら再発火.
                    if (isInMovementState)
                    {
                        animator.SetTrigger(triggerName);
                    }
                    else
                    {
                        // 攻撃アニメーション確認済み → 以降は再発火しない.
                        animationConfirmed = true;
                    }
                }
                else
                {
                    // 確認済み: アニメーション完了して移動系に復帰 → 監視終了.
                    if (isInMovementState) return;
                }
            }
        }

        /// <summary>
        /// アニメーターを強制的にIdle状態にする.
        /// パリィ後等でアニメーションが停滞した場合のフォールバック.
        /// </summary>
        public void ForceIdleState()
        {
            if (animator == null) return;
            animator.SetInteger(IsMoveHash, 0);
            animator.Play("Idle", 0, 0f);
        }

        /// <summary>
        /// アクション用アニメーション速度を設定（移動速度による自動調整を一時停止）.
        /// </summary>
        /// <param name="speed">速度倍率（1.0が通常速度）.</param>
        public void SetAnimatorSpeed(float speed)
        {
            actionSpeedOverride = true;
            animator.speed = speed;
        }

        /// <summary>
        /// アクション用アニメーション速度制御を解除し、移動速度による自動調整を再開.
        /// </summary>
        public void ClearActionAnimatorSpeed()
        {
            actionSpeedOverride = false;
            // 即座にanimator.speedを通常速度に復元（次Updateまでの遅延を防止）.
            if (animator != null)
                animator.speed = 1.0f;
        }

        /// <summary>
        /// 死亡通知: 以降の歩き/アイドル/着地アニメーション遷移を停止し、着地判定を即座に解除.
        /// </summary>
        public void NotifyDead()
        {
            isDead = true;
            animator.SetInteger(IsMoveHash, 0);
            animator.SetBool(IsGroundedHash, false);
        }

        // 居合ワーニング用アニメーター（Inspector上で設定）.
        [Header("居合ワーニング")]
        [SerializeField] private Animator iaiWarningAnimator;

        /// <summary>
        /// 居合発動可能ワーニングの開始/停止.
        /// </summary>
        public void SetIaiWarning(bool active)
        {
            if (iaiWarningAnimator == null) return;
            if (active)
            {
                iaiWarningAnimator.gameObject.SetActive(true);
                iaiWarningAnimator.SetTrigger("Start");
            }
            else
            {
                iaiWarningAnimator.SetTrigger("Stop");
            }
        }
    }

    public interface IPlayerAnimation
    {
        void SetMove(float move);
        void SetInputDirection(float inputX);
        void SetGrounded(bool isGrounded);
        void SetGuard(bool isGuarding);
        void PlayAttack();
        void PlayDodge();
        void PlayHurt();
        void PlayDead();
        void PlayJump();
        void PlayJumpOnGround();
        void PlayTrigger(string triggerName);
        void RegisterHitDetectionCallbacks(Action onStart, Action onEnd);
        void ClearHitDetectionCallbacks();
        void SetSuppressMovement(bool suppress);
        UniTask EnsureAttackAnimation(string triggerName);
        void SetAnimatorSpeed(float speed);
        void ClearActionAnimatorSpeed();
        void NotifyDead();
        void SetIaiWarning(bool active);
        void ForceIdleState();
    }
}