using UnityEngine;

namespace Common
{
    /// <summary>
    /// InputSystem_Actions
    /// </summary>
    public class InputSystemActionsManager : SingletonMonoBase<InputSystemActionsManager>
    {
        private InputSystem_Actions _InputSystemActions;
        /// <summary>
        /// アクションマップの情報を取得する
        /// </summary>
        /// <returns></returns>
        public InputSystem_Actions GetInputSystem_Actions()
        {
            if (_InputSystemActions == null)
            {
                _InputSystemActions = new InputSystem_Actions();
            }
            return _InputSystemActions;
        }

        /// <summary>
        /// Player入力の有効化
        /// </summary>
        public void EnablePlayer()
        {
            if (_InputSystemActions == null)
            {
                _InputSystemActions = new InputSystem_Actions();
            }
            _InputSystemActions?.Player.Enable();
            _InputSystemActions?.UI.Disable();
        }

        /// <summary>
        /// UI入力の有効化
        /// </summary>
        public void EnableUI()
        {
            if (_InputSystemActions == null)
            {
                _InputSystemActions = new InputSystem_Actions();
            }
            _InputSystemActions?.Player.Disable();
            _InputSystemActions?.UI.Enable();
        }

        /// <summary>
        /// Player入力の無効化
        /// </summary>
        public void DisablePlayer()
        {
            if (_InputSystemActions == null)
            {
                _InputSystemActions = new InputSystem_Actions();
            }
            _InputSystemActions?.Player.Disable();
            Debug.Log("プレイヤー操作無効化");
        }

        /// <summary>
        /// UI入力の有効化
        /// </summary>
        public void UIDisable()
        {
            if (_InputSystemActions == null)
            {
                _InputSystemActions = new InputSystem_Actions();
            }
            _InputSystemActions?.UI.Disable();
            Debug.Log("UI操作無効化");
        }

        /// <summary>
        /// CharacterController入力の無効化
        /// </summary>
        public void DisableCharacterController()
        {
            _InputSystemActions?.CharacterController.Disable();
            Debug.Log("CharacterController操作無効化");
        }

        /// <summary>
        /// 全てのアクションマップを無効化
        /// </summary>
        public void DisableAll()
        {
            _InputSystemActions?.CharacterController.Disable();
            _InputSystemActions?.Player.Disable();
            _InputSystemActions?.UI.Disable();
            Debug.Log("全入力操作無効化");
        }

        /// <summary>
        /// アプリケーション終了時処理 - InputSystemリーク防止.
        /// </summary>
        private void OnApplicationQuit()
        {
            CleanupInputSystem();
        }

        /// <summary>
        /// 破棄時処理 - InputSystemリーク防止.
        /// </summary>
        private void OnDestroy()
        {
            CleanupInputSystem();
        }

        /// <summary>
        /// InputSystemクリーンアップ処理.
        /// </summary>
        private void CleanupInputSystem()
        {
            if (_InputSystemActions != null)
            {
                _InputSystemActions.CharacterController.Disable();
                _InputSystemActions.Player.Disable();
                _InputSystemActions.UI.Disable();
                _InputSystemActions.Dispose();
                _InputSystemActions = null;
                Debug.Log("[InputSystemActionsManager] InputSystem cleaned up.");
            }
        }
    }
}