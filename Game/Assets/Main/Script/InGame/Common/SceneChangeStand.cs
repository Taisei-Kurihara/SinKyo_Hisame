using Common;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace InGame.Common
{
    /// <summary>
    /// ゲーム終了条件待機シングルトン.
    /// 条件登録時に自動で待機開始、いずれかの条件が達成されたら全て破棄してシーン遷移.
    /// </summary>
    public class SceneChangeStand : SingletonMonoBase<SceneChangeStand>
    {
        private List<IGameEndCondition> _conditions = new List<IGameEndCondition>();
        private CancellationTokenSource _masterCts;
        private ISceneInfo _targetSceneInfo;
        private Action _onConditionMet;
        private bool _isWaiting = false;
        private bool _isConditionMet = false;

        /// <summary>
        /// 条件を登録して即座に待機開始.
        /// </summary>
        /// <param name="condition">ゲーム終了条件.</param>
        /// <param name="targetSceneInfo">条件達成時の遷移先シーン情報.</param>
        /// <param name="onConditionMet">条件達成時のコールバック（省略可）.</param>
        public void RegisterCondition(IGameEndCondition condition, ISceneInfo targetSceneInfo, Action onConditionMet = null)
        {
            // 前回のセッションで条件達成済みの場合はリセット.
            if (_isConditionMet)
            {
                Debug.Log("[SceneChangeStand] 前回セッション完了済み - リセット実行.");
                Reset();
            }

            _conditions.Add(condition);
            _targetSceneInfo = targetSceneInfo;
            _onConditionMet = onConditionMet;

            // マスターCTSがなければ作成.
            if (_masterCts == null)
            {
                _masterCts = new CancellationTokenSource();
            }

            Debug.Log($"[SceneChangeStand] 条件登録. 現在の条件数: {_conditions.Count}");

            // 新しい条件の待機を即座に開始.
            StartConditionWaitAsync(condition, _conditions.Count - 1, _masterCts.Token).Forget();
        }

        /// <summary>
        /// 個別条件の待機を開始.
        /// </summary>
        private async UniTaskVoid StartConditionWaitAsync(IGameEndCondition condition, int index, CancellationToken token)
        {
            try
            {
                bool result = await condition.WaitForConditionAsync(token);

                if (result && !_isConditionMet)
                {
                    _isConditionMet = true;
                    Debug.Log($"[SceneChangeStand] 条件達成. インデックス: {index}");

                    // 他の条件をキャンセル.
                    _masterCts?.Cancel();

                    // 全条件を破棄.
                    DisposeAllConditions();

                    // コールバック実行.
                    _onConditionMet?.Invoke();
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"[SceneChangeStand] 条件キャンセル. インデックス: {index}");
            }
        }

        /// <summary>
        /// 遷移先シーン情報を取得.
        /// </summary>
        public ISceneInfo GetTargetSceneInfo()
        {
            return _targetSceneInfo;
        }

        /// <summary>
        /// 全条件を破棄.
        /// </summary>
        public void DisposeAllConditions()
        {
            foreach (var condition in _conditions)
            {
                condition.Dispose();
            }
            _conditions.Clear();

            Debug.Log("[SceneChangeStand] 全条件破棄完了.");
        }

        /// <summary>
        /// リセット.
        /// </summary>
        public void Reset()
        {
            _masterCts?.Cancel();
            _masterCts?.Dispose();
            _masterCts = null;

            DisposeAllConditions();
            _targetSceneInfo = null;
            _onConditionMet = null;
            _isWaiting = false;
            _isConditionMet = false;
        }
    }
}
