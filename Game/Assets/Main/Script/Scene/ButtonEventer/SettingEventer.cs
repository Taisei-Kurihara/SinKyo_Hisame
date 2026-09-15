using Common;
using Cysharp.Threading.Tasks;
using InGame.Common;
using SceneInfo;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

namespace SceneEventer
{
    public enum SettingType
    {
        Game = 0,
        Audio = 1,
        Window = 2
    }

    public class SettingEventer : MenuButtonEventer
    {
        // Addressableで読み込んだリソースのハンドル管理（List化）
        private List<AsyncOperationHandle<GameObject>> currentSettingHandles = new List<AsyncOperationHandle<GameObject>>();
        private List<GameObject> currentSettingObjects = new List<GameObject>();
        [SerializeField]
        private Button Game;
        [SerializeField]
        private Button Audio;
        [SerializeField]
        private Button Window;
        [SerializeField]
        private Button Back;
        [SerializeField]
        private Button Save;

        //ボタンクールタイム用
        private CoolTime CoolTimeButton;

        // buttonsSlot 実装（abstract 強制）.
        private ButtonSlotDictionary _buttonsSlot;
        protected override ButtonSlotDictionary buttonsSlot => _buttonsSlot;

        protected override void Init()
        {
            _buttonsSlot = new ButtonSlotDictionary()
            {
                { (MenuButtonIndex)0, 0, () => LoadAndDisplaySettings(SettingType.Game).Forget()  },
                { (MenuButtonIndex)1, 1, () => LoadAndDisplaySettings(SettingType.Audio).Forget() },
                { (MenuButtonIndex)2, 2, () => LoadAndDisplaySettings(SettingType.Window).Forget() },
            };

            buttons = new MenuButton[][]
            {
                new MenuButton[]{ new MenuButton{ button = Game,   index = (MenuButtonIndex)0 } },
                new MenuButton[]{ new MenuButton{ button = Audio,  index = (MenuButtonIndex)1 } },
                new MenuButton[]{ new MenuButton{ button = Window, index = (MenuButtonIndex)2 } },
            };
        }

        /// <summary>
        /// 設定の読み込みと表示
        /// </summary>
        /// <param name="name">設定名</param>
        public async UniTask LoadAndDisplaySettings(SettingType name)
        {
            // SettingTypeの番号をインデックスとして使用
            int index = (int)name;
            
            // リストのサイズを調整（必要な場合）
            while (currentSettingHandles.Count <= index)
            {
                currentSettingHandles.Add(default);
                currentSettingObjects.Add(null);
            }

            // 既存の設定画面があれば破棄（該当インデックスのみ）
            ReleaseSettingAtIndex(index);

            // Addressableを使用して設定を読み込み
            string addressKey = $"Settings/{name}Panel";
            var handle = Addressables.LoadAssetAsync<GameObject>(addressKey);
            
            await handle.Task;
            
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                // 設定パネルをインスタンス化
                var settingObject = Instantiate(handle.Result, transform);
                
                // リストの指定インデックスに保存
                currentSettingHandles[index] = handle;
                currentSettingObjects[index] = settingObject;
                
                // UIへの表示処理
                settingObject.SetActive(true);
            }
            else
            {
                Debug.LogError($"設定パネルの読み込みに失敗しました: {name}");
                Addressables.Release(handle);
            }
        }

        /// <summary>
        /// 設定の保存
        /// </summary>
        public void SaveSettings()
        {
            // 現在の設定を保存/適用.
            // 設定データの永続化処理.
        }

        /// <summary>
        /// キャンセル処理.
        /// </summary>
        public void CancelSettings()
        {
            // 前の画面に戻る.
            // 設定を保存せずに読み込みデータを全削除.
            ReleaseCurrentSettings();
            
            // 変更前の状態に復元
        }

        /// <summary>
        /// 現在の設定画面を破棄（全削除）.
        /// </summary>
        private void ReleaseCurrentSettings()
        {
            // 全てのインスタンス化したオブジェクトを破棄.
            for (int i = 0; i < currentSettingObjects.Count; i++)
            {
                if (currentSettingObjects[i] != null)
                {
                    Destroy(currentSettingObjects[i]);
                    currentSettingObjects[i] = null;
                }
            }

            // 全てのAddressableのハンドルを解放.
            for (int i = 0; i < currentSettingHandles.Count; i++)
            {
                if (currentSettingHandles[i].IsValid())
                {
                    Addressables.Release(currentSettingHandles[i]);
                }
            }
            
            // リストをクリア.
            currentSettingObjects.Clear();
            currentSettingHandles.Clear();
        }

        /// <summary>
        /// 指定インデックスの設定画面を破棄.
        /// </summary>
        private void ReleaseSettingAtIndex(int index)
        {
            // 指定インデックスのオブジェクトを破棄.
            if (index < currentSettingObjects.Count && currentSettingObjects[index] != null)
            {
                Destroy(currentSettingObjects[index]);
                currentSettingObjects[index] = null;
            }

            // 指定インデックスのハンドルを解放.
            if (index < currentSettingHandles.Count && currentSettingHandles[index].IsValid())
            {
                Addressables.Release(currentSettingHandles[index]);
                currentSettingHandles[index] = default;
            }
        }

        /// <summary>
        /// オブジェクト破棄時の処理.
        /// </summary>
        private void OnDestroy()
        {
            // リソースの確実な解放.
            ReleaseCurrentSettings();
        }
    }
}