using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

public class TWebSocketClient : Thrift.Transport.TTransport
{
    private readonly ClientWebSocket ws;
    private bool disposed;

    public Uri TransportUri { get; private set; }
    public WebSocketMessageType MessageType { get; private set; }
    public ClientWebSocketOptions WebSocketOptions { get { return ws.Options; } }
    private readonly SemaphoreSlim _read_semapthore;
    private readonly SemaphoreSlim _send_semapthore;

    public TWebSocketClient(Uri transport_uri, WebSocketMessageType message_type, bool validate_certs = true) {
        _read_semapthore = new SemaphoreSlim(1);
        _send_semapthore = new SemaphoreSlim(1);
        ws = new ClientWebSocket();
        if (!validate_certs)
            ws.Options.RemoteCertificateValidationCallback = (sender, certificate, chain, errors) => true;

        disposed = false;
        TransportUri = transport_uri;
        MessageType = message_type;
    }
    public override bool IsOpen => ws.State == WebSocketState.Open;

    public override async void Close()
    {
        try {
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bye", CancellationToken.None);
        }
        catch {
            // ignored
        }
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<object>(null);
    }

    public override async Task OpenAsync(CancellationToken cancellationToken)
    {
        await ws.ConnectAsync(TransportUri, cancellationToken);
    }

    public override async ValueTask<int> ReadAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
    {
        await _read_semapthore.WaitAsync();
        try
        {
            var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer, offset, length), cancellationToken);
            // UTF8Encoding utf8 = new UTF8Encoding();
            // Console.WriteLine($"Read {result.Count} !! {utf8.GetString(buffer, offset, length)}");
            return result.Count;
        }
        finally {
            _read_semapthore.Release();
        }
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
    {
        await _send_semapthore.WaitAsync();
        try
        {
            await ws.SendAsync(new ArraySegment<byte>(buffer, offset, length), MessageType, false, cancellationToken);
            // UTF8Encoding utf8 = new UTF8Encoding();
            // Console.WriteLine($"send {utf8.GetString(buffer, offset, length)}");
        }
        finally
        {
            _send_semapthore.Release();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposed)
            return;

        if (disposing)
        {
            ws.Dispose();
        }
        disposed = true;
    }
}

