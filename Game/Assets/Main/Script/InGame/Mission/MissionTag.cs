/// <summary>
/// ミッションタグ（フラグ化enum）.
/// ビット演算で複数条件を同時管理できる.
/// 難易度 × 条件 × Enemy名 をまとめて1つで扱える.
///
/// ビット割り当て:
///   0-4:  難易度 (Easy=0, Normal=1, Hard=2)
///   5-10: 条件   (BossNormal=5, Endless=7)
///   11~:  Enemy名 (Wendigo=11)
/// </summary>
[System.Flags]
public enum MissionTag
{
    None = 0,

    // --- 難易度 (bit 0-4) ---
    Easy   = 1 << 0,
    Normal = 1 << 1,
    Hard   = 1 << 2,

    // --- 条件 (bit 5-10) ---
    /// <summary>Boss撃破で終了モード.</summary>
    BossNormal = 1 << 5,
    /// <summary>プレイ���ーが死ぬまで敵が再出現し続けるモード.</summary>
    Endless    = 1 << 7,

    // --- Enemy名 (bit 11~) ---
    Wendigo = 1 << 11,
}
