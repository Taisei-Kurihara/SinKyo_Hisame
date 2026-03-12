using UnityEngine;
using Common;

/// <summary>
/// カメラ制限用のステージ端マーカー.
/// シーン上に4つのTransformを配置してステージの上下左右端を定義する.
/// Awake時に値をキャッシュし、不要なTransformを削除する.
/// </summary>
public class StageBoundsMarker : SingletonMonoBase<StageBoundsMarker>
{
    [Header("上")]
    [SerializeField] private Transform top;
    [Header("下")]
    [SerializeField] private Transform bottom;
    [Header("左")]
    [SerializeField] private Transform left;
    [Header("右")]
    [SerializeField] private Transform right;

    [Header("設定")]
    [SerializeField] private bool destroyMarkersAfterRead = true;

    // フォールバック値.
    private const float DefaultTop = 3f;
    private const float DefaultBottom = -5f;
    private const float DefaultLeft = -13f;
    private const float DefaultRight = 13f;

    // キャッシュ値.
    private float cachedTopY;
    private float cachedBottomY;
    private float cachedLeftX;
    private float cachedRightX;

    // 自動生成ではなくシーン配置のインスタンスのみ返す.
    public static StageBoundsMarker Current => instance;

    public float TopY => cachedTopY;
    public float BottomY => cachedBottomY;
    public float LeftX => cachedLeftX;
    public float RightX => cachedRightX;

    private void Awake()
    {
        instance = this;

        // Transform から値を読み取りキャッシュ.
        cachedTopY = top != null ? top.position.y : DefaultTop;
        cachedBottomY = bottom != null ? bottom.position.y : DefaultBottom;
        cachedLeftX = left != null ? left.position.x : DefaultLeft;
        cachedRightX = right != null ? right.position.x : DefaultRight;

        // 不要なマーカーTransformを削除.
        if (destroyMarkersAfterRead)
        {
            if (top != null) Destroy(top.gameObject);
            if (bottom != null) Destroy(bottom.gameObject);
            if (left != null) Destroy(left.gameObject);
            if (right != null) Destroy(right.gameObject);
        }
    }
}
