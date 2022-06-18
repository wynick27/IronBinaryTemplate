using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace IronBinaryTemplate
{


    /// <summary>
    /// Abstract class for template variable.
    /// </summary>
    public abstract class BinaryTemplateVariable
    {
        public virtual TypeDefinition Type { get; internal set; }
        public string Name { get; set; }
        public abstract long? Start { get; }

        public abstract long? Size { get; }
        public virtual long? BitSize => Size.HasValue ? Size.Value * 8 : null;
        public abstract object Value { get; set; }


        public virtual BinaryTemplateContext Context { get => Parent?.Context; }
        public BinaryTemplateScope Parent { get; internal set; }
        public CustomAttributeCollection CustomAttributes { get; internal set; }

        public bool TryGetAttribute(string name, out CustomAttribute attr)
        {
            if (CustomAttributes != null && CustomAttributes.TryGetValue(name, out attr))
                return true;
            if (Type != null && Type.CustomAttributes != null && Type.CustomAttributes.TryGetValue(name, out attr))
                return true;
            attr = null;
            return false;
        }
    }

    /// <summary>
    /// Represents a variable of primitive type.
    /// </summary>
    public class ScalarVariable : BinaryTemplateVariable
    {
        private BinaryTemplateReaderState state;
        public override long? Start => state?.Position;
        public override long? Size => Type?.Size;

        public override long? BitSize => Type?.BitSize;

        BinaryTemplateContext _context;
        public override BinaryTemplateContext Context { get => _context; }
        public override object Value { get => Context.ReadBasicType(Type, state); set => throw new NotImplementedException(); }

        public ScalarVariable(BinaryTemplateContext context, TypeDefinition type)
        {
            if (!type.Size.HasValue)
                throw new ArgumentException("Scalar must have fixed size.");
            Type = type;
            state = context.MapType(type);
            _context = context;
        }

        public override string ToString()
        {
            return Value?.ToString();
        }

    }

    /// <summary>
    /// Wraps the CLR array.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class BinaryTemplateWrapperArray<T> : BinaryTemplateVariable, IBinaryTemplateArray
    {
        protected BinaryTemplateReaderState state;
        public override long? Start => state?.Position;
        public T[] Array { get; protected set; }
        public TypeDefinition ElementType;

        public override long? Size => Type.Size ?? Array?.Length * ElementType.Size;

        public int Length => Array.Length;

        public override object Value { get => Array; set => throw new NotImplementedException(); }


        public BinaryTemplateWrapperArray(BinaryTemplateContext context, TypeDefinition type, BinaryTemplateReaderState state)
        {
            Type = type;
            this.state = state;
            if (type is ArrayDefinition arraydef)
                ElementType = arraydef.Element;
            else
                throw new ArgumentException("Type must be an array.");
            if (!type.Size.HasValue)
                throw new ArgumentException("Type must have fixed size.");
            
        }

        public BinaryTemplateWrapperArray(T[] array, TypeDefinition type, BinaryTemplateReaderState state)
        {
            Type = type;
            this.state = state;
            Array = array;
            if (type is ArrayDefinition arraydef)
                ElementType = arraydef.Element;
            else
                throw new ArgumentException("Type must be an array.");
        }

        public BinaryTemplateVariable GetVariable(int index)
        {
            return new ArrayProxyVariable<T>(this, index);
        }
    }

    public class ArrayProxyVariable<T> : BinaryTemplateVariable
    {
        int _index;
        public override long? Start => parent.Start + _index * parent.ElementType.Size.Value;
        public override long? Size => parent.ElementType.Size.Value;

        BinaryTemplateWrapperArray<T> parent;
        public override object Value { get => parent.Array.GetValue(_index); set => parent.Array.SetValue(value,_index); }

        public ArrayProxyVariable(BinaryTemplateWrapperArray<T> parent, int index)
        {
            this.parent = parent;
            this._index = index;
        }

    }

    /// <summary>
    /// Represents a local variable, can be complex type.
    /// </summary>
    public class LocalVariable : BinaryTemplateVariable
    {
        object value;

        public LocalVariable(string name, TypeDefinition typeDefinition, object value = null)
        {
            Name = name;
            Type = typeDefinition;
            Value = value;
        }

        public override long? Start => null;
        public override long? Size => null;
        public override object Value { get => value; set => this.value = value; }
    }

    public class LocalVariable<T> : BinaryTemplateVariable
    {
        T value;

        public LocalVariable(string name, TypeDefinition typeDefinition, object value = null)
        {
            Name = name;
            Type = typeDefinition;
            Value = value;
        }

        public override long? Start => null;
        public override long? Size => null;
        public override object Value { get => value; set => this.value = (T)value; }
    }

    /// <summary>
    /// Represents a const variable.
    /// </summary>
    public class ConstVariable : BinaryTemplateVariable
    {
        object value;

        public ConstVariable(string name, TypeDefinition typeDefinition, object value)
        {
            Name = name;
            Type = typeDefinition;
            this.value = value;
        }
        public override long? Start => null;
        public override long? Size => null;
        public override object Value { get => value; set => throw new InvalidOperationException("Cannot modify const variable."); }
    }

    /// <summary>
    /// Basic array, inits every element on creation.
    /// </summary>
    public class BinaryTemplateArray : BinaryTemplateScope, IBinaryTemplateArray
    {

        private BinaryTemplateReaderState _state;
        protected BinaryTemplateContext _context;
        public override BinaryTemplateContext Context  => _context;

        public int Length => Variables.Count;

        public BinaryTemplateArray(BinaryTemplateContext context, TypeDefinition type, BinaryTemplateReaderState state) : base(type, state.Position, false)
        {
            _context = context;
            _state = state;
        }

        public virtual object this[int index]
        {
            get => Variables[index].Value;
            set => Variables[index].Value = value;
        }


        public void AddVariable(BinaryTemplateVariable value)
        {
            value.Parent = this;
            value.Name = $"{this.Name}[{Variables.Count}]";
            Variables.Add(value);
            
            this._size = _size + (long)value.Size;
        }

        public BinaryTemplateVariable GetVariable(int index)
        {
            return Variables[index];
        }
    }

    /// <summary>
    /// Creates array element lazyily when accessed through index. Used with fixed size structure array or optimize = true.
    /// </summary>
    public class BinaryTemplateLazyArray : BinaryTemplateVariable, IBinaryTemplateArray
    {

        long _elemSize = 0;
        int _length = 0;
        long _start;
        BinaryTemplateVariable _cache = null;
        int _cachindex = -1;
        object[] _arguments = null;
        protected BinaryTemplateContext _context;

        public override long? Size => _elemSize * _length;

        public int Length => _length;

        public override long? Start => _start;

        public override object Value { get => this; set => throw new NotImplementedException(); }

        public BinaryTemplateLazyArray(BinaryTemplateContext context, TypeDefinition type, long start, int length, object[] args)
        {
            Type = type;
            _start = start;
            _length = length;
            _context = context;
            _arguments = args;
            if (type.Size.HasValue)
            {
                _elemSize = type.Size.Value / length;
                context.MapType(type);
            }
            else
            {
                _cache = (Type as ArrayDefinition).Element.CreateInstance(_context, this.Parent, _arguments);
                _elemSize = _cache.Size.Value;
                context.SkipBytes(_cache.Size.Value *  (length-1));
            }
        }

        public BinaryTemplateVariable GetVariable(int index)
        {

            if (index == _cachindex && _cache != null)
            {
                return _cache;
            }
            if (index >= _length)
                throw new IndexOutOfRangeException();
            long pos = Context.Position;
            Context.Position = _start + index * _elemSize;
            var value = (Type as ArrayDefinition).Element.CreateInstance(_context, this.Parent, _arguments);
            value.Name = $"{this.Name}[{index}]";
            value.Parent = this.Parent;
            _cache = value;
            Context.Position = pos;
            
            return value;
        }


    }

    public class BinaryTemplateScope : BinaryTemplateVariable, IDynamicMetaObjectProvider, IBinaryTemplateScope
    {

        public VariableCollection Variables { get; protected set; }



        bool _isUnion;

        protected long _size;

        protected long _bitSize;

        protected long _start;
        public override long? Size => _size + _bitSize/8 + ((_bitSize % 8 == 0) ? 0 : 1);

        public override long? BitSize => _size * 8 + _bitSize;

        public override long? Start => _start;

        public override object Value { get => this; set => throw new NotImplementedException(); }

        public BinaryTemplateScope(TypeDefinition type, long start, bool isUnion)
        {
            Type = type;
            _isUnion = isUnion;
            _start = start;
            Variables = new VariableCollection();
        }

        string _newvarname;
        BinaryTemplateVariable _newvar;

        internal void BeginNewVariable(string name)
        {
            _newvarname = name;
            if (_isUnion)
            {
                Context.Position = Start.Value;
            }
        }

        internal void BeginNewScope(BinaryTemplateVariable value)
        {
            _newvar = value;
            _newvar.Name = _newvarname;
            _newvar.Parent = this;

        }

        internal void EndNewVariable(BinaryTemplateVariable value)
        {
            value.Name = _newvarname;
            SetVariable(value);
            _newvarname = null;
            _newvar = null;
            if (_isUnion)
            {
                Context.Position = Start.Value + _size;
            }
        }
        public virtual void SetVariable(BinaryTemplateVariable value)
        {
            value.Parent = this;
            Variables.Add(value);
            if (value.Size.HasValue)
            {
                if (_isUnion)
                {
                    this._size = Math.Max(_size, (long)value.Size);
                }
                    
                else
                {
                    if (value.BitSize != value.Size * 8)
                        this._bitSize = _bitSize + (long)value.BitSize;
                    else
                        this._size = _size + (long)value.Size;
                }
                    
                
            } else
            {
                if (value is LocalVariable)
                    return;
                throw new ArgumentException("Variable has no size.");
            }
            

        }

        public virtual bool TryGetVariable(string name, out BinaryTemplateVariable var)
        {
            
            if (name == "this")
            {
                var = this;
                return true;
            }
            if (_newvar != null && name == _newvar.Name)
            {
                var = _newvar;
                return true;
            }
            if (Variables.TryGetValue(name, out var))
                return true;
            else if (Parent != null)
                return Parent.TryGetVariable(name, out var);
            return false;
        }

        public virtual BinaryTemplateVariable GetVariable(string name)
        {
            TryGetVariable(name, out BinaryTemplateVariable var);
            return var;
        }

        protected object GetVariableValue(string name)
        {
            if (!TryGetVariable(name, out BinaryTemplateVariable var))
                throw new MemberAccessException(name);
            //Console.WriteLine($"{Context.Position} GetVariable {name} = {var.Value}");

            if (var is IBinaryTemplateScope || var is IBinaryTemplateArray)
                return var;
            else
                return var.Value;
        }


        private void SetVariableValue(string name, object value)
        {
            if (!TryGetVariable(name, out BinaryTemplateVariable var))
                throw new MemberAccessException(name);
           // Console.WriteLine($"{Context.Position} SetVariable {name} = {value}");
            var.Value = value;
        }

        public DynamicMetaObject GetMetaObject(Expression parameter)
        {
            return new BinaryTemplateMetaObject(this, parameter);
        }

        public virtual ICallableFunction GetFunction(string name)
        {
            if (Parent != null)
                return Parent.GetFunction(name);
            return null;
        }

        public object this[string name]
        {
            get => GetVariableValue(name);
            set => SetVariableValue(name,value);
        }

    }

    public class BinaryTemplateMetaObject : DynamicMetaObject
    {
        private static PropertyInfo indexer;
        static BinaryTemplateMetaObject()
        {
            foreach (PropertyInfo pi in typeof(BinaryTemplateScope).GetProperties())
            {
                if (pi.GetIndexParameters().Length > 0)
                {
                    indexer = pi;
                }
            }
        }

        public BinaryTemplateMetaObject(BinaryTemplateScope scope, Expression parameter) : base(parameter, BindingRestrictions.Empty, scope)
        {
        }

        public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
        {
            Expression target;
            if ((base.Value as BinaryTemplateScope).TryGetVariable(binder.Name,out BinaryTemplateVariable variable))
                target = Expression.MakeIndex(Expression.Convert(this.Expression, typeof(BinaryTemplateScope)), indexer, new[] { Expression.Constant(binder.Name) });
            else
                target = Expression.Throw(Expression.Constant(new ArgumentException($"Cannot find member {binder.Name}.")));


            var restrictions = BindingRestrictions
                .GetInstanceRestriction(this.Expression, base.Value);
            return new DynamicMetaObject(target, restrictions);
        }

    }

    public class BinaryTemplateRootScope : BinaryTemplateScope
    {
        public bool HasErrors => Errors.Count == 0;
        public List<Exception> Errors { get; }
        
        public override BinaryTemplateContext Context { get; }

        public override bool TryGetVariable(string name, out BinaryTemplateVariable var)
        {
            var result = base.TryGetVariable(name, out var);
            if (result)
                return true;
            BinaryTemplateRootDefinition rootdef = Type as BinaryTemplateRootDefinition;
            return rootdef.TryGetConsts(name, out var);
        }


        public override ICallableFunction GetFunction(string name)
        {
            BinaryTemplateRootDefinition rootdef = Type as BinaryTemplateRootDefinition;
            if (rootdef.TryGetFunctions(name, out ICallableFunction func))
                return func;
            if (Parent != null)
                return Parent.GetFunction(name);
            return null;
        }

        public BinaryTemplateRootScope(BinaryTemplateContext context, BinaryTemplateRootDefinition type) : base(type, 0, false)
        {
            Type = type;
            Context = context;
            Errors = new List<Exception>();
        }
    }


    public class BinaryTemplateVariableScope : IBinaryTemplateScope
    {
        public BinaryTemplateVariable Variable { get; }
        public Dictionary<string,object> Arguments { get; }
        public BinaryTemplateVariableScope(BinaryTemplateVariable var)
        {
            Variable = var;
            Arguments = new Dictionary<string, object>();
        }
        public BinaryTemplateVariable GetVariable(string name)
        {
            if (name == "this")
                return Variable;
            else if (Arguments.TryGetValue(name, out object obj))
                return new LocalVariable(name, TypeDefinition.FromClrType(obj.GetType()), obj);
            else if (Variable is IBinaryTemplateScope btscope)
            {
                var variable = btscope.GetVariable(name);
                if (variable != null)
                    return variable;
            }
            return Variable.Parent.GetVariable(name);
        }
    }

    public class BinaryTemplateDuplicatedArray : BinaryTemplateVariable, IBinaryTemplateArray
    {
        public VariableCollection Variables { get; protected set; }
        BinaryTemplateVariable LastVariable;
        public override long? Start => LastVariable.Start;

        public override long? Size => LastVariable.Size;

        public int Length => Variables.Count;

        public override object Value { get => LastVariable.Value; set => LastVariable.Value = value; }


        public BinaryTemplateDuplicatedArray()
        {
            Variables = new VariableCollection();
        }
        public void AddVariable(BinaryTemplateVariable value)
        {
            value.Name = $"{this.Name}[{Variables.Count}]";
            Variables.Add(value);
            LastVariable = value;
        }

        public BinaryTemplateVariable GetVariable(int index)
        {
            return Variables[index];
        }
    }

    public class BinaryTemplateWStringVariable : BinaryTemplateWrapperArray<char>
    {
        string value;
        public override object Value { get => value; set => throw new NotImplementedException(); }


        public BinaryTemplateWStringVariable(char[] array, TypeDefinition type, BinaryTemplateReaderState state)
            :base(array,type, state)
        {
            value = array.ToString();
        }

        public BinaryTemplateWStringVariable(string str, TypeDefinition type, BinaryTemplateReaderState state)
            : base(str.ToCharArray(), type, state)
        {
            value = str;
        }
    }


    public class BinaryTemplateStringVariable : BinaryTemplateWrapperArray<byte>
    {

        public override object Value { get => new BinaryTemplateString(Array); set => throw new NotImplementedException(); }


        public BinaryTemplateStringVariable(byte[] array, TypeDefinition type, BinaryTemplateReaderState state)
            : base(array, type, state)
        {

        }

    }
    public class BinaryTemplateString
    {
        byte[] data;
        Encoding encoding;

        public static implicit operator BinaryTemplateString(byte[] str)
        {
            return new BinaryTemplateString(str);
        }

        public static implicit operator byte[](BinaryTemplateString str)
        {
            return str.data;
        }

        public static implicit operator BinaryTemplateString(sbyte[] str)
        {
            return new BinaryTemplateString((object)str as byte[]);
        }

        public static implicit operator sbyte[](BinaryTemplateString str)
        {
            return (object)str.data as sbyte[];
        }

        public static implicit operator string(BinaryTemplateString str)
        {
            return str.ToString();
        }

        public BinaryTemplateString(byte[] data, Encoding encoding = null)
        {
            this.data = data;
            this.encoding = encoding == null ? Encoding.UTF8 : encoding;
        }
        public BinaryTemplateString(string str, Encoding encoding = null)
        {
            this.encoding = encoding == null ? Encoding.UTF8 : encoding;
            this.data = this.encoding.GetBytes(str);
        }
        public static BinaryTemplateString operator +(BinaryTemplateString s1, byte b)
        {
            var newstr = new byte[s1.data.Length + 1];
            s1.data.CopyTo(newstr, 0);
            newstr[s1.data.Length] = b;
            return new BinaryTemplateString(newstr, s1.encoding);
        }
        public static BinaryTemplateString operator +(BinaryTemplateString s1, byte[] s2)
        {
            var newstr = new byte[s1.data.Length + s2.Length];
            s1.data.CopyTo(newstr, 0);
            s2.CopyTo(newstr, s1.data.Length);
            return new BinaryTemplateString(newstr, s1.encoding);
        }

        public static BinaryTemplateString operator +(BinaryTemplateString s1, BinaryTemplateString s2)
        {
            return s1 + s2.data;
        }
        public static BinaryTemplateString operator +(byte[] s1, BinaryTemplateString s2)
        {
            var newstr = new byte[s1.Length + s2.data.Length];
            s1.CopyTo(newstr, 0);
            s2.data.CopyTo(newstr, s1.Length);
            return new BinaryTemplateString(newstr, s2.encoding);
        }


        public static bool operator ==(BinaryTemplateString s1, BinaryTemplateString s2)
        {
            if (ReferenceEquals(s1, s2))
                return true;
            if (ReferenceEquals(s1, null))
                return false;
            if (ReferenceEquals(s2, null))
                return false;
            return s1.StrCmp(s2.data);
        }

        public static bool operator !=(BinaryTemplateString s1, BinaryTemplateString s2)
            => !(s1 == s2);

        public static bool operator ==(BinaryTemplateString s1, byte[] s2)
        {
            if (ReferenceEquals(s1, s2))
                return true;
            if (ReferenceEquals(s1, null))
                return false;
            if (ReferenceEquals(s2, null))
                return false;
            return s1.StrCmp(s2);
        }

        public static bool operator !=(BinaryTemplateString s1, byte[] s2)
            => !(s1 == s2);

        public bool StrCmp(byte[] other)
        {
            if (data.Length < other.Length && other[data.Length - 1] != 0)
                return false;
            if (data.Length > other.Length && data[other.Length - 1] != 0)
                return false;

            for (int i=0;i<Math.Min(data.Length,other.Length);i++)
            {
                if (data[i] != other[i])
                    return false;
            }
            return true;
        }

        public sbyte this[int index]
        {
            get => (sbyte) data[index];
            set => data[index] = (byte)value;
        }
        public override string ToString()
        {
            return Encoding.UTF8.GetString(data);
        }
    }
}
