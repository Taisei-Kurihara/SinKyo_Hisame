using UnityEngine;
using Cysharp.Threading.Tasks;
using InGame.Player;

/// <summary>
/// Wendigo バーサーク専用行動: メテオドロップ.
/// プレイヤーから離れてステージ外に退避→画面外から繰り返し急降下攻撃を行う.
/// フロー: 退避Rush → (テレポート上部 → 急落下 → 着地Howling → ジャンプ上部) × 3〜4回.
/// </summary>
public class EnemState_Wendig_MeteorDrop : EnemState_abstract
{
    // 退避Rush設定.
    private float rushSpeed = 20f;
    private float offScreenMargin = 5f;

    // 急降下設定.
    private float teleportHeight = 15f;
    private float fallInitialSpeed = 48.5f;
    private float groundCheckDistance = 0.5f;

    // ジャンプ設定.
    private float jumpUpSpeed = 25f;

    // ループ設定.
    private int minLoopCount = 3;
    private int maxLoopCount = 4;

    // 着地判定用レイヤーマスク.
    private int groundLayerMask = -1;

    // ライフサイクル間共有データ.
    private Rigidbody2D rb;
    private Transform ownerTransform;
    private Animator animator;
    private Collider2D mainColl;
    private RigidbodyConstraints2D originalConstraints;
    private bool originalIsTrigger;
    private bool stateModified = false;
    private AfterimageEffect afterimageEffect;
    private EnemyModel_Wendig ownerWendigModel;

    public EnemState_Wendig_MeteorDrop()
    {
        postActionWaitFrames = 60;
    }

    protected override async UniTask OnPreAction(EnemyModel_abstract enemyModel)
    {
        stateModified = false;

        if (!EnemNullSafetyHelper.IsValidWithAnimator(enemyModel)) { isAborted = true; return; }

        rb = enemyModel.Rigidbody;
        ownerTransform = enemyModel.Presenter.transform;
        animator = enemyModel.Animator;
        mainColl = enemyModel.Presenter.MainColl;
        ownerWendigModel = enemyModel as EnemyModel_Wendig;

        if (rb == null || ownerTransform == null) { isAborted = true; return; }

        // レイヤーマスク初期化（JumpSlash同様: Platform | Default）.
        if (groundLayerMask == -1)
        {
            int platformLayer = LayerMask.NameToLayer("Platform");
            groundLayerMask = (1 << platformLayer) | (1 << LayerMask.NameToLayer("Default"));
        }

        // 元の状態を保存.
        originalConstraints = rb.constraints;
        originalIsTrigger = mainColl != null ? mainColl.isTrigger : false;

        // ジャンプ中はMove=2でIdol/Walk遷移をブロック.
        animator.SetInteger("Move", 2);

        Debug.Log($"[MeteorDrop] OnPreAction開始 - {ownerTransform.gameObject.name}");
    }

    protected override async UniTask OnAction(EnemyModel_abstract enemyModel)
    {
        if (!EnemNullSafetyHelper.IsValid(enemyModel)) { isAborted = true; return; }

        // === Phase1: 退避Rush ===
        Debug.Log("[MeteorDrop] Phase1: 退避Rush開始");
        await ExecuteRetreatRush(enemyModel);
        if (isAborted) return;

        // === Phase2: 画面外インジケーター「?m」モードON ===
        var indicator = enemyModel.Presenter.OffScreenIndicator;
        indicator?.SetUnknownMode(true);
        Debug.Log("[MeteorDrop] Phase2: インジケーター不明モードON");

        // === Phase3: 急降下ループ ===
        int loopCount = Random.Range(minLoopCount, maxLoopCount + 1);
        Debug.Log($"[MeteorDrop] Phase3: 急降下ループ開始 (回数: {loopCount})");

        for (int i = 0; i < loopCount; i++)
        {
            if (!EnemNullSafetyHelper.IsValid(enemyModel)) { isAborted = true; break; }

            Debug.Log($"[MeteorDrop] ループ {i + 1}/{loopCount} 開始");

            // 3a. プレイヤー上方にテレポート.
            TeleportAbovePlayer();

            // 少し待機（テレポート直後の安定化）.
            await UniTask.Delay(200);
            if (!EnemNullSafetyHelper.IsValid(enemyModel)) { isAborted = true; break; }

            // 3b. 急落下.
            await ExecuteFall(enemyModel);
            if (isAborted) break;

            // 3c. 着地後ハウリング.
            if (!EnemNullSafetyHelper.IsValid(enemyModel)) { isAborted = true; break; }

            // Y軸制約を復元してからHowling.
            rb.constraints = originalConstraints;
            if (mainColl != null) mainColl.isTrigger = originalIsTrigger;

            if (ownerWendigModel != null)
            {
                Debug.Log($"[MeteorDrop] ループ {i + 1}/{loopCount} ハウリング実行");
                await ownerWendigModel.TriggerHowling();
            }

            if (!EnemNullSafetyHelper.IsValid(enemyModel)) { isAborted = true; break; }

            // 3d. 最後のループ以外はジャンプで画面外上部に戻る.
            if (i < loopCount - 1)
            {
                Debug.Log($"[MeteorDrop] ループ {i + 1}/{loopCount} ジャンプで上昇");
                await JumpBackUp(enemyModel);
                if (isAborted) break;
            }
        }

        // === Phase4: インジケーター通常モードに復帰 ===
        indicator?.SetUnknownMode(false);
        Debug.Log("[MeteorDrop] Phase4: インジケーター通常モード復帰");
    }

    protected override async UniTask OnAfterPostAction(EnemyModel_abstract enemyModel)
    {
        // 元の状態を復元（常に実行 — クリーンアップ保証）.
        RestoreState();

        if (EnemNullSafetyHelper.IsValid(enemyModel))
        {
            enemyModel.Presenter?.ClearAttackImminent();
            enemyModel.IsJumping = false;

            // インジケーター不明モードを確実に解除.
            enemyModel.Presenter.OffScreenIndicator?.SetUnknownMode(false);
        }

        if (EnemNullSafetyHelper.IsValidWithAnimator(enemyModel))
        {
            animator.SetBool("OnGround", true);
            animator.SetInteger("Move", 0);
        }

        Debug.Log("[MeteorDrop] OnAfterPostAction完了");
        await UniTask.CompletedTask;
    }

    // === Phase1: 退避Rush ===

    private async UniTask ExecuteRetreatRush(EnemyModel_abstract enemyModel)
    {
        if (!EnemNullSafetyHelper.IsValid(enemyModel)) { isAborted = true; return; }

        // プレイヤーと反対方向のステージ端+マージンを目標に.
        var playerScope = Object.FindFirstObjectByType<PlayerScope>();
        if (playerScope == null) { isAborted = true; return; }
        Transform playerTransform = playerScope.transform;

        float playerX = playerTransform.position.x;
        float enemyX = ownerTransform.position.x;
        float targetX = (enemyX >= playerX)
            ? enemyModel.StageMax.x + offScreenMargin
            : enemyModel.StageMin.x - offScreenMargin;

        float direction = Mathf.Sign(targetX - enemyX);
        float distance = Mathf.Abs(targetX - enemyX);
        float rushDuration = distance / rushSpeed;

        // Rush方向を向く.
        EnemFacingHelper.FaceDirection(ownerTransform, direction);

        // Y軸固定、Colliderをtriggerに.
        rb.constraints = originalConstraints | RigidbodyConstraints2D.FreezePositionY;
        if (mainColl != null) mainColl.isTrigger = true;
        stateModified = true;
        enemyModel.IsJumping = true;

        // 残像エフェクト開始.
        afterimageEffect = ownerTransform.GetComponent<AfterimageEffect>();
        if (afterimageEffect == null)
        {
            afterimageEffect = ownerTransform.gameObject.AddComponent<AfterimageEffect>();
        }
        afterimageEffect.SetColor(new Color(1f, 0.2f, 0.2f, 0.6f));
        afterimageEffect.SetFadeDuration(0.6f);
        afterimageEffect.SetSpawnInterval(0.015f);
        afterimageEffect.StartEffect();

        // 速度設定して時間待ち.
        float elapsed = 0f;
        while (elapsed < rushDuration)
        {
            if (!EnemNullSafetyHelper.IsValid(enemyModel)) { isAborted = true; break; }
            rb.linearVelocity = new Vector2(direction * rushSpeed, 0f);
            elapsed += Time.deltaTime;
            await UniTask.Yield();
        }

        rb.linearVelocity = Vector2.zero;
        afterimageEffect?.StopEffect();

        Debug.Log($"[MeteorDrop] 退避Rush完了 - pos: {ownerTransform.position}");
    }

    // === Phase3a: プレイヤー上方にテレポート ===

    private void TeleportAbovePlayer()
    {
        var playerScope = Object.FindFirstObjectByType<PlayerScope>();
        if (playerScope == null) return;

        Vector3 playerPos = playerScope.transform.position;
        ownerTransform.position = new Vector3(playerPos.x, playerPos.y + teleportHeight, 0f);
        rb.linearVelocity = Vector2.zero;

        Debug.Log($"[MeteorDrop] テレポート完了 - pos: {ownerTransform.position}");
    }

    // === Phase3b-c: 急落下 + 着地検知 ===

    private async UniTask ExecuteFall(EnemyModel_abstract enemyModel)
    {
        if (!EnemNullSafetyHelper.IsValid(enemyModel)) { isAborted = true; return; }

        // Y軸制約解除（落下のため）.
        rb.constraints = originalConstraints & ~RigidbodyConstraints2D.FreezePositionY;
        if (mainColl != null) mainColl.isTrigger = true;
        enemyModel.IsJumping = true;

        // 空中状態アニメーション.
        if (EnemNullSafetyHelper.IsValidWithAnimator(enemyModel))
        {
            animator.SetBool("OnGround", false);
            animator.SetInteger("Move", 2);
        }

        // 下向き初速を与える.
        rb.linearVelocity = new Vector2(0f, -fallInitialSpeed);

        // 着地タイミング計算.
        float gravity = Mathf.Abs(Physics2D.gravity.y * rb.gravityScale);
        if (gravity < 0.1f) gravity = 9.81f;

        // h = v0*t + 0.5*g*t^2 → t = (-v0 + sqrt(v0^2 + 2*g*h)) / g
        float v0 = fallInitialSpeed;
        float fallTime = (-v0 + Mathf.Sqrt(v0 * v0 + 2f * gravity * teleportHeight)) / gravity;

        // 計算上の着地時間の70%まで待ってから地面チェック開始.
        float checkStartTime = fallTime * 0.7f;
        await UniTask.Delay((int)(checkStartTime * 1000));

        if (!EnemNullSafetyHelper.IsValid(enemyModel)) { isAborted = true; return; }

        // 地面検知ループ.
        bool landed = false;
        float timeout = fallTime * 2f;
        float elapsed = checkStartTime;

        while (!landed && elapsed < timeout)
        {
            if (!EnemNullSafetyHelper.IsValid(enemyModel)) { isAborted = true; return; }

            Vector2 feetPos = GetFeetPosition(enemyModel);
            RaycastHit2D hit = Physics2D.Raycast(feetPos, Vector2.down, groundCheckDistance, groundLayerMask);
            if (hit.collider != null)
            {
                landed = true;
                rb.linearVelocity = Vector2.zero;

                // 着地位置補正（足元を地面に合わせる）.
                float feetOffset = feetPos.y - ownerTransform.position.y;
                ownerTransform.position = new Vector3(
                    ownerTransform.position.x,
                    hit.point.y - feetOffset,
                    ownerTransform.position.z);
            }

            await UniTask.Yield();
            elapsed += Time.deltaTime;
        }

        // タイムアウト時は強制着地.
        if (!landed)
        {
            Debug.LogWarning("[MeteorDrop] 着地タイムアウト - 強制着地");
            rb.linearVelocity = Vector2.zero;
            ownerTransform.position = new Vector3(
                ownerTransform.position.x,
                enemyModel.StageMin.y,
                ownerTransform.position.z);
        }

        // 着地アニメーション.
        if (EnemNullSafetyHelper.IsValidWithAnimator(enemyModel))
        {
            animator.SetBool("OnGround", true);
        }

        enemyModel.IsJumping = false;

        Debug.Log($"[MeteorDrop] 着地完了 - pos: {ownerTransform.position}, landed: {landed}");
    }

    // === Phase3e: ジャンプで画面外上部に戻る ===

    private async UniTask JumpBackUp(EnemyModel_abstract enemyModel)
    {
        if (!EnemNullSafetyHelper.IsValid(enemyModel)) { isAborted = true; return; }

        // Y軸制約解除.
        rb.constraints = originalConstraints & ~RigidbodyConstraints2D.FreezePositionY;
        if (mainColl != null) mainColl.isTrigger = true;
        enemyModel.IsJumping = true;

        // 空中アニメーション.
        if (EnemNullSafetyHelper.IsValidWithAnimator(enemyModel))
        {
            animator.SetBool("OnGround", false);
            animator.SetInteger("Move", 2);
        }

        // 上方に速度設定.
        rb.linearVelocity = new Vector2(0f, jumpUpSpeed);

        // 画面外に出るまで待機.
        float safetyTimeout = 3f;
        float elapsed = 0f;

        while (elapsed < safetyTimeout)
        {
            if (!EnemNullSafetyHelper.IsValid(enemyModel)) { isAborted = true; return; }

            Vector3 vp = Camera.main.WorldToViewportPoint(ownerTransform.position);
            if (vp.y > 1.2f) break;

            elapsed += Time.deltaTime;
            await UniTask.Yield();
        }

        rb.linearVelocity = Vector2.zero;
        Debug.Log($"[MeteorDrop] ジャンプ上昇完了 - pos: {ownerTransform.position}");
    }

    // === ヘルパー ===

    private Vector2 GetFeetPosition(EnemyModel_abstract enemyModel)
    {
        Collider2D col = enemyModel.GetComponent<Collider2D>();
        if (col != null)
        {
            return new Vector2(col.bounds.center.x, col.bounds.min.y);
        }
        return (Vector2)ownerTransform.position;
    }

    private void RestoreState()
    {
        if (!stateModified) return;
        if (rb != null)
        {
            rb.constraints = originalConstraints;
            rb.linearVelocity = Vector2.zero;
        }
        if (mainColl != null)
        {
            mainColl.isTrigger = originalIsTrigger;
        }
        stateModified = false;
    }
}
