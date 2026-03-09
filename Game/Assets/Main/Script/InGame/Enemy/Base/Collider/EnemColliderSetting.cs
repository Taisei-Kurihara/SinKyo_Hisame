using UnityEngine;

/// <summary> 個別コライダーの設定 </summary>
[System.Serializable]
public class EnemColliderSetting
{
    // コライダーの種類.
    public EnemColliderType colliderType = EnemColliderType.Box;

    // 位置オフセット.
    public Vector2 offset = Vector2.zero;

    // サイズ（BoxとCapsule用）.
    private Vector2 _size = new Vector2(1f, 1f);
    public Vector2 size
    {
        get => _size;
        set
        {
            Debug.Log($"[EnemColliderSetting] size設定: {value}");
            _size = value;
        }
    }

    // 半径（Circle用）.
    public float radius = 0.5f;

    // カプセルの方向（Capsule用）.
    public CapsuleDirection2D capsuleDirection = CapsuleDirection2D.Vertical;

    // 明示的にサイズを設定するコンストラクタ.
    public EnemColliderSetting() { }

    public EnemColliderSetting(EnemColliderType type, Vector2 offset, Vector2 size)
    {
        this.colliderType = type;
        this.offset = offset;
        this._size = size;
        Debug.Log($"[EnemColliderSetting] コンストラクタ - Type: {type}, Offset: {offset}, Size: {size}");
    }
}
