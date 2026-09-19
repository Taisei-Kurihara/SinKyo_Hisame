using Cysharp.Threading.Tasks;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using InGame.Common;

namespace Common
{
    /// <summary>
    /// ボタンアニメーションの種別.
    /// MenuButtonEventer の Init() 内で設定し、Awake でストラテジーを生成する.
    /// </summary>
    public enum MenuButtonAnimatorType
    {
        ColorBlink, // デフォルト: Image.color のブリンク.
        TextSlide,  // テキストを X 方向にスライドさせる.
    }

    /// <summary>
    /// アニメーションストラテジーに渡すボタン情報.
    /// MenuButton から Image・TextMeshProUGUI を一括取得してキャッシュする.
    /// </summary>
    public readonly struct MenuButtonAnimInfo
    {
        public readonly MenuButton button;
        public readonly Image img;
        public readonly TextMeshProUGUI labelText;
        /// <summary>
        /// TextSlide アニメーションで移動する RectTransform.
        /// MenuButton.textSlideRT が設定されていればそれを使用し、
        /// 未設定の場合は labelText.rectTransform にフォールバックする.
        /// </summary>
        public readonly RectTransform slideRT;

        public MenuButtonAnimInfo(MenuButton mb)
        {
            button    = mb;
            img       = mb?.button?.GetComponent<Image>();
            labelText = mb?.labelText;

            // textSlideRT が Inspector で設定されていれば優先使用（視覚テキスト専用 RT）.
            // 未設定の場合は labelText.rectTransform を使うが、button 本体 RT と同一オブジェクトなら
            // null（アニメーションしない）とする — 当たり判定 RT を動かさないための保護.
            if (mb?.textSlideRT != null)
            {
                slideRT = mb.textSlideRT;
            }
            else
            {
                var buttonRT = mb?.button?.transform as RectTransform;
                var labelRT  = mb?.labelText?.rectTransform;
                slideRT = (labelRT != null && labelRT != buttonRT) ? labelRT : null;
            }
        }
    }

    /// <summary>
    /// ボタンアニメーションのストラテジーインターフェース.
    /// MenuButtonEventer が保持し、選択・決定・無効化などの各タイミングで呼ぶ.
    /// </summary>
    public interface IMenuButtonAnimator
    {
        /// <summary>新しいボタンが選択された時のアニメーション開始. prevInfo は直前のボタン（なければ null）.</summary>
        void StartSelectAnim(MenuButtonAnimInfo newInfo, MenuButtonAnimInfo? prevInfo);

        /// <summary>選択アニメーションをキャンセル（DisableEventer / ResetSelection 等）.</summary>
        void CancelSelectAnim();

        /// <summary>決定アニメーションを再生して完了まで待機する.</summary>
        UniTask PlaySubmitAnimAsync(MenuButtonAnimInfo info, CancellationToken token);

        /// <summary>EarlyFire モード用の早期発火遅延時間（秒）.</summary>
        float GetSubmitEarlyFireDelay();

        /// <summary>全ボタンを無効化状態（白など）にリセットする.</summary>
        void DisableAll(MenuButton[][] buttons);

        /// <summary>全ボタンを非選択状態にリセットする.</summary>
        void ResetAll(MenuButton[][] buttons);

        /// <summary>マウスカーソルがボタンから外れた時の処理.</summary>
        void OnHoverExit(MenuButtonAnimInfo info);

        /// <summary>リソース解放.</summary>
        void Dispose();
    }
}
