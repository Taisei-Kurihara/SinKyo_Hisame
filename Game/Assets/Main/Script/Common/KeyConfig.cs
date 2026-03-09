using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Common
{
    /// <summary>
    /// キーバインド設定について
    /// </summary>
    public class KeyConfig
    {
        // キーコンフィグの保存パス
        private string keyConfigFolderPath = "/KeyConfig/";
        private string pathKeyConfig = "/KeyConfig.json";
        private InputSystem_Actions actions;

        public void Initialize()
        {
            // 初期化時にアクションを取得
            actions = InputSystemActionsManager.Instance().GetInputSystem_Actions();
            if (actions == null)
            {
                Debug.LogError("Input actions are not initialized.");
            }
        }

        // 保存先のフルパス
        private string GetKeyConfigPath()
        {
            return Application.dataPath + keyConfigFolderPath + pathKeyConfig;
        }

        /// <summary>
        /// バインディングタイプ
        /// </summary>
        public enum KeyBindType
        {
            Keyboard,
            Gamepad,
            Mouse
        }

        /// <summary>
        /// キーコンフィグを保存する
        /// </summary>
        public void SaveJsonData()
        {
            // actions が null の場合は処理しない
            if (actions == null)
            {
                Debug.LogWarning("No input actions to save.");
                return;
            }

            // フォルダが存在しない場合は作成
            string folderPath = Application.dataPath + keyConfigFolderPath;
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                Debug.Log("KeyConfig folder created.");
            }

            // JSONデータを保存
            string _json = actions.SaveBindingOverridesAsJson();
            string fullPath = GetKeyConfigPath();
            using (var sw = new StreamWriter(fullPath, false, System.Text.Encoding.UTF8))
            {
                sw.Write(_json);
                Debug.Log("KeyConfig saved successfully.");
            }
        }

        /// <summary>
        /// キーコンフィグをロードする
        /// </summary>
        public void LoadJsonData()
        {
            string fullPath = GetKeyConfigPath();

            if (File.Exists(fullPath))
            {
                using (var stream = new StreamReader(fullPath))
                {
                    // JSONファイルをStringとして読み込む
                    string _file = stream.ReadToEnd();
                    actions.asset.LoadBindingOverridesFromJson(_file);
                    Debug.Log("KeyConfig loaded successfully.");
                }
            }
            else
            {
                Debug.LogWarning("No KeyConfig file found. Creating default config.");
                // ファイルがない場合は、デフォルトのキー設定を保存する
                SaveJsonData();
            }
        }

        /// <summary>
        /// 初期化メソッド (アプリ起動時)
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void KeyConfigInitialize()
        {
            KeyConfig keyConfig = new KeyConfig();
            keyConfig.Initialize();
            // コンフィグ初期化設定
            keyConfig.LoadJsonData();
        }

        /// <summary>
        /// キーバインディング
        /// </summary>
        public struct Binding
        {
            public InputAction inputAction;
            public int indexBinding;

            /// <summary>
            /// 変更後パス取得
            /// </summary>
            public string GetEffectivePath()
            {
                return inputAction.bindings[indexBinding].effectivePath;
            }

            /// <summary>
            /// 現在のキー情報のみを取得
            /// </summary>
            public string GetEffectiveKey()
            {
                string path = inputAction.bindings[indexBinding].effectivePath;
                string[] parts = path.Split('/');
                return parts[parts.Length - 1]; // 最後の要素を返す
            }

            /// <summary>
            /// デフォルトパス取得
            /// </summary>
            public string GetPath()
            {
                return inputAction.bindings[indexBinding].path;
            }
        }

        /// <summary>
        /// 同じキーバインドを探す
        /// </summary>
        public Binding? OnSearchKeyBind(InputActionMap _actionMap, string _path, InputAction _inputAction)
        {
            foreach (var _action in _actionMap)
            {
                for (int i = 0; i < _action.bindings.Count; i++)
                {
                    var binding = _action.bindings[i];
                    // Pathの設定が同じ場合、処理を終了する
                    if (binding.effectivePath == _path)
                    {
                        // Actionが同じではない場合のみ取得
                        if (_inputAction != _action)
                        {
                            return new Binding { inputAction = _action, indexBinding = i };
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// バインディングタイプがどれか探す
        /// </summary>
        public int? OnSearchBindingType(InputAction _action, KeyBindType _keyBindType)
        {
            string devicePrefix = _keyBindType switch
            {
                KeyBindType.Keyboard => "<Keyboard>",
                KeyBindType.Gamepad => "<Gamepad>",
                KeyBindType.Mouse => "<Mouse>",
                _ => null
            };
            for (int i = 0; i < _action.bindings.Count; i++)
            {
                var binding = _action.bindings[i];
                // 空パス対策
                if (string.IsNullOrEmpty(binding.path))
                    continue;

                if (binding.path.StartsWith(devicePrefix))
                {
                    return i;
                }
            }
            return null;
        }

        private InputActionRebindingExtensions.RebindingOperation rebindOperation;

        // リバインドキャンセル
        public void Cancel()
        {
            if (rebindOperation != null)
            {
                rebindOperation?.Cancel();
                rebindOperation?.Dispose();
                rebindOperation = null;
            }
        }

        /// <summary>
        /// キーバインド設定/キーボードorMouse
        /// </summary>
        public Binding? OnEventChangedKeyboard(InputAction _action, Action _complete = null)
        {
            int? _keyboardNum = OnSearchBindingType(_action, KeyBindType.Keyboard);
            int? _mouseboardNum = OnSearchBindingType(_action, KeyBindType.Mouse);

            int num = 0;
            if (_keyboardNum == null && _mouseboardNum == null)
            {
                Debug.LogError(_action.name + "が設定されていません。ただちに設定をお願いします。");
                return null;
            }
            else if (_keyboardNum != null && _mouseboardNum != null)
            {
                Debug.LogError(_action.name + "がマウス、キーボード両方に設定されています。ただちに設定をお願いします。");
                return null;
            }
            else if (_keyboardNum != null)
            {
                num = (int)_keyboardNum;
            }
            else if (_mouseboardNum != null)
            {
                num = (int)_mouseboardNum;
            }

            Cancel();

            _action.Disable();

            // 事前にパスを保存しておく
            string beforePath = _action.bindings[num].effectivePath;
            Binding? bind = new Binding { inputAction = _action, indexBinding = (int)num };

            rebindOperation = _action.PerformInteractiveRebinding(num)
                .WithControlsExcluding("Gamepad") // ゲームパッドは除外
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(callback =>
                {
                    Binding? binding = OnSearchKeyBind(_action.actionMap, _action.bindings[(int)num].effectivePath, _action);
                    if (binding != null)
                    {
                        binding.Value.inputAction.ApplyBindingOverride(binding.Value.indexBinding, beforePath);
                    }
                    _complete?.Invoke();

                    SaveJsonData();
                    callback.Dispose();
                    _action.Enable();
                    rebindOperation = null;
                }).Start();

            return bind;
        }

        /// <summary>
        /// キーバインド設定/GamePad用
        /// </summary>
        public Binding? OnEventChangedGamePad(InputAction _action, Action _complete = null)
        {
            Cancel();

            int? num = OnSearchBindingType(_action, KeyBindType.Gamepad);
            if (num == null)
            {
                Debug.LogError("GamePadのアクションタイプが登録されていません。");
                return null;
            }

            string beforePath = _action.bindings[(int)num].effectivePath;
            Binding? bind = new Binding { inputAction = _action, indexBinding = (int)num };

            _action.Disable();
            rebindOperation = _action.PerformInteractiveRebinding((int)num)
                .WithControlsExcluding("Keyboard,Mouse") // キーボードを入力受付拒否
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(callback =>
                {
                    Binding? binding = OnSearchKeyBind(_action.actionMap, _action.bindings[(int)num].effectivePath, _action);
                    if (binding != null)
                    {
                        binding.Value.inputAction.ApplyBindingOverride(binding.Value.indexBinding, beforePath);
                    }
                    _complete?.Invoke();

                    SaveJsonData();
                    callback.Dispose();
                    _action.Enable();
                    rebindOperation = null;
                }).Start();

            return bind;
        }
    }
}
