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

        // 被弾バウンスエフェクト（プレイヤーの位置から攻撃方向を算出）.
        var hitBounce = GetComponent<EnemyHitBounce>();
        if (hitBounce != null && damage > 0)
        {
            float attackDirX = 1f;
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                attackDirX = Mathf.Sign(player.transform.position.x - transform.position.x);
            }
            hitBounce.Bounce(attackDirX);
        }

        Debug.Log($"[EnemyStatus_abstract] OnDamaged完了 - 残りHP: {hp.Value}");
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
        Debug.Log($"[EnemyStatus_abstract] DeadAnim完了");
        // Destroy処理はDeathManagerに移譲.
    }

    protected virtual UniTask DeadAnim()
    {
        Debug.Log($"[EnemyStatus_abstract] DeadAnim - {gameObject.name}");
        return UniTask.CompletedTask;
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
