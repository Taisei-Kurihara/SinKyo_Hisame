using UnityEngine;
using Cysharp.Threading.Tasks;
using InGame.Common;

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

        // DeathManagerに死亡を通知（AI停止等はDeathManager経由で実行）.
        await DeathManager.Instance.NotifyEnemyDeath();

        Debug.Log($"[EnemInterruptState_Dead_Wendig] OnDeathComplete完了");
    }
}
