using Cysharp.Threading.Tasks;

// Wendig用 回避パリィスタン（1秒の短スタン、行動は再開される）.
public class EnemInterruptState_ParryStan_Wendig : EnemInterruptState_Stan_abstract
{
    public EnemInterruptState_ParryStan_Wendig()
    {
        stanBoolName = "Stan";
        stanDuration = 1f;
    }

    protected override async UniTask OnStanProcess(EnemyModel_abstract enemyModel)
    {
        UnityEngine.Debug.Log($"[EnemInterruptState_ParryStan_Wendig] OnStanProcess開始 (1sec)");

        // スタンSE再生.
        if (enemyModel?.Presenter != null)
        {
            enemyModel.Presenter.PlaySE("Stan");
        }

        // パリィスタン持続時間分待機.
        await UniTask.Delay((int)(stanDuration * 1000));
    }
}
