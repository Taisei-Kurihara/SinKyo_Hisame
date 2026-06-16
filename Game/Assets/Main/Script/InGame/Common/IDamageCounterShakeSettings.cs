namespace InGame
{
    /// <summary>
    /// DamageCounter出現時の振動設定インターフェース.
    /// 心拍数100未満で発動するスケール＋左右振動の設定を定義.
    /// </summary>
    public interface IDamageCounterShakeSettings
    {
        /// <summary>左右振動の振幅.</summary>
        float ShakeAmplitude { get; }

        /// <summary>振動の周波数.</summary>
        int ShakeFrequency { get; }

        /// <summary>振動の持続時間（秒）.</summary>
        float ShakeDuration { get; }

        /// <summary>振動の減衰率（0=減衰なし, 1=完全減衰）.</summary>
        float ShakeDampingRatio { get; }

        /// <summary>スケール 0→1 の時間（秒）.</summary>
        float ScaleUpDuration { get; }
    }
}
