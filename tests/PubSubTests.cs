using nng.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace nng.Tests
{
    using static nng.Native.Msg.UnsafeNativeMethods;
    using static nng.Native.Socket.UnsafeNativeMethods;
    using static nng.Tests.Util;

    public class PubSubTests
    {
        TestFactory factory = new TestFactory();

        [Fact]
        public async Task BasicPubSub()
        {
            var url = UrlRandomIpc();
            var pub = PubSocket<NngMessage>.Create(url);
            await Task.Delay(100);
            var sub = SubSocket<NngMessage>.Create(url);
            var topic = System.Text.Encoding.ASCII.GetBytes("topic");
            Assert.True(sub.Subscribe(topic));
            var ret = nng_msg_alloc(out var msg, 0);
            ret = nng_msg_append(msg, topic);
            ret = nng_sendmsg(pub.NngSocket, msg, 0);
            ret = nng_recvmsg(sub.NngSocket, out var recv, 0);
        }

        [Fact]
        public async Task PubSub()
        {
            var url = UrlRandomIpc();
            var barrier = new AsyncBarrier(2);
            var pub = Task.Run(async () => {
                var pubSocket = factory.CreatePublisher(url);
                await barrier.SignalAndWait();
                await Task.Delay(200);
                Assert.True(await pubSocket.Send(factory.CreateMsg()));
                //await Task.Delay(100);
            });
            var sub = Task.Run(async () => {
                await barrier.SignalAndWait();
                var subSocket = factory.CreateSubscriber(url);
                await subSocket.Receive(CancellationToken.None);
            });
            
            await AssertWait(1000, pub, sub);
        }

        [Fact]
        public async Task BrokerTest()
        {
            await PubSubBrokerAsync(1, 1, 1);
        }

        async Task PubSubBrokerAsync(int numPublishers, int numSubscribers, int numMessagesPerSender, int msTimeout = 1000)
        {
            // In pub/sub pattern, each message is sent to every receiver
            int numTotalMessages = numPublishers * numSubscribers * numMessagesPerSender;
            var counter = new AsyncCountdownEvent(numTotalMessages);
            var cts = new CancellationTokenSource();

            var broker = new Broker(new PubSubBrokerImpl(factory));
            var tasks = await broker.RunAsync(numPublishers, numSubscribers, numMessagesPerSender, counter, cts.Token);

            await AssertWait(msTimeout, counter.WaitAsync());
            await CancelAndWait(cts, tasks.ToArray());
        }
    }

    
    class PubSubBrokerImpl : IBrokerImpl<NngMessage>
    {
        public TestFactory Factory { get; private set; }

        public PubSubBrokerImpl(TestFactory factory)
        {
            Factory = factory;
        }

        public IReceiveAsyncContext<NngMessage> CreateInSocket(string url)
        {
            return Factory.CreatePuller(url, true);
        }
        public ISendAsyncContext<NngMessage> CreateOutSocket(string url)
        {
            return Factory.CreatePublisher(url);
        }
        public IReceiveAsyncContext<NngMessage> CreateClient(string url)
        {
            return Factory.CreateSubscriber(url);
        }
    }
}
