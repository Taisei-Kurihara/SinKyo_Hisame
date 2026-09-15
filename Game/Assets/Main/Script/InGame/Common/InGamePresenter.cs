using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// インゲーム状態の中央調整 Singleton.
///
/// ■ 役割
///   - PlayerBattleState / EnemyBattleState / PlayerHeartRateState を保持する.
///   - 各状態への遷移時に登録された UnityAction リストを一括実行する.
///   - Player・Enemy・エフェクト系クラスがここに自分のコールバックを登録することで
///     相互参照なしに状態変化を受け取れる.
///
/// ■ 使い方（登録側）
///   void OnEnable()  => InGamePresenter.Instance.RegisterOnEnemyState(EnemyBattleState.StunLong, OnChance);
///   void OnDisable() => InGamePresenter.Instance.UnregisterOnEnemyState(EnemyBattleState.StunLong, OnChance);
///
/// ■ 使い方（通知側）
///   InGamePresenter.Instance.SetEnemyState(EnemyBattleState.StunLong);
/// </summary>
public class InGamePresenter
{
    // ===================== Singleton =====================

    private static InGamePresenter _instance;
    public static InGamePresenter Instance
    {
        get
        {
            if (_instance == null)
                _instance = new InGamePresenter();
            return _instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
    }

    private InGamePresenter()
    {
        // 各辞書を全 enum 値で初期化（GetOrCreate を省略するため）.
        foreach (EnemyBattleState s in System.Enum.GetValues(typeof(EnemyBattleState)))
            _enemyStateActions[s] = new List<UnityAction>();

        foreach (PlayerBattleState s in System.Enum.GetValues(typeof(PlayerBattleState)))
            _playerStateActions[s] = new List<UnityAction>();

        foreach (PlayerHeartRateState s in System.Enum.GetValues(typeof(PlayerHeartRateState)))
            _heartRateStateActions[s] = new List<UnityAction>();
    }

    // ===================== 状態 =====================

    public EnemyBattleState    EnemyState     { get; private set; } = EnemyBattleState.None;
    public PlayerBattleState   PlayerState    { get; private set; } = PlayerBattleState.None;
    public PlayerHeartRateState HeartRateState { get; private set; } = PlayerHeartRateState.Normal;

    // ===================== 敵 軌道中心 Transform =====================

    /// <summary>
    /// 必殺技でプレイヤーが周回する軌道の中心として使う Y 高さ参照 Transform.
    /// EnemyPresenter_abstract.Awake() で登録される.
    /// null の場合は PlayerPresenter が Enemy ルート位置を使用する.
    /// </summary>
    public Transform EnemyOrbitCenter { get; private set; }

    /// <summary>
    /// Transform 位置に加算するワールド空間オフセット（Inspector で微調整).
    /// X ずれ・Y ずれを個別に補正できる.
    /// </summary>
    public Vector2 EnemyOrbitOffset { get; private set; }

    /// <summary>Enemy の Awake() から呼ばれ、必殺技軌道中心を登録する.</summary>
    public void SetEnemyOrbitCenter(Transform t, Vector2 offset = default)
    {
        EnemyOrbitCenter = t;
        EnemyOrbitOffset = offset;
    }

    // ===================== 辞書 =====================

    private readonly Dictionary<EnemyBattleState,    List<UnityAction>> _enemyStateActions    = new();
    private readonly Dictionary<PlayerBattleState,   List<UnityAction>> _playerStateActions   = new();
    private readonly Dictionary<PlayerHeartRateState, List<UnityAction>> _heartRateStateActions = new();

    // ===================== 登録 / 解除 =====================

    public void RegisterOnEnemyState(EnemyBattleState state, UnityAction action)
    {
        if (action == null) return;
        _enemyStateActions[state].Add(action);
    }

    public void UnregisterOnEnemyState(EnemyBattleState state, UnityAction action)
    {
        if (action == null) return;
        _enemyStateActions[state].Remove(action);
    }

    public void RegisterOnPlayerState(PlayerBattleState state, UnityAction action)
    {
        if (action == null) return;
        _playerStateActions[state].Add(action);
    }

    public void UnregisterOnPlayerState(PlayerBattleState state, UnityAction action)
    {
        if (action == null) return;
        _playerStateActions[state].Remove(action);
    }

    public void RegisterOnHeartRateState(PlayerHeartRateState state, UnityAction action)
    {
        if (action == null) return;
        _heartRateStateActions[state].Add(action);
    }

    public void UnregisterOnHeartRateState(PlayerHeartRateState state, UnityAction action)
    {
        if (action == null) return;
        _heartRateStateActions[state].Remove(action);
    }

    // ===================== 通知 =====================

    /// <summary>
    /// 敵の状態を更新し、登録済み UnityAction を全実行する.
    /// 同じ状態への重複 Set でも通知を行う（再入許容）.
    /// </summary>
    public void SetEnemyState(EnemyBattleState state)
    {
        EnemyState = state;
        DispatchActions(_enemyStateActions[state]);
    }

    /// <summary>
    /// プレイヤーの状態を更新し、登録済み UnityAction を全実行する.
    /// </summary>
    public void SetPlayerState(PlayerBattleState state)
    {
        PlayerState = state;
        DispatchActions(_playerStateActions[state]);
    }

    /// <summary>
    /// 心拍数状態を更新し、登録済み UnityAction を全実行する.
    /// 状態に変化がない場合は通知しない.
    /// </summary>
    public void SetHeartRateState(PlayerHeartRateState state)
    {
        if (HeartRateState == state) return;
        HeartRateState = state;
        DispatchActions(_heartRateStateActions[state]);
    }

    // ===================== リセット =====================

    /// <summary>全登録を解除し状態を初期値に戻す（シーン遷移時等）.</summary>
    public void Reset()
    {
        foreach (var list in _enemyStateActions.Values)    list.Clear();
        foreach (var list in _playerStateActions.Values)   list.Clear();
        foreach (var list in _heartRateStateActions.Values) list.Clear();

        EnemyState      = EnemyBattleState.None;
        PlayerState     = PlayerBattleState.None;
        HeartRateState  = PlayerHeartRateState.Normal;
        EnemyOrbitCenter = null;
        EnemyOrbitOffset = Vector2.zero;
    }

    public static void DisposeInstance()
    {
        _instance?.Reset();
        _instance = null;
    }

    // ===================== 内部ヘルパー =====================

    private static void DispatchActions(List<UnityAction> actions)
    {
        // リストをコピーしてから実行（コールバック内で Unregister されても安全）.
        for (int i = actions.Count - 1; i >= 0; i--)
        {
            if (i < actions.Count)
                actions[i]?.Invoke();
        }
    }
}
