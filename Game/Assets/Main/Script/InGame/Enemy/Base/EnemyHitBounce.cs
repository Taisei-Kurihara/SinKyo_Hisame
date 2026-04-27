using UnityEngine;
using Cysharp.Threading.Tasks;

/// <summary>
/// 被弾時にスプライトだけが跳ね返るエフェクト.
/// サンドバッグが押されて元に戻るような動き.
/// 攻撃方向と逆にスプライトが移動し、放物線で元の位置に戻る.
/// </summary>
public class EnemyHitBounce : MonoBehaviour
{
    [Header("バウンス設定")]
    [Tooltip("バウンス対象のTransform（未設定時は子のSpriteRendererを自動検索）")]
    [SerializeField] private Transform spriteRoot;

    [Tooltip("バウンスの最大距離")]
    [SerializeField] private float bounceDistance = 0.2f;

    [Tooltip("バウンスの持続時間（秒）")]
    [SerializeField] private float bounceDuration = 0.25f;

    [Tooltip("持続時間の何%が経過するまで次のバウンスを受け付けないか（0.0〜1.0）")]
    [SerializeField] private float bounceIgnorePercent = 0.7f;

    private Vector3 originalLocalPos;
    private bool isBouncing = false;
    private float bounceElapsed = 0f;

    private void Awake()
    {
        FindSpriteRoot();
    }

    /// <summary>
    /// spriteRootを自動検索（子のSpriteRendererを探す）.
    /// </summary>
    private void FindSpriteRoot()
    {
        if (spriteRoot != null)
        {
            originalLocalPos = spriteRoot.localPosition;
            return;
        }

        // 子オブジェクトのSpriteRendererを検索（自身を除く）.
        var renderers = GetComponentsInChildren<SpriteRenderer>();
        foreach (var sr in renderers)
        {
            if (sr.transform != transform)
            {
                spriteRoot = sr.transform;
                originalLocalPos = spriteRoot.localPosition;
                return;
            }
        }

        // 子がない場合は自身のSpriteRendererを使用.
        var selfRenderer = GetComponent<SpriteRenderer>();
        if (selfRenderer != null)
        {
            spriteRoot = transform;
            originalLocalPos = Vector3.zero;
        }
    }

    /// <summary>
    /// spriteRootを外部から設定.
    /// </summary>
    public void SetSpriteRoot(Transform root)
    {
        spriteRoot = root;
        if (spriteRoot != null)
        {
            originalLocalPos = spriteRoot.localPosition;
        }
    }

    /// <summary>
    /// バウンス距離を設定.
    /// </summary>
    public void SetBounceDistance(float distance)
    {
        bounceDistance = distance;
    }

    /// <summary>
    /// バウンスを実行.
    /// 攻撃方向と逆にスプライトが移動し、放物線で元の位置に戻る.
    /// </summary>
    /// <param name="attackDirectionX">攻撃が来た方向（正=右から、負=左から）.</param>
    public void Bounce(float attackDirectionX)
    {
        if (spriteRoot == null) return;

        // バウンス中かつ持続時間のN%未満なら無視（多段ヒット対策）.
        if (isBouncing && bounceElapsed < bounceDuration * bounceIgnorePercent)
        {
            return;
        }

        BounceAsync(attackDirectionX).Forget();
    }

    private async UniTaskVoid BounceAsync(float attackDirectionX)
    {
        // 既にバウンス中なら即座にリセットして再開始.
        if (isBouncing && spriteRoot != null)
        {
            spriteRoot.localPosition = originalLocalPos;
        }

        isBouncing = true;
        bounceElapsed = 0f;

        // 攻撃方向と逆にスプライトが飛ぶ.
        float moveDir = attackDirectionX >= 0 ? -1f : 1f;

        while (bounceElapsed < bounceDuration)
        {
            if (spriteRoot == null) break;

            float t = bounceElapsed / bounceDuration;
            // 放物線: 4t(1-t) → t=0で0、t=0.5で最大、t=1で0に戻る.
            float parabola = 4f * t * (1f - t);
            float offset = bounceDistance * parabola;

            spriteRoot.localPosition = originalLocalPos + new Vector3(moveDir * offset, 0f, 0f);
            bounceElapsed += Time.deltaTime;
            await UniTask.Yield();
        }

        // 元の位置に確実に戻す.
        if (spriteRoot != null)
        {
            spriteRoot.localPosition = originalLocalPos;
        }
        isBouncing = false;
    }
}
