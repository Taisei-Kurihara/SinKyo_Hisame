using UnityEngine;

// EnemColliderState_abstract を継承した None クラス.
public class EnemColliderState_None : EnemColliderState_abstract
{
    protected override void OnHit(GameObject target, Collider2D hitCollider)
    {
        // 何もしない.
        Debug.Log($"[EnemColliderState_None] OnHit - Target: {target.name} (処理なし)");
    }
}
