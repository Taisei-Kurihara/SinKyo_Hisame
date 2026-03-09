using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using InGame.Player;




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



public class EnemAIModel_Wendig_Normal : EnemAIModel_abstract
{
    // ランダム移動のカウント.
    private int randomMoveCount = 0;
    private int maxRandomMoveBeforeAction = 3;

    // State一覧.
    private EnemState_Wendig_Move moveState = new EnemState_Wendig_Move();
    private EnemState_Wendig_MeleeApproach meleeApproachState = new EnemState_Wendig_MeleeApproach();
    private EnemState_Wendig_MeleeAttack meleeAttackState = new EnemState_Wendig_MeleeAttack();
    private EnemState_Wendig_Rush rushState = new EnemState_Wendig_Rush();
    private EnemState_Wendig_Howling howlingState = new EnemState_Wendig_Howling();
    private EnemState_Wendig_TripleAttack tripleAttackState = new EnemState_Wendig_TripleAttack();

    // State取得用プロパティ.
    public EnemState_Wendig_Howling HowlingState => howlingState;
    public EnemState_Wendig_TripleAttack TripleAttackState => tripleAttackState;


    // 割り込みStateリスト.
    private EnemInterruptStateList_Wendig interruptStateList = new EnemInterruptStateList_Wendig();
    public EnemInterruptStateList_Wendig InterruptStateList => interruptStateList;

    // 現在実行中のアクション設定.
    private EnemAIActionSetting currentActionSetting = null;

    // 初期化済みフラグ.
    private bool isInitialized = false;

    // バトル開始済みフラグ.
    private bool hasBattleStarted = false;

    // カメラ範囲設定（プレイヤーを中心とした範囲）.
    private float cameraViewRangeX = 8f;
    private float cameraViewRangeY = 5f;

    // 近接攻撃タイプの重み設定.
    private float meleeAttackWeight = 1f;        // 通常攻撃の重み.
    private float baytWeight = 1f;               // Bayt攻撃の重み.
    private float howlingWeight = 1f;            // Howling攻撃の重み.
    private float tripleAttackWeight = 1f;       // TripleAttack攻撃の重み.

    // 突進制限用カウンター.
    private int meleeAttacksSinceLastRush = 0;
    private int minMeleeAttacksBeforeRush = 3;   // 突進前に必要な近接攻撃回数.

    // 近距離攻撃前のランダム移動必須回数.
    private int minRandomMovesBeforeMelee = 1;

    // 怒り状態関連.
    private bool isAngry = false;
    private bool pendingAngerHowling = false;
    private int lastMeleeAttackType = -1;  // 連続攻撃防止用.

    // 怒り状態変更通知.
    public void NotifyAngerStateChanged(bool angry)
    {
        isAngry = angry;
        if (angry)
        {
            pendingAngerHowling = true;
            Debug.Log($"[AngerAI] NotifyAngerStateChanged → Angry! Howling予約 pendingAngerHowling:true");
        }
        else
        {
            // 怒り解除時: アニメーション速度を通常に戻す.
            if (ownerModel != null && ownerModel.Animator != null)
            {
                ownerModel.Animator.speed = 1.0f;
                Debug.Log($"[AngerAI] NotifyAngerStateChanged → Normal! Animator.speed → 1.0f");
            }
            Debug.Log($"[AngerAI] 怒り状態解除通知 - isAngry:false");
        }
    }

    // アクション設定を初期化.
    private void InitializeActionSettings()
    {
        if (isInitialized) return;

        // 近接攻撃設定.
        AddActionSetting(new EnemAIActionSetting
        {
            actionState = meleeAttackState,
            repeatableCount = -1,
            activationDistance = 2f,
            moveStartDistance = 7f,
            activationWeight = 2f,
            moveState = meleeApproachState,
            shouldActivate = true
        });

        // 突進攻撃設定.
        AddActionSetting(new EnemAIActionSetting
        {
            actionState = rushState,
            repeatableCount = -1,
            activationDistance = 8f,
            moveStartDistance = 10f,
            activationWeight = 1f,
            moveState = moveState,
            shouldActivate = true
        });

        isInitialized = true;
    }

    // Wendig固有のAI処理を実装.
    protected override async UniTask OnAIUpdate(CancellationToken token)
    {
        // 初期化.
        InitializeActionSettings();

        // バトル開始処理（初回のみ）.
        if (!hasBattleStarted && ownerModel is EnemyModel_Wendig wendigModel)
        {
            hasBattleStarted = true;
            Debug.Log($"[EnemAIModel_Wendig_Normal] バトル開始 - Howling → TripleAttack");
            await wendigModel.TriggerBattleStart();
        }

        // 怒り状態Howling実行（怒り開始時に1回だけ）.
        if (pendingAngerHowling && ownerModel is EnemyModel_Wendig wendigModelAnger)
        {
            pendingAngerHowling = false;
            Debug.Log($"[AngerAI] ★怒りHowling実行開始★");
            await wendigModelAnger.TriggerHowling();
            // 怒り中: アニメーション速度1.2倍.
            if (ownerModel.Animator != null)
            {
                ownerModel.Animator.speed = 1.2f;
                Debug.Log($"[AngerAI] Animator.speed → 1.2f (怒り中)");
            }
        }

        // 怒り中: ランダム移動なし、即攻撃.
        int effectiveMinRandomMovesBeforeMelee = isAngry ? 0 : minRandomMovesBeforeMelee;
        int effectiveMaxRandomMoveBeforeAction = isAngry ? 0 : maxRandomMoveBeforeAction;
        Debug.Log($"[AngerAI] OnAIUpdate - isAngry:{isAngry} effectiveMinMelee:{effectiveMinRandomMovesBeforeMelee} effectiveMaxMove:{effectiveMaxRandomMoveBeforeAction} randomMoveCount:{randomMoveCount}");

        Vector3 targetPos = TargetPosition;
        float distance = ownerTransform != null ? Vector3.Distance(ownerTransform.position, targetPos) : float.MaxValue;

        // カメラ範囲外にいる場合は優先的にカメラ内へ移動.
        if (IsOutsideCameraView(targetPos))
        {
            Debug.Log($"[EnemAIModel_Wendig_Normal] カメラ範囲外 - カメラ内へ移動");
            await MoveToCameraView(targetPos);
        }

        // アクション選択と実行.
        EnemAIActionSetting selectedSetting = SelectActionByDistanceWithRestrictions(distance);

        if (selectedSetting != null)
        {
            currentActionSetting = selectedSetting;

            // 突進攻撃の場合.
            if (selectedSetting.actionState is EnemState_Wendig_Rush rush)
            {
                rush.SetStageEdge(ownerModel.StageMin.x, ownerModel.StageMax.x);
                await selectedSetting.actionState.Act(ownerModel);
                selectedSetting.ConsumeRepeat();
                meleeAttacksSinceLastRush = 0; // 突進後、近接カウンターをリセット.
                randomMoveCount = 0;
                Debug.Log($"[EnemAIModel_Wendig_Normal] 突進完了 - 近接カウンターリセット");
                currentActionSetting = null;
                return;
            }

            // 近接攻撃の場合、抽選でattack/bayt/howling/tripleAttackを選択.
            if (selectedSetting.actionState is EnemState_Wendig_MeleeAttack && ownerModel is EnemyModel_Wendig wendigModelMelee)
            {
                int selectedMeleeType = SelectMeleeAttackType();
                switch (selectedMeleeType)
                {
                    case 1: // bayt.
                        Debug.Log($"[EnemAIModel_Wendig_Normal] 抽選結果: bayt");
                        await wendigModelMelee.TriggerBayt();
                        break;
                    case 2: // howling.
                        Debug.Log($"[EnemAIModel_Wendig_Normal] 抽選結果: howling");
                        await wendigModelMelee.TriggerHowling();
                        break;
                    case 3: // tripleAttack.
                        Debug.Log($"[EnemAIModel_Wendig_Normal] 抽選結果: tripleAttack");
                        await wendigModelMelee.TriggerTripleAttack();
                        break;
                    default: // 0 = attack (通常攻撃).
                        Debug.Log($"[EnemAIModel_Wendig_Normal] 抽選結果: attack (通常攻撃)");
                        await selectedSetting.actionState.Act(ownerModel);
                        break;
                }
                selectedSetting.ConsumeRepeat();
                meleeAttacksSinceLastRush++; // 近接カウンター増加.
                randomMoveCount = 0;
                Debug.Log($"[EnemAIModel_Wendig_Normal] 近接攻撃カウント: {meleeAttacksSinceLastRush}");
                currentActionSetting = null;
                return;
            }

            // その他のアクション.
            await selectedSetting.actionState.Act(ownerModel);
            selectedSetting.ConsumeRepeat();
            randomMoveCount = 0;
        }
        else
        {
            // 移動開始距離内のアクションを探す（制限付き）.
            EnemAIActionSetting moveActionSetting = SelectMoveActionByDistanceWithRestrictions(distance);

            if (moveActionSetting != null && randomMoveCount >= effectiveMaxRandomMoveBeforeAction)
            {
                // 近接攻撃の場合、最低N回のランダム移動が必要.
                if (moveActionSetting.actionState is EnemState_Wendig_MeleeAttack && randomMoveCount < effectiveMinRandomMovesBeforeMelee)
                {
                    moveActionSetting = null; // 条件を満たさないのでランダム移動へ.
                }
            }

            if (moveActionSetting != null && randomMoveCount >= effectiveMaxRandomMoveBeforeAction)
            {
                // 移動stateで接近してからアクションを実行.
                currentActionSetting = moveActionSetting;

                // 移動stateを設定.
                if (moveActionSetting.moveState is EnemState_Wendig_MeleeApproach approach)
                {
                    approach.SetTargetPosition(targetPos);
                    approach.SetSpeedMultiplier(isAngry ? 2.6f : 1.3f);
                }
                else if (moveActionSetting.moveState is EnemState_Wendig_Move move)
                {
                    move.SetMovePos(targetPos);
                    move.SetLookAtPos(targetPos);
                    move.SetSpeedMultiplier(isAngry ? 4f : 2f);
                }

                // 移動実行（stateがタイムアウトまたは完了するまで待機）.
                await moveActionSetting.moveState.Act(ownerModel);

                // アプローチ中にプレイヤー近接検知で中断した場合、即座に近接攻撃抽選.
                bool approachProximity = moveActionSetting.moveState is EnemState_Wendig_MeleeApproach approachCheck && approachCheck.StoppedByPlayerProximity;
                bool moveProximity = moveActionSetting.moveState is EnemState_Wendig_Move moveCheck && moveCheck.StoppedByPlayerProximity;
                if (await TryProximityMeleeAttack(approachProximity || moveProximity))
                {
                    currentActionSetting = null;
                    return;
                }

                // 移動後、アクション発動距離内なら発動.
                float newDistance = ownerTransform != null ? Vector3.Distance(ownerTransform.position, TargetPosition) : float.MaxValue;
                if (newDistance <= moveActionSetting.activationDistance)
                {
                    // 突進攻撃の場合.
                    if (moveActionSetting.actionState is EnemState_Wendig_Rush rush)
                    {
                        rush.SetStageEdge(ownerModel.StageMin.x, ownerModel.StageMax.x);
                        await moveActionSetting.actionState.Act(ownerModel);
                        moveActionSetting.ConsumeRepeat();
                        meleeAttacksSinceLastRush = 0;
                        randomMoveCount = 0;
                        Debug.Log($"[EnemAIModel_Wendig_Normal] 移動後突進完了 - 近接カウンターリセット");
                        currentActionSetting = null;
                        return;
                    }

                    // 近接攻撃の場合、抽選でattack/bayt/howling/tripleAttackを選択.
                    if (moveActionSetting.actionState is EnemState_Wendig_MeleeAttack && ownerModel is EnemyModel_Wendig wendigModel2)
                    {
                        int selectedMeleeType = SelectMeleeAttackType();
                        switch (selectedMeleeType)
                        {
                            case 1: // bayt.
                                Debug.Log($"[EnemAIModel_Wendig_Normal] 移動後 抽選結果: bayt");
                                await wendigModel2.TriggerBayt();
                                break;
                            case 2: // howling.
                                Debug.Log($"[EnemAIModel_Wendig_Normal] 移動後 抽選結果: howling");
                                await wendigModel2.TriggerHowling();
                                break;
                            case 3: // tripleAttack.
                                Debug.Log($"[EnemAIModel_Wendig_Normal] 移動後 抽選結果: tripleAttack");
                                await wendigModel2.TriggerTripleAttack();
                                break;
                            default: // 0 = attack (通常攻撃).
                                Debug.Log($"[EnemAIModel_Wendig_Normal] 移動後 抽選結果: attack (通常攻撃)");
                                await moveActionSetting.actionState.Act(ownerModel);
                                break;
                        }
                        moveActionSetting.ConsumeRepeat();
                        meleeAttacksSinceLastRush++;
                        randomMoveCount = 0;
                        Debug.Log($"[EnemAIModel_Wendig_Normal] 近接攻撃カウント: {meleeAttacksSinceLastRush}");
                        currentActionSetting = null;
                        return;
                    }

                    // その他のアクション.
                    await moveActionSetting.actionState.Act(ownerModel);
                    moveActionSetting.ConsumeRepeat();
                }
                randomMoveCount = 0;
            }
            else
            {
                // ランダム移動.
                randomMoveCount++;
                Vector2 randomPos = new Vector2(
                    Random.Range(ownerModel.StageMin.x, ownerModel.StageMax.x),
                    Random.Range(ownerModel.StageMin.y, ownerModel.StageMax.y)
                );
                moveState.SetMovePos(randomPos);
                moveState.SetLookAtPos(targetPos);
                moveState.SetSpeedMultiplier(isAngry ? 4f : 2f);

                // 移動実行（stateがタイムアウトまたは完了するまで待機）.
                await moveState.Act(ownerModel);

                // プレイヤー近接検知で移動中断した場合、近接攻撃抽選を実行.
                if (await TryProximityMeleeAttack(moveState.StoppedByPlayerProximity))
                {
                    currentActionSetting = null;
                    return;
                }
            }
        }

        currentActionSetting = null;
    }

    // 近接攻撃タイプを抽選で選択.
    // 戻り値: 0 = attack (通常攻撃), 1 = bayt, 2 = howling, 3 = tripleAttack
    private int SelectMeleeAttackType()
    {
        // 各タイプの重みを配列化（連続攻撃防止のため前回と同じタイプは重み0）.
        float[] weights = { meleeAttackWeight, baytWeight, howlingWeight, tripleAttackWeight };
        if (lastMeleeAttackType >= 0 && lastMeleeAttackType < weights.Length)
        {
            weights[lastMeleeAttackType] = 0f;
        }

        float totalWeight = 0f;
        for (int i = 0; i < weights.Length; i++) totalWeight += weights[i];

        // 全て0になった場合のフォールバック（前回と異なるものをランダム選択）.
        if (totalWeight <= 0f)
        {
            int result;
            do { result = Random.Range(0, 4); } while (result == lastMeleeAttackType);
            lastMeleeAttackType = result;
            return result;
        }

        float randomValue = Random.Range(0f, totalWeight);
        float cumulative = 0f;
        for (int i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];
            if (randomValue < cumulative)
            {
                lastMeleeAttackType = i;
                return i;
            }
        }

        // フォールバック.
        int fallback = weights.Length - 1;
        lastMeleeAttackType = fallback;
        return fallback;
    }

    // 制限付きでアクションを選択（発動距離内）.
    private EnemAIActionSetting SelectActionByDistanceWithRestrictions(float distance)
    {
        int effectiveMinMelee = isAngry ? 0 : minRandomMovesBeforeMelee;
        var activatableActions = new System.Collections.Generic.List<EnemAIActionSetting>();
        float totalWeight = 0f;

        foreach (var setting in actionSettings)
        {
            if (!setting.CanActivate()) continue;
            if (distance > setting.activationDistance) continue;

            // 突進攻撃の制限チェック.
            if (setting.actionState is EnemState_Wendig_Rush)
            {
                if (meleeAttacksSinceLastRush < minMeleeAttacksBeforeRush)
                {
                    Debug.Log($"[EnemAIModel_Wendig_Normal] 突進スキップ - 近接カウント: {meleeAttacksSinceLastRush}/{minMeleeAttacksBeforeRush}");
                    continue;
                }
            }

            // 近接攻撃の制限チェック（怒り中は制限なし）.
            if (setting.actionState is EnemState_Wendig_MeleeAttack)
            {
                if (randomMoveCount < effectiveMinMelee)
                {
                    Debug.Log($"[EnemAIModel_Wendig_Normal] 近接スキップ - ランダム移動カウント: {randomMoveCount}/{effectiveMinMelee}");
                    continue;
                }
            }

            activatableActions.Add(setting);
            totalWeight += setting.activationWeight;
        }

        if (activatableActions.Count == 0) return null;

        // 重みづけでランダム選択.
        float randomValue = Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        foreach (var setting in activatableActions)
        {
            currentWeight += setting.activationWeight;
            if (randomValue <= currentWeight)
            {
                return setting;
            }
        }

        return activatableActions[activatableActions.Count - 1];
    }

    // 制限付きで移動アクションを選択.
    private EnemAIActionSetting SelectMoveActionByDistanceWithRestrictions(float distance)
    {
        int effectiveMinMelee = isAngry ? 0 : minRandomMovesBeforeMelee;
        var moveActions = new System.Collections.Generic.List<EnemAIActionSetting>();
        float totalWeight = 0f;

        foreach (var setting in actionSettings)
        {
            if (!setting.CanActivate()) continue;
            if (distance <= setting.activationDistance) continue;
            if (distance > setting.moveStartDistance) continue;
            if (setting.moveState == null) continue;

            // 突進攻撃の制限チェック.
            if (setting.actionState is EnemState_Wendig_Rush)
            {
                if (meleeAttacksSinceLastRush < minMeleeAttacksBeforeRush)
                {
                    continue;
                }
            }

            // 近接攻撃の制限チェック（怒り中は制限なし）.
            if (setting.actionState is EnemState_Wendig_MeleeAttack)
            {
                if (randomMoveCount < effectiveMinMelee)
                {
                    continue;
                }
            }

            moveActions.Add(setting);
            totalWeight += setting.activationWeight;
        }

        if (moveActions.Count == 0) return null;

        // 重みづけでランダム選択.
        float randomValue = Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        foreach (var setting in moveActions)
        {
            currentWeight += setting.activationWeight;
            if (randomValue <= currentWeight)
            {
                return setting;
            }
        }

        return moveActions[moveActions.Count - 1];
    }

    // カメラ範囲外かどうかをチェック.
    private bool IsOutsideCameraView(Vector3 playerPos)
    {
        if (ownerTransform == null) return false;

        Vector3 enemyPos = ownerTransform.position;
        float distanceX = Mathf.Abs(enemyPos.x - playerPos.x);
        float distanceY = Mathf.Abs(enemyPos.y - playerPos.y);

        return distanceX > cameraViewRangeX || distanceY > cameraViewRangeY;
    }

    // カメラ範囲内へ移動.
    private async UniTask MoveToCameraView(Vector3 playerPos)
    {
        if (ownerTransform == null || ownerModel == null) return;

        Vector3 enemyPos = ownerTransform.position;

        // プレイヤーの近くでカメラ内に収まる位置を計算.
        float targetX = playerPos.x;
        float targetY = playerPos.y;

        // X方向の調整.
        if (enemyPos.x > playerPos.x + cameraViewRangeX)
        {
            targetX = playerPos.x + cameraViewRangeX * 0.7f;
        }
        else if (enemyPos.x < playerPos.x - cameraViewRangeX)
        {
            targetX = playerPos.x - cameraViewRangeX * 0.7f;
        }

        // ステージ範囲内にクランプ.
        targetX = Mathf.Clamp(targetX, ownerModel.StageMin.x, ownerModel.StageMax.x);
        targetY = Mathf.Clamp(targetY, ownerModel.StageMin.y, ownerModel.StageMax.y);

        Vector2 cameraTargetPos = new Vector2(targetX, targetY);

        moveState.SetMovePos(cameraTargetPos);
        moveState.SetLookAtPos(playerPos);
        moveState.SetMoveTimeout(3f);
        moveState.SetSpeedMultiplier(isAngry ? 8f : 4f);

        await moveState.Act(ownerModel);

        // デフォルトに戻す.
        moveState.SetMoveTimeout(2f);
        moveState.SetSpeedMultiplier(isAngry ? 4f : 2f);
    }

    // プレイヤー近接検知による近接攻撃抽選を実行するヘルパー.
    // 戻り値: 近接攻撃を実行した場合true.
    private async UniTask<bool> TryProximityMeleeAttack(bool stoppedByProximity)
    {
        if (!stoppedByProximity) return false;
        if (!(ownerModel is EnemyModel_Wendig wendigModelProx)) return false;

        int meleeType = SelectMeleeAttackType();
        string[] typeNames = { "Attack(通常)", "Bayt(噛みつき)", "Howling(遠吠え)", "TripleAttack(三連撃)" };
        Debug.Log($"[AngerAI] プレイヤー近接検知 → 近接攻撃抽選結果: {typeNames[meleeType]} (type:{meleeType}) isAngry:{isAngry}");
        switch (meleeType)
        {
            case 1:
                await wendigModelProx.TriggerBayt();
                break;
            case 2:
                await wendigModelProx.TriggerHowling();
                break;
            case 3:
                await wendigModelProx.TriggerTripleAttack();
                break;
            default:
                await meleeAttackState.Act(ownerModel);
                break;
        }
        meleeAttacksSinceLastRush++;
        randomMoveCount = 0;
        return true;
    }
}

// Wendig用 移動State.
public class EnemState_Wendig_Move : EnemState_abstract
{
    private Vector2 moveTargetPos;
    private Vector3 lookAtPos;
    private float moveTimeout = 2f;
    private float speedMultiplier = 2f;

    // 移動中のプレイヤー近接検知用.
    private float meleeAttackRange = 2f;

    // プレイヤー近接で移動中断したかどうか（AIループ側で参照）.
    public bool StoppedByPlayerProximity { get; private set; } = false;

    public void SetMovePos(Vector2 targetPos)
    {
        moveTargetPos = targetPos;
    }

    public void SetLookAtPos(Vector3 targetPos)
    {
        lookAtPos = targetPos;
    }

    public void SetMoveTimeout(float timeout)
    {
        moveTimeout = timeout;
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = multiplier;
    }

    public override async UniTask Act(EnemyModel_abstract enemyModel)
    {
        if (!EnemNullSafetyHelper.IsValid(enemyModel)) return;

        // フラグリセット.
        StoppedByPlayerProximity = false;

        // EnemMovementHelperに委譲.
        var result = await EnemMovementHelper.ExecuteMove(enemyModel, new EnemMoveParams
        {
            Destination = moveTargetPos,
            Classification = EnemMoveClassification.RandomMove,
            SpeedMultiplier = speedMultiplier,
            FacingMode = EnemFacingMode.FacePlayer,
            PlayerPosition = lookAtPos,
            Timeout = moveTimeout,
            ArrivalThreshold = 0.5f,
            SetMoveAnimation = true,
            CancelConditions = new System.Collections.Generic.List<EnemMoveCancelCondition>
            {
                new EnemMoveCancelCondition
                {
                    CancelReason = "PlayerProximity",
                    ShouldCancel = (model, pos) =>
                    {
                        // プレイヤーとの距離が近接範囲内ならキャンセル.
                        return Vector2.Distance(pos, (Vector2)lookAtPos) <= meleeAttackRange;
                    }
                }
            }
        });

        // プレイヤー近接検知結果を反映.
        StoppedByPlayerProximity = result.Cancelled && result.CancelReason == "PlayerProximity";
    }
}

// Wendig用 近接アプローチState（移動のみ、コライダーなし）.
public class EnemState_Wendig_MeleeApproach : EnemState_abstract
{
    private Vector3 targetPosition;
    private float speedMultiplier = 3f;

    // アプローチの設定.
    private float approachDuration = 1.5f;
    private float arrivalThreshold = 1.5f;

    // プレイヤー近接検知用.
    private float playerProximityRange = 2f;
    public bool StoppedByPlayerProximity { get; private set; } = false;

    public void SetTargetPosition(Vector3 pos)
    {
        targetPosition = pos;
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = multiplier;
    }

    public override async UniTask Act(EnemyModel_abstract enemyModel)
    {
        if (!EnemNullSafetyHelper.IsValid(enemyModel)) return;

        // フラグリセット.
        StoppedByPlayerProximity = false;

        // プレイヤー位置取得用.
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");

        // EnemMovementHelperに委譲.
        var result = await EnemMovementHelper.ExecuteMove(enemyModel, new EnemMoveParams
        {
            Destination = (Vector2)targetPosition,
            Classification = EnemMoveClassification.ApproachTarget,
            SpeedMultiplier = speedMultiplier,
            FacingMode = EnemFacingMode.FaceMovement,
            PlayerPosition = targetPosition,
            Timeout = approachDuration,
            ArrivalThreshold = arrivalThreshold,
            SetMoveAnimation = true,
            CancelConditions = new System.Collections.Generic.List<EnemMoveCancelCondition>
            {
                new EnemMoveCancelCondition
                {
                    CancelReason = "PlayerProximity",
                    ShouldCancel = (model, pos) =>
                    {
                        // プレイヤーとの実際の距離を直接チェック.
                        if (playerObj == null) return false;
                        return Vector2.Distance(pos, (Vector2)playerObj.transform.position) <= playerProximityRange;
                    }
                }
            }
        });

        // プレイヤー近接検知結果を反映.
        StoppedByPlayerProximity = result.Cancelled && result.CancelReason == "PlayerProximity";
    }
}

// Wendig用 近距離攻撃State（コライダーはここで生成）.
public class EnemState_Wendig_MeleeAttack : EnemState_abstract
{
    // 軽い斬撃: 0.45倍.
    private float attackMultiplier = 0.45f;
    private Vector2 attackOffset = new Vector2(0.1f, 0f);
    private Vector2 attackSize = new Vector2(0.35f, 2f);

    // ヒット処理.
    private EnemColliderState_Wendig_MeleeAttack colliderState = new EnemColliderState_Wendig_MeleeAttack();

    public override async UniTask Act(EnemyModel_abstract enemyModel)
    {
        if (!EnemNullSafetyHelper.IsValidWithAnimator(enemyModel)) return;

        float animSpeed = enemyModel.AnimSpeed;
        Animator animator = enemyModel.Animator;

        // 現在の攻撃力から実ダメージを計算.
        int attackDamage = 23;
        if (enemyModel is EnemyModel_Wendig wendigModel)
        {
            attackDamage = (int)(wendigModel.GetCurrentAttackPower() * attackMultiplier);
        }

        // ヒット対象リストをクリア.
        colliderState.ClearHitTargets();
        colliderState.SetDamage(attackDamage);

        animator.SetTrigger("Attack");

        // === 前段階 ===.
        // 200ms待機 → 攻撃通告(パリィ可能) → 300ms待機.
        if (!await EnemAttackPhaseHelper.PlayAttackPremonition(
            enemyModel, 500f, true, 300f, animSpeed)) return;

        // === 攻撃中 ===.
        // Attackアニメーション終了まで当たり判定を維持.
        Vector2 colliderOffset = new Vector2(-Mathf.Abs(attackOffset.x), attackOffset.y);

        await EnemColliderHelper.ExecuteColliderPhaseUntil(
            enemyModel,
            new EnemColliderHelper.ColliderPhaseConfig
            {
                colliderType = EnemColliderType.Box,
                offset = colliderOffset,
                size = attackSize,
                damage = attackDamage,
                duration = 0.5f,
                colliderState = colliderState
            },
            () =>
            {
                if (!EnemNullSafetyHelper.IsValidWithAnimator(enemyModel)) return true;
                var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                return !stateInfo.IsName("Attack") || stateInfo.normalizedTime >= 1f;
            });

        // === 攻撃後 ===.
        // フレーム待機 → Attack_End トリガー.
        if (!await EnemAttackPhaseHelper.WaitPostAttackFrames(enemyModel, 40, animSpeed)) return;

        if (EnemNullSafetyHelper.IsValidWithAnimator(enemyModel))
        {
            animator.SetTrigger("Attack_End");
        }
    }
}

// Wendig用 近距離攻撃のヒット処理.
public class EnemColliderState_Wendig_MeleeAttack : EnemColliderState_PlayerDamage
{
    public EnemColliderState_Wendig_MeleeAttack()
    {
        // ダメージはAct時に動的に設定される.
        damage = 23;
    }

    protected override void OnHit(GameObject target, Collider2D hitCollider)
    {
        //Debug.Log($"[EnemColliderState_Wendig_MeleeAttack] OnHit - Target: {target.name}, Damage: {damage}");
        GuardState guardState = DamagePlayer(target, damage);
        //Debug.Log($"[EnemColliderState_Wendig_MeleeAttack] ダメージ結果 - GuardState: {guardState}");
    }
}

// Wendig用 突進State.
public class EnemState_Wendig_Rush : EnemState_abstract
{
    // 前進突進: 2.25倍.
    private float rushMultiplier = 2.25f;
    private int rushDamage = 113;

    // ヒット処理.
    private EnemColliderState_Wendig_Rush colliderState = new EnemColliderState_Wendig_Rush();

    private float stageMinX;
    private float stageMaxX;
    private float rushSpeed = 20f;
    private float moveToEdgeSpeed = 12f;
    private int waitFrames = 40;

    public void SetStageEdge(float minX, float maxX)
    {
        stageMinX = minX;
        stageMaxX = maxX;
    }

    public override async UniTask Act(EnemyModel_abstract enemyModel)
    {
        if (!EnemNullSafetyHelper.IsValid(enemyModel)) return;

        // 現在の攻撃力から実ダメージを計算.
        if (enemyModel is EnemyModel_Wendig wendigModel)
        {
            rushDamage = (int)(wendigModel.GetCurrentAttackPower() * rushMultiplier);
        }

        Rigidbody2D rb = enemyModel.Rigidbody;
        Transform ownerTransform = enemyModel.Presenter.transform;
        Animator animator = enemyModel.Animator;
        Collider2D mainColl = enemyModel.Presenter.MainColl;
        float animSpeed = enemyModel.AnimSpeed;

        // 怒り時は速度2倍.
        float angerSpeedMult = animSpeed > 1f ? 2f : 1f;
        float effectiveRushSpeed = rushSpeed * angerSpeedMult;
        float effectiveMoveToEdgeSpeed = moveToEdgeSpeed * angerSpeedMult;

        if (rb == null || ownerTransform == null) return;

        // 元の状態を保存.
        RigidbodyConstraints2D originalConstraints = rb.constraints;
        bool originalIsTrigger = mainColl != null ? mainColl.isTrigger : false;

        // 突進中はY軸固定とコライダーをトリガーに設定.
        rb.constraints = originalConstraints | RigidbodyConstraints2D.FreezePositionY;
        if (mainColl != null)
        {
            mainColl.isTrigger = true;
        }

        Vector2 currentPos = ownerTransform.position;

        // 近い方の端を選択.
        float distToMin = Mathf.Abs(currentPos.x - stageMinX);
        float distToMax = Mathf.Abs(currentPos.x - stageMaxX);
        float targetEdgeX = distToMin < distToMax ? stageMinX : stageMaxX;
        float oppositeEdgeX = distToMin < distToMax ? stageMaxX : stageMinX;

        // === 前段階 ===.
        // 1. 端の方向を見ながら端まで移動.
        float targetEdgeDirectionX = Mathf.Sign(targetEdgeX - currentPos.x);
        EnemFacingHelper.FaceDirection(ownerTransform, targetEdgeDirectionX);

        // EnemMovementHelperで端へ移動.
        var edgeMoveResult = await EnemMovementHelper.ExecuteMove(enemyModel, new EnemMoveParams
        {
            Destination = new Vector2(targetEdgeX, currentPos.y),
            Classification = EnemMoveClassification.RushPreMove,
            SpeedMultiplier = effectiveMoveToEdgeSpeed / enemyModel.MoveSpeed.x,
            FacingMode = EnemFacingMode.FaceMovement,
            Timeout = 5f,
            ArrivalThreshold = 0.5f,
            SetMoveAnimation = true
        });

        if (edgeMoveResult.NullInterrupted)
        {
            RestoreState(rb, mainColl, originalConstraints, originalIsTrigger);
            return;
        }

        // 2. 逆方向を見る.
        if (!EnemNullSafetyHelper.IsValid(enemyModel))
        {
            RestoreState(rb, mainColl, originalConstraints, originalIsTrigger);
            return;
        }
        float oppositeDirectionX = Mathf.Sign(oppositeEdgeX - ownerTransform.position.x);
        EnemFacingHelper.FaceDirection(ownerTransform, oppositeDirectionX);

        // 3. Assault_Pre トリガー実行.
        if (!EnemNullSafetyHelper.IsValidWithAnimator(enemyModel))
        {
            RestoreState(rb, mainColl, originalConstraints, originalIsTrigger);
            return;
        }
        animator.SetTrigger("Assault_Pre");
        await UniTask.WaitUntil(() =>
        {
            if (!EnemNullSafetyHelper.IsValidWithAnimator(enemyModel)) return true;
            return animator.GetCurrentAnimatorStateInfo(0).IsName("Assault_Pre") == false ||
                   animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f;
        });

        // 攻撃通告: パリィ不可 (突進の0.3秒前).
        if (!EnemNullSafetyHelper.IsValid(enemyModel))
        {
            RestoreState(rb, mainColl, originalConstraints, originalIsTrigger);
            return;
        }
        enemyModel.Presenter.PlayAttackWarning(false);
        await UniTask.Delay((int)(300 / animSpeed));

        // === 攻撃中 ===.
        if (!EnemNullSafetyHelper.IsValid(enemyModel))
        {
            RestoreState(rb, mainColl, originalConstraints, originalIsTrigger);
            return;
        }

        // 突進中のヒット検出を設定（既存コライダー使用）.
        colliderState.ClearHitTargets();
        colliderState.SetDamage(rushDamage);
        EnemyAttackHitDetector rushHitDetector = null;
        if (mainColl != null)
        {
            rushHitDetector = EnemColliderHelper.AttachHitDetector(
                ownerTransform, colliderState,
                new System.Collections.Generic.List<Collider2D> { mainColl });
        }

        animator.SetTrigger("Assault_Assault");

        // 逆端まで突進.
        while (Mathf.Abs(ownerTransform.position.x - oppositeEdgeX) > 0.5f)
        {
            if (!EnemNullSafetyHelper.IsValid(enemyModel))
            {
                if (rushHitDetector != null) Object.Destroy(rushHitDetector);
                RestoreState(rb, mainColl, originalConstraints, originalIsTrigger);
                return;
            }
            rb.linearVelocity = new Vector2(oppositeDirectionX * effectiveRushSpeed, rb.linearVelocity.y);
            await UniTask.Yield();
        }

        // 突進ヒット検出を終了.
        if (rushHitDetector != null)
        {
            Object.Destroy(rushHitDetector);
        }

        if (!EnemNullSafetyHelper.IsValid(enemyModel))
        {
            RestoreState(rb, mainColl, originalConstraints, originalIsTrigger);
            return;
        }
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);

        // === 攻撃後 ===.
        animator.SetTrigger("Assault_End");

        // フレーム待機.
        if (!await EnemAttackPhaseHelper.WaitPostAttackFrames(enemyModel, waitFrames, animSpeed))
        {
            RestoreState(rb, mainColl, originalConstraints, originalIsTrigger);
            return;
        }

        if (EnemNullSafetyHelper.IsValidWithAnimator(enemyModel))
        {
            animator.SetTrigger("Assault_Finish");
        }

        // 元の状態を復元.
        RestoreState(rb, mainColl, originalConstraints, originalIsTrigger);
    }

    // 元の状態を復元するヘルパーメソッド.
    private void RestoreState(Rigidbody2D rb, Collider2D mainColl, RigidbodyConstraints2D originalConstraints, bool originalIsTrigger)
    {
        if (rb != null)
        {
            rb.constraints = originalConstraints;
        }
        if (mainColl != null)
        {
            mainColl.isTrigger = originalIsTrigger;
        }
    }
}

// Wendig用 突進のヒット処理.
public class EnemColliderState_Wendig_Rush : EnemColliderState_PlayerDamage
{
    public EnemColliderState_Wendig_Rush()
    {
        // ダメージはAct時に動的に設定される (前進突進: 2.25倍).
        damage = 113;
        // 突進の吹き飛ばし力は10倍.
        knockbackForce = 10f;
        // パリィ・ガード貫通.
        powerlevel = InGame.Common.PowerlevelConst.EnemyRush;
    }

    protected override void OnHit(GameObject target, Collider2D hitCollider)
    {
        //Debug.Log($"[EnemColliderState_Wendig_Rush] OnHit - Target: {target.name}, Damage: {damage}");
        GuardState guardState = DamagePlayer(target, damage);
        //Debug.Log($"[EnemColliderState_Wendig_Rush] ダメージ結果 - GuardState: {guardState}");
    }
}

// 敵攻撃のヒット検出用コンポーネント.
public class EnemyAttackHitDetector : MonoBehaviour
{
    private EnemColliderState_abstract colliderState;
    private System.Collections.Generic.List<Collider2D> targetColliders;

    public void Initialize(EnemColliderState_abstract state, System.Collections.Generic.List<Collider2D> colliders)
    {
        // ランタイム専用コンポーネントとしてフラグ設定.
        hideFlags = HideFlags.HideAndDontSave;
        colliderState = state;
        targetColliders = colliders;
        //Debug.Log($"[EnemyAttackHitDetector] Initialize - Collider数: {colliders.Count}");
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 対象のコライダーからのヒットかチェック.
        if (colliderState == null || targetColliders == null) return;

        // プレイヤータグをチェック.
        if (!other.CompareTag("Player")) return;

        //Debug.Log($"[EnemyAttackHitDetector] OnTriggerEnter2D - {other.gameObject.name}");

        // 攻撃者のTransformを設定（エネミーの向き判定用）.
        colliderState.SetAttackerTransform(transform);

        // ヒット処理を実行（重複処理はcolliderStateが防ぐ）.
        colliderState.TryProcessHit(other.gameObject, other);
    }
}

// Wendig用 Dead割り込みState.
public class EnemInterruptState_Dead_Wendig : EnemInterruptState_Dead_abstract
{
    public EnemInterruptState_Dead_Wendig()
    {
        deathAnimationDelay = 2f;
    }

    protected override async UniTask OnDeathComplete(EnemyModel_abstract enemyModel)
    {
        Debug.Log($"[EnemInterruptState_Dead_Wendig] OnDeathComplete開始");

        if (enemyModel == null || enemyModel.Presenter == null)
        {
            Debug.LogWarning($"[EnemInterruptState_Dead_Wendig] OnDeathComplete中断 - enemyModel or Presenter が null");
            return;
        }

        // AIループを停止.
        enemyModel.EnemAIStop();
        Debug.Log($"[EnemInterruptState_Dead_Wendig] AIループ停止");

        // GameObjectを破棄（既存の処理）.
        Object.Destroy(enemyModel.gameObject);
        Debug.Log($"[EnemInterruptState_Dead_Wendig] GameObject破棄");

        await UniTask.CompletedTask;
    }
}

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
        Debug.Log($"[EnemInterruptState_Stan_Wendig] OnStanProcess開始");

        // スタンSE再生.
        if (enemyModel?.Presenter != null)
        {
            enemyModel.Presenter.PlaySE("Stan");
        }

        // スタン持続時間分待機.
        await UniTask.Delay((int)(stanDuration * 1000));
    }
}

// Wendig用 Bayt割り込みState.
public class EnemInterruptState_Bayt_Wendig : EnemInterruptState_abstract
{
    public EnemInterruptState_Bayt_Wendig()
    {
        stateType = EnemyState.Attack;
        priority = 20;
    }

    // 噛みつく: 1.5倍.
    private float attackMultiplier = 1.5f;
    private Vector2 attackOffset = new Vector2(0.1f, 0f);
    private Vector2 attackSize = new Vector2(0.35f, 2f);

    public override async UniTask Act(EnemyModel_abstract enemyModel)
    {
        Debug.Log($"[EnemInterruptState_Bayt_Wendig] Act開始");

        if (!EnemNullSafetyHelper.IsValidWithAnimator(enemyModel)) return;

        // 現在の攻撃力から実ダメージを計算.
        int attackDamage = 75;
        if (enemyModel is EnemyModel_Wendig wendigModel)
        {
            attackDamage = (int)(wendigModel.GetCurrentAttackPower() * attackMultiplier);
        }

        Animator animator = enemyModel.Animator;
        float animSpeed = enemyModel.AnimSpeed;

        // Baytアニメーション開始.
        animator.SetTrigger("Bayt");

        // === 前段階 ===.
        // 1100ms待機 → 攻撃通告(パリィ可能) → 400ms待機.
        if (!await EnemAttackPhaseHelper.PlayAttackPremonition(
            enemyModel, 1500f, true, 400f, animSpeed)) return;

        // === 攻撃中 ===.
        // 攻撃判定を400ms維持.
        var colliderState = new EnemColliderState_Wendig_Bayt();
        colliderState.SetDamage(attackDamage);
        colliderState.ClearHitTargets();

        Vector2 colliderOffset = new Vector2(-Mathf.Abs(attackOffset.x), attackOffset.y);

        await EnemColliderHelper.ExecuteColliderPhase(
            enemyModel,
            new EnemColliderHelper.ColliderPhaseConfig
            {
                colliderType = EnemColliderType.Box,
                offset = colliderOffset,
                size = attackSize,
                damage = attackDamage,
                duration = 0.5f,
                colliderState = colliderState
            },
            400f, animSpeed);

        if (!EnemNullSafetyHelper.IsValid(enemyModel)) return;

        // Baytアニメーション完了を待機.
        float baytAnimWait = 0f;
        float baytAnimWaitMax = 1.5f;
        bool foundBaytState = false;
        while (baytAnimWait < baytAnimWaitMax)
        {
            if (!EnemNullSafetyHelper.IsValidWithAnimator(enemyModel)) break;
            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsName("Bayt"))
            {
                foundBaytState = true;
                if (stateInfo.normalizedTime >= 1f) break;
            }
            else if (foundBaytState)
            {
                // Baytステートから抜けた → 完了.
                break;
            }
            baytAnimWait += Time.deltaTime;
            await UniTask.Yield();
        }

        // === 攻撃後 ===.
        // フレーム待機 → Bayt_End トリガー.
        if (!await EnemAttackPhaseHelper.WaitPostAttackFrames(enemyModel, 40, animSpeed)) return;

        if (EnemNullSafetyHelper.IsValidWithAnimator(enemyModel))
        {
            animator.SetTrigger("Bayt_End");
        }

        Debug.Log($"[EnemInterruptState_Bayt_Wendig] Act完了");
    }
}

// Wendig用 Baytのヒット処理.
public class EnemColliderState_Wendig_Bayt : EnemColliderState_PlayerDamage
{
    public EnemColliderState_Wendig_Bayt()
    {
        // ダメージはAct時に動的に設定される (噛みつく: 1.5倍).
        damage = 75;
    }

    protected override void OnHit(GameObject target, Collider2D hitCollider)
    {
        Debug.Log($"[EnemColliderState_Wendig_Bayt] OnHit - Target: {target.name}, Damage: {damage}");
        GuardState guardState = DamagePlayer(target, damage);
    }
}

// Wendig用 Howling(遠吠え)State.
public class EnemState_Wendig_Howling : EnemState_abstract
{
    // ハウリング: 1.8倍.
    private float attackMultiplier = 1.8f;
    private float attackRadius = 3f;

    public override async UniTask Act(EnemyModel_abstract enemyModel)
    {
        Debug.Log($"[EnemState_Wendig_Howling] Act開始");

        if (!EnemNullSafetyHelper.IsValidWithAnimator(enemyModel)) return;

        // 現在の攻撃力から実ダメージを計算.
        int attackDamage = 90;
        if (enemyModel is EnemyModel_Wendig wendigModel)
        {
            attackDamage = (int)(wendigModel.GetCurrentAttackPower() * attackMultiplier);
        }

        float animSpeed = enemyModel.AnimSpeed;

        // Howlingアニメーション開始.
        enemyModel.Animator.SetTrigger("Howling");

        // === 前段階 ===.
        // 300ms待機 → 攻撃通告(パリィ不可) → 1000ms待機.
        if (!await EnemAttackPhaseHelper.PlayAttackPremonition(
            enemyModel, 1300f, false, 1000f, animSpeed)) return;

        // === 攻撃中 ===.
        // 円形の攻撃判定を生成し400ms維持.
        var colliderState = new EnemColliderState_Wendig_Howling();
        colliderState.SetDamage(attackDamage);
        colliderState.ClearHitTargets();

        await EnemColliderHelper.ExecuteColliderPhase(
            enemyModel,
            new EnemColliderHelper.ColliderPhaseConfig
            {
                colliderType = EnemColliderType.Circle,
                offset = Vector2.zero,
                radius = attackRadius,
                damage = attackDamage,
                duration = 0.5f,
                colliderState = colliderState
            },
            400f, animSpeed);

        // === 攻撃後 ===.
        // Howling_End トリガー実行.
        if (EnemNullSafetyHelper.IsValidWithAnimator(enemyModel))
        {
            enemyModel.Animator.SetTrigger("Howling_End");
        }

        Debug.Log($"[EnemState_Wendig_Howling] Act完了");
    }
}

// Wendig用 Howlingのヒット処理.
public class EnemColliderState_Wendig_Howling : EnemColliderState_PlayerDamage
{
    public EnemColliderState_Wendig_Howling()
    {
        // ダメージはAct時に動的に設定される (ハウリング: 1.8倍).
        damage = 90;
        // ハウリングの吹き飛ばし力は10倍.
        knockbackForce = 10f;
        // パリィ・ガード貫通.
        powerlevel = InGame.Common.PowerlevelConst.EnemyHowling;
    }

    protected override void OnHit(GameObject target, Collider2D hitCollider)
    {
        Debug.Log($"[EnemColliderState_Wendig_Howling] OnHit - Target: {target.name}, Damage: {damage}");
        GuardState guardState = DamagePlayer(target, damage);
    }
}

// Wendig用 TripleAttack(三連撃)State.
public class EnemState_Wendig_TripleAttack : EnemState_abstract
{
    // 三連撃: 0.75/1.1/1.35倍.
    private float[] attackMultipliers = { 0.75f, 1.1f, 1.35f };
    private Vector2 attackOffset = new Vector2(0.1f, 0f);
    private Vector2 attackSize = new Vector2(0.35f, 2f);

    // 三連撃のトリガー名.
    private string[] attackTriggers = { "TripleAttack_0", "TripleAttack_1", "TripleAttack_2" };

    public override async UniTask Act(EnemyModel_abstract enemyModel)
    {
        Debug.Log($"[EnemState_Wendig_TripleAttack] Act開始");

        if (!EnemNullSafetyHelper.IsValidWithAnimator(enemyModel)) return;

        // 基礎攻撃力を取得.
        float baseAttackPower = 50f;
        if (enemyModel is EnemyModel_Wendig wendigModel)
        {
            baseAttackPower = wendigModel.GetCurrentAttackPower();
        }

        Animator animator = enemyModel.Animator;
        float animSpeed = enemyModel.AnimSpeed;

        // TripleAttack開始トリガー実行.
        animator.SetTrigger("TripleAttack");

        // === 前段階 ===.
        // 200ms待機 → 攻撃通告(パリィ可能) → 300ms待機.
        if (!await EnemAttackPhaseHelper.PlayAttackPremonition(
            enemyModel, 500f, true, 300f, animSpeed)) return;

        // === 攻撃中 ===.
        // 三連撃を実行.
        Vector2 colliderOffset = new Vector2(-Mathf.Abs(attackOffset.x), attackOffset.y);

        for (int i = 0; i < 3; i++)
        {
            if (!EnemNullSafetyHelper.IsValidWithAnimator(enemyModel)) break;

            // 各段のダメージを計算.
            int attackDamage = (int)(baseAttackPower * attackMultipliers[i]);

            // 攻撃トリガー実行.
            animator.SetTrigger(attackTriggers[i]);

            if (!await EnemAttackPhaseHelper.DelayWithAnimSpeed(enemyModel, 100f, animSpeed)) break;

            // 攻撃判定を100ms維持.
            var colliderState = new EnemColliderState_PlayerDamage();
            colliderState.SetDamage(attackDamage);
            colliderState.ClearHitTargets();

            if (!await EnemColliderHelper.ExecuteColliderPhase(
                enemyModel,
                new EnemColliderHelper.ColliderPhaseConfig
                {
                    colliderType = EnemColliderType.Box,
                    offset = colliderOffset,
                    size = attackSize,
                    damage = attackDamage,
                    duration = 0.3f,
                    colliderState = colliderState
                },
                100f, animSpeed)) break;

            // 攻撃間の待機（最終段以外）.
            if (i < 2)
            {
                if (!await EnemAttackPhaseHelper.DelayWithAnimSpeed(enemyModel, 100f, animSpeed)) return;
            }
        }

        // === 攻撃後 ===.
        // 100ms待機 → TripleAttack_End トリガー.
        if (!await EnemAttackPhaseHelper.DelayWithAnimSpeed(enemyModel, 100f, animSpeed)) return;

        if (EnemNullSafetyHelper.IsValidWithAnimator(enemyModel))
        {
            animator.SetTrigger("TripleAttack_End");
        }

        Debug.Log($"[EnemState_Wendig_TripleAttack] Act完了");
    }
}

// 割り込みStateリスト管理クラス.
public class EnemInterruptStateList_Wendig
{
    // 割り込みStateリスト.
    private List<EnemInterruptState_abstract> interruptStates = new List<EnemInterruptState_abstract>();

    // Wendig用の割り込みState.
    private EnemInterruptState_Dead_Wendig deadState = new EnemInterruptState_Dead_Wendig();
    private EnemInterruptState_Stan_Wendig stanState = new EnemInterruptState_Stan_Wendig();
    private EnemInterruptState_Bayt_Wendig baytState = new EnemInterruptState_Bayt_Wendig();

    // 現在実行中の割り込みState.
    private EnemInterruptState_abstract currentInterruptState = null;
    public bool IsInterrupting => currentInterruptState != null;

    public EnemInterruptStateList_Wendig()
    {
        // 優先度順に追加.
        interruptStates.Add(deadState);
        interruptStates.Add(stanState);
        interruptStates.Add(baytState);
        Debug.Log($"[EnemInterruptStateList_Wendig] 初期化完了 - State数: {interruptStates.Count}");
    }

    // Dead Stateを取得.
    public EnemInterruptState_Dead_Wendig GetDeadState()
    {
        return deadState;
    }

    // Stan Stateを取得.
    public EnemInterruptState_Stan_Wendig GetStanState()
    {
        return stanState;
    }

    // Bayt Stateを取得.
    public EnemInterruptState_Bayt_Wendig GetBaytState()
    {
        return baytState;
    }

    // 指定されたStateTypeの割り込みStateを取得.
    public EnemInterruptState_abstract GetInterruptState(EnemyState stateType)
    {
        foreach (var state in interruptStates)
        {
            if (state.StateType == stateType)
            {
                return state;
            }
        }
        return null;
    }

    // 割り込みStateを実行.
    public async UniTask ExecuteInterrupt(EnemInterruptState_abstract state, EnemyModel_abstract enemyModel)
    {
        if (state == null) return;

        // 現在の割り込みより優先度が低い場合は実行しない.
        if (currentInterruptState != null && state.Priority <= currentInterruptState.Priority)
        {
            Debug.Log($"[EnemInterruptStateList_Wendig] 割り込み拒否 - 現在の優先度: {currentInterruptState.Priority}, 要求: {state.Priority}");
            return;
        }

        currentInterruptState = state;
        Debug.Log($"[EnemInterruptStateList_Wendig] 割り込み実行 - StateType: {state.StateType}, Priority: {state.Priority}");

        await state.Act(enemyModel);

        currentInterruptState = null;
        Debug.Log($"[EnemInterruptStateList_Wendig] 割り込み完了");
    }
}
