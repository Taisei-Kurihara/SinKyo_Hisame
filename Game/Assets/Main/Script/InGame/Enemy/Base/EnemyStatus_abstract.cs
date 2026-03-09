using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

public abstract class EnemyStatus_abstract : MonoBehaviour
{

    protected EnemyPresenter_abstract presenter = null;
    public EnemyPresenter_abstract Presenter { set { if (presenter == null) presenter = value; } }
    public new abstract string name { get; }

    public ReactiveProperty<float> hp = new ReactiveProperty<float> (100);
    public float maxhp { get; set; } = 100f;

    protected virtual ChangeHP defaultDamage { get; set; } = new ChangeHP_Damage_Default();
    protected virtual ChangeHP defaultHeal { get; set; } = new ChangeHP_Heal_Default();

    // =========================================================
    // 怒り状態.
    // =========================================================
    public enum AngerState { Normal, Angry }
    public AngerState currentAngerState { get; protected set; } = AngerState.Normal;

    // 怒りゲージ現在値.
    protected float angerGaugeCurrent = 0f;
    public float AngerGaugeCurrent => angerGaugeCurrent;

    // クランプ変数（減少下限/上昇上限）.
    protected float angerGaugeClamp = 0f;

    // 最大値.
    protected float maxAngerGauge = 100f;

    // 通常状態: 減少設定.
    protected float normalDecayRateMax = 5f;
    protected float normalDecayRateMin = 0.1f;
    protected float normalDecayRecoveryDuration = 10f;
    protected float normalClampDecayRate = 2f;
    protected float normalClampIncreaseRatio = 0.5f;

    // 怒り状態: 減少設定.
    protected float angryDecayRateMax = 8f;
    protected float angryDecayRateMin = 0.1f;
    protected float angryDecayRecoveryDuration = 10f;
    protected float angryClampDecayRate = 4f;

    // 減少速度追跡.
    private float currentDecayRate = 5f;
    private float decayRecoveryTimer = 0f;
    private bool isDecayRecovering = false;

    // ログ出力用タイマー（毎フレームのスパム防止）.
    private float angerLogTimer = 0f;
    private const float angerLogInterval = 2f;

    // 怒り状態変更コールバック.
    protected System.Action<AngerState> onAngerStateChanged;
    public void SetOnAngerStateChanged(System.Action<AngerState> cb) { onAngerStateChanged = cb; }



    public abstract void Init();

    private void Awake()
    {
        Debug.Log($"[EnemyStatus_abstract] Awake - {gameObject.name}, HP: {hp.Value}, MaxHP: {maxhp}");
        hp
            .Where(_ => _ <= 0)
            .Take(1)
            .Subscribe(_ =>
            {
                Debug.Log($"[EnemyStatus_abstract] HP <= 0 検知 - Dead処理開始");
                Dead().Forget();
            })
            .AddTo(this);
        Debug.Log($"[EnemyStatus_abstract] Awake完了 - HP監視設定済み");
    }

    public virtual async UniTask OnDamaged(float damage, ChangeHP aschangeHP = null)
    {
        Debug.Log($"[EnemyStatus_abstract] OnDamaged - {gameObject.name}, Damage: {damage}, 現在HP: {hp.Value}");
        aschangeHP ??= defaultDamage;
        await aschangeHP.OnHPChange(this, damage);
        Debug.Log($"[EnemyStatus_abstract] OnDamaged完了 - 残りHP: {hp.Value}");

        // 怒りゲージ増加.
        OnAngerGaugeIncrease(damage);
    }

    public virtual async UniTask OnHeal(float heal, ChangeHP aschangeHP = null)
    {
        Debug.Log($"[EnemyStatus_abstract] OnHeal - {gameObject.name}, Heal: {heal}, 現在HP: {hp.Value}");
        aschangeHP ??= defaultHeal;
        await aschangeHP.OnHPChange(this, heal);
        Debug.Log($"[EnemyStatus_abstract] OnHeal完了 - 残りHP: {hp.Value}");
    }

    protected virtual async UniTask Dead()
    {
        Debug.Log($"[EnemyStatus_abstract] Dead開始 - {gameObject.name}");
        await DeadAnim();
        Debug.Log($"[EnemyStatus_abstract] DeadAnim完了 - GameObject破棄");
        Destroy(this.gameObject);
    }

    protected virtual UniTask DeadAnim()
    {
        Debug.Log($"[EnemyStatus_abstract] DeadAnim - {gameObject.name}");
        return UniTask.CompletedTask;
    }

    // =========================================================
    // 怒りゲージ更新 (毎フレーム呼び出し).
    // =========================================================
    protected virtual void UpdateAngerGauge(float deltaTime)
    {
        // 定期ログ出力（ゲージが0でない時のみ）.
        if (angerGaugeCurrent > 0f || currentAngerState == AngerState.Angry)
        {
            angerLogTimer += deltaTime;
            if (angerLogTimer >= angerLogInterval)
            {
                angerLogTimer = 0f;
                Debug.Log($"[AngerGauge] 定期 - State:{currentAngerState} Current:{angerGaugeCurrent:F2}/{maxAngerGauge} Clamp:{angerGaugeClamp:F2} DecayRate:{currentDecayRate:F2} Recovering:{isDecayRecovering}");
            }
        }

        // 減少速度の回復（min→max 線形補間）.
        if (isDecayRecovering)
        {
            float recoveryDuration = currentAngerState == AngerState.Normal
                ? normalDecayRecoveryDuration
                : angryDecayRecoveryDuration;
            float decayMax = currentAngerState == AngerState.Normal
                ? normalDecayRateMax
                : angryDecayRateMax;
            float decayMin = currentAngerState == AngerState.Normal
                ? normalDecayRateMin
                : angryDecayRateMin;

            decayRecoveryTimer += deltaTime;
            float t = Mathf.Clamp01(decayRecoveryTimer / recoveryDuration);
            currentDecayRate = Mathf.Lerp(decayMin, decayMax, t);

            if (t >= 1f)
            {
                isDecayRecovering = false;
            }
        }

        if (currentAngerState == AngerState.Normal)
        {
            // Normal時: clampはゆっくり減少.
            angerGaugeClamp = Mathf.Max(0f, angerGaugeClamp - normalClampDecayRate * deltaTime);

            // currentはclampを下回らず減少.
            angerGaugeCurrent -= currentDecayRate * deltaTime;
            angerGaugeCurrent = Mathf.Max(angerGaugeCurrent, angerGaugeClamp);
            angerGaugeCurrent = Mathf.Max(angerGaugeCurrent, 0f);

            // max到達で怒り開始.
            if (angerGaugeCurrent >= maxAngerGauge)
            {
                EnterAngerState();
            }
        }
        else // Angry
        {
            // Angry時: clampとcurrent両方減少.
            angerGaugeClamp = Mathf.Max(0f, angerGaugeClamp - angryClampDecayRate * deltaTime);
            angerGaugeCurrent -= currentDecayRate * deltaTime;
            angerGaugeCurrent = Mathf.Max(angerGaugeCurrent, 0f);

            // current=0で怒り解除.
            if (angerGaugeCurrent <= 0f)
            {
                ExitAngerState();
            }
        }
    }

    // =========================================================
    // 怒りゲージ増加 (被ダメージ時).
    // =========================================================
    protected virtual void OnAngerGaugeIncrease(float damage)
    {
        float increase = CalculateAngerIncrease(damage);
        float prevCurrent = angerGaugeCurrent;
        float prevClamp = angerGaugeClamp;

        if (currentAngerState == AngerState.Normal)
        {
            angerGaugeCurrent = Mathf.Min(angerGaugeCurrent + increase, maxAngerGauge);
            angerGaugeClamp += increase * normalClampIncreaseRatio;
            angerGaugeClamp = Mathf.Min(angerGaugeClamp, maxAngerGauge);
        }
        else // Angry
        {
            // 怒り中: currentはclampを超えない.
            angerGaugeCurrent = Mathf.Min(angerGaugeCurrent + increase, angerGaugeClamp);
        }

        Debug.Log($"[AngerGauge] 増加 - State:{currentAngerState} Damage:{damage:F1} Increase:{increase:F2} Current:{prevCurrent:F2}→{angerGaugeCurrent:F2} Clamp:{prevClamp:F2}→{angerGaugeClamp:F2} Max:{maxAngerGauge}");

        // 減少速度を最小値にリセット.
        float decayMin = currentAngerState == AngerState.Normal
            ? normalDecayRateMin
            : angryDecayRateMin;
        currentDecayRate = decayMin;
        decayRecoveryTimer = 0f;
        isDecayRecovering = true;
    }

    // =========================================================
    // ダメージから怒りゲージ増加量を計算 (派生クラスでオーバーライド可能).
    // =========================================================
    protected virtual float CalculateAngerIncrease(float damage)
    {
        return damage;
    }

    // =========================================================
    // 怒り状態遷移.
    // =========================================================
    protected virtual void EnterAngerState()
    {
        currentAngerState = AngerState.Angry;
        Debug.Log($"[AngerGauge] ★怒り状態開始★ - {gameObject.name} Current:{angerGaugeCurrent:F2} Clamp:{angerGaugeClamp:F2} コールバック登録:{onAngerStateChanged != null}");
        onAngerStateChanged?.Invoke(AngerState.Angry);
    }

    protected virtual void ExitAngerState()
    {
        currentAngerState = AngerState.Normal;
        angerGaugeCurrent = 0f;
        angerGaugeClamp = 0f;
        Debug.Log($"[AngerGauge] ★怒り状態解除★ - {gameObject.name} ゲージリセット コールバック登録:{onAngerStateChanged != null}");
        onAngerStateChanged?.Invoke(AngerState.Normal);
    }

    // =========================================================
    // Unity Update - 怒りゲージ更新.
    // =========================================================
    protected virtual void Update()
    {
        UpdateAngerGauge(Time.deltaTime);
    }
}


public enum ChangeHPtype
{
    None,
    Damage,
    Heal
}

public interface ChangeHP
{
    public ChangeHPtype changeHPtype { get; }
    public UniTask OnHPChange(EnemyStatus_abstract nowstatus, float hp);
}

public class ChangeHP_Damage_Default : ChangeHP
{
    public ChangeHPtype changeHPtype { get; private set; } = ChangeHPtype.Damage;

    public UniTask OnHPChange(EnemyStatus_abstract nowstatus, float damage)
    {
        Debug.Log($"[ChangeHP_Damage_Default] OnHPChange - Damage: {damage}, 現在HP: {nowstatus.hp.Value}");
        if (damage <= 0)
        {
            Debug.Log($"[ChangeHP_Damage_Default] ダメージ0以下のためスキップ");
            return UniTask.CompletedTask;
        }
        float oldHp = nowstatus.hp.Value;
        nowstatus.hp.Value = Mathf.Clamp(nowstatus.hp.Value - damage, 0, nowstatus.maxhp);
        Debug.Log($"[ChangeHP_Damage_Default] HP変更: {oldHp} -> {nowstatus.hp.Value}");
        return UniTask.CompletedTask;
    }
}

public class ChangeHP_Heal_Default : ChangeHP
{
    public ChangeHPtype changeHPtype { get; private set; } = ChangeHPtype.Heal;

    public UniTask OnHPChange(EnemyStatus_abstract nowstatus, float heal)
    {
        Debug.Log($"[ChangeHP_Heal_Default] OnHPChange - Heal: {heal}, 現在HP: {nowstatus.hp.Value}");
        if (heal <= 0)
        {
            Debug.Log($"[ChangeHP_Heal_Default] 回復0以下のためスキップ");
            return UniTask.CompletedTask;
        }
        float oldHp = nowstatus.hp.Value;
        nowstatus.hp.Value = Mathf.Clamp(nowstatus.hp.Value + heal, 0, nowstatus.maxhp);
        Debug.Log($"[ChangeHP_Heal_Default] HP変更: {oldHp} -> {nowstatus.hp.Value}");
        return UniTask.CompletedTask;
    }
}
