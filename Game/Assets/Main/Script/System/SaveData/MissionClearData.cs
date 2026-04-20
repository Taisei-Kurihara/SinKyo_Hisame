using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// ミッション単体のクリア状況エントリ.
/// </summary>
[System.Serializable]
public class MissionClearEntry
{
    /// <summary>ミッション名（MissionData.missionNameと対応）.</summary>
    public string missionName;

    /// <summary>クリア済みか.</summary>
    public bool cleared;

    /// <summary>ミッションタグ（検索用にコピー保持）.</summary>
    public MissionTag tags;
}

/// <summary>
/// 全ミッションのクリア状況データ.
/// JSON形式でpersistentDataPathに保存.
/// </summary>
[System.Serializable]
public class MissionClearData
{
    private const string FileName = "mission_clear.json";

    /// <summary>クリア状況リスト.</summary>
    public List<MissionClearEntry> entries = new List<MissionClearEntry>();

    // --- Save / Load ---

    /// <summary>保存先フルパス.</summary>
    private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    /// <summary>
    /// データを保存.
    /// 保存前にmissionName順にソートする.
    /// </summary>
    public void Save()
    {
        // ソート済みで保存（整合性チェック用）.
        entries.Sort((a, b) => string.Compare(a.missionName, b.missionName, System.StringComparison.Ordinal));

        string json = JsonUtility.ToJson(this, prettyPrint: true);
        File.WriteAllText(FilePath, json);
        Debug.Log($"[MissionClearData] Save完了 → {FilePath} ({entries.Count}件)");
    }

    /// <summary>
    /// データを読み込み.
    /// ファイルが存在しない場合は空データを返す.
    /// 読み込み後にソート順を確認し、不整合があれば再保存する.
    /// </summary>
    public static MissionClearData Load()
    {
        if (!File.Exists(FilePath))
        {
            Debug.Log($"[MissionClearData] セーブファイル未検出 → 新規作成: {FilePath}");
            return new MissionClearData();
        }

        string json = File.ReadAllText(FilePath);
        var data = JsonUtility.FromJson<MissionClearData>(json);

        if (data == null)
        {
            Debug.LogWarning("[MissionClearData] JSONパース失敗 → 新規作成");
            return new MissionClearData();
        }

        // 整合性チェック: ソート順確認.
        if (!IsSorted(data.entries))
        {
            Debug.Log("[MissionClearData] ソート順不整合検出 → 再保存");
            data.Save();
        }

        Debug.Log($"[MissionClearData] Load完了 ({data.entries.Count}件)");
        return data;
    }

    // --- クエリ ---

    /// <summary>
    /// MissionTagでフィルタしたクリア状況を返す（AND検索）.
    /// </summary>
    public List<MissionClearEntry> GetClears(MissionTag filter)
    {
        if (filter == MissionTag.None) return new List<MissionClearEntry>(entries);

        return entries
            .Where(e => (e.tags & filter) == filter)
            .ToList();
    }

    /// <summary>
    /// ミッション名でクリア状況を取得. 未登録ならnull.
    /// </summary>
    public MissionClearEntry GetEntry(string missionName)
    {
        return entries.Find(e => e.missionName == missionName);
    }

    // --- 更新 ---

    /// <summary>
    /// クリア状況を設定（未登録なら追加、既存なら更新）.
    /// </summary>
    public void SetCleared(string missionName, MissionTag tags, bool cleared = true)
    {
        var entry = GetEntry(missionName);
        if (entry != null)
        {
            entry.cleared = cleared;
            entry.tags = tags;
        }
        else
        {
            entries.Add(new MissionClearEntry
            {
                missionName = missionName,
                cleared = cleared,
                tags = tags
            });
        }
    }

    /// <summary>
    /// MissionDataリストとの同期.
    /// 新しいミッションがあれば追加、削除されたミッションがあれば除去し、差分があれば再保存.
    /// </summary>
    public void SyncWithMissionList(List<MissionData> allMissions)
    {
        var missionNames = new HashSet<string>(allMissions.Select(m => m.missionName));
        var existingNames = new HashSet<string>(entries.Select(e => e.missionName));

        bool changed = false;

        // 新規ミッション追加.
        foreach (var mission in allMissions)
        {
            if (!existingNames.Contains(mission.missionName))
            {
                entries.Add(new MissionClearEntry
                {
                    missionName = mission.missionName,
                    cleared = false,
                    tags = mission.tags
                });
                changed = true;
            }
        }

        // 削除されたミッション除去.
        int removedCount = entries.RemoveAll(e => !missionNames.Contains(e.missionName));
        if (removedCount > 0) changed = true;

        if (changed)
        {
            Debug.Log($"[MissionClearData] ミッションリスト同期 → 差分あり、再保存");
            Save();
        }
    }

    // --- 内部ヘルパー ---

    /// <summary>missionName順にソートされているか確認.</summary>
    private static bool IsSorted(List<MissionClearEntry> list)
    {
        for (int i = 1; i < list.Count; i++)
        {
            if (string.Compare(list[i - 1].missionName, list[i].missionName, System.StringComparison.Ordinal) > 0)
            {
                return false;
            }
        }
        return true;
    }
}
