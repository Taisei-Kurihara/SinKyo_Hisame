using UnityEngine;
using Cysharp.Threading.Tasks;

// Wendig用 Howling(遠吠え)State.
public class EnemState_Wendig_Howling : EnemState_abstract
{
    // ハウリング: 1.8倍.
    private float attackMultiplier = 1.8f;
    private float attackRadius = 3f;

    public override async UniTask Act(EnemyModel_abstract enemyModel)
    {
        Debug.Log($"[EnemState_Wendig_Howling] Act開始");

        if (!EnemNullSafetyHelper.IsValidWithAnimator(enemyModel)) return;

        // 現在の攻撃力から実ダメージを計算.
        int attackDamage = 90;
        if (enemyModel is EnemyModel_Wendig wendigModel)
        {
            attackDamage = (int)(wendigModel.GetCurrentAttackPower() * attackMultiplier);
        }

        float animSpeed = enemyModel.AnimSpeed;

        // Howlingアニメーション開始.
        enemyModel.Animator.SetTrigger("Howling");

        // === 前段階 ===.
        // 300ms待機 → 攻撃通告(パリィ不可) → 1000ms待機.
        if (!await EnemAttackPhaseHelper.PlayAttackPremonition(
            enemyModel, 1300f, false, 1000f, animSpeed)) return;

        // === 攻撃中 ===.
        // 円形の攻撃判定を生成し400ms維持.
        var colliderState = new EnemColliderState_Wendig_Howling();
        colliderState.SetDamage(attackDamage);
        colliderState.ClearHitTargets();

        await EnemColliderHelper.ExecuteColliderPhase(
            enemyModel,
            new EnemColliderHelper.ColliderPhaseConfig
            {
                colliderType = EnemColliderType.Circle,
                offset = Vector2.zero,
                radius = attackRadius,
                damage = attackDamage,
                duration = 0.5f,
                colliderState = colliderState
            },
            400f, animSpeed);

        // === 攻撃後 ===.
        // Howling_End トリガー実行.
        if (EnemNullSafetyHelper.IsValidWithAnimator(enemyModel))
        {
            enemyModel.Animator.SetTrigger("Howling_End");
        }

        Debug.Log($"[EnemState_Wendig_Howling] Act完了");
    }
}
