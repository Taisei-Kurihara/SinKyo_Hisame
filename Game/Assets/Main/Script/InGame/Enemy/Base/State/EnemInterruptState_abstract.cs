using UnityEngine;
using Cysharp.Threading.Tasks;

// 割り込みState抽象クラス（Dead, Stanなど通常のAIループを中断するState用）.
public abstract class EnemInterruptState_abstract
{
    protected EnemyState stateType = EnemyState.None;
    public EnemyState StateType => stateType;

    // 割り込み優先度（高いほど優先）.
    protected int priority = 0;
    public int Priority => priority;

    // 割り込みStateを実行.
    public virtual async UniTask Act(EnemyModel_abstract enemyModel)
    {
        Debug.Log($"[EnemInterruptState_abstract] Act - StateType: {stateType}");
        await UniTask.CompletedTask;
    }
}
