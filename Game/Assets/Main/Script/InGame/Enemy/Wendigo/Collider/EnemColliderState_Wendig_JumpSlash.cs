using UnityEngine;
using InGame.Common;
using InGame.Player;

// Wendig用 とびかかり切りのヒット処理.
public class EnemColliderState_Wendig_JumpSlash : EnemColliderState_PlayerDamage
{
    public EnemColliderState_Wendig_JumpSlash()
    {
        // ダメージはAct時に動的に設定される.
        damage = 75;
        // 中程度の吹き飛ばし.
        knockbackForce = 5f;
        // 通常近接攻撃相当.
        powerlevel = PowerlevelConst.EnemyMeleeAttack;
    }

    protected override void OnHit(GameObject target, Collider2D hitCollider)
    {
        GuardState guardState = DamagePlayer(target, damage);
    }
}
