using System;
using Cysharp.Threading.Tasks;
using LitMotion;
using UnityEngine;
#if UNITY_EDITOR
using NaughtyAttributes;
#endif

/// <summary>
/// フェード基底クラス.
/// LitMotionを使用したFadeIn/Out. 継承先でフェード対象を決定する.
/// LoadScene_interface を実装し SceneManager から UniTask として呼び出せる.
/// </summary>
public abstract class Fade_abstract : MonoBehaviour, LoadScene_interface
{
#if UNITY_EDITOR
    [SerializeField]
    private float _debugDuration = 0.4f;
#endif

    private MotionHandle _currentHandle;

    protected virtual void Start()
    {
        // FadeManagerに自身を登録.
        SingletonFadeManager.Instance().Register(this);
    }

    /// <summary> フェードイン（透明→不透明）. </summary>
    public void FadeIn(float duration = 0.4f, Action onComplete = null)
    {
        Cancel();
        _currentHandle = CreateFadeMotion(0f, 1f, duration, onComplete);
    }

    /// <summary> フェードアウト（不透明→透明）. </summary>
    public void FadeOut(float duration = 0.4f, Action onComplete = null)
    {
        Cancel();
        _currentHandle = CreateFadeMotion(1f, 0f, duration, onComplete);
    }

    /// <summary> LoadScene_interface: フェードイン（透明→不透明）を UniTask で待機. </summary>
    public UniTask StartFadeIn()
    {
        var tcs = new UniTaskCompletionSource();
        FadeIn(0.4f, () => tcs.TrySetResult());
        return tcs.Task;
    }

    /// <summary> LoadScene_interface: フェードアウト（不透明→透明）を UniTask で待機. </summary>
    public UniTask StartFadeOut()
    {
        var tcs = new UniTaskCompletionSource();
        FadeOut(0.4f, () => tcs.TrySetResult());
        return tcs.Task;
    }

#if UNITY_EDITOR
    [Button("FadeIn")]
    private void DebugFadeIn() => FadeIn(_debugDuration);

    [Button("FadeOut")]
    private void DebugFadeOut() => FadeOut(_debugDuration);
#endif

    /// <summary> フェードモーション生成. 継承先で実装. </summary>
    protected abstract MotionHandle CreateFadeMotion(float from, float to, float duration, Action onComplete);

    /// <summary> イージング種別. 継承先でオーバーライド可能. </summary>
    protected virtual Ease GetEase()
    {
        return Ease.Linear;
    }

    /// <summary> 実行中のモーションをキャンセル. </summary>
    protected void Cancel()
    {
        if (_currentHandle.IsActive())
        {
            _currentHandle.Cancel();
        }
    }

    protected virtual void OnDestroy()
    {
        Cancel();
    }
}
