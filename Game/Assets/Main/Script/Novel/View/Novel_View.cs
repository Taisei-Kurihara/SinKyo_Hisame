using TMPro;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using System;

namespace Novel.View
{
    /// <summary>
    /// ノベルシーン用View
    /// </summary>
    public class Novel_View : MonoBehaviour
    {
        /// <summary>
        /// 文字表示。
        /// </summary>
        [SerializeField]
        private TextMeshProUGUI NovelText;
        private CancellationTokenSource NovelCanell;

        /// <summary>
        /// 文字を読み込む。
        /// </summary>
        public void NovelTextLoad_Nomal(string Text)
        {
            NovelText.text = Text;
        }
        /// <summary>
        /// 文字送り＋読み込み
        /// </summary>
        /// <param name="LoadText"></param>
        /// <param name="LoadNum"></param>
        /// <returns></returns>
        public async UniTask NovelTextLoad_Into(string LoadText,float LoadNum)
        {
            NovelCanell?.Cancel();
            NovelCanell?.Dispose();
            NovelCanell = new CancellationTokenSource();
            var token = NovelCanell.Token;

            NovelText.text = LoadText;
            var length = NovelText.text.Length;


            try
            {
                for (int i = 0; i < length; i++)
                {
                    NovelText.maxVisibleCharacters = i;
                    await UniTask.WaitForSeconds(LoadNum, cancellationToken: token);
                }
            }
            catch (OperationCanceledException)
            {
                // キャンセル時の処理
            }
            finally
            {
                // 最後にすべての文字を表示しておく
                NovelText.maxVisibleCharacters = length;

                NovelCanell?.Cancel();
                NovelCanell?.Dispose();
                NovelCanell = null;
            }
        }

        /// <summary>
        /// 選択肢の表示
        /// </summary>
        public void OnChoices()
        {

        }

    }
}