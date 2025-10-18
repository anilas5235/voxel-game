using UnityEngine;

namespace Utils
{
    /// <summary>
    ///   <para>Base class for MonoBehaviour singletons. Only one instance will exist at runtime.
    ///  There is no instance creation logic, so you must ensure the singleton is created in the scene.</para>
    /// </summary>
    /// <remarks>Author: Niklas Borchers</remarks>
    [DefaultExecutionOrder(-100)]
    public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        /// <summary>
        /// The singleton instance of this type.
        /// </summary>
        public static T Instance { get; private set; }

        /// <summary>
        /// Ensures only one instance exists and assigns the static Instance property.
        /// </summary>
        protected virtual void Awake()
        {
            if (!Instance) Instance = gameObject.GetComponent<T>();
            else if (Instance.GetInstanceID() != GetInstanceID())
            {
                Destroy(this);
            }
        }

        /// <summary>
        /// Clears the static Instance property if this instance is destroyed.
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}