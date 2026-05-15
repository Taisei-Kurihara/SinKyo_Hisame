using Common;
using Cysharp.Threading.Tasks;
using InGame;
using InGame.Player;
using Setting;
using UnityEngine;

namespace SceneInfo
{
    /// <summary>
    /// デバッグルーム
    /// </summary>
    public class PlayerDebugInfo : ISceneInfo
    {
        string ISceneInfo.SceneName => "PlayerDebug";

        UniTask ISceneInfo.End() => UniTask.CompletedTask;

        async UniTask ISceneInfo.Init()
        {
            Debug.Log("デバッグルーム:Player");
            PlayerManager playerManager = PlayerManager.Instance();
            playerManager.pulseModel.ResetToBase();
            await playerManager.InstantiateCharacter("PlayerCharacter");

            // ヒットエフェクトプール初期化.
            await HitEffectPool.Instance(false).InitPool("HitEffect");

            // プレイヤーエフェクトプール初期化（スタン:1個、回復:3個）.
            var playerEffectPool = PlayerEffectPool.Instance(false);
            await playerEffectPool.InitPool("PlayerEffect_Stun", 1);
            await playerEffectPool.InitPool("PlayerEffect_Heal", 3);
            await playerEffectPool.InitPool("UP", 2);
            await playerEffectPool.InitPool("Down", 2);

            // BGM再生（ビルド版ではawaitしないとAddressables読み込み完了前にフローが終了する）.
            await AudioManager.Instance().LoadBgm("BGM_BaseHome");
        }

        void ISceneInfo.InputStart()
        {
        }

        void ISceneInfo.InputStop()
        {
        }
    }
}
