using UnityEngine;

namespace Novel.Data
{
    /// <summary>
    /// ノベルの基礎データ
    /// </summary>
    public class INovel_Data
    {
        //シーンデータ
        public string SceneName;
        //シーンデータが変更されたときに呼び出す。
        public string SceneAddress;
        //ナレーション文かどうか
        public bool NarrationOnOff;
        //プライマリーキー扱い。
        public string CharacterName;
        //キャラクターのデータを呼び出す。
        public string[] CharacterAddress;
        //実際の文字。
        public string NovelData;
    }
}