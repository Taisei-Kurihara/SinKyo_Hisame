using UnityEngine;
using Common;
using InGame.Common;
using UnityEngine.UI;
using Unity.VisualScripting;
using TMPro;
using UnityEngine.InputSystem;

namespace SceneEventer
{
    /// <summary>
    /// Eventer
    /// </summary>
    public class KeyConfigEventer : MenuButtonEventer
    {
        //KeyConfig config=new KeyConfig();

        [SerializeField] private ScrollRect scrollRect;

        //Buttonで定義していく
        [SerializeField] private Button firstAttackKeySelect;
        [SerializeField] private Button secondAttackKeySelect;
        [SerializeField] private Button restrainAttackSelect;
        [SerializeField] private Button specialAttackSelect;
        [SerializeField] private Button dodge;
        [SerializeField] private Button jump;
        [SerializeField] private Button heal;
        [SerializeField] private Button guard;

        [Header("KeyConfig表示用Text")]
        [SerializeField] private TextMeshProUGUI firstAttackKeyText;
        [SerializeField] private TextMeshProUGUI secondAttackKeyText;
        [SerializeField] private TextMeshProUGUI restrainAttackText;
        [SerializeField] private TextMeshProUGUI specialAttackText;
        [SerializeField] private TextMeshProUGUI dodgeText;
        [SerializeField] private TextMeshProUGUI jumpText;
        [SerializeField] private TextMeshProUGUI healText;
        [SerializeField] private TextMeshProUGUI guardText;


        // buttonsSlot 実装（abstract 強制）.
        private ButtonSlotDictionary _buttonsSlot;
        protected override ButtonSlotDictionary buttonsSlot => _buttonsSlot;

        KeyConfig keyConfig = new KeyConfig();
        protected override void Init()
        {
            keyConfig.Initialize();

            //テキスト全部初期化
            SetTextInitializeKeyBoard();

            _buttonsSlot = new ButtonSlotDictionary()
            {
                { (MenuButtonIndex)0, 0, FirstAttackEvent    },
                { (MenuButtonIndex)1, 1, SecondAttackEvent   },
                { (MenuButtonIndex)2, 2, RestrainAttackEvent },
                { (MenuButtonIndex)3, 3, SpecialAttackEvent  },
                { (MenuButtonIndex)4, 4, DodgeEvent          },
                { (MenuButtonIndex)5, 5, JumpEvent           },
                { (MenuButtonIndex)6, 6, HealEvent           },
                { (MenuButtonIndex)7, 7, GuardEvent          },
            };

            buttons = new MenuButton[][]
            {
                new MenuButton[]{ new MenuButton{ button = firstAttackKeySelect, index = (MenuButtonIndex)0 } },
                new MenuButton[]{ new MenuButton{ button = secondAttackKeySelect, index = (MenuButtonIndex)1 } },
                new MenuButton[]{ new MenuButton{ button = restrainAttackSelect, index = (MenuButtonIndex)2 } },
                new MenuButton[]{ new MenuButton{ button = specialAttackSelect, index = (MenuButtonIndex)3 } },
                new MenuButton[]{ new MenuButton{ button = dodge, index = (MenuButtonIndex)4 } },
                new MenuButton[]{ new MenuButton{ button = jump, index = (MenuButtonIndex)5 } },
                new MenuButton[]{ new MenuButton{ button = heal, index = (MenuButtonIndex)6 } },
                new MenuButton[]{ new MenuButton{ button = guard, index = (MenuButtonIndex)7 } },
            };
        }

        public void FirstAttackEvent()
        {
            keyConfig.OnEventChangedKeyboard(
                action.CharacterController.FirstAttack,
                () => SetTextInitializeKeyBoard()
            );
        }

        public void SecondAttackEvent()
        {
            keyConfig.OnEventChangedKeyboard(
                action.CharacterController.SecondAttack,
                () => SetTextInitializeKeyBoard()
            );
        }

        public void RestrainAttackEvent()
        {
            keyConfig.OnEventChangedKeyboard(
                action.CharacterController.RestrainAttack,
                () => SetTextInitializeKeyBoard()
            );
        }

        public void SpecialAttackEvent()
        {
            keyConfig.OnEventChangedKeyboard(
                action.CharacterController.SpecialAttack,
                () => SetTextInitializeKeyBoard()
            );
        }

        public void DodgeEvent()
        {
            keyConfig.OnEventChangedKeyboard(
                action.CharacterController.Dodge,
                () => SetTextInitializeKeyBoard()
            );
        }

        public void JumpEvent()
        {
            keyConfig.OnEventChangedKeyboard(
                action.CharacterController.Jump,
                () => SetTextInitializeKeyBoard()
            );
        }

        public void HealEvent()
        {
            keyConfig.OnEventChangedKeyboard(
                action.CharacterController.Heal,
                () => SetTextInitializeKeyBoard()
            );
        }

        public void GuardEvent()
        {
            keyConfig.OnEventChangedKeyboard(
                action.CharacterController.Guard,
                () => SetTextInitializeKeyBoard()
            );
        }
        
        private void SetFirstAttackText(string _text) { firstAttackKeyText.text = _text; }
        private void SetSecondAttackText(string _text) { secondAttackKeyText.text = _text; }
        private void SetRestrainAttackText(string _text) { restrainAttackText.text = _text; }
        private void SetSpecialAttackText(string _text) { specialAttackText.text = _text; }
        private void SetDodgeText(string _text) { dodgeText.text = _text; }
        private void SetJumpText(string _text) { jumpText.text = _text; }
        private void SetHealText(string _text) { healText.text = _text; }
        private void SetGuardText(string _text) { guardText.text = _text; }


        /// <summary>
        /// 全キーの現在のバインディングキーを取得する時の処理。少々量が多め。
        /// </summary>
        private void SetTextInitializeKeyBoard()
        {
            // FirstAttack
            SetTextByAction(action.CharacterController.FirstAttack, firstAttackKeyText);

            // SecondAttack
            SetTextByAction(action.CharacterController.SecondAttack, secondAttackKeyText);

            // RestrainAttack
            SetTextByAction(action.CharacterController.RestrainAttack, restrainAttackText);

            // SpecialAttack
            SetTextByAction(action.CharacterController.SpecialAttack, specialAttackText);

            // Dodge
            SetTextByAction(action.CharacterController.Dodge, dodgeText);

            // Jump
            SetTextByAction(action.CharacterController.Jump, jumpText);

            // Heal
            SetTextByAction(action.CharacterController.Heal, healText);

            // Guard
            SetTextByAction(action.CharacterController.Guard, guardText);
        }
        private void SetTextByAction(InputAction action, TextMeshProUGUI text)
        {
            // Keyboard
            int? kbIndex = keyConfig.OnSearchBindingType(action, KeyConfig.KeyBindType.Keyboard);
            // Mouse
            int? mouseIndex = keyConfig.OnSearchBindingType(action, KeyConfig.KeyBindType.Mouse);

            int? index = kbIndex ?? mouseIndex; // 優先度：Keyboard → Mouse
            if (index != null)
            {
                var binding = new KeyConfig.Binding { inputAction = action, indexBinding = index.Value };
                text.text = binding.GetEffectiveKey();
            }
            else
            {
                text.text = "未設定";
            }
        }
    }
}
