using System;
using System.Collections.Generic;

public static class Extensions
{
    private static Random _random = new Random();

    public static T Random<T>(this List<T> list)
    {
        if (list == null || list.Count == 0)
        {
            throw new InvalidOperationException("Cannot retrieve a random element from an empty or null list.");
        }

        int index = _random.Next(list.Count);
        return list[index];
    }
}