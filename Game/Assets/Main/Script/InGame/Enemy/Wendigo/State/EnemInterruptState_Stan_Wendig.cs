using Cysharp.Threading.Tasks;

// Wendig用 Stan(スタン)割り込みState.
public class EnemInterruptState_Stan_Wendig : EnemInterruptState_Stan_abstract
{
    public EnemInterruptState_Stan_Wendig()
    {
        stanBoolName = "Stan";
        stanDuration = 2f;
    }

    protected override async UniTask OnStanProcess(EnemyModel_abstract enemyModel)
    {
        UnityEngine.Debug.Log($"[EnemInterruptState_Stan_Wendig] OnStanProcess開始");

        // スタンSE再生.
        if (enemyModel?.Presenter != null)
        {
            enemyModel.Presenter.PlaySE("Stan");
        }

        // スタン持続時間分待機.
        await UniTask.Delay((int)(stanDuration * 1000));
    }
}
