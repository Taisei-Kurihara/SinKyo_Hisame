using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using InGame.Player;

public abstract class EnemAIModel_abstract
{

    // stageの端について取得方法を考える必要あり.
    // 自身の大きさを取得する方法 => 主コライダーの大きさで取るようにする.
    private CancellationTokenSource cts;
    private bool isRunning = false;

    // アクション設定リスト.
    protected List<EnemAIActionSetting> actionSettings = new List<EnemAIActionSetting>();

    // 自身のTransform（位置取得用）.
    protected Transform ownerTransform;

    // 自身のEnemyModel（Act呼び出し用）.
    protected EnemyModel_abstract ownerModel;

    // ターゲットオブジェクト（プレイヤー）.
    protected GameObject targetObject;

    // ターゲット位置を取得するプロパティ.
    public Vector3 TargetPosition
    {
        get
        {
            // PlayerScopeからプレイヤーを検索.
            var playerScope = Object.FindFirstObjectByType<PlayerScope>();
            if (playerScope != null)
            {
                targetObject = playerScope.gameObject;
                return targetObject.transform.position;
            }

            // プレイヤーが存在しない場合.
            targetObject = null;

#if UNITY_EDITOR
            // エディタ上ではマウスポインタの位置を追跡.
            Vector3 mouseScreenPos = Input.mousePosition;
            mouseScreenPos.z = 10f; // カメラからの距離.
            if (Camera.main != null)
            {
                return Camera.main.ScreenToWorldPoint(mouseScreenPos);
            }
#endif
            // exe（ビルド）では自身の位置を返す.
            if (ownerTransform != null)
            {
                return ownerTransform.position;
            }

            return Vector3.zero;
        }
    }

    // 自身のTransformを設定.
    public void SetOwnerTransform(Transform transform)
    {
        Debug.Log($"[EnemAIModel_abstract] SetOwnerTransform - Transform: {(transform != null ? transform.name : "null")}");
        ownerTransform = transform;
    }

    // 自身のEnemyModelを設定.
    public void SetOwnerModel(EnemyModel_abstract model)
    {
        Debug.Log($"[EnemAIModel_abstract] SetOwnerModel - Model: {(model != null ? model.gameObject.name : "null")}");
        ownerModel = model;
    }

    // ループを開始.
    public void StartLoop()
    {
        Debug.Log($"[EnemAIModel_abstract] StartLoop - isRunning: {isRunning}");
        if (isRunning) return;
        cts = new CancellationTokenSource();
        isRunning = true;
        Debug.Log($"[EnemAIModel_abstract] AILoop開始");
        AILoop(cts.Token).Forget();
    }

    // ループを停止.
    public void StopLoop()
    {
        Debug.Log($"[EnemAIModel_abstract] StopLoop - isRunning: {isRunning}");
        if (!isRunning) return;
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
        isRunning = false;
        Debug.Log($"[EnemAIModel_abstract] AILoop停止完了");
    }

    public virtual async UniTask AILoop(CancellationToken token)
    {
        Debug.Log($"[EnemAIModel_abstract] AILoop開始 - ownerModel: {(ownerModel != null ? ownerModel.gameObject.name : "null")}");

        // ownerModelとPresenterが設定されるまで待機（最大60フレーム）.
        int waitCount = 0;
        const int maxWaitFrames = 60;
        while ((ownerModel == null || ownerModel.Presenter == null) && waitCount < maxWaitFrames)
        {
            if (token.IsCancellationRequested)
            {
                Debug.Log($"[EnemAIModel_abstract] AILoop - 待機中にキャンセル要求");
                return;
            }
            Debug.Log($"[EnemAIModel_abstract] AILoop - ownerModel/Presenter待機中 ({waitCount}/{maxWaitFrames})");
            await UniTask.Yield(token);
            waitCount++;
        }

        // 待機後もnullの場合は開始しない.
        if (ownerModel == null || ownerModel.Presenter == null)
        {
            Debug.LogError($"[EnemAIModel_abstract] AILoop開始失敗 - ownerModel or Presenter が {maxWaitFrames}フレーム待機後もnull");
            StopLoop();
            return;
        }

        Debug.Log($"[EnemAIModel_abstract] AILoop - 初期化完了、ループ開始");
        int loopCount = 0;
        while (!token.IsCancellationRequested)
        {
            // ownerModelまたはPresenterが破棄されている場合はループを終了.
            if (ownerModel == null || ownerModel.Presenter == null)
            {
                Debug.LogWarning($"[EnemAIModel_abstract] AILoop終了 - ownerModel or Presenter が null (loopCount: {loopCount})");
                StopLoop();
                return;
            }

            try
            {
                await OnAIUpdate(token);
                await UniTask.Yield(token);
                loopCount++;
                if (loopCount % 60 == 0) // 60フレームごとにログ
                {
                    Debug.Log($"[EnemAIModel_abstract] AILoop実行中 - loopCount: {loopCount}");
                }
            }
            catch (System.Exception e) when (e is not System.OperationCanceledException)
            {
                Debug.LogError($"[EnemAIModel_abstract] AILoop例外発生: {e.Message}");
                // MissingReferenceExceptionなどの場合はループを終了.
                if (ownerModel == null || ownerModel.Presenter == null)
                {
                    Debug.LogWarning($"[EnemAIModel_abstract] AILoop終了 - 例外後のnullチェック");
                    StopLoop();
                    return;
                }
                throw;
            }
        }
        Debug.Log($"[EnemAIModel_abstract] AILoop終了 - キャンセル要求 (loopCount: {loopCount})");
    }

    // 継承クラスでAI処理を実装.
    protected virtual async UniTask OnAIUpdate(CancellationToken token)
    {
        await UniTask.CompletedTask;
    }

    // アクション設定を追加.
    protected void AddActionSetting(EnemAIActionSetting setting)
    {
        setting.Initialize();
        actionSettings.Add(setting);
    }

    // 距離に基づいてアクションを選択（重みづけ）.
    protected EnemAIActionSetting SelectActionByDistance(float distance)
    {
        // 発動距離内のアクションをフィルタリング.
        var activatableActions = new List<EnemAIActionSetting>();
        float totalWeight = 0f;

        foreach (var setting in actionSettings)
        {
            if (setting.CanActivate() && distance <= setting.activationDistance)
            {
                activatableActions.Add(setting);
                totalWeight += setting.activationWeight;
            }
        }

        if (activatableActions.Count == 0) return null;

        // 重みづけでランダム選択.
        float randomValue = Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        foreach (var setting in activatableActions)
        {
            currentWeight += setting.activationWeight;
            if (randomValue <= currentWeight)
            {
                return setting;
            }
        }

        return activatableActions[activatableActions.Count - 1];
    }

    // 移動開始距離内のアクションを選択.
    protected EnemAIActionSetting SelectMoveActionByDistance(float distance)
    {
        // 移動開始距離内のアクションをフィルタリング.
        var moveActions = new List<EnemAIActionSetting>();
        float totalWeight = 0f;

        foreach (var setting in actionSettings)
        {
            if (setting.CanActivate() &&
                distance > setting.activationDistance &&
                distance <= setting.moveStartDistance &&
                setting.moveState != null)
            {
                moveActions.Add(setting);
                totalWeight += setting.activationWeight;
            }
        }

        if (moveActions.Count == 0) return null;

        // 重みづけでランダム選択.
        float randomValue = Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        foreach (var setting in moveActions)
        {
            currentWeight += setting.activationWeight;
            if (randomValue <= currentWeight)
            {
                return setting;
            }
        }

        return moveActions[moveActions.Count - 1];
    }
}
