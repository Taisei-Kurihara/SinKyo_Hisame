/// <summary>
/// WendigoのHPフェーズ定義.
/// 各フェーズはHP量とHPバー表示割合を持つ.
/// </summary>
[System.Serializable]
public class WendigHpPhase
{
    /// <summary>このフェーズのHP量.</summary>
    public float hp;

    /// <summary>HPバー上の表示割合（0-1）. 全フェーズの合計が1.0になること.</summary>
    public float displayRatio;

    /// <summary>フェーズ名（デバッグ用）.</summary>
    public string phaseName;
}
