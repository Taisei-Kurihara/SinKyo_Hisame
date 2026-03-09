using Cysharp.Threading.Tasks;
using UnityEngine;

// EnemyPresenter_abstractを継承したWendig用プレゼンター.
public class EnemyPresenter_Wendig : EnemyPresenter_abstract
{
    // 敵名.
    protected override string EnemyName => "Wendigo";

    // Wendig用の型でアクセスするためのプロパティ.
    public EnemyModel_Wendig WendigModel => model as EnemyModel_Wendig;
    public EnemyStatus_Wendig WendigStatus => status as EnemyStatus_Wendig;

    // model/statusのAddComponentを行う.
    protected override void InitComponents()
    {
        //Debug.Log($"[EnemyPresenter_Wendig] InitComponents開始 - {gameObject.name}");
        model = gameObject.AddComponent<EnemyModel_Wendig>();
        //Debug.Log($"[EnemyPresenter_Wendig] EnemyModel_Wendig追加完了");
        status = gameObject.AddComponent<EnemyStatus_Wendig>();
        //Debug.Log($"[EnemyPresenter_Wendig] EnemyStatus_Wendig追加完了");
        //Debug.Log($"[EnemyPresenter_Wendig] InitComponents完了");
    }

    // Wendigo用SE初期化.
    protected override async UniTask InitializeSE()
    {
        await base.InitializeSE();

        // Wendigo用SEアクション登録.
        seRegistry.Register("Stan", "SE_Stan");
        seRegistry.Register("AttackPre", "SE_AttackPre");

        // AudioClip読み込み（SE未登録の場合は警告が出るが処理は継続）.
        await sePlayer.LoadClipsAsync("SE_Stan", "SE_AttackPre");
    }
}
