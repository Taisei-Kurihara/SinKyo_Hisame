using Cysharp.Threading.Tasks;
using InGame.Enemy;
using UnityEngine;

// EnemyStatus_abstractを継承したWendig用ステータス.
public class EnemyStatus_Wendig : EnemyStatus_abstract
{
    public override string name => "Wendigo";

    // EnemyModel_Wendigへの参照.
    private EnemyModel_Wendig wendigModel;

    // Wendig基礎攻撃力.
    private const float defaultBaseAttackPower = 50f;
    private const float defaultEnragedAttackPower = 75f;
    private const float defaultEnrageHpThreshold = 12500f;

    private float baseAttackPower = defaultBaseAttackPower;
    private float enragedAttackPower = defaultEnragedAttackPower;
    private float enrageHpThreshold = defaultEnrageHpThreshold;

    // 現在の攻撃力を取得.
    public float GetCurrentAttackPower()
    {
        return hp.Value <= enrageHpThreshold ? enragedAttackPower : baseAttackPower;
    }

    public override void Init()
    {
        Debug.Log($"[EnemyStatus_Wendig] Init - {gameObject.name}");
        wendigModel = GetComponent<EnemyModel_Wendig>();

        // 難易度に応じたHP倍率（EnemyManagerからMissionTags取得）.
        var missionTags = EnemyManager.Instance(false).CurrentMissionTags;
        float hpMultiplier = 1f;
        if ((missionTags & MissionTag.Difficulty_Easy) != 0)
        {
            hpMultiplier = 0.6f;
        }
        else if ((missionTags & MissionTag.Difficulty_Hard) != 0)
        {
            hpMultiplier = 1.5f;
        }

        // 全ステータスを明示的にリセット.
        maxhp = 20000f * hpMultiplier;
        hp.Value = maxhp;
        baseAttackPower = defaultBaseAttackPower;
        enragedAttackPower = defaultEnragedAttackPower;
        enrageHpThreshold = defaultEnrageHpThreshold * hpMultiplier;
        Debug.Log($"[EnemyStatus_Wendig] Init完了 - HP:{maxhp} EnrageThreshold:{enrageHpThreshold} (倍率:{hpMultiplier})");
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
