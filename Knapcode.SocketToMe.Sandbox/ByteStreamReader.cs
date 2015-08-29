using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Knapcode.SocketToMe.Sandbox
{
    public class ByteStreamReader
    {
        private readonly Stream _stream;
        private readonly bool _preserveLineEndings;
        private readonly Encoding _encoding;
        private readonly string _lineEnding;
        private readonly byte[] _lineEndingBuffer;
        private readonly byte[] _buffer;

        private int _position;
        private int _bufferSize;

        public ByteStreamReader(Stream stream, int bufferSize, bool preserveLineEndings)
        {
            _stream = stream;
            _preserveLineEndings = preserveLineEndings;
            _encoding = new UTF8Encoding(false);
            _lineEnding = "\r\n";
            _lineEndingBuffer = _encoding.GetBytes("\r\n");
            _buffer = new byte[bufferSize];

            _position = 0;
            _bufferSize = -1;
        }

        public async Task<string> ReadLineAsync()
        {
            await EnsureFirstReadAsync();

            if (_bufferSize == 0)
            {
                return null;
            }

            var lineStream = new MemoryStream();
            int lineEndingPosition = 0;
            while (lineEndingPosition < _lineEndingBuffer.Length && _bufferSize > 0)
            {
                int endPosition;
                for (endPosition = _position; endPosition < _bufferSize; endPosition++)
                {
                    if (_buffer[endPosition] == _lineEndingBuffer[lineEndingPosition])
                    {
                        lineEndingPosition++;
                        if (lineEndingPosition == _lineEndingBuffer.Length)
                        {
                            endPosition++;
                            break;
                        }
                    }
                    else if (lineEndingPosition > 0)
                    {
                        lineEndingPosition = 0;
                    }
                }

                lineStream.Write(_buffer, _position, endPosition - _position);
                _position = endPosition;

                if (endPosition == _bufferSize)
                {
                    _bufferSize = await _stream.ReadAsync(_buffer, 0, _buffer.Length);
                    _position = 0;
                }
            }

            var line = _encoding.GetString(lineStream.GetBuffer(), 0, (int) lineStream.Length);
            if (!_preserveLineEndings && line.EndsWith(_lineEnding))
            {
                line = line.Substring(0, line.Length - _lineEnding.Length);
            }

            return line;
        }

        public async Task<int> ReadAsync(byte[] buffer, int offset, int count)
        {
            if (_bufferSize >= 0)
            {
                count = Math.Min(count, _bufferSize - _position);
                Buffer.BlockCopy(_buffer, _position, buffer, offset, count);
                _position += count;

                if (_position == _bufferSize)
                {
                    _bufferSize = -1;
                }

                return count;
            }

            return await _stream.ReadAsync(buffer, offset, count);
        }

        private async Task EnsureFirstReadAsync()
        {
            if (_bufferSize < 0)
            {
                _bufferSize = await _stream.ReadAsync(_buffer, 0, _buffer.Length);
            }
        }
    }
}