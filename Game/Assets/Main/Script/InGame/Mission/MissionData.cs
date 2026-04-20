using UnityEngine;

/// <summary>
/// ミッション定義データ（ScriptableObject）.
/// Unityエディタ上で Create > DF > MissionData で作成可能.
/// </summary>
[CreateAssetMenu(menuName = "DF/MissionData")]
public class MissionData : ScriptableObject
{
    /// <summary>ミッション名（セーブデータのキーとしても使用）.</summary>
    public string missionName;

    /// <summary>ミッションタグ（難易度 × 条件 × Enemy名）.</summary>
    public MissionTag tags;

    /// <summary>ミッション説明文.</summary>
    [TextArea(2, 5)]
    public string description;

    /// <summary>クリア報酬値.</summary>
    public int reward;
}
