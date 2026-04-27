using System.Collections.Generic;
using UnityEngine;
using InGame.Player;

/// <summary>
/// Zan（斬撃エフェクト）の当たり判定コントローラー.
/// スプライトの見た目通りの PolygonCollider2D を使用し、
/// パルスゲージに連動してスケールを変更する.
/// </summary>
public class ZanHitController : MonoBehaviour
{
    private PolygonCollider2D polyCollider;
    private SpriteRenderer spriteRenderer;
    private Vector3 baseScale;
    private ContactFilter2D enemyFilter;

    // OverlapCollider 結果バッファ.
    private List<Collider2D> overlapResults = new List<Collider2D>(16);

    // 攻撃タイプ別カラー.
    private static readonly Color iaiColor = new Color(0.3f, 0.7f, 0.7f, 1f);
    private static readonly Color defaultAttackColor = new Color(1f, 0.1f, 0f, 1f);

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        // PolygonCollider2D を追加（スプライトから自動生成）.
        polyCollider = GetComponent<PolygonCollider2D>();
        if (polyCollider == null)
        {
            polyCollider = gameObject.AddComponent<PolygonCollider2D>();
        }
        polyCollider.isTrigger = true;
        polyCollider.enabled = false;

        // 初期スケールを保持.
        baseScale = transform.localScale;

        // Enemy レイヤーフィルター設定.
        enemyFilter = new ContactFilter2D();
        enemyFilter.SetLayerMask(LayerMask.GetMask("Enemy"));
        enemyFilter.useLayerMask = true;
        enemyFilter.useTriggers = false;

        // 初期状態: α=0 + 非アクティブ.
        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = 0f;
            spriteRenderer.color = c;
        }
        gameObject.SetActive(false);
    }

    /// <summary>
    /// パルスゲージと攻撃タイプに応じてスケールを更新.
    /// Iai: 0→0.5x, 99→1.0x
    /// Normal: 100→1.0x, 180→2.0x
    /// Weak: Normal と同じ × 0.75
    /// </summary>
    public void UpdateScale(float pulse, PlayerAttackType attackType = PlayerAttackType.None)
    {
        float scaleFactor;

        if (attackType == PlayerAttackType.Iai)
        {
            // Iai: 0→0.5x, 99→1.0x.
            float clampedPulse = Mathf.Clamp(pulse, 0f, 99f);
            scaleFactor = Mathf.Lerp(0.5f, 1.0f, clampedPulse / 99f);
        }
        else if (attackType == PlayerAttackType.Weak)
        {
            // Weak: 100→0.75x, 180→1.25x.
            if (pulse <= 100f)
            {
                scaleFactor = 0.75f;
            }
            else if (pulse <= 180f)
            {
                scaleFactor = Mathf.Lerp(0.75f, 1.25f, (pulse - 100f) / 80f);
            }
            else
            {
                scaleFactor = 1.25f;
            }
        }
        else
        {
            // Normal / None: 100→1.0x, 180→1.5x.
            if (pulse <= 100f)
            {
                scaleFactor = 1.0f;
            }
            else if (pulse <= 180f)
            {
                scaleFactor = Mathf.Lerp(1.0f, 1.5f, (pulse - 100f) / 80f);
            }
            else
            {
                scaleFactor = 1.5f;
            }
        }

        transform.localScale = baseScale * scaleFactor;
    }

    /// <summary>
    /// Zan を表示して当たり判定を有効化.
    /// </summary>
    public void Show(bool isIai)
    {
        gameObject.SetActive(true);

        // 攻撃タイプに応じてカラー設定.
        if (spriteRenderer != null)
        {
            spriteRenderer.color = isIai ? iaiColor : defaultAttackColor;
        }

        polyCollider.enabled = true;
    }

    /// <summary>
    /// Zan を非表示にして当たり判定を無効化.
    /// </summary>
    public void Hide()
    {
        polyCollider.enabled = false;
        // α=0 にしてから非アクティブ.
        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = 0f;
            spriteRenderer.color = c;
        }
        gameObject.SetActive(false);
    }

    /// <summary>
    /// PolygonCollider2D でEnemyレイヤーとの重なりを検出.
    /// </summary>
    /// <returns>重なっているコライダーのリスト.</returns>
    public List<Collider2D> DetectEnemies()
    {
        overlapResults.Clear();
        if (polyCollider.enabled)
        {
            Physics2D.OverlapCollider(polyCollider, enemyFilter, overlapResults);
        }
        return overlapResults;
    }
}
