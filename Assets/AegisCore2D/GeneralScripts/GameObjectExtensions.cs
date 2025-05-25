using UnityEngine;

namespace AegisCore2D.GeneralScripts
{
    public static class GameObjectExtensions
    {
        /// <summary>
        /// Gets a component of type T on the GameObject or its parents.
        /// </summary>
        public static T GetComponentInSelfOrParent<T>(this GameObject gameObject) where T : class
        {
            if (gameObject == null) return null;
            T component = gameObject.GetComponent<T>();
            if (component == null)
            {
                component = gameObject.GetComponentInParent<T>();
            }
            return component;
        }

        /// <summary>
        /// Gets a component of type T on the Component's GameObject or its parents.
        /// </summary>
        public static T GetComponentInSelfOrParent<T>(this Component component) where T : class
        {
            if (component == null) return null;
            T comp = component.GetComponent<T>();
            if (comp == null)
            {
                comp = component.GetComponentInParent<T>();
            }
            return comp;
        }
    }
}