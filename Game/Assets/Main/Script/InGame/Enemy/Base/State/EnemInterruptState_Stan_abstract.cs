using UnityEngine;
using Cysharp.Threading.Tasks;

// Stan(スタン)割り込みState抽象クラス.
public abstract class EnemInterruptState_Stan_abstract : EnemInterruptState_abstract
{
    protected string stanBoolName = "Stan";
    protected float stanDuration = 2f;

    public EnemInterruptState_Stan_abstract()
    {
        stateType = EnemyState.Damaged;
        priority = 50; // Stanの優先度.
    }

    public override async UniTask Act(EnemyModel_abstract enemyModel)
    {
        Debug.Log($"[EnemInterruptState_Stan_abstract] Act開始");

        if (enemyModel == null || enemyModel.Animator == null)
        {
            Debug.LogWarning($"[EnemInterruptState_Stan_abstract] Act中断 - enemyModel or Animator が null");
            return;
        }

        // Stan開始：アニメーションSetBool true.
        enemyModel.Animator.SetBool(stanBoolName, true);
        Debug.Log($"[EnemInterruptState_Stan_abstract] {stanBoolName} = true 設定");

        // スタン処理（継承クラスでオーバーライド可能）.
        await OnStanProcess(enemyModel);

        // Stan終了：アニメーションSetBool false.
        if (enemyModel != null && enemyModel.Animator != null)
        {
            enemyModel.Animator.SetBool(stanBoolName, false);
            Debug.Log($"[EnemInterruptState_Stan_abstract] {stanBoolName} = false 設定");
        }

        Debug.Log($"[EnemInterruptState_Stan_abstract] Act完了");
    }

    // スタン中の処理（継承クラスでオーバーライド）.
    protected virtual async UniTask OnStanProcess(EnemyModel_abstract enemyModel)
    {
        // 未実装（協議中）.
        await UniTask.CompletedTask;
    }
}
