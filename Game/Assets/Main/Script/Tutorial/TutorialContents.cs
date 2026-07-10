using UnityEngine;
using InGame.Player;

namespace Tutorial
{
    // =====================================================
    // チュートリアル内容定義
    // 各クラスが ITutorialContent を実装し、
    // TutorialManager の tutorials リストに順番に登録する.
    // =====================================================

    /// <summary>移動チュートリアル. A方向1sec + D方向1sec 累積（0.1sec単位）.</summary>
    public class TutorialContent_Move : ITutorialContent
    {
        public string Title => "移動";
        public string Description =>
            "PS 左スティック / Xbox 左スティック / PC A・Dキー で\n" +
            "キャラクターを左右に移動できます。\n\n" +
            "スティックを傾けるほど\n" +
            "移動速度が上がります。";
        public string VideoAddress => "Tutorial_Move";
        public string OperationName => "移動";
        public string OperationKey => "PS 左スティック / Xbox 左スティック / PC A・D";
        public string Supplement => "";
        public bool IsInformational => false;
        public bool CountsForCompletion => true;

        private float leftTime = 0f;
        private float rightTime = 0f;
        private const int barSlots = 10; // 0.1sec × 10 = 1sec

        public string GetProgressBarText()
        {
            int leftDone = Mathf.Min(Mathf.FloorToInt(leftTime * 10f), barSlots);
            int rightDone = Mathf.Min(Mathf.FloorToInt(rightTime * 10f), barSlots);
            string leftBar = new string('I', leftDone) + new string(' ', barSlots - leftDone);
            string rightBar = new string('I', rightDone) + new string(' ', barSlots - rightDone);
            string leftComp = leftDone >= barSlots ? "(完)" : "";
            string rightComp = rightDone >= barSlots ? "(完)" : "";
            return $"A [{leftBar}]{leftComp}\nD [{rightBar}]{rightComp}";
        }

        public bool CheckCompletion(InputSystem_Actions inputActions)
        {
            var move = inputActions.CharacterController.Move.ReadValue<Vector2>();
            if (move.x < -0.1f) leftTime += Time.deltaTime;
            if (move.x > 0.1f) rightTime += Time.deltaTime;
            return leftTime >= 1f && rightTime >= 1f;
        }

        public void ResetMonitoring() { leftTime = 0f; rightTime = 0f; }
    }

    /// <summary>弱攻撃チュートリアル. 入力即反応バー + 入力後1sec待機.</summary>
    public class TutorialContent_WeakAttack : ITutorialContent
    {
        public string Title => "弱攻撃";
        public string Description =>
            "PS □ / Xbox X / PC J で弱攻撃を繰り出します。\n\n" +
            "素早く連打することで\n" +
            "連続攻撃（コンボ）に繋がります。";
        public string VideoAddress => "Tutorial_WeakAttack";
        public string OperationName => "弱攻撃";
        public string OperationKey => "PS □ / Xbox X / PC J";
        public string Supplement => "";
        public bool IsInformational => false;
        public bool CountsForCompletion => true;

        private bool inputDetected = false;
        private float waitTimer = 0f;

        public string GetProgressBarText()
        {
            return inputDetected ? "[I](完)" : "[ ]";
        }

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

    /// <summary>強攻撃チュートリアル. 入力即反応バー + 入力後1sec待機.</summary>
    public class TutorialContent_StrongAttack : ITutorialContent
    {
        public string Title => "強攻撃";
        public string Description =>
            "PS △ / Xbox Y / PC K で強攻撃を繰り出します。\n\n" +
            "弱攻撃よりダメージが大きく、\n" +
            "吹き飛ばし効果があります。";
        public string VideoAddress => "Tutorial_StrongAttack";
        public string OperationName => "強攻撃";
        public string OperationKey => "PS △ / Xbox Y / PC K";
        public string Supplement => "";
        public bool IsInformational => false;
        public bool CountsForCompletion => true;

        private bool inputDetected = false;
        private float waitTimer = 0f;

        public string GetProgressBarText()
        {
            return inputDetected ? "[I](完)" : "[ ]";
        }

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

    /// <summary>回避チュートリアル. 回避入力検知 + 1sec待機.</summary>
    public class TutorialContent_Dodge : ITutorialContent
    {
        public string Title => "回避";
        public string Description =>
            "PS L2・R2 / Xbox LT・RT / PC Shift で回避を行います。\n\n" +
            "回避中は無敵時間があり、\n" +
            "敵の攻撃をすり抜けられます。\n\n" +
            "敵の攻撃タイミングに合わせて回避すると\n" +
            "パリィが発動し、反撃できます。";
        public string VideoAddress => "Tutorial_Dodge";
        public string OperationName => "回避";
        public string OperationKey => "PS L2・R2 / Xbox LT・RT / PC Shift";
        public string Supplement => "";
        public bool IsInformational => false;
        public bool CountsForCompletion => true;

        private bool inputDetected = false;
        private float waitTimer = 0f;

        public string GetProgressBarText()
        {
            return inputDetected ? "[I](完)" : "[ ]";
        }

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

    /// <summary>回復チュートリアル. 回復入力検知 + 1sec待機.</summary>
    public class TutorialContent_Recovery : ITutorialContent
    {
        public string Title => "回復";
        public string Description =>
            "PS ○ / Xbox B / PC R で回復を行います。\n\n" +
            "回復すると最大HPの1/5を回復し、\n" +
            "心拍数が 100 にリセットされます。\n\n" +
            "回復には回復ポイント（最大3回）を\n" +
            "消費します。\n" +
            "吸収ゲージが3個以上あれば\n" +
            "ゲージ消費で回復ポイントを\n" +
            "温存できます。";
        public string VideoAddress => "Tutorial_Recovery";
        public string OperationName => "回復";
        public string OperationKey => "PS ○ / Xbox B / PC R";
        public string Supplement => "";
        public bool IsInformational => false;
        public bool CountsForCompletion => true;

        private bool inputDetected = false;
        private float waitTimer = 0f;

        public string GetProgressBarText()
        {
            return inputDetected ? "[I](完)" : "[ ]";
        }

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
        public string Title => "心拍数上昇の効果";
        public string Description =>
            "攻撃・回避・被弾などのアクションを行うと\n" +
            "心拍数が上昇します。\n\n" +
            "心拍数が高いほどアクション速度が上がりますが、\n" +
            "200 に達するとスタン状態になります。\n\n" +
            "心拍数が 100 を超えると\n" +
            "画面が赤くなり始めます。";
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
        public string Title => "心拍数 200 到達";
        public string Description =>
            "心拍数が 200 に達すると\n" +
            "一定時間スタン状態になります。\n" +
            "スタン中は行動できません。\n\n" +
            "スタン終了後、心拍数は 100 まで下がります。\n\n" +
            "心拍数の管理が重要です。";
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

    /// <summary>心拍数を抑える（弱）チュートリアル. 5sec途切れず長押し（1sec区切り）.</summary>
    public class TutorialContent_HeartResist : ITutorialContent
    {
        public string Title => "心拍数を抑える（弱）";
        public string Description =>
            "PS R1 / Xbox RB / PC L を押し続けることで\n" +
            "心拍数を徐々に下げることができます。\n\n" +
            "心拍数が 100 未満の低心拍状態では、\n" +
            "画面が暗くなり視界が悪化しますが、\n" +
            "居合攻撃のダメージが増加します。\n\n" +
            "5秒以上押し続ける、\n" +
            "または心拍が 15 以上低下すると\n" +
            "居合攻撃の準備が整います。";
        public string VideoAddress => "Tutorial_HeartResist";
        public string OperationName => "心拍数を抑える（弱）";
        public string OperationKey => "PS R1 / Xbox RB / PC L";
        public string Supplement => "5秒間途切れず長押し";
        public bool IsInformational => false;
        public bool CountsForCompletion => true;

        private float holdTime = 0f;
        private const int requiredSec = 5;

        public string GetProgressBarText()
        {
            int done = Mathf.Min(Mathf.FloorToInt(holdTime), requiredSec);
            string bar = new string('I', done) + new string(' ', requiredSec - done);
            string comp = done >= requiredSec ? "(完)" : "";
            return $"[{bar}]{comp}";
        }

        public bool CheckCompletion(InputSystem_Actions inputActions)
        {
            if (inputActions.CharacterController.HeartResist.IsPressed())
            {
                holdTime += Time.deltaTime;
                if (holdTime >= requiredSec)
                    return true;
            }
            else
            {
                holdTime = 0f;
            }
            return false;
        }

        public void ResetMonitoring() { holdTime = 0f; }
    }

    /// <summary>居合チュートリアル. 居合発動を検知（状態監視） + 1sec待機.</summary>
    public class TutorialContent_Iai : ITutorialContent
    {
        public string Title => "居合";
        public string Description =>
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
            "居合の攻撃力が段階的に低下します\n" +
            "（200 で最大 50% 減少）。\n\n" +
            "居合攻撃は大ダメージを与え、\n" +
            "発動時に心拍数を\n" +
            "次の 25 刻みの値まで上昇させます。\n\n" +
            "【キーボード操作の補足】\n" +
            "同時押しがうまくいかない場合は\n" +
            "L+O+J(K) ではなく\n" +
            "L+J(K) または O+J(K) で\n" +
            "発動を試してください。";
        public string VideoAddress => "Tutorial_Iai";
        public string OperationName => "居合";
        public string OperationKey => "鼓動を抑える + 攻撃ボタン";
        public string Supplement => "";
        public bool IsInformational => false;
        public bool CountsForCompletion => true;

        private bool iaiDetected = false;
        private float waitTimer = 0f;

        public string GetProgressBarText()
        {
            return iaiDetected ? "[I](完)" : "[ ]";
        }

        public bool CheckCompletion(InputSystem_Actions inputActions)
        {
            if (!iaiDetected)
            {
                if (PlayerPresenter.IsIaiPerformed)
                    iaiDetected = true;
                return false;
            }
            waitTimer += Time.deltaTime;
            return waitTimer >= 1f;
        }

        public void ResetMonitoring()
        {
            iaiDetected = false;
            waitTimer = 0f;
            PlayerPresenter.IsIaiPerformed = false;
        }
    }

    /// <summary>心拍数を抑える（強）チュートリアル. 2sec途切れず同時押し（1sec区切り）.</summary>
    public class TutorialContent_HeartResistStrong : ITutorialContent
    {
        public string Title => "心拍数を抑える（強）";
        public string Description =>
            "PS R1 + L1 / Xbox RB + LB / PC L + O を\n" +
            "同時押しすることで\n" +
            "心拍数を 3倍 の速度で下げられます。\n\n" +
            "強モード中は移動速度が大幅に低下するので、\n" +
            "敵の動きに注意しながら使いましょう。\n\n" +
            "強モードでは 2秒 で居合の準備が整います\n" +
            "（弱モードは 5秒 必要）。";
        public string VideoAddress => "Tutorial_HeartResistStrong";
        public string OperationName => "心拍数を抑える（強）";
        public string OperationKey => "PS R1 + L1 / Xbox RB + LB / PC L + O";
        public string Supplement => "2秒間途切れず同時押し";
        public bool IsInformational => false;
        public bool CountsForCompletion => true;

        private float holdTime = 0f;
        private const int requiredSec = 2;

        public string GetProgressBarText()
        {
            int done = Mathf.Min(Mathf.FloorToInt(holdTime), requiredSec);
            string bar = new string('I', done) + new string(' ', requiredSec - done);
            string comp = done >= requiredSec ? "(完)" : "";
            return $"[{bar}]{comp}";
        }

        public bool CheckCompletion(InputSystem_Actions inputActions)
        {
            if (inputActions.CharacterController.HeartResist.IsPressed()
                && inputActions.CharacterController.Guard.IsPressed())
            {
                holdTime += Time.deltaTime;
                if (holdTime >= requiredSec)
                    return true;
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
        public string Title => "心拍数 0 の危険";
        public string Description =>
            "心拍数が 0 になると、\n" +
            "2 秒後から継続ダメージを受け始めます。\n\n" +
            "心拍数を下げすぎないよう注意してください。\n\n" +
            "攻撃や回避を行うことで\n" +
            "心拍数を回復できます。";
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
        public string Title => "吸収ゲージ";
        public string Description =>
            "パリィ成功時に吸収ゲージが蓄積されます。\n\n" +
            "吸収ゲージが 3個 以上溜まると、\n" +
            "回復ポイントの代わりに\n" +
            "ゲージ消費で回復が可能です。\n\n" +
            "ゲージが最大容量を超えると、\n" +
            "溢れた分が自動的にHP回復に\n" +
            "変換されます。";
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
        public string Title => "操作一覧";
        public string Description =>
            "【移動】\n" +
            "PS 左スティック / Xbox 左スティック / PC A・D\n\n" +
            "【弱攻撃】\n" +
            "PS □ / Xbox X / PC J\n\n" +
            "【強攻撃】\n" +
            "PS △ / Xbox Y / PC K\n\n" +
            "【回避】\n" +
            "PS L2・R2 / Xbox LT・RT / PC Shift\n\n" +
            "【パリィ】\n" +
            "敵の攻撃に合わせて回避\n\n" +
            "【回復】\n" +
            "PS ○ / Xbox B / PC R\n\n" +
            "【鼓動を抑える（弱）】\n" +
            "PS R1 / Xbox RB / PC L\n\n" +
            "【鼓動を抑える（強）】\n" +
            "PS R1 + L1 / Xbox RB + LB / PC L + O\n\n" +
            "【居合】\n" +
            "鼓動を抑える中に条件達成で攻撃ボタン";
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

    /// <summary>パリィ説明チュートリアル（説明windowのみ、完了率に含まない）.</summary>
    public class TutorialContent_Parry : ITutorialContent
    {
        public string Title => "パリィ";
        public string Description =>
            "敵の攻撃に合わせて回避すると\n" +
            "パリィが発動します。\n\n" +
            "パリィ成功時は敵をスタンさせ、\n" +
            "回避居合が発動できます。\n\n" +
            "パリィ不可攻撃（赤い予兆）は\n" +
            "パリィできません。回避してください。";
        public string VideoAddress => "Tutorial_Parry";
        public string OperationName => "パリィ";
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
    /// 元の ITutorialContent を参照し、分割されたDescriptionとページ番号情報を保持する.
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
