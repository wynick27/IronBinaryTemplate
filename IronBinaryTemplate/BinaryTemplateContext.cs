using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;


namespace IronBinaryTemplate
{
    public class BinaryTemplateContext : IServiceProvider
    {
        List<Stream> Files;
        BinaryTemplateReader CurrentReader;
        Dictionary<Type, object> Services;


        public event Action<VariableDeclaration> VariableCreating;
        public event Action<VariableDeclaration, BinaryTemplateVariable> VariableCreated;

        protected static Dictionary<Type, Func<BinaryTemplateContext, object>> readfunctionsbyte = new Dictionary<Type, Func<BinaryTemplateContext, object>>()
        {
            { typeof(short), ctx => ctx.CurrentReader.ReadInt16() },
            { typeof(int), ctx => ctx.CurrentReader.ReadInt32() },
            { typeof(long), ctx => ctx.CurrentReader.ReadInt64() },
            { typeof(sbyte), ctx => ctx.CurrentReader.ReadSByte() },
            { typeof(ushort), ctx => ctx.CurrentReader.ReadUInt16() },
            { typeof(uint), ctx => ctx.CurrentReader.ReadUInt32() },
            { typeof(ulong), ctx => ctx.CurrentReader.ReadUInt64() },
            { typeof(byte), ctx => ctx.CurrentReader.ReadByte() },
            { typeof(float), ctx => ctx.CurrentReader.ReadSingle() },
            { typeof(double), ctx => ctx.CurrentReader.ReadDouble() },
        };

        protected static Dictionary<Type, Func<BinaryTemplateContext, int, object>> readfunctionsbit = new Dictionary<Type, Func<BinaryTemplateContext,int, object>>()
        {
            { typeof(short), (ctx,size) => ctx.CurrentReader.ReadInt16(size) },
            { typeof(int), (ctx,size) => ctx.CurrentReader.ReadInt32(size) },
            { typeof(long), (ctx,size) => ctx.CurrentReader.ReadInt64(size) },
            { typeof(sbyte), (ctx,size) => ctx.CurrentReader.ReadSByte(size) },
            { typeof(ushort), (ctx,size) => ctx.CurrentReader.ReadUInt16(size) },
            { typeof(uint), (ctx,size) => ctx.CurrentReader.ReadUInt32(size) },
            { typeof(ulong), (ctx,size) => ctx.CurrentReader.ReadUInt64(size) },
            { typeof(byte), (ctx,size) => ctx.CurrentReader.ReadByte(size) },
        };

        internal void WriteBasicType(TypeDefinition type, BinaryTemplateReaderState state, object value)
        {
            throw new NotImplementedException();
        }

        public long Position 
        {
            get
            {
                return _statevalid ? _state.Position : CurrentReader.BaseStream.Position;
            }
            set
            {
                CurrentReader.BaseStream.Position = value;
                _statevalid = false;
            }
        }

        public BinaryTemplateContext()
        {
            Files = new List<Stream>();
        }
        public BinaryTemplateContext(Stream s)
            :this()
        {
            AddStream(s);
        }

        void AddStream(Stream s,bool setcurrent = true)
        {
            Files.Add(s);
            if (setcurrent)
                CurrentReader = new BinaryTemplateReader(s);
        }

        public Stream AddFile(string path, bool canwrite = false, bool setcurrent = true)
        {
            Stream s = File.Open(path, FileMode.Open, canwrite ? FileAccess.ReadWrite : FileAccess.Read, FileShare.Read);
            
            Files.Add(s);
            if (setcurrent)
                CurrentReader = new BinaryTemplateReader(s);
            return s;
        }

        public BinaryTemplateReaderState MapType(TypeDefinition type, int length = 1)
        {
           // System.Console.WriteLine($"{type} {Position} {CurrentReader.Position}");
            return type.IsBitfield ? SkipBits(type.BitSize.Value * length, type.Size.Value) : SkipBytes(type.Size.Value * length);
        }


        protected BinaryTemplateReaderState _state;
        protected bool _statevalid;

        public BinaryTemplateReaderState SkipBytes(long bytes)
        {
            if (!_statevalid)
            {
                _state = CurrentReader.SaveState();
                _statevalid = true;
            }
            var result = _state.Clone();
            _state.Position += bytes;
            return result;
            //return CurrentReader.SkipBytes(bytes);
        }


        public BinaryTemplateReaderState SkipBits(long bits,int bytecount)
        {
            if (!_statevalid)
            {
                _state = CurrentReader.SaveState();
                _statevalid = true;
            }

            if (_state.PaddedMode)
            {
                if (_state.ByteCount != bytecount)
                {
                    _state.Position += _state.ByteCount;
                    _state.BitPosition = 0;
                    _state.ByteCount = (byte)bytecount;
                }

            }
            else
                bytecount = 1;

            _state.Position += ((_state.BitPosition + bits) / (bytecount * 8)) * bytecount;
            _state.BitPosition = (int)(_state.BitPosition + bits) % (bytecount * 8);
            return _state.Clone();
            // return CurrentReader.SkipBits(bits, bytesize);
        }

        public object ReadBasicType(TypeDefinition type, BinaryTemplateReaderState state, bool restorestate=true)
        {
            System.Console.WriteLine($"{type} {state.Position} {state.BitPosition}");
           if (!restorestate)
                _statevalid = false;
            var curstate = restorestate ? CurrentReader.SaveState() : null;
            CurrentReader.LoadState(state);
            object result;
            if (type.IsBitfield)
            {
                if (!readfunctionsbit.ContainsKey(type.ClrType))
                    throw new InvalidOperationException($"{type.ClrType.FullName} is cannot be read as bitfield.");
                result = readfunctionsbit[type.ClrType](this, type.BitSize.Value);
            } else
            {
                if (!readfunctionsbyte.ContainsKey(type.ClrType))
                    throw new InvalidOperationException($"{type.ClrType.FullName} is not basic type.");
                result = readfunctionsbyte[type.ClrType](this);
            }
            if (!readfunctionsbyte.ContainsKey(type.ClrType))
                throw new InvalidOperationException($"{type.ClrType.FullName} is not basic type.");
            if (restorestate)
                CurrentReader.LoadState(curstate);
            return result;
        }

        public object ReadBasicType(Type type, long pos)
        {
            var oldpos = Position;
            Position = pos;
            if (!readfunctionsbyte.ContainsKey(type))
                throw new InvalidOperationException($"{type.FullName} is not basic type.");
            var func = readfunctionsbyte[type];
            var result = func(this);
            Position = oldpos;
            return result;
        }


        public byte[] ReadString(int maxLen = -1)
        {
            List<byte> chars = new List<byte>();
            byte ch;
            int curLen = 0;
            while ((curLen < maxLen || maxLen == -1) && (ch = CurrentReader.ReadByte()) != '\0')
            {
                chars.Add(ch);
                curLen++;
            }
            return chars.ToArray();
        }

        public string ReadWString(int maxLen = -1)
        {
            StringBuilder sb = new StringBuilder();
            char ch;
            int curLen = 0;
            while ((curLen < maxLen || maxLen == -1) && (ch = (char)CurrentReader.ReadUInt16()) != '\0')
            {
                sb.Append(ch);
                curLen++;
            }
            return sb.ToString();
        }

        public string ReadWChars(int length)
        {
            StringBuilder sb = new StringBuilder();
            for (int i=0;i<length;i++)
            {
                sb.Append((char)CurrentReader.ReadUInt16());
            }
            return sb.ToString();
        }


        [TemplateCallable]
        protected virtual void BigEndian()
        {
            CurrentReader.LittleEndian = false;
            _statevalid = false;
        }
        [TemplateCallable]
        protected virtual void LittleEndian()
        {
            CurrentReader.LittleEndian = true;
            _statevalid = false;
        }
        [TemplateCallable]
        protected virtual bool IsBigEndian()
        {
            return !CurrentReader.LittleEndian;
        }
        [TemplateCallable]
        protected virtual bool IsLittleEndian()
        {
            return CurrentReader.LittleEndian;
        }
        [TemplateCallable]
        protected virtual void BitfieldDisablePadding()
        {
            CurrentReader.PaddedMode = false;
            _statevalid = false;
        }
        [TemplateCallable]
        protected virtual void BitfieldEnablePadding()
        {
            CurrentReader.PaddedMode = true;
            _statevalid = false;
        }
        [TemplateCallable]
        protected virtual bool IsBitfieldLeftToRight()
        {
            return !CurrentReader.BitfieldRightToLeft;
        }
        [TemplateCallable]
        protected virtual bool IsBitfieldPaddingEnabled()
        {
            return CurrentReader.PaddedMode;
        }
        [TemplateCallable]
        protected virtual void BitfieldLeftToRight()
        {
            CurrentReader.BitfieldRightToLeft = false;
            _statevalid = false;
        }
        [TemplateCallable]
        protected virtual void BitfieldRightToLeft()
        {
            CurrentReader.BitfieldRightToLeft = true;
            _statevalid = false;
        }
        [TemplateCallable]
        protected virtual bool FEof()
        {
            return CurrentReader.BaseStream.Length <= CurrentReader.BaseStream.Position;
        }
        [TemplateCallable]
        protected virtual long FileSize()
        {
            return CurrentReader.BaseStream.Length;
        }
        [TemplateCallable]
        protected virtual long FTell()
        {
            if (_statevalid)
                return _state.Position;
            return CurrentReader.BaseStream.Position;
        }
        [TemplateCallable]
        protected virtual int FSeek(long pos)
        {
            try
            {
                _statevalid = false;
                CurrentReader.BaseStream.Position = pos;
                return 0;
            }
            catch (Exception)
            {
                return -1;
            }
            
        }
        [TemplateCallable]
        protected virtual int FSkip(long skip)
        {
            try
            {
                _statevalid = false;
                CurrentReader.BaseStream.Position += skip;
                return 0;
            }
            catch (Exception)
            {
                return -1;
            }
        }


        [TemplateCallable]
        protected virtual BinaryTemplateString GetFileName()
        {
            return new BinaryTemplateString(GetFileNameW());
        }

        [TemplateCallable]
        protected virtual string GetFileNameW()
        {
            if (CurrentReader.BaseStream is FileStream fileStream)
                return fileStream.Name;
            else
                return "";
        }

        [TemplateCallable]
        public byte[] ReadString(long pos, int maxLen = -1)
        {
            
            var state = CurrentReader.SaveState();
            if (pos != -1) CurrentReader.Position = pos;
            var value = ReadString(maxLen);
            CurrentReader.LoadState(state);
            return value;
        }

        [TemplateCallable]
        public string ReadWString(long pos, int maxLen = -1)
        {

            var state = CurrentReader.SaveState();
            if (pos != -1) CurrentReader.Position = pos;
            var value = ReadWString(maxLen);
            CurrentReader.LoadState(state);
            return value;
        }

        [TemplateCallable]
        public int ReadStringLength(long pos, int maxLen = -1)
        {

            var state = CurrentReader.SaveState();
            if (pos != -1) CurrentReader.Position = pos;
            var value = ReadString(maxLen).Length;
            CurrentReader.LoadState(state);
            return value;
        }

        [TemplateCallable]
        public int ReadWStringLength(long pos, int maxLen = -1)
        {

            var state = CurrentReader.SaveState();
            if (pos != -1) CurrentReader.Position = pos;
            var value = ReadWString(maxLen).Length;
            CurrentReader.LoadState(state);
            return value;
        }


        [TemplateCallable]
        protected virtual double ReadDouble(long pos = -1)
        {
            var state = CurrentReader.SaveState();
            if (pos != -1) CurrentReader.Position = pos;
            var value = CurrentReader.ReadDouble();
            CurrentReader.LoadState(state);
            return value;
        }
        [TemplateCallable]
        protected virtual float ReadFloat(long pos = -1)
        {
            var state = CurrentReader.SaveState();
            if (pos != -1) CurrentReader.Position = pos;
            var value = CurrentReader.ReadSingle();
            CurrentReader.LoadState(state);
            return value;
        }
#if NET6_0_OR_GREATER
        [TemplateCallable]
       protected virtual Half ReadHFloat(long pos = -1)
        {
            var state = CurrentReader.SaveState();
            if (pos != -1) CurrentReader.Position = pos;
#if NET6_0_OR_GREATER
            var value = CurrentReader.ReadHalf();
#else
            var value = CurrentReader.ReadHalf();
#endif
            CurrentReader.LoadState(state);
            return value;
        }
#endif
        [TemplateCallable]
        protected virtual int ReadInt(long pos = -1)
        {
            var state = CurrentReader.SaveState();
            if (pos != -1) CurrentReader.Position = pos;
            var value = CurrentReader.ReadInt32();
            CurrentReader.LoadState(state);
            return value;
        }
        [TemplateCallable]
        protected virtual long ReadInt64(long pos = -1)
        {
            var state = CurrentReader.SaveState();
            if (pos != -1) CurrentReader.Position = pos;
            var value = CurrentReader.ReadInt64();
            CurrentReader.LoadState(state);
            return value;
        }
        [TemplateCallable]
        protected virtual long ReadQuad(long pos = -1)
        {
            return ReadInt64(pos);
        }
        [TemplateCallable]
        protected virtual short ReadShort(long pos = -1)
        {
            var state = CurrentReader.SaveState();
            if (pos != -1) CurrentReader.Position = pos;
            var value = CurrentReader.ReadInt16();
            CurrentReader.LoadState(state);
            return value;
        }
        [TemplateCallable]
        protected virtual byte ReadUByte(long pos = -1)
        {
            var state = CurrentReader.SaveState();
            if (pos != -1) CurrentReader.Position = pos;
            var value = CurrentReader.ReadByte();
            CurrentReader.LoadState(state);
            return value;
        }
        [TemplateCallable]
        protected virtual sbyte ReadByte(long pos = -1)
        {
            return (sbyte)ReadUByte(pos);
        }

        [TemplateCallable]
        protected virtual uint ReadUInt(long pos = -1)
        {
            var state = CurrentReader.SaveState();
            if (pos != -1) CurrentReader.Position = pos;
            var value = CurrentReader.ReadUInt32();
            CurrentReader.LoadState(state);
            return value;
        }
        [TemplateCallable]
        protected virtual ulong ReadUInt64(long pos = -1)
        {
            var state = CurrentReader.SaveState();
            if (pos != -1) CurrentReader.Position = pos;
            var value = CurrentReader.ReadUInt64();
            CurrentReader.LoadState(state);
            return value;
        }
        [TemplateCallable]
        protected virtual ulong ReadUQuad(long pos = -1)
        {
            return ReadUInt64(pos);
        }
        [TemplateCallable]
        protected virtual ushort ReadUShort(long pos = -1)
        {
            var state = CurrentReader.SaveState();
            if (pos != -1) CurrentReader.Position = pos;
            var value = CurrentReader.ReadUInt16();
            CurrentReader.LoadState(state);
            return value;
        }

        [TemplateCallable]
        protected virtual void ReadBytes(byte[] buffer, long pos, int n)
        {
            if (n > buffer.Length)
                throw new ArgumentException();
           var state = CurrentReader.SaveState();
            CurrentReader.Position = pos;
            var value = CurrentReader.BaseStream.Read(buffer,0,n);
            CurrentReader.LoadState(state);
        }

        public byte[] ReadBytes(int length)
        {
            _statevalid = false;
            return CurrentReader.ReadBytes(length);
        }

        public BinaryTemplateReaderState SaveState()
        {
            return CurrentReader.SaveState();
        }

        public void LoadState(BinaryTemplateReaderState state)
        {
            CurrentReader.LoadState(state);
        }

        public object GetService(Type serviceType)
        {
            if (Services.TryGetValue(serviceType, out object value))
                return value;
            return null;
        }

        public void BeginNewVariable(VariableDeclaration variableDeclaration)
        {
            if (VariableCreating != null)
                VariableCreating.Invoke(variableDeclaration);
        }

        public void EndNewVariable(VariableDeclaration variableDeclaration, BinaryTemplateVariable variable)
        {
            if (VariableCreated != null)
                VariableCreated.Invoke(variableDeclaration, variable);
        }
    }
}
