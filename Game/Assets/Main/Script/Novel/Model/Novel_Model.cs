using UnityEngine;
using R3;


namespace Novel.Model
{ 
    public class Novel_Model
    {
        public readonly float TextLoadTime = 0.01f;
        
        
        //名前
        public ReadOnlyReactiveProperty<string> NameText => _nameText;
        private readonly ReactiveProperty<string> _nameText = new ReactiveProperty<string>("");
        //文字
        public ReadOnlyReactiveProperty<string> Text => _text;
        private readonly ReactiveProperty<string> _text = new ReactiveProperty<string>("");

        public ReactiveProperty<string> test {get; private set;} = new ReactiveProperty<string>();
        public void ReadNameText(string nameText)
        {
            _nameText.Value = nameText;
        }
        public void ReadText(string text)
        {
            _text.Value += text;
        }

        /// <summary>
        /// シナリオのデータをロードする。
        /// </summary>
        public void ScenarioLoaderData()
        {

        }
    }
}