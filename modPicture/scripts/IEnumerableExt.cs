using System;
using System.Collections.Generic;
using System.Linq;

namespace Iterator
{
    internal static class IEnumerableExt
    {
        public static IEnumerable<T> Intersperse<T>(this IEnumerable<T> self, T separator)
        {
            var iter = self.GetEnumerator();
            if (iter.MoveNext()) {
                var current = iter.Current;
                while (iter.MoveNext()) {
                    yield return current;
                    yield return separator;
                    current = iter.Current;
                }
                yield return current;
            }
        }

        public static IEnumerable<U> Map<T, U>(this IEnumerable<T> self, Func<T, U> f)
        {
            return self.Select(f);
        }
    }
}