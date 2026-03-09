using UnityEngine;
using UnityEngine.InputSystem;

namespace Common 
{
    /// <summary>
    /// コントローラーが接続した時の処理。
    /// </summary>
    public class ControllerManager : SingletonMonoBase<ControllerManager>
    {
        public void Start()
        {
            

                // 全デバイスを取得
                var devices = InputSystem.devices;

                foreach (var device in devices)
                {
                    if (device is Gamepad)
                    {//デバイスがゲームパッド(コントローラー)の時だけ処理
                        Gamepad gamepad = device as Gamepad;
                        Debug.Log($"コントローラー検出: {gamepad.displayName}");
                    }
                }
        }
      
        private void OnEnable()
        {
            //デバイスの接続/切断のイベントに処理登録
            InputSystem.onDeviceChange += OnDeviceChange;
        }


        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (device is Gamepad)
            {//デバイスがゲームパッド(コントローラー)の時だけ処理
                switch (change)
                {
                    case InputDeviceChange.Added:
                        Debug.Log($"コントローラー接続: {device.displayName}");
                        break;
                    case InputDeviceChange.Removed:
                        Debug.Log($"コントローラー切断: {device.displayName}");
                        break;
                }
            }
        }

    } 

}
