using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Tutorial
{
    /// <summary>
    /// チュートリアル表示パネル.
    /// Inspector で各UIコンポーネントを設定する.
    ///
    /// TutorialWindow が Awake 時にこのコンポーネントをクローンして
    /// panelA（原本）/ panelB（自動複製）の2枚でスライドアニメーションを実現する.
    /// </summary>
    public class TutorialWindowPanel : MonoBehaviour
    {
        [Header("テキスト")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;

        [Header("動画")]
        [SerializeField] private RawImage videoRawImage;
        [SerializeField] private VideoPlayer videoPlayer;

        [Header("動画なし時の代替画像")]
        [SerializeField] private Image noAnimImage;

        // ---- ランタイム状態 ----
        private RectTransform _rect;
        private RenderTexture videoRenderTexture;
        private AsyncOperationHandle<VideoClip> videoHandle;
        private bool videoHandleValid = false;

        // ---- 公開 API ----

        /// <summary>パネルのRectTransform（スライドアニメーション用）.</summary>
        public RectTransform PanelRect => _rect != null ? _rect : (_rect = GetComponent<RectTransform>());

        /// <summary>パネルが現在表示中かどうか.</summary>
        public bool IsVisible => gameObject.activeInHierarchy;

        /// <summary>パネルを表示.</summary>
        public void Show() => gameObject.SetActive(true);

        /// <summary>パネルを非表示.</summary>
        public void Hide() => gameObject.SetActive(false);

        /// <summary>
        /// 指定したチュートリアル内容をパネルに反映し、動画を読み込む.
        /// </summary>
        public async UniTask DisplayAsync(ITutorialContent content)
        {
            if (content == null) return;

            if (titleText != null)
                titleText.text = content.Title;
            if (descriptionText != null)
                descriptionText.text = content.Description;

            await LoadVideoAsync(content.VideoAddress);
        }

        // ---- 動画 ----

        private async UniTask LoadVideoAsync(string address)
        {
            ReleaseVideo();

            if (string.IsNullOrEmpty(address))
            {
                if (videoRawImage != null) videoRawImage.gameObject.SetActive(false);
                if (noAnimImage != null)   noAnimImage.gameObject.SetActive(true);
                return;
            }

            // 動画あり: 代替画像を非表示.
            if (noAnimImage != null) noAnimImage.gameObject.SetActive(false);

            try
            {
                videoHandle = Addressables.LoadAssetAsync<VideoClip>(address);
                videoHandleValid = true;
                VideoClip clip = await videoHandle;

                if (clip == null || videoPlayer == null) return;

                if (videoRenderTexture != null)
                    videoRenderTexture.Release();

                videoRenderTexture        = new RenderTexture((int)clip.width, (int)clip.height, 0);
                videoPlayer.clip          = clip;
                videoPlayer.renderMode    = VideoRenderMode.RenderTexture;
                videoPlayer.targetTexture = videoRenderTexture;
                videoPlayer.isLooping     = true;

                if (videoRawImage != null)
                {
                    videoRawImage.texture = videoRenderTexture;
                    videoRawImage.gameObject.SetActive(true);
                }

                videoPlayer.Play();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TutorialWindowPanel] 動画読み込み失敗: {address} / {e.Message}");
                if (videoRawImage != null) videoRawImage.gameObject.SetActive(false);
                if (noAnimImage != null)   noAnimImage.gameObject.SetActive(true);
            }
        }

        /// <summary>動画リソースを解放する.</summary>
        public void ReleaseVideo()
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
