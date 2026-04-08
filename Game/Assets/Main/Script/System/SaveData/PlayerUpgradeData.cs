using System.IO;
using UnityEngine;

/// <summary>
/// プレイヤー強化データ.
/// 現在はスタブ実装（フィールドとSave/Load構造のみ、中身は空/デフォルト値）.
/// 将来的にPlayerStatusInitModelへのApplyで強化を反映する.
/// </summary>
[System.Serializable]
public class PlayerUpgradeData
{
    private const string FileName = "player_upgrade.json";

    // --- 強化項目（全てデフォルト=未強化） ---

    /// <summary>攻撃力倍率.</summary>
    public float attackMultiplier = 1f;

    /// <summary>防御力倍率.</summary>
    public float defenseMultiplier = 1f;

    /// <summary>移動速度倍率.</summary>
    public float speedMultiplier = 1f;

    /// <summary>最大回復回数.</summary>
    public int maxHealCount = 3;

    // --- Save / Load ---

    /// <summary>保存先フルパス.</summary>
    private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    /// <summary>
    /// データを保存.
    /// </summary>
    public void Save()
    {
        string json = JsonUtility.ToJson(this, prettyPrint: true);
        File.WriteAllText(FilePath, json);
        Debug.Log($"[PlayerUpgradeData] Save完了 → {FilePath}");
    }

    /// <summary>
    /// データを読み込み.
    /// ファイルが存在しない場合はデフォルト値のデータを返す.
    /// </summary>
    public static PlayerUpgradeData Load()
    {
        if (!File.Exists(FilePath))
        {
            Debug.Log($"[PlayerUpgradeData] セーブファイル未検出 → デフォルト値で新規作成: {FilePath}");
            return new PlayerUpgradeData();
        }

        string json = File.ReadAllText(FilePath);
        var data = JsonUtility.FromJson<PlayerUpgradeData>(json);

        if (data == null)
        {
            Debug.LogWarning("[PlayerUpgradeData] JSONパース失敗 → デフォルト値で新規作成");
            return new PlayerUpgradeData();
        }

        Debug.Log($"[PlayerUpgradeData] Load完了");
        return data;
    }

    // --- 強化適用（スタブ） ---

    /// <summary>
    /// 強化値をPlayerStatusInitModelに適用する.
    /// TODO: 将来実装.
    /// </summary>
    public void Apply()
    {
        // 将来的にPlayerStatusInitModelの値を強化倍率で補正する.
        // 例:
        //   initModel.speed *= speedMultiplier;
        //   initModel.healNum = maxHealCount;
        Debug.Log("[PlayerUpgradeData] Apply（未実装スタブ）");
    }

    /// <summary>
    /// 全強化をリセットしてデフォルトに戻す.
    /// </summary>
    public void Reset()
    {
        attackMultiplier = 1f;
        defenseMultiplier = 1f;
        speedMultiplier = 1f;
        maxHealCount = 3;
        Debug.Log("[PlayerUpgradeData] Reset → デフォルト値");
    }
}
