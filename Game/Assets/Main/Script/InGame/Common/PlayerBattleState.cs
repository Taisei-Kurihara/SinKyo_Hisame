/// <summary>
/// ゲームプレイ上のプレイヤーの状態.
/// InGamePresenter が保持し通知に使う.
/// </summary>
public enum PlayerBattleState
{
    None,

    Idle,
    Moving,
    Attacking,

    /// <summary>居合攻撃中.</summary>
    Iai,

    /// <summary>必殺技（チャンス攻撃）発動中.</summary>
    ChanceAttack,

    Dodging,
    Stunned,
    Dead,
}
