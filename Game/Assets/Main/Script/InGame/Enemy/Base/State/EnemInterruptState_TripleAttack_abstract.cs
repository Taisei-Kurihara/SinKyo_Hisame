using UnityEngine;
using Cysharp.Threading.Tasks;

// TripleAttack(三連撃)割り込みState抽象クラス.
public abstract class EnemInterruptState_TripleAttack_abstract : EnemInterruptState_abstract
{
    public EnemInterruptState_TripleAttack_abstract()
    {
        stateType = EnemyState.Attack;
        priority = 40; // TripleAttackの優先度.
    }

    public override async UniTask Act(EnemyModel_abstract enemyModel)
    {
        Debug.Log($"[EnemInterruptState_TripleAttack_abstract] Act開始");

        if (enemyModel == null || enemyModel.Animator == null)
        {
            Debug.LogWarning($"[EnemInterruptState_TripleAttack_abstract] Act中断 - enemyModel or Animator が null");
            return;
        }

        // TripleAttack処理（継承クラスでオーバーライド）.
        await OnTripleAttackProcess(enemyModel);

        Debug.Log($"[EnemInterruptState_TripleAttack_abstract] Act完了");
    }

    // TripleAttack処理（継承クラスでオーバーライド）.
    protected virtual async UniTask OnTripleAttackProcess(EnemyModel_abstract enemyModel)
    {
        await UniTask.CompletedTask;
    }
}
