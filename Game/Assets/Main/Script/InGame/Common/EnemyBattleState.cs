/// <summary>
/// ゲームプレイ上の敵の状態.
/// 既存の EnemyState (AI内部用) とは別枠。InGamePresenter が保持し通知に使う.
/// </summary>
public enum EnemyBattleState
{
    None,

    Idle,
    Moving,

    /// <summary>パリィ可能な攻撃中.</summary>
    AttackParryable,

    /// <summary>パリィ不可な攻撃中（Rush など）.</summary>
    AttackNonParryable,

    /// <summary>大技発動中（MeteorDrop 落下フェーズ）.</summary>
    BigAttackActive,

    /// <summary>通常スタン（短時間）.</summary>
    StunShort,

    /// <summary>長時間スタン（MeteorDropStan 5sec）= チャンス状態のトリガー.</summary>
    StunLong,

    Dead,
}
