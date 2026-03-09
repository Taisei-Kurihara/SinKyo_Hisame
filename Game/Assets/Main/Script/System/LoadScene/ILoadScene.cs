using Cysharp.Threading.Tasks;

/// <summary>
/// シーンロード処理のインターフェース定義
/// シーン遷移時のフェード処理を実装するためのインターフェース
/// </summary>
public interface ILoadScene
{
    /// <summary>
    /// フェードイン処理を開始（画面を明るくする）
    /// </summary>
    UniTask StartFadeIn();

    /// <summary>
    /// フェードアウト処理を開始（画面を暗くする）
    /// </summary>
    UniTask StartFadeOut();
}