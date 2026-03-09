using UnityEngine;
using InGame.Common;
using InGame.Player;

// Wendig用 Howlingのヒット処理.
public class EnemColliderState_Wendig_Howling : EnemColliderState_PlayerDamage
{
    public EnemColliderState_Wendig_Howling()
    {
        // ダメージはAct時に動的に設定される (ハウリング: 1.8倍).
        damage = 90;
        // ハウリングの吹き飛ばし力は10倍.
        knockbackForce = 10f;
        // パリィ・ガード貫通.
        powerlevel = PowerlevelConst.EnemyHowling;
    }

    protected override void OnHit(GameObject target, Collider2D hitCollider)
    {
        Debug.Log($"[EnemColliderState_Wendig_Howling] OnHit - Target: {target.name}, Damage: {damage}");
        GuardState guardState = DamagePlayer(target, damage);
    }
}
