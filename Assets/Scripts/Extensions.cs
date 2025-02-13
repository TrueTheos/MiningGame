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
            throw new InvalidOperationException("Cannot retrieve a random element from an empty or null list.");
        }

        int index = _random.Next(list.Count);
        return list[index];
    }

    public static int Random(this Vector2Int vector)
    {
        return UnityEngine.Random.Range(vector.x, vector.y + 1);
    }

    public static IEnumerable<Vector2Int> GetNeighbors(this Vector2Int tile)
    {
        yield return tile + Vector2Int.up;
        yield return tile + Vector2Int.down;
        yield return tile + Vector2Int.left;
        yield return tile + Vector2Int.right;
    }
}