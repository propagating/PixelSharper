using System.Diagnostics;
using System.Threading;
using NUnit.Framework;
using PixelSharper.Core.Extensions.Net;

namespace PixelSharperTests
{
    [TestFixture]
    public class NetworkTests
    {
        private enum MsgId : uint { Ping, Echo }

        [Test]
        public void Message_PushPop_IsLifoAndTracksSize()
        {
            var msg = new Message<MsgId> { Header = new MessageHeader<MsgId> { Id = MsgId.Ping } };
            msg.Push(42);
            msg.Push(3.5f);
            Assert.AreEqual(8, msg.Header.Size); // int + float

            Assert.AreEqual(3.5f, msg.Pop<float>(), 1e-6); // LIFO: last pushed comes out first
            Assert.AreEqual(42, msg.Pop<int>());
            Assert.AreEqual(0, msg.Header.Size);
        }

        [Test]
        public void TsQueue_DequeOperations()
        {
            var q = new TsQueue<int>();
            Assert.IsTrue(q.Empty());
            q.PushBack(1);
            q.PushBack(2);
            q.PushFront(0);
            Assert.AreEqual(3, q.Count());
            Assert.AreEqual(0, q.PopFront());
            Assert.AreEqual(2, q.PopBack());
            Assert.AreEqual(1, q.Front());
            Assert.AreEqual(1, q.Count());
        }

        private sealed class TestServer : ServerInterface<MsgId>
        {
            public volatile int Received;
            public volatile int LastPayload;
            public TestServer(ushort port) : base(port) { }
            protected override bool OnClientConnect(Connection<MsgId> client) => true;
            protected override void OnMessage(Connection<MsgId> client, Message<MsgId> msg)
            {
                LastPayload = msg.Pop<int>();
                Received++;
            }
        }

        [Test]
        public void Loopback_ClientSend_ServerReceives()
        {
            const ushort port = 61299;
            var server = new TestServer(port);
            Assert.IsTrue(server.Start());
            try
            {
                var client = new ClientInterface<MsgId>();
                Assert.IsTrue(client.Connect("127.0.0.1", port));

                var msg = new Message<MsgId> { Header = new MessageHeader<MsgId> { Id = MsgId.Ping } };
                msg.Push(12345);
                client.Send(msg); // queued; flushed once the handshake completes

                var sw = Stopwatch.StartNew();
                while (server.Received == 0 && sw.ElapsedMilliseconds < 5000)
                {
                    server.Update();
                    Thread.Sleep(10);
                }

                Assert.AreEqual(1, server.Received);
                Assert.AreEqual(12345, server.LastPayload);

                client.Disconnect();
            }
            finally
            {
                server.Stop();
            }
        }
    }
}
