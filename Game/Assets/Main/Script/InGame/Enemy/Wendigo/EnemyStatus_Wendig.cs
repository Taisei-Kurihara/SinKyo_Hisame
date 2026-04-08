using Cysharp.Threading.Tasks;
using UnityEngine;

// EnemyStatus_abstractを継承したWendig用ステータス.
public class EnemyStatus_Wendig : EnemyStatus_abstract
{
    public override string name => "Wendigo";

    // EnemyModel_Wendigへの参照.
    private EnemyModel_Wendig wendigModel;

    // Wendig基礎攻撃力.
    private float baseAttackPower = 50f;
    private float enragedAttackPower = 75f;
    private float enrageHpThreshold = 12500f;

    // 現在の攻撃力を取得.
    public float GetCurrentAttackPower()
    {
        return hp.Value <= enrageHpThreshold ? enragedAttackPower : baseAttackPower;
    }

    public override void Init()
    {
        Debug.Log($"[EnemyStatus_Wendig] Init - {gameObject.name}");
        wendigModel = GetComponent<EnemyModel_Wendig>();
        // Wendig HPを20000に設定（全フェーズ合計）.
        maxhp = 20000f;
        hp.Value = maxhp;
        Debug.Log($"[EnemyStatus_Wendig] Init完了 - HP:{maxhp}");
    }

    private void Start()
    {
        // Initで取得できなかった場合の再取得.
        if (wendigModel == null)
        {
            wendigModel = GetComponent<EnemyModel_Wendig>();
        }
    }

    public override async UniTask OnDamaged(float damage, ChangeHP aschangeHP = null)
    {
        // フェーズ遷移チェック（HP bar subscription より先に）.
        wendigModel?.CheckPhaseTransition(hp.Value - damage);
        // HP減少処理.
        await base.OnDamaged(damage, aschangeHP);
        // 怒りゲージ: HP減少×2をAI層に通知.
        wendigModel?.NotifyDamageForAnger(damage * 2f);
    }

    protected override async UniTask Dead()
    {
        Debug.Log($"[EnemyStatus_Wendig] Dead開始 - DeadState使用 - {gameObject.name}");

        // EnemyModel_WendigのTriggerDeadを使用.
        if (wendigModel != null)
        {
            await wendigModel.TriggerDead();
        }
        else
        {
            // フォールバック：通常の死亡処理.
            Debug.LogWarning($"[EnemyStatus_Wendig] wendigModelがnull - 通常の死亡処理を実行");
            await base.Dead();
        }
    }

    protected override UniTask DeadAnim()
    {
        // DeadStateを使用するため、ここでは何もしない.
        return UniTask.CompletedTask;
    }
}
