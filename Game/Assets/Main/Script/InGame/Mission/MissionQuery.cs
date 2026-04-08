using System.Collections.Generic;
using System.Linq;

/// <summary>
/// ミッション検索ユ���ティリティ.
/// MissionTagのビット演算で複数条件を絞り込む.
/// </summary>
public static class MissionQuery
{
    /// <summary>
    /// AND検索: filterに含まれる全フラグが一致するミッションを返す.
    /// 例: (Wendigo | Hard) → Wendigoかつハード難易度のミッションのみ.
    /// </summary>
    public static List<MissionData> FindMissions(
        List<MissionData> allMissions,
        MissionTag filter)
    {
        return allMissions
            .Where(m => (m.tags & filter) == filter)
            .ToList();
    }

    /// <summary>
    /// OR検索: filterに含まれるいずれかのフラグが一致するミッションを返��.
    /// 例: (Wendigo | Hard) → Wendigoまたはハード難易度のミッション.
    /// </summary>
    public static List<MissionData> FindMissionsAny(
        List<MissionData> allMissions,
        MissionTag filter)
    {
        return allMissions
            .Where(m => (m.tags & filter) != MissionTag.None)
            .ToList();
    }
}
