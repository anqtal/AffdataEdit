using System.Collections.Generic;
using UnityEngine;

namespace Arcade.Gameplay
{
    // Only visible notes own scene objects. Bound the idle cache after a dense passage.
    internal sealed class ArcNoteVisualPool
    {
        private const int MaxIdleCount = 256;
        private readonly Stack<GameObject> idle = new Stack<GameObject>();

        internal GameObject Rent(GameObject prefab, Transform parent)
        {
            GameObject instance = null;
            while (idle.Count > 0 && !instance) instance = idle.Pop();
            if (!instance) instance = Object.Instantiate(prefab, parent);
            else
            {
                instance.transform.SetParent(parent, false);
                instance.SetActive(true);
            }
            return instance;
        }

        internal void Return(GameObject instance)
        {
            instance.SetActive(false);
            if (idle.Count < MaxIdleCount) idle.Push(instance);
            else Object.Destroy(instance);
        }

        internal void Clear()
        {
            while (idle.Count > 0)
            {
                var instance = idle.Pop();
                if (instance) Object.Destroy(instance);
            }
        }
    }
}
