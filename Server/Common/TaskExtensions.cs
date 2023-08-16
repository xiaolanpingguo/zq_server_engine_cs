using System.Threading.Tasks;

namespace ZQ
{
    public static class TaskExtensions
    {
        public static async void FireAndForget(this Task task)
        {
            await task;
        }
        public static async void FireAndForget<T>(this Task<T> task)
        {
            await task;
        }
    }
}
