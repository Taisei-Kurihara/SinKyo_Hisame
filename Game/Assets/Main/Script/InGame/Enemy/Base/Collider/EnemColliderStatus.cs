using UnityEngine;
using System.Collections.Generic;

/// <summary> 攻撃全体のコライダー設定（複数コライダーを一度のヒットとして扱う） </summary>
public class EnemColliderStatus
{
    // 親Transform（コライダーを追加する対象）.
    public Transform parentTransform;

    //(既)修:コライダーのサイズ設定が行われるようにしてください
    // 個別コライダー設定のリスト.
    public List<EnemColliderSetting> colliderSettings = new List<EnemColliderSetting>();

    // 当たり判定の持続時間.
    public float duration = 0.5f;

    // ダメージ量.
    public float damage = 10f;

    // 生成されたコライダーのリスト.
    private List<Collider2D> createdColliders = new List<Collider2D>();

    // コライダーを生成（親に直接追加）.
    public List<Collider2D> CreateColliders()
    {
        if (parentTransform == null)
        {
            Debug.LogWarning($"[EnemColliderStatus] CreateColliders - parentTransform が null");
            return createdColliders;
        }

        Debug.Log($"[EnemColliderStatus] CreateColliders - 設定数: {colliderSettings.Count}, Parent: {parentTransform.name}");

        foreach (var setting in colliderSettings)
        {
            Debug.Log($"[EnemColliderStatus] Setting処理 - Type: {setting.colliderType}, Size: {setting.size}, Offset: {setting.offset}");

            Collider2D collider = null;

            switch (setting.colliderType)
            {
                case EnemColliderType.Box:
                    var boxCollider = parentTransform.gameObject.AddComponent<BoxCollider2D>();
                    boxCollider.offset = setting.offset;
                    boxCollider.size = setting.size;
                    boxCollider.isTrigger = true;
                    collider = boxCollider;
                    Debug.Log($"[EnemColliderStatus] BoxCollider2D追加完了 - Size: {boxCollider.size}, Offset: {boxCollider.offset}, isTrigger: {boxCollider.isTrigger}");
                    break;

                case EnemColliderType.Circle:
                    var circleCollider = parentTransform.gameObject.AddComponent<CircleCollider2D>();
                    circleCollider.offset = setting.offset;
                    circleCollider.radius = setting.radius;
                    circleCollider.isTrigger = true;
                    collider = circleCollider;
                    Debug.Log($"[EnemColliderStatus] CircleCollider2D追加 - Offset: {setting.offset}, Radius: {setting.radius}");
                    break;

                case EnemColliderType.Capsule:
                    var capsuleCollider = parentTransform.gameObject.AddComponent<CapsuleCollider2D>();
                    capsuleCollider.offset = setting.offset;
                    capsuleCollider.size = setting.size;
                    capsuleCollider.direction = setting.capsuleDirection;
                    capsuleCollider.isTrigger = true;
                    collider = capsuleCollider;
                    Debug.Log($"[EnemColliderStatus] CapsuleCollider2D追加 - Offset: {setting.offset}, Size: {setting.size}");
                    break;
            }

            if (collider != null)
            {
                collider.hideFlags = HideFlags.HideAndDontSave;
                createdColliders.Add(collider);
                Debug.Log($"[EnemColliderStatus] コライダー追加成功 - 総数: {createdColliders.Count}");
            }
        }

        Debug.Log($"[EnemColliderStatus] CreateColliders完了 - 生成数: {createdColliders.Count}");
        return createdColliders;
    }

    // 生成したコライダーを削除.
    public void DestroyColliders()
    {
        Debug.Log($"[EnemColliderStatus] DestroyColliders - 削除数: {createdColliders.Count}");

        foreach (var collider in createdColliders)
        {
            if (collider != null)
            {
                Object.Destroy(collider);
            }
        }
        createdColliders.Clear();
    }

    // 指定されたコライダーがこのステータスで生成されたものかチェック.
    public bool ContainsCollider(Collider2D collider)
    {
        return createdColliders.Contains(collider);
    }
}
