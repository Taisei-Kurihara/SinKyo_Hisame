using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Wendigo用AIマスタークラス.
/// 状態（State/設定）を保持し、行動決定ロジックはUpdaterに委譲する.
/// HPフェーズ管理とUpdater切り替えを担当する.
/// </summary>
public class EnemAIModel_Wendig_Normal : EnemAIModel_abstract
{
    // --- State一覧 ---
    private EnemState_Wendig_Move moveState = new EnemState_Wendig_Move();
    private EnemState_Wendig_MeleeApproach meleeApproachState = new EnemState_Wendig_MeleeApproach();
    private EnemState_Wendig_MeleeAttack meleeAttackState = new EnemState_Wendig_MeleeAttack();
    private EnemState_Wendig_Rush rushState = new EnemState_Wendig_Rush();
    private EnemState_Wendig_Howling howlingState = new EnemState_Wendig_Howling();
    private EnemState_Wendig_TripleAttack tripleAttackState = new EnemState_Wendig_TripleAttack();
    private EnemState_Wendig_JumpSlash jumpSlashState = new EnemState_Wendig_JumpSlash();

    // --- State公開プロパティ（Updaterからアクセス用） ---
    public EnemState_Wendig_Move MoveState => moveState;
    public EnemState_Wendig_MeleeApproach MeleeApproachState => meleeApproachState;
    public EnemState_Wendig_MeleeAttack MeleeAttackState => meleeAttackState;
    public EnemState_Wendig_Rush RushState => rushState;
    public EnemState_Wendig_Howling HowlingState => howlingState;
    public EnemState_Wendig_TripleAttack TripleAttackState => tripleAttackState;
    public EnemState_Wendig_JumpSlash JumpSlashState => jumpSlashState;

    // --- 割り込みStateリスト ---
    private EnemInterruptStateList_Wendig interruptStateList = new EnemInterruptStateList_Wendig();
    public EnemInterruptStateList_Wendig InterruptStateList => interruptStateList;

    // --- HPフェーズ管理 ---
    private List<WendigHpPhase> hpPhases = new List<WendigHpPhase>();
    private int currentPhaseIndex = 0;
    private float totalHp;

    /// <summary>現在のフェーズインデックス.</summary>
    public int CurrentPhaseIndex => currentPhaseIndex;

    /// <summary>現在のフェーズ名.</summary>
    public string CurrentPhaseName => hpPhases.Count > currentPhaseIndex ? hpPhases[currentPhaseIndex].phaseName : "Unknown";

    // --- 初期化 ---
    private bool isInitialized = false;

    public EnemAIModel_Wendig_Normal()
    {
        // HPフェーズ初期化.
        InitHpPhases();
        // 初期Updater: Normal.
        SetUpdater(new EnemAIUpdater_Wendig_Normal(this));
    }

    // === HPフェーズ管理 ===

    private void InitHpPhases()
    {
        hpPhases.Clear();
        hpPhases.Add(new WendigHpPhase
        {
            hp = 7500f,
            displayRatio = 0.5f,
            phaseName = "Normal"
        });
        hpPhases.Add(new WendigHpPhase
        {
            hp = 12500f,
            displayRatio = 0.5f,
            phaseName = "Berserk"
        });

        totalHp = 0f;
        foreach (var phase in hpPhases) totalHp += phase.hp;

        currentPhaseIndex = 0;
    }

    /// <summary>
    /// remainingHp（status.hp.Value）からHPバーパーセントを計算（フェーズ按分）.
    /// </summary>
    public float CalculateHpBarPercent(float remainingHp)
    {
        float consumed = totalHp - Mathf.Max(0f, remainingHp);
        float barConsumed = 0f;

        for (int i = 0; i < hpPhases.Count; i++)
        {
            if (consumed <= 0f) break;
            float phaseConsumption = Mathf.Min(consumed, hpPhases[i].hp);
            barConsumed += (phaseConsumption / hpPhases[i].hp) * hpPhases[i].displayRatio;
            consumed -= phaseConsumption;
        }

        return Mathf.Clamp01(1f - barConsumed);
    }

    /// <summary>
    /// フェーズ遷移チェック（ダメージ時に呼ぶ）.
    /// </summary>
    public void CheckPhaseTransition(float remainingHp)
    {
        float consumed = totalHp - Mathf.Max(0f, remainingHp);
        int newPhaseIndex = 0;
        float accumulated = 0f;

        for (int i = 0; i < hpPhases.Count; i++)
        {
            accumulated += hpPhases[i].hp;
            if (consumed >= accumulated && i < hpPhases.Count - 1)
            {
                newPhaseIndex = i + 1;
            }
            else
            {
                break;
            }
        }

        if (newPhaseIndex > currentPhaseIndex)
        {
            Debug.Log($"[WendigMasterAI] HPフェーズ遷移 → {hpPhases[newPhaseIndex].phaseName} (index:{newPhaseIndex}, remainingHp:{remainingHp})");
            currentPhaseIndex = newPhaseIndex;
            OnPhaseTransition(newPhaseIndex);
        }
    }

    /// <summary>フェーズ遷移時の処理.</summary>
    private void OnPhaseTransition(int newPhaseIndex)
    {
        var phase = hpPhases[newPhaseIndex];
        if (phase.phaseName == "Berserk")
        {
            // 暴走Updaterに切り替え.
            var berserkUpdater = new EnemAIUpdater_Wendig_Berserk(this);
            SwitchUpdater(berserkUpdater);
            Debug.Log($"[WendigMasterAI] 暴走Updater切り替え要求");
        }
    }

    /// <summary>現在のフェーズのHP量を取得（怒り閾値計算用）.</summary>
    public float GetCurrentPhaseHp()
    {
        return hpPhases.Count > currentPhaseIndex ? hpPhases[currentPhaseIndex].hp : 0f;
    }

    // === 怒りゲージ転送 ===

    /// <summary>ダメージ由来の怒りゲージ増加を現在のUpdaterに転送.</summary>
    public void NotifyDamageForAnger(float amount)
    {
        CurrentUpdater?.IncreaseAngerGauge(amount);
    }

    // === アクション設定初期化 ===

    /// <summary>アクション設定を初期化（Updaterから呼び出し可能）.</summary>
    public void EnsureInitialized()
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

        // とびかかり切り攻撃設定（移動なし、直接ジャンプ）.
        AddActionSetting(new EnemAIActionSetting
        {
            actionState = jumpSlashState,
            repeatableCount = -1,
            activationDistance = 15f,
            moveStartDistance = 15f,
            activationWeight = 1.5f,
            moveState = null,
            shouldActivate = true
        });

        isInitialized = true;
    }
}
