using UnityEngine;
using System.Collections.Generic;
using InGame.Player;
using InGame.Common;

/// <summary> 命中時の処理 </summary>
public abstract class EnemColliderState_abstract
{
    // ヒット済みの対象を記録（同一対象への重複処理を防ぐ）.
    protected HashSet<GameObject> hitTargets = new HashSet<GameObject>();

    // コライダーステータス.
    protected EnemColliderStatus colliderStatus;

    // ダメージ量.
    protected int damage = 10;

    // 吹き飛ばしの力（howlingと突進は10倍、それ以外は3倍）.
    protected float knockbackForce = 3f;

    // 攻撃のPowerlevel（デフォルトは通常近接攻撃）.
    protected int powerlevel = PowerlevelConst.EnemyMeleeAttack;

    // 攻撃者のTransform（エネミーの向き判定用）.
    protected Transform attackerTransform;

    // 攻撃者のTransformを設定.
    public void SetAttackerTransform(Transform t)
    {
        attackerTransform = t;
    }

    // コライダーステータスを設定.
    public void SetColliderStatus(EnemColliderStatus status)
    {
        Debug.Log($"[EnemColliderState_abstract] SetColliderStatus - Damage: {status.damage}, ColliderCount: {status.colliderSettings.Count}");
        colliderStatus = status;
        damage = (int)status.damage;
    }

    // ダメージを設定.
    public void SetDamage(int dmg)
    {
        damage = dmg;
    }

    // ヒット対象リストをクリア（攻撃開始時に呼び出す）.
    public void ClearHitTargets()
    {
        Debug.Log($"[EnemColliderState_abstract] ClearHitTargets - 以前のヒット数: {hitTargets.Count}");
        hitTargets.Clear();
    }

    // ヒット処理（当たり判定のいずれかに当たった時に呼ばれる）.
    public bool TryProcessHit(GameObject target, Collider2D hitCollider)
    {
        if (target == null)
        {
            Debug.LogWarning($"[EnemColliderState_abstract] TryProcessHit - target が null");
            return false;
        }

        // 既にヒット済みの対象はスキップ.
        if (hitTargets.Contains(target))
        {
            Debug.Log($"[EnemColliderState_abstract] TryProcessHit - {target.name} は既にヒット済み、スキップ");
            return false;
        }

        // ヒット済みリストに追加.
        hitTargets.Add(target);
        Debug.Log($"[EnemColliderState_abstract] TryProcessHit - {target.name} をヒット済みリストに追加");

        // 実際のヒット処理を実行.
        OnHit(target, hitCollider);
        return true;
    }

    // 継承クラスで実際のヒット処理を実装.
    protected virtual void OnHit(GameObject target, Collider2D hitCollider)
    {
        Debug.Log($"[EnemColliderState_abstract] OnHit - Target: {target.name}, Collider: {hitCollider.name}");
    }

    // プレイヤーにダメージを与える共通処理.
    protected GuardState DamagePlayer(GameObject target, int attackDamage)
    {
        var playerScope = target.GetComponent<PlayerScope>();
        if (playerScope == null)
        {
            Debug.Log($"[EnemColliderState_abstract] DamagePlayer - PlayerScopeが見つからない: {target.name}");
            return GuardState.None;
        }

        Debug.Log($"[EnemColliderState_abstract] DamagePlayer - プレイヤーヒット確認 name={target.name}");

        // ガード状態を事前に確認.
        bool isGuarding = playerScope.playerControllModel != null;
        Debug.Log($"[EnemColliderState_abstract] DamagePlayer - プレイヤー状態確認 playerControllModel存在={isGuarding}");

        // エネミーが向いている方向を算出.
        float knockbackDirX = 1f;
        if (attackerTransform != null)
        {
            knockbackDirX = attackerTransform.localScale.x >= 0 ? -1f : 1f;
        }

        // ダメージ処理（DamageDataに吹き飛ばし力と方向を含めて渡す）.
        var damageData = new DamageData(attackDamage, powerlevel, knockbackForce, knockbackDirX);
        GuardState guardState = playerScope.OnReceiveAttack(damageData);
        Debug.Log($"[EnemColliderState_abstract] DamagePlayer - ダメージ処理結果 ガード状態={guardState}, 与ダメージ={attackDamage}, 吹き飛ばし力={knockbackForce}, 方向={knockbackDirX}");

        // 実際のダメージ量を計算.
        int actualDamage = guardState == GuardState.Parry ? 0 : (guardState == GuardState.Guard ? attackDamage / 2 : attackDamage);
        Debug.Log($"[EnemColliderState_abstract] DamagePlayer - 実ダメージ={actualDamage}");

        return guardState;
    }
}
