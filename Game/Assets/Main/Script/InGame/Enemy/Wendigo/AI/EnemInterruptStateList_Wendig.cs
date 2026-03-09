using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;

// 割り込みStateリスト管理クラス.
public class EnemInterruptStateList_Wendig
{
    // 割り込みStateリスト.
    private List<EnemInterruptState_abstract> interruptStates = new List<EnemInterruptState_abstract>();

    // Wendig用の割り込みState.
    private EnemInterruptState_Dead_Wendig deadState = new EnemInterruptState_Dead_Wendig();
    private EnemInterruptState_Stan_Wendig stanState = new EnemInterruptState_Stan_Wendig();
    private EnemInterruptState_Bayt_Wendig baytState = new EnemInterruptState_Bayt_Wendig();

    // 現在実行中の割り込みState.
    private EnemInterruptState_abstract currentInterruptState = null;
    public bool IsInterrupting => currentInterruptState != null;

    public EnemInterruptStateList_Wendig()
    {
        // 優先度順に追加.
        interruptStates.Add(deadState);
        interruptStates.Add(stanState);
        interruptStates.Add(baytState);
        Debug.Log($"[EnemInterruptStateList_Wendig] 初期化完了 - State数: {interruptStates.Count}");
    }

    // Dead Stateを取得.
    public EnemInterruptState_Dead_Wendig GetDeadState()
    {
        return deadState;
    }

    // Stan Stateを取得.
    public EnemInterruptState_Stan_Wendig GetStanState()
    {
        return stanState;
    }

    // Bayt Stateを取得.
    public EnemInterruptState_Bayt_Wendig GetBaytState()
    {
        return baytState;
    }

    // 指定されたStateTypeの割り込みStateを取得.
    public EnemInterruptState_abstract GetInterruptState(EnemyState stateType)
    {
        foreach (var state in interruptStates)
        {
            if (state.StateType == stateType)
            {
                return state;
            }
        }
        return null;
    }

    // 割り込みStateを実行.
    public async UniTask ExecuteInterrupt(EnemInterruptState_abstract state, EnemyModel_abstract enemyModel)
    {
        if (state == null) return;

        // 現在の割り込みより優先度が低い場合は実行しない.
        if (currentInterruptState != null && state.Priority <= currentInterruptState.Priority)
        {
            Debug.Log($"[EnemInterruptStateList_Wendig] 割り込み拒否 - 現在の優先度: {currentInterruptState.Priority}, 要求: {state.Priority}");
            return;
        }

        currentInterruptState = state;
        Debug.Log($"[EnemInterruptStateList_Wendig] 割り込み実行 - StateType: {state.StateType}, Priority: {state.Priority}");

        await state.Act(enemyModel);

        currentInterruptState = null;
        Debug.Log($"[EnemInterruptStateList_Wendig] 割り込み完了");
    }
}
