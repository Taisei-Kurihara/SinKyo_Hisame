using Common;
using Cysharp.Threading.Tasks;
using InGame.Enemy;
using SceneInfo;
using UnityEngine;
using UnityEngine.UI;

namespace SceneEventer
{
    public class TitleEventer : ButtonEventer
    {
        [SerializeField]
        private Button gameStart;
        [SerializeField]
        private Button newGame;
        [SerializeField]
        private Button setting;
        [SerializeField]
        private Button QuitGame;


        //ここで全ての処理のボタン押したらっていう処理を書く
        protected override void ButtonEvents(Button button)
        {
            switch (button)
            {
                case var _ when button == gameStart:
                    GameStart();
                    break;
                case var _ when button == newGame:
                    GameStart();
                    break;
                case var _ when button == setting:
                    break;
                case var _ when button == QuitGame:
                    Quit();
                    break;
            }
        }

        protected override void Init()
        {
            buttons = new Button[][]
            {
                    new Button[]{gameStart},
                    new Button[]{newGame},
                    new Button[]{setting},
                    new Button[]{QuitGame}
            };
        }

        [Header("ミッション設定")]
        [Tooltip("難易度")]
        [SerializeField] private MissionTag difficulty = MissionTag.Difficulty_Normal;

        [Tooltip("条件")]
        [SerializeField] private MissionTag condition = MissionTag.Condition_BossNormal;

        [Tooltip("Enemy名")]
        [SerializeField] private MissionTag enemyName = MissionTag.Enemy_Wendigo;

        public const string MissionTagsPrefsKey = "MissionTags";

        /// <summary>選択中のミッションタグ（全カテゴリ合成）.</summary>
        public MissionTag SelectedTags => difficulty | condition | enemyName;

        public void GameStart()
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

        public void NewGame()
        {
            SceneManager.Instance().LoadMainScene(new StageSelectInfo()).Forget();
        }

        /// <summary>
        /// MissionTag(Enemy_*) → EnemyName enum変換.
        /// </summary>
        private static EnemyName MissionTagToEnemyName(MissionTag tag)
        {
            if ((tag & MissionTag.Enemy_Wendigo) != 0) return EnemyName.Wendigo;
            return EnemyName.None;
        }

        /// <summary>
        /// Game終了処理。
        /// </summary>
        public void Quit()
        {
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }
        
    }
}