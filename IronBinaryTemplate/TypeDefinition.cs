using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;


namespace IronBinaryTemplate
{

    public enum BasicTypeRank
    {
        WChar = 0x21,
        //String = 0x30,

        Char = 0x10,
        Byte = 0x13,
        Int16 = 0x20,
        UInt16 = 0x23,
        Int32 = 0x30,
        UInt32 = 0x33,
        Int64 = 0x40,
        UInt64 = 0x43,


        Half = 0x50,
        Single = 0x60,
        Double = 0x70,

    }

    public enum TypeKind
    {
        Void,
        Integer,
        Float,
        Array,
        Struct,
        Union,
        Enum,
        Function,
        TypeAlias,
        UserDefined,
    }

    public abstract class TypeDefinition : IEquatable<TypeDefinition>
    {

        public TypeDefinition(string name = "", TypeKind type = TypeKind.Void)
        {
            Name = name;
            TypeKind = type;
            IsComplete = true;
        }
        public virtual bool IsFixedSize => Size.HasValue;

        public virtual int? BitSize => Size.HasValue ? (Size.Value * 8) as int? : null;

        public virtual int? Size { get; }

        public bool IsBitfield { get => (Size.HasValue && BitSize.HasValue) ? BitSize.Value != Size.Value * 8 : false; }
        public virtual bool IsComplete { get; internal set; }

        public TypeKind TypeKind { get; protected set; }
        public string Name { get; set; }
        public virtual Type ClrType { get; internal set; }

        public virtual Type LocalClrType { get => ClrType; }
        public virtual bool IsBasicType => false;
        public virtual bool IsStructOrUnion => false;

        public virtual TypeDefinition UnderlyingType { get => this;}

        public List<VariableDeclaration> References { get; protected set; }

        public virtual bool IsEnum => false;

        public CompoundDefinition Parent;

        public SourceSpan SourceLocation;

        public static bool operator ==(TypeDefinition t1, TypeDefinition t2)
        {
            if (ReferenceEquals(t1, t2))
                return true;
            if (ReferenceEquals(t1, null))
                return false;
            if (ReferenceEquals(t2, null))
                return false;
            return t1.Equals(t2);
        }

        public static bool operator !=(TypeDefinition t1, TypeDefinition t2)
         => !(t1 == t2);

        protected static Dictionary<Type, TypeDefinition> typemapping = new Dictionary<Type, TypeDefinition>()
        {
            { typeof(void), VoidType.Instance },
            { typeof(char), new BasicType("wchar_t",typeof(char),sizeof(char), BasicTypeRank.WChar) },
            { typeof(sbyte), new BasicType("char",typeof(sbyte),sizeof(sbyte), BasicTypeRank.Char) },
            { typeof(byte), new BasicType("uchar",typeof(byte),sizeof(byte), BasicTypeRank.Byte) },
            { typeof(short), new BasicType("short",typeof(short),sizeof(short), BasicTypeRank.Int16) },
            { typeof(int), new BasicType("int",typeof(int),sizeof(int), BasicTypeRank.Int32) },
            { typeof(long), new BasicType("int64",typeof(long),sizeof(long), BasicTypeRank.Int64) },
            { typeof(ushort), new BasicType("ushort",typeof(ushort),sizeof(ushort), BasicTypeRank.UInt16) },
            { typeof(uint), new BasicType("uint",typeof(uint),sizeof(uint), BasicTypeRank.UInt32) },
            { typeof(ulong), new BasicType("uint64",typeof(ulong),sizeof(ulong), BasicTypeRank.UInt64) },
            { typeof(float), new BasicType("float",typeof(float),sizeof(float), BasicTypeRank.Single) },
            { typeof(double), new BasicType("double",typeof(double),sizeof(double), BasicTypeRank.Double) },
#if NET5_0_OR_GREATER
             { typeof(Half), new BasicType("hfloat",typeof(Half),2, BasicTypeRank.Half) },
#endif
        };


        protected static Dictionary<string, TypeDefinition> typenamemapping = new Dictionary<string, TypeDefinition>()
        {
            { "void", VoidType.Instance },
            { "char", BasicType.FromClrType(typeof(sbyte)) },
            { "uchar", BasicType.FromClrType(typeof(byte)) },
            { "short", BasicType.FromClrType(typeof(short)) },
            { "ushort", BasicType.FromClrType(typeof(ushort)) },
            { "int", BasicType.FromClrType(typeof(int)) },
            { "uint", BasicType.FromClrType(typeof(uint)) },
            { "int64", BasicType.FromClrType(typeof(long)) },
            { "uint64", BasicType.FromClrType(typeof(ulong)) },
            { "float", BasicType.FromClrType(typeof(float)) },
            { "double", BasicType.FromClrType(typeof(double)) },
#if NET5_0_OR_GREATER
            { "hfloat", BasicType.FromClrType(typeof(Half)) },
#endif
        };


        static TypeDefinition()
        {
            
            
            var typealiasmap = new (string, Type)[] { 
                ("byte", typeof(sbyte)) ,
                ("ubyte", typeof(byte)) ,
                ("int16", typeof(short)) ,
                ("uint16", typeof(ushort)) ,
                ("int32", typeof(int)) ,
                ("uint32", typeof(uint)) ,
                ("long", typeof(int)) ,
                ("ulong", typeof(uint)) ,
                ("quad", typeof(long)) ,
                ("uquad", typeof(ulong)) ,
            };
            foreach (var (from, to) in typealiasmap)
            {
                typenamemapping.Add(from, FromClrType(to, from));
            }
            foreach (var typename in typenamemapping.Keys.ToArray())
            {
                typenamemapping.Add(typename.ToUpper(), BasicType.FromString(typename));
            }
            typealiasmap = new (string, Type)[] {
                ("WORD", typeof(short)) ,
                ("DWORD", typeof(int)) ,
                ("QWORD", typeof(long)) ,
                ("__int64", typeof(long)) ,
                ("__uint64", typeof(ulong)) ,
            };
            foreach (var (from, to) in typealiasmap)
            {
                typenamemapping.Add(from, FromClrType(to, from));
            }
            typenamemapping.Add("wchar_t", FromClrType(typeof(char)));
            typenamemapping.Add("string", FromString("char").GetArrayType());
            typenamemapping.Add("wstring", FromString("wchar_t").GetArrayType());
        }

        public virtual int RequiredArguments => 0;

        public virtual CustomAttributeCollection CustomAttributes
            => null;

        public abstract BinaryTemplateVariable CreateInstance(BinaryTemplateContext context, IBinaryTemplateScope scope, params object[] args);
        public abstract BinaryTemplateVariable CreateLocalInstance(IBinaryTemplateScope scope, object initizlizer, params object[] args);

        public virtual TypeDefinition GetArrayType(int? length = null)
        {
            ArrayDefinition type;
            if (this is BasicType bs)
            {
                if (bs.Rank == BasicTypeRank.Byte)
                    type = new ByteArrayDefinition(length);
                else if (bs.Rank == BasicTypeRank.Char)
                    type = new CharArrayDefinition(length);
                else if (bs.Rank == BasicTypeRank.WChar)
                    type = new WCharArrayDefinition(length);
                else
                    type = new ArrayDefinition(this, length);
            }
            else
                type = new ArrayDefinition(this, length);
            return type;
        }


        public virtual TypeDefinition GetBitfieldType(int bitsize)
        {
            if (this is BasicType bs)
            {
                return new BasicType(bs, bitsize);
            }
            else
                throw new NotSupportedException("Only basic type supports bitfield.");
        }

        public static TypeDefinition FromString(string name)
        {
            TypeDefinition type;
            typenamemapping.TryGetValue(name, out type);
            return type;

        }

        public static TypeDefinition FromClrType(Type type)
        {
            TypeDefinition def = null;
            typemapping.TryGetValue(type, out def);
            return def;
        }

        public static TypeDefinition FromClrType(Type type, string name)
        {
            TypeDefinition def = null;
            if (typemapping.TryGetValue(type, out def))
            {
                def = def.MemberwiseClone() as TypeDefinition;
                def.Name = name;
            }
            return def;
        }


        public override string ToString()
        {
            return Name;
        }

        public virtual string ToString(string varname)
        {
            return $"{Name} {varname}";
        }

        public virtual bool Equals(TypeDefinition other)
        {
            if (other == null)
                return false;
            else if (other is TypeAliasDefinition alias)
                return this.Equals(alias.UnderlyingType);
            return ReferenceEquals(this, other);
        }

        public override bool Equals(Object other)
        {
            if (other is TypeDefinition typedef)
                return Equals(typedef);
            return false;
            
        }

    }

    public class VoidType : TypeDefinition
    {
        public override int? Size => 0;
        public static VoidType Instance = new VoidType();
        private VoidType()
        {
            Name = "void";
        }
        public override Type ClrType { get => typeof(void); internal set { } }
        public override BinaryTemplateVariable CreateInstance(BinaryTemplateContext context, IBinaryTemplateScope scope, params object[] args)
        {
            throw new InvalidOperationException("Cannot create object of void type.");
        }

        public override BinaryTemplateVariable CreateLocalInstance(IBinaryTemplateScope scope, object initizlizer, params object[] args)
        {
            throw new InvalidOperationException("Cannot create object of void type.");
        }

    }

    public class BasicType : TypeDefinition
    {
        public override bool IsBasicType => true;
        public override bool IsFixedSize => true;

        int _size = 0;
        int _bitSize = 0;
        public override int? Size => _size;

        public override int? BitSize => _bitSize;

        public BasicTypeRank Rank { get; }
        public bool IsSigned { get => ((int)Rank & 1) == 0; }

        static Dictionary<BasicTypeRank, BasicType> typecodemapping = new Dictionary<BasicTypeRank, BasicType>();

        static void InitTypeCodeMapping()
        {

            foreach (var type in typemapping.Values)
            {
                if (type == null)
                    continue;
                if (type is BasicType basictype && type.Name != "wchar_t")
                    typecodemapping.Add(basictype.Rank, basictype);
            }
        }

        public BasicType(string name, Type type, int size, BasicTypeRank rank)
        {
            Name = name;
            ClrType = type;
            _size = size;
            _bitSize = size * 8;
            Rank = rank;
            TypeKind = rank >= BasicTypeRank.Half ? TypeKind.Float : TypeKind.Integer;
        }

        public BasicType(BasicType basetype, int bitsize)
        {
            Name = basetype.Name;
            ClrType = basetype.ClrType;
            _size = basetype.Size.Value;
            if (bitsize > _size * 8)
                throw new NotSupportedException("Bitsize cannot exceed underlying type size.");
            _bitSize = bitsize;
            Rank = basetype.Rank;
            TypeKind = basetype.TypeKind;
        }

        public static BasicType FromTypeRank(BasicTypeRank typerank)
        {
            BasicType def = null;
            if (typecodemapping.Count == 0)
                InitTypeCodeMapping();
            typecodemapping.TryGetValue(typerank, out def);
            return def;
        }

        public override BinaryTemplateVariable CreateInstance(BinaryTemplateContext context, IBinaryTemplateScope scope, params object[] args)
        {
            return new ScalarVariable(context, this);
        }

        public override BinaryTemplateVariable CreateLocalInstance(IBinaryTemplateScope scope, object initizlizer, params object[] args)
        {
            if (initizlizer == null)
                initizlizer = Activator.CreateInstance(LocalClrType);
            else if (initizlizer.GetType() != this.LocalClrType)
                initizlizer = RuntimeHelpers.ChangeType(initizlizer, this.LocalClrType);
            return new LocalVariable("", this, initizlizer);
        }

        public override bool Equals(TypeDefinition other)
        {
            if (other == null)
                return false;
            else if (other is TypeAliasDefinition alias)
                return this.Equals(alias.UnderlyingType);
            return this.ClrType == other.ClrType && this.BitSize == other.BitSize;
        }

        public override int GetHashCode()
        {
            return ClrType.GetHashCode() ^ BitSize ?? 0;
        }

    }

    public class EnumDefinition : TypeDefinition, IBinaryTemplateScope, ILexicalScope
    {
        public VariableCollection Values { get; }

        int _size = 0;
        int _bitSize = 0;

        public override int? Size => _size;
        public override int? BitSize => _bitSize;
        public override bool IsBasicType => true;
        public override bool IsFixedSize => true;

        public IEnumerable<string> Keys => (Values as IDictionary<string, BinaryTemplateVariable>).Keys;

        public int Count => Values.Count;

        public BinaryTemplateVariable this[string key] => Values[key];


        public EnumDefinition(string name, Type clrType = null)
        {
            TypeKind = TypeKind.Enum;
            Name = name;
            ClrType = clrType != null ? clrType : typeof(int);
            _size = BasicType.FromClrType(ClrType).Size.Value;
            _bitSize = _size * 8;
            Values = new VariableCollection();
            ScopeParam = Expression.Parameter(typeof(IBinaryTemplateScope));
            References = new List<VariableDeclaration>();
            IsComplete = false;
        }

        public EnumDefinition(EnumDefinition enumdef, int bitsize)
        {
            TypeKind = TypeKind.Enum;
            Name = enumdef.Name;
            ClrType = enumdef.ClrType;
            _size = enumdef._size;
            _bitSize = bitsize;
            Values = enumdef.Values;
            ScopeParam = enumdef.ScopeParam;
            References = enumdef.References;
            IsComplete = true;
        }

        public override BinaryTemplateVariable CreateInstance(BinaryTemplateContext context, IBinaryTemplateScope scope, params object[] args)
        {
            return new ScalarVariable(context, this);
        }

        public string FindName(object value)
        {
            throw new NotImplementedException();
        }

        public override BinaryTemplateVariable CreateLocalInstance(IBinaryTemplateScope scope, object initizlizer, params object[] args)
        {
            return new LocalVariable("", this, initizlizer);
        }
        public override string ToString()
        {
            return $"enum {Name}";
        }


        public override string ToString(string varname)
        {
            return $"enum {Name} {varname}";
        }

        public override TypeDefinition GetBitfieldType(int bitsize)
        {
            return new EnumDefinition(this, bitsize);
        }

        public BinaryTemplateVariable GetVariable(string name)
        {
            return Values[name];
        }

        public override bool Equals(TypeDefinition other)
        {
            if (other is TypeAliasDefinition alias)
                return this.Equals(alias.UnderlyingType);
            else if (other is EnumDefinition enumdef)
                return this.Values == enumdef.Values && this._bitSize == enumdef._bitSize;
            return false;
        }

        public override int GetHashCode()
        {
            return Values.GetHashCode() ^ BitSize ?? 0;
        }

        public ParameterExpression ScopeParam { get; }

        public Expression ScopeArg => ScopeParam;

        public Expression Context => null;

        IEnumerable<BinaryTemplateVariable> IReadOnlyDictionary<string, BinaryTemplateVariable>.Values => (Values as IDictionary<string, BinaryTemplateVariable>).Values;

        public ParameterExpression GetParameter(string name)
        {
            if (name == "$scope")
                return ScopeParam;
            return null;
        }

        public ParameterExpression[] GetParameterList()
        {
            return new[] { ScopeParam};
        }

        public bool ContainsKey(string key)
        {
            return Values.Contains(key);
        }

        public bool TryGetValue(string key, [MaybeNullWhen(false)] out BinaryTemplateVariable value)
        {
            return Values.TryGetValue(key, out value);
        }

        public IEnumerator<KeyValuePair<string, BinaryTemplateVariable>> GetEnumerator()
        {
            return (Values as IDictionary<string, BinaryTemplateVariable>).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Values.GetEnumerator();
        }
    }

    public class TypeAliasDefinition : TypeDefinition
    {
        public override TypeDefinition UnderlyingType { get; }

        private CustomAttributeCollection _attributes;
        public override CustomAttributeCollection CustomAttributes
            => _attributes;

        //  public override bool IsFixedSize => Length != 0 && Element.IsFixedSize;
        public override int? Size => UnderlyingType.Size;

        public override int? BitSize => UnderlyingType.BitSize;

        public override bool IsEnum => UnderlyingType.IsEnum;
        public override bool IsStructOrUnion => UnderlyingType.IsStructOrUnion;

        public override Type ClrType { get => UnderlyingType.ClrType; internal set => UnderlyingType.ClrType = value; }

        public TypeAliasDefinition(string name, TypeDefinition type, CustomAttributeCollection attributes = null)
        {
            Name = name;
            UnderlyingType = type;
            TypeKind = TypeKind.TypeAlias;
            References = new List<VariableDeclaration>();
            while (UnderlyingType is TypeAliasDefinition typealias)
                UnderlyingType = typealias.UnderlyingType;
            IsComplete = true;
            _attributes = attributes;
        }

        public override TypeDefinition GetArrayType(int? length = null)
        {
            return UnderlyingType.GetArrayType(length);
        }

        public override TypeDefinition GetBitfieldType(int bitsize)
        {
            return UnderlyingType.GetBitfieldType(bitsize);
        }
        public override bool IsBasicType => UnderlyingType.IsBasicType;

        public override bool IsFixedSize => UnderlyingType.IsFixedSize;

        public override Type LocalClrType => UnderlyingType.LocalClrType;

        public override int RequiredArguments => UnderlyingType.RequiredArguments;

        public override BinaryTemplateVariable CreateInstance(BinaryTemplateContext context, IBinaryTemplateScope scope, params object[] args)
        {
            var instance = UnderlyingType.CreateInstance(context, scope, args);
            instance.Type = this;
            return instance;
        }

        public override BinaryTemplateVariable CreateLocalInstance(IBinaryTemplateScope scope, object initizlizer, params object[] args)
        {
            return UnderlyingType.CreateLocalInstance(scope, initizlizer, args);
        }

        public override bool Equals(TypeDefinition other)
        {
            if(other is TypeAliasDefinition alias)
                return this.Equals(alias.UnderlyingType);
            return UnderlyingType.Equals(other);
        }

        public override string ToString()
        {
            return $"{Name}";
        }


        public override string ToString(string varname)
        {
            return $"{Name} {varname}";
        }
    }

    public class ArrayDefinition : TypeDefinition
    {
        public TypeDefinition Element { get;  }
        public int? Length { get; set; }

        public override Type LocalClrType => Element.LocalClrType.MakeArrayType();

        public override int? Size => Length.HasValue ? (Element?.Size * Length) : null;
        
        public ArrayDefinition(TypeDefinition elemtype, int? length)
        {
            Element = elemtype;
            TypeKind = TypeKind.Array;
            Length = length;
            ClrType = typeof(BinaryTemplateArray);
            References = elemtype.References;
        }

        public override BinaryTemplateVariable CreateInstance(BinaryTemplateContext context, IBinaryTemplateScope scope, params object[] args)
        {
            
            if (!Length.HasValue)
                throw new InvalidOperationException("Incomplete type. Cannot create array of undefined size.");
            if (Element.Size.HasValue)
            {
                BinaryTemplateLazyArray lazyarray = new BinaryTemplateLazyArray(context, this, context.Position, Length.Value, args);
                return lazyarray;
            }
            else
            {
                BinaryTemplateArray array = new BinaryTemplateArray(context, this, context.SaveState());
                for (int i = 0; i < Length; i++)
                {
                    array.AddVariable(Element.CreateInstance(context, scope, args));
                }
                return array;
            }
            
            
        }

        public override BinaryTemplateVariable CreateLocalInstance(IBinaryTemplateScope scope, object initizlizer, params object[] args)
        {
            //TODO add initializer support.
            if (!Length.HasValue)
                throw new InvalidOperationException("Incomplete type. Cannot create array of undefined size.");

            var array = Array.CreateInstance(Element.LocalClrType, Length.Value);
            return new LocalVariable("", this, array);
        }


        public override string ToString()
        {
            return $"{Element}[{Length}]";

        }

        public override string ToString(string varname)
        {
            return $"{Element.ToString(varname)}[{Length}]";

        }

        public override bool Equals(TypeDefinition other)
        {
            if (other is TypeAliasDefinition alias)
                return this.Equals(alias.UnderlyingType);
            else if (other is ArrayDefinition arraydef)
                return this.Element == arraydef.Element && this.Length == arraydef.Length;
            return false;
        }
    }

    public class ByteArrayDefinition : ArrayDefinition
    {
        public ByteArrayDefinition(int? length) : base(BasicType.FromTypeRank(BasicTypeRank.Byte), length)
        {
        }

        public override BinaryTemplateVariable CreateInstance(BinaryTemplateContext context, IBinaryTemplateScope scope, params object[] args)
        {
            if (!Length.HasValue)
                throw new InvalidOperationException("Incomplete type. Cannot create array of undefined size.");
            if (Length > 256)
            {
                
                var array =  new BinaryTemplateLazyArray(context, this, context.Position, Length.Value, args);
                return array;

            }
            else
            {
                var state = context.SaveState();
                var bytes = context.ReadBytes(Length.Value);
                return new BinaryTemplateWrapperArray<byte>(bytes, this, state);

            }
        }
    }

    public class CharArrayDefinition : ArrayDefinition
    {
        public override Type ClrType { get => typeof(byte[]); internal set { } }
        public CharArrayDefinition(int? length) : base(BasicType.FromTypeRank(BasicTypeRank.Char), length)
        {
        }

        public override BinaryTemplateVariable CreateInstance(BinaryTemplateContext context, IBinaryTemplateScope scope, params object[] args)
        {
            byte[] bytes;
            BinaryTemplateReaderState state = context.SaveState();
            if (!Length.HasValue)
                bytes = context.ReadString();
            else
                bytes = context.ReadBytes(Length.Value);
            return new BinaryTemplateStringVariable(bytes, this, state);
        }

        public override BinaryTemplateVariable CreateLocalInstance(IBinaryTemplateScope scope, object initizlizer, params object[] args)
        {
            //Todo: chack initializer
            object value = null;
            if (Length.HasValue)
                return new LocalVariable("",this,new sbyte[Length.Value]);
            else
            {
                if (initizlizer == null)
                    value = new BinaryTemplateString("");
                else if (initizlizer is BinaryTemplateString)
                    value = initizlizer;
            }
            return new LocalVariable("", this, value);
        }

        public override string ToString()
        {
            if (!Length.HasValue)
                return "string";
            return base.ToString();
        }
    }


    public class WCharArrayDefinition : ArrayDefinition
    {
        public override Type ClrType { get => typeof(string); internal set { } }
        public WCharArrayDefinition(int? length) : base(BasicType.FromClrType(typeof(char)), length)
        {
        }

        public override BinaryTemplateVariable CreateInstance(BinaryTemplateContext context, IBinaryTemplateScope scope, params object[] args)
        {
            string bytes;
            var state = context.SaveState();
            if (!Length.HasValue)
                bytes = context.ReadWString();
            else
                bytes = context.ReadWChars(Length.Value);
            return new BinaryTemplateWStringVariable(bytes, this, state);
        }

        public override string ToString()
        {
            if (!Length.HasValue)
                return "wstring";
            return base.ToString();
        }
    }


    public class CompoundDefinition : TypeDefinition, ILexicalScope
    {

        public DefinitionCollection Children { get; }

        public List<ParameterExpression> Parameters { get; }

        public Expression Context => Parameters[0];

        public Expression ScopeArg => Parameters[1];

        public List<Expression> Statements { get; }

        Delegate CompiledFunction;

        protected int _size;
        protected int _bitSize;
        public override int? BitSize => _bitSize + _size * 8;

        public override int? Size => IsSimple ? _size + _bitSize / 8 + (_bitSize % 8 == 0 ? 0 : 1) : null;

        public override int RequiredArguments => Parameters.Count - 2;

        public override bool IsFixedSize => IsSimple && Size.HasValue;

        public bool IsSimple { get; protected set; }

        internal void AddDefinition(Expression expr)
        {

            if (expr is VariableDeclaration)
            {
                var var = expr as VariableDeclaration;
                if (var.Name != null && Children.Contains(var.Name) || !var.IsFixedSize)
                {
                    IsSimple = false;
                    return;
                }
                Children.Add(var);
                if (TypeKind == TypeKind.Union)
                {
                    if (var.IsBitfield)
                        _size = Math.Max(_bitSize, var.BitSize.Value);
                    else
                        _size = Math.Max(_size, var.Size.Value);

                    var.Parent = this;
                } else
                {
                    if (var.IsBitfield)
                        _bitSize = _bitSize + var.BitSize.Value;
                    else
                        _size += var.Size.Value;
                    var.Parent = this;
                }
                
            }
            else if (expr is BlockExpression)
            {
                foreach (var child in (expr as BlockExpression).Expressions)
                    AddDefinition(child);
            }
            else
            {
                IsSimple = false;
            }

        }


        internal void AddStatement(Expression expr)
        {
            Statements.Add(expr);
            AddDefinition(expr);
        }

        public CompoundDefinition(TypeKind deftype = TypeKind.Struct, string name = "") : base(name)
        {
            TypeKind = deftype;
            IsSimple = true;
            _size = 0;
            ClrType = typeof(BinaryTemplateScope);
            Statements = new List<Expression>();
            Children = new DefinitionCollection();
            Parameters = new List<ParameterExpression>();
            Parameters.Add(Expression.Parameter(typeof(BinaryTemplateContext), "$context"));
            Parameters.Add(Expression.Parameter(typeof(IBinaryTemplateScope), "$scope"));
            References = new List<VariableDeclaration>();
            IsComplete = false;
        }

        public Delegate Compile()
        {
            var lambda = Expression.Lambda(Expression.Block(Statements), Parameters);
            System.Console.WriteLine(lambda.ToString());
            return lambda.Compile();
        }




        public override BinaryTemplateVariable CreateInstance(BinaryTemplateContext context, IBinaryTemplateScope scope, params object[] args)
        {
            if (CompiledFunction == null)
                CompiledFunction = Compile();
            BinaryTemplateScope childscope = new BinaryTemplateScope(this, context.Position, TypeKind == TypeKind.Union);
            if (scope is BinaryTemplateScope btscope)
                btscope.BeginNewScope(childscope);
            
            var objects = new object[] { context, childscope }.Concat(args).ToArray();
            CompiledFunction.DynamicInvoke(objects);
            Array.Copy(objects, 2, args, 0, args.Length);
            return childscope;
        }

        public override BinaryTemplateVariable CreateLocalInstance(IBinaryTemplateScope scope, object initizlizer, params object[] args)
        {
            throw new NotSupportedException("Currently local structs are not supported.");
        }

        public override string ToString()
        {
            string typekind = TypeKind == TypeKind.Struct ? "struct" : "union";
            return $"{typekind} {Name}";

        }

        public override string ToString(string varname)
        {
            string typekind = TypeKind == TypeKind.Struct ? "struct" : "union";
            return $"{typekind} {Name} {varname}";

        }

        public ParameterExpression GetParameter(string name)
        {
            if (name == "$scope")
                return Parameters[1];
            else
            {
                var result = Parameters.Find(param => param.Name == name);
                if (result == null && Parent != null)
                    return Parent.GetParameter(name);
                return result;
            }
        }

        public ParameterExpression[] GetParameterList()
        {
            return Parameters.ToArray();
        }
    }


    public class FunctionDefinition : CompoundDefinition, ICallableFunction
    {
        public Expression Body { get; internal set; }
        public TypeDefinition ReturnType { get; }
        public List<VariableDeclaration> ParameterDeclaration { get; }

        public List<FunctionCallExpr> References { get; }

        Type ICallableFunction.ReturnType => ReturnType == null ? typeof(void) : ReturnType.LocalClrType;


        public Delegate CompiledFunction = null;
        public LambdaExpression LambdaExpression = null;


        public CallInfo GetCallInfo()
        {
            return new CallInfo(Parameters.Count);
        }

        public FunctionDefinition(string funcname, IList<VariableDeclaration> variables, TypeDefinition returntype)
        {
            TypeKind = TypeKind.Function;
            Name = funcname;
            ParameterDeclaration = new List<VariableDeclaration>(variables);
            ReturnType = returntype;
            References = new List<FunctionCallExpr>();
            
        }

        internal void CreateLambdaExpression()
        {
            foreach (var decl in ParameterDeclaration)
            {
                var type = decl.TypeDefinition.ClrType;
                if (decl.IsReference && type.IsValueType)
                    type = type.MakeByRefType();
                Parameters.Add(Expression.Parameter(type, decl.Name));
            }
            LambdaExpression = Expression.Lambda(Body, Parameters);
        }



        public Expression GetCallExpression(ILexicalScope scope, List<Expression> callarguments)
        {
            if (LambdaExpression == null)
                CreateLambdaExpression();
            var arguments = new List<Expression>() { scope.Context, scope.ScopeArg };
            arguments.AddRange(callarguments);

            if ((arguments == null ? 0 : arguments.Count) != LambdaExpression.Parameters.Count)
                throw new InvalidOperationException($"Function {Name} called with wrong numbers of arguments.");

            return RuntimeHelpers.GetArgumentConvertedExpression(LambdaExpression.Parameters, arguments, converted => Expression.Invoke(LambdaExpression, converted));
        }

        public object Call(params object[] args)
        {
            if (CompiledFunction == null)
                CompiledFunction = LambdaExpression.Compile();
            return CompiledFunction.DynamicInvoke(args);
        }


        public override string ToString()
        {
            var paramlist = string.Join(", ", ParameterDeclaration.Select(var => var.ToString()));
            return $"{ReturnType} {Name}({paramlist})";
        }
    }

        public class BinaryTemplateRootDefinition : CompoundDefinition, IBinaryTemplateScope
        {
        public Dictionary<string, FunctionDefinition> Functions { get; }
        public TypeDefinitionCollection Typedefs { get; }
        public VariableCollection EnumConsts { get; }

        public BinaryTemplate Runtime { get; }
        public List<BinaryTemplateError> Errors { get; internal set; }

        public Action<BinaryTemplateContext, IBinaryTemplateScope> CompiledFunction { get; private set; }

        public IEnumerable<string> Keys => (EnumConsts as IDictionary<string,BinaryTemplateVariable>).Keys;

        public IEnumerable<BinaryTemplateVariable> Values => (EnumConsts as IDictionary<string, BinaryTemplateVariable>).Values;

        public int Count => EnumConsts.Count;

        public BinaryTemplateVariable this[string key] => EnumConsts[key];

        public BinaryTemplateRootDefinition(BinaryTemplate runtime) : base()
        {
            Runtime = runtime;
            Functions = new Dictionary<string, FunctionDefinition>();
            Typedefs = new TypeDefinitionCollection();
            EnumConsts = new VariableCollection(false);
            Errors = new List<BinaryTemplateError>();
        }

        public BinaryTemplateRootDefinition(BinaryTemplate runtime, IList<BinaryTemplateError> errors) : this(runtime)
        {
            Errors.AddRange(errors);
        }
        public TypeDefinition FindOrDeclareType(TypeKind deftype, string text, bool isdefinition)
        {
            TypeDefinition type;
            if (text != null && TryGetType(text, out type))
            {
                if (type.UnderlyingType.TypeKind != deftype || type.IsComplete && isdefinition)
                    throw new Exception($"Type {text} already defined.");
                return type;
            }
            else
            {
                var newtype = new CompoundDefinition(deftype, text);
                Typedefs.Add(newtype);
                return newtype;
            }
            
        }

        public TypeDefinition FindType(TypeKind deftype, string text)
        {
            TypeDefinition type;
            if (text != null)
                throw new ArgumentNullException(text);
            if (TryGetType(text, out type))
            {
                if (type.UnderlyingType.TypeKind != deftype)
                    throw new Exception($"Type {text} defined as different kind.");
                return type;
            }
            else
                throw new KeyNotFoundException($"Type {text} not found.");
        }
        public bool TryGetType(string name, out TypeDefinition type)
        {
            type = TypeDefinition.FromString(name);
            if (type == null)
            {
                var result = Runtime.BuiltinTypes.TryGetValue(name, out type);
                if (!result)
                    return Typedefs.TryGetValue(name, out type);
            }
            return true;
        }

        public bool TryGetConsts(string name, out BinaryTemplateVariable var)
        {
            var result = Runtime.BuiltinConstants.TryGetValue(name, out var);
            if (!result)
                return EnumConsts.TryGetValue(name, out var);
            return true;
        }

        public new Action<BinaryTemplateContext, IBinaryTemplateScope> Compile()
        {
            if (CompiledFunction != null)
                return CompiledFunction;
            
            var lambda = Expression.Lambda<Action<BinaryTemplateContext, IBinaryTemplateScope>>(Expression.Block(Statements), Parameters);

            CompiledFunction = lambda.Compile();
            return CompiledFunction;
        }

        public void Execute(BinaryTemplateContext context, IBinaryTemplateScope scope)
        {
            Compile()(context, scope);
        }

        public BinaryTemplateVariable GetVariable(string name)
        {
            if (TryGetConsts(name, out BinaryTemplateVariable var))
                return var;
            throw new MemberAccessException($"Cannot find const {name}");
        }

        public BinaryTemplateVariable GetVariable(int index)
        {
            throw new MemberAccessException($"Cannot find const in index {index}");
        }

        public bool TryGetFunctions(string funcname, out ICallableFunction func)
        {
            var result = Runtime.BuiltinFunctions.TryGetValue(funcname, out func);
            if (!result)
            {
                FunctionDefinition funcdef;
                result = Functions.TryGetValue(funcname, out funcdef);
                func = funcdef;
                return result;
            }
                
            return true;
        }

        public bool AddConst(ConstVariable constvar)
        {
            if (EnumConsts.Contains(constvar))
                return false;
            EnumConsts.Add(constvar);
            return true;
        }


        public void DefineType(TypeDefinition deftype)
        {
            if (deftype == null)
                throw new ArgumentNullException(nameof(deftype));
            if (deftype.Name == null)
                throw new ArgumentNullException(nameof(deftype.Name));
            if (Typedefs.TryGetValue(deftype.Name, out TypeDefinition type))
            {
                if (deftype == type)
                    return;
                else if (deftype.UnderlyingType.TypeKind == type.UnderlyingType.TypeKind)
                {
                    if (type.UnderlyingType.IsComplete && deftype.UnderlyingType.IsComplete)
                        throw new Exception($"Type {type.Name} is already defined.");
                    else if (deftype.IsComplete)
                    {
                        Typedefs.Remove(type);
                        Typedefs.Add(deftype);
                        if (type.References != null)
                        {
                            deftype?.References.AddRange(type.References);
                            foreach (var typeref in type.References)
                            {
                                typeref.ElementType = deftype;
                            }
                        }
                            
                    }
                }
                else
                    throw new Exception($"Type {type.Name} defined as different kind.");
            }
            else
                Typedefs.Add(deftype);
        }

        public bool ContainsKey(string key)
        {
            return EnumConsts.Contains(key);
        }

        public bool TryGetValue(string key, [MaybeNullWhen(false)] out BinaryTemplateVariable value)
        {
            return EnumConsts.TryGetValue(key, out value);
        }

        public IEnumerator<KeyValuePair<string, BinaryTemplateVariable>> GetEnumerator()
        {
            return (EnumConsts as IDictionary<string,BinaryTemplateVariable>).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return EnumConsts.GetEnumerator();
        }
    }


}
