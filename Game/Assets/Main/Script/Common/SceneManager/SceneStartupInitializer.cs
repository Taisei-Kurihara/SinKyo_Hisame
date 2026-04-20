using UnityEngine;
using InGame.Enemy;

namespace Common
{
    /// <summary>
    /// テスト用スタートアップクラス.
    /// シーンに配置することで、そのシーンから直接開始した場合にも
    /// 他のシーンから遷移してきた場合と同等の初期化が行われるようにする.
    ///
    /// 使用方法:
    /// 1. テストしたいシーンの適当なGameObjectにこのコンポーネントをアタッチ.
    /// 2. Inspectorでテスト用のデフォルト値を設定（例: defaultEnemy = Wendigo）.
    /// 3. シーンを直接Play → デフォルト値が適用されて初期化が実行される.
    /// 4. 他のシーンから遷移してきた場合は何もしない（通常フローのまま）.
    ///
    /// 原理:
    /// - Awake()はRuntimeInitializeOnLoadMethod(AfterSceneLoad)より先に実行される.
    /// - 直接起動判定: SceneManagerシングルトンがDontDestroyOnLoadに未生成 = 直接起動.
    /// - 直接起動時のみPlayerPrefs等のテスト用デフォルト値を設定.
    /// - その後SceneManager.Init()が通常通りISceneInfo.Init()を呼び出す.
    /// </summary>
    public class SceneStartupInitializer : MonoBehaviour
    {
        [Header("テスト用デフォルト設定")]
        [Tooltip("直接起動時に生成するデフォルトのEnemy（Noneの場合はEnemyを生成しない）.")]
        [SerializeField] private EnemyName defaultEnemy = EnemyName.Wendigo;

        private void Awake()
        {
            // SceneManagerがDontDestroyOnLoadに存在する = 他のシーンから遷移してきた.
            // 存在しない = このシーンから直接起動した.
            var existingSceneManager = FindFirstObjectByType<SceneManager>();
            if (existingSceneManager != null)
            {
                return;
            }

            Debug.Log("[SceneStartupInitializer] 直接起動検出 → テスト用デフォルト値を設定.");
            SetupTestDefaults();
        }

        /// <summary>
        /// テスト用デフォルト値を設定.
        /// SceneManager.Init()内のISceneInfo.Init()で読み取られる.
        /// </summary>
        private void SetupTestDefaults()
        {
            // EnemyNameが未設定の場合、デフォルト値を設定.
            if (defaultEnemy != EnemyName.None && !PlayerPrefs.HasKey("EnemyName"))
            {
                PlayerPrefs.SetInt("EnemyName", (int)defaultEnemy);
                PlayerPrefs.Save();
                Debug.Log($"[SceneStartupInitializer] EnemyName設定: {defaultEnemy} ({(int)defaultEnemy})");
            }
        }
    }
}
