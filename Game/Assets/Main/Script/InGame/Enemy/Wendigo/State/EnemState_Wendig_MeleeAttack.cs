using UnityEngine;
using Cysharp.Threading.Tasks;

// Wendig用 近距離攻撃State（コライダーはここで生成）.
public class EnemState_Wendig_MeleeAttack : EnemState_abstract
{
    // 軽い斬撃: 0.45倍.
    private float attackMultiplier = 0.45f;
    private Vector2 attackOffset = new Vector2(0.1f, 0f);
    private Vector2 attackSize = new Vector2(0.35f, 2f);

    // ヒット処理.
    private EnemColliderState_Wendig_MeleeAttack colliderState = new EnemColliderState_Wendig_MeleeAttack();

    // 攻撃前の溜め時間（秒）.
    private float attackPreDelay = 1.0f;

    // ライフサイクル間共有データ.
    private int attackDamage;

    public EnemState_Wendig_MeleeAttack()
    {
        postActionWaitFrames = 60;
    }

    protected override async UniTask OnPreAction(EnemyModel_abstract enemyModel)
    {
        if (!EnemNullSafetyHelper.IsValidWithAnimator(enemyModel)) { isAborted = true; return; }

        float animSpeed = enemyModel.AnimSpeed;

        // 現在の攻撃力から実ダメージを計算.
        attackDamage = 23;
        if (enemyModel is EnemyModel_Wendig wendigModel)
        {
            attackDamage = (int)(wendigModel.GetCurrentAttackPower() * attackMultiplier);
        }

        // ヒット対象リストをクリア.
        colliderState.ClearHitTargets();
        colliderState.SetDamage(attackDamage);

        // 前回のトリガーが残留している場合に備えてリセット.
        enemyModel.Animator.ResetTrigger("Attack_End");
        enemyModel.Animator.ResetTrigger("Attack");

        // === 溜めアニメーション ===.
        enemyModel.Animator.SetTrigger("Attack_Pre");

        // 溜め時間待機（attackPreDelayで調整可能）.
        int preDelayMs = (int)(attackPreDelay * 1000f / animSpeed);
        await UniTask.Delay(preDelayMs);
        if (!EnemNullSafetyHelper.IsValidWithAnimator(enemyModel)) { isAborted = true; return; }

        // === 攻撃アニメーション開始 ===.
        enemyModel.Animator.SetTrigger("Attack");

        // === 前段階 ===.
        // 200ms待機 → 攻撃通告(パリィ可能) → 300ms待機.
        if (!await EnemAttackPhaseHelper.PlayAttackPremonition(
            enemyModel, 500f, true, 300f, animSpeed)) { isAborted = true; return; }
    }

    protected override async UniTask OnAction(EnemyModel_abstract enemyModel)
    {
        if (!EnemNullSafetyHelper.IsValidWithAnimator(enemyModel)) { isAborted = true; return; }

        Animator animator = enemyModel.Animator;

        // === 攻撃中 ===.
        // Attackアニメーション終了まで当たり判定を維持.
        Vector2 colliderOffset = new Vector2(-Mathf.Abs(attackOffset.x), attackOffset.y);

        await EnemColliderHelper.ExecuteColliderPhaseUntil(
            enemyModel,
            new EnemColliderHelper.ColliderPhaseConfig
            {
                colliderType = EnemColliderType.Box,
                offset = colliderOffset,
                size = attackSize,
                damage = attackDamage,
                duration = 0.5f,
                colliderState = colliderState
            },
            () =>
            {
                if (!EnemNullSafetyHelper.IsValidWithAnimator(enemyModel)) return true;
                var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                return !stateInfo.IsName("Attack") || stateInfo.normalizedTime >= 1f;
            });
    }

    protected override async UniTask OnAfterPostAction(EnemyModel_abstract enemyModel)
    {
        if (EnemNullSafetyHelper.IsValidWithAnimator(enemyModel))
        {
            enemyModel.Animator.ResetTrigger("Attack");
            enemyModel.Animator.SetTrigger("Attack_End");
        }
        await UniTask.CompletedTask;
    }
}
