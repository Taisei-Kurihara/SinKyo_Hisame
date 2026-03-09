// 敵の状態を管理するenum.
public enum EnemyState
{
    None,
    Prepare,    // ゲーム開始前の準備段階.
    Appear,     // 登場時の演出.
    Idle,
    Move,
    Attack,
    Damaged,
    Dead
}
