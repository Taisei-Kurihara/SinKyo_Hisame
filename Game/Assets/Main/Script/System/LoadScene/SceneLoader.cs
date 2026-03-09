using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 使用可能なシーンを定義する列挙型
/// </summary>
public enum GameScene
{
    Title,
    MainGame,
    Result
}

/// <summary>
/// シーンローダークラス
/// シーンの切り替えとロード状態の管理を行う
/// </summary>
public class SceneLoader : MonoBehaviour
{
    private static SceneLoader instance;

    [Header("設定")]
    [SerializeField] private GameObject loadScreenPrefab;

    private bool isLoading = false;
    private GameScene currentScene;

    /// <summary>
    /// インスタンスの取得
    /// </summary>
    public static SceneLoader Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<SceneLoader>();
                if (instance == null)
                {
                    GameObject go = new GameObject("SceneLoader");
                    instance = go.AddComponent<SceneLoader>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 現在のシーンを取得
    /// </summary>
    public GameScene CurrentScene => currentScene;

    /// <summary>
    /// ロード中かどうかを取得
    /// </summary>
    public bool IsLoading => isLoading;

    /// <summary>
    /// 指定されたシーンに変更する
    /// </summary>
    /// <param name="newScene">変更先のシーン</param>
    public async UniTask ChangeScene(GameScene newScene)
    {
        // 既にロード中、または同じシーンの場合は処理をスキップ
        if (isLoading || currentScene == newScene)
        {
            Debug.Log($"シーン変更をスキップ: isLoading={isLoading}, currentScene={currentScene}, newScene={newScene}");
            return;
        }

        isLoading = true;
        await LoadSceneAsync(newScene);
        currentScene = newScene;
        isLoading = false;
    }

    /// <summary>
    /// シーンを非同期でロードする
    /// </summary>
    /// <param name="scene">ロードするシーン</param>
    private async UniTask LoadSceneAsync(GameScene scene)
    {
        GameObject loadScreenInstance = null;
        ILoadScene loadScene = null;

        // ロード画面のプレハブが設定されている場合はインスタンス化
        if (loadScreenPrefab != null)
        {
            loadScreenInstance = Instantiate(loadScreenPrefab);
            DontDestroyOnLoad(loadScreenInstance);
            loadScene = loadScreenInstance.GetComponent<ILoadScene>();

            // フェードイン開始
            if (loadScene != null)
            {
                await loadScene.StartFadeIn();
            }
        }

        // シーンの非同期ロード
        Debug.Log($"シーン「{scene}」のロードを開始");
        var asyncOperation = SceneManager.LoadSceneAsync(scene.ToString());

        if (asyncOperation != null)
        {
            // ロードが完了するまで待機
            while (!asyncOperation.isDone)
            {
                float progress = Mathf.Clamp01(asyncOperation.progress / 0.9f);
                Debug.Log($"ロード進行度: {progress * 100:F0}%");
                await UniTask.Yield();
            }
        }
        else
        {
            Debug.LogError($"シーン「{scene}」のロードに失敗しました");
        }

        // フェードアウト開始
        if (loadScene != null)
        {
            await loadScene.StartFadeOut();
        }

        // ロード画面を削除
        if (loadScreenInstance != null)
        {
            Destroy(loadScreenInstance);
        }

        Debug.Log($"シーン「{scene}」のロードが完了");
    }

    /// <summary>
    /// 次のシーンへ遷移
    /// </summary>
    public async UniTask LoadNextScene()
    {
        GameScene nextScene = currentScene switch
        {
            GameScene.Title => GameScene.MainGame,
            GameScene.MainGame => GameScene.Result,
            GameScene.Result => GameScene.Title,
            _ => GameScene.Title
        };

        await ChangeScene(nextScene);
    }
}