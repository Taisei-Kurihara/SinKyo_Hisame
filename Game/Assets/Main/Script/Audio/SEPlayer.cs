using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
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

        // AudioClip名 : AudioClip.
        private readonly Dictionary<string, AudioClip> clips = new();

        // Addressableハンドル管理（解放用）.
        private readonly Dictionary<string, AsyncOperationHandle<AudioClip>> handles = new();

        /// <summary> AudioSourceを取得.  </summary>
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

        /// <summary> SEPlayerを持つGameObjectを生成して返す. </summary>
        public static SEPlayer Create(string objectName = "SEPlayer")
        {
            GameObject obj = new GameObject(objectName);
            AudioSource source = obj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            SEPlayer player = obj.AddComponent<SEPlayer>();
            return player;
        }

        /// <summary>  SEClipRegistryからAudioClipをAddressablesで読み込んで辞書登録. </summary>
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

        /// <summary> AddressablesからAudioClipを読み込んで辞書登録. </summary>
        public async UniTask LoadClipAsync(string clipAddress)
        {
            if (clips.ContainsKey(clipAddress))
            {
                return;
            }

            try
            {
                // Addressablesにキーが登録されているか事前確認.
                AsyncOperationHandle<IList<IResourceLocation>> locHandle =
                    Addressables.LoadResourceLocationsAsync(clipAddress, typeof(AudioClip));
                IList<IResourceLocation> locations = await locHandle;
                bool exists = locations != null && locations.Count > 0;
                Addressables.Release(locHandle);

                if (!exists)
                {
                    return;
                }

                AsyncOperationHandle<AudioClip> handle = Addressables.LoadAssetAsync<AudioClip>(clipAddress);
                AudioClip clip = await handle;

                if (handle.Status == AsyncOperationStatus.Succeeded && clip != null)
                {
                    clips[clipAddress] = clip;
                    handles[clipAddress] = handle;
                }
                else
                {
                    if (handle.IsValid())
                    {
                        Addressables.Release(handle);
                    }
                }
            }
            catch (Exception)
            {
                // Addressablesキー未登録等のエラーはサイレントスキップ.
            }
        }

        /// <summary> 複数のAudioClipを一括読み込み. </summary>
        public async UniTask LoadClipsAsync(params string[] clipAddresses)
        {
            List<UniTask> loadTasks = new();
            foreach (var address in clipAddresses)
            {
                loadTasks.Add(LoadClipAsync(address));
            }
            await UniTask.WhenAll(loadTasks);
        }

        /// <summary> AudioClipが登録されているか確認. </summary>
        public bool HasClip(string clipName) => clips.ContainsKey(clipName);

        #region 再生処理

        /// <summary> 指定されたAudioClipを再生. </summary>
        public void Play(string clipName, float volume = 1)
        {
            if (clips.TryGetValue(clipName, out var clip))
            {
                audioSource.PlayOneShot(clip, volume);
            }
            // 未登録のClipはサイレントスキップ.
        }

        /// <summary> SEClipRegistryのアクション名でAudioClipを再生. </summary>
        public void PlayByAction(SEClipRegistry registry, string actionName)
        {
            string clipName = registry.GetClipName(actionName);
            if (!string.IsNullOrEmpty(clipName))
            {
                Play(clipName);
            }
        }

        #endregion



        #region リリース処理
        /// <summary> 全てのAudioClipリソースを解放. </summary>
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

        /// <summary> 指定されたAudioClipリソースを解放. </summary>
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
        #endregion
    }
}
