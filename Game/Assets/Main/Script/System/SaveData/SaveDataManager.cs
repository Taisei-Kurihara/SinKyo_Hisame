using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// セーブデータ一元管理クラス（純C#シングルトン）.
/// TitleSceneInfo.Init()から呼び出され、起動時に全データをロードする.
/// </summary>
public class SaveDataManager
{
    // --- シングルトン ---
    private static SaveDataManager _instance;
    public static SaveDataManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new SaveDataManager();
            }
            return _instance;
        }
    }

    private bool isInitialized = false;

    // --- データ ---

    /// <summary>プレイヤー強化データ.</summary>
    public PlayerUpgradeData PlayerUpgrade { get; private set; }

    /// <summary>ミッションクリア状況データ.</summary>
    public MissionClearData MissionClears { get; private set; }

    // --- 初期化 ---

    /// <summary>
    /// 全データをロードして初期化.
    /// 起動時に1回だけ呼ぶ（TitleSceneInfo.Init等）.
    /// 2回目以降の呼び出しはスキップ.
    /// </summary>
    public void Initialize()
    {
        if (isInitialized)
        {
            Debug.Log("[SaveDataManager] 初期化済み → スキップ");
            return;
        }

        Debug.Log("[SaveDataManager] Initialize開始");
        LoadAll();
        isInitialized = true;
        Debug.Log("[SaveDataManager] Initialize完了");
    }

    /// <summary>
    /// 全データをロード.
    /// </summary>
    public void LoadAll()
    {
        PlayerUpgrade = PlayerUpgradeData.Load();
        MissionClears = MissionClearData.Load();
        Debug.Log("[SaveDataManager] LoadAll完了");
    }

    /// <summary>
    /// 全データを保存.
    /// </summary>
    public void SaveAll()
    {
        PlayerUpgrade?.Save();
        MissionClears?.Save();
        Debug.Log("[SaveDataManager] SaveAll完了");
    }

    // --- クエリ ---

    /// <summary>
    /// MissionTagでフィルタしたクリア状況を返す.
    /// </summary>
    public List<MissionClearEntry> GetMissionClears(MissionTag filter)
    {
        if (MissionClears == null)
        {
            Debug.LogWarning("[SaveDataManager] MissionClears未初期化 → 空リスト返却");
            return new List<MissionClearEntry>();
        }
        return MissionClears.GetClears(filter);
    }
}
