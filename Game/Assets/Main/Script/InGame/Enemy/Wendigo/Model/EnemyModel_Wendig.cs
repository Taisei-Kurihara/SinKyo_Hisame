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

    private void Start()
    {
        // 怒り状態変更コールバック登録.
        if (wendigStatus != null)
        {
            wendigStatus.SetOnAngerStateChanged(OnAngerStateChanged);
            Debug.Log($"[EnemyModel_Wendig] Start - 怒り状態コールバック登録完了");
        }
        else
        {
            Debug.LogWarning($"[EnemyModel_Wendig] Start - wendigStatusがnull! 怒りコールバック登録失敗");
        }
    }

    // 怒り状態変更時のコールバック.
    private void OnAngerStateChanged(EnemyStatus_abstract.AngerState newState)
    {
        Debug.Log($"[EnemyModel_Wendig] 怒り状態変更 - {newState}");
        if (newState == EnemyStatus_abstract.AngerState.Angry)
        {
            // 怒り開始 → AIModelにHowling実行を通知.
            AIModel.NotifyAngerStateChanged(true);
        }
        else
        {
            // 怒り解除 → AIModelに通知.
            AIModel.NotifyAngerStateChanged(false);
        }
    }

    // 現在の攻撃力を取得.
    public float GetCurrentAttackPower()
    {
        return wendigStatus != null ? wendigStatus.GetCurrentAttackPower() : 50f;
    }




    public override void EnemAIStart()
    {
        //Debug.Log($"[EnemyModel_Wendig] EnemAIStart - {gameObject.name}");
        AIModel.SetOwnerTransform(transform);
        AIModel.SetOwnerModel(this);
        AIModel.StartLoop();
        //Debug.Log($"[EnemyModel_Wendig] EnemAIStart完了");
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
