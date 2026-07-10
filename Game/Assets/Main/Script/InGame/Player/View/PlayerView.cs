using InGame.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections.Generic;
using System;
using Cysharp.Threading.Tasks;
#if UNITY_EDITOR
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

        [Header("OverlapTransparency マスク制御")]
        [SerializeField] private Material overlapSharedMaterial;
        private Material overlapMaterial;

        // オーバーレイ演出用の状態（心拍数 + HP連動）.
        private int overlayHeartRate = 100;
        private float overlayHpPercent = 1f;

        // 脈動アニメーション用（C#側で管理 / current→targetを毎フレーム補間）.
        private float overlayTargetTiling = 0f;
        private float overlayCurrentTiling = 0f;
        private float overlayTargetSpeed = 4f;
        private float overlayCurrentSpeed = 4f;
        private float overlayTargetAmplitude = 0.07f;
        private float overlayCurrentAmplitude = 0.07f;
        private Color overlayTargetColor = new Color(0f, 0f, 0f, 0f);
        private Color overlayCurrentColor = new Color(0f, 0f, 0f, 0f);
        private float overlayPhase = 0f;
        private const float overlaySmoothRate = 3f;

        [Header("血管エフェクト（高心拍数時）")]
        [SerializeField] private Material bloodVesselsMaterial;

        private float bloodVesselsTargetAlpha = 0f;
        private float bloodVesselsCurrentAlpha = 0f;

        // ---- 心音オーディオ（AudioSource 2つでクロスフェード切り替え） ----
        [Header("心音オーディオ")]
        [SerializeField] private string heartbeatSlowClipAddress = "SE_Heartbeat_Slow";
        [SerializeField] private string heartbeatFastClipAddress = "SE_Heartbeat_Fast";
        private AudioSource heartbeatSourceA;
        private AudioSource heartbeatSourceB;
        private bool heartbeatUsingA = true; // 現在Aが再生中.
        private AudioClip heartbeatSlowClip;
        private AudioClip heartbeatFastClip;
        private AsyncOperationHandle<AudioClip> heartbeatSlowHandle;
        private AsyncOperationHandle<AudioClip> heartbeatFastHandle;
        private bool heartbeatClipLoaded = false;
        private bool heartbeatIsFast = false; // 現在Fastクリップを使用中.
        private const int heartbeatSwitchThreshold = 160; // Slow/Fast切り替え閾値.

        // 心音パラメータ（心拍数から算出）.
        private float heartbeatTargetVolume = 0f;
        private float heartbeatCurrentVolume = 0f;
        private const float heartbeatSmoothRate = 3f;
        // クロスフェード中フラグ.
        private bool heartbeatCrossfading = false;
        private float crossfadeProgress = 0f;
        private const float crossfadeDuration = 0.5f;

        [Header("ブラー制御（心拍数連動）")]
        [SerializeField] private List<Material> blurMaterials = new();
        [SerializeField] private float blurMaxSize = 20f;

        private float blurTargetSize = 0f;
        private float blurCurrentSize = 0f;
        private float blurTargetAlpha = 1f;
        private float blurCurrentAlpha = 1f;

        [Header("DPS表示")]
        [SerializeField] private TextMeshProUGUI dpsText;
        // DPS計算用ダメージ履歴.
        private readonly List<(float time, float damage)> damageHistory = new();
        private const float dpsHistoryMaxSeconds = 5f;

        // InGame中の最高DPS記録.
        private float maxDps1 = 0f;
        private float maxDps3 = 0f;
        private float maxDps5 = 0f;

        // DPS表示の有効状態.
        private bool isDpsActive = false;

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
        private void Start()
        {
            // DPS表示を初期状態で無効化.
            if (dpsText != null)
            {
                dpsText.gameObject.SetActive(false);
            }

            // 心音AudioSource 2つを生成.
            heartbeatSourceA = gameObject.AddComponent<AudioSource>();
            heartbeatSourceA.playOnAwake = false;
            heartbeatSourceA.loop = true;
            heartbeatSourceA.volume = 0f;

            heartbeatSourceB = gameObject.AddComponent<AudioSource>();
            heartbeatSourceB.playOnAwake = false;
            heartbeatSourceB.loop = true;
            heartbeatSourceB.volume = 0f;

            // 心音クリップをAddressablesから非同期ロード.
            LoadHeartbeatClipAsync().Forget();
        }

        private async UniTaskVoid LoadHeartbeatClipAsync()
        {
            try
            {
                // Slow/Fast 2つのクリップを並列ロード.
                heartbeatSlowHandle = Addressables.LoadAssetAsync<AudioClip>(heartbeatSlowClipAddress);
                heartbeatFastHandle = Addressables.LoadAssetAsync<AudioClip>(heartbeatFastClipAddress);

                heartbeatSlowClip = await heartbeatSlowHandle;
                heartbeatFastClip = await heartbeatFastHandle;

                bool slowOk = heartbeatSlowHandle.Status == AsyncOperationStatus.Succeeded && heartbeatSlowClip != null;
                bool fastOk = heartbeatFastHandle.Status == AsyncOperationStatus.Succeeded && heartbeatFastClip != null;

                if (slowOk && fastOk)
                {
                    heartbeatClipLoaded = true;
                    // 初期状態はSlow.
                    heartbeatIsFast = false;
                    heartbeatSourceA.clip = heartbeatSlowClip;
                    heartbeatSourceB.clip = heartbeatSlowClip;
                    Debug.Log($"[PlayerView] 心音クリップ Slow/Fast ロード完了");
                }
                else
                {
                    if (!slowOk) Debug.LogWarning($"[PlayerView] 心音クリップ '{heartbeatSlowClipAddress}' ロード失敗");
                    if (!fastOk) Debug.LogWarning($"[PlayerView] 心音クリップ '{heartbeatFastClipAddress}' ロード失敗");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlayerView] 心音クリップロード例外: {e.Message}");
            }
        }

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

            overlayHeartRate = heartRate;
            UpdateOverlayEffect();
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

            overlayHpPercent = percent;
            UpdateOverlayEffect();
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
            // デバッグ: Zキーで EnemyのHPを半分にする.
            if (Input.GetKeyDown(KeyCode.Z))
            {
                var enemyPresenter = UnityEngine.Object.FindFirstObjectByType<EnemyPresenter_abstract>();
                if (enemyPresenter != null && enemyPresenter.Status != null)
                {
                    float halfHp = enemyPresenter.Status.hp.Value * 0.5f;
                    enemyPresenter.Status.OnDamaged(halfHp).Forget();
                    Debug.Log($"[PlayerView] デバッグ: Enemy HP半分 ({enemyPresenter.Status.hp.Value} → {enemyPresenter.Status.hp.Value - halfHp})");
                }
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

            // オーバーレイ脈動アニメーション
            // （全パラメータを滑らかに補間）.
            {
                var mat = GetOverlapMaterial();
                if (mat != null)
                {
                    float s = 1f - Mathf.Exp(-overlaySmoothRate * Time.deltaTime);
                    overlayCurrentTiling = Mathf.Lerp(overlayCurrentTiling, overlayTargetTiling, s);
                    overlayCurrentSpeed = Mathf.Lerp(overlayCurrentSpeed, overlayTargetSpeed, s);
                    overlayCurrentAmplitude = Mathf.Lerp(overlayCurrentAmplitude, overlayTargetAmplitude, s);
                    overlayCurrentColor = Color.Lerp(overlayCurrentColor, overlayTargetColor, s);

                    // 位相を蓄積（speed変化時に位相ジャンプしない）.
                    overlayPhase += overlayCurrentSpeed * Time.deltaTime;

                    float pulse = overlayCurrentTiling *
                        (1f + overlayCurrentAmplitude * Mathf.Sin(overlayPhase));
                    mat.SetTextureScale("_MaskTex", new Vector2(pulse, pulse));
                    mat.SetColor("_Color", overlayCurrentColor);
                }
            }

            // ブラー演出（心拍数連動）を滑らかに補間・適用.
            {
                float s2 = 1f - Mathf.Exp(-overlaySmoothRate * Time.deltaTime);
                blurCurrentSize = Mathf.Lerp(blurCurrentSize, blurTargetSize, s2);
                blurCurrentAlpha = Mathf.Lerp(blurCurrentAlpha, blurTargetAlpha, s2);

                Shader.SetGlobalFloat("_BlurSize", blurCurrentSize);
                Shader.SetGlobalFloat("_BlurAlpha", blurCurrentAlpha);
            }

            // 血管エフェクト _FadeAlpha を滑らかに補間・適用.
            if (bloodVesselsMaterial != null)
            {
                float s3 = 1f - Mathf.Exp(-overlaySmoothRate * Time.deltaTime);
                bloodVesselsCurrentAlpha = Mathf.Lerp(bloodVesselsCurrentAlpha, bloodVesselsTargetAlpha, s3);
                // ターゲットが0のとき、補間の残留値を完全にカット.
                if (bloodVesselsTargetAlpha <= 0f && bloodVesselsCurrentAlpha < 0.05f)
                    bloodVesselsCurrentAlpha = 0f;
                bloodVesselsMaterial.SetFloat("_FadeAlpha", bloodVesselsCurrentAlpha);
            }

            // ---- 心音オーディオ更新 ----
            UpdateHeartbeatAudio();

            // Bキーで DPS表示の有効/無効を切り替え.
            if (Input.GetKeyDown(KeyCode.B))
            {
                isDpsActive = !isDpsActive;
                if (dpsText != null)
                {
                    dpsText.gameObject.SetActive(isDpsActive);
                }
            }

            // DPS表示更新.
            UpdateDPS();
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

        // ---- OverlapTransparency マスク制御 ----

        /// <summary>ランタイムでRendererを指定（per-instanceマテリアル取得）.</summary>
        public void SetOverlapTarget(Renderer renderer)
        {
            if (renderer != null)
            {
                overlapMaterial = renderer.material;
            }
        }

        private Material GetOverlapMaterial()
        {
            if (overlapMaterial == null && overlapSharedMaterial != null)
            {
                overlapMaterial = overlapSharedMaterial;
            }
            return overlapMaterial;
        }

        /// <summary>マスクテクスチャのTilingを設定.</summary>
        public void SetMaskTiling(Vector2 tiling)
        {
            var mat = GetOverlapMaterial();
            if (mat != null) mat.SetTextureScale("_MaskTex", tiling);
        }

        /// <summary>マスクテクスチャのOffsetを設定.</summary>
        public void SetMaskOffset(Vector2 offset)
        {
            var mat = GetOverlapMaterial();
            if (mat != null) mat.SetTextureOffset("_MaskTex", offset);
        }

        /// <summary>マスクテクスチャを差し替え.</summary>
        public void SetMaskTexture(Texture texture)
        {
            var mat = GetOverlapMaterial();
            if (mat != null) mat.SetTexture("_MaskTex", texture);
        }

        // ---- 心拍数 + HP連動オーバーレイ演出 ----

        /// <summary>
        /// 心拍数とHP状態からオーバーレイ演出パラメータを算出・適用.
        /// </summary>
        private void UpdateOverlayEffect()
        {
            var mat = GetOverlapMaterial();
            if (mat == null) return;

            float hrTiling, hrSpeed, hrAmplitude;
            Color hrColor;

            if (overlayHeartRate < 100)
            {
                // ---- HR < 100: 黒オーバーレイ ----
                hrColor = new Color(0f, 0f, 0f, 1f);
                // HR 100→30 で tiling 0.7→1.0.
                float tTile = Mathf.InverseLerp(100f, 30f, overlayHeartRate);
                hrTiling = Mathf.Lerp(0.7f, 1.0f, tTile);
                hrAmplitude = 0.4f;
                // HR 30→70 で speed 2→5.
                float t = Mathf.InverseLerp(30f, 70f, overlayHeartRate);
                hrSpeed = Mathf.Lerp(2f, 4f, t);
            }
            else
            {
                // ---- HR >= 100: 赤オーバーレイ ----
                hrColor = new Color(170f / 255f, 0f, 0f, 170f / 255f);
                // HR 100→180 で tiling 0→0.7, speed 4→15.
                float t = Mathf.InverseLerp(100f, 180f, overlayHeartRate);
                hrTiling = Mathf.Lerp(0f, 0.7f, t);
                hrSpeed = Mathf.Lerp(4f, 15f, t);
                hrAmplitude = 0.14f;
            }

            // ---- 低HP演出（HP < 0.33）: 心拍数パラメータと平均 ----
            if (overlayHpPercent < 0.33f)
            {
                Color hpColor = new Color(130f / 255f, 0f, 0f, 170f / 255f);
                float hpTiling = 1.5f;
                float hpAmplitude = 0.35f;
                float hpSpeed = 2f;

                hrColor = (hrColor + hpColor) * 0.5f;
                hrTiling = (hrTiling + hpTiling) * 0.5f;
                hrAmplitude = (hrAmplitude + hpAmplitude) * 0.5f;
                hrSpeed = (hrSpeed + hpSpeed) * 0.5f;
            }

            // ターゲット値を設定（Update()で毎フレーム滑らかに補間適用）.
            overlayTargetColor = hrColor;
            overlayTargetTiling = hrTiling;
            overlayTargetSpeed = hrSpeed;
            overlayTargetAmplitude = hrAmplitude;

            // ---- 血管エフェクト（HR 170→190 で alpha 0→0.999, 170未満は明示的に0）----
            bloodVesselsTargetAlpha = overlayHeartRate < 170
                ? 0f
                : Mathf.InverseLerp(170f, 190f, overlayHeartRate) * 0.999f;

            // ---- 心音パラメータ算出 ----
            // volume: HR 100から離れるほど大きく（HR 0 or 200 で最大）.
            // clip: HR < 160 → Slow, HR >= 160 → Fast（クロスフェードで切り替え）.
            {
                float distFrom100 = Mathf.Abs(overlayHeartRate - 100f) / 100f;
                heartbeatTargetVolume = Mathf.Clamp01(distFrom100);
            }

            // ---- BGM音量（HR 90-110: 1.0, HR 50以下/150以上: 0.3）----
            {
                float bgmMul = 1f;
                if (overlayHeartRate < 90)
                {
                    // HR 90→50 で 1.0→0.3.
                    bgmMul = Mathf.Lerp(0.3f, 1f, Mathf.InverseLerp(50f, 90f, overlayHeartRate));
                }
                else if (overlayHeartRate > 110)
                {
                    // HR 110→150 で 1.0→0.3.
                    bgmMul = Mathf.Lerp(1f, 0.3f, Mathf.InverseLerp(110f, 150f, overlayHeartRate));
                }
                Setting.AudioManager.Instance(false)?.SetBgmHeartRateMultiplier(bgmMul);
            }

            // ---- ブラー制御（HR < 100 で 0→max, alpha 1→0.7）----
            // ---- カメラズーム（HR < 100 でプレイヤー中心にズーム、端無示）----
            if (overlayHeartRate < 100)
            {
                float tBlur = Mathf.InverseLerp(100f, 30f, overlayHeartRate);
                blurTargetSize = Mathf.Lerp(0f, blurMaxSize, tBlur);
                blurTargetAlpha = Mathf.Lerp(1f, 0.7f, tBlur);

                // HR 100→30 でズーム 1.0→0.7.
                float zoomFactor = Mathf.Lerp(1f, 0.7f, tBlur);
                global::Common.CameraManager.Instance(false)?.SetHeartRateZoom(zoomFactor);
            }
            else
            {
                blurTargetSize = 0f;
                blurTargetAlpha = 1f;
                global::Common.CameraManager.Instance(false)?.SetHeartRateZoom(1f);
            }
        }

        // ---- 心音オーディオ制御 ----

        /// <summary>
        /// 心音のvolume/pitchを滑らかに補間し、pitch変更時はクロスフェードで切り替える.
        /// </summary>
        private void UpdateHeartbeatAudio()
        {
            if (!heartbeatClipLoaded) return;

            var activeSource = heartbeatUsingA ? heartbeatSourceA : heartbeatSourceB;
            var inactiveSource = heartbeatUsingA ? heartbeatSourceB : heartbeatSourceA;

            // volume補間.
            float s = 1f - Mathf.Exp(-heartbeatSmoothRate * Time.deltaTime);
            heartbeatCurrentVolume = Mathf.Lerp(heartbeatCurrentVolume, heartbeatTargetVolume, s);

            // HR 160 を閾値に Slow/Fast クリップ切り替え（クロスフェード）.
            bool shouldBeFast = overlayHeartRate >= heartbeatSwitchThreshold;
            if (shouldBeFast != heartbeatIsFast && !heartbeatCrossfading)
            {
                // クロスフェード開始: inactive側に新クリップを設定して再生開始.
                heartbeatCrossfading = true;
                crossfadeProgress = 0f;
                heartbeatIsFast = shouldBeFast;
                inactiveSource.clip = shouldBeFast ? heartbeatFastClip : heartbeatSlowClip;
                inactiveSource.volume = 0f;
                if (!inactiveSource.isPlaying)
                {
                    inactiveSource.Play();
                }
            }

            if (heartbeatCrossfading)
            {
                // クロスフェード進行.
                crossfadeProgress += Time.deltaTime / crossfadeDuration;
                float t = Mathf.Clamp01(crossfadeProgress);

                activeSource.volume = heartbeatCurrentVolume * (1f - t);
                inactiveSource.volume = heartbeatCurrentVolume * t;

                if (t >= 1f)
                {
                    // クロスフェード完了: active/inactiveを入れ替え.
                    activeSource.Stop();
                    heartbeatUsingA = !heartbeatUsingA;
                    heartbeatCrossfading = false;
                }
            }
            else
            {
                // クロスフェード中でない: 通常のvolume更新.
                activeSource.volume = heartbeatCurrentVolume;

                // volume > 0 なら再生開始.
                if (heartbeatCurrentVolume > 0.01f && !activeSource.isPlaying)
                {
                    activeSource.Play();
                }
                // volume ≈ 0 なら停止.
                else if (heartbeatCurrentVolume <= 0.01f && activeSource.isPlaying)
                {
                    activeSource.Stop();
                }
            }
        }

        /// <summary>
        /// 心音を即座に停止（死亡時等）.
        /// </summary>
        public void StopHeartbeatAudio()
        {
            heartbeatTargetVolume = 0f;
            heartbeatCurrentVolume = 0f;
            heartbeatCrossfading = false;
            if (heartbeatSourceA != null && heartbeatSourceA.isPlaying) heartbeatSourceA.Stop();
            if (heartbeatSourceB != null && heartbeatSourceB.isPlaying) heartbeatSourceB.Stop();
        }

        private void OnDestroy()
        {
            // 心音クリップのAddressablesハンドルを解放.
            if (heartbeatSlowHandle.IsValid())
                Addressables.Release(heartbeatSlowHandle);
            if (heartbeatFastHandle.IsValid())
                Addressables.Release(heartbeatFastHandle);
        }

        // ---- DPS計測 ----

        /// <summary>
        /// 与ダメージを記録（Presenterから呼び出し）.
        /// </summary>
        public void RecordDamage(float damage)
        {
            damageHistory.Add((Time.time, damage));
        }

        /// <summary>
        /// 指定秒数ウィンドウ内の合計ダメージからDPSを算出.
        /// </summary>
        private float CalculateDPS(float windowSeconds)
        {
            float cutoff = Time.time - windowSeconds;
            float total = 0f;
            for (int i = damageHistory.Count - 1; i >= 0; i--)
            {
                if (damageHistory[i].time < cutoff) break;
                total += damageHistory[i].damage;
            }
            return total / windowSeconds;
        }

        /// <summary>
        /// 古い履歴を削除し、DPS表示を更新.
        /// </summary>
        private void UpdateDPS()
        {
            if (dpsText == null || !isDpsActive) return;

            // 5秒より古い履歴を除去.
            float cutoff = Time.time - dpsHistoryMaxSeconds;
            while (damageHistory.Count > 0 && damageHistory[0].time < cutoff)
            {
                damageHistory.RemoveAt(0);
            }

            float dps1 = CalculateDPS(1f);
            float dps3 = CalculateDPS(3f);
            float dps5 = CalculateDPS(5f);

            // 最高記録を更新.
            if (dps1 > maxDps1) maxDps1 = dps1;
            if (dps3 > maxDps3) maxDps3 = dps3;
            if (dps5 > maxDps5) maxDps5 = dps5;

            dpsText.text = $"DPS_1sec:{dps1:F0}\nDPS_3sec:{dps3:F0}\nDPS_5sec:{dps5:F0}\n\nMaxDPS_1sec:{maxDps1:F0}\nMaxDPS_3sec:{maxDps3:F0}\nMaxDPS_5sec:{maxDps5:F0}";
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