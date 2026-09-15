using UnityEngine;
using Cysharp.Threading.Tasks;

// Dead割り込みState抽象クラス.
public abstract class EnemInterruptState_Dead_abstract : EnemInterruptState_abstract
{
    protected float deathAnimationDelay = 2f;

    public EnemInterruptState_Dead_abstract()
    {
        stateType = EnemyState.Dead;
        priority = 100; // Deadは最高優先度.
    }

    public override async UniTask Act(EnemyModel_abstract enemyModel)
    {
        Debug.Log($"[EnemInterruptState_Dead_abstract] Act開始");

        if (enemyModel == null || enemyModel.Animator == null)
        {
            Debug.LogWarning($"[EnemInterruptState_Dead_abstract] Act中断 - enemyModel or Animator が null");
            return;
        }

        // 競合する可能性のあるトリガーをリセット.
        var animator = enemyModel.Animator;
        animator.ResetTrigger("Attack");
        animator.ResetTrigger("Stun");
        animator.ResetTrigger("Hurt");

        // Deadアニメーショントリガー実行（リトライ付き）.
        animator.SetTrigger("Dead");

        // 数フレームにわたって死亡アニメーション再生を確認し、未再生なら再発火.
        int maxRetry = 10;
        bool confirmed = false;
        for (int i = 0; i < maxRetry; i++)
        {
            await UniTask.Yield();
            if (enemyModel == null || enemyModel.Animator == null) break;
            var info = enemyModel.Animator.GetCurrentAnimatorStateInfo(0);
            if (info.IsName("Dead"))
            {
                confirmed = true;
                break;
            }
            animator.SetTrigger("Dead");
        }

        // リトライでも遷移しなかった場合、Deadステートを強制再生.
        if (!confirmed && enemyModel != null && enemyModel.Animator != null)
        {
            animator.Play("Dead", 0, 0f);
        }

        // 2秒待機.
        await UniTask.Delay((int)(deathAnimationDelay * 1000));

        // 継承クラスで実装する死亡後処理.
        await OnDeathComplete(enemyModel);

        Debug.Log($"[EnemInterruptState_Dead_abstract] Act完了");
    }

    // 死亡後の処理（継承クラスでオーバーライド）.
    protected virtual async UniTask OnDeathComplete(EnemyModel_abstract enemyModel)
    {
        await UniTask.CompletedTask;
    }
}
