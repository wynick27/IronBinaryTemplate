using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;


namespace IronBinaryTemplate
{
    public class BinaryTemplateContext
    {
        List<Stream> Files;
        BinaryTemplateReader CurrentReader;

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



        public long Position { get => CurrentReader.BaseStream.Position; set => CurrentReader.BaseStream.Position = value; }

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
            Stream s = File.Open(path, FileMode.Open, canwrite ? FileAccess.ReadWrite : FileAccess.Read);
            
            Files.Add(s);
            if (setcurrent)
                CurrentReader = new BinaryTemplateReader(s);
            return s;
        }

        public BinaryTemplateReaderState MapType(TypeDefinition type)
        {
            return type.IsBitfield ? CurrentReader.SkipBits(type.BitSize.Value, type.Size.Value) : CurrentReader.SkipBytes(type.Size.Value);
        }

        public object ReadBasicType(TypeDefinition type, BinaryTemplateReaderState state, bool restorestate=true)
        {
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

        public byte[] ReadString()
        {
            List<byte> chars = new List<byte>();
            byte ch;
            while ((ch = CurrentReader.ReadByte()) != '\0')
            {
                chars.Add(ch);
            }
            return chars.ToArray();
        }

        public string ReadWString()
        {
            StringBuilder sb = new StringBuilder();
            char ch;
            while ((ch = (char)CurrentReader.ReadUInt16()) != '\0')
            {
                sb.Append(ch);
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
        }
        [TemplateCallable]
        protected virtual void LittleEndian()
        {
            CurrentReader.LittleEndian = true;
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
        }
        [TemplateCallable]
        protected virtual void BitfieldEnablePadding()
        {
            CurrentReader.PaddedMode = true;
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
        }
        [TemplateCallable]
        protected virtual void BitfieldRightToLeft()
        {
            CurrentReader.BitfieldRightToLeft = true;
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
            return CurrentReader.BaseStream.Position;
        }
        [TemplateCallable]
        protected virtual int FSeek(long pos)
        {
            try
            {
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
    }
}
