using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System;
using InGame;
using InGame.Common;
using Common;

namespace InGame.Player
{
    /// <summary>
    /// コライダーの種類.
    /// </summary>
    public enum PlayerColliderType
    {
        Box,
        Circle,
        Capsule
    }

    /// <summary>
    /// 個別コライダーの設定.
    /// </summary>
    [System.Serializable]
    public class PlayerColliderSetting
    {
        // コライダーの種類.
        public PlayerColliderType colliderType = PlayerColliderType.Box;

        // 位置オフセット.
        public Vector2 offset = Vector2.zero;

        // サイズ（BoxとCapsule用）.
        public Vector2 size = new Vector2(3f, 3f);

        // 半径（Circle用）.
        public float radius = 1f;

        // カプセルの方向（Capsule用）.
        public CapsuleDirection2D capsuleDirection = CapsuleDirection2D.Vertical;

        public PlayerColliderSetting() { }

        public PlayerColliderSetting(PlayerColliderType type, Vector2 offset, Vector2 size)
        {
            this.colliderType = type;
            this.offset = offset;
            this.size = size;
        }
    }

    /// <summary>
    /// 攻撃全体のコライダー設定.
    /// </summary>
    public class PlayerColliderStatus
    {
        // 親Transform（コライダーを追加する対象）.
        public Transform parentTransform;

        // 個別コライダー設定のリスト.
        public List<PlayerColliderSetting> colliderSettings = new List<PlayerColliderSetting>();

        // 当たり判定の持続時間.
        public float duration = 0.5f;

        // ダメージ量.
        public float damage = 10f;

        // 生成されたコライダーのリスト.
        private List<Collider2D> createdColliders = new List<Collider2D>();

        // コライダーを生成.
        public List<Collider2D> CreateColliders()
        {
            if (parentTransform == null)
            {
                Debug.LogWarning($"[PlayerColliderStatus] CreateColliders - parentTransform が null.");
                return createdColliders;
            }

            // localScale.xの符号でオフセット反転を判定.
            float scaleSign = Mathf.Sign(parentTransform.localScale.x);

            //Debug.Log($"[PlayerColliderStatus] CreateColliders - 設定数: {colliderSettings.Count}, scaleSign: {scaleSign}");

            foreach (var setting in colliderSettings)
            {
                Collider2D collider = null;

                // localScaleが負の場合、オフセットのX座標を反転.
                Vector2 adjustedOffset = new Vector2(setting.offset.x * scaleSign, setting.offset.y);

                switch (setting.colliderType)
                {
                    case PlayerColliderType.Box:
                        var boxCollider = parentTransform.gameObject.AddComponent<BoxCollider2D>();
                        boxCollider.offset = adjustedOffset;
                        boxCollider.size = setting.size;
                        boxCollider.isTrigger = true;
                        collider = boxCollider;
                        break;

                    case PlayerColliderType.Circle:
                        var circleCollider = parentTransform.gameObject.AddComponent<CircleCollider2D>();
                        circleCollider.offset = adjustedOffset;
                        circleCollider.radius = setting.radius;
                        circleCollider.isTrigger = true;
                        collider = circleCollider;
                        break;

                    case PlayerColliderType.Capsule:
                        var capsuleCollider = parentTransform.gameObject.AddComponent<CapsuleCollider2D>();
                        capsuleCollider.offset = adjustedOffset;
                        capsuleCollider.size = setting.size;
                        capsuleCollider.direction = setting.capsuleDirection;
                        capsuleCollider.isTrigger = true;
                        collider = capsuleCollider;
                        break;
                }

                if (collider != null)
                {
                    collider.hideFlags = HideFlags.HideAndDontSave;
                    createdColliders.Add(collider);
                }
            }

            //Debug.Log($"[PlayerColliderStatus] CreateColliders完了 - 生成数: {createdColliders.Count}");
            return createdColliders;
        }

        // 生成したコライダーを削除.
        public void DestroyColliders()
        {
            //Debug.Log($"[PlayerColliderStatus] DestroyColliders - 削除数: {createdColliders.Count}");

            foreach (var collider in createdColliders)
            {
                if (collider != null)
                {
                    UnityEngine.Object.Destroy(collider);
                }
            }
            createdColliders.Clear();
        }

        // 指定されたコライダーがこのステータスで生成されたものかチェック.
        public bool ContainsCollider(Collider2D collider)
        {
            return createdColliders.Contains(collider);
        }
    }

    /// <summary>
    /// 命中時の処理基底クラス.
    /// </summary>
    public abstract class PlayerColliderState_abstract
    {
        // ヒット済みの対象を記録.
        protected HashSet<GameObject> hitTargets = new HashSet<GameObject>();

        // コライダーステータス.
        protected PlayerColliderStatus colliderStatus;

        // ダメージ量.
        protected float damage = 10f;

        // コライダーステータスを設定.
        public void SetColliderStatus(PlayerColliderStatus status)
        {
            colliderStatus = status;
            damage = status.damage;
        }

        // ダメージを設定.
        public void SetDamage(float dmg)
        {
            damage = dmg;
        }

        // ヒット対象リストをクリア.
        public void ClearHitTargets()
        {
            hitTargets.Clear();
        }

        // ヒット処理.
        public bool TryProcessHit(GameObject target, Collider2D hitCollider)
        {
            if (target == null) return false;

            // 既にヒット済みの対象はスキップ.
            if (hitTargets.Contains(target)) return false;

            // ヒット済みリストに追加.
            hitTargets.Add(target);
            //Debug.Log($"[PlayerColliderState] TryProcessHit - {target.name} ヒット.");

            // 実際のヒット処理を実行.
            OnHit(target, hitCollider);
            return true;
        }

        // 継承クラスで実際のヒット処理を実装.
        protected virtual void OnHit(GameObject target, Collider2D hitCollider)
        {
            //Debug.Log($"[PlayerColliderState] OnHit - Target: {target.name}");
        }

        // 敵にダメージを与える共通処理.
        protected void DamageEnemy(GameObject target, float attackDamage)
        {
            var enemyPresenter = target.GetComponent<EnemyPresenter_abstract>();
            if (enemyPresenter == null)
            {
                //Debug.Log($"[PlayerColliderState] DamageEnemy - EnemyPresenter が見つからない: {target.name}");
                return;
            }

            //Debug.Log($"[PlayerColliderState] DamageEnemy - 敵ヒット name={target.name}, damage={attackDamage}");

            // ダメージ処理.
            if (enemyPresenter.Status != null)
            {
                enemyPresenter.Status.OnDamaged(attackDamage).Forget();
            }
        }
    }

    /// <summary>
    /// 攻撃種別（ヒットストップ時間に影響）.
    /// </summary>
    public enum PlayerAttackType
    {
        Weak,       // 弱攻撃: 0.12秒.
        Normal,     // 通常攻撃: 0.2秒.
        Iai         // 居合: 0.3秒.
    }

    /// <summary>
    /// 敵ダメージ用クラス.
    /// </summary>
    public class PlayerColliderState_EnemyDamage : PlayerColliderState_abstract
    {
        // ヒット数カウント.
        private int hitCount = 0;

        // ヒット時コールバック.
        private Action onHitCallback;

        // 攻撃種別.
        private PlayerAttackType attackType = PlayerAttackType.Normal;

        // ヒット数を取得.
        public int GetHitCount() => hitCount;

        // ヒット数をリセット.
        public void ResetHitCount() => hitCount = 0;

        // ヒット時コールバックを設定.
        public void SetOnHitCallback(Action callback)
        {
            onHitCallback = callback;
        }

        // 攻撃種別を設定.
        public void SetAttackType(PlayerAttackType type)
        {
            attackType = type;
        }

        protected override void OnHit(GameObject target, Collider2D hitCollider)
        {
            DamageEnemy(target, damage);

            // 攻撃のHit位置を算出（攻撃元からエネミーコライダーの最近接点）.
            Vector3 hitPos = target.transform.position;
            bool facingRight = true;
            if (colliderStatus != null && colliderStatus.parentTransform != null)
            {
                Vector2 attackOrigin = colliderStatus.parentTransform.position;
                hitPos = hitCollider.ClosestPoint(attackOrigin);
                facingRight = colliderStatus.parentTransform.localScale.x < 0f;
            }

            // 被弾エフェクトをHit位置に攻撃方向で生成.
            HitEffectPool.Instance(false).Spawn(hitPos, facingRight);

            // ヒットストップ発動.
            HitStopManager.Instance(false)?.PlayHitStop(attackType);

            hitCount++;

            // 鼓動上昇: 攻撃を振る（Hit） +0.5.
            PlayerManager.Instance().pulseModel.OnAttackHit();

            // ヒット時コールバック実行.
            onHitCallback?.Invoke();

            // 心拍数上昇時の追加多段ヒット（居合以外）.
            if (attackType != PlayerAttackType.Iai)
            {
                int extraHits = CalculateExtraHits();
                if (extraHits > 0)
                {
                    float extraDamage = damage * 0.275f;
                    ExecuteExtraHitsAsync(target, hitCollider, hitPos, facingRight, extraHits, extraDamage).Forget();
                }
            }
        }

        /// <summary>
        /// 心拍数に基づく追加ヒット回数を計算.
        /// 心拍数0から20上がるごとに1回追加、180で4回上限.
        /// </summary>
        private int CalculateExtraHits()
        {
            float pulse = PlayerManager.Instance().pulseModel.GetPulseGauge();
            // 100が基準、100未満は追加なし.
            if (pulse <= 100f) return 0;
            // 100から20上がるごとに1回、最大4回.
            int hits = (int)((pulse - 100f) / 20f);
            return Mathf.Clamp(hits, 0, 4);
        }

        /// <summary>
        /// 追加ヒットを0.1秒間隔で実行（hitstop/effect/SE付き）.
        /// </summary>
        private async UniTaskVoid ExecuteExtraHitsAsync(GameObject target, Collider2D hitCollider, Vector3 hitPos, bool facingRight, int count, float extraDamage)
        {
            for (int i = 0; i < count; i++)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(0.1f), ignoreTimeScale: true);

                if (target == null) break;

                // 追加ダメージ.
                DamageEnemy(target, extraDamage);

                // エフェクト再生（最初+30度、以降15度ずつ下方向にズラす）.
                float effectAngle = 30f - (15f * i);
                HitEffectPool.Instance(false)?.Spawn(hitPos, facingRight, effectAngle);

                // SE再生（コールバック経由）.
                onHitCallback?.Invoke();
            }
        }
    }

    /// <summary>
    /// 攻撃ヒット検出コンポーネント（Physics2D.OverlapBoxベース）.
    /// </summary>
    public class PlayerAttackHitDetector : MonoBehaviour
    {
        private PlayerColliderState_abstract colliderState;
        private List<PlayerColliderSetting> colliderSettings;
        private Transform parentTransform;

        // 検出対象のレイヤーマスク.
        private LayerMask enemyLayerMask;

        public void Initialize(PlayerColliderState_abstract state, string tag = "Enemy")
        {
            // 旧互換用（使用されない）.
            colliderState = state;
        }

        public void Initialize(PlayerColliderState_abstract state, List<PlayerColliderSetting> settings, Transform parent)
        {
            // ランタイム専用コンポーネントとしてフラグ設定.
            hideFlags = HideFlags.HideAndDontSave;
            colliderState = state;
            colliderSettings = settings;
            parentTransform = parent;

            // Enemyレイヤーを取得（存在しない場合は全レイヤー）.
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer >= 0)
            {
                enemyLayerMask = 1 << enemyLayer;
            }
            else
            {
                // Enemyレイヤーがない場合はデフォルトレイヤーを含む全レイヤー.
                enemyLayerMask = Physics2D.AllLayers;
            }
        }

        private void FixedUpdate()
        {
            if (colliderState == null || colliderSettings == null || parentTransform == null) return;

            // 各コライダー設定に対してOverlapで判定.
            foreach (var setting in colliderSettings)
            {
                CheckOverlap(setting);
            }
        }

        private void CheckOverlap(PlayerColliderSetting setting)
        {
            // localScale.xの符号でオフセット反転を判定.
            float scaleSign = Mathf.Sign(parentTransform.localScale.x);
            Vector2 adjustedOffset = new Vector2(setting.offset.x * scaleSign, setting.offset.y);

            Vector2 worldPos = (Vector2)parentTransform.position + adjustedOffset;
            Vector2 size = setting.size;

            // 親のスケールを考慮.
            float scaleX = Mathf.Abs(parentTransform.lossyScale.x);
            float scaleY = Mathf.Abs(parentTransform.lossyScale.y);
            Vector2 scaledSize = new Vector2(size.x * scaleX, size.y * scaleY);

            // 親の回転を取得.
            float angle = parentTransform.eulerAngles.z;

            Collider2D[] hits = null;

            switch (setting.colliderType)
            {
                case PlayerColliderType.Box:
                    hits = Physics2D.OverlapBoxAll(worldPos, scaledSize, angle, enemyLayerMask);
                    break;

                case PlayerColliderType.Circle:
                    float scaledRadius = setting.radius * Mathf.Max(scaleX, scaleY);
                    hits = Physics2D.OverlapCircleAll(worldPos, scaledRadius, enemyLayerMask);
                    break;

                case PlayerColliderType.Capsule:
                    hits = Physics2D.OverlapCapsuleAll(worldPos, scaledSize, setting.capsuleDirection, angle, enemyLayerMask);
                    break;
            }

            if (hits == null) return;

            foreach (var hit in hits)
            {
                ProcessHit(hit);
            }
        }

        private void ProcessHit(Collider2D collision)
        {
            if (colliderState == null) return;

            // EnemyPresenterコンポーネントで判定.
            var enemyPresenter = collision.GetComponent<EnemyPresenter_abstract>();
            if (enemyPresenter == null)
            {
                enemyPresenter = collision.GetComponentInParent<EnemyPresenter_abstract>();
            }
            if (enemyPresenter == null) return;

            // ヒット処理を実行（重複処理はcolliderStateが防ぐ）.
            colliderState.TryProcessHit(enemyPresenter.gameObject, collision);
        }
    }
}
