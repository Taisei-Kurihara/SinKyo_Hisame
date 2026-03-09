using UnityEngine;
using InGame.Player;

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
