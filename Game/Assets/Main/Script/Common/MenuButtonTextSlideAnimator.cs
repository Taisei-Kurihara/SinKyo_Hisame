using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using TMPro;
using UnityEngine;
using InGame.Common;

namespace Common
{
    /// <summary>
    /// テキストを X 方向にスライドさせるボタンアニメーション.
    ///
    /// 選択時   : labelText を現在位置 → +80 へ 0.3s・EaseOutQuint でスライド.
    /// 選択解除 : 現在 X 位置 → 0 へ速度一定（距離ベース時間）でスライド.
    /// 決定時   : 現在位置 → +80 → 0 を各 0.15s で再生.
    ///
    /// 複数ボタンが同時にリターンアニメーション中でも独立して動作するよう
    /// TextMeshProUGUI キーの CTS 辞書で管理する.
    /// </summary>
    public class MenuButtonTextSlideAnimator : IMenuButtonAnimator
    {
        private const float SlideDistance = 80f;
        private const float SlideDuration = 0.3f;
        private const float BlinkHalfDuration = 0.5f;

        // 現在フォワードスライド中のボタン.
        private MenuButtonAnimInfo? _forwardInfo;
        private CancellationTokenSource _forwardCts;

        // 各テキストのリターンアニメーション CTS（複数同時進行を許可）.
        private readonly Dictionary<TextMeshProUGUI, CancellationTokenSource> _returnCtsByText
            = new Dictionary<TextMeshProUGUI, CancellationTokenSource>();

        // 選択中ボタンのテキスト alpha 点滅.
        private CancellationTokenSource _blinkCts;
        private TextMeshProUGUI _blinkingText;

        // ============================
        // === IMenuButtonAnimator ===
        // ============================

        public void StartSelectAnim(MenuButtonAnimInfo newInfo, MenuButtonAnimInfo? prevInfo)
        {
            // 現在フォワードスライド中のボタンが切り替わった場合: 停止してリターン開始.
            if (_forwardInfo.HasValue && _forwardInfo.Value.labelText != newInfo.labelText)
            {
                _forwardCts?.Cancel();
                _forwardCts?.Dispose();
                _forwardCts = null;
                StartReturn(_forwardInfo.Value);
                _forwardInfo = null;
            }

            // 新しいボタンのリターンアニメーションが進行中ならキャンセル.
            CancelReturn(newInfo.labelText);

            // フォワードスライド開始.
            _forwardCts?.Cancel();
            _forwardCts?.Dispose();
            _forwardCts = new CancellationTokenSource();
            _forwardInfo = newInfo;
            SlideForwardAsync(newInfo, _forwardCts.Token).Forget();

            // テキスト alpha 点滅: 前のボタンを止めてアルファを戻し、新しいボタンを点滅開始.
            StopBlink();
            if (newInfo.labelText != null)
            {
                _blinkCts = new CancellationTokenSource();
                _blinkingText = newInfo.labelText;
                ImageColorAnimator.BlinkAlphaLoopAsync(newInfo.labelText, 1f, 0f, BlinkHalfDuration, _blinkCts.Token).Forget();
            }
        }

        public void CancelSelectAnim()
        {
            if (!_forwardInfo.HasValue) return;
            _forwardCts?.Cancel();
            _forwardCts?.Dispose();
            _forwardCts = null;
            StartReturn(_forwardInfo.Value);
            _forwardInfo = null;
            StopBlink();
        }

        public async UniTask PlaySubmitAnimAsync(MenuButtonAnimInfo info, CancellationToken token)
        {
            if (info.slideRT == null) return;

            // CancelSelectAnim() が開始したリターンアニメーションがあれば先に止める.
            // そのまま放置すると同一テキストに2つのモーションが競合するため.
            CancelReturn(info.labelText);

            var rt = info.slideRT;
            float startX = rt.anchoredPosition.x;
            float half = SlideDuration * 0.5f;

            await LMotion.Create(startX, SlideDistance, half)
                .WithEase(Ease.OutQuint)
                .BindToAnchoredPositionX(rt)
                .ToUniTask(token);

            await LMotion.Create(SlideDistance, 0f, half)
                .WithEase(Ease.OutQuint)
                .BindToAnchoredPositionX(rt)
                .ToUniTask(token);
        }

        public float GetSubmitEarlyFireDelay() => SlideDuration * 0.33f;

        public void DisableAll(MenuButton[][] buttons) => SnapAllToZero(buttons);

        public void ResetAll(MenuButton[][] buttons) => SnapAllToZero(buttons);

        public void OnHoverExit(MenuButtonAnimInfo info)
        {
            // フォワードスライド中のボタンがホバーアウトされたらリターン開始.
            if (_forwardInfo.HasValue && _forwardInfo.Value.labelText == info.labelText)
            {
                _forwardCts?.Cancel();
                _forwardCts?.Dispose();
                _forwardCts = null;
                StartReturn(info);
                _forwardInfo = null;
                StopBlink();
            }
        }

        public void Dispose()
        {
            _blinkCts?.Cancel();
            _blinkCts?.Dispose();
            _blinkCts = null;
            _blinkingText = null;

            _forwardCts?.Cancel();
            _forwardCts?.Dispose();
            _forwardCts = null;
            _forwardInfo = null;

            var disposeCopy = new System.Collections.Generic.List<CancellationTokenSource>(_returnCtsByText.Values);
            _returnCtsByText.Clear();
            foreach (var cts in disposeCopy)
            {
                cts?.Cancel();
                cts?.Dispose();
            }
        }

        // ============================
        // === 内部処理 ===
        // ============================

        private async UniTaskVoid SlideForwardAsync(MenuButtonAnimInfo info, CancellationToken token)
        {
            if (info.slideRT == null) return;
            var rt = info.slideRT;
            float fromX = rt.anchoredPosition.x;
            try
            {
                await LMotion.Create(fromX, SlideDistance, SlideDuration)
                    .WithEase(Ease.OutQuint)
                    .BindToAnchoredPositionX(rt)
                    .ToUniTask(token);
            }
            catch (OperationCanceledException) { }
        }

        private void StartReturn(MenuButtonAnimInfo info)
        {
            if (info.labelText == null) return;
            CancelReturn(info.labelText);
            var cts = new CancellationTokenSource();
            _returnCtsByText[info.labelText] = cts;
            SlideReturnAsync(info, cts.Token).Forget();
        }

        private async UniTaskVoid SlideReturnAsync(MenuButtonAnimInfo info, CancellationToken token)
        {
            if (info.slideRT == null) return;
            var rt = info.slideRT;
            float fromX = rt.anchoredPosition.x;

            // すでに原点にいる場合はスキップ.
            if (Mathf.Approximately(fromX, 0f))
            {
                TryCleanupOwnReturn(info.labelText, token);
                return;
            }

            // 距離ベースで時間を計算: 速度を一定に保つ.
            float duration = SlideDuration * (Mathf.Abs(fromX) / SlideDistance);
            try
            {
                await LMotion.Create(fromX, 0f, duration)
                    .WithEase(Ease.OutQuint)
                    .BindToAnchoredPositionX(rt)
                    .ToUniTask(token);
            }
            catch (OperationCanceledException) { }
            finally
            {
                // 辞書のエントリが自分のトークンと一致する場合のみ削除する.
                // 高速ナビゲーションで同じボタンへの別世代リターンが登録済みの場合は触らない.
                TryCleanupOwnReturn(info.labelText, token);
            }
        }

        /// <summary>
        /// 辞書のエントリが token と同一世代のときだけ Dispose + Remove する.
        /// </summary>
        private void TryCleanupOwnReturn(TextMeshProUGUI text, CancellationToken token)
        {
            if (text == null) return;
            if (_returnCtsByText.TryGetValue(text, out var currentCts) && currentCts.Token == token)
            {
                currentCts.Dispose();
                _returnCtsByText.Remove(text);
            }
        }

        private void CancelReturn(TextMeshProUGUI text)
        {
            if (text == null) return;
            if (_returnCtsByText.TryGetValue(text, out var cts))
            {
                cts?.Cancel();
                cts?.Dispose();
                _returnCtsByText.Remove(text);
            }
        }

        private void SnapAllToZero(MenuButton[][] buttons)
        {
            // すべてのアニメーションを中断してテキスト位置・alpha を原点にスナップ.
            StopBlink();

            _forwardCts?.Cancel();
            _forwardCts?.Dispose();
            _forwardCts = null;
            _forwardInfo = null;

            // cts.Cancel() が SlideReturnAsync の finally → TryCleanupOwnReturn → Remove() を呼ぶため
            // イテレーション中に辞書が変更される。先にコピー → クリア → キャンセルの順で対処する.
            var returnCtsCopy = new System.Collections.Generic.List<CancellationTokenSource>(_returnCtsByText.Values);
            _returnCtsByText.Clear();
            foreach (var cts in returnCtsCopy)
            {
                cts?.Cancel();
                cts?.Dispose();
            }

            if (buttons == null) return;
            foreach (var row in buttons)
            {
                if (row == null) continue;
                foreach (var mb in row)
                {
                    // テキスト alpha をリセット.
                    if (mb?.labelText != null)
                    {
                        var c = mb.labelText.color;
                        c.a = 1f;
                        mb.labelText.color = c;
                    }

                    var buttonRT = mb?.button?.transform as RectTransform;
                    var labelRT  = mb?.labelText?.rectTransform;
                    var rt = (labelRT != null && labelRT != buttonRT) ? labelRT : null;
                    if (rt == null) continue;
                    var pos = rt.anchoredPosition;
                    pos.x = 0f;
                    rt.anchoredPosition = pos;
                }
            }
        }

        /// <summary>
        /// テキスト点滅を停止し alpha を 1 に戻す.
        /// </summary>
        private void StopBlink()
        {
            _blinkCts?.Cancel();
            _blinkCts?.Dispose();
            _blinkCts = null;
            if (_blinkingText != null)
            {
                var c = _blinkingText.color;
                c.a = 1f;
                _blinkingText.color = c;
                _blinkingText = null;
            }
        }
    }
}
