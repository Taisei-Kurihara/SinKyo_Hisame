using UnityEngine;
using Cysharp.Threading.Tasks;

// Wendig用 MeteorDrop中の居合スタン（2秒）.
// 空中にいる場合は落下→着地してからスタン時間のカウントを開始する.
public class EnemInterruptState_MeteorDropStan_Wendig : EnemInterruptState_Stan_abstract
{
    // 着地判定用.
    private int groundLayerMask = -1;
    private const float groundCheckDistance = 0.5f;

    public EnemInterruptState_MeteorDropStan_Wendig()
    {
        stanBoolName = "Stan";
        stanDuration = 5f;
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
        if (rb != null && !IsOnGround(enemyModel))
        {
            // Y軸制約を解除して落下可能にする.
            rb.constraints &= ~RigidbodyConstraints2D.FreezePositionY;
            // 急速落下開始（重力のみだと遅いため初速を付与）.
            rb.linearVelocity = new Vector2(0f, -20f);

            Debug.Log($"[MeteorDropStan] 空中検知 → 落下開始");

            // 着地待ちループ.
            float timeout = 3f;
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                if (enemyModel == null) return;
                if (IsOnGround(enemyModel)) break;
                await UniTask.Yield();
                elapsed += Time.deltaTime;
            }

            // 着地: 速度をゼロに.
            if (rb != null) rb.linearVelocity = Vector2.zero;
            Debug.Log($"[MeteorDropStan] 着地確認 (elapsed: {elapsed:F2}s)");
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

    /// <summary>
    /// 足元レイキャストで着地判定.
    /// </summary>
    private bool IsOnGround(EnemyModel_abstract enemyModel)
    {
        var transform = enemyModel?.Presenter?.transform;
        if (transform == null) return true;

        // レイヤーマスク初期化.
        if (groundLayerMask == -1)
        {
            int platformLayer = LayerMask.NameToLayer("Platform");
            int defaultLayer = LayerMask.NameToLayer("Default");
            groundLayerMask = (1 << platformLayer) | (1 << defaultLayer);
        }

        // 足元座標を算出.
        Vector2 feetPos;
        var col = enemyModel.GetComponent<Collider2D>();
        if (col != null)
        {
            feetPos = new Vector2(col.bounds.center.x, col.bounds.min.y);
        }
        else
        {
            feetPos = (Vector2)transform.position;
        }

        RaycastHit2D hit = Physics2D.Raycast(feetPos, Vector2.down, groundCheckDistance, groundLayerMask);
        return hit.collider != null;
    }
}
