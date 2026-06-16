using UnityEngine;

namespace InGame
{
    /// <summary>
    /// 血痕エフェクト.
    /// 地形Hit位置に出現し、法線方向に回転、Y展開→フェード→プール返却.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class BloodSplatter : MonoBehaviour
    {
        // --- スプライト設定 ---
        [Header("Sprites")]
        [Tooltip("ランダムに選択される血痕画像群")]
        [SerializeField] private Sprite[] splatterSprites;

        // --- タイミング設定 ---
        [Header("Timing")]
        [Tooltip("Y展開にかかる時間（秒）")]
        [SerializeField] private float expandDuration = 0.3f;
        [Tooltip("表示維持時間（秒）")]
        [SerializeField] private float displayDuration = 1.0f;
        [Tooltip("フェードにかかる時間（秒）")]
        [SerializeField] private float fadeDuration = 0.5f;

        // --- ランダム角度 ---
        [Header("Rotation")]
        [Tooltip("法線回転に加えるランダム角度幅（±）")]
        [SerializeField] private float randomAngleRange = 5f;

        // --- 参照 ---
        [Header("References")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        // --- ランタイム ---
        private float elapsed;
        private float totalLifetime;
        private Color baseColor;

        // フェーズ管理.
        private enum Phase { Expand, Display, Fade }
        private Phase currentPhase;

        private void Awake()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            totalLifetime = expandDuration + displayDuration + fadeDuration;
        }

        /// <summary>
        /// BloodSplatterPoolから呼ばれる初期化.
        /// </summary>
        /// <param name="direction">DamageCounterの進行方向.</param>
        /// <param name="normal">地形の法線方向.</param>
        /// <param name="parentTarget">親子付け対象.</param>
        public void Initialize(Vector2 direction, Vector2 normal, Transform parentTarget)
        {
            elapsed = 0f;
            currentPhase = Phase.Expand;

            // ランダムスプライト選択.
            if (splatterSprites != null && splatterSprites.Length > 0)
            {
                spriteRenderer.sprite = splatterSprites[Random.Range(0, splatterSprites.Length)];
            }

            // アルファリセット.
            baseColor = spriteRenderer.color;
            baseColor.a = 1f;
            spriteRenderer.color = baseColor;

            // Y方向スケール0で開始.
            transform.localScale = new Vector3(transform.localScale.x, 0f, transform.localScale.z);

            // DamageCounterの進行方向に基づく回転.
            // 進行方向をZ軸回転角に変換 + ランダム角加算.
            float dirAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float randomOffset = Random.Range(-randomAngleRange, randomAngleRange);
            transform.rotation = Quaternion.Euler(0f, 0f, dirAngle + randomOffset);

            // hit対象に親子付け.
            if (parentTarget != null)
            {
                transform.SetParent(parentTarget);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            elapsed += dt;

            switch (currentPhase)
            {
                case Phase.Expand:
                    UpdateExpand();
                    break;
                case Phase.Display:
                    UpdateDisplay();
                    break;
                case Phase.Fade:
                    UpdateFade();
                    break;
            }
        }

        /// <summary>
        /// Y展開フェーズ: localScale.y を 0→1.
        /// </summary>
        private void UpdateExpand()
        {
            float t = Mathf.Clamp01(elapsed / expandDuration);
            var scale = transform.localScale;
            scale.y = t;
            transform.localScale = scale;

            if (elapsed >= expandDuration)
            {
                currentPhase = Phase.Display;
            }
        }

        /// <summary>
        /// 表示維持フェーズ.
        /// </summary>
        private void UpdateDisplay()
        {
            if (elapsed >= expandDuration + displayDuration)
            {
                currentPhase = Phase.Fade;
            }
        }

        /// <summary>
        /// フェードフェーズ: alpha 1→0.
        /// </summary>
        private void UpdateFade()
        {
            float fadeElapsed = elapsed - expandDuration - displayDuration;
            float t = Mathf.Clamp01(fadeElapsed / fadeDuration);
            float alpha = 1f - t;

            Color c = baseColor;
            c.a = alpha;
            spriteRenderer.color = c;

            if (t >= 1f)
            {
                ReturnToPool();
            }
        }

        /// <summary>
        /// プールに返却.
        /// </summary>
        private void ReturnToPool()
        {
            // 親子付け解除 → プールのtransformに戻す.
            var poolTransform = BloodSplatterPool.Instance(false)?.transform;
            if (poolTransform != null)
            {
                transform.SetParent(poolTransform);
            }

            // スケールリセット.
            transform.localScale = Vector3.one;
            transform.rotation = Quaternion.identity;

            gameObject.SetActive(false);
        }
    }
}
