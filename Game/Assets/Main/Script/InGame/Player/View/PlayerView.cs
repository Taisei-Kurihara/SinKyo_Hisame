using InGame.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;
#if UNITY_EDITOR
using Cysharp.Threading.Tasks;
using InGame.Common;
#endif

namespace InGame.Player
{
    public class PlayerView : MonoBehaviour, IPlayerView
    {

        [SerializeField]
        private Image HpGauge;
        [SerializeField]
        private TextMeshProUGUI breathPointPercent;
        [Header("回復可能回数のUI")]
        [SerializeField]
        private List<Image> healPoint;
        //テキストかUI表示で固定するか非常に悩ましいよね。

        [SerializeField]
        Image heartGage;
        [SerializeField]
        Animator heart;
        [SerializeField]
        TextMeshProUGUI heartText;

        [SerializeField]
        DrainUI drainUI;

        [SerializeField]
        Animator screen;

        List<Image> drainUIGages = new List<Image>();

        // HPゲージアニメーション用.
        private float targetHpPercent = 1f;
        private const float hpGaugeAnimDuration = 0.5f;

        // HPゲージカラー閾値（変更はここだけで可能）.
        [Header("HPゲージカラー設定")]
        [SerializeField] private float hpColorThresholdHigh = 0.8f;
        [SerializeField] private float hpColorThresholdLow = 0.4f;

        [SerializeField] private CanvasGroup statusUI;
        [SerializeField] private CanvasGroup win;
        [SerializeField] private CanvasGroup lose;
        // 心拍数ゲージアニメーション用.
        private float targetHeartFill = 0.25f;
        private float currentHeartFill = 0.25f;
        // 0→200 の全域を 0.5sec (1/60*30) で移動する速度.
        private const float heartGageAnimDuration = 0.5f;

        // 心拍数 fill 0～1 = 0.25～0.82
        // 心拍数 は 0～200 まで
        /// <summary>
        /// 心拍数ゲージ設定.
        /// </summary>
        /// <param name="heartRate">心拍数(0-200).</param>
        public void SetHeartGauge(int heartRate)
        {
            // animatorに心拍数を設定.
            if (heart != null)
            {
                heart.SetInteger("heart", heartRate);
            }

            heartText.text = heartRate.ToString();

            // fillAmountを0.25～0.805の範囲に変換（目標値を保持、Updateで補間）.
            if (heartGage != null)
            {
                float percent = Mathf.Clamp01(heartRate / 200f);
                targetHeartFill = 0.25f + percent * (0.805f - 0.25f);
            }
        }

        public void SetHpGauge(float percent)
        {
            screen.SetBool("Tei", (percent < 0.33f));

            if (targetHpPercent > percent)
            {
                screen.SetTrigger("Hidan");
            }
            targetHpPercent = percent;

            // HP割合に応じたカラーグラデーション.
            // high以上 = 緑 (0,1,0).
            // high ~ low = 緑→黄 (0,1,0) → (1,1,0).
            // low ~ 0 = 黄→赤 (1,1,0) → (1,0,0).
            if (HpGauge != null)
            {
                Color hpColor;
                if (percent > hpColorThresholdHigh)
                {
                    hpColor = new Color(0f, 1f, 0f);
                }
                else if (percent > hpColorThresholdLow)
                {
                    float t = (percent - hpColorThresholdLow) / (hpColorThresholdHigh - hpColorThresholdLow);
                    hpColor = new Color(1f - t, 1f, 0f);
                }
                else
                {
                    float t = percent / hpColorThresholdLow;
                    hpColor = new Color(1f, t, 0f);
                }
                HpGauge.color = hpColor;
            }
        }

        private void Update()
        {
#if UNITY_EDITOR
            // デバッグ: Nキーで Enemy死亡処理を呼び出し.
            if (Input.GetKeyDown(KeyCode.N))
            {
                // 前回の状態をリセットして再実行可能に.
                DeathManager.Instance.DebugReset();
                DeathManager.Instance.NotifyEnemyDeath().Forget();
            }
#endif

            // HPゲージを0.5秒かけてシームレスに補間.
            if (HpGauge != null && !Mathf.Approximately(HpGauge.fillAmount, targetHpPercent))
            {
                float maxDelta = Time.deltaTime / hpGaugeAnimDuration;
                HpGauge.fillAmount = Mathf.MoveTowards(HpGauge.fillAmount, targetHpPercent, maxDelta);
            }

            // 心拍数ゲージを0.5秒かけてシームレスに補間.
            if (heartGage != null && !Mathf.Approximately(currentHeartFill, targetHeartFill))
            {
                // fillRange全域(0.555)を0.5secで移動する速度.
                float maxDelta = (0.805f - 0.25f) / heartGageAnimDuration * Time.deltaTime;
                currentHeartFill = Mathf.MoveTowards(currentHeartFill, targetHeartFill, maxDelta);
                heartGage.fillAmount = currentHeartFill;
            }
        }

        public void SetSkillGauge(float percent)
        {
            int num = (int)percent;
            breathPointPercent.text = num.ToString();
        }

        public void SetHealPointCount(int num)
        {
            int _count=0;
            //表示非表示で対応。
            while(_count < healPoint.Count)
            {
                healPoint[_count].enabled = _count < num;
                _count++;
            }
            //ここ、残り回数次第でUI表示を変えるのか
        }

        public void SetDrainGages(float percent)
        {
            for (int i = 0; i < drainUIGages.Count; i++)
            {
                drainUIGages[i].fillAmount = 0;
            }

            int intp = Mathf.FloorToInt(percent);
            float frac = percent - intp;

            if (intp < 0)
                return;
            else if (intp >= drainUIGages.Count)
            {
                intp = drainUIGages.Count - 1;
                frac = 0;
            }

                drainUIGages[intp].fillAmount = frac;

            for (int i = intp - 1; i >= 0; --i)
            {
                drainUIGages[i].fillAmount = 1;
            }

        }

        public void SetDrainUIGenerat(int num)
        {
            drainUIGages.Add(drainUI.Gage);
            // + 1個作るので <= 条件にしています
            for (int i = 0; i <= num; i++)
            {
                RectTransform ui = Instantiate(drainUI.Back.gameObject,drainUI.Parent.transform).GetComponent<RectTransform>();
                ui.localPosition = new Vector2( (i + 1) * 45f,0);
                drainUIGages.Add(ui.transform.GetChild(0).GetComponent<Image>());

            }
            SetDrainGages(0);
            drainUI.MaxLine.localPosition = new Vector2(((float)num * 45f) + 22.5f, 0);
        }

        // ---- 演出用alpha制御 ----

        // ステータスUI全体のalpha設定.
        public void SetStatusUIAlpha(float alpha)
        {
            if (statusUI != null) statusUI.alpha = alpha;
        }

        // 勝利UIのalpha設定.
        public void SetWinAlpha(float alpha)
        {
            if (win != null) win.alpha = alpha;
        }

        // 敗北UIのalpha設定.
        public void SetLoseAlpha(float alpha)
        {
            if (lose != null) lose.alpha = alpha;
        }

    }

    [System.Serializable]
    public class DrainUI
    {
        [SerializeField]
        GameObject parent;
        public GameObject Parent => parent;

        [SerializeField]
        Image back;
        public Image Back => back;

        [SerializeField]
        Image gage;
        public Image Gage => gage;

        [SerializeField]
        RectTransform maxLine;
        public RectTransform MaxLine => maxLine;
    }
}