using UnityEngine;

namespace InGame.Player
{
    /// <summary>
    /// 初期基準の数値
    /// </summary>
    public class PlayerStatusInitModel
    {
        public float initBreachingPoint { get; private set; } = 100f;
        public float speed { get; private set; } = 7f;

        public float healNum { get; private set; } = 3;
        public float healPower { get; private set; } = 10;
    }
}