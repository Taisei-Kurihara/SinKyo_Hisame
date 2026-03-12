using Audio;
using UnityEngine;
using Common;
using System;
using Cysharp.Threading.Tasks;
using R3;
using SceneInfo;
using InGame.Player;
using InGame.Player.Animation;
using InGame.Common;

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

        // HeartResist状態.
        private bool isHeartResisting = false;
        private float heartResistCooldownEnd = 0f;
        private const float heartResistCooldown = 0.825f;
        private float heartResistStartTime = 0f;

        // 鼓動0デバフ用変数.
        private bool isPulseZero = false;
        private float pulseZeroTimer = 0f;
        private const float pulseZeroDebuffDelay = 2f;

        // 鼓動200スタン用変数.
        private bool isPulseMaxStunning = false;

        // ガード/パリィSE用.
        private SEPlayer guardSEPlayer;

        // ゲームオーバー画面.
        private GameOverView gameOverView;

        // Jak連撃コンボ用変数.
        private int jakComboCount = 0;
        private float jakLastAttackTime = 0f;
        private const float jakComboResetTime = 0.33f;
        private const int jakComboMaxCount = 3;

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
                })
                );
            //InputSystem起動
            inputActions?.CharacterController.Enable();
            inputActions?.Player.Enable();

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
        ///　キー操作関係の関数
        /// </summary>
        public void UpdateController()
        {
            playerStatusModel.Update();

            // ポーズボタン: タイトルに戻る.
            if (inputActions.Player.Pose.WasPressedThisFrame())
            {
                SceneManager.Instance().LoadMainScene(new TitleSceneInfo()).Forget();
            }

            // プレイヤーのアクション中・スタン中は行動入力を受け付けない.
            if (playerModel.enableAction == false && !isPulseMaxStunning)
            {

                // 移動 - 居合中は全操作無視、HeartResist中は0.2→2secかけて0.33へ.
                Vector2 moveInput = isIaiActive ? Vector2.zero : inputActions.CharacterController.Move.ReadValue<Vector2>();
                if (isHeartResisting)
                {
                    float elapsed = UnityEngine.Time.time - heartResistStartTime;
                    float t = Mathf.Clamp01(elapsed / 2.0f);
                    float speedMult = Mathf.Lerp(0.2f, 0.5f, t);
                    moveInput *= speedMult;
                }
                playerModel.OnMove(moveInput);
                // 入力方向をアニメーションコントローラーに通知（攻撃中は呼ばれないため反動反転を防止）.
                playerAnimation?.SetInputDirection(moveInput.x);

                // 居合中は移動以外の操作も全て無視.
                if (!isIaiActive)
                {
                // 攻撃入力.
                if (inputActions.CharacterController.FirstAttack.WasPressedThisFrame())
                { ExecuteJakComboAttackAsync().Forget(); }
                else if (inputActions.CharacterController.SecondAttack.WasPressedThisFrame())
                { EndJakComboIfActive(); ExecuteMeleeAttackAsync("FirstAttack").Forget(); }
                else if (inputActions.CharacterController.SpecialAttack.WasPressedThisFrame())
                { EndJakComboIfActive(); ExecuteMeleeAttackAsync("RestrainAttack").Forget(); }

                // 操作が統一されているものの為、判定を書いていく.
                // 回避.
                if (inputActions.CharacterController.Dodge.WasPressedThisFrame())
                {
                    EndJakComboIfActive();
                    playerModel.OnDodge(inputActions.CharacterController.Move.ReadValue<Vector2>());
                }
                // 回復.
                if (inputActions.CharacterController.Heal.WasPressedThisFrame())
                {
                    EndJakComboIfActive();
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
                // ジャンプ.
                if (inputActions.CharacterController.Jump.WasPressedThisFrame())
                {
                    EndJakComboIfActive();
                    //Debug.Log("[PlayerPresenter] Jump button pressed");
                    playerModel.OnJumpEvent();
                }

                //if (inputActions.CharacterController.Search.WasPressedThisFrame())
                //{
                //    EndJakComboIfActive();
                //    playerSearchModel.SearchStageSelect();
                //}

                if (inputActions.CharacterController.Guard.WasPressedThisFrame())
                {
                    EndJakComboIfActive();
                    //Debug.Log("[PlayerPresenter] Guard Start");
                    guard.GuardStart();
                    playerAnimation?.SetGuard(true);
                    playerModel.SetGuarding(true);
                }
                } // 居合中操作無視ブロック終了.
            }

            // HeartResist開始 (スタン中・居合中・クールダウン中は受け付けない).
            if (!isPulseMaxStunning && !isIaiActive && !playerModel.enableAction
                && UnityEngine.Time.time >= heartResistCooldownEnd
                && inputActions.CharacterController.HeartResist.WasPressedThisFrame())
            {
                isHeartResisting = true;
                heartResistStartTime = UnityEngine.Time.time;
                playerAnimation?.PlayTrigger("Hurt");
                Debug.Log("[PlayerPresenter] HeartResist Start - Hurt trigger");
            }

            // HeartResist実行中 - 鼓動減少 (スタン中・居合中は停止).
            if (!isPulseMaxStunning && !playerModel.enableAction && inputActions.CharacterController.HeartResist.IsPressed() && isHeartResisting)
            {
                // 鼓動100越えの時は秒間10減少、それ以外は秒間5減少.
                float decreaseRate = pulseModel.GetPulseGauge() > 100f ? 10f : 5f;
                // 0.2から2秒かけて本来の減少量に到達.
                float elapsedHR = UnityEngine.Time.time - heartResistStartTime;
                float tHR = Mathf.Clamp01(elapsedHR / 2.0f);
                float decreaseMult = Mathf.Lerp(0.2f, 1.0f, tHR);
                float decreaseAmount = decreaseRate * decreaseMult * UnityEngine.Time.deltaTime;
                pulseModel.ReduceBreachingPoint(decreaseAmount);

                // 心拍数減少時の攻撃力バフ: 減少量に応じてstrengthRateを上昇（1.75倍係数）.
                playerStatusModel.strengthRate += decreaseAmount * 0.01f * 1.75f;
            }

            // HeartResist終了.
            if (inputActions.CharacterController.HeartResist.WasReleasedThisFrame() && isHeartResisting)
            {
                isHeartResisting = false;
                heartResistCooldownEnd = UnityEngine.Time.time + heartResistCooldown;
                if (pulseModel.GetPulseGauge() < 100f)
                {
                    // 鼓動100未満 - 居合攻撃実行.
                    ExecuteIaiAttackAsync().Forget();
                    Debug.Log("[PlayerPresenter] HeartResist End - Iai trigger (pulse < 100)");
                }
                else
                {
                    // 鼓動100以上 - 終了アニメーション.
                    playerAnimation?.PlayTrigger("hearEnd");
                    Debug.Log("[PlayerPresenter] HeartResist End - hearEnd trigger (pulse >= 100)");
                }
            }

            if (inputActions.CharacterController.Guard.WasReleasedThisFrame())
            {
                guard.GuardEnd();
                playerAnimation?.SetGuard(false);
                playerModel.SetGuarding(false);
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
            int attackPowerlevel = damageData.Powerlevel;
            bool canKnockback = true;

            // ガード中の場合.
            if (guard != null && guard.IsGuarding)
            {
                state = guard.CurrentGuardState;

                // パリィ成功時: 吸収ゲージポイント5付与.
                if (state == GuardState.Parry)
                {
                    drainModel?.Increment(5);
                }

                // Powerlevelで上回られた場合は強制防御解除.
                if (guard.IsOverpowered(attackPowerlevel))
                {
                    ForceGuardEnd();
                    Debug.Log($"[PlayerPresenter] Powerlevel上回られ - 強制防御解除 (攻撃:{attackPowerlevel} > ガード:{guard.GetGuardPowerlevel()})");
                }
                else if (state == GuardState.Parry)
                {
                    // パリィ成功時: ダメージ0、ノックバックなし、居合発動.
                    damage = 0;
                    canKnockback = false;
                    ForceGuardEnd();
                    ExecuteIaiAttackAsync().Forget();
                    // パリィ成功SE再生.
                    guardSEPlayer?.Play("SE_Parry");
                    Debug.Log($"[PlayerPresenter] Parry成功 - ダメージ無効化、居合発動");
                }
                else
                {
                    // ガード中はダメージ1/10に軽減 (90%軽減).
                    damage = (int)(damage * 0.1f);
                    // プレイヤーのPowerlevelを上回れない場合吹き飛ばせない.
                    canKnockback = false;
                    Debug.Log($"[PlayerPresenter] Guard - ダメージ1/10軽減適用: {damage}");
                }
            }

            // ダメージ適用.
            playerStatusModel.Damage(damage);

            // 鼓動上昇: 被弾時受けた減少HP×0.85倍.
            pulseModel.OnDamageTaken(damage);

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
        }

        /// <summary>
        /// GameOverViewを設定.
        /// </summary>
        public void SetGameOverView(GameOverView _gameOverView)
        {
            gameOverView = _gameOverView;
        }

        /// <summary>
        /// ガード/パリィSE初期化.
        /// </summary>
        private async UniTaskVoid InitializeGuardSE()
        {
            guardSEPlayer = SEPlayer.Create("PlayerGuardSE");
            await guardSEPlayer.LoadClipsAsync("SE_Parry", "SE_Stan", "SE_Heal");
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

            // 居合中は固定x27倍速（他の影響を受けない）.
            playerAnimation?.SetAnimatorSpeed(27.0f);

            playerAnimation?.PlayTrigger("Iai");
            attackCommander.ExecuteAttack("IaiAttack");

            // アニメーション完了を待機（27倍速のため短時間で終了）.
            await UniTask.Delay(TimeSpan.FromSeconds(iaiDuration / 27.0f));

            // アニメーション速度制御を解除（移動速度による自動調整を再開）.
            playerAnimation?.ClearActionAnimatorSpeed();

            // 居合攻撃の持続時間が終わるまで入力不可を維持.
            await UniTask.Delay(TimeSpan.FromSeconds(iaiDuration - iaiDuration / 27.0f));

            isIaiActive = false;
            playerModel.SetEnableAction(false);
        }

        /// <summary>
        /// 近接攻撃を実行し、終了後0.1秒間を開けてからenableActionをfalseに戻す.
        /// </summary>
        /// <param name="attackName">攻撃名.</param>
        /// <param name="attackDuration">攻撃アニメーションの長さ（秒）.</param>
        private async UniTaskVoid ExecuteMeleeAttackAsync(string attackName, float attackDuration = 0.6f)
        {
            // 攻撃実行を先に行う（attackCommander内でenableActionをチェックしている可能性があるため）.
            attackCommander.ExecuteAttack(attackName);
            playerModel.SetEnableAction(true);

            // 鼓動ゲージ連動: アニメ速度倍率を反映.
            float animSpeedRate = pulseModel.GetAnimationSpeedRate();
            playerAnimation?.SetAnimatorSpeed(animSpeedRate);

            // 鼓動ゲージ連動: 入力不可時間に倍率適用.
            float cooldownRate = pulseModel.GetActionCooldownRate();
            await UniTask.Delay(TimeSpan.FromSeconds(attackDuration * cooldownRate));

            // 終了後0.1秒間を開ける.
            await UniTask.Delay(TimeSpan.FromSeconds(0.1f));

            // アニメーション速度制御を解除（移動速度による自動調整を再開）.
            playerAnimation?.ClearActionAnimatorSpeed();

            playerModel.SetEnableAction(false);
        }

        /// <summary>
        /// Jak連撃コンボ攻撃を実行する.
        /// </summary>
        /// <param name="attackDuration">攻撃アニメーションの長さ（秒）.</param>
        private async UniTaskVoid ExecuteJakComboAttackAsync(float attackDuration = 0.33f)
        {
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

            // 鼓動ゲージ連動: アニメ速度倍率を反映.
            float animSpeedRate = pulseModel.GetAnimationSpeedRate();
            playerAnimation?.SetAnimatorSpeed(3.0f * animSpeedRate);

            // 現在のコンボ段階のアニメーション再生.
            playerAnimation?.PlayTrigger("Jak_" + currentCombo);
            // 当たり判定とaudioは元のFirstAttackと同一（アニメーションなし）.
            // コンボ番号を設定してから攻撃実行.
            attackCommander.SetJakComboNumber(currentCombo);
            attackCommander.ExecuteAttack("JakComboAttack");

            // 鼓動ゲージ連動: 入力不可時間に倍率適用.
            float cooldownRate = pulseModel.GetActionCooldownRate();
            await UniTask.Delay(TimeSpan.FromSeconds(attackDuration * cooldownRate));

            // アニメーション速度制御を解除（移動速度による自動調整を再開）.
            playerAnimation?.ClearActionAnimatorSpeed();

            // 攻撃終了時刻を記録（ここから1/3秒以内に再入力で次のコンボ）.
            jakLastAttackTime = UnityEngine.Time.time;

            playerModel.SetEnableAction(false);
        }

        /// <summary>
        /// 鼓動200到達時のスタン処理: 2秒間行動不可、鼓動を100に戻す.
        /// </summary>
        private async UniTaskVoid ExecutePulseMaxStunAsync()
        {
            isPulseMaxStunning = true;
            Debug.Log($"[PlayerPresenter] 鼓動200到達 - 接地待機中 pulse: {pulseModel.GetPulseGauge()}");

            // 行動不可にする.
            playerModel.SetEnableAction(true);
            EndJakComboIfActive();
            ForceGuardEnd();

            // 接地するまで待機（通常の鼓動減少もisPulseMaxStunning=trueで停止中）.
            await UniTask.WaitUntil(() => playerModel.isGround.Value);

            Debug.Log("[PlayerPresenter] 接地確認 - スタン開始");

            // スタンSE再生.
            guardSEPlayer?.Play("SE_Stan");

            // スタンアニメーション再生.
            playerAnimation?.PlayTrigger("Hurt");

            // スタンエフェクト再生.
            var stunAvator = playerModel.GetAvator();
            if (stunAvator != null)
            {
                PlayerEffectPool.Instance(false).Spawn("PlayerEffect_Stun", stunAvator.transform.position, stunAvator.transform, loop: true);
            }

            // 3秒間かけて鼓動を100まで減少 (100ポイント / 3秒 ≒ 秒間33.3減少).
            float stunDuration = 3f;
            float decreasePerSecond = 100f / stunDuration;
            float elapsed = 0f;
            while (elapsed < stunDuration)
            {
                // 攻撃asyncの完了による上書きを防止: 毎フレーム行動不可を強制維持.
                playerModel.SetEnableAction(true);
                float dt = UnityEngine.Time.deltaTime;
                elapsed += dt;
                pulseModel.ReduceBreachingPoint(decreasePerSecond * dt);
                await UniTask.Yield();
            }
            pulseModel.SetPulseGauge(100f);

            // スタンエフェクト停止.
            PlayerEffectPool.Instance(false).StopAll("PlayerEffect_Stun");

            // 行動可能に戻す.
            playerModel.SetEnableAction(false);
            isPulseMaxStunning = false;
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
        /// Player/Enemy死亡条件をSceneChangeStandに登録.
        /// HP <= 0 から2.5秒後にタイトルへ戻る.
        /// </summary>
        private void RegisterPlayerDeathCondition()
        {
            var titleSceneInfo = new TitleSceneInfo();
            var sceneChangeStand = SceneChangeStand.Instance();

            // Player死亡条件登録.
            var playerDeathCondition = new DeathCondition(playerStatusModel.hp, 2.5f);
            sceneChangeStand.RegisterCondition(
                playerDeathCondition,
                titleSceneInfo,
                () =>
                {
                    ShowGameOver(false);
                }
            );

            // Enemy死亡条件登録.
            RegisterEnemyDeathConditionsAsync(titleSceneInfo).Forget();
        }

        /// <summary>
        /// ゲームオーバー画面を表示（タイトル遷移はボタン押下で行う）.
        /// </summary>
        /// <param name="isVictory">勝利の場合true.</param>
        private void ShowGameOver(bool isVictory)
        {
            if (gameOverView != null)
            {
                gameOverView.Show(isVictory);
            }

            Debug.Log("[GameOverEventer] タイトルへ戻る");
            SceneManager.Instance().LoadMainScene(new TitleSceneInfo()).Forget();
        }

        /// <summary>
        /// Enemyの死亡条件を非同期で登録.
        /// </summary>
        private async UniTaskVoid RegisterEnemyDeathConditionsAsync(TitleSceneInfo titleSceneInfo)
        {
            // Enemyが生成されるまで待機.
            await UniTask.WaitUntil(() => UnityEngine.Object.FindFirstObjectByType<EnemyPresenter_abstract>() != null);

            var enemies = UnityEngine.Object.FindObjectsByType<EnemyPresenter_abstract>(FindObjectsSortMode.None);
            var sceneChangeStand = SceneChangeStand.Instance();

            // カメラ境界はStageBoundsMarker + CameraBoundsUI ベースに移行.
            // CameraManager.Start() で自動適用される.

            foreach (var enemy in enemies)
            {
                if (enemy.Status != null)
                {
                    var enemyDeathCondition = new DeathCondition(enemy.Status.hp, 2.5f);
                    sceneChangeStand.RegisterCondition(
                        enemyDeathCondition,
                        titleSceneInfo,
                        () =>
                        {
                            ShowGameOver(true);

                            Debug.Log("[GameOverEventer] タイトルへ戻る");
                            SceneManager.Instance().LoadMainScene(new TitleSceneInfo()).Forget();
                        }
                    );
                    Debug.Log($"[PlayerPresenter] Enemy死亡条件登録完了 - {enemy.gameObject.name}");
                }
            }
        }
    }
}