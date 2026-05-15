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
    [SerializeField] private int maxAfterimages = 5;

    [Tooltip("残像の生成間隔（秒）")]
    [SerializeField] private float spawnInterval = 0.04f;

    [Tooltip("残像のフェード持続時間（秒）")]
    [SerializeField] private float fadeDuration = 0.3f;

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
        // スプライトが設定されているものを優先.
        foreach (var sr in renderers)
        {
            if (sr.sprite != null) return sr;
        }
        // なければ最初のものを返す.
        return renderers.Length > 0 ? renderers[0] : null;
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
            Debug.LogWarning($"[AfterimageEffect] SpriteRenderer が見つかりません: {gameObject.name}");
            return;
        }
        Debug.Log($"[AfterimageEffect] StartEffect - source: {sourceSpriteRenderer.gameObject.name}");
        isEffectActive = true;
        spawnTimer = 0f;
    }

    /// <summary>
    /// 残像エフェクトを停止（既存の残像はフェードアウトして消える）.
    /// </summary>
    public void StopEffect()
    {
        isEffectActive = false;
        // パラメータをデフォルトに戻す.
        scaleMultiplier = 1f;
        fadeDuration = 0.3f;
        spawnInterval = 0.04f;
    }

    private void Update()
    {
        // 残像生成.
        if (isEffectActive && sourceSpriteRenderer != null)
        {
            spawnTimer += Time.deltaTime;
            if (spawnTimer >= spawnInterval)
            {
                spawnTimer -= spawnInterval;
                SpawnAfterimage();
            }
        }

        // 残像更新.
        for (int i = pool.Count - 1; i >= 0; i--)
        {
            var instance = pool[i];
            if (!instance.active) continue;

            instance.elapsed += Time.deltaTime;
            float t = instance.elapsed / instance.fadeDuration;

            if (t >= 1f)
            {
                // フェード完了 → 非表示.
                instance.active = false;
                instance.gameObject.SetActive(false);
            }
            else
            {
                // アルファフェード.
                Color c = instance.startColor;
                c.a = Mathf.Lerp(instance.startColor.a, 0f, t);
                instance.spriteRenderer.color = c;
            }
        }
    }

    private void SpawnAfterimage()
    {
        // プールから非アクティブなインスタンスを取得、なければ新規作成.
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
            // プール上限チェック.
            if (pool.Count >= maxAfterimages)
            {
                // 最も古いアクティブなものを再利用.
                instance = pool[0];
            }
            else
            {
                instance = CreateInstance();
                pool.Add(instance);
            }
        }

        // 残像の設定.
        instance.gameObject.SetActive(true);
        instance.active = true;
        instance.elapsed = 0f;
        instance.fadeDuration = fadeDuration;
        instance.startColor = afterimageColor;

        // スプライト情報をコピー.
        instance.spriteRenderer.sprite = sourceSpriteRenderer.sprite;
        instance.spriteRenderer.color = afterimageColor;
        instance.spriteRenderer.flipX = sourceSpriteRenderer.flipX;
        instance.spriteRenderer.flipY = sourceSpriteRenderer.flipY;
        instance.spriteRenderer.sortingLayerID = sourceSpriteRenderer.sortingLayerID;
        instance.spriteRenderer.sortingOrder = sourceSpriteRenderer.sortingOrder + sortingOrderOffset;

        // 位置・回転・スケールをコピー（スケール倍率を適用）.
        instance.gameObject.transform.position = sourceSpriteRenderer.transform.position;
        instance.gameObject.transform.rotation = sourceSpriteRenderer.transform.rotation;
        instance.gameObject.transform.localScale = sourceSpriteRenderer.transform.lossyScale * scaleMultiplier;
    }

    private AfterimageInstance CreateInstance()
    {
        var go = new GameObject("Afterimage");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.material = new Material(sourceSpriteRenderer.sharedMaterial);

        return new AfterimageInstance
        {
            gameObject = go,
            spriteRenderer = sr,
            active = false
        };
    }

    private void OnDestroy()
    {
        // プールを破棄.
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
