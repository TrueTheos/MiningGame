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
}