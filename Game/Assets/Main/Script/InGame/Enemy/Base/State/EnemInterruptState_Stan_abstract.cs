using UnityEngine;
using Cysharp.Threading.Tasks;

// Stan(スタン)割り込みState抽象クラス.
public abstract class EnemInterruptState_Stan_abstract : EnemInterruptState_abstract
{
    protected string stanBoolName = "Stan";
    protected float stanDuration = 2f;

    /// <summary>
    /// スタン開始時に InGamePresenter へ通知する戦闘状態.
    /// 継承クラスのコンストラクタで上書きする
    /// （長時間スタン系は StunLong、通常は StunShort）.
    /// </summary>
    protected EnemyBattleState battleStateOnStan = EnemyBattleState.StunShort;

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

        // スタン開始：アタッチ中の当たり判定をすべてキャンセル.
        if (enemyModel.Presenter != null)
        {
            var hitDetectors = enemyModel.Presenter.GetComponents<EnemyAttackHitDetector>();
            foreach (var detector in hitDetectors)
            {
                if (detector != null) UnityEngine.Object.Destroy(detector);
            }
            Debug.Log($"[EnemInterruptState_Stan_abstract] 当たり判定キャンセル - 検出器数: {hitDetectors.Length}");
        }

        // Stan開始：アニメーションSetBool true.
        enemyModel.Animator.SetBool(stanBoolName, true);
        Debug.Log($"[EnemInterruptState_Stan_abstract] {stanBoolName} = true 設定");

        // InGamePresenter へスタン状態を通知（StunShort or StunLong）.
        enemyModel.Presenter?.SetBattleState(battleStateOnStan);

        // スタン処理（継承クラスでオーバーライド可能）.
        await OnStanProcess(enemyModel);

        // Stan終了：アニメーションSetBool false.
        if (enemyModel != null && enemyModel.Animator != null)
        {
            enemyModel.Animator.SetBool(stanBoolName, false);
            Debug.Log($"[EnemInterruptState_Stan_abstract] {stanBoolName} = false 設定");
        }

        // スタン終了を通知.
        enemyModel?.Presenter?.SetBattleState(EnemyBattleState.Idle);

        Debug.Log($"[EnemInterruptState_Stan_abstract] Act完了");
    }

    // スタン中の処理（継承クラスでオーバーライド）.
    protected virtual async UniTask OnStanProcess(EnemyModel_abstract enemyModel)
    {
        // 未実装（協議中）.
        await UniTask.CompletedTask;
    }
}
