// 敵AIのアクション設定クラス.
public class EnemAIActionSetting
{
    // 発動するアクションのstate.
    public EnemState_abstract actionState;

    // 繰り返し発動可能な回数（-1で無限）.
    public int repeatableCount = -1;

    // 現在の残り発動回数.
    private int currentRepeatCount;

    // 発動可能な距離（攻撃が当たる範囲）.
    public float activationDistance = 2f;

    // 移動を開始する可能性のある距離.
    public float moveStartDistance = 5f;

    // 発動可能性（重みづけで計算するための値）.
    public float activationWeight = 1f;

    // 移動開始距離が選択された時用の移動state.
    public EnemState_abstract moveState;

    // 発動するかどうか.
    public bool shouldActivate = true;

    // 初期化.
    public void Initialize()
    {
        currentRepeatCount = repeatableCount;
    }

    // 発動可能かチェック.
    public bool CanActivate()
    {
        if (!shouldActivate) return false;
        if (repeatableCount == -1) return true;
        return currentRepeatCount > 0;
    }

    // 発動回数を消費.
    public void ConsumeRepeat()
    {
        if (repeatableCount != -1 && currentRepeatCount > 0)
        {
            currentRepeatCount--;
        }
    }

    // 発動回数をリセット.
    public void ResetRepeatCount()
    {
        currentRepeatCount = repeatableCount;
    }
}
