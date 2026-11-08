using UnityEngine;

/// <summary>
/// Настройка матрицы столкновений слоёв при запуске игры.
/// Слой Collectable (монеты, зоны бонусов) взаимодействует только с игроком —
/// физике не нужно проверять пары «монета — препятствие», «монета — бочка» и т. д.
/// Слои создаются сборщиком сцены (Tools → Portfolio Demo).
/// </summary>
public static class PhysicsLayers
{
    public const string PlayerLayerName = "Player";
    public const string CollectableLayerName = "Collectable";

    public static int Player => LayerMask.NameToLayer(PlayerLayerName);
    public static int Collectable => LayerMask.NameToLayer(CollectableLayerName);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Configure()
    {
        int player = Player;
        int collectable = Collectable;
        if (player < 0 || collectable < 0)
            return;

        for (int layer = 0; layer < 32; layer++)
            Physics.IgnoreLayerCollision(collectable, layer, layer != player);
    }
}
