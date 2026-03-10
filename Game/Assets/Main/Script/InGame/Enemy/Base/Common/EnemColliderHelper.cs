using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;

// 当たり判定ライフサイクルの共通ヘルパー.
// コライダー生成 → 検出器アタッチ → 待機 → 破棄 のパターンを統一する.
public static class EnemColliderHelper
{
    // コライダーフェーズの設定.
    public class ColliderPhaseConfig
    {
        // コライダーの種類.
        public EnemColliderType colliderType = EnemColliderType.Box;

        // 位置オフセット.
        public Vector2 offset = Vector2.zero;

        // サイズ（BoxとCapsule用）.
        public Vector2 size = new Vector2(0.35f, 2f);

        // 半径（Circle用）.
        public float radius = 0.5f;

        // ダメージ量.
        public int damage = 10;

        // 当たり判定の持続時間.
        public float duration = 0.5f;

        // ヒット処理state.
        public EnemColliderState_abstract colliderState;
    }

    /// <summary>
    /// コライダーを生成し、ヒット検出器を設置し、指定時間後に破棄する（時間ベース）.
    /// </summary>
    /// <param name="enemyModel">敵モデル.</param>
    /// <param name="config">コライダー設定.</param>
    /// <param name="durationMs">当たり判定の持続時間（ミリ秒）.</param>
    /// <param name="animSpeed">アニメーション速度倍率.</param>
    /// <returns>正常完了したかどうか.</returns>
    public static async UniTask<bool> ExecuteColliderPhase(
        EnemyModel_abstract enemyModel,
        ColliderPhaseConfig config,
        float durationMs,
        float animSpeed = 1f)
    {
        if (!EnemNullSafetyHelper.IsValid(enemyModel)) return false;

        Transform ownerTransform = enemyModel.Presenter.transform;

        // 攻撃中はレイヤーをDefaultに変更.
        int originalLayer = SetAttackLayer(ownerTransform);

        // コライダー設定を生成.
        var colliderStatus = CreateColliderStatus(ownerTransform, config);
        var createdColliders = colliderStatus.CreateColliders();

        // ヒット検出用コンポーネントを追加.
        EnemyAttackHitDetector hitDetector = AttachDetector(ownerTransform, config.colliderState, createdColliders);

        // 持続時間待機.
        await UniTask.Delay((int)(durationMs / animSpeed));

        // 破棄.
        CleanupColliders(colliderStatus, hitDetector);

        // レイヤー復元.
        RestoreLayer(ownerTransform, originalLayer);

        return EnemNullSafetyHelper.IsValid(enemyModel);
    }

    /// <summary>
    /// コライダーを生成し、条件が満たされるまで維持してから破棄する（条件ベース）.
    /// MeleeAttackなどアニメーション完了を条件とする攻撃用.
    /// </summary>
    /// <param name="enemyModel">敵モデル.</param>
    /// <param name="config">コライダー設定.</param>
    /// <param name="completionPredicate">完了条件（trueで終了）.</param>
    /// <returns>正常完了したかどうか.</returns>
    public static async UniTask<bool> ExecuteColliderPhaseUntil(
        EnemyModel_abstract enemyModel,
        ColliderPhaseConfig config,
        System.Func<bool> completionPredicate)
    {
        if (!EnemNullSafetyHelper.IsValid(enemyModel)) return false;

        Transform ownerTransform = enemyModel.Presenter.transform;

        // 攻撃中はレイヤーをDefaultに変更.
        int originalLayer = SetAttackLayer(ownerTransform);

        // コライダー設定を生成.
        var colliderStatus = CreateColliderStatus(ownerTransform, config);
        var createdColliders = colliderStatus.CreateColliders();

        // ヒット検出用コンポーネントを追加.
        EnemyAttackHitDetector hitDetector = AttachDetector(ownerTransform, config.colliderState, createdColliders);

        // 1フレーム待ってから条件チェック開始.
        await UniTask.Yield();

        // 完了条件まで待機.
        await UniTask.WaitUntil(completionPredicate);

        // 破棄.
        CleanupColliders(colliderStatus, hitDetector);

        // レイヤー復元.
        RestoreLayer(ownerTransform, originalLayer);

        return EnemNullSafetyHelper.IsValid(enemyModel);
    }

    /// <summary>
    /// 既存のコライダーにヒット検出器を設置する（Rush用）.
    /// 破棄は呼び出し元で行う.
    /// </summary>
    /// <param name="ownerTransform">コライダーの親Transform.</param>
    /// <param name="colliderState">ヒット処理state.</param>
    /// <param name="existingColliders">既存コライダーリスト.</param>
    /// <returns>生成したヒット検出器.</returns>
    public static EnemyAttackHitDetector AttachHitDetector(
        Transform ownerTransform,
        EnemColliderState_abstract colliderState,
        List<Collider2D> existingColliders)
    {
        if (ownerTransform == null || existingColliders == null || existingColliders.Count == 0)
            return null;

        var hitDetector = ownerTransform.gameObject.AddComponent<EnemyAttackHitDetector>();
        hitDetector.Initialize(colliderState, existingColliders);
        return hitDetector;
    }

    /// <summary>
    /// 攻撃開始時にレイヤーをDefaultに変更し、元のレイヤーを返す.
    /// </summary>
    public static int SetAttackLayer(Transform ownerTransform)
    {
        int originalLayer = ownerTransform.gameObject.layer;
        ownerTransform.gameObject.layer = LayerMask.NameToLayer("Default");
        return originalLayer;
    }

    /// <summary>
    /// 攻撃終了時にレイヤーを復元する.
    /// </summary>
    public static void RestoreLayer(Transform ownerTransform, int originalLayer)
    {
        if (ownerTransform != null)
        {
            ownerTransform.gameObject.layer = originalLayer;
        }
    }

    // コライダーステータスを生成する内部関数.
    private static EnemColliderStatus CreateColliderStatus(Transform ownerTransform, ColliderPhaseConfig config)
    {
        var colliderStatus = new EnemColliderStatus
        {
            parentTransform = ownerTransform,
            damage = config.damage,
            duration = config.duration
        };

        EnemColliderSetting setting;
        if (config.colliderType == EnemColliderType.Circle)
        {
            setting = new EnemColliderSetting(EnemColliderType.Circle, config.offset, new Vector2(config.radius, config.radius));
            setting.radius = config.radius;
        }
        else
        {
            setting = new EnemColliderSetting(config.colliderType, config.offset, config.size);
        }
        colliderStatus.colliderSettings.Add(setting);

        return colliderStatus;
    }

    // ヒット検出器を生成してアタッチする内部関数.
    private static EnemyAttackHitDetector AttachDetector(
        Transform ownerTransform,
        EnemColliderState_abstract colliderState,
        List<Collider2D> colliders)
    {
        if (colliders == null || colliders.Count == 0) return null;

        var hitDetector = ownerTransform.gameObject.AddComponent<EnemyAttackHitDetector>();
        hitDetector.Initialize(colliderState, colliders);
        return hitDetector;
    }

    // コライダーとヒット検出器を破棄する内部関数.
    private static void CleanupColliders(EnemColliderStatus colliderStatus, EnemyAttackHitDetector hitDetector)
    {
        colliderStatus.DestroyColliders();
        if (hitDetector != null)
        {
            Object.Destroy(hitDetector);
        }
    }
}
