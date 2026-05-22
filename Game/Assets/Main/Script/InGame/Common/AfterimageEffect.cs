using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 残像エフェクト.
/// SpriteRenderer のスプライトを使って、移動中に残像を残す.
/// StartEffect() / StopEffect() で制御する.
/// </summary>
public class AfterimageEffect : MonoBehaviour
{
    [Header("残像設定")]
    [Tooltip("最大残像数")]
    [SerializeField] private int maxAfterimages = 30;

    [Tooltip("残像の生成間隔（秒）")]
    [SerializeField] private float spawnInterval = 0.006f;

    [Tooltip("残像のフェード持続時間（秒）")]
    [SerializeField] private float fadeDuration = 0.5f;

    [Tooltip("残像の色")]
    [SerializeField] private Color afterimageColor = new Color(0.5f, 0.7f, 1f, 0.6f);

    [Tooltip("残像のSortingOrder オフセット（元のスプライトの背後に描画）")]
    [SerializeField] private int sortingOrderOffset = -1;

    // 残像スケール倍率（1.0 = 本体と同じ）.
    private float scaleMultiplier = 1f;

    // 残像のプール.
    private List<AfterimageInstance> pool = new List<AfterimageInstance>();
    private SpriteRenderer sourceSpriteRenderer;
    private bool isEffectActive = false;
    private float spawnTimer = 0f;

    // ソースから取得したマテリアルキャッシュ.
    private Material cachedMaterial;

    private class AfterimageInstance
    {
        public GameObject gameObject;
        public SpriteRenderer spriteRenderer;
        public float elapsed;
        public float fadeDuration;
        public Color startColor;
        public bool active;
    }

    private void Awake()
    {
        sourceSpriteRenderer = FindValidSpriteRenderer();
    }

    /// <summary>
    /// スプライトが設定されている SpriteRenderer を優先的に検索.
    /// </summary>
    private SpriteRenderer FindValidSpriteRenderer()
    {
        var renderers = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in renderers)
        {
            if (sr.sprite != null) return sr;
        }
        return renderers.Length > 0 ? renderers[0] : null;
    }

    /// <summary>
    /// 残像用マテリアルをキャッシュする.
    /// Resources/AfterimageUnlit (URP Sprite-Unlit-Default) を優先.
    /// 見つからない場合はソースのマテリアルを使用.
    /// </summary>
    private void CacheMaterial()
    {
        if (cachedMaterial != null) return;

        // URP Unlitマテリアルを Resources から読み込み（ビルドでも確実に動作）.
        cachedMaterial = Resources.Load<Material>("AfterimageUnlit");
        if (cachedMaterial != null) return;

        // フォールバック: ソースのマテリアルを使用.
        if (sourceSpriteRenderer == null) return;
        cachedMaterial = sourceSpriteRenderer.sharedMaterial;
    }

    /// <summary>
    /// 残像の色を外部から設定.
    /// </summary>
    public void SetColor(Color color)
    {
        afterimageColor = color;
    }

    /// <summary>
    /// 残像のフェード持続時間を外部から設定.
    /// </summary>
    public void SetFadeDuration(float duration)
    {
        fadeDuration = duration;
    }

    /// <summary>
    /// 残像の生成間隔を外部から設定.
    /// </summary>
    public void SetSpawnInterval(float interval)
    {
        spawnInterval = interval;
    }

    /// <summary>
    /// 残像のスケール倍率を外部から設定（1.0 = 本体と同じ）.
    /// </summary>
    public void SetScaleMultiplier(float multiplier)
    {
        scaleMultiplier = multiplier;
    }

    /// <summary>
    /// SpriteRendererを外部から設定.
    /// </summary>
    public void SetSourceRenderer(SpriteRenderer renderer)
    {
        sourceSpriteRenderer = renderer;
    }

    /// <summary>
    /// 残像エフェクトを開始.
    /// </summary>
    public void StartEffect()
    {
        if (sourceSpriteRenderer == null || sourceSpriteRenderer.sprite == null)
        {
            sourceSpriteRenderer = FindValidSpriteRenderer();
        }
        if (sourceSpriteRenderer == null)
        {
            return;
        }
        CacheMaterial();
        isEffectActive = true;
        spawnTimer = 0f;
    }

    /// <summary>
    /// 残像エフェクトを停止（既存の残像はフェードアウトして消える）.
    /// </summary>
    public void StopEffect()
    {
        isEffectActive = false;
    }

    private void Update()
    {
        if (isEffectActive && sourceSpriteRenderer != null)
        {
            spawnTimer += Time.deltaTime;
            if (spawnTimer >= spawnInterval)
            {
                spawnTimer -= spawnInterval;
                SpawnAfterimage();
            }
        }

        for (int i = pool.Count - 1; i >= 0; i--)
        {
            var instance = pool[i];
            if (!instance.active) continue;

            instance.elapsed += Time.deltaTime;
            float t = instance.elapsed / instance.fadeDuration;

            if (t >= 1f)
            {
                instance.active = false;
                instance.gameObject.SetActive(false);
            }
            else
            {
                Color c = instance.startColor;
                c.a = Mathf.Lerp(instance.startColor.a, 0f, t);
                instance.spriteRenderer.color = c;
            }
        }
    }

    private void SpawnAfterimage()
    {
        if (sourceSpriteRenderer == null || sourceSpriteRenderer.sprite == null)
        {
            sourceSpriteRenderer = FindValidSpriteRenderer();
            if (sourceSpriteRenderer == null || sourceSpriteRenderer.sprite == null) return;
        }

        AfterimageInstance instance = null;
        foreach (var item in pool)
        {
            if (!item.active)
            {
                instance = item;
                break;
            }
        }

        if (instance == null)
        {
            if (pool.Count >= maxAfterimages)
            {
                instance = pool[0];
            }
            else
            {
                instance = CreateInstance();
                pool.Add(instance);
            }
        }

        instance.gameObject.SetActive(true);
        instance.active = true;
        instance.elapsed = 0f;
        instance.fadeDuration = fadeDuration;
        instance.startColor = afterimageColor;

        instance.spriteRenderer.sprite = sourceSpriteRenderer.sprite;
        instance.spriteRenderer.flipX = sourceSpriteRenderer.flipX;
        instance.spriteRenderer.flipY = sourceSpriteRenderer.flipY;
        instance.spriteRenderer.sortingLayerID = sourceSpriteRenderer.sortingLayerID;
        instance.spriteRenderer.sortingOrder = sourceSpriteRenderer.sortingOrder + sortingOrderOffset;
        instance.spriteRenderer.color = afterimageColor;

        // ソースと同じマテリアルを適用.
        if (cachedMaterial != null)
        {
            instance.spriteRenderer.sharedMaterial = cachedMaterial;
        }

        // レイヤーをオーナーに合わせる.
        instance.gameObject.layer = gameObject.layer;

        instance.gameObject.transform.position = sourceSpriteRenderer.transform.position;
        instance.gameObject.transform.rotation = sourceSpriteRenderer.transform.rotation;
        instance.gameObject.transform.localScale = sourceSpriteRenderer.transform.lossyScale * scaleMultiplier;
    }

    private AfterimageInstance CreateInstance()
    {
        var go = new GameObject("Afterimage");
        var sr = go.AddComponent<SpriteRenderer>();
        return new AfterimageInstance
        {
            gameObject = go,
            spriteRenderer = sr,
            active = false
        };
    }

    private void OnDestroy()
    {
        foreach (var instance in pool)
        {
            if (instance.gameObject != null)
            {
                Destroy(instance.gameObject);
            }
        }
        pool.Clear();
    }
}
