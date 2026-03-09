using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;

/// <summary>
/// シーン変更ボタン用のコンポーネント
/// ボタンクリックでシーンを変更する
/// </summary>
public class SceneChangeButton : MonoBehaviour
{
    [Header("設定")]
    [SerializeField] private GameScene targetScene;
    [SerializeField] private Button button;

    private void Start()
    {
        // ボタンが未設定の場合は自身から取得
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (button != null)
        {
            // ボタンクリックイベントにシーン変更処理を登録
            button.onClick.AddListener(() => OnButtonClick().Forget());
        }
        else
        {
            Debug.LogError("ボタンコンポーネントが見つかりません");
        }
    }

    /// <summary>
    /// ボタンクリック時の処理
    /// </summary>
    private async UniTaskVoid OnButtonClick()
    {
        // ロード中は処理をスキップ
        if (SceneLoader.Instance.IsLoading)
        {
            Debug.Log("既にシーンロード中です");
            return;
        }

        // ボタンを無効化
        if (button != null)
        {
            button.interactable = false;
        }

        // シーンを変更
        await SceneLoader.Instance.ChangeScene(targetScene);

        // ボタンを再度有効化（新しいシーンでは破棄されるため実行されない場合もある）
        if (button != null)
        {
            button.interactable = true;
        }
    }

    private void OnDestroy()
    {
        // イベントリスナーを削除
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
        }
    }
}