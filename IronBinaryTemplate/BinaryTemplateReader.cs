using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace IronBinaryTemplate
{
    public class BinaryTemplateReaderState
    {
        public long Position;
        public bool PaddedMode;
        public bool LittleEndian;
        public bool RightToLeft;
        public byte ByteCount;
        public int BitPosition;
    }

    public class BinaryTemplateReader: BinaryReader
    {

        private bool _littleEndian = true;
        private bool _paddedMode = true;
        private byte _cachedByte;
        private int _bitIndex;
        private int _byteSize;
        private bool _bitRightToLeft = true;
        private ulong _cachedBitfield;
        private bool _signExtensionMode = true;

        public bool PaddedMode
        {
            get => _paddedMode;
            set {
                if (_paddedMode != value)
                {
                    _bitIndex = 0;
                    _byteSize = 1;
                }
                    
                _paddedMode = value;
                
            }
        }

        public bool BitfieldRightToLeft
        {
            get => _bitRightToLeft;
            set
            {
                if (_bitRightToLeft != value)
                {
                    _byteSize = 1;
                }
                _bitRightToLeft = value;

            }
        }

        public bool LittleEndian
        {
            get => _littleEndian;
            set
            {
                if (_paddedMode != value)
                    _bitIndex = 0;
                _littleEndian = value;
                _bitRightToLeft = value;
            }
        }

        public long Position
        {
            get => BaseStream.Position - (_bitIndex == 0 ? 0 : _byteSize);
            set
            {
                BaseStream.Position = value;
                _bitIndex = 0;

            }
        }


        public BinaryTemplateReader(Stream s) : base(s)
        {
        }

        public BinaryTemplateReaderState SaveState()
        {
            return new BinaryTemplateReaderState()
            {
                Position = Position,
                LittleEndian = _littleEndian,
                RightToLeft = _bitRightToLeft,
                PaddedMode = _paddedMode,
                ByteCount = (byte)_byteSize,
                BitPosition = _bitIndex,
            };
        }

        public void LoadState(BinaryTemplateReaderState state)
        {
            this.Position = state.Position;
            _littleEndian = state.LittleEndian;
            _bitRightToLeft = state.RightToLeft;
            _paddedMode = state.PaddedMode;
            
            _byteSize = state.ByteCount;
            if (state.BitPosition != 0)
            {
                _bitIndex = 0;
                if (_paddedMode)
                    ReadPackedUBits(_bitIndex, state.ByteCount);
                else
                    _cachedByte = base.ReadByte();
            }
            _bitIndex = state.BitPosition;
        }

        public BinaryTemplateReaderState SkipBytes(long value)
        {
            if (_paddedMode)
            {
                _bitIndex = 0;
                var state = this.SaveState();
                this.Position += value;
                return state;
            }
            else
            {
                var state = this.SaveState();
                _bitIndex = 0;
                this.Position += value;
                _bitIndex = state.BitPosition;
                if (_bitIndex != 0)
                    _cachedByte = base.ReadByte();
                
                return state;
            }
        }

        public BinaryTemplateReaderState SkipBits(long value,int bytesize = 0)
        {
            if (_paddedMode)
            {
                if (bytesize != _byteSize || _bitIndex == 0 || value + _bitIndex > bytesize * 8)
                {
                    _bitIndex = 0;
                    _byteSize = bytesize;
                }
                
                var state = this.SaveState();
                if (_bitIndex == 0)
                    BaseStream.Position += bytesize;
                if (value >= bytesize * 8)
                    this.Position += value / 8;
                value = value % (bytesize * 8);
                _bitIndex += (int)value;
                if (_bitIndex == _byteSize * 8)
                {
                    _bitIndex = 0;
                }
                return state;
            }
            else
            {
                var state = this.SaveState();
                _bitIndex = 0;
                this.Position += value/8;
                _bitIndex = state.BitPosition + (int)(value % 8);
                if (_bitIndex > 7)
                {
                    BaseStream.Position++;
                    _bitIndex -= 8;
                }


                return state;
            }
        }


        public override short ReadInt16()
        {
            if (_littleEndian)
                return _paddedMode ? base.ReadInt16() : BinaryPrimitives.ReadInt16LittleEndian(ReadBytes(2));
            else
                return BinaryPrimitives.ReadInt16BigEndian(ReadBytes(2));
        }

        public override int ReadInt32()
        {
            if (_littleEndian)
                return _paddedMode ? base.ReadInt32() : BinaryPrimitives.ReadInt32LittleEndian(ReadBytes(4));
            else
                return BinaryPrimitives.ReadInt32BigEndian(ReadBytes(4));

        }

        public override long ReadInt64()
        {
            if (_littleEndian)
                return _paddedMode ? base.ReadInt64() : BinaryPrimitives.ReadInt64LittleEndian(ReadBytes(8));
            else
                return BinaryPrimitives.ReadInt64BigEndian(ReadBytes(8));
        }

        public override ushort ReadUInt16()
        {
            if (_littleEndian)
                return _paddedMode ? base.ReadUInt16() : BinaryPrimitives.ReadUInt16LittleEndian(ReadBytes(2));
            else
                return BinaryPrimitives.ReadUInt16BigEndian(ReadBytes(2));
        }

        public override uint ReadUInt32()
        {
            if(_littleEndian)
                return _paddedMode ? base.ReadUInt32() : BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(4));
            else
                return BinaryPrimitives.ReadUInt32BigEndian(ReadBytes(4));
        }

        public override ulong ReadUInt64()
        {
            if (_littleEndian)
                return _paddedMode ? base.ReadUInt64() : BinaryPrimitives.ReadUInt64LittleEndian(ReadBytes(8));
            else
                return BinaryPrimitives.ReadUInt64BigEndian(ReadBytes(8));
        }

        public short ReadInt16(int bitsize)
        {
            if (bitsize == 0)
                return 0;
            else if (bitsize < 0)
                throw new ArgumentException("Bitsize cannot be negative", "bitsize");
            else if (bitsize > 16)
                throw new ArgumentException("Bitsize too large.", "bitsize");
            return _paddedMode ? (short)ReadPackedSBits(bitsize, 2) : (short)ReadSignedBits(bitsize);
        }

        public int ReadInt32(int bitsize)
        {
            if (bitsize == 0)
                return 0;
            else if (bitsize < 0)
                throw new ArgumentException("Bitsize cannot be negative", "bitsize");
            else if (bitsize > 16)
                throw new ArgumentException("Bitsize too large.", "bitsize");
            return _paddedMode ? (int)ReadPackedSBits(bitsize, 4) : (int)ReadSignedBits(bitsize);

        }

        public long ReadInt64(int bitsize)
        {
            if (bitsize == 0)
                return 0;
            else if (bitsize < 0)
                throw new ArgumentException("Bitsize cannot be negative", "bitsize");
            else if (bitsize > 16)
                throw new ArgumentException("Bitsize too large.", "bitsize");
            return _paddedMode ? ReadPackedSBits(bitsize, 8) : ReadSignedBits(bitsize);
        }

        public ushort ReadUInt16(int bitsize)
        {
            if (bitsize == 0)
                return 0;
            else if (bitsize < 0)
                throw new ArgumentException("Bitsize cannot be negative", "bitsize");
            else if (bitsize > 16)
                throw new ArgumentException("Bitsize too large.", "bitsize");
            return _paddedMode ? (ushort)ReadPackedUBits(bitsize, 2) : (ushort)ReadUnsignedBits(bitsize);
        }

        public uint ReadUInt32(int bitsize)
        {
            if (bitsize == 0)
                return 0;
            else if (bitsize < 0)
                throw new ArgumentException("Bitsize cannot be negative", "bitsize");
            else if (bitsize > 16)
                throw new ArgumentException("Bitsize too large.", "bitsize");
            return _paddedMode ? (uint)ReadPackedUBits(bitsize, 4) : (uint)ReadUnsignedBits(bitsize);

        }

        public ulong ReadUInt64(int bitsize)
        {
            if (bitsize == 0)
                return 0;
            else if (bitsize < 0)
                throw new ArgumentException("Bitsize cannot be negative", "bitsize");
            else if (bitsize > 16)
                throw new ArgumentException("Bitsize too large.", "bitsize");
            return _paddedMode ? ReadPackedUBits(bitsize, 8) : ReadUnsignedBits(bitsize);
        }

        public override float ReadSingle()
        {
            return BitConverter.Int32BitsToSingle(ReadInt32());
        }

        public override double ReadDouble()
        {
            return BitConverter.Int64BitsToDouble(ReadInt64());
        }

        public override byte ReadByte()
        {
            if (_paddedMode)
                return base.ReadByte();
            else
                return (byte)ReadUnsignedBits(8);
        }

        public override sbyte ReadSByte()
        {
            return (sbyte)ReadByte();
        }


        public sbyte ReadSByte(int bitsize)
        {
            if (bitsize == 0)
                return 0;
            else if (bitsize < 0)
                throw new ArgumentException("Bitsize cannot be negative", "bitsize");
            else if (bitsize > 8)
                throw new ArgumentException("Bitsize too large.", "bitsize");
            return _paddedMode ? (sbyte)ReadPackedSBits(bitsize, 1) : (sbyte)ReadSignedBits(bitsize);
        }

        public byte ReadByte(int bitsize)
        {
            if (bitsize == 0)
                return 0;
            else if (bitsize < 0)
                throw new ArgumentException("Bitsize cannot be negative", "bitsize");
            else if (bitsize > 8)
                throw new ArgumentException("Bitsize too large.", "bitsize");
            return _paddedMode ? (byte)ReadPackedUBits(bitsize, 1) : (byte)ReadUnsignedBits(bitsize);
        }

        public override byte[] ReadBytes(int count)
        {
            if (_paddedMode)
                return base.ReadBytes(count);
            byte[] result = new byte[count];
            for (int i = 0; i < count; i++)
                result[i] = ReadByte();
            return result;
        }

        public virtual bool ReadBit()
        {
            var bitIndex = _bitIndex & 0x07;
            if (bitIndex == 0)
            {
                _cachedByte = base.ReadByte();
            }
            _bitIndex++;
            return ((_cachedByte << bitIndex) & 0x80) != 0;
        }


        protected virtual long ReadPackedSBits(int count,int packsize)
        {
            if (!_signExtensionMode)
                return (long)ReadPackedUBits(count, packsize);
            if (packsize != _byteSize || _bitIndex == 0)
            {
                _bitIndex = 0;
                _byteSize = packsize;
                if (packsize == 2)
                    _cachedBitfield = ReadUInt16();
                else if (packsize == 4)
                    _cachedBitfield = ReadUInt32();
                else if (packsize == 8)
                    _cachedBitfield = ReadUInt64();
            }
            int lshift, rshift;
            if (_bitRightToLeft)
            {
                lshift = 64  - _bitIndex - count;
                rshift = 64 - count;
            } else
            {
                lshift = 64 - _byteSize * 8 + _bitIndex;
                rshift = 64 - count;
                
            }
            _bitIndex += count;
            return ((long)_cachedBitfield) << lshift >> rshift;
        }


        protected virtual ulong ReadPackedUBits(int count, int packsize)
        {
            if (packsize != _byteSize || _bitIndex == 0)
            {
                _bitIndex = 0;
                _byteSize = packsize;
                if (packsize == 1)
                    _cachedBitfield = ReadByte();
                else if (packsize == 2)
                    _cachedBitfield = ReadUInt16();
                else if (packsize == 4)
                    _cachedBitfield = ReadUInt32();
                else if (packsize == 8)
                    _cachedBitfield = ReadUInt64();
            }
            int lshift, rshift;
            if (_bitRightToLeft)
            {
                lshift = 64 - _bitIndex - count;
                rshift = 64 - count;
            }
            else
            {
                lshift = 64 - _byteSize * 8 + _bitIndex;
                rshift = 64 - count;

            }
            _bitIndex += count;
            return _cachedBitfield << lshift >> rshift;
        }

        public virtual long ReadSignedBits(int count)
        {
            long res = (long)ReadUnsignedBits(count);
            if(!_signExtensionMode)
                return (long)res;
            res = res << (64 - count) >> (64 - count);
            return res;
        }

        public virtual ulong ReadUnsignedBits(int count)
        {
            ulong res = 0;
            int bitread = 0;
            while (count > 0)
            {
                if (_bitIndex == 0)
                    _cachedByte = base.ReadByte();
                int remain = 8 - _bitIndex;
                int toread = Math.Min(count, remain);
                if (_bitRightToLeft)
                    res = res | ((ulong)(byte)(_cachedByte<< 8 - toread - _bitIndex) >> 8 - toread << bitread);
                else
                    res = (res << toread) | (ulong)(byte)(_cachedByte << _bitIndex) >> 8 - toread;
                count -= toread;
                bitread += toread;
                _bitIndex = (_bitIndex + toread) & 0x7;
            }
            return res;
        }

    }
}
