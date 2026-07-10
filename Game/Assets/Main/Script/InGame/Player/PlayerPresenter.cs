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

            // Player死亡条件をSceneChangeStandに登録.
            RegisterPlayerDeathCondition();

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
                                string effectName = newDirection > 0 ? "UP" : "Down";
                                PlayerEffectPool.Instance(false).Spawn(effectName, pulseAvator.transform.position, pulseAvator.transform);
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

                // 攻撃入力（居合条件未達成時のみ）.
                if (!iaiConditionsMet)
                {
                if (inputActions.CharacterController.FirstAttack.WasPressedThisFrame())
                { ExecuteJakComboAttackAsync().Forget(); }
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
                    Debug.Log("[PlayerPresenter] HeartResist Start - ゲージ残存: sheathing_of_sword_2 trigger");
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
                    Debug.Log("[PlayerPresenter] Iai ready - sheathing_of_sword_2 trigger + warning start");
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

            // 鼓動上昇: 現在の鼓動値×0.3.
            pulseModel.OnDamageTaken();

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

        // ---- ポーズボタン: 短押し=tutorial / 長押し=タイトル戻り ----

        /// <summary>
        /// ポーズボタンの押下状態を監視し、短押し/長押しで動作を分岐.
        /// </summary>
        private void UpdatePoseButton()
        {
            var tutorialManager = TutorialManager.Instance(false);
            TutorialView tutorialView = tutorialManager?.View;

            // 押下開始.
            if (inputActions.Player.Pose.WasPressedThisFrame())
            {
                isPoseHolding = true;
                poseHoldTime = 0f;

                // タイトル戻り進捗ウィンドウ表示.
                if (tutorialView?.TitleReturnWindow != null)
                    tutorialView.TitleReturnWindow.SetActive(true);
                tutorialView?.SetTitleReturnProgress(0f);
            }

            // 押下中: 長押し進捗更新.
            if (isPoseHolding && inputActions.Player.Pose.IsPressed())
            {
                poseHoldTime += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(poseHoldTime / titleReturnHoldDuration);

                tutorialView?.SetTitleReturnProgress(progress);

                // 長押し完了 → タイトルに戻る.
                if (progress >= 1f)
                {
                    isPoseHolding = false;
                    isTutorialVisible = false;
                    UnityEngine.Time.timeScale = 1f;
                    if (tutorialView?.TitleReturnWindow != null)
                        tutorialView.TitleReturnWindow.SetActive(false);

                    SceneManager.Instance().LoadMainScene(new TitleSceneInfo()).Forget();
                    return;
                }
            }

            // 離した: 短押し判定.
            if (inputActions.Player.Pose.WasReleasedThisFrame() && isPoseHolding)
            {
                isPoseHolding = false;

                // タイトル戻りウィンドウ非表示.
                if (tutorialView?.TitleReturnWindow != null)
                    tutorialView.TitleReturnWindow.SetActive(false);

                // 短押し → tutorial表示/非表示トグル.
                if (poseHoldTime < shortPressThreshold && tutorialManager != null)
                {
                    if (tutorialManager.IsTutorialScene)
                    {
                        // チュートリアルシーン: 入力監視中(MonitoringInput)のみポーズ可能.
                        if (tutorialManager.CurrentPhase == Tutorial.TutorialPhase.MonitoringInput)
                        {
                            isTutorialVisible = true;
                            tutorialManager.PauseTutorialScene();
                            ignoreMoveUntilRelease = true;
                        }
                        else if (tutorialManager.CurrentPhase == Tutorial.TutorialPhase.ShowingExplanation)
                        {
                            // ポーズ中: 復帰.
                            isTutorialVisible = false;
                            tutorialManager.ResumeTutorialScene();
                        }
                    }
                    else
                    {
                        // ゲームシーン: 従来通りトグル.
                        isTutorialVisible = !isTutorialVisible;
                        if (isTutorialVisible)
                        {
                            tutorialManager.StartTutorial();
                            UnityEngine.Time.timeScale = 0f;
                            ignoreMoveUntilRelease = true;
                        }
                        else
                        {
                            tutorialManager.HideTutorial();
                            UnityEngine.Time.timeScale = 1f;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// ガード/パリィSE初期化.
        /// </summary>
        private async UniTaskVoid InitializeGuardSE()
        {
            guardSEPlayer = SEPlayer.Create("PlayerGuardSE");
            await guardSEPlayer.LoadClipsAsync("SE_Parry", "SE_Stan", "SE_Heal", "SE_PlayerHurt", "SE_IaiReady");
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

            // パリィ不可攻撃の場合: ダメージなし、居合アニメなし、スタンなし.
            if (!isParryable)
            {
                Debug.Log("[PlayerPresenter] 回避居合い → パリィ不可攻撃: ダメージ/スタンなし");

                // 吸収ゲージ上昇（パリィ不可攻撃でも回避成功時は増加）.
                {
                    var drainModel = PlayerManager.Instance().drainModel;
                    int drainAmount = 5;
                    drainModel?.Increment(drainAmount);
                }

                // パリィSE再生.
                guardSEPlayer?.Play("SE_Parry");

                // MeteorDrop中のパリィ: 0.5secスタン.
                if (enemy != null && enemy.IsMeteorDropActive && enemy.Model is EnemyModel_Wendig wendigModelParry)
                {
                    wendigModelParry.TriggerIaiStan().Forget();
                    Debug.Log("[PlayerPresenter] パリィ → MeteorDrop中: 0.5secスタン");
                }

                // 敵の当たり判定無効化 + 無敵延長.
                if (enemy != null && enemy.Model != null)
                {
                    enemy.Model.SkipHitDetection = true;
                    ClearSkipHitDetectionDelayed(enemy.Model).Forget();
                }
                ExtendDodgeInvincibility().Forget();

                return;
            }

            // === パリィ可能攻撃: 居合い攻撃を発動 ===
            ForceGuardEnd();
            playerModel.OnMove(Vector2.zero);
            isIaiActive = true;
            IsIaiPerformed = true;

            // 回避パリィでは心拍数25刻み変化を行わない（居合パリィのみ）.
            // pulseModel.OnIaiActivated();

            // 吸収ゲージ上昇.
            {
                var drainModel = PlayerManager.Instance().drainModel;
                int drainAmount = 5;
                drainModel?.Increment(drainAmount);
            }

            try
            {
                // 移動アニメーション抑制 + 1f後確認.
                playerAnimation?.SetSuppressMovement(true);
                playerAnimation?.SetAnimatorSpeed(27.0f);
                playerAnimation?.PlayTrigger("Iai");
                playerAnimation?.EnsureAttackAnimation("Iai").Forget();

                // ダメージを直接適用.
                if (enemy != null && enemy.Status != null)
                {
                    float iaiDamage = playerStatusModel.strength * playerStatusModel.strengthRate * 5f;
                    enemy.Status.OnDamaged(iaiDamage).Forget();

                    // ダメージカウンター表示（Iai型）.
                    bool facingRight = enemy.transform.position.x > playerModel.GetAvator().transform.position.x;
                    DamageCounterPool.Instance(false)?.Spawn(
                        enemy.transform.position, iaiDamage, PlayerAttackType.Iai, facingRight);

                    Debug.Log($"[PlayerPresenter] 回避居合いダメージ適用: {iaiDamage:F0}");
                }

                // MeteorDrop中: 5secスタン + 無敵削除 + 大技中断.
                // 怒り時専用行動中: 5secスタン（行動中断）.
                // それ以外: 0.5sec IaiStan.
                bool enemyStunnedLong = false; // MeteorDrop/怒りスタン時はプレイヤー即座に操作復帰.
                if (enemy != null && enemy.Model is EnemyModel_Wendig wendigModel)
                {
                    if (enemy.IsMeteorDropActive)
                    {
                        enemyStunnedLong = true;
                        wendigModel.AbortMeteorDrop();
                        wendigModel.TriggerMeteorDropStan().Forget();
                        Debug.Log("[PlayerPresenter] 回避居合い → MeteorDrop中: 5secスタン + 無敵削除 + 大技中断");
                    }
                    else if (enemy.IsAngerAction)
                    {
                        enemyStunnedLong = true;
                        wendigModel.TriggerStan().Forget();
                        Debug.Log("[PlayerPresenter] 回避居合い → 怒り行動Iai: 5secスタン発動（行動中断）");
                    }
                    else
                    {
                        wendigModel.TriggerIaiStan().Forget();
                        Debug.Log("[PlayerPresenter] 回避居合い → 敵短スタン発動（パリィ可能攻撃）");
                    }
                }

                // 居合いSE再生.
                guardSEPlayer?.Play("SE_Parry");

                // アニメーション完了を待機（27倍速で~37ms）.
                float iaiDuration = 1.0f;
                await UniTask.Delay(TimeSpan.FromSeconds(iaiDuration / 27.0f));
                playerAnimation?.ClearActionAnimatorSpeed();

                // 回避パリィは素早いカウンターのため短い硬直（0.15sec）.
                // 長時間スタン時は即復帰.
                if (!enemyStunnedLong)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(0.15f));
                }
            }
            finally
            {
                isIaiActive = false;
                playerAnimation?.ClearActionAnimatorSpeed();
                playerAnimation?.SetSuppressMovement(false);
                // 0.3sec後にアイドル復帰（入力がなければ強制遷移）.
                IdleFallbackAfterParryAsync().Forget();
            }
        }

        /// <summary>
        /// パリィ後0.3sec経過しても新しいアクションがなければ強制的にIdle状態に遷移.
        /// </summary>
        private async UniTaskVoid IdleFallbackAfterParryAsync()
        {
            await UniTask.Delay(TimeSpan.FromSeconds(0.3f));
            // 新しいアクションが開始されていなければアイドルに遷移.
            if (!playerModel.enableAction && !isIaiActive && !isHeartResisting)
            {
                playerAnimation?.ForceIdleState();
            }
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