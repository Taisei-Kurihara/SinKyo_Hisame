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

        // Deadアニメーショントリガー実行（リトライ付き）.
        enemyModel.Animator.SetTrigger("Dead");
        Debug.Log($"[EnemInterruptState_Dead_abstract] Dead トリガー実行");

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
                Debug.Log($"[EnemInterruptState_Dead_abstract] Dead アニメーション確認済 (frame {i})");
                break;
            }
            enemyModel.Animator.SetTrigger("Dead");
        }
        if (!confirmed)
        {
            Debug.LogWarning($"[EnemInterruptState_Dead_abstract] Dead アニメーション未確認 - 強制続行");
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
