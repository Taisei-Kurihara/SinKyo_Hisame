using UnityEngine;
using Common;

// Canvas上のカメラ制限UIマーカー.
// 4つのRectTransform (Image付き) でカメラ制限のスクリーン位置を定義する.
// 各辺の Inner Edge がステージ端と一致するようにカメラが制限される.
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

    // Inner Edge 用のワーク配列.
    private Vector3[] corners = new Vector3[4];

    private void Awake()
    {
        instance = this;
    }

    public bool IsTopEnabled => top.enabled && top.marker != null;
    public bool IsBottomEnabled => bottom.enabled && bottom.marker != null;
    public bool IsLeftEnabled => left.enabled && left.marker != null;
    public bool IsRightEnabled => right.enabled && right.marker != null;

    // Bottom の Inner Edge (上端) の Viewport Y を返す.
    // RectTransform の corners: [0]=左下, [1]=左上, [2]=右上, [3]=右下.
    public float GetBottomInnerViewportY()
    {
        if (!IsBottomEnabled) return 0f;
        bottom.marker.GetWorldCorners(corners);
        return corners[1].y / Screen.height;
    }

    // Top の Inner Edge (下端) の Viewport Y を返す.
    public float GetTopInnerViewportY()
    {
        if (!IsTopEnabled) return 1f;
        top.marker.GetWorldCorners(corners);
        return corners[0].y / Screen.height;
    }

    // Left の Inner Edge (右端) の Viewport X を返す.
    public float GetLeftInnerViewportX()
    {
        if (!IsLeftEnabled) return 0f;
        left.marker.GetWorldCorners(corners);
        return corners[2].x / Screen.width;
    }

    // Right の Inner Edge (左端) の Viewport X を返す.
    public float GetRightInnerViewportX()
    {
        if (!IsRightEnabled) return 1f;
        right.marker.GetWorldCorners(corners);
        return corners[0].x / Screen.width;
    }
}
