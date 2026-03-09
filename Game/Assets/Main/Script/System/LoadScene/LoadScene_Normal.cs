using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 通常のシーンローディングクラス
/// フェードイン・フェードアウト効果を使ったシーン遷移を実装する
/// </summary>
public class LoadScene_Normal : MonoBehaviour, LoadScene_interface
{
    [Header("フェード設定")]
    [SerializeField] private CanvasGroup fadeCanvas;
    [SerializeField] private float fadeTime = 1.0f;

    private float elapsedTime = 0f;

    void Awake()
    {
        // CanvasGroupを取得（SceneManagerから動的に追加される場合の対応）
        if (fadeCanvas == null)
        {
            fadeCanvas = GetComponent<CanvasGroup>();
            if (fadeCanvas == null)
            {
                Debug.LogError("CanvasGroupが見つかりません");
            }
        }
    }

    /// <summary>
    /// フェードイン処理を開始する
    /// 画面を徐々に暗くする（アルファ値を0から1へ）
    /// </summary>
    public async UniTask StartFadeIn()
    {
        if (fadeCanvas == null)
        {
            Debug.LogError("フェードキャンバスが設定されていません");
            return;
        }
        else
        {
            Debug.Log("フェードキャンバス確認");
        }

        fadeCanvas.alpha = 0f;
        elapsedTime = 0f;

        while (fadeCanvas.alpha < 1f)
        {
            elapsedTime += Time.deltaTime;
            fadeCanvas.alpha = Mathf.Clamp01(elapsedTime / fadeTime);
            await UniTask.Yield();
        }
    }

    /// <summary>
    /// フェードアウト処理を開始する
    /// 画面を徐々に明るくする（アルファ値を1から0へ）
    /// </summary>
    public async UniTask StartFadeOut()
    {
        if (fadeCanvas == null)
        {
            Debug.LogError("フェードキャンバスが設定されていません");
            return;
        }

        fadeCanvas.alpha = 1f;
        elapsedTime = 0f;

        while (fadeCanvas.alpha > 0f)
        {
            elapsedTime += Time.deltaTime;
            fadeCanvas.alpha = Mathf.Clamp01(1f - (elapsedTime / fadeTime));
            await UniTask.Yield();
        }
    }
}