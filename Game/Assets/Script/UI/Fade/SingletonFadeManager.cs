using System;
using System.Collections.Generic;
using System.Threading;
using Common;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// フェード管理シングルトン.
/// Fadeのプール管理・遷移割合・読み込み割合を保持.
/// シーン遷移時のFade命令・待機・完了報告を担当.
/// </summary>
public class SingletonFadeManager : SingletonMonoBase<SingletonFadeManager>
{
    private readonly Dictionary<string, Fade_abstract> _pool = new();
    private readonly Dictionary<Type, string> _typeToKey = new();

    /// <summary> フェード遷移割合（0〜1）. </summary>
    public float FadeProgress { get; set; }

    /// <summary> 外部読み込み割合（0〜1）. </summary>
    public float LoadProgress { get; set; }

    /// <summary> 登録待機タイムアウト（秒）. </summary>
    private const float RegisterTimeout = 5f;

    #region 登録・プール

    /// <summary>
    /// Fadeを登録（プールに追加）.
    /// DontDestroyOnLoad objに親子付けし、無効化する.
    /// </summary>
    public void Register(string key, Fade_abstract fade)
    {
        if (_pool.ContainsKey(key))
        {
            Debug.LogWarning($"Fade既に登録済み: {key}");
            return;
        }

        // DontDestroyOnLoad obj（自身）に親子付け.
        fade.transform.SetParent(transform, false);
        fade.gameObject.SetActive(false);

        _pool[key] = fade;
        _typeToKey[fade.GetType()] = key;
    }

    /// <summary> Fade_abstractのStartから自動登録用. </summary>
    public void Register(Fade_abstract fade)
    {
        Register(fade.gameObject.name, fade);
    }

    /// <summary>
    /// 外部からFadeをプール予約.
    /// 期待する型を指定し、一定時間内に登録されなければエラー.
    /// </summary>
    public async UniTask Pool<T>(CancellationToken ct) where T : Fade_abstract
    {
        var type = typeof(T);

        // 既に登録済みならスキップ.
        if (_typeToKey.ContainsKey(type))
            return;

        // 一定時間内に登録されるのを待機.
        float elapsed = 0f;
        while (!_typeToKey.ContainsKey(type))
        {
            if (elapsed >= RegisterTimeout)
            {
                Debug.LogError($"Fade登録タイムアウト: {type.Name} が {RegisterTimeout}秒以内に登録されませんでした.");
                return;
            }

            await UniTask.Yield(ct);
            elapsed += Time.deltaTime;
        }
    }

    #endregion

    #region 取得・返却

    /// <summary> プールからFadeを取得し有効化. </summary>
    public Fade_abstract Get(string key)
    {
        if (!_pool.TryGetValue(key, out var fade))
        {
            Debug.LogError($"Fade未登録: {key}");
            return null;
        }

        fade.gameObject.SetActive(true);
        return fade;
    }

    /// <summary> 型指定でFadeを取得し有効化. </summary>
    public T Get<T>() where T : Fade_abstract
    {
        if (!_typeToKey.TryGetValue(typeof(T), out var key))
        {
            Debug.LogError($"Fade未登録: {typeof(T).Name}");
            return null;
        }

        return Get(key) as T;
    }

    /// <summary> 型指定でFadeを無効化してプールに戻す. </summary>
    public void Return<T>() where T : Fade_abstract
    {
        if (_typeToKey.TryGetValue(typeof(T), out var key))
        {
            Return(key);
        }
    }

    /// <summary> Fadeを無効化してプールに戻す. </summary>
    public void Return(string key)
    {
        if (_pool.TryGetValue(key, out var fade))
        {
            fade.gameObject.SetActive(false);
        }
    }

    #endregion

    #region シーン遷移用Fade命令

    /// <summary>
    /// シーン遷移用: 指定Fade型でFadeOutし、完了を待つ.
    /// Fadeが未登録の場合は登録されるまで待機（タイムアウト付き）.
    /// </summary>
    public async UniTask FadeOutAsync<T>(float duration = 0.4f, CancellationToken ct = default) where T : Fade_abstract
    {
        var fade = await WaitAndGet<T>(ct);
        if (fade == null) return;

        FadeProgress = 0f;
        var tcs = new UniTaskCompletionSource();

        fade.FadeOut(duration, () =>
        {
            FadeProgress = 1f;
            tcs.TrySetResult();
        });

        await tcs.Task;
    }

    /// <summary>
    /// シーン遷移用: 指定Fade型でFadeInし、完了を待つ.
    /// </summary>
    public async UniTask FadeInAsync<T>(float duration = 0.4f, CancellationToken ct = default) where T : Fade_abstract
    {
        var fade = await WaitAndGet<T>(ct);
        if (fade == null) return;

        FadeProgress = 0f;
        var tcs = new UniTaskCompletionSource();

        fade.FadeIn(duration, () =>
        {
            FadeProgress = 1f;
            Return<T>();
            tcs.TrySetResult();
        });

        await tcs.Task;
    }

    /// <summary>
    /// 指定Fade型が登録されるまで待機し、取得して有効化.
    /// タイムアウト時はnullを返す.
    /// </summary>
    private async UniTask<T> WaitAndGet<T>(CancellationToken ct) where T : Fade_abstract
    {
        await Pool<T>(ct);

        if (!_typeToKey.ContainsKey(typeof(T)))
            return null;

        return Get<T>();
    }

    #endregion

    #region 削除・確認

    /// <summary> プールからFadeを削除・破棄. </summary>
    public void Remove(string key)
    {
        if (!_pool.TryGetValue(key, out var fade))
            return;

        if (fade != null)
        {
            _typeToKey.Remove(fade.GetType());
        }

        _pool.Remove(key);

        if (fade != null && fade.gameObject != null)
        {
            UnityEngine.Object.Destroy(fade.gameObject);
        }
    }

    /// <summary> 型指定でプールから削除・破棄. </summary>
    public void Remove<T>() where T : Fade_abstract
    {
        if (_typeToKey.TryGetValue(typeof(T), out var key))
        {
            Remove(key);
        }
    }

    /// <summary> 登録済みか確認. </summary>
    public bool Contains(string key)
    {
        return _pool.ContainsKey(key);
    }

    /// <summary> 型指定で登録済みか確認. </summary>
    public bool Contains<T>() where T : Fade_abstract
    {
        return _typeToKey.ContainsKey(typeof(T));
    }

    #endregion
}
