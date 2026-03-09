using UnityEngine;
using Common;
using UnityEngine.Audio;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.AddressableAssets;
using Cysharp.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;

namespace Setting
{
    /// <summary>
    /// AudioManager
    /// </summary>
    public class AudioManager : SingletonMonoBase<AudioManager>
    {
        //BGM
        private AudioTokenPackage Bgm;
        //SE
        private AudioTokenPackage SoundEffectOne;
        private AudioTokenPackage SoundEffectTwo;

        //Volume用
        private float VolumeBgm;
        private float VolumeSoundEffect;
        

        AsyncOperationHandle<AudioMixer> handle;
        AudioMixer mixer;
        void Awake()
        {
            Bgm = new AudioTokenPackage(gameObject.AddComponent<AudioSource>());
            Bgm.LoopOnOff(true);
            SoundEffectOne = new AudioTokenPackage(gameObject.AddComponent<AudioSource>());
            SoundEffectTwo = new AudioTokenPackage(gameObject.AddComponent<AudioSource>());

            //LoadAudioMixer().Forget();
        }
        /*
        public async UniTask LoadAudioMixer()
        {
            handle=Addressables.LoadAssetAsync<AudioMixer>("AudioMixer");
            mixer=await handle;

            Bgm.audioSource.outputAudioMixerGroup = mixer.FindMatchingGroups("BGM")[0];
            SoundEffectOne.audioSource.outputAudioMixerGroup = mixer.FindMatchingGroups("Effect")[0];
            SoundEffectTwo.audioSource.outputAudioMixerGroup = mixer.FindMatchingGroups("Effect")[0];
        }*/

        public UniTask LoadBgm(string Address)
        {
            return Bgm.Load(Address);
        }

        /// <summary>
        /// BGM音量を設定(0～100).
        /// </summary>
        public void SetBgmVolume(int volume)
        {
            Bgm.SetAudioSource(volume);
        }

        public void StopBgm()
        {
            Bgm.Stop().Forget();
        }
        public void LoadSoundEffect(string Address)
        {
            SoundEffectOne.Load(Address).Forget();
        }

        private void OnDestroy()
        {
            if (handle.IsValid() && handle.Status == AsyncOperationStatus.Succeeded)
            {
                Addressables.Release(handle);
            }
        }

    }
}


public class AudioTokenPackage
{
    public AudioTokenPackage(AudioSource _audioSource)
    {
        this.audioSource = _audioSource;
    }
    public AudioSource audioSource { get; private set;}

    private CancellationTokenSource token;
    AsyncOperationHandle<AudioClip> handle;

    public void SetAudioSource(int num)
    {
        // 音量を0～100の整数からfloat(0.0～1.0)に変換.
        audioSource.volume = Mathf.Clamp01(num / 100f);
    }

    public void LoopOnOff(bool onoff)
    {
        audioSource.loop = onoff;
    }

    public async UniTask Load(string SoundAddress)
    {
        try
        {
            Debug.Log($"[AudioTokenPackage] Load開始: {SoundAddress}");

            // AudioSourceの状態確認.
            if (audioSource == null)
            {
                Debug.LogError($"[AudioTokenPackage] AudioSourceがnull: {SoundAddress}");
                return;
            }

            // 再生を終了.
            await Stop();

            token?.Cancel();
            token?.Dispose();
            token = new CancellationTokenSource();

            handle = Addressables.LoadAssetAsync<AudioClip>(SoundAddress);
            AudioClip audio = await handle;

            if (handle.Status != AsyncOperationStatus.Succeeded || audio == null)
            {
                Debug.LogError($"[AudioTokenPackage] AudioClipの読み込みに失敗: {SoundAddress}");
                return;
            }

            Debug.Log($"[AudioTokenPackage] AudioClip読み込み完了: {SoundAddress} (loadState: {audio.loadState}, length: {audio.length}s, samples: {audio.samples})");

            // AudioDataがロードされていない場合はロードを要求.
            if (audio.loadState == AudioDataLoadState.Unloaded)
            {
                audio.LoadAudioData();
            }

            // AudioDataの読み込み完了を待機.
            while (audio.loadState == AudioDataLoadState.Loading)
            {
                await UniTask.Yield();
            }

            if (audio.loadState != AudioDataLoadState.Loaded)
            {
                Debug.LogError($"[AudioTokenPackage] AudioDataのロードに失敗: {SoundAddress} (loadState: {audio.loadState})");
                return;
            }

            audioSource.clip = audio;
            audioSource.Play();

            // 再生状態の診断ログ.
            Debug.Log($"[AudioTokenPackage] Play実行: {SoundAddress} | isPlaying: {audioSource.isPlaying}, volume: {audioSource.volume}, mute: {audioSource.mute}, loop: {audioSource.loop}, enabled: {audioSource.enabled}, activeInHierarchy: {audioSource.gameObject.activeInHierarchy}");

            // AudioListenerの存在確認.
            if (UnityEngine.Object.FindObjectOfType<AudioListener>() == null)
            {
                Debug.LogWarning($"[AudioTokenPackage] シーンにAudioListenerが存在しません");
            }
        }
        catch (OperationCanceledException)
        {
            Debug.Log($"[AudioTokenPackage] Loadがキャンセルされました: {SoundAddress}");
        }
        catch (InvalidKeyException e)
        {
            Debug.LogWarning($"[AudioTokenPackage] Addressablesキーが見つかりません: {e.Message}");
        }
        catch (Exception e)
        {
            // 予期しない例外をキャッチして確実にログに出す.
            Debug.LogError($"[AudioTokenPackage] 予期しない例外: {SoundAddress} | {e}");
        }
        finally
        {
            token?.Dispose();
            token = null;
            // 再生中は handle を保持し、Stop() 時に解放する.
        }
    }

    public UniTask Stop()
    {
        audioSource.Stop();
        audioSource.clip = null;
        if (handle.IsValid() && handle.Status == AsyncOperationStatus.Succeeded)
        {
            Addressables.Release(handle);
        }
        return UniTask.CompletedTask;
    }
}