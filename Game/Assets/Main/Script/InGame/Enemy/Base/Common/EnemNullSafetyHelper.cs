using UnityEngine;

// Null安全チェックの共通ヘルパー.
public static class EnemNullSafetyHelper
{
    // enemyModelとPresenterの存在チェック.
    public static bool IsValid(EnemyModel_abstract enemyModel)
    {
        return enemyModel != null && enemyModel.Presenter != null;
    }

    // enemyModelとAnimatorの存在チェック.
    public static bool IsValidWithAnimator(EnemyModel_abstract enemyModel)
    {
        return enemyModel != null && enemyModel.Presenter != null && enemyModel.Animator != null;
    }
}
