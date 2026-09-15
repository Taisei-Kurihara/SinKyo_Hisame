using UnityEngine;
using Cysharp.Threading.Tasks;

// Wendig用 MeteorDrop中の居合スタン（2秒）.
// 空中にいる場合は落下→着地してからスタン時間のカウントを開始する.
public class EnemInterruptState_MeteorDropStan_Wendig : EnemInterruptState_Stan_abstract
{
    public EnemInterruptState_MeteorDropStan_Wendig()
    {
        stanBoolName = "Stan";
        stanDuration = 5f;
        // 長時間スタン = チャンス状態のトリガー（InGamePresenter 経由で通知）.
        battleStateOnStan = EnemyBattleState.StunLong;
    }

    protected override async UniTask OnStanProcess(EnemyModel_abstract enemyModel)
    {
        Debug.Log($"[MeteorDropStan] OnStanProcess開始");

        // スタンSE再生.
        if (enemyModel?.Presenter != null)
        {
            enemyModel.Presenter.PlaySE("Stan");
        }

        // MeteorDrop中断後のクリーンアップ(OnAfterPostAction)を1フレーム待つ.
        await UniTask.Yield();

        if (enemyModel == null) return;

        var rb = enemyModel.Rigidbody;
        var transform = enemyModel.Presenter?.transform;

        // 傾きを復元（MeteorDrop落下中は85度傾いている場合あり）.
        if (transform != null)
        {
            transform.rotation = Quaternion.Euler(0f, transform.rotation.eulerAngles.y, 0f);
        }

        // 空中にいる場合: 落下→着地を待ってからスタンカウント開始.
        if (rb != null && !EnemState_abstract.IsOnGround(transform))
        {
            // Y軸制約を解除して落下可能にする.
            rb.constraints &= ~RigidbodyConstraints2D.FreezePositionY;
            // 急速落下開始（重力のみだと遅いため初速を付与）.
            rb.linearVelocity = new Vector2(0f, -20f);

            Debug.Log($"[MeteorDropStan] 空中検知 → 落下開始");

            // 着地待機（基底クラスの共通メソッド使用: Default レイヤーのみ、中心からレイキャスト）.
            bool landed = await EnemState_abstract.WaitForLandingAsync(enemyModel, transform, rb, 3f);

            Debug.Log($"[MeteorDropStan] 着地確認 (landed: {landed})");
        }

        // 着地状態をアニメーターに反映.
        if (enemyModel.Animator != null)
        {
            enemyModel.Animator.SetBool("OnGround", true);
            enemyModel.Animator.SetInteger("Move", 0);
        }
        enemyModel.IsJumping = false;

        Debug.Log($"[MeteorDropStan] 着地済 → スタン{stanDuration}sec開始");

        // スタン持続時間分待機（着地後からカウント開始）.
        await UniTask.Delay((int)(stanDuration * 1000));
    }

}
