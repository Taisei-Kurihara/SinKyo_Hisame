using UnityEngine;
using Cysharp.Threading.Tasks;

// Wendig用 Dead割り込みState.
public class EnemInterruptState_Dead_Wendig : EnemInterruptState_Dead_abstract
{
    public EnemInterruptState_Dead_Wendig()
    {
        deathAnimationDelay = 2f;
    }

    protected override async UniTask OnDeathComplete(EnemyModel_abstract enemyModel)
    {
        Debug.Log($"[EnemInterruptState_Dead_Wendig] OnDeathComplete開始");

        if (enemyModel == null || enemyModel.Presenter == null)
        {
            Debug.LogWarning($"[EnemInterruptState_Dead_Wendig] OnDeathComplete中断 - enemyModel or Presenter が null");
            return;
        }

        // AIループを停止.
        enemyModel.EnemAIStop();
        Debug.Log($"[EnemInterruptState_Dead_Wendig] AIループ停止");

        // GameObjectを破棄（既存の処理）.
        Object.Destroy(enemyModel.gameObject);
        Debug.Log($"[EnemInterruptState_Dead_Wendig] GameObject破棄");

        await UniTask.CompletedTask;
    }
}
