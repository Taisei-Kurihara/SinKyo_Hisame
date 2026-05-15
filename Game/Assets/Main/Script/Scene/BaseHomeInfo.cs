using UnityEngine;
using Common;
using Cysharp.Threading.Tasks;
using InGame;
using InGame.Player;
using Setting;

namespace SceneInfo
{
    /// <summary>
    /// Homeシーン（ベース）
    /// </summary>
    public class BaseHomeInfo : ISceneInfo
    {
        string ISceneInfo.SceneName => "BaseHome";

        UniTask ISceneInfo.End() => UniTask.CompletedTask;

        async UniTask ISceneInfo.Init()
        {
            PlayerManager playerManager = PlayerManager.Instance();
            playerManager.pulseModel.ResetToBase();
            await playerManager.InstantiateCharacter("PlayerCharacter");

            // ヒットエフェクトプール初期化.
            await HitEffectPool.Instance(false).InitPool("HitEffect");

            // プレイヤーエフェクトプール初期化（スタン:1個、回復:3個）.
            var playerEffectPool = PlayerEffectPool.Instance(false);
            await playerEffectPool.InitPool("PlayerEffect_Stun", 1);
            await playerEffectPool.InitPool("PlayerEffect_Heal", 3);

            // BGM再生（ビルド版ではawaitしないとAddressables読み込み完了前にフローが終了する）.
            await AudioManager.Instance().LoadBgm("BGM_BaseHome");
            // BGM音量を40%に設定.
            AudioManager.Instance().SetBgmVolume(40);
        }
        void ISceneInfo.InputStart()
        {
            
        }

        void ISceneInfo.InputStop()
        {
            
        }
    }
}
