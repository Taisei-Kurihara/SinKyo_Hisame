using UnityEngine;
using Cysharp.Threading.Tasks;

// Wendig用 Bayt割り込みState.
public class EnemInterruptState_Bayt_Wendig : EnemInterruptState_abstract
{
    public EnemInterruptState_Bayt_Wendig()
    {
        stateType = EnemyState.Attack;
        priority = 20;
    }

    // 噛みつく: 1.5倍.
    private float attackMultiplier = 1.5f;
    private Vector2 attackOffset = new Vector2(0.1f, 0f);
    private Vector2 attackSize = new Vector2(0.35f, 2f);

    public override async UniTask Act(EnemyModel_abstract enemyModel)
    {
        Debug.Log($"[EnemInterruptState_Bayt_Wendig] Act開始");

        if (!EnemNullSafetyHelper.IsValidWithAnimator(enemyModel)) return;

        // 現在の攻撃力から実ダメージを計算.
        int attackDamage = 75;
        if (enemyModel is EnemyModel_Wendig wendigModel)
        {
            attackDamage = (int)(wendigModel.GetCurrentAttackPower() * attackMultiplier);
        }

        Animator animator = enemyModel.Animator;
        float animSpeed = enemyModel.AnimSpeed;

        // Baytアニメーション開始.
        animator.SetTrigger("Bayt");

        // === 前段階 ===.
        if (!await EnemAttackPhaseHelper.PlayAttackPremonition(
            enemyModel, 1500f, true, 300f, animSpeed)) return;

        // === 攻撃中 ===.
        // 攻撃判定を400ms維持.
        var colliderState = new EnemColliderState_Wendig_Bayt();
        colliderState.SetDamage(attackDamage);
        colliderState.ClearHitTargets();

        Vector2 colliderOffset = new Vector2(-Mathf.Abs(attackOffset.x), attackOffset.y);

        await EnemColliderHelper.ExecuteColliderPhase(
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
            400f, animSpeed);

        if (!EnemNullSafetyHelper.IsValid(enemyModel)) return;

        // Baytアニメーション完了を待機.
        float baytAnimWait = 0f;
        float baytAnimWaitMax = 1.5f;
        bool foundBaytState = false;
        while (baytAnimWait < baytAnimWaitMax)
        {
            if (!EnemNullSafetyHelper.IsValidWithAnimator(enemyModel)) break;
            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsName("Bayt"))
            {
                foundBaytState = true;
                if (stateInfo.normalizedTime >= 1f) break;
            }
            else if (foundBaytState)
            {
                // Baytステートから抜けた → 完了.
                break;
            }
            baytAnimWait += Time.deltaTime;
            await UniTask.Yield();
        }

        // === 攻撃後 ===.
        // フレーム待機 → Bayt_End トリガー.
        if (!await EnemAttackPhaseHelper.WaitPostAttackFrames(enemyModel, 40, animSpeed)) return;

        if (EnemNullSafetyHelper.IsValidWithAnimator(enemyModel))
        {
            animator.SetTrigger("Bayt_End");
        }

        Debug.Log($"[EnemInterruptState_Bayt_Wendig] Act完了");
    }
}
