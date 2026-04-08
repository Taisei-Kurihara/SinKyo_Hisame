using UnityEngine;
using Cysharp.Threading.Tasks;

// EnemyModel_abstractを継承したWendig用モデル.
public class EnemyModel_Wendig : EnemyModel_abstract
{

    protected new EnemAIModel_Wendig_Normal AIModel = new EnemAIModel_Wendig_Normal();

    // EnemyStatus_Wendigへの参照.
    private EnemyStatus_Wendig wendigStatus;

    protected override void Awake()
    {
        base.Awake();
        wendigStatus = GetComponent<EnemyStatus_Wendig>();
    }

    // 現在の攻撃力を取得.
    public float GetCurrentAttackPower()
    {
        return wendigStatus != null ? wendigStatus.GetCurrentAttackPower() : 50f;
    }

    // --- AI層への転送メソッド ---

    /// <summary>ダメージ時の怒りゲージ増加をAIモデルに転送.</summary>
    public void NotifyDamageForAnger(float amount)
    {
        AIModel.NotifyDamageForAnger(amount);
    }

    /// <summary>ダメージ時のフェーズ遷移チェックをAIモデルに転送.</summary>
    public void CheckPhaseTransition(float remainingHp)
    {
        AIModel.CheckPhaseTransition(remainingHp);
    }

    /// <summary>フェーズ対応HPパーセントを取得.</summary>
    public float GetPhaseAwareHpPercent(float remainingHp)
    {
        return AIModel.CalculateHpBarPercent(remainingHp);
    }

    public override void EnemAIStart()
    {
        AIModel.SetOwnerTransform(transform);
        AIModel.SetOwnerModel(this);
        AIModel.StartLoop();
    }

    // Wendig用AIModelを停止するためにオーバーライド.
    public override void EnemAIStop()
    {
        Debug.Log($"[EnemyModel_Wendig] EnemAIStop - {gameObject.name}");
        AIModel.StopLoop();
    }

    // Dead割り込みStateを実行.
    public async UniTask TriggerDead()
    {
        Debug.Log($"[EnemyModel_Wendig] TriggerDead - {gameObject.name}");
        EnemAIStop();
        await AIModel.InterruptStateList.ExecuteInterrupt(AIModel.InterruptStateList.GetDeadState(), this);
    }

    // Stan割り込みStateを実行.
    public async UniTask TriggerStan()
    {
        Debug.Log($"[EnemyModel_Wendig] TriggerStan - {gameObject.name}");
        await AIModel.InterruptStateList.ExecuteInterrupt(AIModel.InterruptStateList.GetStanState(), this);
    }

    // Bayt割り込みStateを実行.
    public async UniTask TriggerBayt()
    {
        Debug.Log($"[EnemyModel_Wendig] TriggerBayt - {gameObject.name}");
        await AIModel.InterruptStateList.ExecuteInterrupt(AIModel.InterruptStateList.GetBaytState(), this);
    }

    // Howling Stateを実行.
    public async UniTask TriggerHowling()
    {
        Debug.Log($"[EnemyModel_Wendig] TriggerHowling - {gameObject.name}");
        await AIModel.HowlingState.Act(this);
    }

    // TripleAttack Stateを実行.
    public async UniTask TriggerTripleAttack()
    {
        Debug.Log($"[EnemyModel_Wendig] TriggerTripleAttack - {gameObject.name}");
        await AIModel.TripleAttackState.Act(this);
    }

    // バトル開始時の演出（Howling → TripleAttack）.
    public async UniTask TriggerBattleStart()
    {
        Debug.Log($"[EnemyModel_Wendig] TriggerBattleStart - {gameObject.name}");
        await TriggerHowling();
        await TriggerTripleAttack();
    }
}
