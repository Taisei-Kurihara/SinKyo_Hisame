using Unity.VisualScripting;
using UnityEngine;


namespace Common
{
    public class SingletonMonoBase<T> : MonoBehaviour where T : SingletonMonoBase<T>
    {
        protected static T instance;

        public static T Instance(bool dontDestroy = true)
        {
            if (instance == null)
            {
                var gameObject = new GameObject(typeof(T).Name);
                instance = gameObject.AddComponent<T>();
                if (dontDestroy == true)
                {
                    DontDestroyOnLoad(gameObject);
                }
            }
            return instance;
        }
    }
}
