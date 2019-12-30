using System;
using System.Threading;
using System.Threading.Tasks;
using Thrift.Transport;
using System.Collections.Concurrent;
using System.IO;
using System.Text;

public class TReverseTunnelServer : Thrift.Transport.TServerTransport, IDisposable
{
    internal class WrappedTransportForTunnel : TTransport
    {
        private readonly TReverseTunnelServer _parent;
        public WrappedTransportForTunnel(TReverseTunnelServer parent)
        {
            _parent = parent;
        }

        public override bool IsOpen => true;

        public override void Close()
        {
            //We do not want the server to close our tunnel, as we have a fake transport
            ;
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            await _parent._transport.FlushAsync();
            _parent._accepted_workers.Take();
            
        }

        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            //It is the parent that manages the connection.
            return Task.FromResult<object>(null);
        }
        public override async ValueTask<int> ReadAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
        {

            var r = await _parent._transport.ReadAsync(buffer, offset, length, cancellationToken);
            _parent.fileStream.Write(buffer, offset, r);
            // UTF8Encoding utf8 = new UTF8Encoding();
            // Console.WriteLine($"Tunnel Read {string.Format("0x{0:X}", buffer[0])} !! {utf8.GetString(buffer, offset, length)}");
            // if (buffer[0] == 0x1)
            //     throw new InvalidDataException("FICL");
            return r;
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
        {
            await _parent._transport.WriteAsync(buffer, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            //Do nothing as we do not own any resource. We are just a wrapper conforming to an abstact class.
        }
    }
    internal readonly TTransport _transport;
    internal BlockingCollection<Object> _accepted_workers;
    internal bool _is_accepting;
    private bool _is_disposed;

    private readonly FileStream fileStream;
    public TReverseTunnelServer(TTransport transport, int acceptable_clients = 10)
    {
        
        fileStream = new FileStream("/tmp/tunnel", FileMode.Create, FileAccess.Write);
        _transport = transport;
        _accepted_workers = new BlockingCollection<object>(acceptable_clients);
        _is_accepting = false;
        _is_disposed = false;
    }

    public override void Close()
    {
        Console.WriteLine("called");
    }

    public override bool IsClientPending()
    {
        
        return _accepted_workers.Count != _accepted_workers.BoundedCapacity;
        // return _is_accepting;
    }

    public override async void Listen()
    {
        await _transport.OpenAsync();
        _is_accepting = true;
    }

    protected override ValueTask<TTransport> AcceptImplementationAsync(CancellationToken cancellationToken)
    {
        if (_is_accepting) {
            _accepted_workers.Add(null, cancellationToken);
            Console.WriteLine("accepting");
            var wrapper = new WrappedTransportForTunnel(this);
            return new ValueTask<TTransport>(_transport);
        }
        throw new TTransportException(TTransportException.ExceptionType.NotOpen);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_is_disposed)
        {
            fileStream.Dispose();
            _is_accepting = false;
            _accepted_workers.Dispose();
            _is_disposed = true;
        }
    }
    public void Dispose()
    {
        Dispose(true);
    }
}