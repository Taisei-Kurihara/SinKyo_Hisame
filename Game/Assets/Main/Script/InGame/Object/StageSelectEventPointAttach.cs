using UnityEngine;
using GameCommon;
using Common;
using InGame.Enemy;
using SceneInfo;
using Cysharp.Threading.Tasks;

namespace GameEventPoint
{

    public class StageSelectEventPointAttach : EventPointAbstract
    {
        [Header("ミッション設定")]
        [Tooltip("難易度")]
        [SerializeField] private MissionTag difficulty = MissionTag.Difficulty_Normal;

        [Tooltip("条件")]
        [SerializeField] private MissionTag condition = MissionTag.Condition_BossNormal;

        [Tooltip("Enemy名")]
        [SerializeField] private MissionTag enemyName = MissionTag.Enemy_Wendigo;

        /// <summary>選択中のミッションタグ（全カテゴリ合成）.</summary>
        public MissionTag SelectedTags => difficulty | condition | enemyName;

        /// <summary>PlayerPrefsキー（シーン間受け渡し用）.</summary>
        public const string MissionTagsPrefsKey = "MissionTags";

        public override void OnEvent()
        {
            // Enemy名 → EnemyName enumへの変換.
            EnemyName enemy = MissionTagToEnemyName(enemyName);
            PlayerPrefs.SetInt("EnemyName", (int)enemy);

            // MissionTags（難易度+条件+Enemy名の合成）を保存.
            PlayerPrefs.SetInt(MissionTagsPrefsKey, (int)SelectedTags);
            PlayerPrefs.Save();

            Debug.Log($"[StageSelect] 難易度:{difficulty} 条件:{condition} Enemy:{enemyName} → Tags:{SelectedTags} ({(int)SelectedTags})");

            // MainSceneInfo で敵生成を含むシーンをロード.
            SceneManager.Instance().LoadMainScene(new MainSceneInfo()).Forget();
        }

        /// <summary>
        /// MissionTag(Enemy_*) → EnemyName enum変換.
        /// </summary>
        private static EnemyName MissionTagToEnemyName(MissionTag tag)
        {
            if ((tag & MissionTag.Enemy_Wendigo) != 0) return EnemyName.Wendigo;
            return EnemyName.None;
        }
    }
}
