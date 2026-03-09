using UnityEngine;

// プレイヤーダメージ用の基本クラス.
public class EnemColliderState_PlayerDamage : EnemColliderState_abstract
{
    protected override void OnHit(GameObject target, Collider2D hitCollider)
    {
        Debug.Log($"[EnemColliderState_PlayerDamage] OnHit - Target: {target.name}, Damage: {damage}");
        DamagePlayer(target, damage);
    }
}
