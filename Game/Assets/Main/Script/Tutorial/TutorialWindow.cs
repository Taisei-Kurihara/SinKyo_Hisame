using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using TMPro;
using Cysharp.Threading.Tasks;

namespace Tutorial
{
    /// <summary>
    /// チュートリアルウィンドウ UI.
    /// Inspector で Title / Description / RawImage / VideoPlayer を設定してください.
    ///
    /// [TMP アイコン表示について]
    ///   説明文に <sprite name="IconName"> または <sprite index=0> を埋め込むと
    ///   TextMeshPro の SpriteAsset に登録されたアイコンが表示されます.
    ///   例: "□ / <sprite name=\"ButtonSquare\"> で攻撃"
    ///   ※ TMP SpriteAsset をプロジェクトに作成し、
    ///     descriptionText の Extra Settings > Sprite Asset に割り当ててください.
    /// </summary>
    public class TutorialWindow : MonoBehaviour
    {
        [Header("テキスト")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;

        [Header("動画")]
        [SerializeField] private RawImage videoRawImage;
        [SerializeField] private VideoPlayer videoPlayer;

        [Header("動画なし時の代替画像")]
        [SerializeField] private Image noAnimImage;

        private RenderTexture videoRenderTexture;
        private AsyncOperationHandle<VideoClip> videoHandle;
        private bool videoHandleValid = false;

        // ---- 公開 API ----

        /// <summary>
        /// 指定したチュートリアル内容をウィンドウに反映し、動画を読み込む.
        /// </summary>
        public async UniTask DisplayAsync(ITutorialContent content)
        {
            if (content == null) return;

            // テキスト更新.
            if (titleText != null)
                titleText.text = content.Title;
            if (descriptionText != null)
                descriptionText.text = content.Description;

            // 動画読み込み.
            await LoadVideoAsync(content.VideoAddress);
        }

        /// <summary>
        /// ウィンドウを表示.
        /// </summary>
        public void Show() => gameObject.SetActive(true);

        /// <summary>
        /// ウィンドウを非表示.
        /// </summary>
        public void Hide() => gameObject.SetActive(false);

        // ---- 動画 ----

        private async UniTask LoadVideoAsync(string address)
        {
            // 前の動画を解放.
            ReleaseVideo();

            if (string.IsNullOrEmpty(address))
            {
                if (videoRawImage != null) videoRawImage.gameObject.SetActive(false);
                if (noAnimImage != null) noAnimImage.gameObject.SetActive(true);
                return;
            }

            // 動画あり: 代替画像を非表示.
            if (noAnimImage != null) noAnimImage.gameObject.SetActive(false);

            // Addressables から VideoClip を非同期読み込み.
            try
            {
                videoHandle = Addressables.LoadAssetAsync<VideoClip>(address);
                videoHandleValid = true;
                VideoClip clip = await videoHandle;

                if (clip == null || videoPlayer == null) return;

                // RenderTexture を VideoPlayer のサイズに合わせて生成.
                if (videoRenderTexture != null)
                    videoRenderTexture.Release();

                videoRenderTexture = new RenderTexture((int)clip.width, (int)clip.height, 0);
                videoPlayer.clip = clip;
                videoPlayer.renderMode = VideoRenderMode.RenderTexture;
                videoPlayer.targetTexture = videoRenderTexture;
                videoPlayer.isLooping = true;

                if (videoRawImage != null)
                {
                    videoRawImage.texture = videoRenderTexture;
                    videoRawImage.gameObject.SetActive(true);
                }

                videoPlayer.Play();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TutorialWindow] 動画読み込み失敗: {address} / {e.Message}");
                if (videoRawImage != null) videoRawImage.gameObject.SetActive(false);
                if (noAnimImage != null) noAnimImage.gameObject.SetActive(true);
            }
        }

        private void ReleaseVideo()
        {
            if (videoPlayer != null)
            {
                videoPlayer.Stop();
                videoPlayer.clip = null;
            }

            if (videoHandleValid && videoHandle.IsValid())
            {
                Addressables.Release(videoHandle);
                videoHandleValid = false;
            }

            if (videoRenderTexture != null)
            {
                videoRenderTexture.Release();
                videoRenderTexture = null;
            }
        }

        private void OnDestroy()
        {
            ReleaseVideo();
        }
    }
}
