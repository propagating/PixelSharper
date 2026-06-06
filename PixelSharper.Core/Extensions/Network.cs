using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace PixelSharper.Core.Extensions.Net;

// Port of olcPGEX_Network (olc::net) — the javidx9 message-based TCP framework. ASIO's async
// io_context maps to C# async tasks on the thread pool; the validation handshake, header/body
// framing, and thread-safe message queues are preserved. T is a user-defined message-id enum.

// Sent at the start of every message: the id + body byte count. Wire format is a fixed 8 bytes
// (int32 id + uint32 size) regardless of the enum's underlying type.
public struct MessageHeader<T> where T : struct, Enum
{
    public T Id;
    public uint Size;
}

// A header + a variable-length body of raw bytes. Push/Pop (de)serialise POD values LIFO.
public class Message<T> where T : struct, Enum
{
    public MessageHeader<T> Header;
    public readonly List<byte> Body = new();

    public int Size => Body.Count;

    public void Push<TData>(TData data) where TData : unmanaged
    {
        var bytes = new byte[Unsafe.SizeOf<TData>()];
        MemoryMarshal.Write(bytes, ref data);
        Body.AddRange(bytes);
        Header.Size = (uint)Body.Count;
    }

    public TData Pop<TData>() where TData : unmanaged
    {
        var size = Unsafe.SizeOf<TData>();
        var i = Body.Count - size;
        var data = MemoryMarshal.Read<TData>(CollectionsMarshal.AsSpan(Body).Slice(i, size));
        Body.RemoveRange(i, size);
        Header.Size = (uint)Body.Count;
        return data;
    }

    public override string ToString() => $"ID:{Convert.ToInt32(Header.Id)} Size:{Header.Size}";
}

// A message tagged with the connection it came from (the client on a server; null on a client).
public class OwnedMessage<T> where T : struct, Enum
{
    public Connection<T> Remote;
    public Message<T> Msg;
}

// A thread-safe double-ended queue with a blocking Wait().
public class TsQueue<T>
{
    private readonly LinkedList<T> _deque = new();
    private readonly object _lock = new();

    public T Front() { lock (_lock) return _deque.First.Value; }
    public T Back() { lock (_lock) return _deque.Last.Value; }

    public T PopFront() { lock (_lock) { var v = _deque.First.Value; _deque.RemoveFirst(); return v; } }
    public T PopBack() { lock (_lock) { var v = _deque.Last.Value; _deque.RemoveLast(); return v; } }

    public void PushBack(T item) { lock (_lock) { _deque.AddLast(item); Monitor.Pulse(_lock); } }
    public void PushFront(T item) { lock (_lock) { _deque.AddFirst(item); Monitor.Pulse(_lock); } }

    public bool Empty() { lock (_lock) return _deque.Count == 0; }
    public int Count() { lock (_lock) return _deque.Count; }
    public void Clear() { lock (_lock) _deque.Clear(); }

    public void Wait() { lock (_lock) { while (_deque.Count == 0) Monitor.Wait(_lock); } }
}

// A single TCP connection (one-to-one). Owns its socket, runs async read/write loops, and performs
// the server<->client validation handshake before any message traffic.
public class Connection<T> where T : struct, Enum
{
    public enum Owner { Server, Client }

    private readonly Socket _socket;
    private NetworkStream _stream;
    private readonly Owner _ownerType;
    private readonly TsQueue<OwnedMessage<T>> _qMessagesIn;
    private readonly TsQueue<Message<T>> _qMessagesOut = new();
    private readonly object _writeLock = new();
    private bool _writing;
    private bool _established; // user messages only flow once the handshake completes

    private ulong _handshakeOut, _handshakeIn, _handshakeCheck;
    private uint _id;
    private ServerInterface<T> _server;

    public Connection(Owner owner, Socket socket, TsQueue<OwnedMessage<T>> qIn, ulong handshakeSeed)
    {
        _ownerType = owner;
        _socket = socket;
        _qMessagesIn = qIn;
        if (owner == Owner.Server)
        {
            _handshakeOut = handshakeSeed;
            _handshakeCheck = Scramble(_handshakeOut);
        }
    }

    public uint GetID() => _id;
    public bool IsConnected() => _socket is { Connected: true };

    public void ConnectToClient(ServerInterface<T> server, uint uid = 0)
    {
        if (_ownerType != Owner.Server || !_socket.Connected) return;
        _id = uid;
        _server = server;
        _stream = new NetworkStream(_socket);
        _ = RunServerSideAsync();
    }

    public void ConnectToServer()
    {
        if (_ownerType != Owner.Client) return;
        _stream = new NetworkStream(_socket);
        _ = RunClientSideAsync();
    }

    public void Disconnect()
    {
        if (IsConnected()) try { _socket.Close(); } catch { /* already closing */ }
    }

    public void Send(Message<T> msg)
    {
        _qMessagesOut.PushBack(msg);
        TryStartWriter();
    }

    // Starts the single writer task, but only after the handshake — so a user Send can't race the
    // validation writes on the stream (asio serialised these; here we gate explicitly).
    private void TryStartWriter()
    {
        lock (_writeLock)
        {
            if (_established && !_writing && !_qMessagesOut.Empty())
            {
                _writing = true;
                _ = WriteLoopAsync();
            }
        }
    }

    // Server: send challenge, await the client's scrambled response, validate, then read messages.
    private async Task RunServerSideAsync()
    {
        try
        {
            await _stream.WriteAsync(BitConverter.GetBytes(_handshakeOut));
            var buf = new byte[8];
            await _stream.ReadExactlyAsync(buf);
            _handshakeIn = BitConverter.ToUInt64(buf, 0);
            if (_handshakeIn == _handshakeCheck)
            {
                _server?.OnClientValidated(this);
                _established = true;
                TryStartWriter();
                await ReadLoopAsync();
            }
            else _socket.Close();
        }
        catch { _socket.Close(); }
    }

    // Client: await the challenge, scramble + return it, then read messages.
    private async Task RunClientSideAsync()
    {
        try
        {
            var buf = new byte[8];
            await _stream.ReadExactlyAsync(buf);
            _handshakeIn = BitConverter.ToUInt64(buf, 0);
            _handshakeOut = Scramble(_handshakeIn);
            await _stream.WriteAsync(BitConverter.GetBytes(_handshakeOut));
            _established = true;
            TryStartWriter();
            await ReadLoopAsync();
        }
        catch { _socket.Close(); }
    }

    private async Task ReadLoopAsync()
    {
        try
        {
            var header = new byte[8];
            while (_socket.Connected)
            {
                await _stream.ReadExactlyAsync(header);
                var id = (T)Enum.ToObject(typeof(T), BitConverter.ToInt32(header, 0));
                var size = BitConverter.ToUInt32(header, 4);
                var msg = new Message<T> { Header = new MessageHeader<T> { Id = id, Size = size } };
                if (size > 0)
                {
                    var body = new byte[size];
                    await _stream.ReadExactlyAsync(body);
                    msg.Body.AddRange(body);
                }
                _qMessagesIn.PushBack(new OwnedMessage<T> { Remote = _ownerType == Owner.Server ? this : null, Msg = msg });
            }
        }
        catch { _socket.Close(); }
    }

    private async Task WriteLoopAsync()
    {
        try
        {
            while (true)
            {
                Message<T> msg;
                lock (_writeLock)
                {
                    if (_qMessagesOut.Empty()) { _writing = false; return; }
                    msg = _qMessagesOut.PopFront();
                }

                var header = new byte[8];
                BitConverter.GetBytes(Convert.ToInt32(msg.Header.Id)).CopyTo(header, 0);
                BitConverter.GetBytes((uint)msg.Body.Count).CopyTo(header, 4);
                await _stream.WriteAsync(header);
                if (msg.Body.Count > 0) await _stream.WriteAsync(msg.Body.ToArray());
            }
        }
        catch
        {
            lock (_writeLock) _writing = false;
            _socket.Close();
        }
    }

    // The olc validation "encryption" — both sides compute it over the server's challenge.
    private static ulong Scramble(ulong input)
    {
        var o = input ^ 0xDEADBEEFC0DECAFEUL;
        o = (o & 0xF0F0F0F0F0F0F0UL) >> 4 | (o & 0x0F0F0F0F0F0F0FUL) << 4;
        return o ^ 0xC0DEFACE12345678UL;
    }
}

// Client side: connect to a server, send messages, drain the Incoming() queue.
public class ClientInterface<T> where T : struct, Enum
{
    private Connection<T> _connection;
    private readonly TsQueue<OwnedMessage<T>> _qMessagesIn = new();

    public bool Connect(string host, ushort port)
    {
        try
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(host, port);
            _connection = new Connection<T>(Connection<T>.Owner.Client, socket, _qMessagesIn, 0);
            _connection.ConnectToServer();
            return true;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Client Exception: {e.Message}");
            return false;
        }
    }

    public void Disconnect()
    {
        if (IsConnected()) _connection.Disconnect();
        _connection = null;
    }

    public bool IsConnected() => _connection?.IsConnected() ?? false;
    public void Send(Message<T> msg) { if (IsConnected()) _connection.Send(msg); }
    public TsQueue<OwnedMessage<T>> Incoming() => _qMessagesIn;
}

// Server side: subclass and override OnClientConnect/Disconnect/Message. Call Start(), then Update()
// each tick to dispatch queued messages.
public abstract class ServerInterface<T> where T : struct, Enum
{
    protected readonly TsQueue<OwnedMessage<T>> QMessagesIn = new();
    protected readonly List<Connection<T>> Connections = new();

    private readonly TcpListener _listener;
    private uint _idCounter = 10000;
    private long _handshakeSeed = DateTime.UtcNow.Ticks;
    private volatile bool _running;

    protected ServerInterface(ushort port) => _listener = new TcpListener(IPAddress.Any, port);

    public bool Start()
    {
        try
        {
            _listener.Start();
            _running = true;
            _ = AcceptLoopAsync();
            return true;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"[SERVER] Exception: {e.Message}");
            return false;
        }
    }

    public void Stop()
    {
        _running = false;
        try { _listener.Stop(); } catch { /* already stopped */ }
    }

    private async Task AcceptLoopAsync()
    {
        while (_running)
        {
            Socket socket;
            try { socket = await _listener.AcceptSocketAsync(); }
            catch { break; }

            var conn = new Connection<T>(Connection<T>.Owner.Server, socket, QMessagesIn,
                (ulong)Interlocked.Increment(ref _handshakeSeed));
            if (OnClientConnect(conn))
            {
                lock (Connections) Connections.Add(conn);
                conn.ConnectToClient(this, _idCounter++);
            }
        }
    }

    public void MessageClient(Connection<T> client, Message<T> msg)
    {
        if (client != null && client.IsConnected())
        {
            client.Send(msg);
        }
        else
        {
            OnClientDisconnect(client);
            lock (Connections) Connections.Remove(client);
        }
    }

    public void MessageAllClients(Message<T> msg, Connection<T> ignore = null)
    {
        var dead = false;
        lock (Connections)
        {
            foreach (var client in Connections)
            {
                if (client != null && client.IsConnected())
                {
                    if (client != ignore) client.Send(msg);
                }
                else
                {
                    OnClientDisconnect(client);
                    dead = true;
                }
            }
            if (dead) Connections.RemoveAll(c => c == null || !c.IsConnected());
        }
    }

    // Dispatch up to maxMessages queued messages (all if < 0); optionally block until one arrives.
    public void Update(int maxMessages = -1, bool wait = false)
    {
        if (wait) QMessagesIn.Wait();
        var count = 0;
        while ((maxMessages < 0 || count < maxMessages) && !QMessagesIn.Empty())
        {
            var msg = QMessagesIn.PopFront();
            OnMessage(msg.Remote, msg.Msg);
            count++;
        }
    }

    // Override points (return false from OnClientConnect to veto a connection).
    protected virtual bool OnClientConnect(Connection<T> client) => false;
    protected virtual void OnClientDisconnect(Connection<T> client) { }
    protected virtual void OnMessage(Connection<T> client, Message<T> msg) { }
    public virtual void OnClientValidated(Connection<T> client) { }
}
