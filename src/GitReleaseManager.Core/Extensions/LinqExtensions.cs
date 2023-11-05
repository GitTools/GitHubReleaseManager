using System;
using System.Collections.Generic;

namespace GitReleaseManager.Core.Extensions
{
    internal static class LinqExtensions
    {
        // This is the equivalent of DistinctBy which is available in .NET 6
        public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            HashSet<TKey> seenKeys = new HashSet<TKey>();
            foreach (TSource element in source)
            {
                if (seenKeys.Add(keySelector(element)))
                {
                    yield return element;
                }
            }
        }
    }
}
