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

        // Howlingは円形攻撃 — ノックバック方向は敵からの相対位置で決定（向き依存ではない）.
        float knockbackDirX = 1f;
        if (attackerTransform != null && target != null)
        {
            float relativeX = target.transform.position.x - attackerTransform.position.x;
            knockbackDirX = relativeX >= 0 ? 1f : -1f;
        }

        var playerScope = target.GetComponent<PlayerScope>();
        if (playerScope == null)
        {
            Debug.Log($"[EnemColliderState_Wendig_Howling] PlayerScopeが見つからない: {target.name}");
            return;
        }

        var damageData = new DamageData(damage, powerlevel, knockbackForce, knockbackDirX);
        GuardState guardState = playerScope.OnReceiveAttack(damageData);
        Debug.Log($"[EnemColliderState_Wendig_Howling] ダメージ処理結果 ガード状態={guardState}, 方向={knockbackDirX}");
    }
}
