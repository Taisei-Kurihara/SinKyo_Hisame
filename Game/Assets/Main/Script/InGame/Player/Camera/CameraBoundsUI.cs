using UnityEngine;
using Common;

/// <summary>
/// Canvas上のカメラ制限UIマーカー.
/// 4つのRectTransform (Image付き) でカメラ制限のスクリーン位置を定義する.
/// 各辺の Inner Edge がステージ端と一致するようにカメラが制限される.
/// Awake時にViewport Inset値をキャッシュし、不要なマーカーを削除する.
/// </summary>
public class CameraBoundsUI : SingletonMonoBase<CameraBoundsUI>
{
    [System.Serializable]
    public class SideConfig
    {
        public bool enabled = true;
        public RectTransform marker;
    }

    [Header("上")]
    [SerializeField] private SideConfig top = new SideConfig();
    [Header("下")]
    [SerializeField] private SideConfig bottom = new SideConfig();
    [Header("左")]
    [SerializeField] private SideConfig left = new SideConfig();
    [Header("右")]
    [SerializeField] private SideConfig right = new SideConfig();

    [Header("設定")]
    [SerializeField] private bool destroyMarkersAfterRead = true;

    // キャッシュ値.
    private bool cachedTopEnabled;
    private bool cachedBottomEnabled;
    private bool cachedLeftEnabled;
    private bool cachedRightEnabled;
    private float cachedTopInnerViewportY;
    private float cachedBottomInnerViewportY;
    private float cachedLeftInnerViewportX;
    private float cachedRightInnerViewportX;

    // Inner Edge 計算用ワーク配列.
    private Vector3[] corners = new Vector3[4];

    // 自動生成ではなくシーン配置のインスタンスのみ返す.
    public static CameraBoundsUI Current => instance;

    public bool IsTopEnabled => cachedTopEnabled;
    public bool IsBottomEnabled => cachedBottomEnabled;
    public bool IsLeftEnabled => cachedLeftEnabled;
    public bool IsRightEnabled => cachedRightEnabled;

    public float GetTopInnerViewportY() => cachedTopInnerViewportY;
    public float GetBottomInnerViewportY() => cachedBottomInnerViewportY;
    public float GetLeftInnerViewportX() => cachedLeftInnerViewportX;
    public float GetRightInnerViewportX() => cachedRightInnerViewportX;

    private void Awake()
    {
        instance = this;

        // 有効フラグをキャッシュ.
        cachedTopEnabled = top.enabled && top.marker != null;
        cachedBottomEnabled = bottom.enabled && bottom.marker != null;
        cachedLeftEnabled = left.enabled && left.marker != null;
        cachedRightEnabled = right.enabled && right.marker != null;

        // RectTransform の World Corners から Viewport Inset をキャッシュ.
        // corners: [0]=左下, [1]=左上, [2]=右上, [3]=右下.
        if (cachedTopEnabled)
        {
            top.marker.GetWorldCorners(corners);
            cachedTopInnerViewportY = corners[0].y / Screen.height;
        }
        else
        {
            cachedTopInnerViewportY = 1f;
        }

        if (cachedBottomEnabled)
        {
            bottom.marker.GetWorldCorners(corners);
            cachedBottomInnerViewportY = corners[1].y / Screen.height;
        }
        else
        {
            cachedBottomInnerViewportY = 0f;
        }

        if (cachedLeftEnabled)
        {
            left.marker.GetWorldCorners(corners);
            cachedLeftInnerViewportX = corners[2].x / Screen.width;
        }
        else
        {
            cachedLeftInnerViewportX = 0f;
        }

        if (cachedRightEnabled)
        {
            right.marker.GetWorldCorners(corners);
            cachedRightInnerViewportX = corners[0].x / Screen.width;
        }
        else
        {
            cachedRightInnerViewportX = 1f;
        }

        // 不要なマーカーを削除.
        if (destroyMarkersAfterRead)
        {
            if (top.marker != null) Destroy(top.marker.gameObject);
            if (bottom.marker != null) Destroy(bottom.marker.gameObject);
            if (left.marker != null) Destroy(left.marker.gameObject);
            if (right.marker != null) Destroy(right.marker.gameObject);
        }
    }
}
