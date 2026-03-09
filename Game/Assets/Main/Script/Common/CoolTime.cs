using UnityEngine;
using System;
using Cysharp.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;

namespace Common
{
    /// <summary>
    /// クールタイムイベントまとめ
    /// </summary>
    public class CoolTimeEvents
    {
        public Action OnStart;
        public Action OnUpdate;
        public Action OnEnd;
        public Action OnWarning;
        public Action OnComplete;
    }

    /// <summary>
    /// クールタイム実行部分
    /// </summary>
    public class CoolTime
    {
        private float _currentTime = 0;
        private float _limitTime = 0;

        private GameObject _destroyLinkObject;
        private CancellationTokenSource _tokenSource;

        private bool _useTimeScale = false;
        private bool _isCoolTimeActive = false;
        private bool _isEndless = false;
        private bool _isFixedUpdate = false;
        private bool _isLoop = false;
        private CoolTimeEvents _events;

        // 時間経過の係数
        private float _timeCoefficient = 1f;

        public async UniTask Run(TimeSpan time, GameObject destroyLinkObject, bool isFixed, bool isEndless, bool useTimeScale, CoolTimeEvents events)
        {
            if (_isCoolTimeActive)
            {
                events?.OnWarning?.Invoke();
                return;
            }

            // トークンの作成
            _tokenSource = CancellationTokenSource.CreateLinkedTokenSource(destroyLinkObject.GetCancellationTokenOnDestroy());
            var linkedToken = _tokenSource.Token;

            try
            {               
                _isCoolTimeActive = true;
                _currentTime = 0;
                _limitTime = (float)time.TotalSeconds;

                _destroyLinkObject = destroyLinkObject;
                _useTimeScale = useTimeScale;
                _isEndless = isEndless;
                _isFixedUpdate = isFixed;
                _events = events;

                _events?.OnStart?.Invoke();

                while (true)
                {
                    linkedToken.ThrowIfCancellationRequested();

                    // DeltaTimeを蓄積する方式に変更（途中の係数変更に対応するため）
                    float deltaTime = _isFixedUpdate ? Time.fixedDeltaTime : (_useTimeScale ? Time.deltaTime : Time.unscaledDeltaTime);
                    _currentTime += deltaTime * _timeCoefficient;

                    _events?.OnUpdate?.Invoke();

                    // 無限ループでない、かつ制限時間を超えたら終了
                    if (!_isEndless && _currentTime >= _limitTime)
                        break;

                    if (_isFixedUpdate)
                        await UniTask.Yield(PlayerLoopTiming.FixedUpdate, linkedToken);
                    else
                        await UniTask.Yield(PlayerLoopTiming.Update, linkedToken);
                }

                // キャンセルされずにループを抜けた場合のみ実行
                _events?.OnComplete?.Invoke();
            }
            catch (OperationCanceledException)
            {
                // キャンセル時はここを通る
            }
            finally
            {
                _events?.OnEnd?.Invoke();

                _tokenSource?.Dispose();
                _tokenSource = null;
                _isCoolTimeActive = false;

                
                //再度実行を行う。
                if (_isLoop)
                {
                    //実行
                    await Run(time, destroyLinkObject, isFixed, isEndless, useTimeScale, events);
                }
            }
        }

        /// <summary>
        /// 強制終了
        /// </summary>
        public void Cancel()
        {
            _tokenSource?.Cancel();
            _isCoolTimeActive = false;
        }

        public void OnLoop() { _isLoop = true; }
        public void OffLoop() { _isLoop = false; }

        /// <summary>
        /// ループも含めて完全停止
        /// </summary>
        public void CancelWithStopLoop()
        {
            Debug.Log("ループ解除+キャンセル");

            // 先にループフラグを折ることが重要
            _isLoop = false;

            // その後キャンセル
            _tokenSource?.Cancel();
            _isCoolTimeActive = false;
        }


        /// <summary>
        /// 延長
        /// </summary>
        public void Plus(float plus) => _limitTime += plus;
        /// <summary>
        /// 短縮
        /// </summary>
        public void Down(float down) => _limitTime -= down;
        /// <summary>
        /// 時間経過の係数を変化させる
        /// </summary>
        public void SetSpeedCoefficient(float coefficient) => _timeCoefficient = coefficient;
        /// <summary>
        /// 経過時間を0にする
        /// </summary>
        public void ResetTime()
        {
            _currentTime = 0;
        }

        // --- アクセッサ ---
        /// <summary>
        /// 残り時間
        /// </summary>
        public float GetRemainingTime() => _isEndless ? 0 : Mathf.Max(0, _limitTime - _currentTime);
        /// <summary>
        /// 上限時間（制限時間）
        /// </summary>
        public float GetLimitTime() => _limitTime;

        /// <summary>
        /// 現在の経過時間
        /// </summary>
        public float GetTotalTime() => _currentTime;

        /// <summary>
        /// 進捗割合（0.0 ～ 1.0）
        /// </summary>
        public float GetPercentTime() => (_limitTime > 0) ? Mathf.Clamp01(_currentTime / _limitTime) : 0;

        /// <summary>
        /// クールタイムが動いているか
        /// </summary>
        public bool IsActive() => _isCoolTimeActive;
    }

    /// <summary>
    /// クールタイム用ビルダー
    /// </summary>
    public class CoolTimeBuilder
    {
        private GameObject _linkObj;
        private TimeSpan _time;
        private bool _isFixed = false;
        private bool _isEndless = false;
        private bool _useTimeScale = true;

        private CoolTimeEvents _events = new CoolTimeEvents();
        private CoolTime _coolTime = null;

        // Thenチェーン用のリスト
        private List<CoolTimeBuilder> _thenChain = new List<CoolTimeBuilder>();

        /// <summary>
        /// 現在実行されているか
        /// </summary>
        private bool _isBaseActive = false;
        private bool _isLoopActive = false;

        private CoolTimeBuilder _currentActiveBuilder;
        private CancellationTokenSource _allTokenSource;

        // ---------- Builder生成 ----------
        public static CoolTimeBuilder Create() => new CoolTimeBuilder();

        public CoolTimeBuilder LinkTo(GameObject obj) { _linkObj = obj; return this; }
        public CoolTimeBuilder SetTime(TimeSpan t) { _time = t; return this; }
        public CoolTimeBuilder UseTimeScale(bool use = true) { _useTimeScale = use; return this; }
        public CoolTimeBuilder Endless(bool use = true) { _isEndless = use; return this; }

        //---------- Loop－－－－－－
        /// <summary>
        /// 引数として実行する場合はこちら
        /// </summary>
        /// <param name="use"></param>
        /// <returns></returns>
        public CoolTimeBuilder UseLoop(bool use = true) { _isLoopActive = use; return this; }
        public void OnLoop () { _coolTime?.OnLoop(); }
        public void OffLoop() { _coolTime?.OffLoop(); }

        // イベント登録
        public CoolTimeBuilder OnStart(Action act) { if (!_isBaseActive) _events.OnStart = act; return this; }
        public CoolTimeBuilder OnUpdate(Action act) { if (!_isBaseActive) { _isFixed = false; _events.OnUpdate = act; } return this; }
        public CoolTimeBuilder OnFixed(Action act) { if (!_isBaseActive) { _isFixed = true; _events.OnUpdate = act; } return this; }
        public CoolTimeBuilder OnEnd(Action act) { if (!_isBaseActive) _events.OnEnd = act; return this; }
        public CoolTimeBuilder OnWarning(Action act) { if (!_isBaseActive) _events.OnWarning = act; return this; }
        public CoolTimeBuilder OnComplete(Action act) { if (!_isBaseActive) _events.OnComplete = act; return this; }

        // Thenチェーン
        public CoolTimeBuilder Then(Func<CoolTimeBuilder, CoolTimeBuilder> next)
        {
            if (!_isBaseActive)
            {
                var builder = new CoolTimeBuilder();
                builder._linkObj = this._linkObj; // リンクオブジェクトを継承
                _thenChain.Add(next(builder));
            }
            return this;
        }

        // ---------- 操作系（実行中のCurrentに対して操作を行う） ----------
        public void Cancel() => _currentActiveBuilder?._coolTime?.Cancel();

        /// <summary>
        /// 現在の処理も、待機中のThenチェーンもすべてキャンセルする
        /// </summary>
        public void AllCancel()
        {
            _currentActiveBuilder?._coolTime?.Cancel();
            _allTokenSource?.Cancel();
            _allTokenSource?.Dispose();
            _allTokenSource = null;
        }
        public void CancelLoop()
        {
            _currentActiveBuilder?._coolTime?.CancelWithStopLoop();
        }
        /// <summary>
        /// 全チェーンも含めループ停止 + 全キャンセル
        /// </summary>
        public void AllCancelLoop()
        {
            _currentActiveBuilder?._coolTime?.CancelWithStopLoop();
            _allTokenSource?.Cancel();
            _allTokenSource?.Dispose();
            _allTokenSource = null;
        }

        public void Plus(float plus) => _currentActiveBuilder?._coolTime?.Plus(plus);
        public void Down(float down) => _currentActiveBuilder?._coolTime?.Down(down);
        public void ResetTime() => _currentActiveBuilder?._coolTime?.ResetTime();
        public void TimeScaleChange(float speed) => _currentActiveBuilder?._coolTime?.SetSpeedCoefficient(speed);

        // ---------- 取得系 ----------
        public float GetRemainingTime() => _currentActiveBuilder?._coolTime?.GetRemainingTime() ?? 0f;
        public float GetLimitTime() => _currentActiveBuilder?._coolTime?.GetLimitTime() ?? (float)_time.TotalSeconds;
        public bool IsActive() => _currentActiveBuilder?._coolTime?.IsActive() ?? false;
        public float GetTotalTime() => _currentActiveBuilder?._coolTime?.GetTotalTime() ?? 0f;
        public float GetPercentTime() => _currentActiveBuilder?._coolTime?.GetPercentTime() ?? 0f;

        // ---------- 実行 ----------
        public CoolTimeBuilder Run()
        {
            if (_linkObj == null)
                throw new InvalidOperationException("LinkTo が設定されていません。必ず LinkTo() を呼んでください。");

            RunInternal().Forget();
            return this;
        }

        // 内部処理（Then対応）
        private async UniTaskVoid RunInternal()
        {
            if (_isBaseActive)
            {
                // すでに起動中の場合、現在のインスタンスでWarningイベント付きのRunを試みる
                if (_currentActiveBuilder != null)
                {
                    await _currentActiveBuilder.RunInternalForThen();
                }
                return;
            }

            _isBaseActive = true;
            _currentActiveBuilder = this;

            _allTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_linkObj.GetCancellationTokenOnDestroy());
            var linkToken = _allTokenSource.Token;

            try
            {
                // 自身の実行
                await RunInternalForThen();
                linkToken.ThrowIfCancellationRequested();

                // Thenチェーンの実行
                foreach (var nextBuilder in _thenChain)
                {
                    if (_linkObj == null) return;

                    _currentActiveBuilder = nextBuilder;
                    await nextBuilder.RunInternalForThen();

                    linkToken.ThrowIfCancellationRequested();
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log("CoolTime: 全ての処理がキャンセルされました。");
            }
            finally
            {
                _allTokenSource?.Dispose();
                _allTokenSource = null;
                _thenChain.Clear();
                _isBaseActive = false;
                _currentActiveBuilder = null;
            }
        }

        private async UniTask RunInternalForThen()
        {
            if (_coolTime == null)
                _coolTime = new CoolTime();

            //ループが行われる設定の場合
            if (_isLoopActive)
            {
                _coolTime.OnLoop();
            }

            await _coolTime.Run(_time, _linkObj, _isFixed, _isEndless, _useTimeScale, _events);
        }
    }
}