using UnityEngine;

// 向き制御の共通ヘルパー.
public static class EnemFacingHelper
{
    // 指定したターゲットの方向を向かせる（2D左右反転）.
    public static void FaceToward(Transform ownerTransform, Vector2 targetPos)
    {
        Vector2 currentPos = ownerTransform.position;
        Vector2 direction = targetPos - currentPos;
        if (direction.x != 0)
        {
            ownerTransform.localScale = new Vector3(
                -Mathf.Sign(direction.x) * Mathf.Abs(ownerTransform.localScale.x),
                ownerTransform.localScale.y,
                ownerTransform.localScale.z
            );
        }
    }

    // 指定した方向に向かせる（移動方向を見る場合）.
    public static void FaceDirection(Transform ownerTransform, float directionX)
    {
        if (directionX != 0)
        {
            ownerTransform.localScale = new Vector3(
                -Mathf.Sign(directionX) * Mathf.Abs(ownerTransform.localScale.x),
                ownerTransform.localScale.y,
                ownerTransform.localScale.z
            );
        }
    }
}
