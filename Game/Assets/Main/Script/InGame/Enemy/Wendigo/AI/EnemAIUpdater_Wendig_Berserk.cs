using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;

/// <summary>
/// Wendigo暴走フェーズのAIアップデーター.
/// 暴走フェーズ突入時にEnemAIModel_Wendig_Normalからスイッチされる.
/// 疲労システムと暴走用行動パターンを持つ.
/// </summary>
public class EnemAIUpdater_Wendig_Berserk : EnemAIUpdater_Wendig_abstract
{
    // --- 内部ステートマシン ---
    private enum BerserkState
    {
        Idle,           // 行動決定待ち.
        AngerHowling,   // 怒りHowling実行中.
        Cooldown        // クールダウン中.
    }
    private BerserkState currentState = BerserkState.Idle;

    // === 疲労システム ===
    private float fatigue = 0f; // 0-1.
    public float Fatigue => fatigue;

    private float berserkElapsedTime = 0f;

    // 疲労パラメータ.
    private const float fatigueOnsetDelay = 30f;     // 暴走開始30秒後から疲労開始.
    private const float fatigueRampDuration = 30f;    // 30秒で0→1に到達（合計60秒時点でmax）.
    private const float fatigueRecoveryDuration = 60f; // max後60秒で1.0→0.5に回復.
    private bool fatigueMaxReached = false;
    private float fatigueRecoveryTimer = 0f;

    // --- 行動トラッキング（Normalより攻撃的） ---
    private int randomMoveCount = 0;
    private int maxRandomMoveBeforeAction = 2; // 暴走時はより攻撃的.
    private EnemAIActionSetting currentActionSetting = null;
    private float aiStartTime = -1f;
    private float lastRushEndTime = -100f;
    private const float postRushCooldown = 1.0f; // 通常より短い.

    // 怒りHowling予約.
    private bool pendingAngerHowling = false;

    // カメラ範囲設定.
    private float cameraViewRangeX = 8f;
    private float cameraViewRangeY = 5f;

    // 近接攻撃タイプの重み設定（暴走時は攻撃系が重い）.
    private float meleeAttackWeight = 1.5f;
    private float baytWeight = 1f;
    private float howlingWeight = 0.5f;
    private float tripleAttackWeight = 1.5f;

    // 突進制限用カウンター（暴走時は少ない）.
    private int meleeAttacksSinceLastRush = 0;
    private int minMeleeAttacksBeforeRush = 2;

    // 近距離攻撃前のランダム移動必須回数.
    private int minRandomMovesBeforeMelee = 0; // 暴走時は制限なし.

    // 連続攻撃防止用.
    private int lastMeleeAttackType = -1;

    // 基本移動速度.
    private const float baseMoveSpeed = 3f;
    private const float baseApproachSpeed = 4.8f;

    public EnemAIUpdater_Wendig_Berserk(EnemAIModel_Wendig_Normal master) : base(master)
    {
        // Berserk phaseのHP（難易度反映済み）から怒り閾値を計算.
        InitAngerThreshold(master.GetCurrentPhaseHp());
    }

    // --- MasterAIへのショートカット ---
    private EnemyModel_abstract ownerModel => masterAI.OwnerModel;
    private Transform ownerTransform => masterAI.OwnerTransform;
    private Vector3 TargetPosition => masterAI.TargetPosition;

    // === 怒り状態フック ===

    protected override void OnEnterAnger()
    {
        pendingAngerHowling = true;
        ApplyCurrentSpeedModifier();
        Debug.Log($"[WendigBerserkUpdater] 怒り開始 → Howling予約");
    }

    protected override void OnExitAnger()
    {
        ApplyCurrentSpeedModifier();
        Debug.Log($"[WendigBerserkUpdater] 怒り解除");
    }

    // === ライフサイクル ===

    protected override async UniTask OnUpdateStart(CancellationToken token)
    {
        // 全内部状態を初期化.
        currentState = BerserkState.Idle;
        berserkElapsedTime = 0f;
        fatigue = 0f;
        fatigueMaxReached = false;
        fatigueRecoveryTimer = 0f;
        randomMoveCount = 0;
        currentActionSetting = null;
        aiStartTime = Time.time;
        lastRushEndTime = -100f;
        pendingAngerHowling = false;
        meleeAttacksSinceLastRush = 0;
        lastMeleeAttackType = -1;
        // 怒りゲージリセット.
        angerGauge = 0f;
        isAngry = false;
        Debug.Log($"[WendigBerserkUpdater] OnUpdateStart - 全状態リセット完了");

        // 暴走開始時の演出（Howling）.
        if (ownerModel is EnemyModel_Wendig wendigModel)
        {
            Debug.Log($"[WendigBerserkUpdater] ★暴走開始 → Howling演出★");
            await wendigModel.TriggerHowling();
        }

        // 初期速度修正適用.
        ApplyCurrentSpeedModifier();
    }

    // === メインループ ===

    protected override async UniTask OnUpdateLoop(CancellationToken token)
    {
        WendigMasterAI.EnsureInitialized();

        if (aiStartTime < 0f)
        {
            aiStartTime = Time.time;
        }

        float deltaTime = Time.deltaTime;

        // 怒りゲージ減衰.
        DecayAngerGauge(deltaTime);

        // 疲労更新.
        UpdateFatigue(deltaTime);

        // 速度修正の再計算.
        ApplyCurrentSpeedModifier();

        // ステートマシン.
        switch (currentState)
        {
            case BerserkState.Idle:
                await ProcessIdle(token);
                break;
            case BerserkState.AngerHowling:
                await ProcessAngerHowling(token);
                break;
            case BerserkState.Cooldown:
                currentState = BerserkState.Idle;
                break;
        }
    }

    // === 疲労更新 ===

    private void UpdateFatigue(float deltaTime)
    {
        berserkElapsedTime += deltaTime;

        if (!fatigueMaxReached)
        {
            // 30秒経過後から疲労が蓄積開始.
            if (berserkElapsedTime >= fatigueOnsetDelay)
            {
                float fatigueTime = berserkElapsedTime - fatigueOnsetDelay;
                fatigue = Mathf.Clamp01(fatigueTime / fatigueRampDuration);

                if (fatigue >= 1f)
                {
                    fatigueMaxReached = true;
                    fatigueRecoveryTimer = 0f;
                    Debug.Log($"[WendigBerserkUpdater] 疲労MAX到達 (elapsed:{berserkElapsedTime:F1}s)");
                }
            }
        }
        else
        {
            // max後: 60秒で1.0→0.5に回復.
            fatigueRecoveryTimer += deltaTime;
            float t = Mathf.Clamp01(fatigueRecoveryTimer / fatigueRecoveryDuration);
            fatigue = Mathf.Lerp(1f, 0.5f, t);
        }
    }

    // === 速度修正の計算と適用 ===

    private void ApplyCurrentSpeedModifier()
    {
        WendigSpeedModifier modifier;

        if (fatigue > 0.01f)
        {
            // 疲労あり: (BerserkDefault × Lerp(Default, BerserkFatigue, fatigue)) × anger.
            WendigSpeedModifier fatigueBlend = WendigSpeedModifier.Lerp(
                WendigSpeedModifier.Default,
                WendigSpeedModifierTable.BerserkFatigue,
                fatigue
            );
            WendigSpeedModifier baseMod = WendigSpeedModifier.Combine(
                WendigSpeedModifierTable.BerserkDefault,
                fatigueBlend
            );

            if (isAngry)
            {
                modifier = WendigSpeedModifier.Combine(baseMod, WendigSpeedModifierTable.BerserkAngry);
            }
            else
            {
                modifier = baseMod;
            }
        }
        else
        {
            // 疲労なし.
            modifier = isAngry
                ? WendigSpeedModifierTable.BerserkAngry
                : WendigSpeedModifierTable.BerserkDefault;
        }

        ApplySpeedModifier(modifier);
    }

    // === ステート処理 ===

    private async UniTask ProcessIdle(CancellationToken token)
    {
        if (pendingAngerHowling)
        {
            currentState = BerserkState.AngerHowling;
            return;
        }

        // 暴走中: ランダム移動なし、即攻撃.
        int effectiveMinRandomMovesBeforeMelee = isAngry ? 0 : minRandomMovesBeforeMelee;
        int effectiveMaxRandomMoveBeforeAction = isAngry ? 0 : maxRandomMoveBeforeAction;

        Vector3 targetPos = TargetPosition;
        float distance = ownerTransform != null ? Vector3.Distance(ownerTransform.position, targetPos) : float.MaxValue;

        // AI開始から0.5秒間は攻撃を行わず（暴走は短い）.
        if (Time.time - aiStartTime < 0.5f)
        {
            await ExecuteRandomMove(targetPos);
            currentActionSetting = null;
            return;
        }

        // 突進後クールダウン.
        if (Time.time - lastRushEndTime < postRushCooldown)
        {
            await ExecuteRandomMove(targetPos);
            currentActionSetting = null;
            return;
        }

        // カメラ範囲外処理.
        bool isOffScreen = IsOutsideCameraView(targetPos);
        if (isOffScreen)
        {
            float yDiffForCamera = ownerTransform != null ? TargetPosition.y - ownerTransform.position.y : 0f;
            if (yDiffForCamera >= 3f)
            {
                Debug.Log($"[WendigBerserkUpdater] カメラ範囲外だがとびかかり切り条件成立");
            }
            else
            {
                await MoveToCameraView(targetPos);
            }
        }

        // アクション選択.
        float offScreenDist = 0f;
        if (isOffScreen && ownerTransform != null)
        {
            float dX = Mathf.Abs(ownerTransform.position.x - targetPos.x) - cameraViewRangeX;
            float dY = Mathf.Abs(ownerTransform.position.y - targetPos.y) - cameraViewRangeY;
            offScreenDist = Mathf.Max(dX, dY, 0f);
        }
        EnemAIActionSetting selectedSetting = SelectActionByDistanceWithRestrictions(distance, offScreenDist);

        if (selectedSetting != null)
        {
            currentActionSetting = selectedSetting;

            if (selectedSetting.actionState is EnemState_Wendig_JumpSlash jumpSlash)
            {
                jumpSlash.SetTargetPosition(targetPos);
                await selectedSetting.actionState.Act(ownerModel);
                selectedSetting.ConsumeRepeat();
                randomMoveCount = 0;
                currentActionSetting = null;
                return;
            }

            if (selectedSetting.actionState is EnemState_Wendig_Rush rush)
            {
                rush.SetStageEdge(ownerModel.StageMin.x, ownerModel.StageMax.x);
                await selectedSetting.actionState.Act(ownerModel);
                selectedSetting.ConsumeRepeat();
                meleeAttacksSinceLastRush = 0;
                randomMoveCount = 0;
                lastRushEndTime = Time.time;
                currentActionSetting = null;
                return;
            }

            if (selectedSetting.actionState is EnemState_Wendig_MeleeAttack && ownerModel is EnemyModel_Wendig wendigModelMelee)
            {
                int selectedMeleeType = SelectMeleeAttackType();
                switch (selectedMeleeType)
                {
                    case 1: await wendigModelMelee.TriggerBayt(); break;
                    case 2: await wendigModelMelee.TriggerHowling(); break;
                    case 3: await wendigModelMelee.TriggerTripleAttack(); break;
                    default: await selectedSetting.actionState.Act(ownerModel); break;
                }
                selectedSetting.ConsumeRepeat();
                meleeAttacksSinceLastRush++;
                randomMoveCount = 0;
                currentActionSetting = null;
                return;
            }

            await selectedSetting.actionState.Act(ownerModel);
            selectedSetting.ConsumeRepeat();
            randomMoveCount = 0;
        }
        else
        {
            EnemAIActionSetting moveActionSetting = SelectMoveActionByDistanceWithRestrictions(distance);

            if (moveActionSetting != null && randomMoveCount >= effectiveMaxRandomMoveBeforeAction)
            {
                if (moveActionSetting.actionState is EnemState_Wendig_MeleeAttack && randomMoveCount < effectiveMinRandomMovesBeforeMelee)
                {
                    moveActionSetting = null;
                }
            }

            if (moveActionSetting != null && randomMoveCount >= effectiveMaxRandomMoveBeforeAction)
            {
                currentActionSetting = moveActionSetting;

                if (moveActionSetting.moveState is EnemState_Wendig_MeleeApproach approach)
                {
                    approach.SetTargetPosition(targetPos);
                    approach.SetSpeedMultiplier(GetApproachSpeed(baseApproachSpeed));
                }
                else if (moveActionSetting.moveState is EnemState_Wendig_Move move)
                {
                    move.SetMovePos(targetPos);
                    move.SetLookAtPos(targetPos);
                    move.SetSpeedMultiplier(GetMoveSpeed(baseMoveSpeed));
                }

                await moveActionSetting.moveState.Act(ownerModel);

                bool approachProximity = moveActionSetting.moveState is EnemState_Wendig_MeleeApproach approachCheck && approachCheck.StoppedByPlayerProximity;
                bool moveProximity = moveActionSetting.moveState is EnemState_Wendig_Move moveCheck && moveCheck.StoppedByPlayerProximity;
                if (await TryProximityMeleeAttack(approachProximity || moveProximity))
                {
                    currentActionSetting = null;
                    return;
                }

                float newDistance = ownerTransform != null ? Vector3.Distance(ownerTransform.position, TargetPosition) : float.MaxValue;
                if (newDistance <= moveActionSetting.activationDistance)
                {
                    if (moveActionSetting.actionState is EnemState_Wendig_Rush rush)
                    {
                        rush.SetStageEdge(ownerModel.StageMin.x, ownerModel.StageMax.x);
                        await moveActionSetting.actionState.Act(ownerModel);
                        moveActionSetting.ConsumeRepeat();
                        meleeAttacksSinceLastRush = 0;
                        randomMoveCount = 0;
                        lastRushEndTime = Time.time;
                        currentActionSetting = null;
                        return;
                    }

                    if (moveActionSetting.actionState is EnemState_Wendig_MeleeAttack && ownerModel is EnemyModel_Wendig wendigModel2)
                    {
                        int selectedMeleeType = SelectMeleeAttackType();
                        switch (selectedMeleeType)
                        {
                            case 1: await wendigModel2.TriggerBayt(); break;
                            case 2: await wendigModel2.TriggerHowling(); break;
                            case 3: await wendigModel2.TriggerTripleAttack(); break;
                            default: await moveActionSetting.actionState.Act(ownerModel); break;
                        }
                        moveActionSetting.ConsumeRepeat();
                        meleeAttacksSinceLastRush++;
                        randomMoveCount = 0;
                        currentActionSetting = null;
                        return;
                    }

                    await moveActionSetting.actionState.Act(ownerModel);
                    moveActionSetting.ConsumeRepeat();
                }
                randomMoveCount = 0;
            }
            else
            {
                await ExecuteRandomMove(targetPos);

                if (await TryProximityMeleeAttack(WendigMasterAI.MoveState.StoppedByPlayerProximity))
                {
                    currentActionSetting = null;
                    return;
                }
            }
        }

        currentActionSetting = null;
    }

    private async UniTask ProcessAngerHowling(CancellationToken token)
    {
        pendingAngerHowling = false;
        if (ownerModel is EnemyModel_Wendig wendigModelAnger)
        {
            Debug.Log($"[WendigBerserkUpdater] ★怒りHowling実行★");
            await wendigModelAnger.TriggerHowling();
        }
        currentState = BerserkState.Idle;
    }

    // --- ヘルパーメソッド ---

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

        var moveState = WendigMasterAI.MoveState;
        moveState.SetMovePos(randomPos);
        moveState.SetLookAtPos(targetPos);
        moveState.SetSpeedMultiplier(GetMoveSpeed(baseMoveSpeed));

        float prevCameraViewRangeX = cameraViewRangeX;
        cameraViewRangeX = 12f;

        await moveState.Act(ownerModel);

        cameraViewRangeX = prevCameraViewRangeX;
    }

    private int SelectMeleeAttackType()
    {
        float[] weights = { meleeAttackWeight, baytWeight, howlingWeight, tripleAttackWeight };
        if (lastMeleeAttackType >= 0 && lastMeleeAttackType < weights.Length)
        {
            weights[lastMeleeAttackType] = 0f;
        }

        float totalWeight = 0f;
        for (int i = 0; i < weights.Length; i++) totalWeight += weights[i];

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

        int fallback = weights.Length - 1;
        lastMeleeAttackType = fallback;
        return fallback;
    }

    private EnemAIActionSetting SelectActionByDistanceWithRestrictions(float distance, float offScreenDistance = 0f)
    {
        int effectiveMinMelee = isAngry ? 0 : minRandomMovesBeforeMelee;
        var activatableActions = new List<EnemAIActionSetting>();
        var actionWeights = new List<float>();
        float totalWeight = 0f;

        foreach (var setting in masterAI.ActionSettings)
        {
            if (!setting.CanActivate()) continue;

            bool isJumpSlash = setting.actionState is EnemState_Wendig_JumpSlash;

            if (!isJumpSlash && distance > setting.activationDistance) continue;

            if (isJumpSlash)
            {
                float yDiff = TargetPosition.y - ownerTransform.position.y;
                if (yDiff < 3f) continue;
            }

            if (setting.actionState is EnemState_Wendig_Rush)
            {
                if (meleeAttacksSinceLastRush < minMeleeAttacksBeforeRush) continue;
            }

            if (setting.actionState is EnemState_Wendig_MeleeAttack)
            {
                if (randomMoveCount < effectiveMinMelee) continue;
            }

            float weight = setting.activationWeight;
            if (isJumpSlash && offScreenDistance > 0f)
            {
                weight += offScreenDistance;
            }

            activatableActions.Add(setting);
            actionWeights.Add(weight);
            totalWeight += weight;
        }

        if (activatableActions.Count == 0) return null;

        float randomValue = Random.Range(0f, totalWeight);
        float currentWeight = 0f;
        for (int i = 0; i < activatableActions.Count; i++)
        {
            currentWeight += actionWeights[i];
            if (randomValue <= currentWeight) return activatableActions[i];
        }

        return activatableActions[activatableActions.Count - 1];
    }

    private EnemAIActionSetting SelectMoveActionByDistanceWithRestrictions(float distance)
    {
        int effectiveMinMelee = isAngry ? 0 : minRandomMovesBeforeMelee;
        var moveActions = new List<EnemAIActionSetting>();
        float totalWeight = 0f;

        foreach (var setting in masterAI.ActionSettings)
        {
            if (!setting.CanActivate()) continue;
            if (distance <= setting.activationDistance) continue;
            if (distance > setting.moveStartDistance) continue;
            if (setting.moveState == null) continue;

            if (setting.actionState is EnemState_Wendig_JumpSlash)
            {
                float yDiff = TargetPosition.y - ownerTransform.position.y;
                if (yDiff < 3f) continue;
            }

            if (setting.actionState is EnemState_Wendig_Rush)
            {
                if (meleeAttacksSinceLastRush < minMeleeAttacksBeforeRush) continue;
            }

            if (setting.actionState is EnemState_Wendig_MeleeAttack)
            {
                if (randomMoveCount < effectiveMinMelee) continue;
            }

            moveActions.Add(setting);
            totalWeight += setting.activationWeight;
        }

        if (moveActions.Count == 0) return null;

        float randomValue = Random.Range(0f, totalWeight);
        float currentWeight = 0f;
        foreach (var setting in moveActions)
        {
            currentWeight += setting.activationWeight;
            if (randomValue <= currentWeight) return setting;
        }

        return moveActions[moveActions.Count - 1];
    }

    private bool IsOutsideCameraView(Vector3 playerPos)
    {
        if (ownerTransform == null) return false;
        Vector3 enemyPos = ownerTransform.position;
        float distanceX = Mathf.Abs(enemyPos.x - playerPos.x);
        float distanceY = Mathf.Abs(enemyPos.y - playerPos.y);
        return distanceX > cameraViewRangeX || distanceY > cameraViewRangeY;
    }

    private async UniTask MoveToCameraView(Vector3 playerPos)
    {
        if (ownerTransform == null || ownerModel == null) return;

        Vector3 enemyPos = ownerTransform.position;
        float targetX = playerPos.x;
        float targetY = playerPos.y;

        if (enemyPos.x > playerPos.x + cameraViewRangeX)
        {
            targetX = playerPos.x + cameraViewRangeX * 0.7f;
        }
        else if (enemyPos.x < playerPos.x - cameraViewRangeX)
        {
            targetX = playerPos.x - cameraViewRangeX * 0.7f;
        }

        targetX = Mathf.Clamp(targetX, ownerModel.StageMin.x, ownerModel.StageMax.x);
        targetY = Mathf.Clamp(targetY, ownerModel.StageMin.y, ownerModel.StageMax.y);

        Vector2 cameraTargetPos = new Vector2(targetX, targetY);

        var moveState = WendigMasterAI.MoveState;
        moveState.SetMovePos(cameraTargetPos);
        moveState.SetLookAtPos(playerPos);
        moveState.SetMoveTimeout(3f);
        moveState.SetSpeedMultiplier(GetMoveSpeed(baseMoveSpeed));

        await moveState.Act(ownerModel);

        moveState.SetMoveTimeout(2f);
        moveState.SetSpeedMultiplier(GetMoveSpeed(baseMoveSpeed));
    }

    private async UniTask<bool> TryProximityMeleeAttack(bool stoppedByProximity)
    {
        if (!stoppedByProximity) return false;
        if (!(ownerModel is EnemyModel_Wendig wendigModelProx)) return false;

        int meleeType = SelectMeleeAttackType();
        switch (meleeType)
        {
            case 1: await wendigModelProx.TriggerBayt(); break;
            case 2: await wendigModelProx.TriggerHowling(); break;
            case 3: await wendigModelProx.TriggerTripleAttack(); break;
            default: await WendigMasterAI.MeleeAttackState.Act(ownerModel); break;
        }
        meleeAttacksSinceLastRush++;
        randomMoveCount = 0;
        return true;
    }
}
