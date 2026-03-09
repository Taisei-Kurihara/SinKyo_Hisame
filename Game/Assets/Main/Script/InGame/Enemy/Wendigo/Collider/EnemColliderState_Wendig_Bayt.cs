using UnityEngine;
using InGame.Player;

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
