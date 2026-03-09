using System;
using R3.Triggers;
using UnityEngine;
using R3;

namespace InGame.Player
{
    /// <summary>
    /// 汎用センサー系判定オブジェクト
    /// </summary>
    public class PlayerAttach : MonoBehaviour
    {
        [SerializeField]
        private SensorObje GroundSensor;
        [Header("左側の壁センサー")]
        [SerializeField]
        private SensorObje WallSensorRightDown;
        [SerializeField]
        private SensorObje WallSensorRightUp;
        [Header("右側の壁センサー")]        
        [SerializeField]
        private SensorObje WallSensorLeftDown;
        [SerializeField]
        private SensorObje WallSensorLeftUp;

        private void Awake()
        {
            GroundSensor.OnSensor();
            WallSensorRightDown.OnSensor();
            WallSensorRightUp.OnSensor();
            WallSensorLeftDown.OnSensor();
            WallSensorLeftUp.OnSensor();
        }

        //アクセッサメソッド
        /// <summary>
        /// 地面センサー
        /// </summary>
        /// <returns></returns>
        public bool GetGroundSensor(){return GroundSensor.Sensor;}
        // 右側の壁センサー
        public bool GetWallSensorRightDown(){return WallSensorRightDown.Sensor;}
        public bool GetWallSensorRightUp(){return WallSensorRightUp.Sensor;}
        // 左側の壁センサー
        public bool GetWallSensorLeftDown(){return WallSensorLeftDown.Sensor;}
        public bool GetWallSensorLeftUp(){return WallSensorLeftUp.Sensor;}

        public void OnDestroy()
        {
            GroundSensor.Release();
            WallSensorRightDown.Release();
            WallSensorRightUp.Release();
            WallSensorLeftDown.Release();
            WallSensorLeftUp.Release();
        }
    }

    /// <summary>
    /// センサーの設定
    /// </summary>
    [Serializable]
    public class SensorObje
    {
        [SerializeField]
        private GameObject SensorPoint;
        private CompositeDisposable disposables = new CompositeDisposable();

        public bool Sensor => SensorNum > 0;
        /// <summary>
        /// センサーが反応している時1以上になっている。
        /// </summary>
        public int SensorNum { get; private set; } = 0;
        public void OnSensor() 
        {
            disposables.Clear();
            disposables.Add(SensorPoint.OnTriggerEnter2DAsObservable()
                .Subscribe(_ =>
                {
                    SensorNum += 1;
                }).AddTo(SensorPoint));
            disposables.Add(SensorPoint.OnTriggerExit2DAsObservable()
            .Where(_ => SensorNum > 0)//0以上の時しかできないようにフィルタ
                .Subscribe(_ =>
            {
                SensorNum--;
            }).AddTo(SensorPoint));
        }
        public void Release()
        {
            disposables.Dispose();
        }
    }
}