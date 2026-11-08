using UnityEngine;

/// <summary>Прогрев пула эффектов при загрузке уровня.</summary>
public class EffectPrewarmer : MonoBehaviour
{
    [System.Serializable]
    public struct Entry
    {
        public GameObject prefab;
        public int count;
    }

    [SerializeField] private Entry[] effects;

    private void Start()
    {
        if (effects == null)
            return;
        foreach (Entry e in effects)
            EffectPool.Prewarm(e.prefab, e.count);
    }
}
