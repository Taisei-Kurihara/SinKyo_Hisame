using Common;
using Cysharp.Threading.Tasks;
using GameEventPoint;
using InGame.Common;
using R3;
using System;
using System.Linq;
using UnityEngine;
using VContainer;
using static UnityEngine.RuleTile.TilingRuleOutput;


namespace InGame.Player
{
    public enum PlayerState
    {
        home,//ホーム画面
        battle,//戦闘ステージ時
        stageSelect//ステージ選択
    }



    /// <summary>
    /// 吸収
    /// </summary>
    /// </summary>
    public class DrainModel
    {
        public ReactiveProperty<int> num {  get; private set; }=new ReactiveProperty<int>();
        public int oneGageMaxNum { get; private set; } = 20;
        public int maxGages { get; private set; } = 10;

        // 条件付きイベントリスト（条件を満たしたら実行するコールバック）.
        private readonly System.Collections.Generic.List<(Func<bool> condition, Action action)> gageEvents = new();

        /// <summary>
        /// ゲージ変動時に条件を満たした場合に実行するイベントを登録.
        /// DrainModel自身は何を実行するかを知らず、外部から動的に仕事を割り当てる.
        /// </summary>
        public void RegisterGageEvent(Func<bool> condition, Action action)
        {
            gageEvents.Add((condition, action));
        }

        public void Increment(int _num)
        {
            if(_num <= 0) return;

            int c = num.Value + _num;

            if(c > oneGageMaxNum * (maxGages+2))
            {
                c = oneGageMaxNum * (maxGages+1);
                // ゲージ溢れ時: HP を maxHp/20 回復.
                var statusModel = PlayerManager.Instance().playerStatusModel;
                statusModel.IncrementHp(statusModel.maxHp / 20);
            }

            num.Value = c;

            // 登録済みイベントの条件チェック・実行.
            CheckGageEvents();
        }

        /// <summary>
        /// 登録済みイベントの条件を確認し、満たされた場合に実行.
        /// </summary>
        private void CheckGageEvents()
        {
            foreach (var (condition, action) in gageEvents)
            {
                if (condition())
                {
                    action();
                }
            }
        }
        public void Decrement(int _num)
        {
            if (_num >= 0) return;
            num.Value -= _num;
        }

        /// <summary>
        /// ゲージを指定数消費できるか判定.
        /// </summary>
        /// <param name="gageCount">消費するゲージ数.</param>
        public bool CanConsumeGages(int gageCount)
        {
            return num.Value >= oneGageMaxNum * gageCount;
        }

        /// <summary>
        /// ゲージを指定数消費する.
        /// </summary>
        /// <param name="gageCount">消費するゲージ数.</param>
        public void ConsumeGages(int gageCount)
        {
            num.Value -= oneGageMaxNum * gageCount;
            if (num.Value < 0) num.Value = 0;
        }
    }

    /// <summary>
    /// プレイヤーの数値系ステータスだけを扱う純粋なモデル
    /// </summary>
    public class PlayerStatusModel
    {
        public PlayerStatusModel(PlayerStatusInitModel _playerStatusInitModel,PulseModel _pulseModel) 
        {
            playerStatusInitModel = _playerStatusInitModel;
            pulseModel = _pulseModel;

            InitializeStatus();
        }
        PlayerStatusInitModel playerStatusInitModel;

        // ---- 初期化 ----
        public void InitializeStatus()
        {
            hp.Value = maxHp;
            healPoint.Value = healPointMax;
            speed = playerStatusInitModel.speed;
        }

        PulseModel pulseModel;

        public void Update()
        {
            float pulse = pulseModel.GetPulseGauge();
            if (pulse <= 25f)
            {
                speedRate = 0.75f;
            }
            else if (pulse <= 100f)
            {
                speedRate = 1.0f;
            }
            else if (pulse <= 175f)
            {
                // 100～175: 1.0～1.5 線形補間.
                speedRate = Mathf.Lerp(1.0f, 1.5f, (pulse - 100f) / 75f);
            }
            else if (pulse <= 190f)
            {
                // 175～190: 1.5～2.0 線形補間.
                speedRate = Mathf.Lerp(1.5f, 2.0f, (pulse - 175f) / 15f);
            }
            else
            {
                // 190～200: 2.0 上限.
                speedRate = 2.0f;
            }
        }


        // ---- 基本パラメータ ----
        public int strength { get; private set; } = 100;
        public float strengthRate =1.0f;


        private float speed;
        public float speedRate = 1.0f;

        public float GetSpeed()
        {
            return speed*speedRate;
        }

        // ---- HP ----
        public int maxHp { get; private set; } = 1000;
        public ReactiveProperty<int> hp { get; private set; }
            = new ReactiveProperty<int>(1000);


        // ---- HP処理 ----
        public void SetHp(int value) => hp.Value = value;
        public void IncrementHp(int _value)
        {
            hp.Value += _value;
            // maxHpを超過しない.
            if (hp.Value > maxHp) hp.Value = maxHp;
        }
        public void Damage(int value)
        {
            hp.Value -= value;
        }
        public float GetHpPercent() => (float)hp.Value / maxHp;

        // ---- Heal処理 ----
        public int healPointMax { get; private set; } = 3;

        public ReactiveProperty<int> healPoint { get; private set; }
            = new ReactiveProperty<int>(3);
        public void Heal()
        {
            // ゲージ3個分あればゲージ消費で回復、なければhealPoint消費.
            var drainModel = PlayerManager.Instance().drainModel;
            if (drainModel != null && drainModel.CanConsumeGages(3))
            {
                drainModel.ConsumeGages(3);
            }
            else if (healPoint.Value > 0)
            {
                healPoint.Value--;
            }
            else
            {
                return;
            }

            // 回復実行: maxHpの1/5回復（maxHpを超過しない）.
            hp.Value = Mathf.Min(hp.Value + maxHp / 5, maxHp);

            // 鼓動リセット: 回復アイテム使用時、鼓動ゲージを100にする.
            PlayerManager.Instance().pulseModel.OnHealItemUsed();
        }
        public void SetHeal(int num) => healPoint.Value = num;
    }

    


    /// <summary>
    /// 裏で近くの周囲に○○があればという処理の集合体
    /// </summary>
    public class PlayerSearchModel
    {
        public PlayerSearchModel(GameObject _avator) {
            playerAvator = _avator;
            checker = new ComponentChecker();
        }

        private GameObject playerAvator;
        //チェッカークラス
        private ComponentChecker checker;

        //マップで、周囲のものを探索する場合
        private const float mapSearch = 5;

        /// <summary>
        /// ステージ選択画面の探索
        /// </summary>
        public void SearchStageSelect()
        {
            StageSelectEventPointAttach stageSelectEventPointAttach = checker.CharacterCheck<StageSelectEventPointAttach>(playerAvator.transform.position, mapSearch);
            if(stageSelectEventPointAttach != null)
            {
                stageSelectEventPointAttach.OnEvent();
            }
        }
    }


    /// <summary>
    /// プレイヤー操作のモデル
    /// </summary>
    public class PlayerControllModel : IDisposable
    {
        public PlayerControllModel(GameObject _avator,Rigidbody2D _rigidbody)
        {
            status = PlayerManager.Instance().playerStatusModel;
            rigidbody = _rigidbody;
            avator = _avator;
            playerAttach = avator.GetComponent<PlayerAttach>(); 
        }
        // -------------------------
        // Inject / Constructor
        // -------------------------
        private readonly PlayerStatusModel status;
        private GameObject avator;
        private Rigidbody2D rigidbody;
        private PlayerAttach playerAttach;

        /// <summary>
        /// アバターを取得.
        /// </summary>
        public GameObject GetAvator() => avator;

        // -------------------------
        // Action State
        // -------------------------
        public bool enableAction { get; private set; } = false;

        /// <summary>
        /// アクション状態を設定.
        /// </summary>
        public void SetEnableAction(bool value) => enableAction = value;

        // -------------------------
        // ガード状態
        // -------------------------
        private bool isGuarding = false;
        private const float guardSpeedMultiplier = 0.5f;

        /// <summary>
        /// ガード状態を設定.
        /// </summary>
        public void SetGuarding(bool value) => isGuarding = value;

        // -------------------------
        // 地面判定 & 移動関連
        // -------------------------
        public ReactiveProperty<bool> isGround { get; private set; } = new ReactiveProperty<bool>(false);
        private float gravityPower = -19.6f;
        private Vector3 move;
        private bool direction;
        private int jumpCount = 0;

        // -------------------------
        // 回避回数制限
        // -------------------------
        private int dodgeCount = 0;
        private const int maxDodgeCount = 2;

        // -------------------------
        // 攻撃関連
        // -------------------------
        private bool attackCount;
        private bool attackActive;
        private CoolTimeBuilder attackActiveCount = new CoolTimeBuilder();

        // -------------------------
        // 回避無敵
        // -------------------------
        private bool isDodgeInvincible = false;
        public bool IsDodgeInvincible => isDodgeInvincible;

        // -------------------------
        // ダッシュ関連
        // -------------------------
        private CoolTimeBuilder dodgeCool = new CoolTimeBuilder();
        private float dashDistance = 4f;
        private float dashSecond = 0.35f;
        private Vector2 dashDir;
        private Vector2 startPos;
        private Vector2 prevOffset;

        

        // =====================================================
        // 初期化-ステータス開始時
        // =====================================================
        public void Initialize()
        {
            status.InitializeStatus();
        }

        /// <summary>
        /// Modelの参照をリセット
        /// </summary>
        public void ResetCharacter()
        {
            avator = null;
            rigidbody = null;
            playerAttach = null;
                              
            enableAction = false;
            jumpCount = 0;
        }

        public void SetPlayerAttach(PlayerAttach attach)
        {
            playerAttach = attach;
        }
        // =====================================================
        // 重力
        // =====================================================
        public void OnGravity()
        {
            rigidbody.AddForce(Vector3.up * gravityPower, ForceMode2D.Force);
        }

        // =====================================================
        // 移動
        // =====================================================
        public void OnMove(Vector2 vec)
        {
            // 防御中は移動速度5割減.
            float speedMultiplier = isGuarding ? guardSpeedMultiplier : 1f;
            move = new Vector3(vec.x * status.GetSpeed() * speedMultiplier, 0, 0);
            move += new Vector3(0, rigidbody.linearVelocity.y, 0);

            rigidbody.linearVelocity = move;

            // 向き変更はPlayerAnimationController.SetMoveのScale方式で処理.

            if (playerAttach != null)
            {
                bool wasOnGround = isGround.Value;
                isGround.Value = playerAttach.GetGroundSensor();

                // 着地した瞬間にジャンプカウント・回避カウントをリセット.
                if (!wasOnGround && isGround.Value)
                {
                    //Debug.Log("[Ground] Landed - jumpCount reset to 0 (transition detected)");
                    jumpCount = 0;
                    dodgeCount = 0;
                }
            }
        }

        // =====================================================
        // ジャンプ
        // =====================================================
        public void OnJumpEvent()
        {
            //Debug.Log($"[Jump] OnJumpEvent called. jumpCount={jumpCount}, isGround={isGround.Value}");
            if (jumpCount < 2)
            {
                jumpCount++;

                move = Vector3.zero;
                move += Vector3.up * 12.0f;

                rigidbody.linearVelocity = move;
            }
            else
            {
                //Debug.Log("[Jump] Jump blocked - jumpCount >= 2");
            }
        }

        // =====================================================
        // ダッシュ
        // =====================================================
        public void OnDodge(Vector2 vec)
        {
            // 回避回数制限: 上限2回.
            if (dodgeCount >= maxDodgeCount) return;
            dodgeCount++;

            // 鼓動上昇: 回避 +1.5.
            PlayerManager.Instance().pulseModel.OnDodge();

            // 鼓動25以下で回避距離半減.
            float currentDashDistance = dashDistance;
            if (PlayerManager.Instance().pulseModel.GetPulseGauge() <= 25f)
            {
                currentDashDistance = dashDistance * 0.5f;
            }
            float capturedDashDistance = currentDashDistance;

            dodgeCool.LinkTo(avator)
            .SetTime(TimeSpan.FromSeconds(dashSecond))
            .OnStart(() =>
            {
                enableAction = true;
                isDodgeInvincible = true;
                rigidbody.linearVelocity = Vector2.zero;

                if (vec == Vector2.zero)
                    vec = direction ? Vector2.left : Vector2.right;

                dashDir = vec.normalized;
                startPos = rigidbody.position;
                prevOffset = Vector2.zero;
            })
            .OnFixed(() =>
            {
                float t = dodgeCool.GetPercentTime();
                float easedT = CalcEase(t);

                Vector2 currentOffset = dashDir * (capturedDashDistance * easedT);
                Vector2 delta = currentOffset - prevOffset;
                prevOffset = currentOffset;

                rigidbody.MovePosition(rigidbody.position + delta);
            })
            .OnEnd(() =>
            {
                enableAction = false;
                isDodgeInvincible = false;
            })
            .Run();
        }
        /// <summary>
        /// イージング処理
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        private float CalcEase(float t)
        {
            if (t < 0.3f)
                return 0.05f + t / 0.3f * 0.5f;
            else if (t < 0.7f)
                return 0.55f + (t - 0.3f) / 0.4f * 0.35f;

            float u = (t - 0.7f) / 0.3f;
            return 0.9f + 0.1f * Mathf.SmoothStep(0f, 1f, u);
        }

        // =====================================================
        // ノックバック（持続力方式）
        // =====================================================
        private bool isKnockbacking = false;

        // 持続吹き飛ばしパラメータ.
        private const float knockbackDuration = 0.4f;
        private const float knockbackHorizontalMultiplier = 5f;
        private const float knockbackUpwardMultiplier = 1.5f;

        /// <summary>
        /// 被弾時の吹き飛ばし処理（持続力方式）.
        /// N秒間連続して力をかけ、接地で入力不可解除.
        /// </summary>
        /// <param name="force">吹き飛ばしの力.</param>
        /// <param name="dirX">吹き飛ばし方向（1=右、-1=左、エネミーの向き）.</param>
        public void OnKnockback(float force = 3f, float dirX = 1f)
        {
            OnKnockbackAsync(force, dirX).Forget();
        }

        private async UniTaskVoid OnKnockbackAsync(float force, float dirX)
        {
            if (rigidbody == null || avator == null) return;

            isKnockbacking = true;
            enableAction = true;
            rigidbody.linearVelocity = Vector2.zero;

            // 吹き飛ばし方向.
            Vector2 horizontalDir = dirX >= 0 ? Vector2.right : Vector2.left;

            // 上方向への初期衝撃.
            rigidbody.AddForce(Vector2.up * force * knockbackUpwardMultiplier, ForceMode2D.Impulse);

            // N秒間連続して横方向の力をかける.
            float elapsed = 0f;
            var token = avator.GetCancellationTokenOnDestroy();
            while (elapsed < knockbackDuration)
            {
                if (rigidbody == null) return;
                rigidbody.AddForce(horizontalDir * force * knockbackHorizontalMultiplier, ForceMode2D.Force);
                elapsed += UnityEngine.Time.fixedDeltaTime;
                await UniTask.WaitForFixedUpdate(cancellationToken: token);
            }

            // 持続力終了後、接地するまで待機.
            while (true)
            {
                if (rigidbody == null || avator == null) return;
                // 接地判定を更新.
                if (playerAttach != null)
                {
                    isGround.Value = playerAttach.GetGroundSensor();
                    if (isGround.Value)
                    {
                        break;
                    }
                }
                await UniTask.WaitForFixedUpdate(cancellationToken: token);
            }

            // 接地 → 入力不可解除、吹き飛ばし終了.
            isKnockbacking = false;
            enableAction = false;
            jumpCount = 0;
            dodgeCount = 0;
        }

        // =====================================================
        // 死亡処理
        // =====================================================
        /// <summary>
        /// 死亡時の吹き飛ばし処理.
        /// Z回転ロック解除して敵と逆方向の斜め上に吹き飛ばす.
        /// </summary>
        /// <param name="enemyPosition">敵の位置.</param>
        /// <param name="force">吹き飛ばし力.</param>
        public void OnDeath(Vector3 enemyPosition, float force = 10f)
        {
            if (rigidbody == null || avator == null) return;

            // Z回転ロック解除.
            rigidbody.constraints = RigidbodyConstraints2D.None;

            // 敵と逆方向を計算.
            Vector2 direction = (avator.transform.position - enemyPosition).normalized;

            // 斜め上方向に調整（上方向成分を追加）.
            direction = new Vector2(direction.x, Mathf.Abs(direction.y) + 0.5f).normalized;
            direction.x *= -1;
            // 吹き飛ばし.
            rigidbody.linearVelocity = Vector2.zero;
            rigidbody.AddForce(direction * force, ForceMode2D.Impulse);

            Debug.Log($"[PlayerControllModel] 死亡吹き飛ばし - 方向: {direction}, 力: {force}");
        }

        // =====================================================
        // Dispose
        // =====================================================
        public void Dispose()
        {
        }
    }
}