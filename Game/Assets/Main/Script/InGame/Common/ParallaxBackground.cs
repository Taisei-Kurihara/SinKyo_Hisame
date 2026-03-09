using UnityEngine;

namespace Common
{
    /// <summary>
    /// 一枚絵の背景をカメラ移動に対して視差スクロールさせるコンポーネント。
    /// parallaxFactor = 0 で完全固定、1 でカメラと同速。
    /// </summary>
    public class ParallaxBackground : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)]
        private float parallaxFactorX = 0.3f;

        [SerializeField, Range(0f, 1f)]
        private float parallaxFactorY = 0.15f;

        private Transform cam;
        private Vector3 startPos;
        private Vector3 camStartPos;

        void Start()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                Debug.LogWarning("[ParallaxBackground] Main Camera が見つかりません。");
                enabled = false;
                return;
            }

            cam = mainCam.transform;
            startPos = transform.position;
            camStartPos = cam.position;
        }

        void LateUpdate()
        {
            Vector3 cameraDelta = cam.position - camStartPos;

            Vector3 pos = transform.position;
            pos.x = startPos.x + cameraDelta.x * parallaxFactorX;
            pos.y = startPos.y + cameraDelta.y * parallaxFactorY;
            transform.position = pos;
        }
    }
}
