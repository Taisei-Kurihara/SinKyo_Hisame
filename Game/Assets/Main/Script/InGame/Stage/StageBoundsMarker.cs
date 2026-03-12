using UnityEngine;
using Common;

// カメラ制限用のステージ端マーカー.
// シーン上に4つのTransformを配置してステージの上下左右端を定義する.
public class StageBoundsMarker : SingletonMonoBase<StageBoundsMarker>
{
    [SerializeField] private Transform left;
    [SerializeField] private Transform right;
    [SerializeField] private Transform top;
    [SerializeField] private Transform bottom;

    // フォールバック値.
    private const float DefaultLeft = -13f;
    private const float DefaultRight = 13f;
    private const float DefaultTop = 3f;
    private const float DefaultBottom = -5f;

    public float LeftX => left != null ? left.position.x : DefaultLeft;
    public float RightX => right != null ? right.position.x : DefaultRight;
    public float TopY => top != null ? top.position.y : DefaultTop;
    public float BottomY => bottom != null ? bottom.position.y : DefaultBottom;

    private void Awake()
    {
        instance = this;
    }
}
