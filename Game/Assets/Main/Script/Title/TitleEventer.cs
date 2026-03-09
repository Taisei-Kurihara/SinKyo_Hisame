using Common;
using Cysharp.Threading.Tasks;
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
                    NewGame();
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

        public void GameStart()
        {
            SceneManager.Instance().LoadMainScene(new MainSceneInfo()).Forget();
        }

        public void NewGame()
        {
            SceneManager.Instance().LoadMainScene(new StageSelectInfo()).Forget();
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