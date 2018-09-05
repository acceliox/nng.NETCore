using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace nng.Tests
{
    static class Util
    {
        public static string UrlRandomIpc() => "ipc://" + Guid.NewGuid().ToString();

        public static async Task AssertWait(int timeoutMs, params Task[] tasks)
        {
            var timeout = Task.Delay(timeoutMs);
            Assert.NotEqual(timeout, await Task.WhenAny(timeout, Task.WhenAll(tasks)));
        }
    }
}