using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZQ
{
    public class SnapshotMemory : MemoryStream, IBufferWriter<byte>
    {
        private int _origin;

        public SnapshotMemory()
        {
        }

        public SnapshotMemory(int capacity) : base(capacity)
        {
        }

        public SnapshotMemory(byte[] buffer) : base(buffer)
        {
        }

        public SnapshotMemory(byte[] buffer, int index, int length) : base(buffer, index, length)
        {
            _origin = index;
        }

        public ReadOnlyMemory<byte> WrittenMemory =>GetBuffer().AsMemory(_origin, (int)Position);

        public ReadOnlySpan<byte> WrittenSpan => GetBuffer().AsSpan(_origin, (int)Position);

        public void Advance(int count)
        {
            long newLength = Position + count;
            if (newLength > Length)
            {
                SetLength(newLength);
            }
            Position = newLength;
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            if (Length - Position < sizeHint)
            {
                SetLength(Position + sizeHint);
            }

            var memory = GetBuffer().AsMemory((int)Position + _origin, (int)(Length - Position));
            return memory;
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            if (Length - Position < sizeHint)
            {
                SetLength(Position + sizeHint);
            }

            var span = GetBuffer().AsSpan((int)Position + _origin, (int)(Length - Position));
            return span;
        }
    }
}
