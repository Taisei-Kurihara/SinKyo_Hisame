using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cysharp.Threading.Tasks;

namespace Audio
{
    /// <summary>
    /// SE再生用クラス.
    /// AudioSourceとstring:AudioClipの辞書を持ち、外部からstring引数で指定されたaudioを再生する.
    /// </summary>
    public class SEPlayer : MonoBehaviour
    {
        private AudioSource audioSource;

        // AudioClip名 : AudioClip の辞書.
        private readonly Dictionary<string, AudioClip> clips = new();

        // Addressableハンドル管理（解放用）.
        private readonly Dictionary<string, AsyncOperationHandle<AudioClip>> handles = new();

        /// <summary>
        /// AudioSourceを取得.
        /// </summary>
        public AudioSource GetAudioSource() => audioSource;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            audioSource.playOnAwake = false;
        }

        /// <summary>
        /// SEPlayerを持つGameObjectを生成して返す.
        /// </summary>
        public static SEPlayer Create(string objectName = "SEPlayer")
        {
            GameObject obj = new GameObject(objectName);
            AudioSource source = obj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            SEPlayer player = obj.AddComponent<SEPlayer>();
            return player;
        }

        /// <summary>
        /// SEClipRegistryからAudioClipをAddressablesで読み込んで辞書登録.
        /// </summary>
        public async UniTask LoadClipsFromRegistry(SEClipRegistry registry, params string[] actionNames)
        {
            List<UniTask> loadTasks = new();

            foreach (var actionName in actionNames)
            {
                string clipName = registry.GetClipName(actionName);
                if (!string.IsNullOrEmpty(clipName) && !clips.ContainsKey(clipName))
                {
                    loadTasks.Add(LoadClipAsync(clipName));
                }
            }

            await UniTask.WhenAll(loadTasks);
        }

        /// <summary>
        /// AddressablesからAudioClipを読み込んで辞書登録.
        /// </summary>
        public async UniTask LoadClipAsync(string clipAddress)
        {
            if (clips.ContainsKey(clipAddress))
            {
                return;
            }

            try
            {
                AsyncOperationHandle<AudioClip> handle = Addressables.LoadAssetAsync<AudioClip>(clipAddress);
                AudioClip clip = await handle;

                if (handle.Status == AsyncOperationStatus.Succeeded && clip != null)
                {
                    clips[clipAddress] = clip;
                    handles[clipAddress] = handle;
                    Debug.Log($"[SEPlayer] AudioClip '{clipAddress}' を読み込みました.");
                }
                else
                {
                    Debug.LogWarning($"[SEPlayer] AudioClip '{clipAddress}' の読み込みに失敗しましたが、処理を継続します.");
                    if (handle.IsValid())
                    {
                        Addressables.Release(handle);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SEPlayer] AudioClip '{clipAddress}' の読み込み中にエラーが発生しましたが、処理を継続します: {e.Message}");
            }
        }

        /// <summary>
        /// 複数のAudioClipを一括読み込み.
        /// </summary>
        public async UniTask LoadClipsAsync(params string[] clipAddresses)
        {
            List<UniTask> loadTasks = new();
            foreach (var address in clipAddresses)
            {
                loadTasks.Add(LoadClipAsync(address));
            }
            await UniTask.WhenAll(loadTasks);
        }

        /// <summary>
        /// 指定されたAudioClipを再生.
        /// </summary>
        public void Play(string clipName)
        {
            if (clips.TryGetValue(clipName, out var clip))
            {
                audioSource.PlayOneShot(clip);
            }
            else
            {
                Debug.LogWarning($"[SEPlayer] AudioClip '{clipName}' が登録されていません.");
            }
        }

        /// <summary>
        /// SEClipRegistryのアクション名でAudioClipを再生.
        /// </summary>
        public void PlayByAction(SEClipRegistry registry, string actionName)
        {
            string clipName = registry.GetClipName(actionName);
            if (!string.IsNullOrEmpty(clipName))
            {
                Play(clipName);
            }
        }

        /// <summary>
        /// 指定されたAudioClipを音量指定で再生.
        /// </summary>
        public void Play(string clipName, float volume)
        {
            if (clips.TryGetValue(clipName, out var clip))
            {
                audioSource.PlayOneShot(clip, volume);
            }
            else
            {
                Debug.LogWarning($"[SEPlayer] AudioClip '{clipName}' が登録されていません.");
            }
        }

        /// <summary>
        /// AudioClipが登録されているか確認.
        /// </summary>
        public bool HasClip(string clipName)
        {
            return clips.ContainsKey(clipName);
        }

        /// <summary>
        /// 全てのAudioClipリソースを解放.
        /// </summary>
        public void ReleaseAll()
        {
            foreach (var kvp in handles)
            {
                if (kvp.Value.IsValid())
                {
                    Addressables.Release(kvp.Value);
                }
            }
            handles.Clear();
            clips.Clear();
        }

        /// <summary>
        /// 指定されたAudioClipリソースを解放.
        /// </summary>
        public void Release(string clipName)
        {
            if (handles.TryGetValue(clipName, out var handle))
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
                handles.Remove(clipName);
            }
            clips.Remove(clipName);
        }

        private void OnDestroy()
        {
            ReleaseAll();
        }
    }
}
