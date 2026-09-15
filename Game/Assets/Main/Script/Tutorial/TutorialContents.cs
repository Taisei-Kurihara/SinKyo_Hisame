using UnityEngine;
using InGame.Player;

namespace Tutorial
{
    // =====================================================
    // チュートリアル内容定義
    // 各クラスが ITutorialContent を実装し、
    // TutorialManager の tutorials リストに順番に登録する.
    // 言語切り替えは Common.LanguageManager.Instance().CurrentLanguage で行う.
    // L プロパティ: 0=JP, 1=EN
    // =====================================================

    /// <summary>ジャンプチュートリアル. ジャンプ入力検知 + 1sec待機.</summary>
    public class TutorialContent_Jump : ITutorialContent
    {
        private static int L => (int)(Common.LanguageManager.Instance(false)?.CurrentLanguage ?? GameLanguage.Japanese);

        public string ContentId => "jump";

        private static readonly string[] _titles = { "ジャンプ", "Jump" };
        public string Title => _titles[L];

        private static readonly string[] _descs =
        {
            "PS × / Xbox A / PC スペース で\n" +
            "ジャンプできます。\n\n" +
            "ジャンプ中も攻撃・回避が可能です。",

            "Press PS × / Xbox A / PC Space to jump.\n\n" +
            "You can attack and dodge while in the air.",
        };
        public string Description => _descs[L];

        public string VideoAddress => "Tutorial_Jump";

        private static readonly string[] _opNames = { "ジャンプ", "Jump" };
        public string OperationName => _opNames[L];

        private static readonly string[] _opKeys = { "PS × / Xbox A / PC スペース", "PS × / Xbox A / PC Space" };
        public string OperationKey => _opKeys[L];

        public string Supplement => "";
        public bool IsInformational => false;
        public bool CountsForCompletion => true;

        private bool inputDetected = false;
        private float waitTimer = 0f;

        public string GetProgressBarText()
        {
            string comp = L == 1 ? "(Done)" : "(完)";
            return inputDetected ? $"[I]{comp}" : "[ ]";
        }

        public bool CheckCompletion(InputSystem_Actions inputActions)
        {
            if (!inputDetected)
            {
                if (inputActions.CharacterController.Jump.WasPressedThisFrame())
                    inputDetected = true;
                return false;
            }
            waitTimer += Time.deltaTime;
            return waitTimer >= 1f;
        }

        public void ResetMonitoring() { inputDetected = false; waitTimer = 0f; }
    }

    /// <summary>移動チュートリアル. A方向1sec + D方向1sec 累積（0.1sec単位）.</summary>
    public class TutorialContent_Move : ITutorialContent
    {
        private static int L => (int)(Common.LanguageManager.Instance(false)?.CurrentLanguage ?? GameLanguage.Japanese);

        public string ContentId => "move";

        private static readonly string[] _titles = { "移動", "Movement" };
        public string Title => _titles[L];

        private static readonly string[] _descs =
        {
            "PS 左スティック / Xbox 左スティック / PC A・Dキー で\n" +
            "キャラクターを左右に移動できます。\n\n" +
            "スティックを傾けるほど\n" +
            "移動速度が上がります。",

            "Use PS Left Stick / Xbox Left Stick / PC A·D\n" +
            "to move left and right.\n\n" +
            "The further you tilt the stick,\nthe faster you move.",
        };
        public string Description => _descs[L];

        public string VideoAddress => "Tutorial_Move";

        private static readonly string[] _opNames = { "移動", "Movement" };
        public string OperationName => _opNames[L];

        private static readonly string[] _opKeys = { "PS 左スティック / Xbox 左スティック / PC A・D", "PS Left Stick / Xbox Left Stick / PC A·D" };
        public string OperationKey => _opKeys[L];

        public string Supplement => "";
        public bool IsInformational => false;
        public bool CountsForCompletion => true;

        private float leftTime = 0f;
        private float rightTime = 0f;
        private const int barSlots = 10;

        public string GetProgressBarText()
        {
            int leftDone  = Mathf.Min(Mathf.FloorToInt(leftTime  * 10f), barSlots);
            int rightDone = Mathf.Min(Mathf.FloorToInt(rightTime * 10f), barSlots);
            string leftBar  = new string('I', leftDone)  + new string(' ', barSlots - leftDone);
            string rightBar = new string('I', rightDone) + new string(' ', barSlots - rightDone);
            string c = L == 1 ? "(Done)" : "(完)";
            string leftComp  = leftDone  >= barSlots ? c : "";
            string rightComp = rightDone >= barSlots ? c : "";
            return $"A [{leftBar}]{leftComp}\nD [{rightBar}]{rightComp}";
        }

        public bool CheckCompletion(InputSystem_Actions inputActions)
        {
            var move = inputActions.CharacterController.Move.ReadValue<Vector2>();
            if (move.x < -0.1f) leftTime  += Time.deltaTime;
            if (move.x >  0.1f) rightTime += Time.deltaTime;
            return leftTime >= 1f && rightTime >= 1f;
        }

        public void ResetMonitoring() { leftTime = 0f; rightTime = 0f; }
    }

    /// <summary>弱攻撃チュートリアル. 入力即反応バー + 入力後1sec待機.</summary>
    public class TutorialContent_WeakAttack : ITutorialContent
    {
        private static int L => (int)(Common.LanguageManager.Instance(false)?.CurrentLanguage ?? GameLanguage.Japanese);

        public string ContentId => "weak_attack";

        private static readonly string[] _titles = { "弱攻撃", "Weak Attack" };
        public string Title => _titles[L];

        private static readonly string[] _descs =
        {
            "PS □ / Xbox X / PC J で弱攻撃を繰り出します。\n\n" +
            "素早く連打することで\n" +
            "連続攻撃（コンボ）に繋がります。",

            "Press PS □ / Xbox X / PC J to perform a weak attack.\n\n" +
            "Tapping rapidly connects into a combo.",
        };
        public string Description => _descs[L];

        public string VideoAddress => "Tutorial_WeakAttack";

        private static readonly string[] _opNames = { "弱攻撃", "Weak Attack" };
        public string OperationName => _opNames[L];

        private static readonly string[] _opKeys = { "PS □ / Xbox X / PC J", "PS □ / Xbox X / PC J" };
        public string OperationKey => _opKeys[L];

        public string Supplement => "";
        public bool IsInformational => false;
        public bool CountsForCompletion => true;

        private bool inputDetected = false;
        private float waitTimer = 0f;

        public string GetProgressBarText() { string c = L == 1 ? "(Done)" : "(完)"; return inputDetected ? $"[I]{c}" : "[ ]"; }

        public bool CheckCompletion(InputSystem_Actions inputActions)
        {
            if (!inputDetected)
            {
                if (inputActions.CharacterController.FirstAttack.WasPressedThisFrame())
                    inputDetected = true;
                return false;
            }
            waitTimer += Time.deltaTime;
            return waitTimer >= 1f;
        }

        public void ResetMonitoring() { inputDetected = false; waitTimer = 0f; }
    }

    /// <summary>強攻撃チュートリアル.</summary>
    public class TutorialContent_StrongAttack : ITutorialContent
    {
        private static int L => (int)(Common.LanguageManager.Instance(false)?.CurrentLanguage ?? GameLanguage.Japanese);

        public string ContentId => "strong_attack";

        private static readonly string[] _titles = { "強攻撃", "Strong Attack" };
        public string Title => _titles[L];

        private static readonly string[] _descs =
        {
            "PS △ / Xbox Y / PC K で強攻撃を繰り出します。\n\n" +
            "弱攻撃よりダメージが大きく、\n" +
            "吹き飛ばし効果があります。",

            "Press PS △ / Xbox Y / PC K to perform a strong attack.\n\n" +
            "Deals more damage than a weak attack\nand knocks enemies back.",
        };
        public string Description => _descs[L];

        public string VideoAddress => "Tutorial_StrongAttack";

        private static readonly string[] _opNames = { "強攻撃", "Strong Attack" };
        public string OperationName => _opNames[L];

        private static readonly string[] _opKeys = { "PS △ / Xbox Y / PC K", "PS △ / Xbox Y / PC K" };
        public string OperationKey => _opKeys[L];

        public string Supplement => "";
        public bool IsInformational => false;
        public bool CountsForCompletion => true;

        private bool inputDetected = false;
        private float waitTimer = 0f;

        public string GetProgressBarText() { string c = L == 1 ? "(Done)" : "(完)"; return inputDetected ? $"[I]{c}" : "[ ]"; }

        public bool CheckCompletion(InputSystem_Actions inputActions)
        {
            if (!inputDetected)
            {
                if (inputActions.CharacterController.SecondAttack.WasPressedThisFrame())
                    inputDetected = true;
                return false;
            }
            waitTimer += Time.deltaTime;
            return waitTimer >= 1f;
        }

        public void ResetMonitoring() { inputDetected = false; waitTimer = 0f; }
    }

    /// <summary>回避チュートリアル.</summary>
    public class TutorialContent_Dodge : ITutorialContent
    {
        private static int L => (int)(Common.LanguageManager.Instance(false)?.CurrentLanguage ?? GameLanguage.Japanese);

        public string ContentId => "dodge";

        private static readonly string[] _titles = { "回避", "Dodge" };
        public string Title => _titles[L];

        private static readonly string[] _descs =
        {
            "PS L2・R2 / Xbox LT・RT / PC Shift で回避を行います。\n\n" +
            "回避中は無敵時間があり、\n" +
            "敵の攻撃をすり抜けられます。\n\n" +
            "敵の攻撃タイミングに合わせて回避すると\n" +
            "パリィが発動し、反撃できます。",

            "Press PS L2·R2 / Xbox LT·RT / PC Shift to dodge.\n\n" +
            "During a dodge you are invincible\nand can pass through enemy attacks.\n\n" +
            "Timing your dodge with an enemy attack\nactivates a Parry for a counter.",
        };
        public string Description => _descs[L];

        public string VideoAddress => "Tutorial_Dodge";

        private static readonly string[] _opNames = { "回避", "Dodge" };
        public string OperationName => _opNames[L];

        private static readonly string[] _opKeys = { "PS L2・R2 / Xbox LT・RT / PC Shift", "PS L2·R2 / Xbox LT·RT / PC Shift" };
        public string OperationKey => _opKeys[L];

        public string Supplement => "";
        public bool IsInformational => false;
        public bool CountsForCompletion => true;

        private bool inputDetected = false;
        private float waitTimer = 0f;

        public string GetProgressBarText() { string c = L == 1 ? "(Done)" : "(完)"; return inputDetected ? $"[I]{c}" : "[ ]"; }

        public bool CheckCompletion(InputSystem_Actions inputActions)
        {
            if (!inputDetected)
            {
                if (inputActions.CharacterController.Dodge.WasPressedThisFrame())
                    inputDetected = true;
                return false;
            }
            waitTimer += Time.deltaTime;
            return waitTimer >= 1f;
        }

        public void ResetMonitoring() { inputDetected = false; waitTimer = 0f; }
    }

    /// <summary>回復チュートリアル.</summary>
    public class TutorialContent_Recovery : ITutorialContent
    {
        private static int L => (int)(Common.LanguageManager.Instance(false)?.CurrentLanguage ?? GameLanguage.Japanese);

        public string ContentId => "recovery";

        private static readonly string[] _titles = { "回復", "Recovery" };
        public string Title => _titles[L];

        private static readonly string[] _descs =
        {
            "PS ○ / Xbox B / PC R で回復を行います。\n\n" +
            "回復すると最大HPの1/5を回復し、\n" +
            "心拍数が 100 にリセットされます。\n\n" +
            "回復には回復ポイント（最大3回）を\n" +
            "消費します。\n" +
            "吸収ゲージが3個以上あれば\n" +
            "ゲージ消費で回復ポイントを\n" +
            "温存できます。",

            "Press PS ○ / Xbox B / PC R to recover.\n\n" +
            "Restores 1/5 of max HP\nand resets heart rate to 100.\n\n" +
            "Consumes a recovery point (max 3).\n" +
            "With 3+ absorption gauge stacks,\nyou can recover without spending a point.",
        };
        public string Description => _descs[L];

        public string VideoAddress => "Tutorial_Recovery";

        private static readonly string[] _opNames = { "回復", "Recovery" };
        public string OperationName => _opNames[L];

        private static readonly string[] _opKeys = { "PS ○ / Xbox B / PC R", "PS ○ / Xbox B / PC R" };
        public string OperationKey => _opKeys[L];

        public string Supplement => "";
        public bool IsInformational => false;
        public bool CountsForCompletion => true;

        private bool inputDetected = false;
        private float waitTimer = 0f;

        public string GetProgressBarText() { string c = L == 1 ? "(Done)" : "(完)"; return inputDetected ? $"[I]{c}" : "[ ]"; }

        public bool CheckCompletion(InputSystem_Actions inputActions)
        {
            if (!inputDetected)
            {
                if (inputActions.CharacterController.Heal.WasPressedThisFrame())
                    inputDetected = true;
                return false;
            }
            waitTimer += Time.deltaTime;
            return waitTimer >= 1f;
        }

        public void ResetMonitoring() { inputDetected = false; waitTimer = 0f; }
    }

    /// <summary>心拍数上昇時の効果説明チュートリアル.</summary>
    public class TutorialContent_HeartRateRise : ITutorialContent
    {
        private static int L => (int)(Common.LanguageManager.Instance(false)?.CurrentLanguage ?? GameLanguage.Japanese);

        public string ContentId => "heart_rate_rise";

        private static readonly string[] _titles = { "心拍数上昇の効果", "Rising Heart Rate" };
        public string Title => _titles[L];

        private static readonly string[] _descs =
        {
            "攻撃・回避・被弾などのアクションを行うと\n" +
            "心拍数が上昇します。\n\n" +
            "心拍数が高いほどアクション速度が上がりますが、\n" +
            "200 に達するとスタン状態になります。\n\n" +
            "心拍数が 100 を超えると\n" +
            "画面が赤くなり始めます。",

            "Actions like attacking, dodging, and taking hits\nincrease your heart rate.\n\n" +
            "A higher heart rate speeds up your actions,\nbut reaching 200 causes a stun.\n\n" +
            "Once your heart rate exceeds 100,\nthe screen begins to turn red.",
        };
        public string Description => _descs[L];

        public string VideoAddress => "Tutorial_HeartRateRise";
        public string OperationName => "";
        public string OperationKey => "";
        public string Supplement => "";
        public bool IsInformational => true;
        public bool CountsForCompletion => true;
        public string GetProgressBarText() => "";
        public bool CheckCompletion(InputSystem_Actions inputActions) => false;
        public void ResetMonitoring() { }
    }

    /// <summary>心拍数200到達時の説明チュートリアル.</summary>
    public class TutorialContent_HeartRate200 : ITutorialContent
    {
        private static int L => (int)(Common.LanguageManager.Instance(false)?.CurrentLanguage ?? GameLanguage.Japanese);

        public string ContentId => "heart_rate_200";

        private static readonly string[] _titles = { "心拍数 200 到達", "Heart Rate 200" };
        public string Title => _titles[L];

        private static readonly string[] _descs =
        {
            "心拍数が 200 に達すると\n" +
            "一定時間スタン状態になります。\n" +
            "スタン中は行動できません。\n\n" +
            "スタン終了後、心拍数は 100 まで下がります。\n\n" +
            "心拍数の管理が重要です。",

            "If your heart rate reaches 200,\nyou are stunned for a set duration.\nYou cannot act while stunned.\n\n" +
            "After the stun, heart rate drops to 100.\n\nManaging your heart rate is crucial.",
        };
        public string Description => _descs[L];

        public string VideoAddress => "Tutorial_HeartRate200";
        public string OperationName => "";
        public string OperationKey => "";
        public string Supplement => "";
        public bool IsInformational => true;
        public bool CountsForCompletion => true;
        public string GetProgressBarText() => "";
        public bool CheckCompletion(InputSystem_Actions inputActions) => false;
        public void ResetMonitoring() { }
    }

    /// <summary>心拍数を抑える（弱）チュートリアル.</summary>
    public class TutorialContent_HeartResist : ITutorialContent
    {
        private static int L => (int)(Common.LanguageManager.Instance(false)?.CurrentLanguage ?? GameLanguage.Japanese);

        public string ContentId => "heart_resist";

        private static readonly string[] _titles = { "心拍数を抑える（弱）", "Suppress Heart Rate (Weak)" };
        public string Title => _titles[L];

        private static readonly string[] _descs =
        {
            "PS R1 / Xbox RB / PC L を押し続けることで\n" +
            "心拍数を徐々に下げることができます。\n\n" +
            "心拍数が 100 未満の低心拍状態では、\n" +
            "画面が暗くなり視界が悪化しますが、\n" +
            "居合攻撃のダメージが増加します。\n\n" +
            "5秒以上押し続ける、\n" +
            "または心拍が 15 以上低下すると\n" +
            "居合攻撃の準備が整います。",

            "Hold PS R1 / Xbox RB / PC L to\ngradually lower your heart rate.\n\n" +
            "Below 100 heart rate, the screen darkens\nand vision worsens, but Iai damage increases.\n\n" +
            "Hold for 5+ seconds or lower heart rate\nby 15+ to ready the Iai strike.",
        };
        public string Description => _descs[L];

        public string VideoAddress => "Tutorial_HeartResist";

        private static readonly string[] _opNames = { "心拍数を抑える（弱）", "Suppress Heart Rate (Weak)" };
        public string OperationName => _opNames[L];

        private static readonly string[] _opKeys = { "PS R1 / Xbox RB / PC L", "PS R1 / Xbox RB / PC L" };
        public string OperationKey => _opKeys[L];

        private static readonly string[] _supplements = { "5秒間途切れず長押し", "Hold for 5 seconds without releasing" };
        public string Supplement => _supplements[L];

        public bool IsInformational => false;
        public bool CountsForCompletion => true;

        private float holdTime = 0f;
        private const int requiredSec = 5;

        public string GetProgressBarText()
        {
            int done = Mathf.Min(Mathf.FloorToInt(holdTime), requiredSec);
            string bar  = new string('I', done) + new string(' ', requiredSec - done);
            string comp = done >= requiredSec ? (L == 1 ? "(Done)" : "(完)") : "";
            return $"[{bar}]{comp}";
        }

        public bool CheckCompletion(InputSystem_Actions inputActions)
        {
            // RB(HeartResist) または LB(Guard) のどちらかで開始できるゲーム仕様に合わせて両方検出.
            if (inputActions.CharacterController.HeartResist.IsPressed()
                || inputActions.CharacterController.Guard.IsPressed())
            {
                holdTime += Time.deltaTime;
                if (holdTime >= requiredSec) return true;
            }
            else
            {
                holdTime = 0f;
            }
            return false;
        }

        public void ResetMonitoring() { holdTime = 0f; }
    }

    /// <summary>居合チュートリアル.</summary>
    public class TutorialContent_Iai : ITutorialContent
    {
        private static int L => (int)(Common.LanguageManager.Instance(false)?.CurrentLanguage ?? GameLanguage.Japanese);

        public string ContentId => "iai";

        private static readonly string[] _titles = { "居合", "Iai Strike" };
        public string Title => _titles[L];

        private static readonly string[] _descs =
        {
            "鼓動を抑える中に以下の条件を\n" +
            "満たした状態で攻撃ボタン\n" +
            "（PS □ / Xbox X / PC J）を押すと\n" +
            "居合攻撃が発動します。\n\n" +
            "・鼓動を抑える（弱）を 5秒以上 維持\n" +
            "・鼓動を抑える（強）を 2秒以上 維持\n" +
            "  （弱⇔強を切り替えても進行割合は引き継がれます）\n\n" +
            "ゲージは鼓動を抑えるを解除しても\n" +
            "5秒かけて徐々に減少します。\n\n" +
            "条件達成時にキャラクターの構えが変化します。\n\n" +
            "心拍数が 100 以上の場合、\n" +
            "居合の攻撃力が指数関数的に急減衰します\n" +
            "（110 付近でほぼ無効化）。\n\n" +
            "居合攻撃は大ダメージを与え、\n" +
            "発動時に心拍数を\n" +
            "次の 25 刻みの値まで上昇させます。\n\n" +
            "【キーボード操作の補足】\n" +
            "同時押しがうまくいかない場合は\n" +
            "L+O+J(K) ではなく\n" +
            "L+J(K) または O+J(K) で\n" +
            "発動を試してください。",

            "While suppressing your heart rate, meet one\nof the following conditions and press the\nattack button (PS □ / Xbox X / PC J)\nto perform an Iai strike.\n\n" +
            "· Hold Suppress (Weak) for 5+ seconds\n" +
            "· Hold Suppress (Strong) for 2+ seconds\n" +
            "  (progress carries over when switching modes)\n\n" +
            "The gauge slowly depletes over 5 seconds\nafter releasing the hold.\n\n" +
            "Your stance changes when the condition is met.\n\n" +
            "At 100+ heart rate, Iai damage decays\nexponentially (nearly zero around 110).\n\n" +
            "Iai deals massive damage and raises\nheart rate to the next multiple of 25.\n\n" +
            "[Keyboard Note]\nIf simultaneous input fails, try\nL+J(K) or O+J(K) instead of L+O+J(K).",
        };
        public string Description => _descs[L];

        public string VideoAddress => "Tutorial_Iai";

        private static readonly string[] _opNames = { "居合", "Iai Strike" };
        public string OperationName => _opNames[L];

        private static readonly string[] _opKeys = { "鼓動を抑える + 攻撃ボタン", "Suppress Heart Rate + Attack Button" };
        public string OperationKey => _opKeys[L];

        public string Supplement => "";
        public bool IsInformational => false;
        public bool CountsForCompletion => true;

        private bool iaiDetected = false;
        private float waitTimer = 0f;

        public string GetProgressBarText() => iaiDetected ? "[I](完)" : "[ ]";

        public bool CheckCompletion(InputSystem_Actions inputActions)
        {
            if (!iaiDetected)
            {
                if (PlayerPresenter.IsIaiPerformed) iaiDetected = true;
                return false;
            }
            waitTimer += Time.deltaTime;
            return waitTimer >= 1f;
        }

        public void ResetMonitoring()
        {
            iaiDetected = false;
            waitTimer   = 0f;
            PlayerPresenter.IsIaiPerformed = false;
        }
    }

    /// <summary>チャンス状態（必殺技）説明チュートリアル.</summary>
    public class TutorialContent_ChanceState : ITutorialContent
    {
        private static int L => (int)(Common.LanguageManager.Instance(false)?.CurrentLanguage ?? GameLanguage.Japanese);

        public string ContentId => "chance_state";

        private static readonly string[] _titles = { "必殺技（チャンス）", "Special Attack (Chance)" };
        public string Title => _titles[L];

        private static readonly string[] _descs =
        {
            "敵の大技（連続落下）の発動中に\n" +
            "居合攻撃を当てると、\n" +
            "大技を中断してスタンさせます。\n\n" +
            "このとき「チャンス状態」になり、\n" +
            "一定時間内に通常攻撃ボタン\n" +
            "（PS □ / Xbox X / PC J）を押すと\n" +
            "必殺技が発動します。\n\n" +
            "必殺技では9回の居合を連続で放ち、\n" +
            "大ダメージを与えます。\n\n" +
            "心拍数が低いほどダメージが上昇し、\n" +
            "心拍数100以上: 2500、\n" +
            "心拍数0: 5000まで上昇します。\n\n" +
            "必殺技発動後は心拍数が100に戻ります。",

            "During an enemy's big attack (Consecutive Meteor Drop),\nlanding an Iai strike interrupts it\nand stuns the enemy.\n\n" +
            "This starts a Chance State.\nPress the attack button\n(PS □ / Xbox X / PC J) within the time limit\nto unleash the special attack.\n\n" +
            "The special attack delivers 9 consecutive Iai strikes\nfor massive damage.\n\n" +
            "Lower heart rate increases damage:\nheart rate 100+: 2500,\nheart rate 0: up to 5000.\n\n" +
            "After the special attack, heart rate resets to 100.",
        };
        public string Description => _descs[L];

        public string VideoAddress => "";
        public string OperationName => "";
        public string OperationKey => "";
        public string Supplement => "";
        public bool IsInformational => true;
        public bool CountsForCompletion => true;
        public string GetProgressBarText() => "";
        public bool CheckCompletion(InputSystem_Actions inputActions) => false;
        public void ResetMonitoring() { }
    }

    /// <summary>心拍数を抑える（強）チュートリアル.</summary>
    public class TutorialContent_HeartResistStrong : ITutorialContent
    {
        private static int L => (int)(Common.LanguageManager.Instance(false)?.CurrentLanguage ?? GameLanguage.Japanese);

        public string ContentId => "heart_resist_strong";

        private static readonly string[] _titles = { "心拍数を抑える（強）", "Suppress Heart Rate (Strong)" };
        public string Title => _titles[L];

        private static readonly string[] _descs =
        {
            "PS R1 + L1 / Xbox RB + LB / PC L + O を\n" +
            "同時押しすることで\n" +
            "心拍数を 3倍 の速度で下げられます。\n\n" +
            "強モード中は移動速度が大幅に低下するので、\n" +
            "敵の動きに注意しながら使いましょう。\n\n" +
            "強モードでは 2秒 で居合の準備が整います\n" +
            "（弱モードは 5秒 必要）。",

            "Hold PS R1 + L1 / Xbox RB + LB / PC L + O\nsimultaneously to lower heart rate at 3× speed.\n\n" +
            "Movement speed drops greatly in strong mode,\nso watch the enemy carefully.\n\n" +
            "Strong mode readies the Iai in 2 seconds\n(weak mode requires 5 seconds).",
        };
        public string Description => _descs[L];

        public string VideoAddress => "Tutorial_HeartResistStrong";

        private static readonly string[] _opNames = { "心拍数を抑える（強）", "Suppress Heart Rate (Strong)" };
        public string OperationName => _opNames[L];

        private static readonly string[] _opKeys = { "PS R1 + L1 / Xbox RB + LB / PC L + O", "PS R1 + L1 / Xbox RB + LB / PC L + O" };
        public string OperationKey => _opKeys[L];

        private static readonly string[] _supplements = { "2秒間途切れず同時押し", "Hold simultaneously for 2 seconds" };
        public string Supplement => _supplements[L];

        public bool IsInformational => false;
        public bool CountsForCompletion => true;

        private float holdTime = 0f;
        private const int requiredSec = 2;

        public string GetProgressBarText()
        {
            int done = Mathf.Min(Mathf.FloorToInt(holdTime), requiredSec);
            string bar  = new string('I', done) + new string(' ', requiredSec - done);
            string comp = done >= requiredSec ? (L == 1 ? "(Done)" : "(完)") : "";
            return $"[{bar}]{comp}";
        }

        public bool CheckCompletion(InputSystem_Actions inputActions)
        {
            if (inputActions.CharacterController.HeartResist.IsPressed()
                && inputActions.CharacterController.Guard.IsPressed())
            {
                holdTime += Time.deltaTime;
                if (holdTime >= requiredSec) return true;
            }
            else
            {
                holdTime = 0f;
            }
            return false;
        }

        public void ResetMonitoring() { holdTime = 0f; }
    }

    /// <summary>心拍数0時の説明チュートリアル.</summary>
    public class TutorialContent_HeartRateZero : ITutorialContent
    {
        private static int L => (int)(Common.LanguageManager.Instance(false)?.CurrentLanguage ?? GameLanguage.Japanese);

        public string ContentId => "heart_rate_zero";

        private static readonly string[] _titles = { "心拍数 0 の危険", "Heart Rate 0 Danger" };
        public string Title => _titles[L];

        private static readonly string[] _descs =
        {
            "心拍数が 0 になると、\n" +
            "2 秒後から継続ダメージを受け始めます。\n\n" +
            "心拍数を下げすぎないよう注意してください。\n\n" +
            "攻撃や回避を行うことで\n" +
            "心拍数を回復できます。",

            "If your heart rate reaches 0,\nyou start taking damage over time after 2 seconds.\n\n" +
            "Be careful not to lower it too far.\n\n" +
            "Attacking or dodging will raise\nyour heart rate again.",
        };
        public string Description => _descs[L];

        public string VideoAddress => "Tutorial_HeartRateZero";
        public string OperationName => "";
        public string OperationKey => "";
        public string Supplement => "";
        public bool IsInformational => true;
        public bool CountsForCompletion => true;
        public string GetProgressBarText() => "";
        public bool CheckCompletion(InputSystem_Actions inputActions) => false;
        public void ResetMonitoring() { }
    }

    /// <summary>吸収ゲージ説明チュートリアル.</summary>
    public class TutorialContent_AbsorbGauge : ITutorialContent
    {
        private static int L => (int)(Common.LanguageManager.Instance(false)?.CurrentLanguage ?? GameLanguage.Japanese);

        public string ContentId => "absorb_gauge";

        private static readonly string[] _titles = { "吸収ゲージ", "Absorption Gauge" };
        public string Title => _titles[L];

        private static readonly string[] _descs =
        {
            "パリィ成功時に吸収ゲージが蓄積されます。\n\n" +
            "吸収ゲージが 3個 以上溜まると、\n" +
            "回復ポイントの代わりに\n" +
            "ゲージ消費で回復が可能です。\n\n" +
            "ゲージが最大容量を超えると、\n" +
            "溢れた分が自動的にHP回復に\n" +
            "変換されます。",

            "Successfully parrying builds up the absorption gauge.\n\n" +
            "With 3+ stacks, you can recover\nusing the gauge instead of a recovery point.\n\n" +
            "If the gauge overflows,\nthe excess is automatically converted to HP.",
        };
        public string Description => _descs[L];

        public string VideoAddress => "";
        public string OperationName => "";
        public string OperationKey => "";
        public string Supplement => "";
        public bool IsInformational => true;
        public bool CountsForCompletion => true;
        public string GetProgressBarText() => "";
        public bool CheckCompletion(InputSystem_Actions inputActions) => false;
        public void ResetMonitoring() { }
    }

    /// <summary>操作一覧チュートリアル（移動の前に表示、完了率に含まない）.</summary>
    public class TutorialContent_Controls : ITutorialContent
    {
        private static int L => (int)(Common.LanguageManager.Instance(false)?.CurrentLanguage ?? GameLanguage.Japanese);

        public string ContentId => "controls";

        private static readonly string[] _titles = { "操作一覧", "Controls" };
        public string Title => _titles[L];

        private static readonly string[] _descs =
        {
            "【移動】\n" +
            "PS 左スティック / Xbox 左スティック / PC A・D\n\n" +
            "【ジャンプ】\n" +
            "PS × / Xbox A / PC スペース\n\n" +
            "【回避】\n" +
            "PS L2・R2 / Xbox LT・RT / PC Shift\n\n" +
            "【弱攻撃】\n" +
            "PS □ / Xbox X / PC J\n\n" +
            "【強攻撃】\n" +
            "PS △ / Xbox Y / PC K\n\n" +
            "【回復】\n" +
            "PS ○ / Xbox B / PC R\n\n" +
            "【パリィ】\n" +
            "敵の攻撃に合わせて回避\n\n" +
            "【鼓動を抑える（弱）】\n" +
            "PS R1 / Xbox RB / PC L\n\n" +
            "【鼓動を抑える（強）】\n" +
            "PS R1 + L1 / Xbox RB + LB / PC L + O\n\n" +
            "【居合】\n" +
            "鼓動を抑える中に条件達成で攻撃ボタン",

            "[Movement]\n" +
            "PS Left Stick / Xbox Left Stick / PC A·D\n\n" +
            "[Jump]\n" +
            "PS × / Xbox A / PC Space\n\n" +
            "[Dodge]\n" +
            "PS L2·R2 / Xbox LT·RT / PC Shift\n\n" +
            "[Weak Attack]\n" +
            "PS □ / Xbox X / PC J\n\n" +
            "[Strong Attack]\n" +
            "PS △ / Xbox Y / PC K\n\n" +
            "[Recovery]\n" +
            "PS ○ / Xbox B / PC R\n\n" +
            "[Parry]\n" +
            "Dodge at the moment of an enemy attack\n\n" +
            "[Suppress Heart Rate (Weak)]\n" +
            "PS R1 / Xbox RB / PC L\n\n" +
            "[Suppress Heart Rate (Strong)]\n" +
            "PS R1 + L1 / Xbox RB + LB / PC L + O\n\n" +
            "[Iai Strike]\n" +
            "Suppress heart rate, meet conditions, then attack",
        };
        public string Description => _descs[L];

        public string VideoAddress => "";
        public string OperationName => "";
        public string OperationKey => "";
        public string Supplement => "";
        public bool IsInformational => true;
        public bool CountsForCompletion => false;
        public string GetProgressBarText() => "";
        public bool CheckCompletion(InputSystem_Actions inputActions) => false;
        public void ResetMonitoring() { }
    }

    /// <summary>パリィ説明チュートリアル（完了率に含まない）.</summary>
    public class TutorialContent_Parry : ITutorialContent
    {
        private static int L => (int)(Common.LanguageManager.Instance(false)?.CurrentLanguage ?? GameLanguage.Japanese);

        public string ContentId => "parry";

        private static readonly string[] _titles = { "パリィ", "Parry" };
        public string Title => _titles[L];

        private static readonly string[] _descs =
        {
            "敵の攻撃に合わせて回避すると\n" +
            "パリィが発動します。\n\n" +
            "パリィ成功時は敵をスタンさせ、\n" +
            "回避居合が発動できます。\n\n" +
            "パリィ不可攻撃（赤い予兆）は\n" +
            "パリィできません。回避してください。",

            "Dodge at the moment of an enemy attack\nto trigger a Parry.\n\n" +
            "A successful Parry stuns the enemy\nand allows a dodge-Iai follow-up.\n\n" +
            "Unparriable attacks (red warning)\ncannot be parried. Dodge them instead.",
        };
        public string Description => _descs[L];

        public string VideoAddress => "Tutorial_Parry";

        private static readonly string[] _opNames = { "パリィ", "Parry" };
        public string OperationName => _opNames[L];

        public string OperationKey => "";
        public string Supplement => "";
        public bool IsInformational => true;
        public bool CountsForCompletion => false;
        public string GetProgressBarText() => "";
        public bool CheckCompletion(InputSystem_Actions inputActions) => false;
        public void ResetMonitoring() { }
    }

    /// <summary>
    /// 長い説明文を分割した1ページ分のラッパー.
    /// </summary>
    public class SplitTutorialContent : ITutorialContent
    {
        private readonly ITutorialContent original;
        private readonly string splitDescription;
        private readonly int pageNumber;
        private readonly int totalPages;
        private readonly bool isLastPage;

        public SplitTutorialContent(ITutorialContent original, string splitDescription, int pageNumber, int totalPages)
        {
            this.original = original;
            this.splitDescription = splitDescription;
            this.pageNumber = pageNumber;
            this.totalPages = totalPages;
            isLastPage = (pageNumber == totalPages);
        }

        // ContentId は分割元のものをそのまま使う（TutorialManager内部判定用）.
        public string ContentId => original.ContentId;

        public string Title => $"{original.Title} [{pageNumber}/{totalPages}]";
        public string Description => splitDescription;
        public string VideoAddress => original.VideoAddress;
        public string OperationName => isLastPage ? original.OperationName : "";
        public string OperationKey => isLastPage ? original.OperationKey : "";
        public string Supplement => isLastPage ? original.Supplement : "";
        public bool IsInformational => isLastPage ? original.IsInformational : true;
        public bool CountsForCompletion => isLastPage ? original.CountsForCompletion : false;
        public string GetProgressBarText() => isLastPage ? original.GetProgressBarText() : "";
        public bool CheckCompletion(InputSystem_Actions inputActions) => isLastPage ? original.CheckCompletion(inputActions) : false;
        public void ResetMonitoring() { if (isLastPage) original.ResetMonitoring(); }
    }

}
