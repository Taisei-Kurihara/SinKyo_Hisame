using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
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
    ///
    /// 初期化: ゲーム開始時に InitializePoolAsync() を呼ぶこと.
    /// これによりスプライトをロード済みのインスタンスをプールに積んでおき、
    /// Spawn 時の非同期ロードラグ（豆腐表示）を完全に除去する.
    /// </summary>
    public class ChanceAttackSlashEffect : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;

        /// <summary>スプライト1ユニットあたりの自然サイズ（高さ）.</summary>
        [SerializeField] private float spriteNaturalHeight = 1f;

        /// <summary>Xスケールの最大値（スラッシュの幅）.</summary>
        [SerializeField] private float maxScaleX = 0.25f;

        /// <summary>アニメーション総時間（秒）.</summary>
        [SerializeField] private float duration = 0.25f;

        // ==========================================
        // Addressables スプライトキャッシュ
        // ==========================================

        /// <summary>Addressables に登録するスプライトのキー名.</summary>
        public const string SpriteAddress = "ChanceAttackSlash";

        private static Sprite _cachedSprite;
        private static AsyncOperationHandle<Sprite> _spriteHandle;

        // ロード失敗 / 未登録時のフォールバック（1×1 白ピクセル）.
        private static Sprite _fallbackSprite;

        // ==========================================
        // オブジェクトプール
        // ==========================================

        private static readonly Queue<ChanceAttackSlashEffect> _pool = new Queue<ChanceAttackSlashEffect>();

        /// <summary>
        /// スプライトをロードしてプールにインスタンスを積む.
        /// ゲーム開始時に一度だけ呼ぶこと. 以後の Spawn はプールから取得するため
        /// 非同期ロードラグが発生しない.
        /// </summary>
        public static async UniTaskVoid InitializePoolAsync(int poolSize = 12)
        {
            // スプライトを先にロード.
            Sprite sprite = await LoadSpriteOnceAsync();

            // プールにインスタンスを積む.
            for (int i = 0; i < poolSize; i++)
            {
                var instance = CreatePooledInstance(sprite);
                instance.gameObject.SetActive(false);
                _pool.Enqueue(instance);
            }
        }

        /// <summary>
        /// 静的ファクトリ: 位置・方向・長さを指定してエフェクトを生成する.
        /// </summary>
        public static void Spawn(Vector3 from, Vector3 to)
        {
            var effect = GetFromPool();
            effect.gameObject.SetActive(true);
            effect.PlayAsync(from, to).Forget();
        }

        /// <summary>
        /// 静的ファクトリ: 開始地点を固定し、<paramref name="trackTarget"/> を毎フレーム追跡して伸びるエフェクトを生成.
        /// </summary>
        public static void SpawnTracking(Vector3 from, Transform trackTarget, float duration)
        {
            var effect = GetFromPool();
            effect.gameObject.SetActive(true);
            effect.duration = duration;
            effect.PlayTrackingAsync(from, trackTarget).Forget();
        }

        // ==========================================
        // プール管理
        // ==========================================

        private static ChanceAttackSlashEffect GetFromPool()
        {
            // 破棄済みインスタンスを除外しながらプールから取得.
            while (_pool.Count > 0)
            {
                var candidate = _pool.Dequeue();
                if (candidate != null)
                    return candidate;
            }

            // プールが枯渇した場合は新規作成（スプライトは既にキャッシュ済み）.
            return CreatePooledInstance(_cachedSprite ?? GetFallbackSprite());
        }

        private static ChanceAttackSlashEffect CreatePooledInstance(Sprite sprite)
        {
            var go = new GameObject("ChanceSlashEffect");
            DontDestroyOnLoad(go);

            var effect = go.AddComponent<ChanceAttackSlashEffect>();
            var sr = go.AddComponent<SpriteRenderer>();
            effect.spriteRenderer = sr;
            sr.sortingLayerName = "Default";
            sr.sortingOrder = 10;
            sr.color = new Color(1f, 1f, 1f, 0.85f);

            // スプライトをここで設定: Spawn 時に非同期ロード不要.
            sr.sprite = sprite;
            if (sprite != null && sprite.pixelsPerUnit > 0f)
                effect.spriteNaturalHeight = sprite.rect.height / sprite.pixelsPerUnit;

            // 初期スケールをゼロに: SetActive(true) 直後に旧スケールで描画されるのを防ぐ.
            go.transform.localScale = Vector3.zero;

            return effect;
        }

        private void ReturnToPool()
        {
            if (this == null) return;
            transform.localScale = Vector3.zero; // 次回取得時に旧スケールで描画されるのを防ぐ.
            gameObject.SetActive(false);
            _pool.Enqueue(this);
        }

        // ==========================================
        // アニメーション
        // ==========================================

        private async UniTaskVoid PlayAsync(Vector3 from, Vector3 to)
        {
            if (spriteRenderer == null) { ReturnToPool(); return; }

            // --- 位置・回転 ---
            transform.position = from;
            Vector3 delta  = to - from;
            float distance = delta.magnitude;
            if (distance < 0.01f) { ReturnToPool(); return; }

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

            ReturnToPool();
        }

        /// <summary>
        /// 追跡モード: <paramref name="from"/> を起点に、毎フレーム <paramref name="trackTarget"/> の位置まで
        /// スプライトを伸ばし続ける. Xスケールは 0 → maxScaleX → 0 のパラボラでアニメーション.
        /// </summary>
        private async UniTaskVoid PlayTrackingAsync(Vector3 from, Transform trackTarget)
        {
            if (spriteRenderer == null) { ReturnToPool(); return; }

            // 起点に配置（毎フレーム to 側だけ更新するため position は固定）.
            transform.position = from;
            // ループ内で distance <= 0.01 の場合はスケールが更新されないため、ここで明示的にゼロ化.
            transform.localScale = Vector3.zero;

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

            ReturnToPool();
        }

        // ==========================================
        // スプライトロード（初期化時のみ使用）
        // ==========================================

        private static async UniTask<Sprite> LoadSpriteOnceAsync()
        {
            if (_cachedSprite != null) return _cachedSprite;

            try
            {
                _spriteHandle = Addressables.LoadAssetAsync<Sprite>(SpriteAddress);
                Sprite result = await _spriteHandle;

                if (_spriteHandle.Status == AsyncOperationStatus.Succeeded && result != null)
                {
                    _cachedSprite = result;
                    return result;
                }

                if (_spriteHandle.IsValid()) Addressables.Release(_spriteHandle);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ChanceAttackSlashEffect] '{SpriteAddress}' ロード失敗: {e.Message}");
            }

            // フォールバック.
            Sprite fb = GetFallbackSprite();
            _cachedSprite = fb;
            return fb;
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
