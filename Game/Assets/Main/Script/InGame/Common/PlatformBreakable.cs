using UnityEngine;
using Cysharp.Threading.Tasks;

namespace InGame.Common
{
    /// <summary>
    /// EnemyレイヤーのオブジェクトがPlatformの範囲内にいる場合にPlatformを一時的に無効化するコンポーネント.
    /// EdgeCollider2DではOnTriggerEnter2Dが発火しないため、FixedUpdate + Physics2D.OverlapAreaで手動検出.
    /// 無効化中は自身と子オブジェクトのレイヤーをDefaultに変更し、Z軸回転で傾けつつスプライトを透明化、一定時間後に復帰する.
    /// </summary>
    public class PlatformBreakable : MonoBehaviour
    {
        private float tiltAngle = 30f;
        private float tiltDuration = 0.5f;
        private float breakDuration = 3f;
        private float restoreDuration = 0.5f;

        private int enemyLayerMask = 0;
        private Collider2D platformCollider;
        private bool isBreaking = false;

        // レイヤー復元用.
        private int[] originalLayers;

        // スプライト透明度制御用.
        private SpriteRenderer[] spriteRenderers;

        // 元の回転を保存（eulerAnglesは0-360範囲で返すためQuaternionで保持）.
        private Quaternion originalRotation;

        private void Awake()
        {
            enemyLayerMask = 1 << LayerMask.NameToLayer("Enemy");
            platformCollider = GetComponentInChildren<Collider2D>();
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
            originalRotation = transform.rotation;
        }

        private void FixedUpdate()
        {
            if (platformCollider == null || isBreaking) return;

            Bounds bounds = platformCollider.bounds;

            // EdgeCollider2Dのboundsは薄いので、Y方向に拡張して検出しやすくする.
            Vector2 min = new Vector2(bounds.min.x, bounds.min.y - 1f);
            Vector2 max = new Vector2(bounds.max.x, bounds.max.y + 1f);

            Collider2D hit = Physics2D.OverlapArea(min, max, enemyLayerMask);
            if (hit != null)
            {
                // Enemyの衝突位置から傾き方向を決定.
                float tiltDir = hit.transform.position.x > transform.position.x ? -1f : 1f;
                BreakSequenceAsync(tiltDir).Forget();
            }
        }

        private async UniTaskVoid BreakSequenceAsync(float tiltDir)
        {
            isBreaking = true;
            var ct = this.GetCancellationTokenOnDestroy();

            // 自身と子オブジェクトのレイヤーをDefaultに変更.
            SetLayerRecursive(gameObject, LayerMask.NameToLayer("Default"));

            // PlatformEffector2Dを無効化.
            var effector = GetComponentInChildren<PlatformEffector2D>();
            if (effector != null)
            {
                effector.enabled = false;
            }

            // Z軸回転 + スプライト透明化: 傾けながら透明にする.
            Quaternion tiltTarget = originalRotation * Quaternion.Euler(0f, 0f, tiltAngle * tiltDir);
            float elapsed = 0f;

            while (elapsed < tiltDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / tiltDuration);
                transform.rotation = Quaternion.Slerp(originalRotation, tiltTarget, t);
                SetSpritesAlpha(1f - t);
                await UniTask.Yield(ct);
            }
            transform.rotation = tiltTarget;
            SetSpritesAlpha(0f);

            // 無効化期間.
            await UniTask.Delay((int)(breakDuration * 1000), cancellationToken: ct);

            // Z軸回転 + スプライト不透明化: 復帰しながら不透明にする.
            elapsed = 0f;
            while (elapsed < restoreDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / restoreDuration);
                transform.rotation = Quaternion.Slerp(tiltTarget, originalRotation, t);
                SetSpritesAlpha(t);
                await UniTask.Yield(ct);
            }
            transform.rotation = originalRotation;
            SetSpritesAlpha(1f);

            // レイヤー復元.
            RestoreLayerRecursive(gameObject);

            // PlatformEffector2D復帰.
            if (effector != null)
            {
                effector.enabled = true;
            }

            isBreaking = false;
        }

        // 子オブジェクトのSpriteRendererの透明度を設定.
        private void SetSpritesAlpha(float alpha)
        {
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] == null) continue;
                Color c = spriteRenderers[i].color;
                c.a = alpha;
                spriteRenderers[i].color = c;
            }
        }

        // 自身と子オブジェクトのレイヤーを変更（元のレイヤーを保存）.
        private void SetLayerRecursive(GameObject obj, int newLayer)
        {
            Transform[] allTransforms = obj.GetComponentsInChildren<Transform>(true);
            originalLayers = new int[allTransforms.Length];

            for (int i = 0; i < allTransforms.Length; i++)
            {
                originalLayers[i] = allTransforms[i].gameObject.layer;
                allTransforms[i].gameObject.layer = newLayer;
            }
        }

        // 保存した元のレイヤーに復元.
        private void RestoreLayerRecursive(GameObject obj)
        {
            if (originalLayers == null) return;

            Transform[] allTransforms = obj.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < allTransforms.Length && i < originalLayers.Length; i++)
            {
                allTransforms[i].gameObject.layer = originalLayers[i];
            }
            originalLayers = null;
        }
    }
}
