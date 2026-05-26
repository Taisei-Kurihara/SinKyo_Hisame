using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;

/// <summary>
/// Wendigo通常行動パターンのAIアップデーター.
/// EnemAIModel_Wendig_Normalの状態を参照しながら行動決定を行う.
/// </summary>
public class EnemAIUpdater_Wendig_Normal : EnemAIUpdater_Wendig_abstract
{
    // --- 内部ステートマシン ---
    private enum UpdaterState
    {
        Idle,           // 行動決定待ち.
        AngerHowling,   // 怒りHowling実行中.
        Cooldown        // クールダウン中.
    }
    private UpdaterState currentState = UpdaterState.Idle;

    // --- 行動トラッキング ---
    private int randomMoveCount = 0;
    private int maxRandomMoveBeforeAction = 3;
    private EnemAIActionSetting currentActionSetting = null;
    private float aiStartTime = -1f;
    private float lastRushEndTime = -100f;
    private const float postRushCooldown = 1.5f;

    // 怒りHowling予約.
    private bool pendingAngerHowling = false;

    // カメラ範囲設定.
    private float cameraViewRangeX = 8f;
    private float cameraViewRangeY = 5f;

    // 近接攻撃タイプの重み設定.
    private float meleeAttackWeight = 1f;
    private float baytWeight = 1f;
    private float howlingWeight = 1f;
    private float tripleAttackWeight = 1f;

    // 突進制限用カウンター.
    private int meleeAttacksSinceLastRush = 0;
    private int minMeleeAttacksBeforeRush = 3;

    // 近距離攻撃前のランダム移動必須回数.
    private int minRandomMovesBeforeMelee = 1;

    // 連続攻撃防止用.
    private int lastMeleeAttackType = -1;

    // 基本移動速度.
    private const float baseMoveSpeed = 3f;
    private const float baseApproachSpeed = 4.8f;

    public EnemAIUpdater_Wendig_Normal(EnemAIModel_Wendig_Normal master) : base(master)
    {
        // Normal phaseのHP → 閾値 = phaseHp / divisor.
        InitAngerThreshold(master.GetCurrentPhaseHp());
    }

    // === 状態の明示的リセット ===

    protected override async UniTask OnUpdateStart(CancellationToken token)
    {
        // 全内部状態を初期化.
        currentState = UpdaterState.Idle;
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
        // 速度修正リセット.
        currentSpeedModifier = WendigSpeedModifier.Default;
        if (masterAI.OwnerModel?.Animator != null)
        {
            masterAI.OwnerModel.Animator.speed = 1f;
        }
        Debug.Log($"[WendigNormalUpdater] OnUpdateStart - 全状態リセット完了");
        await UniTask.CompletedTask;
    }

    // --- MasterAIへのショートカット ---
    private EnemyModel_abstract ownerModel => masterAI.OwnerModel;
    private Transform ownerTransform => masterAI.OwnerTransform;
    private Vector3 TargetPosition => masterAI.TargetPosition;

    // === 怒り状態フック ===

    protected override void OnEnterAnger()
    {
        pendingAngerHowling = true;
        ApplySpeedModifier(WendigSpeedModifierTable.NormalAngry);
        Debug.Log($"[WendigNormalUpdater] 怒り開始 → Howling予約, 速度修正: NormalAngry");
    }

    protected override void OnExitAnger()
    {
        ApplySpeedModifier(WendigSpeedModifier.Default);
        Debug.Log($"[WendigNormalUpdater] 怒り解除 → 速度修正: Default");
    }

    // === メインループ ===

    protected override async UniTask OnUpdateLoop(CancellationToken token)
    {
        // 初期化.
        WendigMasterAI.EnsureInitialized();

        // AI開始時刻を記録.
        if (aiStartTime < 0f)
        {
            aiStartTime = Time.time;
        }

        // 怒りゲージ減衰.
        DecayAngerGauge(Time.deltaTime);

        // ステートマシン.
        switch (currentState)
        {
            case UpdaterState.Idle:
                await ProcessIdle(token);
                break;
            case UpdaterState.AngerHowling:
                await ProcessAngerHowling(token);
                break;
            case UpdaterState.Cooldown:
                currentState = UpdaterState.Idle;
                break;
        }
    }

    // === ステート処理 ===

    private async UniTask ProcessIdle(CancellationToken token)
    {
        // 怒り状態Howling実行（怒り開始時に1回だけ）.
        if (pendingAngerHowling)
        {
            currentState = UpdaterState.AngerHowling;
            return;
        }

        // 怒り中: ランダム移動なし、即攻撃.
        int effectiveMinRandomMovesBeforeMelee = isAngry ? 0 : minRandomMovesBeforeMelee;
        int effectiveMaxRandomMoveBeforeAction = isAngry ? 0 : maxRandomMoveBeforeAction;

        Vector3 targetPos = TargetPosition;
        float distance = ownerTransform != null ? Vector3.Distance(ownerTransform.position, targetPos) : float.MaxValue;

        // AI開始から1秒間は攻撃を行わず、ランダム移動のみ実行.
        if (Time.time - aiStartTime < 1f)
        {
            await ExecuteRandomMove(targetPos);
            currentActionSetting = null;
            return;
        }

        // 突進後1.5秒間は攻撃を行わず、ランダム移動のみ実行.
        if (Time.time - lastRushEndTime < postRushCooldown)
        {
            await ExecuteRandomMove(targetPos);
            currentActionSetting = null;
            return;
        }

        // カメラ範囲外にいる場合の処理.
        bool isOffScreen = IsOutsideCameraView(targetPos);
        if (isOffScreen)
        {
            float yDiffForCamera = ownerTransform != null ? TargetPosition.y - ownerTransform.position.y : 0f;
            if (yDiffForCamera >= 3f)
            {
                Debug.Log($"[WendigNormalUpdater] カメラ範囲外だがとびかかり切り条件成立 - カメラ内移動スキップ");
            }
            else
            {
                await MoveToCameraView(targetPos);
            }
        }

        // アクション選択と実行.
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
            Debug.Log($"[WendigNormalUpdater] ★怒りHowling実行開始★");
            await wendigModelAnger.TriggerHowling();
        }
        currentState = UpdaterState.Idle;
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
