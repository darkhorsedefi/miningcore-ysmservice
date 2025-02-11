using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Miningcore.Extensions
{
    public static class TaskExtensions
    {
        public static void Deconstruct<T1, T2>(this Task<(T1, T2)> task, out T1 item1, out T2 item2)
        {
            var result = task.Result;
            item1 = result.Item1;
            item2 = result.Item2;
        }

        public static void Deconstruct<T1, T2, T3, T4>(
            this Task<(T1, T2, T3, T4)> task,
            out T1 item1, out T2 item2, out T3 item3, out T4 item4)
        {
            var result = task.Result;
            item1 = result.Item1;
            item2 = result.Item2;
            item3 = result.Item3;
            item4 = result.Item4;
        }

        public static async Task<(T1, T2, T3, T4)> AsTypedTuple<T1, T2, T3, T4>(
            this Task<(T1, T2, T3, T4)> task)
        {
            return await task;
        }

        public static void Deconstruct<T1, T2, T3, T4>(
            this ValueTask<(T1, T2, T3, T4)> task,
            out T1 item1, out T2 item2, out T3 item3, out T4 item4)
        {
            var result = task.Result;
            item1 = result.Item1;
            item2 = result.Item2;
            item3 = result.Item3;
            item4 = result.Item4;
        }
    }
}