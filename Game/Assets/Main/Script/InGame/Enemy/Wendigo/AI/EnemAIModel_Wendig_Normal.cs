using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;

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

    // AI開始時刻（攻撃抑制用）.
    private float aiStartTime = -1f;

    // 突進後の攻撃クールダウン.
    private float lastRushEndTime = -100f;
    private const float postRushCooldown = 1.5f;

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

        // AI開始時刻を記録.
        if (aiStartTime < 0f)
        {
            aiStartTime = Time.time;
        }

        // バトル開始処理（初回のみ）— コメントアウト: AI開始直後の攻撃抑制のため.
        // if (!hasBattleStarted && ownerModel is EnemyModel_Wendig wendigModel)
        // {
        //     hasBattleStarted = true;
        //     Debug.Log($"[EnemAIModel_Wendig_Normal] バトル開始 - Howling → TripleAttack");
        //     await wendigModel.TriggerBattleStart();
        // }

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

        // AI開始から1秒間は攻撃を行わず、ランダム移動のみ実行.
        if (Time.time - aiStartTime < 1f)
        {
            Debug.Log($"[EnemAIModel_Wendig_Normal] AI開始ディレイ中 - ランダム移動のみ");
            await ExecuteRandomMove(targetPos);
            currentActionSetting = null;
            return;
        }

        // 突進後1.5秒間は攻撃を行わず、ランダム移動のみ実行.
        if (Time.time - lastRushEndTime < postRushCooldown)
        {
            Debug.Log($"[EnemAIModel_Wendig_Normal] 突進後クールダウン中 - ランダム移動のみ");
            await ExecuteRandomMove(targetPos);
            currentActionSetting = null;
            return;
        }

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
                lastRushEndTime = Time.time; // 突進後クールダウン開始.
                Debug.Log($"[EnemAIModel_Wendig_Normal] 突進完了 - 近接カウンターリセット, クールダウン開始");
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
                    approach.SetSpeedMultiplier(isAngry ? 9.6f : 4.8f);
                }
                else if (moveActionSetting.moveState is EnemState_Wendig_Move move)
                {
                    move.SetMovePos(targetPos);
                    move.SetLookAtPos(targetPos);
                    move.SetSpeedMultiplier(isAngry ? 6f : 3f);
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
                        lastRushEndTime = Time.time; // 突進後クールダウン開始.
                        Debug.Log($"[EnemAIModel_Wendig_Normal] 移動後突進完了 - 近接カウンターリセット, クールダウン開始");
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
                await ExecuteRandomMove(targetPos);

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

    // ランダム移動を実行（自身から最低3ユニット離れた位置を選択）.
    private async UniTask ExecuteRandomMove(Vector3 targetPos)
    {
        randomMoveCount++;
        Vector2 currentPos = ownerTransform.position;
        const float minDistance = 3f;
        const int maxRetries = 5;

        Vector2 randomPos = Vector2.zero;
        for (int i = 0; i < maxRetries; i++)
        {
            randomPos = new Vector2(
                Random.Range(ownerModel.StageMin.x, ownerModel.StageMax.x),
                Random.Range(ownerModel.StageMin.y, ownerModel.StageMax.y)
            );
            if (Vector2.Distance(currentPos, randomPos) >= minDistance) break;
        }

        moveState.SetMovePos(randomPos);
        moveState.SetLookAtPos(targetPos);
        moveState.SetSpeedMultiplier(isAngry ? 6f : 3f);

        // ランダム移動中はカメラ範囲を緩和.
        float prevCameraViewRangeX = cameraViewRangeX;
        cameraViewRangeX = 12f;

        await moveState.Act(ownerModel);

        // カメラ範囲を元に戻す.
        cameraViewRangeX = prevCameraViewRangeX;
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
        var activatableActions = new List<EnemAIActionSetting>();
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
        var moveActions = new List<EnemAIActionSetting>();
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
        moveState.SetSpeedMultiplier(isAngry ? 6f : 3f);

        await moveState.Act(ownerModel);

        // デフォルトに戻す.
        moveState.SetMoveTimeout(2f);
        moveState.SetSpeedMultiplier(isAngry ? 6f : 3f);
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
