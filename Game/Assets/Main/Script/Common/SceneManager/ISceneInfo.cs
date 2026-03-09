using Cysharp.Threading.Tasks;

namespace Common
{
    /// <summary>
    /// シーン情報。
    /// </summary>
    public interface ISceneInfo
    {
        public string SceneName { get; }

        public UniTask Init();
        /// <summary>
        /// シーン終了時
        /// </summary>
        public UniTask End();

        /// <summary>
        /// InputSystem起動
        /// </summary>
        public void InputStart();

        /// <summary>
        /// InputSystem停止
        /// </summary>
        public void InputStop();
    }

    public enum SceneType
    {
        Main,Sub
    }
}
