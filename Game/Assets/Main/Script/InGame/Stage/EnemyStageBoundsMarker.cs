using UnityEngine;
using Common;

// Enemy移動制限用のステージ端マーカー.
// カメラ制限とは独立してEnemy専用の移動範囲を定義する.
public class EnemyStageBoundsMarker : SingletonMonoBase<EnemyStageBoundsMarker>
{
    [SerializeField] private Transform left;
    [SerializeField] private Transform right;
    [SerializeField] private Transform top;
    [SerializeField] private Transform bottom;

    // フォールバック値.
    private static readonly Vector2 defaultMin = new Vector2(-13f, -5f);
    private static readonly Vector2 defaultMax = new Vector2(13f, 0.5f);

    public Vector2 StageMin => new Vector2(
        left != null ? left.position.x : defaultMin.x,
        bottom != null ? bottom.position.y : defaultMin.y);

    public Vector2 StageMax => new Vector2(
        right != null ? right.position.x : defaultMax.x,
        top != null ? top.position.y : defaultMax.y);

    private void Awake()
    {
        instance = this;
    }
}
