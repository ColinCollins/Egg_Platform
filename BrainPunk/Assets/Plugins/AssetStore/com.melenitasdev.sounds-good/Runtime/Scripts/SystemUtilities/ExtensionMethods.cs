using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("SoundsGood.Application")]

namespace MelenitasDev.SoundsGood.SystemUtilities
{
    internal static class ExtensionMethods
    {
        private static Random rng = new Random();

        public static void Shuffle<T> (this Queue<T> queue)
        {
            var list = new List<T>(queue);
            queue.Clear();
            
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                (list[n], list[k]) = (list[k], list[n]);
            }

            foreach (var item in list)
            {
                queue.Enqueue(item);
            }
        }
    }
}

