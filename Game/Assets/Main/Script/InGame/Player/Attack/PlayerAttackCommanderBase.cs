using System;
using System.Collections.Generic;
using UnityEngine;
using Common;
using VContainer;
using InGame.Player;
using InGame.Player.Animation;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Audio;


namespace InGame.Player
{
    /// <summary>
    /// プレイヤー攻撃コマンド管理
    /// </summary>
    public class PlayerAttackCommanderBase
    {
        public PlayerAttackCommanderBase(GameObject _avator)
        {
            avator = _avator;
        }

        private GameObject avator;

        public void SetCommands()
        {

        }

        // SE管理用.
        private SEClipRegistry seRegistry;
        private SEPlayer sePlayer;

        [Inject]
        public PlayerAttackCommanderBase(PlayerControllModel _playerModel)
        {
            playerModel = _playerModel;
            inputs = InputSystemActionsManager.Instance().GetInputSystem_Actions();

            InitializeDefaultCommands();
            InitializeSE().Forget();
        }

        /// <summary>
        /// SE初期化.
        /// </summary>
        private async UniTaskVoid InitializeSE()
        {
            // SEClipRegistryを作成してアクション名とAudioClip名を登録.
            seRegistry = new SEClipRegistry();
            seRegistry.Register("AttackSwing", "SE_AttackSwing");
            seRegistry.Register("AttackMiss", "SE_AttackMiss");
            seRegistry.Register("AttackHit", "SE_AttackHit");
            seRegistry.Register("IaiAttack", "SE_IaiAttack");

            // SEPlayerを持つGameObjectを生成.
            sePlayer = SEPlayer.Create("PlayerAttackSE");

            // AddressablesからAudioClipを読み込み.
            await sePlayer.LoadClipsAsync("SE_AttackSwing", "SE_AttackMiss", "SE_AttackHit", "SE_IaiAttack");

            // 各攻撃コマンドにSEを設定.
            foreach (var kvp in attackCommands)
            {
                kvp.Value.SetSE(seRegistry, sePlayer);
            }
        }

        /// <summary>
        /// デフォルト攻撃を初期化
        /// </summary>
        private void InitializeDefaultCommands()
        {
            //SetAttack(new FirstDefault(), "FirstAttack", inputs.CharacterController.FirstAttack);
            SetAttack(new NormalAttackDefault(), "FirstAttack", inputs.CharacterController.FirstAttack);
            //SetAttack(new ShotDefault(), "SecondAttack", inputs.CharacterController.SecondAttack);
            // SetAttack(new SpecialDefault(), "SpecialAttack", inputs.CharacterController.SpecialAttack);
            // SetAttack(new RestrainDefault(), "RestrainAttack", inputs.CharacterController.RestrainAttack);

            // 居合攻撃（パリィ成功時・HeartResist終了時に発動）.
            SetAttackWithoutAction(new IaiAttack(), "IaiAttack");

            // Jak連撃コンボ攻撃（アニメーションはPresenter側で制御）.
            SetAttackWithoutAction(new JakComboAttack(), "JakComboAttack");
        }

        /// <summary>
        /// InputAction なしで攻撃コマンドを登録（コードからのみ実行）.
        /// </summary>
        public void SetAttackWithoutAction<T>(T command, string key) where T : AbstructAttackBase
        {
            command.Inject(playerModel);
            attackCommands[key] = command;
        }
        /// <summary>
        /// 攻撃コマンドを登録
        /// </summary>
        public void SetAttack<T>(T command, string key, UnityEngine.InputSystem.InputAction action) where T : AbstructAttackBase
        {
            command.SetAction(action);
            command.Inject(playerModel);

            // コライダーは動的生成に変更したため、Addressables読み込みは不要.
            // LoadColliderAsync(key).Forget();

            attackCommands[key] = command;
        }

        /// <summary>
        /// コライダーを Addressable から読み込み、playerModel の avator を親として生成.
        /// </summary>
        private async UniTaskVoid LoadColliderAsync(string key)
        {

            string colliderAddress = key;
            AsyncOperationHandle<GameObject> handle = Addressables.LoadAssetAsync<GameObject>(colliderAddress);

            await handle.Task;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                // playerModel.avator を親として生成.
                GameObject colliderObj = UnityEngine.Object.Instantiate(handle.Result, avator.transform);
                colliderObj.SetActive(false);
                attackColliders[key] = colliderObj;

                // ハンドルを保持して後で解放できるようにする.
                colliderHandles[key] = handle;

                // 対応する攻撃コマンドにコライダーを設定.
                if (attackCommands.TryGetValue(key, out var command))
                {
                    command.SetColl(colliderObj);
                }
            }
            else
            {
                Debug.LogWarning($"コライダー '{colliderAddress}' の読み込みに失敗しました.");
                Addressables.Release(handle);
            }
        }

        private readonly PlayerControllModel playerModel;
        private readonly InputSystem_Actions inputs;

        
        //固定枠にするのであれば。
        private AbstructAttackBase normalAttack;
        private AbstructAttackBase secondAttack;
        private AbstructAttackBase specialAttack;
        private AbstructAttackBase restrainAttack;


        // 攻撃コマンドを Dictionary で管理.
        private readonly Dictionary<string, AbstructAttackBase> attackCommands = new();

        // コライダー管理.
        private readonly Dictionary<string, GameObject> attackColliders = new();

        // Addressable ハンドル管理（解放用）.
        private readonly Dictionary<string, AsyncOperationHandle<GameObject>> colliderHandles = new();

       
        /// <summary>
        /// 攻撃コマンドを実行
        /// </summary>
        public void ExecuteAttack(string key)
        {
            if (attackCommands.TryGetValue(key, out var cmd))
            {
                if (cmd.CanExecute())
                    cmd.Act();
            }
            else
            {
                Debug.LogWarning($"Attack command '{key}' is not registered.");
            }
        }

        /// <summary>
        /// Jak連撃コンボのコンボ番号を設定.
        /// </summary>
        public void SetJakComboNumber(int comboNumber)
        {
            if (attackCommands.TryGetValue("JakComboAttack", out var cmd) && cmd is JakComboAttack jakCombo)
            {
                jakCombo.SetComboNumber(comboNumber);
            }
        }

        /// <summary>
        /// コライダー登録（必要に応じて）
        /// </summary>
        public void RegisterCollider(string key, GameObject collider)
        {
            attackColliders[key] = collider;
            collider.SetActive(false);
        }

        /// <summary>
        /// 攻撃用コライダーをアクティブ化して時間後に無効化.
        /// </summary>
        public void ActivateCollider(string key, float duration = 0.2f)
        {
            if (attackColliders.TryGetValue(key, out var col))
            {
                col.SetActive(true);
                UniTask.Delay(TimeSpan.FromSeconds(duration)).ContinueWith(() => col.SetActive(false)).Forget();
            }
        }

        /// <summary>
        /// Addressable リソースを解放.
        /// </summary>
        public void ReleaseAllColliders()
        {
            foreach (var kvp in colliderHandles)
            {
                if (kvp.Value.IsValid())
                {
                    Addressables.Release(kvp.Value);
                }
            }
            colliderHandles.Clear();

            foreach (var kvp in attackColliders)
            {
                if (kvp.Value != null)
                {
                    UnityEngine.Object.Destroy(kvp.Value);
                }
            }
            attackColliders.Clear();

            // SEリソースを解放.
            ReleaseSE();
        }

        /// <summary>
        /// SEリソースを解放.
        /// </summary>
        public void ReleaseSE()
        {
            if (sePlayer != null)
            {
                sePlayer.ReleaseAll();
                UnityEngine.Object.Destroy(sePlayer.gameObject);
                sePlayer = null;
            }
            seRegistry?.Clear();
            seRegistry = null;
        }
    }

    /// <summary>
    /// 攻撃コマンド共通基底.
    /// </summary>
    public abstract class AbstructAttackBase
    {
        protected PlayerControllModel playerModel;
        protected UnityEngine.InputSystem.InputAction action;

        // コライダーオブジェクト.
        protected GameObject Coll;

        // アニメーションコントローラー.
        protected IPlayerAnimation playerAnimation;

        // 当たり判定システム.
        protected PlayerColliderStatus colliderStatus;
        protected PlayerColliderState_abstract colliderState;
        protected PlayerAttackHitDetector hitDetector;

        // SE再生用.
        protected SEClipRegistry seRegistry;
        protected SEPlayer sePlayer;

        /// <summary>
        /// コライダーを設定.
        /// </summary>
        public void SetColl(GameObject collider)
        {
            Coll = collider;
            Coll.SetActive(false);
        }

        /// <summary>
        /// コライダーを取得.
        /// </summary>
        public GameObject GetColl() => Coll;

        /// <summary>
        /// アニメーションを設定.
        /// </summary>
        public void SetAnimation(IPlayerAnimation animation)
        {
            playerAnimation = animation;
        }

        /// <summary>
        /// コライダー生成初期化（派生クラスでオーバーライド可能）.
        /// </summary>
        public virtual void InitializeColliders()
        {
            // 基底クラスでは何もしない.
            // 派生クラスで必要に応じてコライダーの生成・初期化処理を実装.
        }

        public void SetAction(UnityEngine.InputSystem.InputAction _action)
        {
            action = _action;
        }

        public void Inject(PlayerControllModel model)
        {
            playerModel = model;
        }

        /// <summary>
        /// SE再生用のコンポーネントを設定.
        /// </summary>
        public void SetSE(SEClipRegistry registry, SEPlayer player)
        {
            seRegistry = registry;
            sePlayer = player;
        }

        /// <summary>
        /// SEを再生.
        /// </summary>
        protected void PlaySE(string actionName)
        {
            if (sePlayer != null && seRegistry != null)
            {
                sePlayer.PlayByAction(seRegistry, actionName);
            }
        }

        /// <summary>
        /// 実行可能か判定.
        /// </summary>
        public virtual bool CanExecute()
        {
            return playerModel != null && !playerModel.enableAction;
        }

        public abstract void Act();

        /// <summary>
        /// 攻撃アニメーション再生.
        /// </summary>
        protected void PlayAttackAnimation()
        {
            playerAnimation?.PlayAttack();
            //Debug.Log("[AbstructAttackBase] PlayAttackAnimation - NormalAttackDefault trigger.");
        }

        /// <summary>
        /// Animation Event ベースの当たり判定有効化.
        /// AnimEvent_HitStart で判定開始、AnimEvent_HitEnd で判定終了.
        /// Animation Event が未設定の場合はフォールバックとして即時開始.
        /// </summary>
        /// <param name="parent">コライダーの親Transform.</param>
        /// <param name="damage">ダメージ量.</param>
        /// <param name="duration">判定の最大持続時間（AnimEvent_HitEnd のフォールバック）.</param>
        /// <param name="offset">コライダーオフセット.</param>
        /// <param name="size">コライダーサイズ.</param>
        /// <param name="startTimeoutSec">AnimEvent_HitStart のタイムアウト秒（超えたら即時開始）.</param>
        /// <param name="onHitCallback">ヒット時コールバック.</param>
        /// <param name="attackType">攻撃タイプ.</param>
        /// <returns>ヒット数.</returns>
        protected async UniTask<int> ActivateHitDetectionOnAnimEvent(
            Transform parent,
            float damage,
            float duration,
            Vector2 offset,
            Vector2 size,
            float startTimeoutSec = 0.5f,
            Action onHitCallback = null,
            PlayerAttackType attackType = PlayerAttackType.None)
        {
            // AnimEvent_HitStart 待機用.
            var hitStartTcs = new UniTaskCompletionSource();
            var hitEndTcs = new UniTaskCompletionSource();

            // コールバック登録.
            playerAnimation?.RegisterHitDetectionCallbacks(
                onStart: () => hitStartTcs.TrySetResult(),
                onEnd: () => hitEndTcs.TrySetResult()
            );

            try
            {
                // AnimEvent_HitStart を待機（タイムアウト付き）.
                // タイムアウト時（Animation Event 未設定）は即座にhit判定を開始.
                var startResult = await UniTask.WhenAny(
                    hitStartTcs.Task,
                    UniTask.Delay(TimeSpan.FromSeconds(startTimeoutSec))
                );

                if (startResult == 0)
                {
                    Debug.Log("[AttackBase] AnimEvent_HitStart 検出 → hit判定開始");
                }
                else
                {
                    Debug.LogWarning("[AttackBase] AnimEvent_HitStart タイムアウト → フォールバック即時開始");
                }

                // ここから当たり判定開始.
                var localColliderStatus = new PlayerColliderStatus
                {
                    parentTransform = parent,
                    damage = damage,
                    duration = duration
                };
                localColliderStatus.colliderSettings.Add(new PlayerColliderSetting(PlayerColliderType.Box, offset, size));

                var localColliderState = new PlayerColliderState_EnemyDamage();
                localColliderState.SetColliderStatus(localColliderStatus);
                localColliderState.ClearHitTargets();
                localColliderState.ResetHitCount();

                if (attackType != PlayerAttackType.None)
                {
                    localColliderState.SetAttackType(attackType);
                }

                // ヒットコールバック設定.
                bool hitCallbackFired = false;
                if (onHitCallback != null)
                {
                    localColliderState.SetOnHitCallback(() =>
                    {
                        if (!hitCallbackFired)
                        {
                            hitCallbackFired = true;
                            onHitCallback.Invoke();
                        }
                    });
                }

                // ヒット検出コンポーネント追加.
                PlayerAttackHitDetector localHitDetector = parent.gameObject.AddComponent<PlayerAttackHitDetector>();
                localHitDetector.Initialize(localColliderState, localColliderStatus.colliderSettings, parent);

                try
                {
                    // AnimEvent_HitEnd またはタイムアウトを待機.
                    await UniTask.WhenAny(
                        hitEndTcs.Task,
                        UniTask.Delay(TimeSpan.FromSeconds(duration))
                    );
                }
                finally
                {
                    // ヒット検出コンポーネント削除.
                    if (localHitDetector != null)
                    {
                        UnityEngine.Object.Destroy(localHitDetector);
                    }
                }

                return localColliderState.GetHitCount();
            }
            finally
            {
                // コールバッククリア.
                playerAnimation?.ClearHitDetectionCallbacks();
            }
        }

        // ZanHitController キャッシュ.
        private ZanHitController cachedZanController;

        /// <summary>
        /// ZanHitController を取得（キャッシュ付き）.
        /// </summary>
        private ZanHitController GetZanController()
        {
            if (cachedZanController != null) return cachedZanController;
            if (playerModel == null) return null;
            var avator = playerModel.GetAvator();
            if (avator == null) return null;
            cachedZanController = avator.GetComponentInChildren<ZanHitController>(true);
            return cachedZanController;
        }

        /// <summary>
        /// Zan スプライト形状ベースの当たり判定有効化.
        /// PolygonCollider2D でスプライトの見た目通りのヒット検出を行う.
        /// パルスゲージに連動してZanのスケールを更新する.
        /// </summary>
        protected async UniTask<int> ActivateZanHitDetection(
            Transform parent,
            float damage,
            float duration,
            float startTimeoutSec = 0.02f,
            Action onHitCallback = null,
            PlayerAttackType attackType = PlayerAttackType.None)
        {
            var zanController = GetZanController();
            if (zanController == null)
            {
                Debug.LogWarning("[AttackBase] ZanHitController が見つかりません。フォールバック: Box判定");
                // フォールバック: 従来のBox判定.
                return await ActivateHitDetectionOnAnimEvent(
                    parent, damage, duration,
                    new Vector2(-1.75f, 0f), new Vector2(2.5f, 2f),
                    startTimeoutSec, onHitCallback, attackType);
            }

            // パルスゲージと攻撃タイプに応じてZanスケール更新.
            float pulse = PlayerManager.Instance().pulseModel.GetPulseGauge();
            zanController.UpdateScale(pulse, attackType);

            // AnimEvent 待機用.
            var hitStartTcs = new UniTaskCompletionSource();
            var hitEndTcs = new UniTaskCompletionSource();

            playerAnimation?.RegisterHitDetectionCallbacks(
                onStart: () => hitStartTcs.TrySetResult(),
                onEnd: () => hitEndTcs.TrySetResult()
            );

            try
            {
                // AnimEvent_HitStart 待機.
                var startResult = await UniTask.WhenAny(
                    hitStartTcs.Task,
                    UniTask.Delay(TimeSpan.FromSeconds(startTimeoutSec))
                );

                if (startResult == 0)
                {
                    Debug.Log("[AttackBase] Zan: AnimEvent_HitStart 検出");
                }

                // コライダーステート作成（ヒット処理用）.
                var localColliderStatus = new PlayerColliderStatus
                {
                    parentTransform = parent,
                    damage = damage,
                    duration = duration
                };

                var localColliderState = new PlayerColliderState_EnemyDamage();
                localColliderState.SetColliderStatus(localColliderStatus);
                localColliderState.ClearHitTargets();
                localColliderState.ResetHitCount();

                if (attackType != PlayerAttackType.None)
                {
                    localColliderState.SetAttackType(attackType);
                }

                bool hitCallbackFired = false;
                if (onHitCallback != null)
                {
                    localColliderState.SetOnHitCallback(() =>
                    {
                        if (!hitCallbackFired)
                        {
                            hitCallbackFired = true;
                            onHitCallback.Invoke();
                        }
                    });
                }

                // Zan 表示 + コライダー有効化（攻撃タイプでカラー切替）.
                zanController.Show(attackType);

                try
                {
                    // ヒット検出ループ（AnimEvent_HitEnd or タイムアウトまで）.
                    bool ended = false;
                    float elapsed = 0f;

                    // hitEnd監視タスク.
                    UniTask.WhenAny(
                        hitEndTcs.Task,
                        UniTask.Delay(TimeSpan.FromSeconds(duration))
                    ).ContinueWith(_ => ended = true).Forget();

                    while (!ended)
                    {
                        // OverlapCollider でEnemy検出.
                        var hits = zanController.DetectEnemies();
                        foreach (var hitCollider in hits)
                        {
                            if (hitCollider == null) continue;
                            // EnemyPresenter を持つオブジェクトを探す.
                            var enemyPresenter = hitCollider.GetComponent<EnemyPresenter_abstract>();
                            if (enemyPresenter == null)
                            {
                                enemyPresenter = hitCollider.GetComponentInParent<EnemyPresenter_abstract>();
                            }
                            if (enemyPresenter != null)
                            {
                                localColliderState.TryProcessHit(enemyPresenter.gameObject, hitCollider);
                            }
                        }

                        elapsed += Time.deltaTime;
                        if (elapsed >= duration) break;
                        await UniTask.Yield();
                    }
                }
                finally
                {
                    // Zan 非表示 + コライダー無効化.
                    zanController.Hide();
                }

                return localColliderState.GetHitCount();
            }
            finally
            {
                playerAnimation?.ClearHitDetectionCallbacks();
            }
        }

        /// <summary>
        /// 当たり判定を有効化して一定時間後に無効化.
        /// </summary>
        protected async UniTask ActivateHitDetection(Transform parent, float damage, float duration, Vector2 offset, Vector2 size)
        {
            // コライダーステータス作成.
            colliderStatus = new PlayerColliderStatus
            {
                parentTransform = parent,
                damage = damage,
                duration = duration
            };
            colliderStatus.colliderSettings.Add(new PlayerColliderSetting(PlayerColliderType.Box, offset, size));

            // コライダーステート作成.
            colliderState = new PlayerColliderState_EnemyDamage();
            colliderState.SetColliderStatus(colliderStatus);
            colliderState.ClearHitTargets();

            // ヒット検出コンポーネント追加（Physics2D.Overlap方式）.
            hitDetector = parent.gameObject.AddComponent<PlayerAttackHitDetector>();
            hitDetector.Initialize(colliderState, colliderStatus.colliderSettings, parent);

            //Debug.Log($"[AbstructAttackBase] ActivateHitDetection - Duration: {duration}s.");

            // 持続時間待機.
            await UniTask.Delay(TimeSpan.FromSeconds(duration));

            // ヒット検出コンポーネント削除.
            if (hitDetector != null)
            {
                UnityEngine.Object.Destroy(hitDetector);
                hitDetector = null;
            }

            //Debug.Log("[AbstructAttackBase] ActivateHitDetection - 終了.");
        }
    }

    /// <summary>
    /// デフォルト通常攻撃コマンド.
    /// </summary>
    public class NormalAttackDefault : AbstructAttackBase
    {
        // 強攻撃: 1.2倍.
        private float attackMultiplier = 1.2f;
        private float duration = 0.3f;

        public override void Act()
        {
            if (playerModel == null || playerModel.GetAvator() == null) return;

            GameObject avator = playerModel.GetAvator();

            // 基礎攻撃力 × strengthRate × 技倍率でダメージ計算.
            var statusModel = PlayerManager.Instance().playerStatusModel;
            float damage = statusModel.strength * statusModel.strengthRate * attackMultiplier;

            // 攻撃振りSE再生（攻撃開始時）.
            PlaySE("AttackSwing");

            // animator.trigger "NormalAttackDefault" を実行.
            Animator animator = avator.GetComponent<Animator>();
            if (animator != null)
            {
                animator.SetTrigger("NormalAttackDefault");
            }

            // Zan スプライト形状ベースの当たり判定有効化.
            ActivateHitDetectionAsync(avator.transform, damage).Forget();
        }

        private async UniTask ActivateHitDetectionAsync(Transform parent, float damage)
        {
            bool sePlayedHit = false;

            int hitCount = await ActivateZanHitDetection(
                parent, damage, duration,
                startTimeoutSec: 0.02f,
                onHitCallback: () =>
                {
                    if (!sePlayedHit)
                    {
                        sePlayedHit = true;
                        PlaySE("AttackHit");

                        // 強攻撃ヒット時: 吸収ゲージポイント2付与.
                        var drainModel = PlayerManager.Instance().drainModel;
                        drainModel?.Increment(2);
                    }
                }
            );

            // ミス判定.
            if (hitCount == 0)
            {
                PlaySE("AttackMiss");
                PlayerManager.Instance().pulseModel.OnAttackMiss();
            }

            // 攻撃終了トリガー.
            Animator endAnimator = parent.GetComponent<Animator>();
            if (endAnimator != null)
            {
                endAnimator.SetTrigger("NormalAttackDefaultEnd");
            }
        }
    }

    /// <summary>
    /// Jak連撃コンボ攻撃コマンド（アニメーションはPresenter側で制御、当たり判定とSEのみ）.
    /// </summary>
    public class JakComboAttack : AbstructAttackBase
    {
        // コンボ番号ごとの技倍率（弱攻撃_1:0.25, 弱攻撃_2:0.45, 弱攻撃_3:1.05）.
        private static readonly float[] attackMultipliers = { 0.25f, 0.45f, 1.05f };
        private float duration = 0.3f;

        // 現在のコンボ番号（0:弱攻撃_1, 1:弱攻撃_2, 2:弱攻撃_3）.
        private int currentComboNumber = 0;

        // コンボ番号ごとの吸収ゲージポイント.
        private static readonly int[] drainGaugePoints = { 5, 10, 15 };

        /// <summary>
        /// コンボ番号を設定.
        /// </summary>
        public void SetComboNumber(int comboNumber)
        {
            currentComboNumber = comboNumber;
        }

        public override bool CanExecute()
        {
            // コードから直接呼ばれるため常に実行可能.
            return playerModel != null;
        }

        public override void Act()
        {
            if (playerModel == null || playerModel.GetAvator() == null) return;

            GameObject avator = playerModel.GetAvator();

            // 基礎攻撃力 × strengthRate × コンボ段ごとの技倍率でダメージ計算.
            var statusModel = PlayerManager.Instance().playerStatusModel;
            float multiplier = (currentComboNumber >= 0 && currentComboNumber < attackMultipliers.Length)
                ? attackMultipliers[currentComboNumber]
                : attackMultipliers[0];
            float damage = statusModel.strength * statusModel.strengthRate * multiplier;

            // 攻撃振りSE再生（攻撃開始時）.
            PlaySE("AttackSwing");

            // アニメーションはPresenter側で制御するため、ここでは設定しない.

            // Zan スプライト形状ベースの当たり判定有効化.
            ActivateHitDetectionAsync(avator.transform, damage).Forget();
        }

        private async UniTask ActivateHitDetectionAsync(Transform parent, float damage)
        {
            bool sePlayedHit = false;

            // ヒット時に付与するゲージポイントを取得.
            int gaugePoints = (currentComboNumber >= 0 && currentComboNumber < drainGaugePoints.Length)
                ? drainGaugePoints[currentComboNumber]
                : 1;

            int hitCount = await ActivateZanHitDetection(
                parent, damage, duration,
                startTimeoutSec: 0.02f,
                onHitCallback: () =>
                {
                    if (!sePlayedHit)
                    {
                        sePlayedHit = true;
                        PlaySE("AttackHit");

                        // 吸収ゲージポイント付与.
                        var drainModel = PlayerManager.Instance().drainModel;
                        drainModel?.Increment(gaugePoints);
                    }
                },
                attackType: PlayerAttackType.Weak
            );

            // ミス判定.
            if (hitCount == 0)
            {
                PlaySE("AttackMiss");
                PlayerManager.Instance().pulseModel.OnAttackMiss();
            }
        }
    }

    /// <summary>
    /// 居合攻撃コマンド（パリィ成功時・HeartResist終了時に発動）.
    /// 鼓動ゲージが1減るごとに倍率0.025加算、最大2.5倍.
    /// </summary>
    public class IaiAttack : AbstructAttackBase
    {
        // 攻撃パラメータ.
        // 居合: 鼓動ゲージベースで倍率計算 (最大2.5倍、基礎攻撃力に上乗せ).
        private float duration = 0.4f;

        /// <summary>
        /// 居合ダメージ計算: 基礎攻撃力 × (1 + 鼓動ボーナス).
        /// 鼓動ボーナス = min(2.5, max(0, (100 - 現在鼓動) × 0.025)).
        /// </summary>
        private float CalculateIaiDamage()
        {
            var statusModel = PlayerManager.Instance().playerStatusModel;
            float baseAttack = statusModel.strength * statusModel.strengthRate;
            float currentPulse = PlayerManager.Instance().pulseModel.GetPulseGauge();

            // 鼓動が100から減った分 × 0.025、最大2.5倍ボーナス.
            float pulseReduction = Mathf.Max(0f, 100f - currentPulse);
            float bonusMultiplier = Mathf.Min(2.5f, pulseReduction * 0.025f);

            // 基礎攻撃力に上乗せ (加算式) × 居合ダメージ倍率3倍.
            return baseAttack * (1f + bonusMultiplier) * 3f;
        }

        public override bool CanExecute()
        {
            // 居合はコードから直接呼ばれるため常に実行可能.
            return playerModel != null;
        }

        public override void Act()
        {
            if (playerModel == null || playerModel.GetAvator() == null) return;

            GameObject avator = playerModel.GetAvator();

            // 居合ダメージ計算 (鼓動ゲージベース).
            float damage = CalculateIaiDamage();

            // animator.trigger "Iai" を実行.
            Animator animator = avator.GetComponent<Animator>();
            if (animator != null)
            {
                animator.SetTrigger("Iai");
            }

            Debug.Log($"[IaiAttack] Act - 居合攻撃実行 Damage: {damage}");

            // 居合SE再生.
            PlaySE("IaiAttack");

            // Zan スプライト形状ベースの当たり判定有効化.
            ActivateHitDetectionAsync(avator.transform, damage).Forget();
        }

        private async UniTask ActivateHitDetectionAsync(Transform parent, float damage)
        {
            await ActivateZanHitDetection(
                parent, damage, duration,
                startTimeoutSec: 0.02f,
                onHitCallback: () =>
                {
                    // 居合ヒット時: 吸収ゲージポイント10付与.
                    var drainModel = PlayerManager.Instance().drainModel;
                    drainModel?.Increment(10);
                },
                attackType: PlayerAttackType.Iai
            );
            // 居合はヒット/ミス判定なし（パリィ・HeartResist成功報酬のため）.

            // 居合攻撃終了トリガー.
            Animator endAnimator = parent.GetComponent<Animator>();
            if (endAnimator != null)
            {
                endAnimator.SetTrigger("IaiEnd");
            }
        }
    }


    /// <summary>
    /// 攻撃コマンド保存モデル
    /// </summary>
    public class PlayerAttackCommmanderDataModel
    {
        public PlayerAttackCommmanderDataModel()
        {
            // 初期化
            commandPath = new CommandPath();
            commandBase = new Dictionary<string, AbstructAttackBase>();
        }

        // CommandPathを保持
        public CommandPath commandPath;

        // 攻撃コマンドを保存するDictionary
        public Dictionary<string, AbstructAttackBase> commandBase;

        /// <summary>
        /// デフォルトパスを設定
        /// </summary>
        public void DefaultPathSet()
        {
            commandPath.normalAttackPath = "baseNormalAttack";
            commandPath.secondAttackPath = "baseSecondAttack";
            commandPath.specialAttackPath = "baseSpecialAttack";
            commandPath.restrainAttackPath = "baseRestrainAttack";
        }

        /// <summary>
        /// コマンドを返す
        /// </summary>
        public AbstructAttackBase GetCommand(string path)
        {
            return commandBase[path];
        }

        // データの保存処理（JSONで保存）
        public void Save(string filePath)
        {
            string json = JsonUtility.ToJson(commandPath);
            System.IO.File.WriteAllText(filePath, json);
        }

        // データのロード処理（JSONからロード）
        public void Load(string filePath)
        {
            if (System.IO.File.Exists(filePath))
            {
                string json = System.IO.File.ReadAllText(filePath);
                commandPath = JsonUtility.FromJson<CommandPath>(json);
            }
        }
    }
    /// <summary>
    /// 攻撃コマンドのパスを管理するクラス
    /// </summary>
    [System.Serializable]
    public class CommandPath
    {
        public string normalAttackPath = "baseNormalAttack";
        public string secondAttackPath = "baseSecondAttack";
        public string specialAttackPath = "baseSpecialAttack";
        public string restrainAttackPath = "baseRestrainAttack";
    }
}
