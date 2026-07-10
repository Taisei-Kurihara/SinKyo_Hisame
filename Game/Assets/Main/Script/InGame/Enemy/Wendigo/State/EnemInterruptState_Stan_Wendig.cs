using Cysharp.Threading.Tasks;

// Wendig用 Stan(スタン)割り込みState（怒り行動中の居合スタン: 5秒）.
public class EnemInterruptState_Stan_Wendig : EnemInterruptState_Stan_abstract
{
    public EnemInterruptState_Stan_Wendig()
    {
        stanBoolName = "Stan";
        stanDuration = 5f;
    }

    protected override async UniTask OnStanProcess(EnemyModel_abstract enemyModel)
    {
        UnityEngine.Debug.Log($"[EnemInterruptState_Stan_Wendig] OnStanProcess開始 (5sec)");

        // スタンSE再生.
        if (enemyModel?.Presenter != null)
        {
            enemyModel.Presenter.PlaySE("Stan");
        }

        // スタン持続時間分待機.
        await UniTask.Delay((int)(stanDuration * 1000));
    }
}
