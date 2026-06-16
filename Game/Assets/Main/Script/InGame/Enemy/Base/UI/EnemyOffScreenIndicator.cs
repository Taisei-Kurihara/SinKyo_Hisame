using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Enemy 画面外インジケーター.
/// 敵が画面外にいる時に画面端に��印を表示し、方向と距離を示す.
/// Canvas 上の UI として動作する.
/// </summary>
public class EnemyOffScreenIndicator : MonoBehaviour
{
    [Header("UI参照")]
    [Tooltip("矢印画像")]
    [SerializeField] private Image arrowImage;

    [Tooltip("距離テキスト")]
    [SerializeField] private TextMeshProUGUI distanceText;

    [Header("設定")]
    [Tooltip("画面端からのパディング（ピクセル）")]
    [SerializeField] private float edgePadding = 50f;

    [Tooltip("距離テキストを表示するか")]
    [SerializeField] private bool showDistance = true;

    [Tooltip("追跡対象のY軸オフセット（足元から上方向）")]
    [SerializeField] private float targetYOffset = 1.5f;

    // 追跡対象の敵Transform.
    private Transform targetEnemy;
    private Camera mainCamera;
    private RectTransform canvasRect;
    private RectTransform rectTransform;

    // 不明モード（方向をランダム化、距離を「?m」に表示）.
    private bool isUnknownMode = false;
    private float unknownModeTimer = 0f;
    private const float unknownModeInterval = 0.8f;
    private Vector2 unknownDirection = Vector2.right;

    /// <summary>
    /// インジケーターを初期化.
    /// </summary>
    /// <param name="enemy">追跡対象の敵Transform.</param>
    /// <param name="canvas">親Canvas.</param>
    public void Initialize(Transform enemy, Canvas canvas)
    {
        targetEnemy = enemy;
        mainCamera = Camera.main;
        canvasRect = canvas.GetComponent<RectTransform>();
        rectTransform = GetComponent<RectTransform>();

        // 初期状態は非表示.
        SetVisible(false);
    }

    /// <summary>
    /// 不明モード: 方向をランダム化し、距離を「?m」に表示.
    /// MeteorDrop等で敵の位置を隠す際に使用.
    /// </summary>
    public void SetUnknownMode(bool enabled)
    {
        isUnknownMode = enabled;
        if (enabled)
        {
            unknownModeTimer = unknownModeInterval; // 即座に初回ランダム方向を決定.
        }
    }

    private void LateUpdate()
    {
        if (mainCamera == null || canvasRect == null)
        {
            SetVisible(false);
            return;
        }

        // 不明モード: ランダム方向+「?m」表示.
        if (isUnknownMode)
        {
            UpdateUnknownMode();
            return;
        }

        if (targetEnemy == null)
        {
            SetVisible(false);
            return;
        }

        // 足元ではなく胴体あたりを指すようにオフセット.
        Vector3 targetPos = targetEnemy.position + Vector3.up * targetYOffset;
        Vector3 viewportPos = mainCamera.WorldToViewportPoint(targetPos);

        // カメラの後ろにいる場合は反転.
        if (viewportPos.z < 0)
        {
            viewportPos.x = 1f - viewportPos.x;
            viewportPos.y = 1f - viewportPos.y;
        }

        // 画面内判定（0〜1の範囲内なら画面内）.
        bool isOnScreen = viewportPos.x >= 0f && viewportPos.x <= 1f &&
                          viewportPos.y >= 0f && viewportPos.y <= 1f &&
                          viewportPos.z > 0f;

        if (isOnScreen)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);

        // 画面中心からのベクトルを計算.
        Vector2 screenCenter = new Vector2(0.5f, 0.5f);
        Vector2 direction = new Vector2(viewportPos.x - 0.5f, viewportPos.y - 0.5f).normalized;

        // 画面端にクランプ.
        Vector2 canvasSize = canvasRect.sizeDelta;
        float halfWidth = canvasSize.x * 0.5f - edgePadding;
        float halfHeight = canvasSize.y * 0.5f - edgePadding;

        // direction ���基づいて画面端の位置を計算.
        float absX = Mathf.Abs(direction.x);
        float absY = Mathf.Abs(direction.y);
        float scale;

        if (absX * halfHeight > absY * halfWidth)
        {
            // 左右の端にヒット.
            scale = halfWidth / absX;
        }
        else
        {
            // 上下の端にヒット.
            scale = halfHeight / absY;
        }

        Vector2 indicatorPos = direction * scale;
        rectTransform.anchoredPosition = indicatorPos;

        // 矢印の回転（方向に向ける）.
        if (arrowImage != null)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            arrowImage.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
        }

        // 距離表示.
        if (showDistance && distanceText != null)
        {
            float distance = Vector2.Distance(
                mainCamera.transform.position,
                targetPos);
            distanceText.text = $"{distance:F0}m";
        }
    }

    /// <summary>不明モード更新: ランダム方向に矢印を表示し、距離を「?m」に.</summary>
    private void UpdateUnknownMode()
    {
        SetVisible(true);

        unknownModeTimer += Time.deltaTime;
        if (unknownModeTimer >= unknownModeInterval)
        {
            unknownModeTimer = 0f;
            float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            unknownDirection = new Vector2(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle));
        }

        // 画面端にクランプ.
        Vector2 canvasSize = canvasRect.sizeDelta;
        float halfWidth = canvasSize.x * 0.5f - edgePadding;
        float halfHeight = canvasSize.y * 0.5f - edgePadding;

        float absX = Mathf.Abs(unknownDirection.x);
        float absY = Mathf.Abs(unknownDirection.y);
        float scale;

        if (absX * halfHeight > absY * halfWidth)
        {
            scale = absX > 0.001f ? halfWidth / absX : halfHeight / absY;
        }
        else
        {
            scale = absY > 0.001f ? halfHeight / absY : halfWidth / absX;
        }

        rectTransform.anchoredPosition = unknownDirection * scale;

        if (arrowImage != null)
        {
            float angle = Mathf.Atan2(unknownDirection.y, unknownDirection.x) * Mathf.Rad2Deg;
            arrowImage.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
        }

        if (distanceText != null)
        {
            distanceText.text = "?m";
        }
    }

    private void SetVisible(bool visible)
    {
        if (arrowImage != null) arrowImage.enabled = visible;
        if (distanceText != null) distanceText.enabled = visible;
    }

    /// <summary>
    /// 追跡対象を変更.
    /// </summary>
    public void SetTarget(Transform enemy)
    {
        targetEnemy = enemy;
    }

    private void OnDestroy()
    {
        targetEnemy = null;
    }
}
