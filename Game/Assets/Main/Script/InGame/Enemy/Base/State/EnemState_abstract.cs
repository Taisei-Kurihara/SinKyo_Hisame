using UnityEngine;
using Cysharp.Threading.Tasks;

public abstract class EnemState_abstract
{
    protected EnemyState stateType = EnemyState.None;
    public EnemyState StateType => stateType;

    // 行動後の待機フレーム数（子クラスがコンストラクタ等で設定）.
    protected int postActionWaitFrames = 0;

    // 中断フラグ: 子がtrueにすると OnAction〜OnPostAction をスキップ.
    // OnAfterPostAction は常に実行される（クリーンアップ保証）.
    protected bool isAborted = false;

    // Template Method（子クラスでoverrideしない）.
    public async UniTask Act(EnemyModel_abstract enemyModel)
    {
        isAborted = false;

        // 1. 行動前.
        await OnPreAction(enemyModel);

        // 2. 行動中.
        if (!isAborted)
            await OnAction(enemyModel);

        // 3. 行動後待機前の処理.
        if (!isAborted)
            await OnPrePostAction(enemyModel);

        // 4. 行動後待機（子override不可）.
        if (!isAborted)
            await OnPostAction(enemyModel);

        // 5. 行動後待機後の処理（常に実行 — クリーンアップ保証）.
        await OnAfterPostAction(enemyModel);

        // Idol状態に復帰.
        if (EnemNullSafetyHelper.IsValidWithAnimator(enemyModel))
        {
            enemyModel.Animator.SetInteger("Move", 0);
        }
    }

    // --- ライフサイクルメソッド（子クラスでoverride可） ---

    protected virtual async UniTask OnPreAction(EnemyModel_abstract enemyModel)
    {
        await UniTask.CompletedTask;
    }

    protected virtual async UniTask OnAction(EnemyModel_abstract enemyModel)
    {
        await UniTask.CompletedTask;
    }

    protected virtual async UniTask OnPrePostAction(EnemyModel_abstract enemyModel)
    {
        await UniTask.CompletedTask;
    }

    // 行動後待機（子override不可）.
    private async UniTask OnPostAction(EnemyModel_abstract enemyModel)
    {
        if (postActionWaitFrames <= 0) return;
        if (!EnemNullSafetyHelper.IsValid(enemyModel)) return;

        float animSpeed = enemyModel.AnimSpeed;
        await EnemAttackPhaseHelper.WaitPostAttackFrames(enemyModel, postActionWaitFrames, animSpeed);
    }

    protected virtual async UniTask OnAfterPostAction(EnemyModel_abstract enemyModel)
    {
        await UniTask.CompletedTask;
    }
}
