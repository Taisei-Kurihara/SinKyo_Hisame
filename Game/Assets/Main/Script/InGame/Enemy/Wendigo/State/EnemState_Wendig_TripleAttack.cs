using UnityEngine;
using Cysharp.Threading.Tasks;

// Wendig用 TripleAttack(三連撃)State.
public class EnemState_Wendig_TripleAttack : EnemState_abstract
{
    // 三連撃: 0.75/1.1/1.35倍.
    private float[] attackMultipliers = { 0.75f, 1.1f, 1.35f };
    private Vector2 attackOffset = new Vector2(0.1f, 0f);
    private Vector2 attackSize = new Vector2(0.35f, 2f);

    // 三連撃のトリガー名.
    private string[] attackTriggers = { "TripleAttack_0", "TripleAttack_1", "TripleAttack_2" };

    public override async UniTask Act(EnemyModel_abstract enemyModel)
    {
        Debug.Log($"[EnemState_Wendig_TripleAttack] Act開始");

        if (!EnemNullSafetyHelper.IsValidWithAnimator(enemyModel)) return;

        // 基礎攻撃力を取得.
        float baseAttackPower = 50f;
        if (enemyModel is EnemyModel_Wendig wendigModel)
        {
            baseAttackPower = wendigModel.GetCurrentAttackPower();
        }

        Animator animator = enemyModel.Animator;
        float animSpeed = enemyModel.AnimSpeed;

        // TripleAttack開始トリガー実行.
        animator.SetTrigger("TripleAttack");

        // === 前段階 ===.
        // 200ms待機 → 攻撃通告(パリィ可能) → 300ms待機.
        if (!await EnemAttackPhaseHelper.PlayAttackPremonition(
            enemyModel, 500f, true, 300f, animSpeed)) return;

        // === 攻撃中 ===.
        // 三連撃を実行.
        Vector2 colliderOffset = new Vector2(-Mathf.Abs(attackOffset.x), attackOffset.y);

        for (int i = 0; i < 3; i++)
        {
            if (!EnemNullSafetyHelper.IsValidWithAnimator(enemyModel)) break;

            // 各段のダメージを計算.
            int attackDamage = (int)(baseAttackPower * attackMultipliers[i]);

            // 攻撃トリガー実行.
            animator.SetTrigger(attackTriggers[i]);

            if (!await EnemAttackPhaseHelper.DelayWithAnimSpeed(enemyModel, 100f, animSpeed)) break;

            // 攻撃判定を100ms維持.
            var colliderState = new EnemColliderState_PlayerDamage();
            colliderState.SetDamage(attackDamage);
            colliderState.ClearHitTargets();

            if (!await EnemColliderHelper.ExecuteColliderPhase(
                enemyModel,
                new EnemColliderHelper.ColliderPhaseConfig
                {
                    colliderType = EnemColliderType.Box,
                    offset = colliderOffset,
                    size = attackSize,
                    damage = attackDamage,
                    duration = 0.3f,
                    colliderState = colliderState
                },
                100f, animSpeed)) break;

            // 攻撃間の待機（最終段以外）.
            if (i < 2)
            {
                if (!await EnemAttackPhaseHelper.DelayWithAnimSpeed(enemyModel, 100f, animSpeed)) return;
            }
        }

        // === 攻撃後 ===.
        // 100ms待機 → TripleAttack_End トリガー.
        if (!await EnemAttackPhaseHelper.DelayWithAnimSpeed(enemyModel, 100f, animSpeed)) return;

        if (EnemNullSafetyHelper.IsValidWithAnimator(enemyModel))
        {
            animator.SetTrigger("TripleAttack_End");
        }

        Debug.Log($"[EnemState_Wendig_TripleAttack] Act完了");
    }
}
