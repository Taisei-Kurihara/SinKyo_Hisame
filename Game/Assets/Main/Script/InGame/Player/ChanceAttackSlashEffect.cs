using Cysharp.Threading.Tasks;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace InGame.Player
{
    /// <summary>
    /// チャンス必殺技の切断エフェクト.
    /// スプライトを開始地点（ローカルY=0）から終了地点までY方向に引き伸ばし、
    /// Xスケールを 0 → maxX → 0 でアニメーションさせる.
    ///
    /// 使い方: ChanceAttackSlashEffect.Spawn(from, to) を呼ぶ.
    ///
    /// Addressables 登録名: "ChanceAttackSlash"（Sprite アセット）
    ///   ・スプライト単体を登録すること（プレハブ不要）.
    ///   ・ピボット: 下中央 (0.5, 0).
    ///   ・PPU は任意（スクリプトが自動でスケール計算する）.
    ///   ・未登録の場合は 1×1 白ピクセルスプライトでフォールバック.
    /// </summary>
    public class ChanceAttackSlashEffect : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;

        /// <summary>スプライト1ユニットあたりの自然サイズ（高さ）.ロード後に自動設定.</summary>
        [SerializeField] private float spriteNaturalHeight = 1f;

        /// <summary>Xスケールの最大値（スラッシュの幅）.</summary>
        [SerializeField] private float maxScaleX = 0.25f;

        /// <summary>アニメーション総時間（秒）.</summary>
        [SerializeField] private float duration = 0.25f;

        // ==========================================
        // Addressables スプライトキャッシュ（静的: 初回ロード後は保持）.
        //   Addressables キー: "ChanceAttackSlash"
        // ==========================================

        /// <summary>Addressables に登録するスプライトのキー名.</summary>
        public const string SpriteAddress = "ChanceAttackSlash";

        private static Sprite _cachedSprite;
        private static AsyncOperationHandle<Sprite> _spriteHandle;

        // 複数インスタンスが同時に Spawn されても1回しかロードしないための同期源.
        private static UniTaskCompletionSource<Sprite> _loadSource;

        // ロード失敗 / 未登録時のフォールバック（1×1 白ピクセル）.
        private static Sprite _fallbackSprite;

        // ==========================================

        /// <summary>
        /// 静的ファクトリ: 位置・方向・長さを指定してエフェクトを生成する（静的・瞬間表示）.
        /// </summary>
        public static void Spawn(Vector3 from, Vector3 to)
        {
            var go = new GameObject("ChanceSlashEffect");
            var effect = go.AddComponent<ChanceAttackSlashEffect>();

            var sr = go.AddComponent<SpriteRenderer>();
            effect.spriteRenderer = sr;
            sr.sortingLayerName = "Default";
            sr.sortingOrder = 10;

            effect.PlayAsync(from, to).Forget();
        }

        /// <summary>
        /// 静的ファクトリ: 開始地点を固定し、<paramref name="trackTarget"/> を毎フレーム追跡して伸びるエフェクトを生成.
        /// LitMotion 移動中に呼び出し、プレイヤーが動くにつれてスラッシュが延びていく.
        /// </summary>
        /// <param name="from">スラッシュの起点（開始位置）.</param>
        /// <param name="trackTarget">追跡するトランスフォーム（プレイヤー）.</param>
        /// <param name="duration">エフェクトの総表示時間（移動時間に合わせること）.</param>
        public static void SpawnTracking(Vector3 from, Transform trackTarget, float duration)
        {
            var go = new GameObject("ChanceSlashEffect");
            var effect = go.AddComponent<ChanceAttackSlashEffect>();

            var sr = go.AddComponent<SpriteRenderer>();
            effect.spriteRenderer = sr;
            sr.sortingLayerName = "Default";
            sr.sortingOrder = 10;

            effect.duration = duration;
            effect.PlayTrackingAsync(from, trackTarget).Forget();
        }

        private async UniTaskVoid PlayAsync(Vector3 from, Vector3 to)
        {
            if (spriteRenderer == null) { Destroy(gameObject); return; }

            // --- Addressables からスプライトをロード（初回のみ; 以降はキャッシュを返す）---
            Sprite sprite = await GetOrLoadSpriteAsync();

            // ロード中に GameObject が破棄された場合は中断.
            if (this == null || spriteRenderer == null) return;

            spriteRenderer.sprite = sprite;
            spriteRenderer.color  = new Color(1f, 1f, 1f, 0.85f);

            // スプライトの実サイズからスケール計算基準を設定（PPU に依らず正確な高さを使用）.
            if (sprite != null && sprite.pixelsPerUnit > 0f)
                spriteNaturalHeight = sprite.rect.height / sprite.pixelsPerUnit;

            // --- 位置・回転 ---
            // 開始地点に置き、ローカルY方向が from→to を向くよう回転.
            transform.position = from;
            Vector3 delta  = to - from;
            float distance = delta.magnitude;
            if (distance < 0.01f) { Destroy(gameObject); return; }

            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            // --- Yスケール: 距離をスプライト高さで割って伸ばす ---
            float scaleY = distance / Mathf.Max(spriteNaturalHeight, 0.001f);

            // --- Xアニメーション: 0 → maxScaleX → 0 ---
            var token   = this.GetCancellationTokenOnDestroy();
            float elapsed = 0f;

            try
            {
                while (elapsed < duration)
                {
                    float t      = elapsed / duration;
                    // 0→1→0 のパラボラ: 4*t*(1-t)
                    float xScale = maxScaleX * 4f * t * (1f - t);
                    transform.localScale = new Vector3(xScale, scaleY, 1f);

                    elapsed += Time.deltaTime;
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            catch (OperationCanceledException) { }

            Destroy(gameObject);
        }

        /// <summary>
        /// 追跡モード: <paramref name="from"/> を起点に、毎フレーム <paramref name="trackTarget"/> の位置まで
        /// スプライトを伸ばし続ける. Xスケールは 0 → maxScaleX → 0 のパラボラでアニメーション.
        /// </summary>
        private async UniTaskVoid PlayTrackingAsync(Vector3 from, Transform trackTarget)
        {
            if (spriteRenderer == null) { Destroy(gameObject); return; }

            Sprite sprite = await GetOrLoadSpriteAsync();
            if (this == null || spriteRenderer == null) return;

            spriteRenderer.sprite = sprite;
            spriteRenderer.color  = new Color(1f, 1f, 1f, 0.85f);

            if (sprite != null && sprite.pixelsPerUnit > 0f)
                spriteNaturalHeight = sprite.rect.height / sprite.pixelsPerUnit;

            // 起点に配置（毎フレーム to 側だけ更新するため position は固定）.
            transform.position = from;

            var token   = this.GetCancellationTokenOnDestroy();
            float elapsed = 0f;

            try
            {
                while (elapsed < duration)
                {
                    // 追跡先が消えた場合は最後の位置で止める.
                    Vector3 to = trackTarget != null ? trackTarget.position : (from + (Vector3)(Vector2.up * 0.01f));

                    Vector3 delta    = to - from;
                    float   distance = delta.magnitude;

                    if (distance > 0.01f)
                    {
                        // 方向・長さを毎フレーム更新.
                        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 90f;
                        transform.rotation = Quaternion.Euler(0f, 0f, angle);
                        float scaleY = distance / Mathf.Max(spriteNaturalHeight, 0.001f);

                        float t      = elapsed / duration;
                        float xScale = maxScaleX * 4f * t * (1f - t);
                        transform.localScale = new Vector3(xScale, scaleY, 1f);
                    }

                    elapsed += Time.deltaTime;
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            catch (OperationCanceledException) { }

            Destroy(gameObject);
        }

        /// <summary>
        /// キャッシュ済みスプライトを返す。未ロードなら Addressables からロードして返す.
        /// 複数インスタンスが同時に呼んでもロードは1回のみ.
        /// </summary>
        private static async UniTask<Sprite> GetOrLoadSpriteAsync()
        {
            // キャッシュ済みならすぐ返す.
            if (_cachedSprite != null) return _cachedSprite;

            // 初回 → ロード開始.
            if (_loadSource == null)
            {
                _loadSource = new UniTaskCompletionSource<Sprite>();
                LoadSpriteAsync(_loadSource).Forget();
            }

            // 2回目以降は同じ Task を await（完了済みなら即返る）.
            return await _loadSource.Task;
        }

        private static async UniTaskVoid LoadSpriteAsync(UniTaskCompletionSource<Sprite> source)
        {
            try
            {
                _spriteHandle = Addressables.LoadAssetAsync<Sprite>(SpriteAddress);
                Sprite result = await _spriteHandle;

                if (_spriteHandle.Status == AsyncOperationStatus.Succeeded && result != null)
                {
                    _cachedSprite = result;
                    source.TrySetResult(result);
                    return;
                }

                if (_spriteHandle.IsValid()) Addressables.Release(_spriteHandle);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ChanceAttackSlashEffect] '{SpriteAddress}' ロード失敗: {e.Message}");
            }

            // フォールバック（Addressables に未登録 / ロード失敗）.
            Sprite fb = GetFallbackSprite();
            _cachedSprite = fb;
            source.TrySetResult(fb);
        }

        private static Sprite GetFallbackSprite()
        {
            if (_fallbackSprite != null) return _fallbackSprite;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _fallbackSprite = Sprite.Create(
                tex,
                new Rect(0, 0, 1, 1),
                new Vector2(0.5f, 0f), // ピボット: 下中央.
                1f);
            return _fallbackSprite;
        }
    }
}
