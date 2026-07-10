using UnityEngine;
using InGame.Player;
using LitMotion;

namespace InGame
{
    /// <summary>
    /// ダメージカウンターの移動・コライダー・地形衝突・縮小返却.
    /// プレハブルートにアタッチする.
    /// </summary>
    public class DamageCounter : MonoBehaviour, IDamageCounterShakeSettings
    {
        // --- Iai設定 ---
        [Header("Iai Settings")]
        [Tooltip("Iai時の静止時間（秒）")]
        [SerializeField] private float iaiHoldDuration = 0.5f;
        [Tooltip("Iai時の落下重力")]
        [SerializeField] private float iaiFallGravity = 9.8f;

        // --- 通常/弱攻撃設定 ---
        [Header("Normal/Weak Settings")]
        [Tooltip("横方向初速")]
        [SerializeField] private float horizontalSpeed = 5f;
        [Tooltip("上方向初速")]
        [SerializeField] private float verticalSpeed = 3f;
        [Tooltip("放物線重力")]
        [SerializeField] private float gravity = 8f;

        // --- ライフタイム ---
        [Header("Lifetime")]
        [Tooltip("総生存時間（秒）")]
        [SerializeField] private float totalLifetime = 1.5f;

        // --- 地形衝突時縮小 ---
        [Header("Terrain Hit")]
        [Tooltip("地形衝突時の縮小にかかる時間（秒）")]
        [SerializeField] private float shrinkDuration = 0.3f;

        // --- 出現演出（心拍数100未満時） ---
        [Header("Shake Settings (心拍数100未満)")]
        [Tooltip("左右振動の振幅")]
        [SerializeField] private float shakeAmplitude = 0.3f;
        [Tooltip("振動の周波数")]
        [SerializeField] private int shakeFrequency = 10;
        [Tooltip("振動の持続時間（秒）")]
        [SerializeField] private float shakeDuration = 0.5f;
        [Tooltip("振動の減衰率（0=減衰なし, 1=完全減衰）")]
        [SerializeField] private float shakeDampingRatio = 0.5f;
        [Tooltip("スケール 0→1 の時間（秒）")]
        [SerializeField] private float scaleUpDuration = 0.3f;

        // --- IDamageCounterShakeSettings 実装 ---
        float IDamageCounterShakeSettings.ShakeAmplitude => shakeAmplitude;
        int IDamageCounterShakeSettings.ShakeFrequency => shakeFrequency;
        float IDamageCounterShakeSettings.ShakeDuration => shakeDuration;
        float IDamageCounterShakeSettings.ShakeDampingRatio => shakeDampingRatio;
        float IDamageCounterShakeSettings.ScaleUpDuration => scaleUpDuration;

        // --- TMP参照 ---
        [Header("References")]
        [SerializeField] private DamageCounterTextEffect textEffect;

        // --- ランタイム ---
        private Vector2 velocity;
        private float elapsed;
        private bool isIai;

        // --- 地形衝突関連 ---
        private int platformLayerMask = -1;
        private BoxCollider2D boxCollider;
        private Rigidbody2D rb;
        private bool hitTerrain;
        private float shrinkElapsed;
        private Vector3 shrinkStartScale;
        private Vector3 hitPoint;

        // Canvas子オブジェクトのスケール参照（ルートのscale操作で縮小する用）.
        private float canvasScale;

        // --- LitMotion ---
        private Transform canvasTransform;
        private MotionHandle scaleHandle;
        private MotionHandle shakeHandle;

        private void Awake()
        {
            if (textEffect == null)
                textEffect = GetComponentInChildren<DamageCounterTextEffect>();

            // Rigidbody2D動的追加（kinematic、重力なし）.
            rb = GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.gravityScale = 0f;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            }

            // BoxCollider2D動的追加（trigger）.
            boxCollider = GetComponent<BoxCollider2D>();
            if (boxCollider == null)
            {
                boxCollider = gameObject.AddComponent<BoxCollider2D>();
                boxCollider.isTrigger = true;
            }

            // Canvasの実行時スケールを取得.
            var canvas = GetComponentInChildren<Canvas>();
            canvasScale = canvas != null ? canvas.transform.lossyScale.x : 0.01f;
            canvasTransform = canvas?.transform;

            // 地形衝突レイヤーマスク（Defaultのみ）.
            if (platformLayerMask == -1)
            {
                platformLayerMask = LayerMask.GetMask("Default");
            }
        }

        /// <summary>
        /// DamageCounterPoolから呼ばれる初期化.
        /// </summary>
        public void Initialize(float damage, PlayerAttackType attackType,
                               bool facingRight, float additionalAngle)
        {
            elapsed = 0f;
            hitTerrain = false;
            shrinkElapsed = 0f;
            isIai = (attackType == PlayerAttackType.Iai);
            transform.localScale = Vector3.one;
            transform.rotation = Quaternion.identity;

            // Canvas localPosition リセット.
            if (canvasTransform != null)
            {
                var lp = canvasTransform.localPosition;
                lp.x = 0f;
                canvasTransform.localPosition = lp;
            }

            // 前回のモーションをキャンセル.
            CancelMotions();

            // テキスト設定.
            textEffect?.Setup(damage, attackType);

            // コライダーサイズをテキストBoundsに合わせる.
            UpdateColliderSize();

            if (isIai)
            {
                // Iai: 静止から開始.
                velocity = Vector2.zero;
            }
            else
            {
                // 通常/弱: 横方向 + 上方向の初速.
                float dirX = facingRight ? -1f : 1f;
                velocity = new Vector2(dirX * horizontalSpeed, verticalSpeed);
            }

            // 心拍数100未満: スケール0→1 + 左右振動.
            var playerManager = PlayerManager.Instance(false);
            if (playerManager != null)
            {
                float pulse = playerManager.pulseModel.GetPulseGauge();
                if (pulse < 100f)
                {
                    StartSpawnEffect();
                }
            }
        }

        /// <summary>
        /// 心拍数100未満時の出現演出: スケール0→1 + 左右振動.
        /// </summary>
        private void StartSpawnEffect()
        {
            IDamageCounterShakeSettings settings = this;

            // スケール 0→1（Z軸は1固定）.
            transform.localScale = new Vector3(0f, 0f, 1f);
            scaleHandle = LMotion.Create(0f, 1f, settings.ScaleUpDuration)
                .WithEase(Ease.OutBack)
                .Bind(v =>
                {
                    if (!hitTerrain)
                        transform.localScale = new Vector3(v, v, 1f);
                });

            // 左右振動（Canvas子オブジェクトに適用）.
            if (canvasTransform != null)
            {
                shakeHandle = LMotion.Shake.Create(0f, settings.ShakeAmplitude, settings.ShakeDuration)
                    .WithFrequency(settings.ShakeFrequency)
                    .WithDampingRatio(settings.ShakeDampingRatio)
                    .Bind(v =>
                    {
                        if (canvasTransform != null)
                        {
                            var lp = canvasTransform.localPosition;
                            lp.x = v;
                            canvasTransform.localPosition = lp;
                        }
                    });
            }
        }

        /// <summary>
        /// アクティブなLitMotionモーションをキャンセル.
        /// </summary>
        private void CancelMotions()
        {
            if (scaleHandle.IsActive()) scaleHandle.Cancel();
            if (shakeHandle.IsActive()) shakeHandle.Cancel();
        }

        /// <summary>
        /// BoxCollider2DサイズをTMPテキストBoundsに合わせる.
        /// </summary>
        private void UpdateColliderSize()
        {
            if (boxCollider == null || textEffect == null) return;

            Vector2 textBounds = textEffect.GetTextBoundsSize();
            // テキストBoundsはCanvas内ローカル値。ルート座標にはcanvasScaleをかける.
            boxCollider.size = textBounds * canvasScale;
            boxCollider.offset = Vector2.zero;
        }

        private void Update()
        {
            if (hitTerrain)
            {
                UpdateShrink();
                return;
            }

            float dt = Time.deltaTime;
            elapsed += dt;

            if (isIai)
            {
                // Phase1: 静止 → Phase2: 落下.
                if (elapsed > iaiHoldDuration)
                {
                    velocity.y -= iaiFallGravity * dt;
                    Vector3 pos = transform.position;
                    pos.x += velocity.x * dt;
                    pos.y += velocity.y * dt;
                    pos.z = 0f; // Z軸移動禁止.
                    transform.position = pos;
                }
            }
            else
            {
                // 放物線: 初速 + 重力.
                velocity.y -= gravity * dt;
                Vector3 pos = transform.position;
                pos.x += velocity.x * dt;
                pos.y += velocity.y * dt;
                pos.z = 0f; // Z軸移動禁止.
                transform.position = pos;
            }

            // 正規化プログレス [0..1] をテキストエフェクトに通知.
            float progress = Mathf.Clamp01(elapsed / totalLifetime);
            textEffect?.UpdateProgress(progress);

            // ライフタイム終了でプールに返却.
            if (elapsed >= totalLifetime)
            {
                ReturnToPool();
            }
        }

        /// <summary>
        /// 地形衝突時の縮小処理.
        /// </summary>
        private void UpdateShrink()
        {
            shrinkElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(shrinkElapsed / shrinkDuration);

            // Z軸は1固定で XY のみ縮小.
            float xy = Mathf.Lerp(shrinkStartScale.x, 0f, t);
            transform.localScale = new Vector3(xy, xy, 1f);

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
            CancelMotions();

            // Canvas localPosition リセット.
            if (canvasTransform != null)
            {
                var lp = canvasTransform.localPosition;
                lp.x = 0f;
                canvasTransform.localPosition = lp;
            }

            // 親子付け解除 → プールのtransformに戻す.
            var poolTransform = DamageCounterPool.Instance(false)?.transform;
            if (poolTransform != null)
            {
                transform.SetParent(poolTransform);
            }
            transform.localScale = Vector3.one;
            gameObject.SetActive(false);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (hitTerrain) return;

            // トリガー（攻撃判定など）には反応しない.
            if (other.isTrigger) return;

            // Platformレイヤーチェック.
            if (((1 << other.gameObject.layer) & platformLayerMask) == 0) return;

            // 速度方向にRaycast → 正確なhit point + normal取得.
            Vector2 dir = velocity.normalized;
            if (dir == Vector2.zero) dir = Vector2.down;

            RaycastHit2D hit = Physics2D.Raycast(
                transform.position, dir, 2f, platformLayerMask
            );

            if (hit.collider != null)
            {
                hitPoint = hit.point;
                Vector2 normal = hit.normal;

                // 位置固定.
                transform.position = new Vector3(hitPoint.x, hitPoint.y, 0f);

                // hit対象に親子付け.
                transform.SetParent(other.transform);

                // 血痕エフェクトをスポーン.
                BloodSplatterPool.Instance(false)?.Spawn(hitPoint, dir, normal);
            }
            else
            {
                // Raycastが外れた場合は現在位置で固定.
                hitPoint = transform.position;
                transform.SetParent(other.transform);

                // 血痕エフェクト（法線不明のためVector2.upを使用）.
                BloodSplatterPool.Instance(false)?.Spawn(hitPoint, dir, Vector2.up);
            }

            // スケールモーションをキャンセル（縮小に切り替え）.
            if (scaleHandle.IsActive()) scaleHandle.Cancel();

            // 縮小開始.
            hitTerrain = true;
            shrinkElapsed = 0f;
            shrinkStartScale = transform.localScale;
            velocity = Vector2.zero;
        }
    }
}
