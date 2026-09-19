using Cysharp.Threading.Tasks;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using InGame.Common;

namespace Common
{
    /// <summary>
    /// デフォルトのボタンアニメーション: Image.color のブリンク.
    /// MenuButtonEventer から分離した既存の動作をそのまま保持する.
    /// </summary>
    public class MenuButtonColorAnimator : IMenuButtonAnimator
    {
        private static readonly Color colorDeselected = new Color(0.8f, 0.8f, 0.8f, 0.8f);
        private static readonly Color colorSelected   = Color.white;
        private const float blinkHalfDuration = 0.5f;

        private CancellationTokenSource blinkCts;

        public void StartSelectAnim(MenuButtonAnimInfo newInfo, MenuButtonAnimInfo? prevInfo)
        {
            CancelSelectAnim();

            // 前のボタンを非選択色にスナップ.
            if (prevInfo.HasValue && prevInfo.Value.img != null)
                prevInfo.Value.img.color = colorDeselected;
            if (prevInfo.HasValue && prevInfo.Value.labelText != null)
                prevInfo.Value.labelText.color = colorDeselected;

            if (newInfo.img == null) return;

            blinkCts = new CancellationTokenSource();
            ImageColorAnimator.BlinkLoopAsync(newInfo.img, colorSelected, colorDeselected, blinkHalfDuration, blinkCts.Token).Forget();
            // テキストも同じ CTS で点滅させる.
            if (newInfo.labelText != null)
                ImageColorAnimator.BlinkLoopAsync(newInfo.labelText, colorSelected, colorDeselected, blinkHalfDuration, blinkCts.Token).Forget();
        }

        public void CancelSelectAnim()
        {
            blinkCts?.Cancel();
            blinkCts?.Dispose();
            blinkCts = null;
        }

        public async UniTask PlaySubmitAnimAsync(MenuButtonAnimInfo info, CancellationToken token)
        {
            if (info.img != null)
                await ImageColorAnimator.BlinkOnceAsync(info.img, colorSelected, colorDeselected, blinkHalfDuration, token);
        }

        public float GetSubmitEarlyFireDelay() => blinkHalfDuration * 2f * 0.33f;

        public void DisableAll(MenuButton[][] buttons) => SetAllColor(buttons, Color.white);

        public void ResetAll(MenuButton[][] buttons) => SetAllColor(buttons, colorDeselected);

        public void OnHoverExit(MenuButtonAnimInfo info)
        {
            if (info.img != null)
                info.img.color = colorDeselected;
            if (info.labelText != null)
                info.labelText.color = colorDeselected;
        }

        public void Dispose() => CancelSelectAnim();

        private static void SetAllColor(MenuButton[][] buttons, Color color)
        {
            if (buttons == null) return;
            foreach (var row in buttons)
            {
                if (row == null) continue;
                foreach (var mb in row)
                {
                    if (mb?.button == null) continue;
                    var img = mb.button.GetComponent<Image>();
                    if (img != null) img.color = color;
                    if (mb.labelText != null) mb.labelText.color = color;
                }
            }
        }
    }
}
