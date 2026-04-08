using UnityEngine;

/// <summary>
/// Wendigo速度修正パラメータ.
/// waitMultiplier: 攻撃前待機時間の倍率 (小さいほど短縮).
/// speedMultiplier: アニメーション/移動速度倍率 (大きいほど高速).
/// postActionMultiplier: 攻撃後待機の倍率 (小さいほど短縮).
/// </summary>
public struct WendigSpeedModifier
{
    public float waitMultiplier;
    public float speedMultiplier;
    public float postActionMultiplier;

    /// <summary>デフォルト（倍率なし）.</summary>
    public static WendigSpeedModifier Default => new WendigSpeedModifier
    {
        waitMultiplier = 1f,
        speedMultiplier = 1f,
        postActionMultiplier = 1f
    };

    /// <summary>二つのModifierを合成（乗算）.</summary>
    public static WendigSpeedModifier Combine(WendigSpeedModifier a, WendigSpeedModifier b)
    {
        return new WendigSpeedModifier
        {
            waitMultiplier = a.waitMultiplier * b.waitMultiplier,
            speedMultiplier = a.speedMultiplier * b.speedMultiplier,
            postActionMultiplier = a.postActionMultiplier * b.postActionMultiplier
        };
    }

    /// <summary>二つのModifier間を線形補間.</summary>
    public static WendigSpeedModifier Lerp(WendigSpeedModifier a, WendigSpeedModifier b, float t)
    {
        return new WendigSpeedModifier
        {
            waitMultiplier = Mathf.Lerp(a.waitMultiplier, b.waitMultiplier, t),
            speedMultiplier = Mathf.Lerp(a.speedMultiplier, b.speedMultiplier, t),
            postActionMultiplier = Mathf.Lerp(a.postActionMultiplier, b.postActionMultiplier, t)
        };
    }
}

/// <summary>
/// Wendigo速度修正テーブル.
/// 各状態の組み合わせに対応する速度修正を定義.
/// </summary>
public static class WendigSpeedModifierTable
{
    // Normal phase + 通常状態.
    public static readonly WendigSpeedModifier NormalDefault = WendigSpeedModifier.Default;

    // Normal phase + 怒り中.
    public static readonly WendigSpeedModifier NormalAngry = new WendigSpeedModifier
    {
        waitMultiplier = 0.7f,
        speedMultiplier = 1.2f,
        postActionMultiplier = 0.7f
    };

    // Berserk phase + 通常状態.
    public static readonly WendigSpeedModifier BerserkDefault = new WendigSpeedModifier
    {
        waitMultiplier = 0.7f,
        speedMultiplier = 1.2f,
        postActionMultiplier = 0.7f
    };

    // Berserk phase + 怒り中.
    public static readonly WendigSpeedModifier BerserkAngry = new WendigSpeedModifier
    {
        waitMultiplier = 0.6f,
        speedMultiplier = 1.3f,
        postActionMultiplier = 0.6f
    };

    // Berserk phase + 疲労中.
    public static readonly WendigSpeedModifier BerserkFatigue = new WendigSpeedModifier
    {
        waitMultiplier = 1.2f,
        speedMultiplier = 0.8f,
        postActionMultiplier = 1.2f
    };
}
