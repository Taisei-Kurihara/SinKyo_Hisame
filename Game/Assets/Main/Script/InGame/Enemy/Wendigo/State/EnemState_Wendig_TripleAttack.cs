using UnityEngine;
using Cysharp.Threading.Tasks;

// Wendig用 TripleAttack(三連撃)State.
public class EnemState_Wendig_TripleAttack : EnemState_abstract
{
    // 三連撃: 0.75/1.1/1.35倍.
    private float[] attackMultipliers = { 0.75f, 1.1f, 1.35f };
    private Vector2 attackOffset = new Vector2(0.5f, 0f);
    private Vector2 attackSize = new Vector2(1.5f, 2f);

    // 三連撃のトリガー名.
    private string[] attackTriggers = { "TripleAttack_0", "TripleAttack_1", "TripleAttack_2" };

    // ライフサイクル間共有データ.
    private float baseAttackPower;

    public EnemState_Wendig_TripleAttack()
    {
        postActionWaitFrames = 60;
    }

    protected override async UniTask OnPreAction(EnemyModel_abstract enemyModel)
    {
        if (!EnemNullSafetyHelper.IsValidWithAnimator(enemyModel)) { isAborted = true; return; }

        // 基礎攻撃力を取得.
        baseAttackPower = 50f;
        if (enemyModel is EnemyModel_Wendig wendigModel)
        {
            baseAttackPower = wendigModel.GetCurrentAttackPower();
        }

        // プレイヤーの方を向く.
        var player = Object.FindFirstObjectByType<InGame.Player.PlayerScope>();
        if (player != null)
        {
            EnemFacingHelper.FaceToward(enemyModel.Presenter.transform, player.transform.position);
        }

        // TripleAttack開始トリガー実行.
        enemyModel.Animator.SetTrigger("TripleAttack");

        float animSpeed = enemyModel.AnimSpeed;

        // === 前段階 ===.
        // 200ms待機 → 攻撃通告(パリィ可能) → 300ms待機.
        if (!await EnemAttackPhaseHelper.PlayAttackPremonition(
            enemyModel, 500f, true, 300f, animSpeed)) { isAborted = true; return; }
    }

    protected override async UniTask OnAction(EnemyModel_abstract enemyModel)
    {
        if (!EnemNullSafetyHelper.IsValidWithAnimator(enemyModel)) { isAborted = true; return; }

        var presenter = enemyModel.Presenter;
        Animator animator = enemyModel.Animator;

        // === 攻撃中 ===.
        // 三連撃を Animation Event 同期で実行.
        // 各アニメーションクリップ (TripleAttack_0/_1/_2) に
        // AnimEvent_HitStart / AnimEvent_HitEnd を設定する必要がある.
        Vector2 colliderOffset = new Vector2(-Mathf.Abs(attackOffset.x), attackOffset.y);

        for (int i = 0; i < 3; i++)
        {
            if (!EnemNullSafetyHelper.IsValidWithAnimator(enemyModel)) break;

            // 各段のダメージを計算.
            int attackDamage = (int)(baseAttackPower * attackMultipliers[i]);

            // 攻撃トリガー実行.
            animator.SetTrigger(attackTriggers[i]);

            // Animation Event 待機用.
            var hitStartTcs = new UniTaskCompletionSource();
            bool hitEnded = false;

            presenter.RegisterHitDetectionCallbacks(
                onStart: () => hitStartTcs.TrySetResult(),
                onEnd: () => hitEnded = true
            );

            // AnimEvent_HitStart を待機（タイムアウト500ms: Event未設定時の安全策）.
            await UniTask.WhenAny(
                hitStartTcs.Task,
                UniTask.Delay(500)
            );

            if (!EnemNullSafetyHelper.IsValidWithAnimator(enemyModel))
            {
                presenter.ClearHitDetectionCallbacks();
                break;
            }

            // コライダー生成 → AnimEvent_HitEnd まで維持.
            var colliderState = new EnemColliderState_PlayerDamage();
            colliderState.SetDamage(attackDamage);
            colliderState.ClearHitTargets();

            // 安全タイムアウト用.
            float phaseStartTime = Time.time;

            await EnemColliderHelper.ExecuteColliderPhaseUntil(
                enemyModel,
                new EnemColliderHelper.ColliderPhaseConfig
                {
                    colliderType = EnemColliderType.Box,
                    offset = colliderOffset,
                    size = attackSize,
                    damage = attackDamage,
                    colliderState = colliderState
                },
                () => hitEnded
                    || !EnemNullSafetyHelper.IsValidWithAnimator(enemyModel)
                    || (Time.time - phaseStartTime > 1f) // 安全タイムアウト: Event未設定時
            );

            presenter.ClearHitDetectionCallbacks();
        }
    }

    protected override async UniTask OnPrePostAction(EnemyModel_abstract enemyModel)
    {
        if (!EnemNullSafetyHelper.IsValid(enemyModel)) return;

        float animSpeed = enemyModel.AnimSpeed;
        await EnemAttackPhaseHelper.DelayWithAnimSpeed(enemyModel, 100f, animSpeed);
    }

    protected override async UniTask OnAfterPostAction(EnemyModel_abstract enemyModel)
    {
        // コールバック安全クリア（中断時の保証）.
        enemyModel?.Presenter?.ClearHitDetectionCallbacks();

        if (EnemNullSafetyHelper.IsValidWithAnimator(enemyModel))
        {
            enemyModel.Animator.SetTrigger("TripleAttack_End");
        }
        await UniTask.CompletedTask;
    }
}
