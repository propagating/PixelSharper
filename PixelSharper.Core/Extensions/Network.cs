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

/// <summary>Sent at the start of every message: the id + body byte count. Wire format is a fixed 8 bytes (int32 id + uint32 size) regardless of the enum's underlying type.</summary>
/// <typeparam name="T">User-defined message-id enum.</typeparam>
public struct MessageHeader<T> where T : struct, Enum
{
    /// <summary>The message-id enum value.</summary>
    public T Id;
    /// <summary>Byte count of the message body.</summary>
    public uint Size;
}

/// <summary>A header plus a variable-length body of raw bytes. Push/Pop (de)serialise POD values LIFO.</summary>
/// <typeparam name="T">User-defined message-id enum identifying the message kind.</typeparam>
/// <seealso cref="MessageHeader{T}"/>
/// <seealso cref="OwnedMessage{T}"/>
public class Message<T> where T : struct, Enum
{
    /// <summary>The message header (id + body size).</summary>
    /// <value>The <see cref="MessageHeader{T}"/> whose <see cref="MessageHeader{T}.Size"/> is kept in sync by <see cref="Push{TData}"/>/<see cref="Pop{TData}"/>.</value>
    public MessageHeader<T> Header;
    /// <summary>Raw message body bytes.</summary>
    /// <value>The mutable list of body bytes appended by <see cref="Push{TData}"/> and consumed by <see cref="Pop{TData}"/>.</value>
    public readonly List<byte> Body = new();

    /// <summary>Current body byte count.</summary>
    /// <value>The number of bytes currently in <see cref="Body"/>.</value>
    public int Size => Body.Count;

    /// <summary>Serialises a POD value onto the end of the body and updates the header size.</summary>
    /// <typeparam name="TData">An unmanaged (blittable POD) value type to serialise.</typeparam>
    /// <param name="data">The value to append to <see cref="Body"/>.</param>
    /// <seealso cref="Pop{TData}"/>
    public void Push<TData>(TData data) where TData : unmanaged
    {
        var bytes = new byte[Unsafe.SizeOf<TData>()];
        MemoryMarshal.Write(bytes, in data); // net8: MemoryMarshal.Write's value param is 'in T'
        Body.AddRange(bytes);
        Header.Size = (uint)Body.Count;
    }

    /// <summary>Reads and removes a POD value from the end of the body (LIFO) and updates the header size.</summary>
    /// <typeparam name="TData">An unmanaged (blittable POD) value type to deserialise.</typeparam>
    /// <returns>The value read from the end of <see cref="Body"/>.</returns>
    /// <seealso cref="Push{TData}"/>
    public TData Pop<TData>() where TData : unmanaged
    {
        var size = Unsafe.SizeOf<TData>();
        var i = Body.Count - size;
        var data = MemoryMarshal.Read<TData>(CollectionsMarshal.AsSpan(Body).Slice(i, size));
        Body.RemoveRange(i, size);
        Header.Size = (uint)Body.Count;
        return data;
    }

    /// <summary>Human-readable "ID:n Size:n" description.</summary>
    /// <returns>A string of the form <c>ID:n Size:n</c> describing the header.</returns>
    public override string ToString() => $"ID:{Convert.ToInt32(Header.Id)} Size:{Header.Size}";
}

/// <summary>A message tagged with the connection it came from (the client on a server; null on a client).</summary>
/// <typeparam name="T">User-defined message-id enum identifying the wrapped message's kind.</typeparam>
/// <seealso cref="Message{T}"/>
/// <seealso cref="Connection{T}"/>
public class OwnedMessage<T> where T : struct, Enum
{
    /// <summary>The originating connection (server side); null on a client.</summary>
    /// <value>The <see cref="Connection{T}"/> the message arrived from on a server, or <c>null</c> on a client.</value>
    public Connection<T>? Remote;
    /// <summary>The wrapped message.</summary>
    /// <value>The carried <see cref="Message{T}"/>.</value>
    public Message<T> Msg = null!;
}

/// <summary>A thread-safe double-ended queue with a blocking Wait().</summary>
/// <typeparam name="T">The element type stored in the queue.</typeparam>
/// <remarks>
/// <para>All operations are serialised by an internal lock; <see cref="Wait"/> blocks (via <see cref="Monitor"/>) until <see cref="PushBack"/>/<see cref="PushFront"/> signal an item.</para>
/// </remarks>
public class TsQueue<T>
{
    /// <summary>Backing doubly-linked storage.</summary>
    private readonly LinkedList<T> _deque = new();
    /// <summary>Mutex guarding all access and the Wait/Pulse condition.</summary>
    private readonly object _lock = new();

    /// <summary>Peeks the front item.</summary>
    /// <returns>The item at the front of the queue without removing it.</returns>
    public T Front() { lock (_lock) return _deque.First!.Value; }
    /// <summary>Peeks the back item.</summary>
    /// <returns>The item at the back of the queue without removing it.</returns>
    public T Back() { lock (_lock) return _deque.Last!.Value; }

    /// <summary>Removes and returns the front item.</summary>
    /// <returns>The item removed from the front of the queue.</returns>
    public T PopFront() { lock (_lock) { var v = _deque.First!.Value; _deque.RemoveFirst(); return v; } }
    /// <summary>Removes and returns the back item.</summary>
    /// <returns>The item removed from the back of the queue.</returns>
    public T PopBack() { lock (_lock) { var v = _deque.Last!.Value; _deque.RemoveLast(); return v; } }

    /// <summary>Appends an item to the back and signals a waiter.</summary>
    /// <param name="item">The item to append to the back of the queue.</param>
    /// <seealso cref="Wait"/>
    public void PushBack(T item) { lock (_lock) { _deque.AddLast(item); Monitor.Pulse(_lock); } }
    /// <summary>Prepends an item to the front and signals a waiter.</summary>
    /// <param name="item">The item to prepend to the front of the queue.</param>
    /// <seealso cref="Wait"/>
    public void PushFront(T item) { lock (_lock) { _deque.AddFirst(item); Monitor.Pulse(_lock); } }

    /// <summary>True if the queue is empty.</summary>
    /// <returns><c>true</c> if the queue has no items; otherwise <c>false</c>.</returns>
    public bool Empty() { lock (_lock) return _deque.Count == 0; }
    /// <summary>Current item count.</summary>
    /// <returns>The number of items currently in the queue.</returns>
    public int Count() { lock (_lock) return _deque.Count; }
    /// <summary>Removes all items.</summary>
    public void Clear() { lock (_lock) _deque.Clear(); }

    /// <summary>Blocks the calling thread until the queue is non-empty.</summary>
    /// <remarks>Returns immediately if the queue already has items; otherwise waits to be signalled by <see cref="PushBack"/>/<see cref="PushFront"/>.</remarks>
    public void Wait() { lock (_lock) { while (_deque.Count == 0) Monitor.Wait(_lock); } }
}

/// <summary>A single TCP connection (one-to-one). Owns its socket, runs async read/write loops, and performs the server/client validation handshake before any message traffic.</summary>
/// <typeparam name="T">User-defined message-id enum identifying message kinds carried over this connection.</typeparam>
/// <remarks>
/// <para>ASIO's serialised <c>io_context</c> is mapped to C# <c>async</c>/<c>await</c> over <see cref="NetworkStream"/>; the read and write loops run as independent tasks on the thread pool.</para>
/// <para>The validation handshake must complete before any user message flows: the writer task is gated on an internal <c>_established</c> flag (set after validation) so a user <see cref="Send"/> cannot race the handshake writes on the shared stream. Each message is framed as a fixed 8-byte header (int32 id + uint32 size) followed by the body.</para>
/// </remarks>
/// <seealso cref="ClientInterface{T}"/>
/// <seealso cref="ServerInterface{T}"/>
public class Connection<T> where T : struct, Enum
{
    /// <summary>Which end of the connection this instance represents.</summary>
    public enum Owner {
        /// <summary>Server-owned connection (one per accepted client).</summary>
        Server,
        /// <summary>Client-owned connection to a server.</summary>
        Client }

    /// <summary>The underlying TCP socket.</summary>
    private readonly Socket _socket;
    /// <summary>Stream wrapper used for async reads/writes.</summary>
    private NetworkStream _stream = null!;
    /// <summary>Whether this is the server or client side.</summary>
    private readonly Owner _ownerType;
    /// <summary>Shared inbound queue messages are pushed onto.</summary>
    private readonly TsQueue<OwnedMessage<T>> _qMessagesIn;
    /// <summary>Outbound queue drained by the writer loop.</summary>
    private readonly TsQueue<Message<T>> _qMessagesOut = new();
    /// <summary>Guards the writer-active flag and outbound dequeue.</summary>
    private readonly object _writeLock = new();
    /// <summary>True while a writer task is running.</summary>
    private bool _writing;
    /// <summary>True once the validation handshake completes; user messages only flow afterwards.</summary>
    private bool _established;

    /// <summary>Handshake challenge/response/expected-check values.</summary>
    private ulong _handshakeOut, _handshakeIn, _handshakeCheck;
    /// <summary>Server-assigned connection id.</summary>
    private uint _id;
    /// <summary>Owning server interface (server side only).</summary>
    private ServerInterface<T> _server = null!;

    /// <summary>Creates a connection; on the server side, primes the handshake challenge from the seed.</summary>
    /// <param name="owner">Whether this end is the <see cref="Owner.Server"/> or <see cref="Owner.Client"/>.</param>
    /// <param name="socket">The connected (or accepted) TCP socket this connection owns.</param>
    /// <param name="qIn">The shared inbound queue that received messages are pushed onto.</param>
    /// <param name="handshakeSeed">Server-only challenge seed used to derive the validation handshake; ignored on the client side.</param>
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

    /// <summary>The server-assigned connection id.</summary>
    /// <returns>The connection id assigned by the server (0 until <see cref="ConnectToClient"/> runs).</returns>
    public uint GetID() => _id;
    /// <summary>True if the socket is currently connected.</summary>
    /// <returns><c>true</c> if the underlying socket exists and is connected; otherwise <c>false</c>.</returns>
    public bool IsConnected() => _socket is { Connected: true };

    /// <summary>Server side: assigns an id and starts the handshake + read loop for an accepted client.</summary>
    /// <param name="server">The owning server interface, notified via <see cref="ServerInterface{T}.OnClientValidated"/> on a successful handshake.</param>
    /// <param name="uid">The connection id to assign to this client.</param>
    /// <remarks>No-op unless this is a connected <see cref="Owner.Server"/> connection.</remarks>
    public void ConnectToClient(ServerInterface<T> server, uint uid = 0)
    {
        if (_ownerType != Owner.Server || !_socket.Connected) return;
        _id = uid;
        _server = server;
        _stream = new NetworkStream(_socket);
        _ = RunServerSideAsync();
    }

    /// <summary>Client side: starts the handshake + read loop against the server.</summary>
    /// <remarks>No-op unless this is an <see cref="Owner.Client"/> connection.</remarks>
    public void ConnectToServer()
    {
        if (_ownerType != Owner.Client) return;
        _stream = new NetworkStream(_socket);
        _ = RunClientSideAsync();
    }

    /// <summary>Closes the socket if connected.</summary>
    public void Disconnect()
    {
        if (IsConnected()) try { _socket.Close(); } catch { /* already closing */ }
    }

    /// <summary>Queues a message for sending and kicks the writer loop.</summary>
    /// <param name="msg">The message to enqueue for transmission.</param>
    /// <remarks>The message is not written until the handshake has completed; see the class remarks on write-gating.</remarks>
    public void Send(Message<T> msg)
    {
        _qMessagesOut.PushBack(msg);
        TryStartWriter();
    }

    /// <summary>Starts the single writer task, but only after the handshake — so a user Send can't race the validation writes on the stream (asio serialised these; here we gate explicitly).</summary>
    /// <remarks>
    /// <para>Starts the writer only when the connection is established, no writer is already running, and the outbound queue is non-empty. Gating on the established flag prevents a user <see cref="Send"/> from racing the handshake writes on the shared stream.</para>
    /// </remarks>
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

    /// <summary>Server: send challenge, await the client's scrambled response, validate, then read messages.</summary>
    /// <returns>A task that completes when the read loop ends or the connection is closed.</returns>
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

    /// <summary>Client: await the challenge, scramble + return it, then read messages.</summary>
    /// <returns>A task that completes when the read loop ends or the connection is closed.</returns>
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

    /// <summary>Reads framed messages (8-byte header then body) off the stream and queues them inbound until disconnect.</summary>
    /// <returns>A task that completes when the socket disconnects or a read fails.</returns>
    /// <remarks>The 8-byte header is <c>int32 id</c> followed by <c>uint32 size</c>; a body of <c>size</c> bytes follows when non-zero.</remarks>
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

    /// <summary>Drains the outbound queue, writing each message's header then body, until empty.</summary>
    /// <returns>A task that completes when the outbound queue empties or a write fails.</returns>
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

    /// <summary>The olc validation "encryption" — both sides compute it over the server's challenge.</summary>
    /// <param name="input">The handshake challenge value to scramble.</param>
    /// <returns>The deterministically scrambled value; the server expects the client to return <c>Scramble(challenge)</c>.</returns>
    private static ulong Scramble(ulong input)
    {
        var o = input ^ 0xDEADBEEFC0DECAFEUL;
        o = (o & 0xF0F0F0F0F0F0F0UL) >> 4 | (o & 0x0F0F0F0F0F0F0FUL) << 4;
        return o ^ 0xC0DEFACE12345678UL;
    }
}

/// <summary>Client side: connect to a server, send messages, drain the Incoming() queue.</summary>
/// <typeparam name="T">User-defined message-id enum identifying message kinds.</typeparam>
/// <seealso cref="ServerInterface{T}"/>
/// <seealso cref="Connection{T}"/>
public class ClientInterface<T> where T : struct, Enum
{
    /// <summary>The active connection to the server, if any.</summary>
    private Connection<T>? _connection;
    /// <summary>Inbound message queue populated by the connection.</summary>
    private readonly TsQueue<OwnedMessage<T>> _qMessagesIn = new();

    /// <summary>Opens a TCP connection to the host/port and starts the handshake; returns false on failure.</summary>
    /// <param name="host">The server host name or IP address.</param>
    /// <param name="port">The server TCP port.</param>
    /// <returns><c>true</c> if the socket connected and the handshake was started; <c>false</c> if connecting threw.</returns>
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

    /// <summary>Closes the connection and clears it.</summary>
    public void Disconnect()
    {
        if (IsConnected()) _connection!.Disconnect();
        _connection = null;
    }

    /// <summary>True if a connection exists and is connected.</summary>
    /// <returns><c>true</c> if a connection exists and its socket is connected; otherwise <c>false</c>.</returns>
    public bool IsConnected() => _connection?.IsConnected() ?? false;
    /// <summary>Sends a message if connected.</summary>
    /// <param name="msg">The message to send; ignored if not connected.</param>
    public void Send(Message<T> msg) { if (IsConnected()) _connection!.Send(msg); }
    /// <summary>The inbound message queue to drain each tick.</summary>
    /// <returns>The <see cref="TsQueue{T}"/> of <see cref="OwnedMessage{T}"/> received from the server.</returns>
    public TsQueue<OwnedMessage<T>> Incoming() => _qMessagesIn;
}

/// <summary>Server side: subclass and override OnClientConnect/Disconnect/Message. Call Start(), then Update() each tick to dispatch queued messages.</summary>
/// <typeparam name="T">User-defined message-id enum identifying message kinds.</typeparam>
/// <remarks>
/// <para>The accept loop runs as a background task; accepted sockets are wrapped in a <see cref="Connection{T}"/> and offered to <see cref="OnClientConnect"/>. Inbound messages from all clients land in a shared queue dispatched by <see cref="Update"/> on the calling (game) thread.</para>
/// </remarks>
/// <seealso cref="ClientInterface{T}"/>
/// <seealso cref="Connection{T}"/>
public abstract class ServerInterface<T> where T : struct, Enum
{
    /// <summary>Shared inbound queue all client connections push onto.</summary>
    protected readonly TsQueue<OwnedMessage<T>> QMessagesIn = new();
    /// <summary>Active client connections.</summary>
    protected readonly List<Connection<T>> Connections = new();

    /// <summary>Listening socket for incoming connections.</summary>
    private readonly TcpListener _listener;
    /// <summary>Next connection id to assign.</summary>
    private uint _idCounter = 10000;
    /// <summary>Monotonic seed used to derive per-connection handshake challenges.</summary>
    private long _handshakeSeed = DateTime.UtcNow.Ticks;
    /// <summary>True while the accept loop is running.</summary>
    private volatile bool _running;

    /// <summary>Creates a server bound to the given port on all interfaces.</summary>
    /// <param name="port">The TCP port to listen on (bound to <see cref="IPAddress.Any"/>).</param>
    protected ServerInterface(ushort port) => _listener = new TcpListener(IPAddress.Any, port);

    /// <summary>Starts listening and the accept loop; returns false on failure.</summary>
    /// <returns><c>true</c> if the listener started and the accept loop launched; <c>false</c> if startup threw.</returns>
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

    /// <summary>Stops the accept loop and the listener.</summary>
    public void Stop()
    {
        _running = false;
        try { _listener.Stop(); } catch { /* already stopped */ }
    }

    /// <summary>Accepts incoming sockets, offers each to OnClientConnect, and starts validated ones.</summary>
    /// <returns>A task that completes when the server stops or the listener faults.</returns>
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

    /// <summary>Sends a message to one client, pruning it (and firing OnClientDisconnect) if dead.</summary>
    /// <param name="client">The destination connection.</param>
    /// <param name="msg">The message to send.</param>
    /// <remarks>If <paramref name="client"/> is null or disconnected, <see cref="OnClientDisconnect"/> fires and it is removed from <see cref="Connections"/>.</remarks>
    public void MessageClient(Connection<T>? client, Message<T> msg)
    {
        if (client != null && client.IsConnected())
        {
            client.Send(msg);
        }
        else
        {
            OnClientDisconnect(client!);
            lock (Connections) Connections.Remove(client!);
        }
    }

    /// <summary>Broadcasts a message to every client except an optional one, pruning dead connections.</summary>
    /// <param name="msg">The message to broadcast.</param>
    /// <param name="ignore">An optional connection to skip (e.g. the original sender); pass null to send to all.</param>
    /// <remarks>Dead connections fire <see cref="OnClientDisconnect"/> and are removed from <see cref="Connections"/> after the pass.</remarks>
    public void MessageAllClients(Message<T> msg, Connection<T>? ignore = null)
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
                    OnClientDisconnect(client!);
                    dead = true;
                }
            }
            if (dead) Connections.RemoveAll(c => c == null || !c.IsConnected());
        }
    }

    /// <summary>Dispatch up to maxMessages queued messages (all if less than 0); optionally block until one arrives.</summary>
    /// <param name="maxMessages">Maximum number of queued messages to dispatch this call; a value less than 0 dispatches all available.</param>
    /// <param name="wait">When <c>true</c>, blocks until at least one message is available before dispatching.</param>
    /// <remarks>Each dispatched message invokes <see cref="OnMessage"/> on the calling thread.</remarks>
    public void Update(int maxMessages = -1, bool wait = false)
    {
        if (wait) QMessagesIn.Wait();
        var count = 0;
        while ((maxMessages < 0 || count < maxMessages) && !QMessagesIn.Empty())
        {
            var msg = QMessagesIn.PopFront();
            OnMessage(msg.Remote!, msg.Msg);
            count++;
        }
    }

    /// <summary>Override point: return false to veto an incoming connection.</summary>
    /// <param name="client">The newly accepted (not yet validated) connection.</param>
    /// <returns><c>true</c> to accept and start the connection; <c>false</c> to veto it. The base implementation returns <c>false</c>.</returns>
    protected virtual bool OnClientConnect(Connection<T> client) => false;
    /// <summary>Override point: called when a client disconnects.</summary>
    /// <param name="client">The connection that disconnected.</param>
    protected virtual void OnClientDisconnect(Connection<T> client) { }
    /// <summary>Override point: called per dispatched message in Update.</summary>
    /// <param name="client">The connection the message arrived from.</param>
    /// <param name="msg">The received message.</param>
    /// <seealso cref="Update"/>
    protected virtual void OnMessage(Connection<T> client, Message<T> msg) { }
    /// <summary>Override point: called once a client passes the validation handshake.</summary>
    /// <param name="client">The connection that completed the validation handshake.</param>
    public virtual void OnClientValidated(Connection<T> client) { }
}
