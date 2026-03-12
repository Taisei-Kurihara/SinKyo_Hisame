using UnityEngine;
using Common;

/// <summary>
/// Enemy移動制限用のステージ端マーカー.
/// カメラ制限とは独立してEnemy専用の移動範囲を定義する.
/// Awake時に値をキャッシュし、不要なTransformを削除する.
/// </summary>
public class EnemyStageBoundsMarker : SingletonMonoBase<EnemyStageBoundsMarker>
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
    private static readonly Vector2 defaultMin = new Vector2(-13f, -5f);
    private static readonly Vector2 defaultMax = new Vector2(13f, 0.5f);

    // キャッシュ値.
    private Vector2 cachedStageMin;
    private Vector2 cachedStageMax;

    // 自動生成ではなくシーン配置のインスタンスのみ返す.
    public static EnemyStageBoundsMarker Current => instance;

    public Vector2 StageMin => cachedStageMin;
    public Vector2 StageMax => cachedStageMax;

    private void Awake()
    {
        instance = this;

        // Transform から値を読み取りキャッシュ.
        cachedStageMin = new Vector2(
            left != null ? left.position.x : defaultMin.x,
            bottom != null ? bottom.position.y : defaultMin.y);
        cachedStageMax = new Vector2(
            right != null ? right.position.x : defaultMax.x,
            top != null ? top.position.y : defaultMax.y);

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
