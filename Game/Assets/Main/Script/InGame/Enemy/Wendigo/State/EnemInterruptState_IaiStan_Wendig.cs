using Cysharp.Threading.Tasks;

// Wendig用 回避居合いスタン（0.5秒の短いスタン）.
public class EnemInterruptState_IaiStan_Wendig : EnemInterruptState_Stan_abstract
{
    public EnemInterruptState_IaiStan_Wendig()
    {
        stanBoolName = "Stan";
        stanDuration = 0.5f;
    }

    protected override async UniTask OnStanProcess(EnemyModel_abstract enemyModel)
    {
        UnityEngine.Debug.Log($"[EnemInterruptState_IaiStan_Wendig] OnStanProcess開始 (0.5sec)");

        // スタンSE再生.
        if (enemyModel?.Presenter != null)
        {
            enemyModel.Presenter.PlaySE("Stan");
        }

        // 短スタン持続時間分待機.
        await UniTask.Delay((int)(stanDuration * 1000));
    }
}
