using DG.Tweening;
using System;
using System.Collections.Generic;
using UnityEngine;

public static class Extensions
{
    private static System.Random _random = new System.Random();

    public static T Random<T>(this List<T> list)
    {
        if (list == null || list.Count == 0)
        {
            return default(T);
        }

        int index = _random.Next(list.Count);
        return list[index];
    }

    public static int Random(this Vector2Int vector)
    {
        return UnityEngine.Random.Range(vector.x, vector.y + 1);
    }

    public static float Random(this Vector2 vector)
    {
        return UnityEngine.Random.Range(vector.x, vector.y);
    }

    public static Vector3 ToVector3(this Vector2Int vector, float z = 0f, float offset = 0f)
    {
        return new Vector3(vector.x + offset, vector.y + offset, z);
    }

    public static bool RandTest(this int val)
    {
        return UnityEngine.Random.Range(0, val + 1) < val;
    }

    public static IEnumerable<Vector2Int> GetNeighbors(this Vector2Int tile)
    {
        yield return tile + Vector2Int.up;
        yield return tile + Vector2Int.down;
        yield return tile + Vector2Int.left;
        yield return tile + Vector2Int.right;
    }

    public static bool InMask(this int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    /// <summary>
    /// Adds a layer to an existing LayerMask.
    /// </summary>
    /// <param name="mask">The original LayerMask</param>
    /// <param name="layer">The layer to add (as an int)</param>
    /// <returns>A new LayerMask with the added layer</returns>
    public static LayerMask AddLayer(this LayerMask mask, int layer)
    {
        return mask | (1 << layer);
    }

    /// <summary>
    /// Adds a layer to an existing LayerMask using the layer name.
    /// </summary>
    /// <param name="mask">The original LayerMask</param>
    /// <param name="layerName">The name of the layer to add</param>
    /// <returns>A new LayerMask with the added layer</returns>
    public static LayerMask AddLayer(this LayerMask mask, string layerName)
    {
        return mask | (1 << LayerMask.NameToLayer(layerName));
    }

    /// <summary>
    /// Removes a layer from an existing LayerMask.
    /// </summary>
    /// <param name="mask">The original LayerMask</param>
    /// <param name="layer">The layer to remove (as an int)</param>
    /// <returns>A new LayerMask with the layer removed</returns>
    public static LayerMask RemoveLayer(this LayerMask mask, int layer)
    {
        return mask & ~(1 << layer);
    }

    /// <summary>
    /// Removes a layer from an existing LayerMask using the layer name.
    /// </summary>
    /// <param name="mask">The original LayerMask</param>
    /// <param name="layerName">The name of the layer to remove</param>
    /// <returns>A new LayerMask with the layer removed</returns>
    public static LayerMask RemoveLayer(this LayerMask mask, string layerName)
    {
        return mask & ~(1 << LayerMask.NameToLayer(layerName));
    }

    /// <summary>
    /// Adds another LayerMask to this LayerMask.
    /// </summary>
    /// <param name="mask">The original LayerMask</param>
    /// <param name="other">The LayerMask to add</param>
    /// <returns>A new combined LayerMask</returns>
    public static LayerMask AddMask(this LayerMask mask, LayerMask other)
    {
        return mask | other;
    }

    /// <summary>
    /// Removes another LayerMask from this LayerMask.
    /// </summary>
    /// <param name="mask">The original LayerMask</param>
    /// <param name="other">The LayerMask to remove</param>
    /// <returns>A new LayerMask with the other mask removed</returns>
    public static LayerMask RemoveMask(this LayerMask mask, LayerMask other)
    {
        return mask & ~other;
    }

    public static void Blink(this SpriteRenderer spriteRend, float duration = .3f)
    {
        GameObject flashOverlay = new GameObject("FlashOverlay");
        flashOverlay.transform.SetParent(spriteRend.transform, false);
        flashOverlay.transform.localPosition = Vector3.zero;

        SpriteRenderer overlayRenderer = flashOverlay.AddComponent<SpriteRenderer>();
        overlayRenderer.sprite = spriteRend.sprite;
        overlayRenderer.material = Resources.Load("BlinkMat") as Material;
        overlayRenderer.sortingLayerID = spriteRend.sortingLayerID;
        overlayRenderer.sortingOrder = spriteRend.sortingOrder + 1;
        overlayRenderer.color = new Color(1f, 1f, 1f, 0f);

        overlayRenderer.DOFade(1f, duration / 2)
            .OnComplete(() =>
                overlayRenderer.DOFade(0f, duration / 2));
    
        GameObject.Destroy(flashOverlay, duration / 2);

    }
}