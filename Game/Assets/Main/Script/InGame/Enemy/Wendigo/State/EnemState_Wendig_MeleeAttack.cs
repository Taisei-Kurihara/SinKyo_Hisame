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

    public override async UniTask Act(EnemyModel_abstract enemyModel)
    {
        if (!EnemNullSafetyHelper.IsValidWithAnimator(enemyModel)) return;

        float animSpeed = enemyModel.AnimSpeed;
        Animator animator = enemyModel.Animator;

        // 現在の攻撃力から実ダメージを計算.
        int attackDamage = 23;
        if (enemyModel is EnemyModel_Wendig wendigModel)
        {
            attackDamage = (int)(wendigModel.GetCurrentAttackPower() * attackMultiplier);
        }

        // ヒット対象リストをクリア.
        colliderState.ClearHitTargets();
        colliderState.SetDamage(attackDamage);

        animator.SetTrigger("Attack");

        // === 前段階 ===.
        // 200ms待機 → 攻撃通告(パリィ可能) → 300ms待機.
        if (!await EnemAttackPhaseHelper.PlayAttackPremonition(
            enemyModel, 500f, true, 300f, animSpeed)) return;

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

        // === 攻撃後 ===.
        // フレーム待機 → Attack_End トリガー.
        if (!await EnemAttackPhaseHelper.WaitPostAttackFrames(enemyModel, 40, animSpeed)) return;

        if (EnemNullSafetyHelper.IsValidWithAnimator(enemyModel))
        {
            animator.SetTrigger("Attack_End");
        }
    }
}
