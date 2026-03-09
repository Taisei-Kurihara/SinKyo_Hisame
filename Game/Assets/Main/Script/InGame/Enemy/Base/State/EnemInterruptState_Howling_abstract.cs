using UnityEngine;
using Cysharp.Threading.Tasks;

// Howling(遠吠え)割り込みState抽象クラス.
public abstract class EnemInterruptState_Howling_abstract : EnemInterruptState_abstract
{
    public EnemInterruptState_Howling_abstract()
    {
        stateType = EnemyState.Attack;
        priority = 30; // Howlingの優先度.
    }

    public override async UniTask Act(EnemyModel_abstract enemyModel)
    {
        Debug.Log($"[EnemInterruptState_Howling_abstract] Act開始");

        if (enemyModel == null || enemyModel.Animator == null)
        {
            Debug.LogWarning($"[EnemInterruptState_Howling_abstract] Act中断 - enemyModel or Animator が null");
            return;
        }

        // Howling開始：アニメーショントリガー実行.
        enemyModel.Animator.SetTrigger("Howling");
        Debug.Log($"[EnemInterruptState_Howling_abstract] Howling トリガー実行");

        // Howling処理（継承クラスでオーバーライド可能）.
        await OnHowlingProcess(enemyModel);

        // Howling終了：アニメーショントリガー実行.
        if (enemyModel != null && enemyModel.Animator != null)
        {
            enemyModel.Animator.SetTrigger("Howling_End");
            Debug.Log($"[EnemInterruptState_Howling_abstract] Howling_End トリガー実行");
        }

        Debug.Log($"[EnemInterruptState_Howling_abstract] Act完了");
    }

    // Howling中の処理（継承クラスでオーバーライド）.
    protected virtual async UniTask OnHowlingProcess(EnemyModel_abstract enemyModel)
    {
        await UniTask.CompletedTask;
    }
}
