using System;
using System.Threading;
using Audio;
using Cysharp.Threading.Tasks;
using InGame.Common;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using R3;
using System.Collections.Generic;

namespace Common
{
    public abstract class MenuButtonEventer : MonoBehaviour
    {
        #region 変数 | セッター / ゲッター

        #region flag

        // trueの間のみボタン操作を受け付ける.
        private bool eventerEnable = true;
        // --- Submit中フラグ（連打防止） ---
        private bool isSubmitting = false;

        // Submit アニメーション制御（Immediate 連打時のキャンセル・世代管理）.
        private CancellationTokenSource submitAnimCts;
        private int submitAnimId = 0;

        // ボタン決定SE.
        private SEPlayer menuSEPlayer;



        /// <summary> UI操作を有効化する. </summary>
        public void EnableEventer()
        {
            eventerEnable = true;
            isSubmitting = false;
        }

        /// <summary>
        /// UI操作を無効化する（ビデオ再生中・シーン遷移中等）.
        /// ブリンクを停止し、全ボタン色を白に戻す.
        /// </summary>
        public void DisableEventer()
        {
            eventerEnable = false;
            isSubmitting = false;
            CancelBlink();

            // 全ボタン色をリセット（白 = UI元の色）.
            SetAllButtonsColor(Color.white);
        }

        #endregion

        #region ColorAnimation

        // 非選択時の色.
        private static readonly Color colorDeselected = new Color(0.8f, 0.8f, 0.8f, 0.8f);
        // 選択時・ブリンクの明るい側.
        private static readonly Color colorSelected = Color.white;
        // ブリンクアニメーション1サイクルの片道時間(秒).
        private const float blinkHalfDuration = 0.5f;
        // --- 非同期ブリンクアニメーション制御 ---
        private CancellationTokenSource blinkCts;
        // R3購読の破棄用.
        private CompositeDisposable disposables = new CompositeDisposable();
        #endregion

        #region button列管理

        // ボタン二次元配列: buttons[行(Y)][列(X)].
        // 継承先の Init() で設定する.
        protected virtual MenuButton[][] buttons { get; set; }

        // ボタン → アクション対応表. 継承先で必ず実装する.
        protected abstract ButtonSlotDictionary buttonsSlot { get; }

        // 前回選択されていたボタン.
        private MenuButton previousButton;

        // イベント登録済みボタン（重複登録防止用）.
        private HashSet<MenuButton> alreadyButtonEvent;

        #endregion

        #region Input

        // InputSystem入力アクション.
        protected InputSystem_Actions action;

        // 現在のカーソル位置（二次元配列のインデックス）.
        private int currentIndex_x = 0;
        private int currentIndex_y = 0;

        // 初期カーソル位置. 継承先の Init() で変更可能.
        protected int initialIndex_x = 0;
        protected int initialIndex_y = 0;

        // --- ナビゲーション入力制御 ---
        private float navigateCooldown = 0.2f;
        private float lastNavigateTime = 0f;

        // Navigate入力が一度ニュートラルに戻るまで方向入力を受け付けないガード.
        private bool navigationReady = false;



        /// <summary>
        /// カーソル位置を先頭(0,0)にリセットし、ボタンを再選択する.
        /// メニュー再表示時に使用.
        /// </summary>
        public void ResetSelection()
        {
            currentIndex_y = initialIndex_y;
            currentIndex_x = initialIndex_x;
            navigationReady = false;
            isSubmitting = false;

            CancelBlink();
            SetAllButtonsColor(colorDeselected);

            if (buttons != null && buttons.Length > initialIndex_x
                && buttons[initialIndex_x] != null && buttons[initialIndex_x].Length > initialIndex_y
                && buttons[initialIndex_x][initialIndex_y] != null)
            {
                SelectButton(buttons[initialIndex_x][initialIndex_y]);
            }
        }

        #endregion

        #endregion



        public void Awake()
        {
            alreadyButtonEvent = new HashSet<MenuButton>();

            // InputSystem入力アクションを取得.
            action = InputSystemActionsManager.Instance().GetInputSystem_Actions();

            // 継承先でボタン配列を設定.
            Init();

            // 初期カーソル位置を反映（Init() 内で initialIndex_x/y を変更した場合に対応）.
            currentIndex_y = initialIndex_y;
            currentIndex_x = initialIndex_x;

            // labelText が未設定のボタンを自動初期化（Inspector 未設定時のフォールバック）.
            if (buttons != null)
            {
                foreach (var row in buttons)
                    foreach (var mb in row)
                        mb?.InitMenuButtonLabel();
            }

            // 言語切り替えイベント購読.
            var lm = LanguageManager.Instance(false);
            if (lm != null) lm.OnLanguageChanged += OnLanguageChanged;

            // 起動時に現在の言語でラベルを即時適用（サブクラスの OnLanguageChanged を呼ぶ）.
            if (lm != null) OnLanguageChanged(lm.CurrentLanguage);

            // 全ボタンを非選択色に初期化してから、初期カーソル位置のボタンを選択.
            SetAllButtonsColor(colorDeselected);

            if (buttons != null && buttons.Length > 0 && buttons[currentIndex_x] != null
                && currentIndex_y < buttons[currentIndex_x].Length
                && buttons[currentIndex_x][currentIndex_y] != null)
            {
                SelectButton(buttons[currentIndex_x][currentIndex_y]);
            }

            // シーン遷移直後の残留入力を弾くためクールダウンを初期化.
            lastNavigateTime = Time.unscaledTime;

            // ボタンSE初期化.
            InitMenuSEAsync().Forget();

            // 全ボタンにイベント登録.
            foreach (var row in buttons)
            {
                foreach (var mb in row)
                {
                    if (mb == null || mb.button == null) continue;
                    if (!alreadyButtonEvent.Contains(mb))
                    {
                        alreadyButtonEvent.Add(mb);

                        // EventSystemの自動ナビゲーションを無効化.
                        var buttonNav = mb.button.navigation;
                        buttonNav.mode = Navigation.Mode.None;
                        mb.button.navigation = buttonNav;

                        // ButtonのTransitionによるImage.color上書きを防止.
                        // 色制御は ImageColorAnimator で行うため Transition=None にする.
                        mb.button.transition = Selectable.Transition.None;

                        // Animatorが残っている場合は無効化（Image.colorを上書きするため）.
                        var animator = mb.button.GetComponent<Animator>();
                        if (animator != null)
                            animator.enabled = false;

                        // マウスホバー/クリック用イベント登録.
                        AddHoverEvents(mb);
                        AddButtonEvent(mb);
                    }
                }
            }
        }

        private void OnDestroy()
        {
            var lm = LanguageManager.Instance(false);
            if (lm != null) lm.OnLanguageChanged -= OnLanguageChanged;
            submitAnimCts?.Cancel();
            submitAnimCts?.Dispose();
            submitAnimCts = null;
            CancelBlink();
            disposables?.Dispose();
            if (menuSEPlayer != null)
            {
                menuSEPlayer.ReleaseAll();
                UnityEngine.Object.Destroy(menuSEPlayer.gameObject);
                menuSEPlayer = null;
            }
        }

        private async UniTaskVoid InitMenuSEAsync()
        {
            menuSEPlayer = SEPlayer.Create("MenuButtonSE");
            await menuSEPlayer.LoadClipsAsync("SE_Button");
        }

        protected virtual void Init() { }
        protected virtual void OnButtonSelected(MenuButton button) { }
        protected virtual void OnButtonSubmitted(MenuButton button) { }
        /// <summary>言語変更時に呼ばれる. 継承先でボタンラベルを更新する.</summary>
        protected virtual void OnLanguageChanged(GameLanguage lang) { }


        #region 入力対応関数
        /// <summary>
        /// R3でボタンのOnClickを購読し、押下時にSubmitアニメーション+buttonsSlot発火.
        /// </summary>
        public void AddButtonEvent(MenuButton target)
        {
            disposables.Add(
                target.button.OnClickAsObservable()
                .Subscribe(_ =>
                {
                    if (!eventerEnable) return;
                    var mode = buttonsSlot?.GetFireMode(target.index) ?? ButtonFireMode.AfterAnimation;
                    // Immediate: 連打時もアニメーション中断して再発火. それ以外: isSubmitting 中は無視.
                    if (!isSubmitting || mode == ButtonFireMode.Immediate)
                    {
                        OnButtonSubmitted(target);
                        SubmitButton(target);
                    }
                }));
        }

        /// <summary>
        /// マウスホバー用EventTriggerを登録.
        /// PointerEnter: ボタン選択（ブリンク開始）
        /// PointerExit:  非選択色に戻す
        /// </summary>
        protected void AddHoverEvents(MenuButton target)
        {
            EventTrigger trigger = target.button.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = target.button.gameObject.AddComponent<EventTrigger>();

            // --- PointerEnter ---
            EventTrigger.Entry entryEnter = new EventTrigger.Entry();
            entryEnter.eventID = EventTriggerType.PointerEnter;
            entryEnter.callback.AddListener((eventData) => {
                if (!eventerEnable) return;
                // カーソルインデックスを更新.
                for (int x = 0; x < buttons.Length; x++)
                {
                    if (buttons[x] == null) continue;
                    for (int y = 0; y < buttons[x].Length; y++)
                    {
                        if (buttons[x][y] == target)
                        {
                            currentIndex_x = x;
                            currentIndex_y = y;
                        }
                    }
                }
                SelectButton(target);
            });
            trigger.triggers.Add(entryEnter);

            // --- PointerExit ---
            EventTrigger.Entry entryExit = new EventTrigger.Entry();
            entryExit.eventID = EventTriggerType.PointerExit;
            entryExit.callback.AddListener((eventData) => {
                if (!eventerEnable) return;
                var img = target.button.GetComponent<Image>();
                if (img != null)
                    img.color = colorDeselected;
            });
            trigger.triggers.Add(entryExit);
        }
        #endregion


        public void Update()
        {
            // UI入力が無効になっていた場合に毎フレーム有効化を保証（タイトル等でのマウス操作問題対策）.
            if (action != null && !action.UI.enabled)
                action.UI.Enable();
            CursolUpdate();
        }

        private void CursolUpdate()
        {
            if (!eventerEnable) return;
            if (buttons == null || buttons.Length == 0) return;

            Vector2 nav = action.UI.Navigate.ReadValue<Vector2>();

            // 初回ガード: Navigateが一度ニュートラルに戻るまで入力を無視.
            if (!navigationReady)
            {
                if (nav.sqrMagnitude < 0.25f)
                    navigationReady = true;
                return;
            }

            // ナビゲーション入力があるときのみ選択変更（cooldown付き）.
            if (Time.unscaledTime - lastNavigateTime >= navigateCooldown)
            {
                if (nav.y > 0.5f)
                {
                    currentIndex_y = (currentIndex_y - 1 + buttons[currentIndex_x].Length) % buttons[currentIndex_x].Length;
                    SelectButton(buttons[currentIndex_x][currentIndex_y]);
                    lastNavigateTime = Time.unscaledTime;
                }
                else if (nav.y < -0.5f)
                {
                    currentIndex_y = (currentIndex_y + 1) % buttons[currentIndex_x].Length;
                    SelectButton(buttons[currentIndex_x][currentIndex_y]);
                    lastNavigateTime = Time.unscaledTime;
                }
                else if (nav.x > 0.5f && buttons.Length > 1)
                {
                    currentIndex_x = (currentIndex_x + 1) % buttons.Length;
                    currentIndex_y = Mathf.Clamp(currentIndex_y, 0, buttons[currentIndex_x].Length - 1);
                    SelectButton(buttons[currentIndex_x][currentIndex_y]);
                    lastNavigateTime = Time.unscaledTime;
                }
                else if (nav.x < -0.5f && buttons.Length > 1)
                {
                    currentIndex_x = (currentIndex_x - 1 + buttons.Length) % buttons.Length;
                    currentIndex_y = Mathf.Clamp(currentIndex_y, 0, buttons[currentIndex_x].Length - 1);
                    SelectButton(buttons[currentIndex_x][currentIndex_y]);
                    lastNavigateTime = Time.unscaledTime;
                }
            }

            // 決定入力（cooldown とは独立）.
            if (action.UI.Submit.WasPressedThisFrame())
            {
                var mb = buttons[currentIndex_x][currentIndex_y];
                var mode = buttonsSlot?.GetFireMode(mb.index) ?? ButtonFireMode.AfterAnimation;
                if (!isSubmitting || mode == ButtonFireMode.Immediate)
                {
                    OnButtonSubmitted(mb);
                    SubmitButton(mb);
                }
            }
        }

        private void SelectButton(MenuButton menuButton)
        {
            if (menuButton == null || menuButton.button == null) return;
            CancelBlink();

            // 前のボタン → 非選択色.
            if (previousButton != null && previousButton != menuButton)
            {
                var prevImg = previousButton.button.GetComponent<Image>();
                if (prevImg != null)
                    prevImg.color = colorDeselected;
            }

            // EventSystem上で選択状態に設定.
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(menuButton.button.gameObject);
            menuButton.button.Select();

            // 現在のボタンを記録.
            previousButton = menuButton;

            // ブリンクアニメーション開始.
            StartBlink(menuButton);

            // SE等のコールバック.
            OnButtonSelected(menuButton);
        }

        private void StartBlink(MenuButton menuButton)
        {
            CancelBlink();
            var img = menuButton.button.GetComponent<Image>();
            if (img == null) return;
            blinkCts = new CancellationTokenSource();
            ImageColorAnimator.BlinkLoopAsync(img, colorSelected, colorDeselected, blinkHalfDuration, blinkCts.Token).Forget();
        }

        private void CancelBlink()
        {
            blinkCts?.Cancel();
            blinkCts?.Dispose();
            blinkCts = null;
        }

        /// <summary>
        /// ボタン決定処理. ブリンク1サイクル再生後にbuttonsSlotのアクションを発火.
        /// </summary>
        private void SubmitButton(MenuButton menuButton)
        {
            // 既存アニメーションをキャンセル（Immediate 連打時に前アニメを中断）.
            submitAnimCts?.Cancel();
            submitAnimCts?.Dispose();
            submitAnimCts = new CancellationTokenSource();
            int myId = ++submitAnimId;

            isSubmitting = true;
            menuSEPlayer?.Play("SE_Button");
            CancelBlink();
            RunSubmitAnimAsync(menuButton, submitAnimCts.Token, myId).Forget();
        }

        private async UniTaskVoid RunSubmitAnimAsync(MenuButton menuButton, CancellationToken submitToken, int animId)
        {
            var img = menuButton.button.GetComponent<Image>();
            var fireMode = buttonsSlot?.GetFireMode(menuButton.index) ?? ButtonFireMode.AfterAnimation;

            // Submit キャンセル + オブジェクト破棄 の両方で中断できるようリンク.
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                submitToken, gameObject.GetCancellationTokenOnDestroy());
            var token = linkedCts.Token;

            try
            {
                if (fireMode == ButtonFireMode.Immediate)
                {
                    // 即時発火してからアニメーション（連打でキャンセル・再発火可）.
                    buttonsSlot?.InvokeByKey(menuButton.index);
                    if (img != null)
                        await ImageColorAnimator.BlinkOnceAsync(img, colorSelected, colorDeselected, blinkHalfDuration, token);
                }
                else if (fireMode == ButtonFireMode.EarlyFire)
                {
                    // アニメーション開始と並走し、0.33 倍経過時点で発火（シーン移動系）.
                    float earlyDelay = blinkHalfDuration * 2f * 0.33f;
                    if (img != null)
                    {
                        var animTask = ImageColorAnimator.BlinkOnceAsync(img, colorSelected, colorDeselected, blinkHalfDuration, token);
                        await UniTask.Delay(TimeSpan.FromSeconds(earlyDelay), ignoreTimeScale: true, cancellationToken: token);
                        buttonsSlot?.InvokeByKey(menuButton.index);
                        await animTask;
                    }
                    else
                    {
                        await UniTask.Delay(TimeSpan.FromSeconds(earlyDelay), ignoreTimeScale: true, cancellationToken: token);
                        buttonsSlot?.InvokeByKey(menuButton.index);
                    }
                }
                else // AfterAnimation（デフォルト）
                {
                    if (img != null)
                        await ImageColorAnimator.BlinkOnceAsync(img, colorSelected, colorDeselected, blinkHalfDuration, token);
                    buttonsSlot?.InvokeByKey(menuButton.index);
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                // 最新世代のアニメーションのみ isSubmitting をリセット.
                if (submitAnimId == animId)
                    isSubmitting = false;
            }
        }

        /// <summary>
        /// 全ボタンのImage.colorを指定色に設定.
        /// </summary>
        private void SetAllButtonsColor(Color color)
        {
            if (buttons == null) return;
            foreach (var row in buttons)
            {
                if (row == null) continue;
                foreach (var mb in row)
                {
                    if (mb == null || mb.button == null) continue;
                    var img = mb.button.GetComponent<Image>();
                    if (img != null) img.color = color;
                }
            }
        }
    }
}

// ============================================================
// BiDictionary<TKey, TValue>
// 双方向一意マッピング. TKey ↔ TValue の両方向で一意性を保証.
// ============================================================
public class BiDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
{
    private readonly Dictionary<TKey, TValue> _forward = new();
    private readonly Dictionary<TValue, TKey> _reverse = new();

    public void Add(TKey key, TValue value)
    {
        // 両方向で一意性を強制（事故防止）.
        if (_forward.ContainsKey(key)) throw new System.ArgumentException($"Key重複: {key}");
        if (_reverse.ContainsKey(value)) throw new System.ArgumentException($"Value重複: {value}");
        _forward[key] = value;
        _reverse[value] = key;
    }

    // コレクション初期化子 ({ {key, value}, ... }) を使えるようにする.
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _forward.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    // [] アクセス.
    public TValue this[TKey key] => _forward[key];

    public TValue GetByKey(TKey key) => _forward[key];
    public TKey GetByValue(TValue value) => _reverse[value];

    /// <summary>
    /// 既存キーの値のみ更新する（サブクラスのアクションリスト等は保持したまま）.
    /// </summary>
    public void UpdateValueByKey(TKey key, TValue newValue)
    {
        if (!_forward.TryGetValue(key, out var oldValue))
            throw new System.ArgumentException($"Key not found: {key}");
        _reverse.Remove(oldValue);
        _forward[key] = newValue;
        _reverse[newValue] = key;
    }

    public bool RemoveByKey(TKey key)
    {
        if (!_forward.TryGetValue(key, out var value)) return false;
        _forward.Remove(key);
        _reverse.Remove(value);
        return true;
    }

    public bool RemoveByValue(TValue value)
    {
        if (!_reverse.TryGetValue(value, out var key)) return false;
        _reverse.Remove(value);
        _forward.Remove(key);
        return true;
    }
}

// ============================================================
// ButtonFireMode
// ボタン発火タイミングの種別.
// ============================================================
public enum ButtonFireMode
{
    AfterAnimation, // アニメーション完了後に発火（デフォルト）.
    Immediate,      // 即時発火（メニュー開閉・トグル系）. 連打でアニメーション中断・再発火.
    EarlyFire,      // アニメーション開始から 0.33 倍経過時点で発火（シーン移動系）.
}

// ============================================================
// ButtonSlotDictionary
// BiDictionary<MenuButtonIndex, int> を継承し、
// 各スロットに List<UnityAction> と ButtonFireMode を紐付ける.
// Init() 内で { index, rowIndex, action } の形で初期化し、
// ButtonEvents の代わりに InvokeByKey() で発火する.
// ============================================================
public class ButtonSlotDictionary : BiDictionary<MenuButtonIndex, int>
{
    private readonly Dictionary<MenuButtonIndex, List<UnityAction>> _actions = new();
    private readonly Dictionary<MenuButtonIndex, ButtonFireMode>    _fireModes = new();

    // 2引数 Add（アクションなし・デフォルト AfterAnimation）.
    public new void Add(MenuButtonIndex key, int value)
    {
        base.Add(key, value);
        _actions[key]   = new List<UnityAction>();
        _fireModes[key] = ButtonFireMode.AfterAnimation;
    }

    // 3引数 Add（デフォルト AfterAnimation）.
    public void Add(MenuButtonIndex key, int value, UnityAction action)
    {
        Add(key, value, action, ButtonFireMode.AfterAnimation);
    }

    // 4引数 Add（コレクション初期化子対応: { index, rowIndex, action, mode }）.
    public void Add(MenuButtonIndex key, int value, UnityAction action, ButtonFireMode mode)
    {
        base.Add(key, value);
        _actions[key]   = new List<UnityAction>();
        _fireModes[key] = mode;
        if (action != null) _actions[key].Add(action);
    }

    /// <summary> ボタンの発火モードを取得. 未登録の場合は AfterAnimation. </summary>
    public ButtonFireMode GetFireMode(MenuButtonIndex key)
    {
        return _fireModes.TryGetValue(key, out var mode) ? mode : ButtonFireMode.AfterAnimation;
    }

    /// <summary>
    /// 既存スロットにアクションを追加（複数登録可）.
    /// </summary>
    public void AddAction(MenuButtonIndex key, UnityAction action)
    {
        if (!_actions.ContainsKey(key)) _actions[key] = new List<UnityAction>();
        if (action != null) _actions[key].Add(action);
    }

    /// <summary>
    /// 対応するスロットの全アクションを発火.
    /// </summary>
    public void InvokeByKey(MenuButtonIndex key)
    {
        if (_actions.TryGetValue(key, out var list))
            foreach (var a in list) a?.Invoke();
    }
}
