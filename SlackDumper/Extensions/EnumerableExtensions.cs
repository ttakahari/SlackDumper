using System;
using System.Collections.Generic;

namespace SlackDumper.Extensions
{
    public static class EnumerableExtensions
    {
        public static string Combine<T>(this IEnumerable<T> source, string separator)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return string.Join(separator, source);
        }
    }
}
