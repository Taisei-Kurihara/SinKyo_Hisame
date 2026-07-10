namespace Tutorial
{
    /// <summary>
    /// チュートリアル1ページ分の内容を定義するインターフェース.
    /// </summary>
    public interface ITutorialContent
    {
        /// <summary>ウィンドウタイトル.</summary>
        string Title { get; }

        /// <summary>
        /// 説明文.
        /// TextMeshPro スプライトアイコンを埋め込む場合は
        ///   <sprite name="IconName"> または <sprite index=0>
        /// の形式を使用してください（TMP SpriteAsset 設定が必要）.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// 説明動画の Addressables アドレス.
        /// 空文字の場合は動画なし（RawImage 非表示）.
        /// </summary>
        string VideoAddress { get; }

        /// <summary>操作名（完了率テキスト表示用）. 空=表示なし.</summary>
        string OperationName { get; }

        /// <summary>操作キー（完了率テキスト表示用）. 空=表示なし.</summary>
        string OperationKey { get; }

        /// <summary>補足情報（完了率テキスト表示用）. 空=表示なし.</summary>
        string Supplement { get; }

        /// <summary>
        /// true=説明のみ（入力監視不要）のページ.
        /// </summary>
        bool IsInformational { get; }

        /// <summary>
        /// true=チュートリアル完了率に含める. false=完了率に含めない.
        /// </summary>
        bool CountsForCompletion { get; }

        /// <summary>
        /// プログレスバーテキスト（動的生成）.
        /// 空文字=プログレスバー不要.
        /// </summary>
        string GetProgressBarText();

        /// <summary>
        /// 入力完了チェック（毎フレーム呼出）.
        /// 完了条件を満たしたら true を返す.
        /// IsInformational=true のページでは呼ばれない.
        /// </summary>
        bool CheckCompletion(InputSystem_Actions inputActions);

        /// <summary>監視状態をリセットする.</summary>
        void ResetMonitoring();
    }
}
