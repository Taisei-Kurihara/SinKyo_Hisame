using Common;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

/// <summary>
/// UI管理用マネージャー
/// Canvasの生成と管理を行う
/// </summary>
public class UIManager : SingletonMonoBase<UIManager>
{
    // Canvas準備完了フラグ
    private bool setUpCanvas = false;

    // Canvasを保持する変数
    private GameObject canvasObject;

    private void Awake()
    {
        AsyncInit().Forget();
    }

    /// <summary>
    /// 非同期初期化処理
    /// </summary>
    private async UniTask AsyncInit()
    {
        try
        {
            // Canvas_Blankプレハブをロードして生成
            var handle = Addressables.InstantiateAsync("Canvas_Blank");
            await handle.Task;
            canvasObject = handle.Result;

            // 生成したCanvasをこのオブジェクトの子として設定
            if (canvasObject != null)
            {
                canvasObject.transform.SetParent(transform);
                DontDestroyOnLoad(canvasObject);
            }
        }
        catch (UnityEngine.AddressableAssets.InvalidKeyException e)
        {
            Debug.LogWarning($"Canvas_Blankプレハブが見つかりません。動的にCanvasを作成します: {e.Message}");

            // フォールバック: Canvasを動的作成
            canvasObject = new GameObject("Canvas_Blank");
            canvasObject.transform.SetParent(transform);

            // Canvasコンポーネントを追加
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            // CanvasScalerを追加
            var canvasScaler = canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasScaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920, 1080);

            // GraphicRaycasterを追加
            canvasObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            DontDestroyOnLoad(canvasObject);
        }

        setUpCanvas = true;
    }

    /// <summary>
    /// 外部からCanvas objectを親子付けできる関数
    /// SetUpCanvasがtrueになるまで待機してから実行
    /// </summary>
    /// <param name="childObject">親子付けするオブジェクト</param>
    public async UniTask AttachToCanvas(GameObject childObject)
    {
        // SetUpCanvasがtrueになるまで待機
        await UniTask.WaitUntil(() => setUpCanvas);

        if (canvasObject != null && childObject != null)
        {
            childObject.transform.SetParent(canvasObject.transform, false);

            // RectTransformがある場合のみ設定
            var rectTransform = childObject.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                // 全画面に配置
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.sizeDelta = Vector2.zero;
                rectTransform.anchoredPosition = Vector2.zero;
            }
        }
    }

    /// <summary>
    /// Canvasオブジェクトを取得
    /// </summary>
    /// <returns>Canvas GameObject</returns>
    public GameObject GetCanvas()
    {
        return canvasObject;
    }

    /// <summary>
    /// Canvasが準備完了しているかを確認
    /// </summary>
    /// <returns>準備完了状態</returns>
    public bool IsCanvasReady()
    {
        return setUpCanvas;
    }
}