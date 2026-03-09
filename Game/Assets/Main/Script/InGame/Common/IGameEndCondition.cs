using Cysharp.Threading.Tasks;
using System.Threading;

namespace InGame.Common
{
    /// <summary>
    /// ゲーム終了条件インターフェース.
    /// </summary>
    public interface IGameEndCondition
    {
        /// <summary>
        /// 条件が満たされるまで待機.
        /// </summary>
        /// <param name="token">キャンセルトークン.</param>
        /// <returns>条件達成でtrue.</returns>
        UniTask<bool> WaitForConditionAsync(CancellationToken token);

        /// <summary>
        /// 条件の破棄処理.
        /// </summary>
        void Dispose();
    }
}
