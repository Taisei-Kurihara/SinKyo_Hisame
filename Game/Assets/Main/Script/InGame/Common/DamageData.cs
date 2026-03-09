namespace InGame.Common
{
    /// <summary>
    /// ダメージ処理で受け渡されるデータクラス.
    /// </summary>
    public class DamageData
    {
        /// <summary>
        /// ダメージ量.
        /// </summary>
        public int Damage { get; set; }

        /// <summary>
        /// Powerlevel（どちらの処理を優先するかの値）.
        /// 0 ~ 100 の範囲.
        /// </summary>
        public int Powerlevel { get; set; }

        /// <summary>
        /// 吹き飛ばしの力.
        /// </summary>
        public float KnockbackForce { get; set; }

        /// <summary>
        /// 吹き飛ばし方向（1=右、-1=左）.
        /// </summary>
        public float KnockbackDirectionX { get; set; }

        /// <summary>
        /// コンストラクタ.
        /// </summary>
        /// <param name="damage">ダメージ量.</param>
        /// <param name="powerlevel">Powerlevel（0-100）.</param>
        /// <param name="knockbackForce">吹き飛ばしの力.</param>
        /// <param name="knockbackDirectionX">吹き飛ばし方向（1=右、-1=左）.</param>
        public DamageData(int damage, int powerlevel, float knockbackForce = 3f, float knockbackDirectionX = 1f)
        {
            Damage = damage;
            Powerlevel = powerlevel;
            KnockbackForce = knockbackForce;
            KnockbackDirectionX = knockbackDirectionX;
        }
    }

    /// <summary>
    /// Powerlevel定数.
    /// </summary>
    public static class PowerlevelConst
    {
        // プレイヤー.
        public const int PlayerParry = 60;
        public const int PlayerGuard = 75;

        // エネミー攻撃.
        public const int EnemyMeleeAttack = 50;
        public const int EnemyByt = 70;
        public const int EnemyHowling = 80;
        public const int EnemyRush = 80;
    }
}
