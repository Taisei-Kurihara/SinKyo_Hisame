using UnityEngine;
using InGame.Common;
using InGame.Player;

// Wendig用 突進のヒット処理.
public class EnemColliderState_Wendig_Rush : EnemColliderState_PlayerDamage
{
    public EnemColliderState_Wendig_Rush()
    {
        // ダメージはAct時に動的に設定される (前進突進: 2.25倍).
        damage = 113;
        // 突進の吹き飛ばし力は10倍.
        knockbackForce = 10f;
        // パリィ・ガード貫通.
        powerlevel = PowerlevelConst.EnemyRush;
    }

    protected override void OnHit(GameObject target, Collider2D hitCollider)
    {
        //Debug.Log($"[EnemColliderState_Wendig_Rush] OnHit - Target: {target.name}, Damage: {damage}");
        GuardState guardState = DamagePlayer(target, damage);
        //Debug.Log($"[EnemColliderState_Wendig_Rush] ダメージ結果 - GuardState: {guardState}");
    }
}
