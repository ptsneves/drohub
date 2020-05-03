using System;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace DroHub.Helpers.Thrift {
    public class ChannelInputStream : Stream  {
        private readonly Channel<byte[]> _input_channel;
        private MemoryStream _memory_stream;

        public ChannelInputStream(Channel<byte[]> input_channel) {
            _input_channel = input_channel;
            _memory_stream = new MemoryStream(128);
        }

        public override int Read(byte[] buffer, int offset, int count) {
            throw new NotImplementedException();
        }

        private void regenerateMemoryStream() {
            var new_memory_stream = new MemoryStream();
            _memory_stream.CopyTo(new_memory_stream);
            _memory_stream = new_memory_stream;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int length,
            CancellationToken cancellationToken) {
            while (length > _memory_stream.Position) {
                var chan_read = await _input_channel.Reader.ReadAsync(cancellationToken);
                _memory_stream.Write(chan_read);
            }

            _memory_stream.Seek(0, SeekOrigin.Begin);
            var f = await _memory_stream.ReadAsync(buffer, offset, length, cancellationToken);
            if (f != length)
                throw new InvalidProgramException();
            regenerateMemoryStream();
            return f;
        }

        public override void Close() {
            _memory_stream.Dispose();
            base.Close();
        }

        public override long Seek(long offset, SeekOrigin origin) {
            throw new NotImplementedException();
        }

        public override void SetLength(long value) {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count) {
            throw new NotImplementedException();
        }

        public override Task WriteAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }

        public override bool CanRead => !_input_channel.Reader.Completion.IsCompleted && _memory_stream.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotImplementedException();
        public override long Position {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        public override void Flush() {
            throw new NotImplementedException();
        }
    }
}