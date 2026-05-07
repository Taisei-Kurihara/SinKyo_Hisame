using UnityEngine;
using Cysharp.Threading.Tasks;

// Wendig用 Howling(遠吠え)State.
public class EnemState_Wendig_Howling : EnemState_abstract
{
    // ハウリング: 1.8倍.
    private float attackMultiplier = 1.8f;
    private float attackRadius = 3f;

    // ライフサイクル間共有データ.
    private int attackDamage;
    private Transform ownerTransform;
    private AfterimageEffect afterimageEffect;

    public EnemState_Wendig_Howling()
    {
        postActionWaitFrames = 60;
    }

    protected override async UniTask OnPreAction(EnemyModel_abstract enemyModel)
    {
        if (!EnemNullSafetyHelper.IsValidWithAnimator(enemyModel)) { isAborted = true; return; }

        // 現在の攻撃力から実ダメージを計算.
        attackDamage = 90;
        if (enemyModel is EnemyModel_Wendig wendigModel)
        {
            attackDamage = (int)(wendigModel.GetCurrentAttackPower() * attackMultiplier);
        }

        float animSpeed = enemyModel.AnimSpeed;

        // Howlingアニメーション開始.
        enemyModel.Animator.SetTrigger("Howling");

        // === 前段階 ===.
        if (!await EnemAttackPhaseHelper.PlayAttackPremonition(
            enemyModel, 500f, false, 300f, animSpeed)) { isAborted = true; return; }
    }

    protected override async UniTask OnAction(EnemyModel_abstract enemyModel)
    {
        if (!EnemNullSafetyHelper.IsValid(enemyModel)) { isAborted = true; return; }

        ownerTransform = enemyModel.Presenter.transform;
        float animSpeed = enemyModel.AnimSpeed;

        // 赤い残像エフェクト開始.
        afterimageEffect = ownerTransform.GetComponent<AfterimageEffect>();
        if (afterimageEffect == null)
        {
            afterimageEffect = ownerTransform.gameObject.AddComponent<AfterimageEffect>();
        }
        afterimageEffect.SetColor(new Color(1f, 0.2f, 0.2f, 0.6f));
        afterimageEffect.SetScaleMultiplier(1.2f);
        afterimageEffect.StartEffect();

        // 円形の攻撃判定を生成し400ms維持.
        var colliderState = new EnemColliderState_Wendig_Howling();
        colliderState.SetDamage(attackDamage);
        colliderState.ClearHitTargets();

        await EnemColliderHelper.ExecuteColliderPhase(
            enemyModel,
            new EnemColliderHelper.ColliderPhaseConfig
            {
                colliderType = EnemColliderType.Circle,
                offset = Vector2.zero,
                radius = attackRadius,
                damage = attackDamage,
                duration = 0.5f,
                colliderState = colliderState
            },
            400f, animSpeed);

        // 残像エフェクト停止.
        afterimageEffect?.StopEffect();
    }

    protected override async UniTask OnAfterPostAction(EnemyModel_abstract enemyModel)
    {
        if (EnemNullSafetyHelper.IsValidWithAnimator(enemyModel))
        {
            enemyModel.Animator.SetTrigger("Howling_End");
        }
        await UniTask.CompletedTask;
    }
}
