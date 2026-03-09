using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cysharp.Threading.Tasks;

namespace Audio
{
    /// <summary>
    /// SE用のアクション名とAudioClip名の辞書を保持するクラス.
    /// PlayerPresenterやEnemyPresenterで使用する.
    /// </summary>
    public class SEClipRegistry
    {
        // アクション名 : AudioClip名 の辞書.
        private readonly Dictionary<string, string> actionToClipName = new();

        /// <summary>
        /// アクション名とAudioClip名を登録.
        /// </summary>
        public void Register(string actionName, string clipName)
        {
            actionToClipName[actionName] = clipName;
        }

        /// <summary>
        /// 複数のアクションを一括登録.
        /// </summary>
        public void RegisterRange(Dictionary<string, string> mappings)
        {
            foreach (var kvp in mappings)
            {
                actionToClipName[kvp.Key] = kvp.Value;
            }
        }

        /// <summary>
        /// アクション名からAudioClip名を取得.
        /// </summary>
        public string GetClipName(string actionName)
        {
            if (actionToClipName.TryGetValue(actionName, out var clipName))
            {
                return clipName;
            }
            Debug.LogWarning($"[SEClipRegistry] アクション '{actionName}' のAudioClipが登録されていません.");
            return null;
        }

        /// <summary>
        /// アクション名が登録されているか確認.
        /// </summary>
        public bool HasAction(string actionName)
        {
            return actionToClipName.ContainsKey(actionName);
        }

        /// <summary>
        /// 登録を解除.
        /// </summary>
        public void Unregister(string actionName)
        {
            actionToClipName.Remove(actionName);
        }

        /// <summary>
        /// 全ての登録を解除.
        /// </summary>
        public void Clear()
        {
            actionToClipName.Clear();
        }
    }
}
