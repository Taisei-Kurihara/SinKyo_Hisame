using Audio;
using UnityEngine;
using Common;
using System;
using Cysharp.Threading.Tasks;
using R3;
using SceneInfo;
using InGame;
using InGame.Player;
using InGame.Player.Animation;
using InGame.Common;
using Tutorial;
using LitMotion;
using LitMotion.Extensions;

namespace InGame.Player
{
    public class PlayerPresenter :IDisposable
    {
        private IPlayerView view;

        //必要変数。
        private InputSystem_Actions inputActions;

        public PlayerPresenter(DrainModel _drainModel,PlayerSearchModel _playerSearch,PlayerControllModel _playerModel,PlayerAttackCommanderBase _playerAttackCommander, IGuard _guard, GameObject _avator)
        {
            drainModel = _drainModel;
            playerSearchModel = _playerSearch;

            playerModel = _playerModel;
            playerStatusModel = PlayerManager.Instance().playerStatusModel;
            pulseModel = PlayerManager.Instance().pulseModel;

            inputActions = InputSystemActionsManager.Instance().GetInputSystem_Actions();
            this.attackCommander = _playerAttackCommander;

            // ガード初期化.
            guard = _guard;
            guard.Inject(playerModel);
            guard.SetAction(inputActions.CharacterController.Guard);

            // アニメーション取得.
            playerAnimation = _avator.GetComponent<IPlayerAnimation>();
        }

        private DrainModel drainModel;
        private PlayerSearchModel playerSearchModel;

        private IPlayerAnimation playerAnimation;

        private PlayerControllModel playerModel;
        private PlayerStatusModel playerStatusModel;
        private PulseModel pulseModel;

        private PlayerAttackCommanderBase attackCommander;

        private IGuard guard = new Guard_Player_Default();

        private bool playerControllerEnable = true;
        private bool playerActionEnable = true;

        // HeartResist状態.
        private bool isHeartResisting = false;
        private bool isStrongHeartResist = false;
        private bool wasMovingDuringHeartResist = false;
        private float heartResistCooldownEnd = 0f;
        private const float heartResistCooldown = 0.825f;
        private float heartResistStartTime = 0f;

        // 居合発動条件用.
        private float iaiReadyRatio = 0f;
        private bool iaiReadyNotified = false; // 居合発動可能通知済みフラグ.
        private const float iaiWeakRequiredSeconds = 5f;
        private const float iaiStrongRequiredSeconds = 2f;
        private const float iaiGaugeDecayDuration = 5f; // HeartResist解除後のゲージ減衰時間（秒）.

        // ポーズボタン長押し判定用.
        private bool isPoseHolding = false;
        private float poseHoldTime = 0f;
        private const float titleReturnHoldDuration = 1.5f;
        private const float shortPressThreshold = 0.3f;
        private bool isTutorialVisible = false;

        // チュートリアル出現時の移動入力無視用.
        private bool ignoreMoveUntilRelease = false;

        // 鼓動0デバフ用変数.
        private bool isPulseZero = false;
        private float pulseZeroTimer = 0f;
        private const float pulseZeroDebuffDelay = 2f;

        // 鼓動200スタン用変数.
        public static bool IsPlayerStunning { get; private set; } = false;

        /// <summary>居合発動検知フラグ（チュートリアル監視用）.</summary>
        public static bool IsIaiPerformed { get; set; } = false;
        private bool isPulseMaxStunning = false;

        // チャンス状態（Enemy大技スタン時、必殺技発動可能）.
        private bool isChanceState = false;
        private float chanceStateEndTime = 0f;
        private const float chanceStateDuration = 8f; // MeteorDropStan(5sec)+着地時間バッファ.
        // 必殺技確定勝利フラグ: 必殺技ダメージ >= 敵HP のとき true.
        // 通常勝利演出を抑制し、必殺技完了後に特殊勝利演出を発動する.
        private bool isLethalChanceAttack = false;

        // 居合hosi アニメーションループ制御.
        private System.Threading.CancellationTokenSource hosiLoopCts;
        private bool stunInterruptedByDamage = false;

        // SE用.
        private SEPlayer guardSEPlayer;

        // 回避居合い設定.
        private float iaiDodgeDistance = 5f;    // 距離条件.
        private float iaiDodgeAngle = 180f;     // 方向条件（±度、初期値は全方向有効）.
        private const float iaiDodgeTimeWindow = 0.5f; // タイミング判定窓（秒）.
        private const float iaiDodgeInvincibilityExtension = 0.5f; // パリィ不可攻撃時の無敵延長（秒）.

        // ゲームオーバー画面.
        private GameOverView gameOverView;

        // Jak連撃コンボ用変数.
        private int jakComboCount = 0;
        private float jakLastAttackTime = 0f;
        private const float jakComboResetTime = 0.33f;
        private const int jakComboMaxCount = 3;

        // 心拍エフェクト方向トラッキング.
        private float previousPulseValue = 100f;
        private int pulseDirection = 0;  // +1=上昇, -1=下降, 0=未定.
        private float pulseEffectCooldown = 0f;
        private const float pulseEffectSameDirectionInterval = 1.5f;  // 同方向の場合のクールダウン（秒）.
        private const float pulseChangeThreshold = 0.5f;  // 方向判定の閾値.

        private CompositeDisposable compositeDisposePlayer=new CompositeDisposable();

        /// <summary>
        /// Playerが生成された時の、Update処理発行関数
        /// </summary>
        public void InitalizePlayerEvents()
        {
            playerStatusModel.SetHp(1000);

            // ガード/パリィSE初期化.
            InitializeGuardSE().Forget();

            // 必殺技スラッシュエフェクトをプール初期化（スプライトロード + インスタンス事前生成）.
            ChanceAttackSlashEffect.InitializePoolAsync().Forget();

            // Player死亡条件をSceneChangeStandに登録.
            RegisterPlayerDeathCondition();

            // InGamePresenter に Enemy 長時間スタン通知コールバックを登録.
            // EnemyBattleState.StunLong に遷移した瞬間に確実に isChanceState が立つ.
            InGamePresenter.Instance.RegisterOnEnemyState(EnemyBattleState.StunLong, OnEnemyStunLong);

            //Playerのキーコンフィグ
            compositeDisposePlayer.Add(
                Observable.EveryUpdate()
                .Where(_ => playerControllerEnable)
                .Subscribe(_ => 
                {
                    //Debug.Log("test3");

                    UpdateController();
                }));
            //FixedUpdate
            compositeDisposePlayer.Add(
                Observable.EveryUpdate(UnityFrameProvider.FixedUpdate)
                .Where(_=> playerControllerEnable)
                .Subscribe(_ =>
                {

                    //Debug.Log("test");
                    playerModel.OnGravity();

                    // 鼓動200スタン: 鼓動が200に到達したら2秒間行動不可、鼓動を100に戻す.
                    // ※OnIdleDecreaseより前にチェックしないと即減少で検知できない.
                    if (!isPulseMaxStunning && pulseModel.GetPulseGauge() >= pulseModel.maxPulseGauge)
                    {
                        ExecutePulseMaxStunAsync().Forget();
                    }

                    // 鼓動減少: 攻撃を振らない時、秒間1減少（100未満にはならない）.
                    // HeartResist中・スタン中は別の減少処理が走るためスキップ.
                    if (!isHeartResisting && !isPulseMaxStunning)
                    {
                        pulseModel.OnIdleDecrease(UnityEngine.Time.fixedDeltaTime);
                    }

                    // 鼓動0デバフ: 鼓動が0の間、2秒後から秒間200ダメージ.
                    if (pulseModel.GetPulseGauge() <= 0f)
                    {
                        if (!isPulseZero)
                        {
                            isPulseZero = true;
                            pulseZeroTimer = 0f;
                        }
                        pulseZeroTimer += UnityEngine.Time.fixedDeltaTime;
                        if (pulseZeroTimer >= pulseZeroDebuffDelay)
                        {
                            int debuffDamage = (int)(playerStatusModel.maxHp / 5f * UnityEngine.Time.fixedDeltaTime);
                            if (debuffDamage > 0)
                            {
                                playerStatusModel.Damage(debuffDamage);
                            }
                        }
                    }
                    else
                    {
                        isPulseZero = false;
                        pulseZeroTimer = 0f;
                    }

                    // Jak連撃コンボタイムアウトチェック（アクション中でない時のみ）.
                    if (jakComboCount > 0 && playerModel.enableAction == false && UnityEngine.Time.time - jakLastAttackTime > jakComboResetTime)
                    {
                        // アニメーション速度制御を解除（移動速度による自動調整を再開）.
                        playerAnimation?.ClearActionAnimatorSpeed();
                        playerAnimation?.PlayTrigger("Jak_End");
                        jakComboCount = 0;
                    }

                    // 心拍エフェクト方向検出（累積方式）.
                    // previousPulseValue はエフェクト発火 or 方向確定時のみ更新.
                    // 緩やかな変化でも累積で閾値を超えれば検出される.
                    float currentPulse = pulseModel.GetPulseGauge();

                    // 心拍数状態を InGamePresenter へ通知（状態変化時のみ発火）.
                    var hrState = currentPulse >= 100f ? PlayerHeartRateState.Critical
                                : currentPulse >= 70f  ? PlayerHeartRateState.Elevated
                                : PlayerHeartRateState.Normal;
                    InGamePresenter.Instance.SetHeartRateState(hrState);

                    float accumulatedDelta = currentPulse - previousPulseValue;
                    pulseEffectCooldown -= UnityEngine.Time.fixedDeltaTime;

                    if (Mathf.Abs(accumulatedDelta) >= pulseChangeThreshold)
                    {
                        int newDirection = accumulatedDelta > 0f ? 1 : -1;
                        bool directionChanged = (pulseDirection != 0 && newDirection != pulseDirection);

                        if (directionChanged || pulseEffectCooldown <= 0f)
                        {
                            var pulseAvator = playerModel.GetAvator();
                            if (pulseAvator != null)
                            {
                                string effectName     = newDirection > 0 ? "UP"   : "Down";
                                string oppositeEffect = newDirection > 0 ? "Down" : "UP";
                                Vector3 effectPos = pulseAvator.transform.position;
                                if (newDirection < 0) effectPos.y += 1.5f;
                                // 逆方向エフェクトをプールに返却し、常に片方のみ表示.
                                PlayerEffectPool.Instance(false)?.StopAll(oppositeEffect);
                                PlayerEffectPool.Instance(false)?.Spawn(effectName, effectPos, pulseAvator.transform);
                            }
                            pulseEffectCooldown = pulseEffectSameDirectionInterval;
                        }

                        pulseDirection = newDirection;
                        previousPulseValue = currentPulse;
                    }
                })
                );
            //InputSystem起動
            inputActions?.CharacterController.Enable();
            inputActions?.Player.Enable();

            // 着地検出: isGround が false→true になった時に JumpOnGround + Idol トリガー.
            {
                bool prevGround = playerModel.isGround.Value;
                bool isFirstEmission = true;
                compositeDisposePlayer.Add(
                    playerModel.isGround
                    .Subscribe(current =>
                    {
                        // 初回（ReactivePropertyの初期値）はスキップ.
                        if (isFirstEmission)
                        {
                            isFirstEmission = false;
                            prevGround = current;
                            return;
                        }

                        // false → true 遷移 = 着地（死亡後は無視）.
                        if (!prevGround && current && playerControllerEnable)
                        {
                            Debug.Log("[PlayerPresenter] 着地検出 - JumpOnGround / Idol トリガー発火");
                            playerAnimation?.PlayJumpOnGround();
                            playerAnimation?.PlayTrigger("Idol");
                        }
                        prevGround = current;
                    }));
            }

            // HP <= 0 で死亡処理.
            compositeDisposePlayer.Add(
                playerStatusModel.hp
                .Where(hp => hp <= 0)
                .Take(1)
                .Subscribe(_ =>
                {
                    Debug.Log("[PlayerPresenter] HP <= 0 - 死亡処理開始.");

                    // 入力停止.
                    playerControllerEnable = false;
                    inputActions?.CharacterController.Disable();
                    inputActions?.Player.Disable();

                    // 歩き/着地アニメーション遷移を停止し着地判定を即座に解除.
                    playerAnimation?.NotifyDead();

                    // 死亡アニメーション再生.
                    playerAnimation?.PlayTrigger("Dead");

                    // 敵の位置を取得して吹き飛ばし.
                    var enemy = UnityEngine.Object.FindFirstObjectByType<EnemyPresenter_abstract>();
                    if (enemy != null)
                    {
                        playerModel.OnDeath(enemy.transform.position);
                    }
                    else
                    {
                        // 敵がいない場合は後ろに吹き飛ばす.
                        playerModel.OnDeath(playerModel.GetAvator().transform.position + Vector3.right);
                    }
                }));
        }
        /// <summary>
        /// プレイヤーを起動する
        /// </summary>
        /// <param name="able"></param>
        public void SetPlayerEnable(bool able)
        {
            playerControllerEnable = able;
            InitalizePlayerEvents();
        }

        /// <summary>
        /// プレイヤーのアクション（移動・攻撃等）の有効/無効を切り替え.
        /// ポーズボタン(ESC)は無効化されない.
        /// </summary>
        public void SetPlayerActionEnable(bool enable)
        {
            playerActionEnable = enable;
        }

        
        /// <summary>
        ///　キー操作関係の関数
        /// </summary>
        public void UpdateController()
        {
            playerStatusModel.Update();

            // ポーズボタン: 短押し=tutorial表示 / 長押し=タイトルに戻る.
            UpdatePoseButton();

            // デバッグキー（エディタ専用）.
            UpdateDebugKeys();

            // チュートリアル等でアクション無効化中は移動・攻撃等をスキップ.
            if (!playerActionEnable) return;

            // プレイヤーのアクション中・スタン中は行動入力を受け付けない.
            if (playerModel.enableAction == false && !isPulseMaxStunning)
            {

                // 移動 - 居合中は全操作無視、HeartResist中は弱:0.2→0.5 ramp / 強:×0.01.
                Vector2 moveInput = isIaiActive ? Vector2.zero : inputActions.CharacterController.Move.ReadValue<Vector2>();

                // チュートリアル出現後、移動入力が一度0に戻るまで無視.
                if (ignoreMoveUntilRelease)
                {
                    if (Mathf.Approximately(moveInput.x, 0f))
                        ignoreMoveUntilRelease = false;
                    else
                        moveInput = Vector2.zero;
                }

                if (isHeartResisting)
                {
                    if (isStrongHeartResist)
                    {
                        moveInput *= 0.01f;
                    }
                    else
                    {
                        float elapsed = UnityEngine.Time.time - heartResistStartTime;
                        float t = Mathf.Clamp01(elapsed / 2.0f);
                        float speedMult = Mathf.Lerp(0.2f, 0.5f, t);
                        moveInput *= speedMult;
                    }
                }
                playerModel.OnMove(moveInput);
                // 入力方向をアニメーションコントローラーに通知（攻撃中は呼ばれないため反動反転を防止）.
                playerAnimation?.SetInputDirection(moveInput.x);

                // HeartResist弱中: 移動→停止時にsheathing_of_swordトリガーを再発火してアニメーション復帰.
                if (isHeartResisting && !isStrongHeartResist)
                {
                    bool isMovingNow = Mathf.Abs(moveInput.x) > 0.01f;
                    if (wasMovingDuringHeartResist && !isMovingNow)
                    {
                        // 居合発動可能状態なら sheathing_of_sword_2、そうでなければ sheathing_of_sword.
                        string trigger = iaiReadyNotified ? "sheathing_of_sword_2" : "sheathing_of_sword";
                        playerAnimation?.PlayTrigger(trigger);
                    }
                    wasMovingDuringHeartResist = isMovingNow;
                }

                // 居合中は移動以外の操作も全て無視.
                if (!isIaiActive)
                {
                // 居合発動条件チェック: 達成中は通常攻撃を無効化し、居合に専念させる.
                bool iaiConditionsMet = false;
                if (isHeartResisting)
                {
                    iaiConditionsMet = iaiReadyRatio >= 1f;
                }

                // チャンス状態タイムアウトリセット.
                if (isChanceState && UnityEngine.Time.time > chanceStateEndTime)
                {
                    isChanceState = false;
                    Debug.Log("[PlayerPresenter] チャンス状態タイムアウト");
                }

                // チャンス状態: 居合条件より優先して必殺技発動.
                if (isChanceState && inputActions.CharacterController.FirstAttack.WasPressedThisFrame())
                {
                    isChanceState = false;
                    isIaiActive = true; // 同フレームでの居合二重発動を防止.
                    EndJakComboIfActive();
                    ExecuteChanceAttackAsync().Forget();
                }
                // 攻撃入力（居合条件未達成時のみ）.
                else if (!iaiConditionsMet)
                {
                if (inputActions.CharacterController.FirstAttack.WasPressedThisFrame())
                {
                    ExecuteJakComboAttackAsync().Forget();
                }
                else if (inputActions.CharacterController.SecondAttack.WasPressedThisFrame())
                { EndJakComboIfActive(); ExecuteMeleeAttackAsync("FirstAttack").Forget(); }
                else if (inputActions.CharacterController.SpecialAttack.WasPressedThisFrame())
                { EndJakComboIfActive(); ExecuteMeleeAttackAsync("RestrainAttack").Forget(); }
                }

                // 操作が統一されているものの為、判定を書いていく.
                // 回避（回避居合い判定付き）.
                if (inputActions.CharacterController.Dodge.WasPressedThisFrame())
                {
                    EndJakComboIfActive();
                    attackCommander.ForceHideZanEffect();
                    Vector2 dodgeDir = inputActions.CharacterController.Move.ReadValue<Vector2>();
                    var dodgeIaiResult = CheckDodgeIaiCondition(dodgeDir);
                    if (dodgeIaiResult.shouldTriggerIai)
                    {
                        ExecuteDodgeIaiAsync(dodgeDir, dodgeIaiResult.enemy, dodgeIaiResult.isParryable).Forget();
                    }
                    else
                    {
                        playerModel.OnDodge(dodgeDir);

                        // パリィ不可攻撃（怒り行動）中の回避: 専用SE再生のみ（ダメージ/スタンなし）.
                        var parryEnemy = UnityEngine.Object.FindFirstObjectByType<EnemyPresenter_abstract>();
                        if (parryEnemy != null && parryEnemy.IsAngerAction)
                        {
                            Vector2 pPos = playerModel.GetAvator() != null
                                ? (Vector2)playerModel.GetAvator().transform.position
                                : Vector2.zero;
                            float pDist = Vector2.Distance(pPos, (Vector2)parryEnemy.transform.position);
                            if (pDist <= iaiDodgeDistance)
                            {
                                Debug.Log("[PlayerPresenter] 通常回避 → パリィ不可攻撃: ダメージ/スタンなし");
                            }
                        }
                    }
                }
                // 回復.
                if (inputActions.CharacterController.Heal.WasPressedThisFrame())
                {
                    EndJakComboIfActive();
                    attackCommander.ForceHideZanEffect();
                    int healPointBefore = playerStatusModel.healPoint.Value;
                    int gageBefore = drainModel.num.Value;
                    playerStatusModel.Heal();
                    // 回復成功時（healPointまたはゲージ消費時）にエフェクト・SE再生.
                    if (playerStatusModel.healPoint.Value < healPointBefore || drainModel.num.Value < gageBefore)
                    {
                        guardSEPlayer?.Play("SE_Heal");
                        var healAvator = playerModel.GetAvator();
                        if (healAvator != null)
                        {
                            PlayerEffectPool.Instance(false).Spawn("PlayerEffect_Heal", healAvator.transform.position, healAvator.transform);
                        }
                    }
                }
                // Platformすり抜け（下方向入力0.5以上でPlatform上にいる場合）.
                if (moveInput.y < -0.5f && playerModel.PlatformDetector.IsOnPlatform)
                {
                    playerModel.OnDropThroughPlatform();
                }
                // ジャンプ.
                if (inputActions.CharacterController.Jump.WasPressedThisFrame())
                {
                    EndJakComboIfActive();
                    attackCommander.ForceHideZanEffect();
                    playerModel.OnJumpEvent();
                    playerAnimation?.PlayJump();
                }

                //if (inputActions.CharacterController.Search.WasPressedThisFrame())
                //{
                //    EndJakComboIfActive();
                //    playerSearchModel.SearchStageSelect();
                //}

                } // 居合中操作無視ブロック終了.
            }

            // HeartResist開始 (スタン中・居合中・クールダウン中は受け付けない).
            // HeartResist(RB/L) または Guard(LB/O=強) のどちらかで開始.
            if (!isPulseMaxStunning && !isIaiActive && !playerModel.enableAction
                && UnityEngine.Time.time >= heartResistCooldownEnd
                && !isHeartResisting
                && (inputActions.CharacterController.HeartResist.WasPressedThisFrame()
                    || inputActions.CharacterController.Guard.WasPressedThisFrame()))
            {
                isHeartResisting = true;
                heartResistStartTime = UnityEngine.Time.time;
                wasMovingDuringHeartResist = false;

                // ゲージが既に溜まっている場合は居合準備済みアニメを再生.
                if (iaiReadyRatio >= 1f)
                {
                    iaiReadyNotified = true;
                    playerAnimation?.PlayTrigger("sheathing_of_sword_2");
                    // 居合発動可能状態で再度抑えた: hosi を即時再生してループ再起動.
                    playerAnimation?.PlayTrigger("hosi");
                    StartHosiLoop();
                    Debug.Log("[PlayerPresenter] HeartResist Start - ゲージ残存: sheathing_of_sword_2 + hosi trigger");
                }
                else
                {
                    iaiReadyNotified = false;
                    playerAnimation?.PlayTrigger("sheathing_of_sword");
                    Debug.Log($"[PlayerPresenter] HeartResist Start - sheathing_of_sword trigger (ratio={iaiReadyRatio:F2})");
                }
            }

            // HeartResist強モード判定（enableActionに関係なく毎フレーム更新）.
            // 片方押し中にもう片方をWasPressedThisFrameで押した瞬間にも強へ移行できるよう、
            // IsPressed()とWasPressedThisFrame()の両方で判定する.
            if (isHeartResisting && !isPulseMaxStunning)
            {
                bool hrPressed = inputActions.CharacterController.HeartResist.IsPressed();
                bool guPressed = inputActions.CharacterController.Guard.IsPressed();
                bool hrJustPressed = inputActions.CharacterController.HeartResist.WasPressedThisFrame();
                bool guJustPressed = inputActions.CharacterController.Guard.WasPressedThisFrame();

                // どちらかが押されていてもう片方が新たに押された瞬間 → 強に即移行.
                if ((hrPressed && guJustPressed) || (guPressed && hrJustPressed))
                    isStrongHeartResist = true;
                // 両方押し続けている間は強を維持.
                else if (hrPressed && guPressed)
                    isStrongHeartResist = true;
                // 片方だけ押している場合は弱（既に強になっていたら維持しない）.
                else if (hrPressed || guPressed)
                    isStrongHeartResist = false;

                // 強モード中は移動アニメーション完全抑制.
                playerAnimation?.SetSuppressMovement(isStrongHeartResist);
            }

            // HeartResist実行中 - 鼓動減少 (スタン中・居合中は停止).
            if (!isPulseMaxStunning && !playerModel.enableAction && isHeartResisting
                && (inputActions.CharacterController.HeartResist.IsPressed()
                    || inputActions.CharacterController.Guard.IsPressed()))
            {
                // 鼓動100越えの時は秒間10減少、それ以外は秒間5減少.
                float decreaseRate = pulseModel.GetPulseGauge() > 100f ? 10f : 5f;
                // 強は3倍.
                if (isStrongHeartResist) decreaseRate *= 3f;
                // 0.2から2秒かけて本来の減少量に到達.
                float elapsedHR = UnityEngine.Time.time - heartResistStartTime;
                float tHR = Mathf.Clamp01(elapsedHR / 2.0f);
                float decreaseMult = Mathf.Lerp(0.2f, 1.0f, tHR);
                float decreaseAmount = decreaseRate * decreaseMult * UnityEngine.Time.deltaTime;
                pulseModel.ReduceBreachingPoint(decreaseAmount);

                // 心拍数減少時の攻撃力バフ: 減少量に応じてstrengthRateを上昇（1.75倍係数）.
                playerStatusModel.strengthRate += decreaseAmount * 0.01f * 1.75f;

                // 居合発動割合を蓄積（弱:5秒、強:2秒で100%）.
                float requiredSec = isStrongHeartResist ? iaiStrongRequiredSeconds : iaiWeakRequiredSeconds;
                iaiReadyRatio += (1f / requiredSec) * UnityEngine.Time.deltaTime;

                // 居合発動可能通知（条件達成時に1回だけ sheathing_of_sword_2 トリガー）.
                if (!iaiReadyNotified && iaiReadyRatio >= 1f)
                {
                    iaiReadyNotified = true;
                    playerAnimation?.PlayTrigger("sheathing_of_sword_2");
                    playerAnimation?.SetIaiWarning(true);
                    guardSEPlayer?.Play("SE_IaiReady");
                    // 居合発動可能: hosi アニメーション再生 + 2secループ開始.
                    playerAnimation?.PlayTrigger("hosi");
                    StartHosiLoop();
                    Debug.Log("[PlayerPresenter] Iai ready - sheathing_of_sword_2 + hosi trigger + loop start");
                }
            }

            // 居合発動判定（HeartResist中 + ゲージ100% + 攻撃キー）.
            if (isHeartResisting && !isIaiActive
                && inputActions.CharacterController.FirstAttack.WasPressedThisFrame()
                && iaiReadyRatio >= 1f)
            {
                isHeartResisting = false;
                isStrongHeartResist = false;
                iaiReadyRatio = 0f;
                iaiReadyNotified = false;
                heartResistCooldownEnd = UnityEngine.Time.time + heartResistCooldown;
                playerAnimation?.SetSuppressMovement(false);
                playerAnimation?.SetIaiWarning(false);
                CancelHosiLoop();
                ExecuteIaiAttackAsync().Forget();
                Debug.Log($"[PlayerPresenter] Iai発動 (ratio={iaiReadyRatio:F2})");
            }

            // HeartResist終了（両方離されたとき）.
            // ゲージはリセットせず、5秒かけて0に減衰する.
            if (isHeartResisting
                && !inputActions.CharacterController.HeartResist.IsPressed()
                && !inputActions.CharacterController.Guard.IsPressed())
            {
                isHeartResisting = false;
                isStrongHeartResist = false;
                // iaiReadyRatio はリセットしない（5sec減衰）.
                wasMovingDuringHeartResist = false;
                heartResistCooldownEnd = UnityEngine.Time.time + heartResistCooldown;
                playerAnimation?.SetSuppressMovement(false);
                playerAnimation?.SetIaiWarning(false);
                // 居合は攻撃キーで発動するため、ここでは終了アニメのみ.
                playerAnimation?.PlayTrigger("hearEnd");
                Debug.Log($"[PlayerPresenter] HeartResist End - hearEnd trigger (ratio={iaiReadyRatio:F2}, 5sec減衰開始)");
            }

            // HeartResist非実行中: 居合ゲージを5秒かけて0に減衰.
            if (!isHeartResisting && iaiReadyRatio > 0f)
            {
                iaiReadyRatio -= (1f / iaiGaugeDecayDuration) * UnityEngine.Time.deltaTime;
                if (iaiReadyRatio <= 0f)
                {
                    iaiReadyRatio = 0f;
                    iaiReadyNotified = false;
                    playerAnimation?.SetIaiWarning(false);
                    CancelHosiLoop();
                }
            }

            // HeartResist非実行中: strengthRateを5秒かけて1.0fに減衰.
            if (!isHeartResisting && playerStatusModel.strengthRate > 1.0f)
            {
                playerStatusModel.strengthRate -= 0.5f * UnityEngine.Time.deltaTime;
                if (playerStatusModel.strengthRate < 1.0f)
                    playerStatusModel.strengthRate = 1.0f;
            }
        }

        /// <summary>
        /// 攻撃を受けたことを外部から受け取る.
        /// </summary>
        /// <param name="damageData">ダメージデータ（ダメージ量とPowerlevel）.</param>
        /// <returns>攻撃を受けた時点のガード状態.</returns>
        public GuardState OnReceiveAttack(DamageData damageData)
        {
            // 回避無敵中はダメージ無視.
            if (playerModel.IsDodgeInvincible)
            {
                return GuardState.None;
            }

            // 居合中は無敵.
            if (isIaiActive)
            {
                return GuardState.None;
            }

            GuardState state = GuardState.None;
            int damage = damageData.Damage;
            bool canKnockback = true;

            // ダメージ適用.
            playerStatusModel.Damage(damage);

            // 被ダメージSE.
            guardSEPlayer?.Play("SE_PlayerHurt");

            // 鼓動上昇: 被弾ダメージ×0.25.
            pulseModel.OnDamageTaken(damage);

            // スタン中に被弾 → スタン解除.
            if (isPulseMaxStunning && damage > 0)
            {
                stunInterruptedByDamage = true;
                Debug.Log("[PlayerPresenter] スタン中に被弾 - スタン解除");
            }

            // 攻撃中に被弾: 攻撃クールタイムを即座にリセット.
            if (playerModel.enableAction)
            {
                playerAnimation?.ClearActionAnimatorSpeed();
                playerModel.SetEnableAction(false);
                playerAnimation?.SetSuppressMovement(false);
            }

            // 吹き飛ばし処理（Powerlevelで上回った場合のみ）.
            if (canKnockback)
            {
                playerModel.OnKnockback(damageData.KnockbackForce, damageData.KnockbackDirectionX);
            }

            return state;
        }

        /// <summary>
        /// 攻撃を受けたことを外部から受け取る（後方互換用）.
        /// </summary>
        /// <param name="damage">受けるダメージ量.</param>
        /// <returns>攻撃を受けた時点のガード状態.</returns>
        public GuardState OnReceiveAttack(int damage)
        {
            return OnReceiveAttack(new DamageData(damage, PowerlevelConst.EnemyMeleeAttack));
        }

        /// <summary>
        /// 強制ガード解除（Powerlevelで上回られた時）.
        /// </summary>
        private void ForceGuardEnd()
        {
            if (guard != null && guard.IsGuarding)
            {
                guard.GuardEnd();
                playerAnimation?.SetGuard(false);
                playerModel.SetGuarding(false);
                Debug.Log("[PlayerPresenter] ForceGuardEnd - ガード強制解除");
            }
        }

        /// <summary>
        /// View連結関数-戦闘部分(Reactive)
        /// </summary>
        public void EventBattleView()
        {

            view.SetDrainUIGenerat(drainModel.maxGages);

            //吸収値
            compositeDisposePlayer.Add(
                drainModel.num.Subscribe(_ => {
                    float percent = 0;
                    percent = (float)_ / (float)drainModel.oneGageMaxNum;

                    view.SetDrainGages(percent);
                })
                );

            //回復残り回数
            compositeDisposePlayer.Add(
            playerStatusModel.healPoint.Subscribe(_ => {
                view.SetHealPointCount(_);
            }));

            // 回復ポイント増加（吸収ゲージ3消費→回復ポイント変換）時にSE再生.
            {
                int prevHealPoint = playerStatusModel.healPoint.Value;
                compositeDisposePlayer.Add(
                    playerStatusModel.healPoint.Subscribe(newVal =>
                    {
                        if (newVal > prevHealPoint)
                            guardSEPlayer?.Play("SE_HealPointRestore");
                        prevHealPoint = newVal;
                    }));
            }

            //HP割合
            compositeDisposePlayer.Add(
            playerStatusModel.hp.Subscribe(_ => {
                view.SetHpGauge(playerStatusModel.GetHpPercent()); 
            }));

            //BreachingPointの割合。
            compositeDisposePlayer.Add(
            pulseModel.pulseGauge.Subscribe(_ => {
                view.SetSkillGauge(_ / 200*100);
                }));

            // 鼓動ゲージ.
            compositeDisposePlayer.Add(
            pulseModel.pulseGauge.Subscribe(_ => {
                view.SetHeartGauge((int)_);
            }));
        }


        /// <summary>
        /// View部分のインポート
        /// </summary>
        public void GetView(IPlayerView _view)
        {
            this.view = _view;
            // DPS計測用にPlayerManagerにもView登録.
            PlayerManager.Instance(false)?.SetDPSView(_view);
        }

        /// <summary>
        /// GameOverViewを設定.
        /// </summary>
        public void SetGameOverView(GameOverView _gameOverView)
        {
            gameOverView = _gameOverView;
        }

        // ---- デバッグキー（エディタ専用） ----

        /// <summary>
        /// エディタ専用デバッグキーの処理.
        /// C キー: 長時間スタン（チャンス状態）を即時発動.
        /// </summary>
        private void UpdateDebugKeys()
        {
#if UNITY_EDITOR
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return;

            // C キー: 長時間スタン + チャンス状態を即時発動.
            if (keyboard.cKey.wasPressedThisFrame)
            {
                var enemy = UnityEngine.Object.FindFirstObjectByType<EnemyPresenter_abstract>();
                if (enemy != null && enemy.Model is EnemyModel_Wendig wendigDebug)
                {
                    wendigDebug.AbortMeteorDrop();
                    wendigDebug.TriggerMeteorDropStan().Forget();
                }
                isChanceState = true;
                chanceStateEndTime = UnityEngine.Time.time + chanceStateDuration;
                Debug.Log("[DEBUG] Cキー: 長時間スタン + チャンス状態 発動");
            }
#endif
        }

        // ---- ポーズボタン: ESC押下でポーズメニュー表示 ----
        // PauseMenuEventer 参照（ポーズメニュー制御）.
        private InGame.Common.PauseMenuEventer pauseMenuEventer;

        /// <summary>
        /// ポーズボタン(ESC)の押下を監視し、ポーズメニューを表示する.
        /// メニュー内の操作（再開/チュートリアル/設定/終了）は PauseMenuEventer が処理.
        /// </summary>
        private void UpdatePoseButton()
        {
            // PauseMenuEventer の遅延取得.
            if (pauseMenuEventer == null)
            {
                pauseMenuEventer = UnityEngine.Object.FindObjectOfType<InGame.Common.PauseMenuEventer>();
                if (pauseMenuEventer != null) { /* 取得成功 */ }
            }

            // ESC押下 → ポーズメニュー表示.
            if (inputActions.Player.Pose.WasPressedThisFrame())
            {
                if (pauseMenuEventer != null && !pauseMenuEventer.IsMenuVisible)
                {
                    pauseMenuEventer.ShowMenu();
                    ignoreMoveUntilRelease = true;
                }
            }
        }

        /// <summary>
        /// ガード/パリィSE初期化.
        /// </summary>
        private async UniTaskVoid InitializeGuardSE()
        {
            guardSEPlayer = SEPlayer.Create("PlayerGuardSE");
            await guardSEPlayer.LoadClipsAsync("SE_Parry", "SE_Stan", "SE_Heal", "SE_PlayerHurt", "SE_IaiReady", "SE_HealPointRestore");
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            //Debug.Log("[PlayerPresenter] Dispose");

            // InputSystem無効化.
            inputActions?.CharacterController.Disable();
            inputActions?.Player.Disable();

            // ガード/パリィSEリソース解放.
            if (guardSEPlayer != null)
            {
                guardSEPlayer.ReleaseAll();
                UnityEngine.Object.Destroy(guardSEPlayer.gameObject);
                guardSEPlayer = null;
            }

            compositeDisposePlayer?.Dispose();

            // InGamePresenter のコールバック登録を解除.
            InGamePresenter.Instance.UnregisterOnEnemyState(EnemyBattleState.StunLong, OnEnemyStunLong);
        }

        // =====================================================================
        // InGamePresenter コールバック
        // =====================================================================

        /// <summary>
        /// EnemyBattleState.StunLong 遷移時に InGamePresenter から呼ばれる.
        /// ポーリングによるフラグ監視を廃止し、通知ドリブンでチャンス状態を開始する.
        /// </summary>
        private void OnEnemyStunLong()
        {
            isChanceState = true;
            chanceStateEndTime = UnityEngine.Time.time + chanceStateDuration;

            // 必殺技確定勝利チェック: 9ヒット合計ダメージ >= 敵現在HP なら通常勝利演出を抑制.
            var enemy = UnityEngine.Object.FindFirstObjectByType<EnemyPresenter_abstract>();
            if (enemy?.Status != null && enemy.Status.hp.Value > 0f)
            {
                float pulse = pulseModel.GetPulseGauge();
                float totalDamage = pulse >= 100f ? 2500f : Mathf.Lerp(5000f, 2500f, pulse / 100f);
                if (totalDamage >= enemy.Status.hp.Value)
                {
                    isLethalChanceAttack = true;
                    InGame.Common.DeathManager.Instance.SuppressNormalVictory();
                    Debug.Log($"[PlayerPresenter] 必殺技確定勝利: totalDmg({totalDamage:F0}) >= hp({enemy.Status.hp.Value:F0})");
                }
            }

            Debug.Log("[PlayerPresenter] InGamePresenter(StunLong) → チャンス状態開始");
        }

        // 居合中フラグ.
        private bool isIaiActive = false;

        /// <summary>
        /// 居合中かどうかを取得.
        /// </summary>
        public bool IsIaiActive => isIaiActive;

        /// <summary>
        /// 居合攻撃を実行し、完了後にenableActionをfalseに戻す.
        /// 居合中: 無敵、移動無視、モーション速度x3（他の影響を受けない）、ダメージx5、心拍数上昇量x3.
        /// </summary>
        /// <param name="iaiDuration">居合アニメーションの長さ（秒）.</param>
        private async UniTaskVoid ExecuteIaiAttackAsync(float iaiDuration = 1.0f)
        {
            // 居合中はguardを強制解除.
            ForceGuardEnd();

            // 居合開始時に移動を強制停止.
            playerModel.OnMove(Vector2.zero);

            playerModel.SetEnableAction(true);
            isIaiActive = true;
            IsIaiPerformed = true;

            // 居合発動: 心拍数を次の25刻み閾値へ上昇.
            pulseModel.OnIaiActivated();

            // 居合中は固定x27倍速（他の影響を受けない）.
            playerAnimation?.SetAnimatorSpeed(27.0f);

            try
            {
                // 移動アニメーション抑制 + 1f後確認.
                playerAnimation?.SetSuppressMovement(true);
                playerAnimation?.PlayTrigger("Iai");
                playerAnimation?.EnsureAttackAnimation("Iai").Forget();
                attackCommander.ExecuteAttack("IaiAttack");

                // アニメーション完了を待機（27倍速のため短時間で終了）.
                await UniTask.Delay(TimeSpan.FromSeconds(iaiDuration / 27.0f));

                // アニメーション速度制御を解除（移動速度による自動調整を再開）.
                playerAnimation?.ClearActionAnimatorSpeed();

                // 居合攻撃の持続時間が終わるまで入力不可を維持.
                await UniTask.Delay(TimeSpan.FromSeconds(iaiDuration - iaiDuration / 27.0f));
            }
            finally
            {
                isIaiActive = false;
                playerModel.SetEnableAction(false);
                playerAnimation?.ClearActionAnimatorSpeed();
                if (!isStrongHeartResist) playerAnimation?.SetSuppressMovement(false);
            }
        }

        // ---- 回避居合い ----

        /// <summary>
        /// 回避居合い条件チェック結果.
        /// </summary>
        private struct DodgeIaiResult
        {
            public bool shouldTriggerIai;
            public EnemyPresenter_abstract enemy;
            public bool isParryable;
        }

        /// <summary>
        /// 回避入力時に居合い発動条件をチェック.
        /// </summary>
        private DodgeIaiResult CheckDodgeIaiCondition(Vector2 dodgeDir)
        {
            var result = new DodgeIaiResult { shouldTriggerIai = false };

            // 敵を取得.
            var enemy = UnityEngine.Object.FindFirstObjectByType<EnemyPresenter_abstract>();
            if (enemy == null) return result;

            // 距離チェック.
            Vector2 playerPos = playerModel.GetAvator() != null
                ? (Vector2)playerModel.GetAvator().transform.position
                : Vector2.zero;
            Vector2 enemyPos = (Vector2)enemy.transform.position;
            float distance = Vector2.Distance(playerPos, enemyPos);
            if (distance > iaiDodgeDistance) return result;

            // 方向チェック（Player→Enemyの方向と回避方向の角度差）.
            if (dodgeDir != Vector2.zero && iaiDodgeAngle < 180f)
            {
                Vector2 toEnemy = (enemyPos - playerPos).normalized;
                float angle = Vector2.Angle(dodgeDir.normalized, toEnemy);
                if (angle > iaiDodgeAngle) return result;
            }

            // MeteorDrop中は常に発動（落下フェーズでは IsRushing/IsAttackImminent が立たないため特例）.
            if (enemy.IsMeteorDropActive)
            {
                result.shouldTriggerIai = true;
                result.enemy = enemy;
                result.isParryable = false; // MeteorDrop は常にパリィ不可.
                Debug.Log("[PlayerPresenter] 回避居合い条件成立(MeteorDrop)");
                return result;
            }

            // タイミングチェック.
            bool isRushing = enemy.IsRushing;
            bool isAttackImminent = enemy.IsAttackImminent
                && UnityEngine.Time.time - enemy.AttackWarningTime <= iaiDodgeTimeWindow;

            // 突進: タイミング不問（距離+方向条件のみ）.
            // 通常攻撃: タイミング条件必須.
            if (!isRushing && !isAttackImminent) return result;

            result.shouldTriggerIai = true;
            result.enemy = enemy;
            result.isParryable = enemy.IsCurrentAttackParryable;
            Debug.Log($"[PlayerPresenter] 回避居合い条件成立 - 距離:{distance:F1} 突進:{isRushing} パリィ可:{result.isParryable}");
            return result;
        }

        /// <summary>
        /// 回避居合いを実行: 回避 + 居合い攻撃 + 敵への効果.
        /// </summary>
        private async UniTaskVoid ExecuteDodgeIaiAsync(Vector2 dodgeDir, EnemyPresenter_abstract enemy, bool isParryable)
        {
            // 通常回避を実行（無敵+移動）.
            playerModel.OnDodge(dodgeDir);

            // パリィ不可攻撃の場合.
            if (!isParryable)
            {
                // 吸収ゲージ上昇.
                {
                    var drainModel = PlayerManager.Instance().drainModel;
                    drainModel?.Increment(5);
                }

                // パリィSE再生.
                guardSEPlayer?.Play("SE_Parry");

                // パリィ不可攻撃（大技含む）: 当たり判定無効化 + 無敵延長のみ.
                // ※ 大技への長時間スタン/チャンス状態は居合いでのみ発動する.
                Debug.Log("[PlayerPresenter] 回避 → パリィ不可攻撃: ダメージ/スタンなし");

                // 敵の当たり判定無効化 + 無敵延長.
                if (enemy != null && enemy.Model != null)
                {
                    enemy.Model.SkipHitDetection = true;
                    ClearSkipHitDetectionDelayed(enemy.Model).Forget();
                }
                ExtendDodgeInvincibility().Forget();

                return;
            }

            // === パリィ可能攻撃: パリィ処理（Iai ではなくパリィスタン）===
            Debug.Log("[PlayerPresenter] 回避 → パリィ成功: ParryStan発動");

            // 吸収ゲージ上昇.
            {
                var drainModel = PlayerManager.Instance().drainModel;
                drainModel?.Increment(5);
            }

            // パリィSE.
            guardSEPlayer?.Play("SE_Parry");

            // パリィスタン（1sec）発動.
            if (enemy != null && enemy.Model is EnemyModel_Wendig wendigModelNormal)
            {
                wendigModelNormal.TriggerParryStan().Forget();
            }

            // 敵の当たり判定無効化 + 無敵延長.
            if (enemy != null && enemy.Model != null)
            {
                enemy.Model.SkipHitDetection = true;
                ClearSkipHitDetectionDelayed(enemy.Model).Forget();
            }
            ExtendDodgeInvincibility().Forget();

        }

        /// <summary>
        /// パリィ後0.5sec経過したら強制的にIdle状態に遷移.
        /// 移動していなくても確実にアイドルに戻す.
        /// </summary>
        /// <summary>
        /// hosi アニメーションを 2sec ごとにループ再生.
        /// 居合発動可能状態(iaiReadyRatio >= 1f)の間だけ再生する.
        /// </summary>
        private void StartHosiLoop()
        {
            hosiLoopCts?.Cancel();
            hosiLoopCts?.Dispose();
            hosiLoopCts = new System.Threading.CancellationTokenSource();
            HosiLoopAsync(hosiLoopCts.Token).Forget();
        }

        private void CancelHosiLoop()
        {
            hosiLoopCts?.Cancel();
            hosiLoopCts?.Dispose();
            hosiLoopCts = null;
        }

        private async UniTaskVoid HosiLoopAsync(System.Threading.CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(2f), cancellationToken: token);
                    if (iaiReadyRatio >= 1f)
                    {
                        playerAnimation?.PlayTrigger("hosi");
                        Debug.Log("[PlayerPresenter] hosi ループ再生");
                    }
                }
            }
            catch (OperationCanceledException) { }
        }

        private async UniTaskVoid IdleFallbackAfterParryAsync()
        {
            await UniTask.Delay(TimeSpan.FromSeconds(0.5f));
            if (!isIaiActive && !isHeartResisting)
            {
                playerAnimation?.ForceIdleState();
            }
        }

        /// <summary>
        /// チャンス必殺技（Enemy大技スタン中）: 9回の回避居合ループ.
        /// 各ヒット後タイムスケール0でフリーズし、Enemyを中心に±60°以上の位置に移動して繰り返す.
        /// 最終ヒット後: 納刀→白黒フラッシュ→扇状出血エフェクト→ダメージカウンター.
        /// </summary>
        private async UniTaskVoid ExecuteChanceAttackAsync()
        {
            //Enemy取得
            var enemy = UnityEngine.Object.FindFirstObjectByType<EnemyPresenter_abstract>();
            if (enemy == null || enemy.Status == null) return;

            //自身を取得
            var avator = playerModel.GetAvator();
            if (avator == null) return;

            // Rigidbody2D キャッシュ（地面埋まり防止用。isTrigger は変更しない）.
            var avatorRb = avator.GetComponent<Rigidbody2D>();
            var avatorCol = avator.GetComponent<Collider2D>();
            float playerHalfHeight = avatorCol != null ? avatorCol.bounds.extents.y : 0.5f;
            var enemyCol2D = enemy.GetComponent<Collider2D>();

            playerModel.SetEnableAction(true);
            isIaiActive = true;
            playerAnimation?.SetSuppressMovement(true);
            InGamePresenter.Instance.SetPlayerState(PlayerBattleState.ChanceAttack);

            // 必殺技演出: "Hisatu" トリガー + 25/60秒 時間停止.
            view.PlayHisatu();

            // 確定勝利の場合: 他の入力を即時無効化（必殺技完了まで不要）.
            if (isLethalChanceAttack)
            {
                SetPlayerActionEnable(false);
                Debug.Log("[PlayerPresenter] 必殺技確定勝利: 操作無効化");
            }



            const int hitCount = 9;
            const float attackDistance = 5f;
            // ダメージ: 心拍数100以上=2500、99以下は2500→5000へ上昇（0時=5000）.
            float chanceAttackPulse = pulseModel.GetPulseGauge();
            float totalDamage = chanceAttackPulse >= 100f ? 2500f : Mathf.Lerp(5000f, 2500f, chanceAttackPulse / 100f);
            float hitDamage = totalDamage / hitCount;
            int groundMask = 1 << LayerMask.NameToLayer("Default");

            Vector2 enemyPos2D = (Vector2)enemy.transform.position;
            Vector2 playerPos2D = (Vector2)avator.transform.position;
            // InGamePresenter 経由で Enemy Inspector 設定の軌道中心 Transform を取得.
            // chanceAttackOrbitCenter（空obj）が設定されていればその position をそのまま使用.
            // 未設定時は Collider2D.bounds.center（ワールド空間の物理中心）をフォールバックに使用.
            var orbitTransform = InGamePresenter.Instance.EnemyOrbitCenter;
            Vector2 enemyBoundsCenter = enemyCol2D != null ? (Vector2)enemyCol2D.bounds.center : enemyPos2D;
            Vector2 orbitCenter = orbitTransform != null
                ? (Vector2)orbitTransform.position
                : enemyBoundsCenter;
            float currentAngle = Mathf.Atan2(playerPos2D.y - orbitCenter.y,
                                              playerPos2D.x - orbitCenter.x) * Mathf.Rad2Deg;

            Debug.Log($"[PlayerPresenter] チャンス必殺技開始 - 1ヒットダメージ: {hitDamage:F0}, " +
                      $"orbitCenter={orbitCenter:F2}, boundsCenter={enemyBoundsCenter:F2}, " +
                      $"orbitTransform={(orbitTransform != null ? orbitTransform.gameObject.name : "null")}");

            // 実行中のLitMotionハンドル（finally でキャンセル保険）.
            MotionHandle moveHandle = default;

            try
            {
                for (int i = 0; i < hitCount; i++)
                {
                    // ── 軌道中心を毎ヒット更新（敵の移動追従）────────────────
                    enemyPos2D        = (Vector2)enemy.transform.position;
                    enemyBoundsCenter = enemyCol2D != null ? (Vector2)enemyCol2D.bounds.center : enemyPos2D;
                    orbitCenter = orbitTransform != null
                        ? (Vector2)orbitTransform.position
                        : enemyBoundsCenter;

                    // 2回目以降: 前の軌道角度から ±60° 以上離れた有効角度を取得.
                    if (i > 0)
                    {
                        currentAngle = GetValidChanceAttackAngle(
                            orbitCenter, currentAngle, attackDistance, groundMask);
                    }

                    // ── 開始位置（軌道上）を計算・地形補正 ──────────────────
                    float rad = currentAngle * Mathf.Deg2Rad;
                    Vector2 startPos2D = orbitCenter + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * attackDistance;

                    // 1. 地面 Y 補正.
                    Vector2 rayOriginS = new Vector2(startPos2D.x, startPos2D.y + 3f);
                    RaycastHit2D groundHitS = Physics2D.Raycast(rayOriginS, Vector2.down, 8f, groundMask);
                    if (groundHitS.collider != null)
                    {
                        float safeY = groundHitS.point.y + playerHalfHeight;
                        if (startPos2D.y < safeY) startPos2D.y = safeY;
                    }
                    // 2. OverlapCircle で地形内部チェック.
                    int overlapAttempts = 0;
                    while (Physics2D.OverlapCircle(startPos2D, playerHalfHeight * 0.5f, groundMask) != null
                           && overlapAttempts < 5)
                    {
                        startPos2D.y += playerHalfHeight;
                        overlapAttempts++;
                    }
                    // ─────────────────────────────────────────────────────────

                    // 開始位置にテレポート.
                    float avatorZ = avator.transform.position.z;
                    avator.transform.position = new Vector3(startPos2D.x, startPos2D.y, avatorZ);
                    if (avatorRb != null) avatorRb.linearVelocity = Vector2.zero;

                    // Enemy方向を向く (localScale.x < 0 = 右向き).
                    bool enemyToRight = enemyBoundsCenter.x > startPos2D.x;
                    Vector3 ls = avator.transform.localScale;
                    avator.transform.localScale = new Vector3(
                        enemyToRight ? -Mathf.Abs(ls.x) : Mathf.Abs(ls.x), ls.y, ls.z);

                    // ── 終了位置: orbitCenter を通り抜けた先（同距離延長）──────────────
                    // startPos → orbitCenter の距離と同じだけ orbitCenter の先へ延長する.
                    // → プレイヤーが敵を切り抜けるような軌跡になる.
                    // レイキャスト（障害物）のみ最優先で終了位置を上書きする.
                    Vector2 targetPos    = orbitTransform != null ? (Vector2)orbitTransform.position : enemyBoundsCenter;
                    Vector2 moveDir2D    = targetPos - startPos2D;
                    float   distToTarget = moveDir2D.magnitude;
                    if (distToTarget > 0.01f) moveDir2D /= distToTarget;

                    // 終了位置 = orbitCenter の先、startPos→orbitCenter と同距離だけ延長.
                    float   fullDist = distToTarget * 2f;
                    Vector2 endPos2D = startPos2D + moveDir2D * fullDist;

                    // 障害物チェック（最優先）: 地形があれば手前を終了位置に更新.
                    RaycastHit2D obstacleHit = Physics2D.Raycast(startPos2D, moveDir2D, fullDist, groundMask);
                    float actualDist = fullDist;
                    if (obstacleHit.collider != null)
                    {
                        actualDist = Mathf.Max(obstacleHit.distance - playerHalfHeight, 0.1f);
                        endPos2D   = startPos2D + moveDir2D * actualDist;
                    }
                    // ─────────────────────────────────────────────────────────

                    // 移動時間: 速度一定（attackDistance を baseTravelDuration 秒で移動）.
                    // 終了位置が手前に更新されても速度は変わらず、時間が比例して短縮される.
                    const float baseTravelDuration = 0.3f;
                    float moveDuration = baseTravelDuration * (actualDist / Mathf.Max(fullDist, 0.001f));
                    moveDuration = Mathf.Max(moveDuration, 0.05f);

                    Vector3 startPos3D = new Vector3(startPos2D.x, startPos2D.y, avatorZ);
                    Vector3 endPos3D   = new Vector3(endPos2D.x,   endPos2D.y,   avatorZ);

                    // 切断エフェクト（開始位置を起点にプレイヤーを追跡し移動中に伸びる）.
                    ChanceAttackSlashEffect.SpawnTracking(startPos3D, avator.transform, moveDuration);

                    // 居合アニメーション開始（LitMotion 移動と同時）.
                    playerAnimation?.SetAnimatorSpeed(27.0f);
                    playerAnimation?.PlayTrigger("Iai");

                    // LitMotion で開始位置 → 終了位置へ移動（加速突進）.
                    moveHandle = LMotion.Create(startPos3D, endPos3D, moveDuration)
                        .WithEase(Ease.InQuad)
                        .BindToPosition(avator.transform);
                    await moveHandle.ToUniTask();
                    moveHandle = default;

                    playerAnimation?.ClearActionAnimatorSpeed();
                    if (avatorRb != null) avatorRb.linearVelocity = Vector2.zero;

                    // ダメージは全ヒット後に一括適用（DamageCounter 出現タイミング）.
                    guardSEPlayer?.Play("SE_Parry");

                    Debug.Log($"[PlayerPresenter] チャンス必殺技 ヒット {i + 1}/{hitCount} - " +
                              $"start={startPos2D:F2}, end={endPos2D:F2}, dist={actualDist:F2}sec={moveDuration:F2}");

                    if (i < hitCount - 1)
                    {
                        // 到達後フリーズ（ヒット演出）. 遅延時間は移動距離と独立.
                        Time.timeScale = 0f;

                        // 間隔: 0.5sec → 0.2sec（6回目で到達）.
                        float t        = Mathf.Clamp01(i / 5.0f);
                        float interval = Mathf.Lerp(0.5f, 0.2f, t);

                        await UniTask.Delay(
                            TimeSpan.FromSeconds(interval), Cysharp.Threading.Tasks.DelayType.UnscaledDeltaTime);

                        Time.timeScale = 1f;
                    }
                }

                // === 9ヒット完了後の演出 ===

                // 納刀アニメーション
                playerAnimation?.PlayTrigger("sheathing_of_sword");

                // 0.5sec待機
                await UniTask.Delay(TimeSpan.FromSeconds(0.5f));

                // 白黒フラッシュ (Fillモード: 背景白/オブジェクト黒、0.2sec、演出中は時間停止)
                FullscreenBlackEffectFeature.FillEnabled    = true;
                FullscreenBlackEffectFeature.Blend          = 1f;
                FullscreenBlackEffectFeature.GrayscaleRatio = 0f;
                FullscreenBlackEffectFeature.IsEnabled      = true;
                Time.timeScale = 0f;
                await UniTask.Delay(TimeSpan.FromSeconds(0.2f), ignoreTimeScale: true);
                Time.timeScale = 1f;
                FullscreenBlackEffectFeature.Blend          = 0f;
                FullscreenBlackEffectFeature.IsEnabled      = false;

                // 必殺技後: 心拍数を100にリセット.
                pulseModel.SetPulseGauge(pulseModel.GetBasePulseGauge());

                // 出血エフェクト 9個: 上方向扇状
                // 5個 (先行: -80, -40, 0, +40, +80度)
                Vector3 bleedPos = enemy.transform.position;
                float[] firstFanAngles  = { -80f, -40f, 0f, 40f, 80f };
                float[] secondFanAngles = { -60f, -20f, 20f, 60f };

                foreach (float a in firstFanAngles)
                {
                    BloodSplatterPool.Instance(false)?.Spawn(
                        bleedPos, ChanceAttackRotateVec(Vector2.up, a), Vector2.up);
                }

                // 0.25sec後に残り4個 (-60, -20, +20, +60度)
                await UniTask.Delay(TimeSpan.FromSeconds(0.25f));

                foreach (float a in secondFanAngles)
                {
                    BloodSplatterPool.Instance(false)?.Spawn(
                        bleedPos, ChanceAttackRotateVec(Vector2.up, a), Vector2.up);
                }

                // 全ダメージを一括適用（DamageCounter 出現タイミング）.
                enemy.Status.OnDamaged(totalDamage).Forget();

                // ダメージカウンター: 9個 個別 + 合計1個
                bool facingRight = enemy.transform.position.x > avator.transform.position.x;
                for (int i = 0; i < hitCount; i++)
                {
                    float angleOffset = (i - 4) * 10f; // -40 〜 +40度
                    DamageCounterPool.Instance(false)?.Spawn(
                        enemy.transform.position, hitDamage,
                        PlayerAttackType.Iai, facingRight, angleOffset);
                }
                // 合計カウンター (少し上にオフセット)
                DamageCounterPool.Instance(false)?.Spawn(
                    enemy.transform.position + Vector3.up * 1.5f,
                    totalDamage, PlayerAttackType.Iai, facingRight);

                Debug.Log($"[PlayerPresenter] チャンス必殺技完了 - 合計ダメージ: {totalDamage:F0}");
            }
            finally
            {
                if (moveHandle.IsActive()) moveHandle.Cancel();
                Time.timeScale = 1f;
                isIaiActive = false;
                playerAnimation?.ClearActionAnimatorSpeed();
                playerAnimation?.SetSuppressMovement(false);
                playerModel.SetEnableAction(false);
                playerAnimation?.PlayTrigger("IaiEnd");
                IdleFallbackAfterParryAsync().Forget();
                InGamePresenter.Instance.SetPlayerState(PlayerBattleState.Idle);

                // 確定勝利: 特殊勝利演出を開始（白黒演出なし、0.5sec後ズーム）.
                if (isLethalChanceAttack)
                {
                    isLethalChanceAttack = false;
                    InGame.Common.DeathManager.Instance.NotifyEnemyDeathSpecial().Forget();
                }
            }
        }

        /// <summary>
        /// Enemyを中心に currentAngle から ±60°以上離れた、
        /// Physics2D.Raycastでスペースが確認できる角度を返す.
        /// </summary>
        private float GetValidChanceAttackAngle(
            Vector2 orbitCenter, float currentAngle, float distance, int layerMask)
        {
            const int   maxAttempts  = 12;   // 試行回数増加
            const float minAngleDiff = 60f;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                float sign   = UnityEngine.Random.value > 0.5f ? 1f : -1f;
                float offset = sign * UnityEngine.Random.Range(minAngleDiff, 180f);
                float candidateAngle = currentAngle + offset;

                if (IsChanceAngleValid(orbitCenter, candidateAngle, distance, layerMask))
                    return candidateAngle;
            }

            // フォールバック候補を順に検証して最初に有効なものを返す.
            float[] fallbacks = { currentAngle + 180f, 90f, 270f, 45f, 135f };
            foreach (float fb in fallbacks)
            {
                if (IsChanceAngleValid(orbitCenter, fb, distance, layerMask))
                    return fb;
            }

            return currentAngle + 180f; // 最終フォールバック（全検証失敗時）.
        }

        /// <summary>
        /// 指定角度のテレポート先が地形と重ならないかを確認する.
        ///   1. orbitCenter → 候補位置 へのレイキャストが通る
        ///   2. 候補位置が OverlapCircle で地形内部でない
        /// </summary>
        private static bool IsChanceAngleValid(
            Vector2 orbitCenter, float angleDeg, float distance, int layerMask)
        {
            float   rad  = angleDeg * Mathf.Deg2Rad;
            Vector2 dir  = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            Vector2 dest = orbitCenter + dir * distance;

            // 経路チェック: 中間に地形があれば無効.
            RaycastHit2D pathHit = Physics2D.Raycast(orbitCenter, dir, distance + 0.5f, layerMask);
            if (pathHit.collider != null) return false;

            // 到着点チェック: 地形内部なら無効.
            if (Physics2D.OverlapPoint(dest, layerMask) != null) return false;

            return true;
        }

        /// <summary>Vector2 を指定角度（度）回転させる.</summary>
        private static Vector2 ChanceAttackRotateVec(Vector2 v, float angleDeg)
        {
            float r   = angleDeg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(r);
            float sin = Mathf.Sin(r);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }

        /// <summary>
        /// 一定時間後にSkipHitDetectionを解除.
        /// </summary>
        private async UniTaskVoid ClearSkipHitDetectionDelayed(EnemyModel_abstract model)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(1.0f));
            if (model != null)
            {
                model.SkipHitDetection = false;
                Debug.Log("[PlayerPresenter] SkipHitDetection解除");
            }
        }

        /// <summary>
        /// 回避無敵を延長（パリィ不可攻撃の回避居合い時）.
        /// </summary>
        private async UniTaskVoid ExtendDodgeInvincibility()
        {
            // 通常回避の無敵が切れるのを待つ（OnDodge内の0.35秒）.
            await UniTask.Delay(TimeSpan.FromSeconds(0.35f));
            // まだ居合い中なら無敵を維持.
            if (isIaiActive)
            {
                playerModel.SetDodgeInvincible(true);
                await UniTask.Delay(TimeSpan.FromSeconds(iaiDodgeInvincibilityExtension));
                playerModel.SetDodgeInvincible(false);
                Debug.Log("[PlayerPresenter] 回避無敵延長終了");
            }
        }

        /// <summary>
        /// 近接攻撃を実行し、終了後0.1秒間を開けてからenableActionをfalseに戻す.
        /// </summary>
        /// <param name="attackName">攻撃名.</param>
        /// <param name="attackDuration">攻撃アニメーションの長さ（秒）.</param>
        private async UniTaskVoid ExecuteMeleeAttackAsync(string attackName, float attackDuration = 0.6f)
        {
            // 移動アニメーション抑制.
            playerAnimation?.SetSuppressMovement(true);

            // 攻撃実行を先に行う（attackCommander内でenableActionをチェックしている可能性があるため）.
            attackCommander.ExecuteAttack(attackName);
            playerModel.SetEnableAction(true);

            try
            {
                // 1f後に攻撃アニメーション確認.
                string animTriggerName = attackName == "FirstAttack" ? "NormalAttackDefault" : attackName;
                playerAnimation?.EnsureAttackAnimation(animTriggerName).Forget();

                // 鼓動ゲージ連動: アニメ速度倍率を反映.
                float animSpeedRate = pulseModel.GetAnimationSpeedRate();
                playerAnimation?.SetAnimatorSpeed(animSpeedRate);

                // 鼓動ゲージ連動: 入力不可時間に倍率適用.
                float cooldownRate = pulseModel.GetActionCooldownRate();
                await UniTask.Delay(TimeSpan.FromSeconds(attackDuration * cooldownRate));

                // 終了後0.1秒間を開ける.
                await UniTask.Delay(TimeSpan.FromSeconds(0.1f));
            }
            finally
            {
                // アニメーション速度制御を解除（移動速度による自動調整を再開）.
                playerAnimation?.ClearActionAnimatorSpeed();

                playerModel.SetEnableAction(false);
                if (!isStrongHeartResist) playerAnimation?.SetSuppressMovement(false);
            }
        }

        /// <summary>
        /// Jak連撃コンボ攻撃を実行する.
        /// </summary>
        /// <param name="attackDuration">攻撃アニメーションの長さ（秒）.</param>
        private async UniTaskVoid ExecuteJakComboAttackAsync(float attackDuration = 0.33f)
        {
            // 移動アニメーション抑制.
            playerAnimation?.SetSuppressMovement(true);

            // 重複実行防止のため最初にアクション中フラグを立てる.
            playerModel.SetEnableAction(true);

            // タイムアウトでコンボリセット（攻撃終了後1/3秒以内に再入力がなかった場合）.
            if (UnityEngine.Time.time - jakLastAttackTime > jakComboResetTime && jakComboCount > 0)
            {
                jakComboCount = 0;
            }

            // コンボ上限到達時はリセット（0 => 1 => 2 => 0...）.
            if (jakComboCount >= jakComboMaxCount)
            {
                jakComboCount = 0;
            }

            // 使用するコンボ番号を保存.
            int currentCombo = jakComboCount;

            // コンボカウント増加（次の攻撃用に先に増加）.
            jakComboCount++;

            try
            {
                // 鼓動ゲージ連動: アニメ速度倍率を反映.
                float animSpeedRate = pulseModel.GetAnimationSpeedRate();
                playerAnimation?.SetAnimatorSpeed(3.0f * animSpeedRate);

                // 現在のコンボ段階のアニメーション再生.
                playerAnimation?.PlayTrigger("Jak_" + currentCombo);
                // 1f後に攻撃アニメーション確認.
                playerAnimation?.EnsureAttackAnimation("Jak_" + currentCombo).Forget();
                // 当たり判定とaudioは元のFirstAttackと同一（アニメーションなし）.
                // コンボ番号を設定してから攻撃実行.
                attackCommander.SetJakComboNumber(currentCombo);
                attackCommander.ExecuteAttack("JakComboAttack");

                // 鼓動ゲージ連動: 入力不可時間に倍率適用.
                float cooldownRate = pulseModel.GetActionCooldownRate();
                await UniTask.Delay(TimeSpan.FromSeconds(attackDuration * cooldownRate));
            }
            finally
            {
                // アニメーション速度制御を解除（移動速度による自動調整を再開）.
                playerAnimation?.ClearActionAnimatorSpeed();

                // 攻撃終了時刻を記録（ここから1/3秒以内に再入力で次のコンボ）.
                jakLastAttackTime = UnityEngine.Time.time;

                playerModel.SetEnableAction(false);
                if (!isStrongHeartResist) playerAnimation?.SetSuppressMovement(false);
            }
        }

        /// <summary>
        /// 鼓動200到達時のスタン処理: 2秒間行動不可、鼓動を100に戻す.
        /// </summary>
        private async UniTaskVoid ExecutePulseMaxStunAsync()
        {
            isPulseMaxStunning = true;
            IsPlayerStunning = true;
            stunInterruptedByDamage = false;
            Debug.Log($"[PlayerPresenter] 鼓動200到達 - スタン即時開始 pulse: {pulseModel.GetPulseGauge()}");

            // 行動不可にする（着地前から即座に開始）.
            playerModel.SetEnableAction(true);
            EndJakComboIfActive();
            ForceGuardEnd();

            Debug.Log("[PlayerPresenter] スタン開始");

            // スタンSE再生.
            guardSEPlayer?.Play("SE_Stan");

            // スタンアニメーション再生.
            playerAnimation?.PlayTrigger("Sutan");

            // スタンエフェクト再生.
            var stunAvator = playerModel.GetAvator();
            if (stunAvator != null)
            {
                PlayerEffectPool.Instance(false).Spawn("PlayerEffect_Stun", stunAvator.transform.position, stunAvator.transform, loop: true);
            }

            // 接地待機中に受けたダメージによるフラグをリセット（ループ開始直前）.
            stunInterruptedByDamage = false;

            // スタン時間（鼓動減少とは独立）.
            float stunDuration = 3f;
            // スタン中の鼓動減少速度（秒間50: 約2秒で200→100）.
            float decreasePerSecond = 50f;
            float elapsed = 0f;
            Debug.Log($"[PlayerPresenter] スタンループ開始 - duration: {stunDuration}s, pulse: {pulseModel.GetPulseGauge()}, decrease/s: {decreasePerSecond}");
            while (elapsed < stunDuration && !stunInterruptedByDamage)
            {
                // 攻撃asyncの完了による上書きを防止: 毎フレーム行動不可を強制維持.
                playerModel.SetEnableAction(true);
                float dt = UnityEngine.Time.deltaTime;
                elapsed += dt;

                // 鼓動を減少させ、100以下にならないようにクランプ.
                float currentPulse = pulseModel.GetPulseGauge();
                if (currentPulse > pulseModel.GetBasePulseGauge())
                {
                    float newPulse = currentPulse - decreasePerSecond * dt;
                    if (newPulse < pulseModel.GetBasePulseGauge())
                    {
                        newPulse = pulseModel.GetBasePulseGauge();
                    }
                    pulseModel.SetPulseGauge(newPulse);
                }

                await UniTask.Yield();
            }
            Debug.Log($"[PlayerPresenter] スタンループ終了 - elapsed: {elapsed:F2}s, interrupted: {stunInterruptedByDamage}, pulse: {pulseModel.GetPulseGauge()}");

            if (stunInterruptedByDamage)
            {
                Debug.Log("[PlayerPresenter] スタン被弾中断 - 鼓動維持して行動再開");
            }
            else
            {
                // スタン終了時に100に戻す（念のため）.
                pulseModel.SetPulseGauge(pulseModel.GetBasePulseGauge());
            }

            // スタンエフェクト停止.
            PlayerEffectPool.Instance(false).StopAll("PlayerEffect_Stun");

            // スタン終了アニメーション再生.
            playerAnimation?.PlayTrigger("EndSutan");
            Debug.Log("[PlayerPresenter] EndSutan トリガー発火");

            // 行動可能に戻す.
            playerModel.SetEnableAction(false);
            isPulseMaxStunning = false;
            IsPlayerStunning = false;
            stunInterruptedByDamage = false;
        }

        /// <summary>
        /// Jak連撃コンボを終了する（他の入力時）.
        /// </summary>
        private void EndJakComboIfActive()
        {
            if (jakComboCount > 0)
            {
                // アニメーション速度制御を解除（移動速度による自動調整を再開）.
                playerAnimation?.ClearActionAnimatorSpeed();
                playerAnimation?.PlayTrigger("Jak_End");
                jakComboCount = 0;
            }
        }

        /// <summary>
        /// Player/Enemy死亡条件をDeathManagerに登録.
        /// HP <= 0 で勝利/敗北演出を開始する.
        /// </summary>
        private void RegisterPlayerDeathCondition()
        {
            var titleSceneInfo = new TitleSceneInfo();
            var sceneChangeStand = SceneChangeStand.Instance();

            // DeathManagerにPlayer HP監視を登録.
            DeathManager.Instance.MonitorPlayerHP(playerStatusModel.hp);

            // SceneChangeStand互換の条件を登録（他の条件とのキャンセル連携用）.
            sceneChangeStand.RegisterCondition(DeathManager.Instance.CreateGameEndCondition(), titleSceneInfo);

            // Enemy HP監視登録.
            RegisterEnemyDeathConditionsAsync().Forget();
        }

        /// <summary>
        /// EnemyのHP監視をDeathManagerに非同期で登録.
        /// </summary>
        private async UniTaskVoid RegisterEnemyDeathConditionsAsync()
        {
            // Enemyが生成されるまで待機.
            await UniTask.WaitUntil(() => UnityEngine.Object.FindFirstObjectByType<EnemyPresenter_abstract>() != null);

            var enemies = UnityEngine.Object.FindObjectsByType<EnemyPresenter_abstract>(FindObjectsSortMode.None);

            foreach (var enemy in enemies)
            {
                if (enemy.Status != null)
                {
                    // DeathManagerにEnemy HP監視を登録.
                    DeathManager.Instance.MonitorEnemyHP(enemy.Status.hp);
                    Debug.Log($"[PlayerPresenter] Enemy HP監視登録完了 - {enemy.gameObject.name}");
                }
            }
        }
    }
}