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
        // Wendig固有の初期化処理.
        wendigModel = GetComponent<EnemyModel_Wendig>();
        // Wendig HPを20000に設定.
        maxhp = 20000f;
        hp.Value = maxhp;

        // Wendig用 怒りゲージパラメータ設定.
        maxAngerGauge = 100f;
        normalDecayRateMax = 5f;
        normalDecayRateMin = 0.1f;
        normalDecayRecoveryDuration = 10f;
        normalClampDecayRate = 2f;
        normalClampIncreaseRatio = 0.5f;
        angryDecayRateMax = 8f;
        angryDecayRateMin = 0.1f;
        angryDecayRecoveryDuration = 10f;
        angryClampDecayRate = 4f;

        Debug.Log($"[EnemyStatus_Wendig] Init完了 - HP:{maxhp} MaxAnger:{maxAngerGauge} 変換率:damage/maxhp*maxAnger*5");
    }

    private void Start()
    {
        //Debug.Log($"[EnemyStatus_Wendig] Start - {gameObject.name}, HP: {hp.Value}, MaxHP: {maxhp}");
        // Initで取得できなかった場合の再取得.
        if (wendigModel == null)
        {
            wendigModel = GetComponent<EnemyModel_Wendig>();
        }
    }

    public override async UniTask OnDamaged(float damage, ChangeHP aschangeHP = null)
    {
        //Debug.Log($"[EnemyStatus_Wendig] OnDamaged - {gameObject.name}, Damage: {damage}");
        await base.OnDamaged(damage, aschangeHP);
        //Debug.Log($"[EnemyStatus_Wendig] OnDamaged完了 - 残りHP: {hp.Value}");
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

    // Wendig用 怒りゲージ増加量計算 (HP20000に対する適切な変換率).
    // ダメージ → ゲージ増加: damage / maxhp * maxAngerGauge * 5 (5倍係数でHP20%分のダメージで怒り到達).
    protected override float CalculateAngerIncrease(float damage)
    {
        float result = (damage / maxhp) * maxAngerGauge * 5f;
        Debug.Log($"[EnemyStatus_Wendig] CalculateAngerIncrease - Damage:{damage:F1} → Increase:{result:F2} (damage/{maxhp}*{maxAngerGauge}*5)");
        return result;
    }

    protected override UniTask DeadAnim()
    {
        //Debug.Log($"[EnemyStatus_Wendig] DeadAnim - {gameObject.name}");
        // DeadStateを使用するため、ここでは何もしない.
        return UniTask.CompletedTask;
    }
}
