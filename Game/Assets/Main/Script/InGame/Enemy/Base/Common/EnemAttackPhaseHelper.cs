using UnityEngine;
using Cysharp.Threading.Tasks;

// 攻撃フェーズの共通ヘルパークラス.
// 前段階・攻撃中・攻撃後の3フェーズを統一的に管理する.
public static class EnemAttackPhaseHelper
{
    /// <summary>
    /// 攻撃予兆を再生する共通関数.
    /// totalWaitTimeMs全体を待機し、内部で攻撃の何秒前に予兆を表示するかを管理する.
    /// </summary>
    /// <param name="enemyModel">敵モデル.</param>
    /// <param name="totalWaitTimeMs">攻撃前の総待機時間（ミリ秒）.</param>
    /// <param name="isParryable">パリィ可能な攻撃ならtrue.</param>
    /// <param name="premonitionLeadTimeMs">予兆表示から攻撃までの時間（ミリ秒）.</param>
    /// <param name="animSpeed">アニメーション速度倍率.</param>
    /// <returns>途中でnull化した場合false、正常完了true.</returns>
    public static async UniTask<bool> PlayAttackPremonition(
        EnemyModel_abstract enemyModel,
        float totalWaitTimeMs,
        bool isParryable,
        float premonitionLeadTimeMs,
        float animSpeed = 1f)
    {
        // 予兆表示前の待機時間を計算.
        float preWaitMs = totalWaitTimeMs - premonitionLeadTimeMs;
        if (preWaitMs > 0)
        {
            await UniTask.Delay((int)(preWaitMs / animSpeed));
            if (!EnemNullSafetyHelper.IsValid(enemyModel)) return false;
        }

        // 攻撃通告を再生.
        enemyModel.Presenter.PlayAttackWarning(isParryable);

        // 予兆表示後の残り待機.
        if (premonitionLeadTimeMs > 0)
        {
            await UniTask.Delay((int)(premonitionLeadTimeMs / animSpeed));
            if (!EnemNullSafetyHelper.IsValid(enemyModel)) return false;
        }

        return true;
    }

    /// <summary>
    /// 攻撃終了後のフレーム待機を行う共通関数.
    /// </summary>
    /// <param name="enemyModel">敵モデル.</param>
    /// <param name="waitFrames">待機フレーム数.</param>
    /// <param name="animSpeed">アニメーション速度倍率.</param>
    /// <returns>途中でnull化した場合false.</returns>
    public static async UniTask<bool> WaitPostAttackFrames(
        EnemyModel_abstract enemyModel,
        int waitFrames,
        float animSpeed = 1f)
    {
        int effectiveFrames = (int)(waitFrames / animSpeed);
        for (int i = 0; i < effectiveFrames; i++)
        {
            if (!EnemNullSafetyHelper.IsValid(enemyModel)) return false;
            await UniTask.Yield();
        }
        return true;
    }

    /// <summary>
    /// アニメーション速度考慮済みの遅延.
    /// </summary>
    /// <param name="enemyModel">敵モデル.</param>
    /// <param name="delayMs">遅延時間（ミリ秒）.</param>
    /// <param name="animSpeed">アニメーション速度倍率.</param>
    /// <returns>途中でnull化した場合false.</returns>
    public static async UniTask<bool> DelayWithAnimSpeed(
        EnemyModel_abstract enemyModel,
        float delayMs,
        float animSpeed)
    {
        await UniTask.Delay((int)(delayMs / animSpeed));
        return EnemNullSafetyHelper.IsValid(enemyModel);
    }
}
