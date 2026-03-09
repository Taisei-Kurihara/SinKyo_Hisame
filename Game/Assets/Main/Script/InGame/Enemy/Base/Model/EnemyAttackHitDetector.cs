using UnityEngine;
using System.Collections.Generic;

// 敵攻撃のヒット検出用コンポーネント.
public class EnemyAttackHitDetector : MonoBehaviour
{
    private EnemColliderState_abstract colliderState;
    private List<Collider2D> targetColliders;

    public void Initialize(EnemColliderState_abstract state, List<Collider2D> colliders)
    {
        // ランタイム専用コンポーネントとしてフラグ設定.
        hideFlags = HideFlags.HideAndDontSave;
        colliderState = state;
        targetColliders = colliders;
        //Debug.Log($"[EnemyAttackHitDetector] Initialize - Collider数: {colliders.Count}");
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 対象のコライダーからのヒットかチェック.
        if (colliderState == null || targetColliders == null) return;

        // プレイヤータグをチェック.
        if (!other.CompareTag("Player")) return;

        //Debug.Log($"[EnemyAttackHitDetector] OnTriggerEnter2D - {other.gameObject.name}");

        // 攻撃者のTransformを設定（エネミーの向き判定用）.
        colliderState.SetAttackerTransform(transform);

        // ヒット処理を実行（重複処理はcolliderStateが防ぐ）.
        colliderState.TryProcessHit(other.gameObject, other);
    }
}
