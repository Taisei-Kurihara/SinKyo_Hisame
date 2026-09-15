/// <summary>
/// プレイヤーの心拍数状態.
/// 心拍数のみを対象とした独立した状態管理。InGamePresenter が保持し通知に使う.
/// </summary>
public enum PlayerHeartRateState
{
    /// <summary>正常域（心拍数 &lt; 70）.</summary>
    Normal,

    /// <summary>上昇域（70 ≤ 心拍数 &lt; 100）.</summary>
    Elevated,

    /// <summary>危険域（100 ≤ 心拍数）. 居合の攻撃力が急減衰する.</summary>
    Critical,
}
