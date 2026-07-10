using Common;
using Cysharp.Threading.Tasks;
using InGame;
using InGame.Enemy;
using InGame.Player;
using Setting;
using Tutorial;
using UnityEngine;

namespace SceneInfo
{
    public class MainSceneInfo : ISceneInfo
    {
        string ISceneInfo.SceneName => "BaseHome";

        UniTask ISceneInfo.End() => UniTask.CompletedTask;

        async UniTask ISceneInfo.Init()
        {
            Debug.Log("[MainSceneInfo] Init開始");
            PlayerManager playerManager = PlayerManager.Instance();
            playerManager.pulseModel.ResetToBase();
            await playerManager.InstantiateCharacter("PlayerCharacter");
            Debug.Log("[MainSceneInfo] PlayerCharacter生成完了");

            // 敵キャラクター生成（PlayerPrefsからEnemyName読み取り）.
            await EnemyManager.Instance().InstantiateEnemyFromPrefs(autoSpawn: true);
            Debug.Log("[MainSceneInfo] Enemy生成完了");

            // ヒットエフェクトプール初期化.
            await HitEffectPool.Instance(false).InitPool("HitEffect");

            // ダメージカウンタープール初期化.
            await DamageCounterPool.Instance(false).InitPool("DamageCounter");

            // 血痕エフェクトプール初期化.
            await BloodSplatterPool.Instance(false).InitPool("BloodSplatter");

            // プレイヤーエフェクトプール初期化（スタン:1個、回復:3個）.
            var playerEffectPool = PlayerEffectPool.Instance(false);
            await playerEffectPool.InitPool("PlayerEffect_Stun", 1);
            await playerEffectPool.InitPool("PlayerEffect_Heal", 3);
            await playerEffectPool.InitPool("UP", 2);
            await playerEffectPool.InitPool("Down", 2);
            Debug.Log("[MainSceneInfo] エフェクトプール初期化完了 → BGM読み込み開始");

            // ゲーム中はチュートリアルウィンドウを非表示で開始.
            TutorialManager.Instance(false)?.HideTutorial();

            // BGM再生（ビルド版ではawaitしないとAddressables読み込み完了前にフローが終了する）.
            await AudioManager.Instance().LoadBgm("BGM_BaseHome");
            AudioManager.Instance().SetBgmVolume(40);
            Debug.Log("[MainSceneInfo] Init完了");
        }

        void ISceneInfo.InputStart()
        {
        }

        void ISceneInfo.InputStop()
        {
        }
    }
}