using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
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

    // ShockWave (Addressables).
    private GameObject shockWavePrefab;
    private AsyncOperationHandle<GameObject> shockWaveHandle;

    public EnemState_Wendig_MeteorDrop()
    {
        postActionWaitFrames = 60;
    }

    /// <summary>外部から大技を中断する（居合ヒット時等）.</summary>
    public void RequestAbort() { isAborted = true; }

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

        // ShockWave プレハブをAddressablesで読み込み.
        try
        {
            var locHandle = Addressables.LoadResourceLocationsAsync("ShockWave");
            var locations = await locHandle;
            Addressables.Release(locHandle);
            if (locations != null && locations.Count > 0)
            {
                shockWaveHandle = Addressables.LoadAssetAsync<GameObject>("ShockWave");
                shockWavePrefab = await shockWaveHandle;
                if (shockWaveHandle.Status != AsyncOperationStatus.Succeeded)
                {
                    shockWavePrefab = null;
                    if (shockWaveHandle.IsValid()) Addressables.Release(shockWaveHandle);
                }
            }
        }
        catch (System.Exception)
        {
            shockWavePrefab = null;
        }
    }

    protected override async UniTask OnAction(EnemyModel_abstract enemyModel)
    {
        if (!EnemNullSafetyHelper.IsValid(enemyModel)) { isAborted = true; return; }

        // MeteorDrop中フラグON.
        if (enemyModel.Presenter != null) enemyModel.Presenter.IsMeteorDropActive = true;

        // 大技専用SE再生.
        enemyModel.Presenter?.PlaySE("MeteorDrop");

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

            // 空中で1秒待機してから落下（abort対応: 毎フレーム確認）.
            if (!await WaitWithAbortCheck(enemyModel, 1000)) break;

            // 3b. 急落下.
            await ExecuteFall(enemyModel);
            if (isAborted) break;

            // 3c. 着地後ハウリング.
            if (!EnemNullSafetyHelper.IsValid(enemyModel)) { isAborted = true; break; }

            // Y軸制約を復元してからHowling.
            rb.constraints = originalConstraints;

            {
                Debug.Log($"[MeteorDrop] ループ {i + 1}/{loopCount} ShockWave + 着地攻撃");

                // ShockWave パーティクル生成.
                if (shockWavePrefab != null)
                {
                    var sw = Object.Instantiate(shockWavePrefab, ownerTransform.position, Quaternion.identity);
                    sw.transform.localScale = new Vector3(5f, 5f, 1f);
                    Object.Destroy(sw, 2f);
                }

                // 着地攻撃通告（パリィ不可）.
                enemyModel.Presenter.PlayAttackWarning(false);

                // 着地攻撃ダメージ計算.
                float attackMultiplier = 1.8f;
                int landingDamage = ownerWendigModel != null
                    ? (int)(ownerWendigModel.GetCurrentAttackPower() * attackMultiplier)
                    : 90;

                // 距離ベースダメージ判定（コライダー内側問題を回避）.
                float damageRadius = 3f;
                Vector2 damageCenter = (Vector2)ownerTransform.position + new Vector2(0f, 1f);
                var landingPlayerScope = Object.FindFirstObjectByType<PlayerScope>();
                if (landingPlayerScope != null)
                {
                    float dist = Vector2.Distance(damageCenter, (Vector2)landingPlayerScope.transform.position);
                    if (dist <= damageRadius)
                    {
                        float kbDir = landingPlayerScope.transform.position.x >= ownerTransform.position.x ? 1f : -1f;
                        var damageData = new InGame.Common.DamageData(
                            landingDamage, InGame.Common.PowerlevelConst.EnemyHowling, 10f, kbDir);
                        landingPlayerScope.OnReceiveAttack(damageData);
                        Debug.Log($"[MeteorDrop] 着地攻撃ヒット (damage={landingDamage}, dist={dist:F2})");
                    }
                    else
                    {
                        Debug.Log($"[MeteorDrop] 着地攻撃ミス (dist={dist:F2} > {damageRadius})");
                    }
                }
            }

            if (!EnemNullSafetyHelper.IsValid(enemyModel)) { isAborted = true; break; }

            // 3c2. 着地後地上で1秒待機（abort対応）.
            Debug.Log($"[MeteorDrop] ループ {i + 1}/{loopCount} 着地後1sec待機");
            if (!await WaitWithAbortCheck(enemyModel, 1000)) break;

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
        // ShockWave Addressables解放.
        if (shockWaveHandle.IsValid())
        {
            Addressables.Release(shockWaveHandle);
            shockWavePrefab = null;
        }

        // 傾きを復元（中断時の安全保証）.
        if (ownerTransform != null)
        {
            ownerTransform.rotation = Quaternion.Euler(0f, ownerTransform.rotation.eulerAngles.y, 0f);
        }

        // 元の状態を復元（常に実行 — クリーンアップ保証）.
        RestoreState();

        // MeteorDrop中フラグOFF.
        if (enemyModel?.Presenter != null) enemyModel.Presenter.IsMeteorDropActive = false;

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

        // Y軸固定.
        rb.constraints = originalConstraints | RigidbodyConstraints2D.FreezePositionY;
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
        if (isAborted) return;
        if (!EnemNullSafetyHelper.IsValid(enemyModel)) { isAborted = true; return; }

        // Y軸制約解除（落下のため）.
        rb.constraints = originalConstraints & ~RigidbodyConstraints2D.FreezePositionY;
        enemyModel.IsJumping = true;

        // 空中状態アニメーション.
        if (EnemNullSafetyHelper.IsValidWithAnimator(enemyModel))
        {
            animator.SetBool("OnGround", false);
            animator.SetInteger("Move", 2);
        }

        // 下向き初速を与える.
        rb.linearVelocity = new Vector2(0f, -fallInitialSpeed);

        // 落下中は85度傾ける（頭を下方向 = ダイブ姿勢）.
        float yRot = ownerTransform.rotation.eulerAngles.y;
        float zTilt = yRot < 90f ? -85f : 85f;
        ownerTransform.rotation = Quaternion.Euler(0f, yRot, zTilt);

        // 地面検知ループ（毎フレームレイキャスト — 時間ベース待機は使わない）.
        bool landed = false;
        float timeout = 5f;
        float elapsed = 0f;

        while (!landed && elapsed < timeout)
        {
            if (isAborted) { rb.linearVelocity = Vector2.zero; return; }
            if (!EnemNullSafetyHelper.IsValid(enemyModel)) { isAborted = true; return; }

            // 回転中はcol.boundsが不正確なため、transform.positionベースで検出.
            // 上方オフセット付きレイキャストで地面に食い込んでいても検出可能.
            float upOffset = 1.0f;
            Vector2 rayOrigin = (Vector2)ownerTransform.position + Vector2.up * upOffset;

            // 落下速度に応じてレイキャスト距離を拡大（高速落下時の地面貫通防止）.
            float fallSpeed = Mathf.Abs(rb.linearVelocity.y);
            float dynamicCheckDist = Mathf.Max(groundCheckDistance + upOffset, fallSpeed * Time.deltaTime * 3f + upOffset);

            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, dynamicCheckDist, groundLayerMask);
            if (hit.collider != null)
            {
                landed = true;
                rb.linearVelocity = Vector2.zero;

                // 傾きを復元.
                ownerTransform.rotation = Quaternion.Euler(0f, ownerTransform.rotation.eulerAngles.y, 0f);

                // 着地位置補正（回転解除後の正しい足元位置で計算）.
                Vector2 correctedFeetPos = GetFeetPosition(enemyModel);
                float feetOffset = correctedFeetPos.y - ownerTransform.position.y;
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
            // 傾きを復元.
            ownerTransform.rotation = Quaternion.Euler(0f, ownerTransform.rotation.eulerAngles.y, 0f);
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
        if (isAborted) return;
        if (!EnemNullSafetyHelper.IsValid(enemyModel)) { isAborted = true; return; }

        // Y軸制約解除.
        rb.constraints = originalConstraints & ~RigidbodyConstraints2D.FreezePositionY;
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
            if (isAborted) { rb.linearVelocity = Vector2.zero; return; }
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

    /// <summary>
    /// abort対応の待機: 毎フレームisAbortedとValidityを確認しながらrealtime基準で待機.
    /// timeScale=0でも正常に動作する.
    /// </summary>
    private async UniTask<bool> WaitWithAbortCheck(EnemyModel_abstract enemyModel, int milliseconds)
    {
        float waitSec = milliseconds / 1000f;
        float elapsed = 0f;
        while (elapsed < waitSec)
        {
            if (isAborted) return false;
            if (!EnemNullSafetyHelper.IsValid(enemyModel)) { isAborted = true; return false; }
            await UniTask.Yield();
            elapsed += Time.unscaledDeltaTime;
        }
        return true;
    }

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
        stateModified = false;
    }
}
