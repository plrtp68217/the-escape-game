using System.Collections.Generic;
using Godot;

namespace EscapeGame.Inventory;

/// <summary>
/// Утилиты для оценки видимого размера 3D-моделей предметов и приведения их к
/// нужному размеру. Размер считаем по AABB видимых мешей в МИРОВЫХ координатах —
/// это учитывает вложенные трансформы GLB (иначе размер вычисляется неверно и
/// предмет получается гигантским или исчезающе мелким).
/// </summary>
public static class ModelBounds
{
    // Объединённый AABB всех видимых мешей узла в мировых координатах. Узел должен
    // быть в дереве (нужны валидные GlobalTransform).
    public static bool TryComputeWorldAabb(Node3D root, out Aabb result)
    {
        result = default;
        bool any = false;

        foreach (VisualInstance3D visual in FindVisuals(root))
        {
            Aabb worldBounds = visual.GlobalTransform * visual.GetAabb();
            result = any ? result.Merge(worldBounds) : worldBounds;
            any = true;
        }

        return any;
    }

    // Масштабирует узел так, чтобы его наибольший видимый размер стал ≈ targetSize.
    public static void FitVisibleSize(Node3D root, float targetSize)
    {
        if (!TryComputeWorldAabb(root, out Aabb world) || world.Size.LengthSquared() <= 0f)
        {
            return;
        }

        float maxDim = world.Size[(int)world.Size.MaxAxisIndex()];
        float scale = Mathf.Clamp(targetSize / maxDim, 0.0001f, 1000f);
        root.Scale *= scale;
    }

    // Увеличивает узел, только если он мельче minSize (нормальные предметы не
    // трогаем). Спасает крохотные модели вроде пилюли, которые иначе не видно.
    public static void EnsureMinVisibleSize(Node3D root, float minSize)
    {
        if (!TryComputeWorldAabb(root, out Aabb world) || world.Size.LengthSquared() <= 0f)
        {
            return;
        }

        float maxDim = world.Size[(int)world.Size.MaxAxisIndex()];
        if (maxDim >= minSize)
        {
            return;
        }

        root.Scale *= Mathf.Clamp(minSize / maxDim, 1f, 1000f);
    }

    public static IEnumerable<VisualInstance3D> FindVisuals(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is VisualInstance3D visual)
            {
                yield return visual;
            }

            foreach (VisualInstance3D nested in FindVisuals(child))
            {
                yield return nested;
            }
        }
    }
}
