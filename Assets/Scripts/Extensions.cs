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
}