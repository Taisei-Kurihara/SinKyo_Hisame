using UnityEngine;

namespace InGame
{
    /// <summary>
    /// 血痕エフェクト.
    /// 地形Hit位置に出現し、法線方向に回転、Animatorによるスプライトアニメーション再生→プール返却.
    /// アニメーションクリップは Loop Time = false に設定すること.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Animator))]
    public class BloodSplatter : MonoBehaviour
    {
        // --- ランダム角度 ---
        [Header("Rotation")]
        [Tooltip("法線回転に加えるランダム角度幅（±）")]
        [SerializeField] private float randomAngleRange = 5f;

        // --- 参照 ---
        [Header("References")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Animator animator;

        private void Awake()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
            if (animator == null)
                animator = GetComponent<Animator>();
        }

        /// <summary>
        /// BloodSplatterPoolから呼ばれる初期化.
        /// </summary>
        /// <param name="direction">DamageCounterの進行方向.</param>
        /// <param name="normal">地形の法線方向.</param>
        public void Initialize(Vector2 direction, Vector2 normal)
        {
            // アニメーションをフレーム0からリセット（プール再利用時の継続防止）.
            if (animator != null)
            {
                animator.Rebind();
                animator.Update(0f);
                // "Play" トリガーでアニメーション開始.
                animator.SetTrigger("Play");
            }

            // アルファリセット.
            if (spriteRenderer != null)
            {
                Color c = spriteRenderer.color;
                c.a = 1f;
                spriteRenderer.color = c;
            }

            // スケールリセット.
            transform.localScale = Vector3.one;

            // 角度は常に (0,0,0) 固定.
            transform.rotation = Quaternion.identity;

            // hitPointを上端として、sprite高さ分だけ下にオフセット.
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                float halfHeight = spriteRenderer.bounds.extents.y;
                transform.position -= new Vector3(0f, halfHeight, 0f);
            }

            // 親子付けしない（グラウンドコライダーのスケール影響を回避）.
            // size常に (1,1,1) 固定.
            transform.localScale = Vector3.one;
        }

        private void Update()
        {
            if (animator == null) return;

            // アニメーション終了検出 → プール返却.
            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.normalizedTime >= 1f && !animator.IsInTransition(0))
            {
                ReturnToPool();
            }
        }

        /// <summary>
        /// プールに返却.
        /// </summary>
        private void ReturnToPool()
        {
            transform.localScale = Vector3.one;
            transform.rotation = Quaternion.identity;

            gameObject.SetActive(false);
        }
    }
}
