using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Threading;
using UnityEngine;

namespace InGame.Common
{
    /// <summary>
    /// メインキャラ死亡時条件.
    /// hp <= 0 且つ hp <= 0 になってから2.5sec後に条件達成.
    /// </summary>
    public class DeathCondition : IGameEndCondition
    {
        private readonly ReactiveProperty<int> _playerHp;
        private readonly ReactiveProperty<float> _enemyHp;
        private readonly float _delaySeconds;
        private IDisposable _subscription;
        private bool _isDisposed = false;

        /// <summary>
        /// Playerの死亡条件用コンストラクタ.
        /// </summary>
        /// <param name="playerHp">PlayerのHP ReactiveProperty.</param>
        /// <param name="delaySeconds">HP0後の遅延秒数.</param>
        public DeathCondition(ReactiveProperty<int> playerHp, float delaySeconds = 2.5f)
        {
            _playerHp = playerHp;
            _enemyHp = null;
            _delaySeconds = delaySeconds;
        }

        /// <summary>
        /// Enemyの死亡条件用コンストラクタ.
        /// </summary>
        /// <param name="enemyHp">EnemyのHP ReactiveProperty.</param>
        /// <param name="delaySeconds">HP0後の遅延秒数.</param>
        public DeathCondition(ReactiveProperty<float> enemyHp, float delaySeconds = 2.5f)
        {
            _playerHp = null;
            _enemyHp = enemyHp;
            _delaySeconds = delaySeconds;
        }

        public async UniTask<bool> WaitForConditionAsync(CancellationToken token)
        {
            try
            {
                if (_playerHp != null)
                {
                    // Player HP監視.
                    await _playerHp
                        .Where(hp => hp <= 0)
                        .FirstAsync(token);
                }
                else if (_enemyHp != null)
                {
                    // Enemy HP監視.
                    await _enemyHp
                        .Where(hp => hp <= 0)
                        .FirstAsync(token);
                }

                Debug.Log($"[DeathCondition] HP <= 0 検知. {_delaySeconds}秒後に条件達成.");

                // 遅延待機.
                await UniTask.Delay(TimeSpan.FromSeconds(_delaySeconds), cancellationToken: token);

                Debug.Log("[DeathCondition] 条件達成.");
                return true;
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[DeathCondition] 条件キャンセル.");
                return false;
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _subscription?.Dispose();
            Debug.Log("[DeathCondition] Dispose.");
        }
    }
}
