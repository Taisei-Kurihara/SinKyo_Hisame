using UnityEngine;
using Cysharp.Threading.Tasks;

public abstract class EnemState_abstract
{
    protected EnemyState stateType = EnemyState.None;
    public EnemyState StateType => stateType;

    public virtual async UniTask Act(EnemyModel_abstract enemyModel)
    {
        Debug.Log($"[EnemState_abstract] Act - StateType: {stateType}");
        await UniTask.CompletedTask;
    }
}
