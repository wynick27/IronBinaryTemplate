using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace IronBinaryTemplate
{

    public class BTNode : Expression
    {
        public override ExpressionType NodeType => ExpressionType.Extension;
        public override Type Type => typeof(void);
        public override bool CanReduce => true;

        public SourceSpan SourceSpan { get; init; }
    }


    public enum ValueAccess
    {
        ByValue,
        Reference,
        Wrapper,
    }
    public interface ILValue
    {
        ValueAccess AccessMode { get; set; }

        bool IsWritable { get; }

        Expression GetAssignmentExpression(Expression valueexpr);

        List<Expression> GetPathExpressions();
    }

    public abstract class Expr : BTNode
    {
        public override Type Type => typeof(object);
        public virtual object Eval(IBinaryTemplateScope scope)
        {
            if (CompiledFunction == null)
            {
                 Compile();
            }
            var allarguments = new List<object>() { scope };
            return CompiledFunction(scope);
        }

        public ILexicalScope LexicalScope { get; internal set; }

        private Func<IBinaryTemplateScope,object> CompiledFunction;


        protected void Compile()
        {
            CompiledFunction = Expression.Lambda<Func<IBinaryTemplateScope, object>>(this, LexicalScope.ScopeParam).Compile(true);
        }
        
    }

    public abstract class Stmt : BTNode
    {
        public override Type Type => typeof(void);

    }

    public class ConstExpr : Expr
    {
        public ConstExpr(object value)
        {
            Value = value;
        }

        public object Value { get; }
        public override object Eval(IBinaryTemplateScope scope)
        {
            return Value;
        }

        public override Type Type => Value.GetType();

        public override Expression Reduce()
        {
            return Expression.Constant(Value);
        }

    }

    public class VariableAccessExpr : Expr, ILValue
    {
        public string VariableName { get; internal set; }

        public VariableAccessExpr(string variable, ILexicalScope scope)
        {
            VariableName = variable;
            LexicalScope = scope;
        }

        public ValueAccess AccessMode { get; set; }

        public Expression StaticBoundExpression => LexicalScope.GetParameter(VariableName);

        public override Type Type => AccessMode == ValueAccess.ByValue ? typeof(object) : StaticBoundExpression?.Type ?? typeof(object);

        public bool IsWritable => StaticBoundExpression != null && StaticBoundExpression.Reduce() is ParameterExpression;

        public List<Expression> GetPathExpressions()
        {
            return new List<Expression>() { Expression.Constant(VariableName) };
        }

        public override Expression Reduce()
        {
            var boundExpression = StaticBoundExpression;
            if (boundExpression != null)
                return AccessMode == ValueAccess.ByValue ? Expression.Convert(boundExpression,typeof(object)) : boundExpression;
            if (AccessMode != ValueAccess.Wrapper)
                return Expression.MakeIndex(LexicalScope.GetParameter("this"), typeof(IBinaryTemplateScope).GetProperties().First(x => x.GetIndexParameters().Length > 0),
                new Expression[] { Expression.Constant(VariableName) });
            else
                return Expression.Call(LexicalScope.GetParameter("this"), typeof(IBinaryTemplateScope).GetMethod("GetVariable"),
                new Expression[] { Expression.Constant(VariableName) });
        }

        public Expression GetAssignmentExpression(Expression valueexpr)
        {
            var result = Reduce();
            return Expression.Assign(result, RuntimeHelpers.DynamicConvert(valueexpr, result.Type));
        }

        public override object Eval(IBinaryTemplateScope scope)
        {
            return scope[this.VariableName];
        }

    }


    public class MemberAccessExpr : Expr, ILValue
    {
        public new Expr Variable { get; internal set; }

        public string Member { get; internal set; }
        public MemberAccessExpr(Expr variable, string member)
        {
            Variable = variable;
            Member = member;
            LexicalScope = Variable.LexicalScope;
        }
        public ValueAccess AccessMode { get; set; }

        public bool IsWritable => false;

        public override Expression Reduce()
        {
            if (AccessMode != ValueAccess.Wrapper)
                return Expression.Dynamic(new BTGetMemberBinder(Member, false), typeof(object), Variable);
            else
                return Expression.Call(Expression.Convert(Variable, typeof(IBinaryTemplateScope)), typeof(IBinaryTemplateScope).GetMethod("GetVariable"),
                new Expression[] { Expression.Constant(Member) });
        }

        public List<Expression> GetPathExpressions()
        {
            var exprs = (Variable as ILValue).GetPathExpressions();
            exprs?.Add(Expression.Constant(Member));
            return exprs;
        }

        public Expression GetAssignmentExpression(Expression valueexpr)
        {
            return Expression.Dynamic(new BTSetMemberBinder(Member, false), typeof(object), Variable, valueexpr);

        }

    }

    public class ArrayAccessExpr : Expr, ILValue
    {
        public new Expr Variable { get; internal set; }

        public Expr Index { get; internal set; }

        public ValueAccess AccessMode { get; set; }

        public bool IsWritable => false;

        public ArrayAccessExpr(Expr variable, Expr index)
        {
            Variable = variable;
            Index = index;
            LexicalScope = Variable.LexicalScope;
        }
        public override Expression Reduce()
        {
            if (AccessMode != ValueAccess.Wrapper)
                return Expression.Dynamic(new BTGetIndexrBinder(new CallInfo(1)), typeof(object), Variable, Index);
            else
                return Expression.Call(Expression.Convert(Variable, typeof(IBinaryTemplateArray)), typeof(IBinaryTemplateArray).GetMethod("GetVariable"),
                new Expression[] { RuntimeHelpers.DynamicConvert(Index, typeof(int)) });
        }

        public List<Expression> GetPathExpressions()
        {
            var exprs = (Variable as ILValue).GetPathExpressions();
            exprs?.Add(Expression.Dynamic(new BTConvertBinder(typeof(int), false), typeof(int), Index));
            return exprs;
        }

        public Expression GetAssignmentExpression(Expression valueexpr)
        {
            return Expression.Dynamic(new BTSetIndexBinder(new CallInfo(1)), typeof(object), Variable, Index, valueexpr);
        }

    }

    public class UnaryExpr : Expr
    {
        public bool IsAssignment => Operator.ToString().EndsWith("Assign");

        public Expression Oprand { get; }

        public ExpressionType Operator { get; }

        public ExpressionType UnderlyingOperation
        {
            get
            {
                var str = Operator.ToString();
                if (str.Contains("Increment"))
                    return ExpressionType.Add;
                else if (str.Contains("Decrement"))
                    return ExpressionType.Subtract;
                else
                    return Operator;
            }
        }

        public bool IsPreOperation => Operator.ToString().Contains("Pre");
        static Dictionary<string, ExpressionType> operatorMap = new Dictionary<string, ExpressionType>()
        {
            { "+", ExpressionType.UnaryPlus },
            { "-", ExpressionType.Negate },
            { "~", ExpressionType.Not },
            { "!", ExpressionType.Extension },
            { "++", ExpressionType.PreIncrementAssign },
            { "--", ExpressionType.PreDecrementAssign },
            { "_++", ExpressionType.PostIncrementAssign },
            { "_--", ExpressionType.PostDecrementAssign },
        };

        //AST.ExpressionType Operator;
        public UnaryExpr(string op, Expr oprand)
        {
            ExpressionType exprType;
            if (!operatorMap.TryGetValue(op, out exprType))
            {
                throw new ArgumentException($"Unknown operator {op}");
            }
            LexicalScope = oprand.LexicalScope;
            Operator = exprType;
            Oprand = oprand;
            
            //Body = Expression.MakeUnary(Operator, oprand, oprand.GetType());
        }

        public Expression Body { get; }

        public override ExpressionType NodeType => ExpressionType.Extension;

        public override Type Type => Reduce().Type;

        public override bool CanReduce => true;

        public override Expression Reduce()
        {
            if (IsAssignment)
            {
                var lvalue = Oprand as ILValue;

                if (lvalue.IsWritable)
                    return Expression.MakeUnary(Operator, Oprand.Reduce(), Oprand.Type);

                if (IsPreOperation)
                {
                    var tempexpr = Expression.Dynamic(new BTBinaryOperationBinder(UnderlyingOperation), typeof(object), Oprand, Expression.Constant(1));
                    return lvalue.GetAssignmentExpression(tempexpr);
                }
                else
                {
                    var tempexpr = Expression.Variable(Oprand.Type);
                    return Expression.Block(
                        new ParameterExpression[] { tempexpr },
                        Expression.Assign(tempexpr, Oprand),
                        lvalue.GetAssignmentExpression(Expression.Dynamic(new BTBinaryOperationBinder(UnderlyingOperation), typeof(object), tempexpr, Expression.Constant(1))),
                        tempexpr
                        );

                }


            }
            return Expression.Dynamic(new BTUnaryOperationBinder(Operator), typeof(object), Oprand);
        }

    }

    public class BinaryExpr : Expr
    {
        static Dictionary<string, ExpressionType> operatorMap = new Dictionary<string, ExpressionType>()
        {
            { "+",ExpressionType.Add },
            { "-",ExpressionType.Subtract },
            { "*",ExpressionType.Multiply },
            { "/",ExpressionType.Divide },
            { "%",ExpressionType.Modulo },
            { "<<",ExpressionType.LeftShift },
            { ">>",ExpressionType.RightShift },
            { "<",ExpressionType.LessThan },
            { ">",ExpressionType.GreaterThan },
            { "<=",ExpressionType.LessThanOrEqual },
            { ">=",ExpressionType.GreaterThanOrEqual },
            { "==",ExpressionType.Equal },
            { "!=",ExpressionType.NotEqual },
            { "&&",ExpressionType.AndAlso },
            { "||",ExpressionType.OrElse },
            { "&",ExpressionType.And },
            { "|",ExpressionType.Or },
            { "^",ExpressionType.ExclusiveOr },
            { "=",ExpressionType.Assign },
            { "+=",ExpressionType.AddAssign },
            { "-=",ExpressionType.SubtractAssign },
            { "*=",ExpressionType.MultiplyAssign },
            { "/=",ExpressionType.DivideAssign },
            { "%=",ExpressionType.ModuloAssign },
            { "<<=",ExpressionType.LeftShiftAssign },
            { ">>=",ExpressionType.RightShiftAssign },
            { "&=",ExpressionType.AndAssign },
            { "|=",ExpressionType.OrAssign },
            { "^=",ExpressionType.ExclusiveOrAssign },
        };

        public bool IsAssignment => Operator.ToString().EndsWith("Assign");
        public ExpressionType UnderlyingOperation => (IsAssignment && Operator != ExpressionType.Assign) ? (ExpressionType)Enum.Parse(typeof(ExpressionType), Operator.ToString().Replace("Assign", "")) : Operator;
        public Expression Left { get; }
        public Expression Right { get; }

        public ExpressionType Operator { get; }

        public Expression Body { get; protected set; }
        public BinaryExpr(ExpressionType op, Expr lhs, Expr rhs)
        {
            Left = lhs;
            Right = rhs;
            LexicalScope = lhs.LexicalScope;
            SetupBody(op, lhs, rhs);
        }

        public BinaryExpr(string op, Expr lhs, Expr rhs)
        {
            Left = lhs;
            Right = rhs;
            LexicalScope = lhs.LexicalScope;
            ExpressionType exprType;
            if (!operatorMap.TryGetValue(op, out exprType))
            {
                throw new ArgumentException($"Unknown operator {op}");
            }
            Operator = exprType;
            SetupBody(Operator, lhs, rhs);
            // Body = Expression.MakeBinary(Operator, lhs, rhs);
        }
        private void SetupBody(ExpressionType Operator, Expression lhs, Expression rhs)
        {
            if (rhs.Type != typeof(object))
                rhs = Expression.Convert(rhs, typeof(object));
            
            if (Operator == ExpressionType.AndAlso || Operator == ExpressionType.OrElse)
                //    Body = IfThenElse(,lhs,rhs);
                Body = Expression.MakeBinary(Operator, RuntimeHelpers.DynamicConvert(lhs, typeof(bool)), RuntimeHelpers.DynamicConvert(rhs, typeof(bool)));
            else if (IsAssignment)
            {
                var lvalue = lhs as ILValue;
                var rightvalue = rhs;
                if (IsAssignment && Operator != ExpressionType.Assign)
                {
                    rightvalue = Expression.Dynamic(new BTBinaryOperationBinder(UnderlyingOperation), typeof(object), lhs, rhs);
                }
                Body = lvalue.GetAssignmentExpression(rightvalue);
            }
            else
            {
                if (lhs.Type != typeof(object))
                    lhs = Expression.Convert(lhs, typeof(object));
                Body = Expression.Dynamic(new BTBinaryOperationBinder(Operator), typeof(object), lhs, rhs);
            }
                
        }

        public override Type Type => Body.Type;

        public override Expression Reduce()
        {
            return Body;
        }


    }

    public class Initializer : BTNode
    {
        public Expression Value { get; }
        public List<Initializer> Initializers { get; }
        public Initializer(Expression value)
        {
            Value = value;
        }
        public Initializer()
        {
            Initializers = new List<Initializer>();
        }

        public override Type Type => Initializers == null ? typeof(object) : typeof(object[]);

        public override Expression Reduce()
        {
            return Initializers == null ? RuntimeHelpers.EnsureObjectResult(Value) : Expression.NewArrayInit(typeof(object), Initializers);
        }
    }

    public class ConditionalExpr : Expr
    {
        public Expr Test { get; }
        public Expr TrueExpr { get; }
        public Expr FalseExpr { get; }

        public ConditionalExpr(Expr test, Expr trueexpr, Expr falseexpr)
        {
            this.Test = test;
            this.TrueExpr = trueexpr;
            this.FalseExpr = falseexpr;
            LexicalScope = Test.LexicalScope;
            if (LexicalScope == null)
                LexicalScope = TrueExpr.LexicalScope;
            if (LexicalScope == null)
                LexicalScope = FalseExpr.LexicalScope;
        }

        public override Expression Reduce()
        {
            return Expression.Condition(RuntimeHelpers.DynamicConvert(Test,typeof(bool)),Convert(TrueExpr,typeof(object)), Convert(FalseExpr,typeof(object)));
        }
    }

    public class CastExpr : Expr
    {
        public TypeDefinition TypeDefinition { get; }
        public Expr Expr { get; }

        public override Type Type => TypeDefinition.ClrType;

        public CastExpr(TypeDefinition typeDefinition, Expr expr)
        {
            TypeDefinition = typeDefinition;
            Expr = expr;
            LexicalScope = expr.LexicalScope;
        }

        public override Expression Reduce()
        {
            return RuntimeHelpers.DynamicConvert(Expr, TypeDefinition.ClrType);
        }
    }

    public class FunctionCallExpr : Expr
    {
        public ICallableFunction Function { get; internal set; }
        public List<Expression> Arguments { get; }
        public string Name { get;}

        public FunctionCallExpr(string funcname, ILexicalScope scope, List<Expression> exprs)
        {
            this.Name = funcname;
            this.Arguments = exprs ?? new List<Expression>();
            this.LexicalScope = scope;
        }

        public override Type Type => Function == null ? typeof(void) : Function.ReturnType;

        public override Expression Reduce()
        {
            if (Function == null)
            {
                Console.WriteLine($"Function {Name} not found.");
                return Expression.Block();
            }
            Debug.Assert(Type == Function.GetCallExpression(LexicalScope, Arguments).Reduce().Type);

            return Function.GetCallExpression(LexicalScope, Arguments);
        }
    }

    public interface ILexicalScope
    {
        ParameterExpression[] GetParameterList();

        ParameterExpression GetParameter(string name);
        ParameterExpression ScopeParam { get; }
        Expression Context { get; }
    }

    public enum AttributeKind
    {
        Literal,
        Function,
        Expression
    }


    public class CustomAttribute : ILexicalScope
    {
        public AttributeKind AttributeKind { get; internal set; }


        public virtual string AsName()
        {
            if (Expr is VariableAccessExpr varaccess)
                return varaccess.VariableName;
            return null;
        }

        public CustomAttribute(string name)
        {
            Name = name;
            ScopeParam = Expression.Parameter(typeof(IBinaryTemplateScope), "scope");
        }

        public virtual object Eval(BinaryTemplateVariable var)
        {
            return Expr.Eval(new BinaryTemplateVariableScope(var));
        }
        public virtual T Eval<T>(BinaryTemplateVariable var)
        {
            try
            {
                object obj = Eval(var);
                return RuntimeHelpers.ChangeType<T>(obj);
            }
            catch (Exception ex)
            {
            }
            
            return default(T);
        }

        public ParameterExpression[] GetParameterList()
        {
            return new[] { ScopeParam };
        }

        public ParameterExpression GetParameter(string name)
            => null;

        public string Name { get; }

        public Expr Expr { get; internal set; }

        public ParameterExpression ScopeParam { get; }

        public Expression Context => null;

        public ICallableFunction Function { get; internal set; }

    }


    public class VariableDeclaration : BTNode
    {

        public TypeDefinition ElementType { get; internal set; }
        public TypeDefinition TypeDefinition { get => GetTypeDefinition(); }
        public string Name { get; internal set; }
        public bool IsConst { get; internal set; }
        public bool IsReference { get; internal set; }
        public bool IsLocal { get; internal set; }
        public bool IsArray => ArrayDimensions == null ? false : ArrayDimensions.Count > 0;
        public bool IsBitfield => BitfieldExpression != null;

        public bool IsConstBitfield => BitfieldExpression is ConstExpr && (BitfieldExpression as ConstExpr).Type.IsPrimitive;
        public Initializer Initializer { get; internal set; }
        public CustomAttributeCollection CustomAttributes { get; internal set; }

        public List<Expression> Arguments = new List<Expression>();
        public List<Expression> ArrayDimensions = new List<Expression>();
        public Expression BitfieldExpression { get; internal set; }
        public override Type Type => typeof(BinaryTemplateVariable);

        public List<VariableAccessExpr> References { get; }

        public CompoundDefinition Parent;

        public bool IsConstArray
        {
            get
            {
                if (IsArray)
                {
                    return ArrayDimensions.All(arg => arg is ConstExpr && (arg as ConstExpr).Type.IsPrimitive);
                }
                return false;
            }
        }

        public bool IsFixedSize
        {
            get
            {
                return ElementType.IsFixedSize && (IsArray ? IsConstArray : true) && (IsBitfield ? IsConstBitfield : true);
            }
        }
        public int? Size
        {
            get
            {
                if (!IsFixedSize)
                    return null;
                if (IsArray)
                {
                    return ArrayDimensions.Aggregate(ElementType.Size, (size,arg) => size * (int)((arg as ConstExpr).Value));
                }
                return ElementType.Size;
            }
        }

        public int? BitSize
        {
            get
            {
                if (!IsFixedSize)
                    return null;
                var bitsize = IsBitfield ? (int)(BitfieldExpression as ConstExpr).Value : ElementType.BitSize;
                if (IsArray)
                {
                    return ArrayDimensions.Aggregate(bitsize, (size, arg) => size * (int)((arg as ConstExpr).Value));
                }
                return bitsize;
            }
        }

        public Type ClrType
        {
            get
            {
                var type = TypeDefinition.ClrType;
                if (IsReference)
                    return type.MakeByRefType();
                return type;
            }
        }

        public VariableDeclaration(TypeDefinition typedef)
        {
            ElementType = typedef;
        }

        protected TypeDefinition GetTypeDefinition(IList<int?> arguments = null)
        {
            var typedef = ElementType;
            if (IsArray)
            {
                if (IsConstArray)
                {
                    foreach (ConstExpr arraydimension in ArrayDimensions.Reverse<Expression>())
                    {
                        typedef = typedef.GetArrayType(RuntimeHelpers.ChangeType<int>(arraydimension.Value));
                    }
                }
                else if (arguments != null)
                {
                    foreach (var arraydimension in arguments.Reverse())
                    {
                        typedef = typedef.GetArrayType(arraydimension);
                    }
                }
                else
                {
                    foreach (var arraydimension in ArrayDimensions)
                    {
                        typedef = typedef.GetArrayType(null);
                    }
                    //throw new ArgumentNullException("arguments");
                }

            }
            else if (IsBitfield)
            {
                if (IsConstBitfield)
                {
                    typedef = typedef.GetBitfieldType(RuntimeHelpers.ChangeType<int>((BitfieldExpression as ConstExpr).Value));
                }
                else if (arguments != null)
                {
                    ;
                    typedef = typedef.GetBitfieldType(arguments[0].Value);
                }
                else
                {
                    throw new ArgumentNullException("arguments");
                }
            }
            return typedef;
        }

        public BinaryTemplateVariable CreateInstance(BinaryTemplateContext context, BinaryTemplateScope scope, IList<int?> arrayarguments, IList<object> arguments,object initializer = null)
        {
            var typedef = GetTypeDefinition(arrayarguments);
            scope.BeginNewVariable(Name);
            BinaryTemplateVariable variable;
            if (IsLocal)
            {
                variable = typedef.CreateLocalInstance(scope, initializer, arguments.ToArray());
            }
            else
                variable = typedef.CreateInstance(context, scope, arguments.ToArray());
            scope.EndNewVariable(variable);
            variable.CustomAttributes = this.CustomAttributes;
            return variable;

        }
        public override Expression Reduce()
        {
            List<Expression> arrayexprs = new List<Expression>();

            if (ArrayDimensions != null)
            {
                foreach (var expr in ArrayDimensions)
                {
                    if (expr == null)
                        arrayexprs.Add(Expression.Constant(null, typeof(int?)));
                    else
                        arrayexprs.Add(RuntimeHelpers.DynamicConvert(expr, typeof(int?)));
                }
            }
            var arraydimensions = Expression.NewArrayInit(typeof(int?), arrayexprs);
           
            
            int argumentCount = Arguments == null  ? 0 : Arguments.Count;
            CompoundDefinition compounddef = ElementType.UnderlyingType as CompoundDefinition;
            List<ParameterExpression> parameters = null;
            if (compounddef != null)
            {
                if (compounddef.Parameters.Count != argumentCount + 2)
                    throw new RuntimeError("Argument and parameter count mismatch in constructor.");
                parameters = compounddef.Parameters.Skip(2).ToList();
            }
            else if (argumentCount > 0)
                throw new RuntimeError("Only struct or union supports constructor.");

            return RuntimeHelpers.GetArgumentConvertedArrayExpression(parameters, Arguments, (converted) =>
              {
                  Expression initializerexpr = Initializer;

              if (!IsLocal && !IsConst && Initializer != null)
                          throw new RuntimeError("Template variables cannot have an initializer.");
              if (initializerexpr == null)
                      initializerexpr = Expression.Constant(null);

                  return Expression.Call(Expression.Constant(this), typeof(VariableDeclaration).GetMethod("CreateInstance"),
                      Parent.Context, Parent.ScopeParam, arraydimensions, converted, initializerexpr);
              });
        }

        internal VariableDeclaration Clone()
        {
            var newvar = MemberwiseClone() as VariableDeclaration;
            newvar.ElementType.References?.Add(newvar);
            return newvar;
        }

        public override string ToString()
        {
            return $"{ElementType} {Name}";
        }
    }


    public class IfStmt : Stmt
    {
        public Expression Condition { get; }

        public Expression TrueStmt { get; }

        public Expression FalseStmt { get; }
    }

    public class LoopStmt : Stmt
    {

        public LoopStmt(Expression condition, Expression loopBody, LabelTarget loopBreak, LabelTarget loopContinue)
        {
            Condition = condition;
            LoopBody = loopBody;
            LoopBreak = loopBreak;
            LoopContinue = loopContinue;
        }

        public Expression Condition { get; }

        public Expression LoopBody { get; }

        public LabelTarget LoopBreak { get; }
        public LabelTarget LoopContinue { get; }

    }


    public class WhileStmt : LoopStmt
    {
        public WhileStmt(Expression condition, Expression loopBody, LabelTarget loopBreak, LabelTarget loopContinue) : base(condition, loopBody, loopBreak, loopContinue)
        {
        }

        public override Expression Reduce()
        {
            return Expression.Loop(Expression.IfThenElse(RuntimeHelpers.DynamicConvert(Condition, typeof(bool)),
                LoopBody,
                Expression.Break(LoopBreak))
                    , LoopBreak, LoopContinue);
        }
    }
    public class DoWhileStmt : LoopStmt
    {
        public DoWhileStmt(Expression condition, Expression loopBody, LabelTarget loopBreak, LabelTarget loopContinue) : base(condition, loopBody, loopBreak, loopContinue)
        {
        }

        public override Expression Reduce()
        {
            return Expression.Loop(Expression.Block(
                    LoopBody,
                    Expression.IfThen(Expression.IsFalse(RuntimeHelpers.DynamicConvert(Condition, typeof(bool))),
                    Expression.Break(LoopBreak)))
                    , LoopBreak, LoopContinue);
        }
    }

    public class ForStmt : WhileStmt
    {
        public ForStmt(Expression forInit, Expression forCondition, Expression forUpdate, Expression loopBody, LabelTarget loopBreak, LabelTarget loopContinue) : base(forCondition, loopBody, loopBreak, loopContinue)
        {
            ForInit = forInit;
            ForUpdate = forUpdate;
        }

        public Expression ForInit { get; }

        public Expression ForUpdate { get; }

        public override Expression Reduce()
        {
            Expression forexpr = LoopBody;
            if (ForUpdate != null)
                forexpr = Expression.Block(LoopBody, ForUpdate);
            if (Condition != null)
                forexpr = Expression.IfThenElse(
                RuntimeHelpers.DynamicConvert(Condition, typeof(bool)), forexpr,
                Expression.Break(LoopBreak));
            forexpr = Expression.Loop(forexpr, LoopBreak, LoopContinue);
            if (ForInit != null)
                forexpr = Expression.Block(ForInit, forexpr);
            return forexpr;
        }
    }


    public class JumpStmt : Stmt
    {
        private Expression Body { get; }

        public override Type Type => Body.Type;

        public JumpStmt(Expression expression)
        {
            this.Body = expression;
        }

        public override Expression Reduce()
        {
            return Body;
        }

    }

    public class BreakStmt : JumpStmt
    {
        public BreakStmt(Expression expression) : base(expression)
        {
        }
    }

    public class ContinueStmt : JumpStmt
    {
        public ContinueStmt(Expression expression) : base(expression)
        {
        }
    }

    public class ReturnStmt : JumpStmt
    {
        public ReturnStmt(Expression gotoExpression) : base(gotoExpression)
        {
        }
    }
    public class SwitchStmt : Stmt
    {
        public Expr TestValueExpr { get; set; }
        public List<SwitchCaseStmt> SwitchCases { get; }
        public SwitchCaseStmt DefaultCase { get; }
        public Expression DefaultExpr { get; }


        public override ExpressionType NodeType => ExpressionType.Extension;

        public override Type Type => typeof(void);

        public override bool CanReduce => true;
        public bool IsAllConst
        {
            get
            {
                foreach (var caseexpr in SwitchCases)
                {
                    foreach (var casevalues in caseexpr.TestValues)
                    {
                        if (!(casevalues is ConstExpr))
                            return false;
                    }
                }
                return true;
            }
        }
        public bool HasFallThrough
        {
            get
            {
                return SwitchCases.Any(expr => expr.Fallthrough);
            }
        }
        public SwitchStmt(Expr expr, BlockExpression block, LabelTarget breakLabel)
        {
            TestValueExpr = expr;
            SwitchCases = new List<SwitchCaseStmt>();
            SwitchCaseStmt currentcase = null;

            foreach (var stmt in block.Expressions)
            {
                if (stmt is SwitchCaseStmt switchcaseexpr)
                {

                    SwitchCases.Add(switchcaseexpr);
                    if (currentcase != null)
                        currentcase.SetFallthrough(switchcaseexpr);
                    currentcase = switchcaseexpr;
                    if (DefaultCase == null && currentcase.IsDefault)
                        DefaultCase = currentcase;
                }
                else
                {
                    if (currentcase != null)
                        currentcase.Statements.Add(stmt);
                }
            }
            if (currentcase != null)
                currentcase.SetFallthrough(null);
            if (DefaultCase != null)
            {
                
                if (DefaultCase.TestValues.Count == 0)
                {
                    SwitchCases.Remove(DefaultCase);
                    DefaultExpr = DefaultCase.GetBodyExpr();
                }
                    
                else
                    DefaultExpr = Expression.Goto(DefaultCase.CaseLabel);
            }
        }

        public override Expression Reduce()
        {
            if (IsAllConst)
            {
                Type supertype = GetSuperType();
                if (supertype != null)
                {
                    return Expression.Switch(RuntimeHelpers.DynamicConvert(TestValueExpr, supertype), DefaultExpr, SwitchCases.Select(caseexpr => caseexpr.GetSwitchCaseExpression(supertype)).ToArray());
                }
            }
            return SwitchCases.Reverse<SwitchCaseStmt>().Aggregate(DefaultExpr, (elseexpr, caseexpr) =>
            elseexpr != null ? Expression.IfThenElse(RuntimeHelpers.DynamicConvert(caseexpr.GetTestExpr(TestValueExpr), typeof(bool)), caseexpr.GetBodyExpr(), elseexpr) :
            Expression.IfThen(RuntimeHelpers.DynamicConvert(caseexpr.GetTestExpr(TestValueExpr),typeof(bool)), caseexpr.GetBodyExpr()));
        }

        private Type GetSuperType()
        {
            Type supertype = null;
            foreach (var caseexpr in SwitchCases)
            {
                foreach (var casevalues in caseexpr.TestValues)
                {
                    if (!(casevalues is ConstExpr constexpr))
                        return null;
                    else if (supertype == null)
                        supertype = constexpr.Type;
                    else
                    {
                        supertype = RuntimeHelpers.LiftType(supertype, constexpr.Type);
                    }
                }
            }
            if (supertype == typeof(BinaryTemplateString))
                return typeof(string);
            return supertype;
        }
    }


    public class SwitchCaseStmt : Expression
    {
        public bool IsDefault;
        public List<Expr> TestValues { get; }
        public List<Expression> Statements { get; }

        private LabelTarget _caseLabel;


        public LabelTarget CaseLabel
        {
            get
            {
                if (_caseLabel == null)
                    _caseLabel = Expression.Label();
                return _caseLabel;
            }
        }

        public SwitchCaseStmt NextCase;

        public bool Fallthrough { get; private set; }

        public override Type Type => typeof(void);

        public override ExpressionType NodeType => ExpressionType.Extension;

        public SwitchCaseStmt()
        {
            TestValues = new List<Expr>();
            Statements = new List<Expression>();
        }

        public void SetFallthrough(SwitchCaseStmt nextcase)
        {
            Fallthrough = true;
            if (Statements.Count != 0 && Statements[^1] is JumpStmt)
            {
                Fallthrough = false;
            }
            else if (nextcase != null)
            {
                NextCase = nextcase;
            }
        }

        public Expression GetBodyExpr()
        {

            var exprs = new List<Expression>();
            if (_caseLabel != null)
                exprs.Add(Expression.Label(_caseLabel));
            exprs.AddRange(Statements);
            if (Fallthrough)
            {
                if (NextCase != null)
                    exprs.Add(Expression.Goto(NextCase.CaseLabel));
            }

            else
                exprs.RemoveAt(exprs.Count - 1);
            return Expression.Block(typeof(void),exprs);
        }

        public Expr GetTestExpr(Expr testexpr)
        {

            var exprs = TestValues.Select(testvalue => new BinaryExpr(ExpressionType.Equal, testexpr, testvalue));
            return exprs.Aggregate((expr1, expr2) => new BinaryExpr(ExpressionType.OrElse, expr1, expr2));
        }

        public SwitchCase GetSwitchCaseExpression(Type type = null)
        {
            var testvalues = TestValues;
            if (type != null)
            {
                testvalues = new List<Expr>();
                foreach (ConstExpr testvalue in TestValues)
                {
                    testvalues.Add(new ConstExpr(RuntimeHelpers.ChangeType(testvalue.Value, type)));
                }
            }
            return Expression.SwitchCase(GetBodyExpr(), testvalues);
        }

    }




}
