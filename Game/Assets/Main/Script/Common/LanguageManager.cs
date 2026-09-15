using System;
using UnityEngine;

/// <summary>
/// ゲーム内表示言語.
/// </summary>
public enum GameLanguage
{
    Japanese = 0,
    English  = 1,
}

namespace Common
{
    /// <summary>
    /// 言語切り替えシングルトン.
    /// 言語変更時に OnLanguageChanged イベントを発火し、
    /// 購読者（UI・チュートリアルコンテンツ等）が自動的に表示を更新できる.
    /// DontDestroyOnLoad でシーン跨ぎ維持.
    /// </summary>
    public class LanguageManager : SingletonMonoBase<LanguageManager>
    {
        // Instance(false) で生成された場合も含め、常にシーン跨ぎ永続化を保証.
        // SingletonMonoBase.Instance() は dontDestroy=false だと DontDestroyOnLoad を
        // 呼ばないため、Awake で自前保証する.
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private GameLanguage currentLanguage = GameLanguage.Japanese;

        /// <summary>現在の言語設定.</summary>
        public GameLanguage CurrentLanguage => currentLanguage;

        /// <summary>
        /// 文字列配列のインデックス (0=JP, 1=EN).
        /// 使用例: strings[(int)LanguageManager.Instance().CurrentLanguage]
        /// </summary>
        public int Index => (int)currentLanguage;

        /// <summary>言語変更時に発火するイベント.</summary>
        public event Action<GameLanguage> OnLanguageChanged;

        /// <summary>言語を設定する. 同じ言語ならイベントは発火しない.</summary>
        public void SetLanguage(GameLanguage lang)
        {
            if (currentLanguage == lang) return;
            currentLanguage = lang;
            OnLanguageChanged?.Invoke(currentLanguage);
        }

        /// <summary>日英を切り替える.</summary>
        public void ToggleLanguage()
        {
            SetLanguage(currentLanguage == GameLanguage.Japanese
                ? GameLanguage.English
                : GameLanguage.Japanese);
        }
    }
}
