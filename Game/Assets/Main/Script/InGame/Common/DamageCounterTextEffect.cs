using UnityEngine;
using TMPro;
using InGame.Player;

namespace InGame
{
    /// <summary>
    /// ダメージカウンターのテキスト設定・フェード.
    /// TMP子オブジェクトにアタッチする.
    /// </summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class DamageCounterTextEffect : MonoBehaviour
    {
        // --- カラー定数（ZanHitControllerと同色） ---
        private static readonly Color IaiColor = new Color(0.3f, 0.7f, 0.7f, 1f);
        private static readonly Color NormalColor = new Color(0.8f, 0.1f, 0.1f, 1f);

        // --- フェード設定 ---
        [Header("Fade")]
        [Tooltip("フェード開始タイミング（0-1）")]
        [SerializeField] private float fadeStartProgress = 0.6f;

        // --- TMP参照 ---
        [Header("References")]
        [SerializeField] private TextMeshProUGUI tmp;

        // --- ランタイム ---
        private float currentProgress;

        private void Awake()
        {
            if (tmp == null)
                tmp = GetComponent<TextMeshProUGUI>();
        }

        /// <summary>
        /// DamageCounterから呼ばれるテキスト初期設定.
        /// </summary>
        public void Setup(float damage, PlayerAttackType attackType)
        {
            currentProgress = 0f;

            bool isIai = (attackType == PlayerAttackType.Iai);

            // テキスト内容.
            tmp.text = Mathf.RoundToInt(damage).ToString();

            // カラー.
            tmp.color = isIai ? IaiColor : NormalColor;

            // アルファ完全不透明にリセット.
            tmp.alpha = 1f;

            // メッシュ更新を強制.
            tmp.ForceMeshUpdate();
        }

        /// <summary>
        /// DamageCounter.Updateから毎フレーム呼ばれるプログレス更新.
        /// </summary>
        public void UpdateProgress(float progress)
        {
            currentProgress = progress;
        }

        /// <summary>
        /// テキストのBoundsサイズを返す（BoxCollider2Dサイズ設定用）.
        /// Canvas scaleは含まない、ローカルBounds.
        /// </summary>
        public Vector2 GetTextBoundsSize()
        {
            if (tmp == null) return Vector2.one;
            tmp.ForceMeshUpdate();
            var bounds = tmp.textBounds;
            return new Vector2(bounds.size.x, bounds.size.y);
        }

        private void LateUpdate()
        {
            if (tmp == null) return;
            ApplyFade();
        }

        /// <summary>
        /// アルファフェードアウト.
        /// </summary>
        private void ApplyFade()
        {
            if (currentProgress < fadeStartProgress) return;

            float fadeT = Mathf.Clamp01(
                (currentProgress - fadeStartProgress) / (1f - fadeStartProgress)
            );
            float alpha = 1f - fadeT;

            // TMP頂点カラーのアルファを操作.
            var textInfo = tmp.textInfo;
            for (int i = 0; i < textInfo.characterCount; i++)
            {
                if (!textInfo.characterInfo[i].isVisible) continue;

                int matIndex = textInfo.characterInfo[i].materialReferenceIndex;
                int vertIndex = textInfo.characterInfo[i].vertexIndex;
                var colors = textInfo.meshInfo[matIndex].colors32;

                byte a = (byte)(alpha * 255);
                for (int j = 0; j < 4; j++)
                {
                    colors[vertIndex + j].a = a;
                }
            }

            // 頂点カラーをメッシュに適用.
            for (int i = 0; i < textInfo.meshInfo.Length; i++)
            {
                textInfo.meshInfo[i].mesh.colors32 = textInfo.meshInfo[i].colors32;
                tmp.UpdateGeometry(textInfo.meshInfo[i].mesh, i);
            }
        }
    }
}
