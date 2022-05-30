using IronBinaryTemplate;
using System;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.CSharp.RuntimeBinder;

namespace IronBinaryTemplate
{
    public class BTBinaryOperationBinder : BinaryOperationBinder
    {
        ExpressionType operation;
        bool isAssignment;

        public BTBinaryOperationBinder(ExpressionType operation)
            : base(ExpressionType.Extension)
        {
            this.operation = operation;
            this.isAssignment = operation.ToString().EndsWith("Assign");
        }



        public override DynamicMetaObject FallbackBinaryOperation(
                    DynamicMetaObject target, DynamicMetaObject arg,
                    DynamicMetaObject errorSuggestion)
        {

            if (!target.HasValue || !arg.HasValue)
            {
                return Defer(target, arg);
            }
            Expression expr1 = target.Expression;
            Expression expr2 = arg.Expression;
            var restrictions = target.Restrictions.Merge(arg.Restrictions)
                .Merge(BindingRestrictions.GetTypeRestriction(
                    target.Expression, target.LimitType))
                .Merge(BindingRestrictions.GetTypeRestriction(
                    arg.Expression, arg.LimitType));
            if (isAssignment && target.LimitType.IsSubclassOf(typeof(BinaryTemplateVariable)))
            {
                restrictions.Merge(BindingRestrictions.GetInstanceRestriction(target.Expression, target.Value));
                if (target.LimitType == typeof(LocalVariable))
                {
                    LocalVariable var = target.Value as LocalVariable;
                    expr1 = Expression.Field(Expression.Convert(target.Expression,typeof(LocalVariable)), "value");
                    expr2 = RuntimeHelpers.EnsureObjectResult(RuntimeHelpers.EnsureType(arg, var.Type.ClrType));
                } else
                {
                    return RuntimeHelpers.CreateThrow(target, new[] { arg }, restrictions, typeof(InvalidOperationException), "Cannot assign non-local variable in template.");
                }
            }
            else if (isAssignment)
            {
                expr2 = RuntimeHelpers.EnsureType(arg, target.LimitType);
            }
            else if (target.LimitType.IsPrimitive && arg.LimitType.IsPrimitive)
            {
                Type resulttype = RuntimeHelpers.LiftType(target.LimitType, arg.LimitType);
                expr1 = RuntimeHelpers.EnsureType(target, resulttype);
                expr2 = RuntimeHelpers.EnsureType(arg, resulttype);
            }
            else
            {
                expr1 = RuntimeHelpers.EnsureType(target, target.LimitType);
                expr2 = RuntimeHelpers.EnsureType(arg, arg.LimitType);
            }

            return new DynamicMetaObject(
                RuntimeHelpers.EnsureObjectResult(
                  Expression.MakeBinary(
                    operation,
                    expr1,
                    expr2)),
                restrictions
            );
        }
    }


    public class BTGetIndexrBinder : GetIndexBinder
    {
        public BTGetIndexrBinder(CallInfo callInfo) : base(callInfo)
        {
        }

        public override DynamicMetaObject FallbackGetIndex(DynamicMetaObject target, DynamicMetaObject[] indexes, DynamicMetaObject errorSuggestion)
        {
            if (!target.HasValue || indexes.Any((a) => !a.HasValue))
            {
                var deferArgs = new DynamicMetaObject[indexes.Length + 1];
                for (int i = 0; i < indexes.Length; i++)
                {
                    deferArgs[i + 1] = indexes[i];
                }
                deferArgs[0] = target;
                return Defer(deferArgs);
            }


            var indexingExpr = RuntimeHelpers.EnsureObjectResult(
                                  RuntimeHelpers.GetIndexingExpression(target,
                                                                       indexes));
            var restrictions = RuntimeHelpers.GetTargetArgsRestrictions(
                                                  target, indexes, false);
            return new DynamicMetaObject(indexingExpr, restrictions);
        }
    }

    public class BTGetMemberBinder : GetMemberBinder
    {
        public BTGetMemberBinder(string name, bool ignoreCase) : base(name, ignoreCase)
        {
        }

        public override DynamicMetaObject FallbackGetMember(DynamicMetaObject target, DynamicMetaObject errorSuggestion)
        {
            if (!target.HasValue) return Defer(target);
            // Find our own binding.
            var flags = BindingFlags.IgnoreCase | BindingFlags.Static |
                        BindingFlags.Instance | BindingFlags.Public;
            var members = target.LimitType.GetMember(this.Name, flags);
            if (members.Length == 1)
            {
                return new DynamicMetaObject(
                    RuntimeHelpers.EnsureObjectResult(
                      Expression.MakeMemberAccess(
                        Expression.Convert(target.Expression,
                                           members[0].DeclaringType),
                        members[0])),

                    BindingRestrictions.GetTypeRestriction(target.Expression,
                                                           target.LimitType));
            }
            else
            {
                return errorSuggestion ??
                    RuntimeHelpers.CreateThrow(
                        target, null,
                        BindingRestrictions.GetTypeRestriction(target.Expression,
                                                               target.LimitType),
                        typeof(MissingMemberException),
                        "cannot bind member, " + this.Name +
                            ", on object " + target.Value.ToString());
            }
        }
    }

    public class BTSetMemberBinder : SetMemberBinder
    {
        public BTSetMemberBinder(string name, bool ignoreCase) : base(name, ignoreCase)
        {
        }

        public override DynamicMetaObject FallbackSetMember(DynamicMetaObject target, DynamicMetaObject value, DynamicMetaObject errorSuggestion)
        {
            if (!target.HasValue) return Defer(target);

            var flags = BindingFlags.IgnoreCase | BindingFlags.Static |
                        BindingFlags.Instance | BindingFlags.Public;
            var members = target.LimitType.GetMember(this.Name, flags);
            if (members.Length == 1)
            {
                MemberInfo mem = members[0];
                Expression val;
                // Should check for member domain type being Type and value being
                // TypeModel, similar to ConvertArguments, and building an
                // expression like GetRuntimeTypeMoFromModel.
                if (mem.MemberType == MemberTypes.Property)
                    val = Expression.Convert(value.Expression,
                                             ((PropertyInfo)mem).PropertyType);
                else if (mem.MemberType == MemberTypes.Field)
                    val = Expression.Convert(value.Expression,
                                             ((FieldInfo)mem).FieldType);
                else
                    return (errorSuggestion ??
                            RuntimeHelpers.CreateThrow(
                                target, null,
                                BindingRestrictions.GetTypeRestriction(
                                    target.Expression,
                                    target.LimitType),
                                typeof(InvalidOperationException),
                                "SetMemberBinder only supports Properties and " +
                                "fields."));
                return new DynamicMetaObject(
                    // Assign returns the stored value, so we're good for Sympl.
                    RuntimeHelpers.EnsureObjectResult(
                      Expression.Assign(
                        Expression.MakeMemberAccess(
                            RuntimeHelpers.EnsureType(target,
                                               members[0].DeclaringType),
                            members[0]),
                        val)),
                    // Don't need restriction test for name since this
                    // rule is only used where binder is used, which is
                    // only used in sites with this binder.Name.                    
                    BindingRestrictions.GetTypeRestriction(target.Expression,
                                                           target.LimitType));
            }
            else
            {
                return errorSuggestion ??
                    RuntimeHelpers.CreateThrow(
                        target, null,
                        BindingRestrictions.GetTypeRestriction(target.Expression,
                                                               target.LimitType),
                        typeof(MissingMemberException),
                         "Could not find suitable member for this object.");
            }
        }
    
    }

    public class BTSetIndexBinder : SetIndexBinder
    {
        public BTSetIndexBinder(CallInfo callInfo) : base(callInfo)
        {
        }

        public override DynamicMetaObject FallbackSetIndex(DynamicMetaObject target, DynamicMetaObject[] indexes, DynamicMetaObject value, DynamicMetaObject errorSuggestion)
        {
            if (!target.HasValue || indexes.Any((a) => !a.HasValue) ||
                !value.HasValue)
            {
                var deferArgs = new DynamicMetaObject[indexes.Length + 2];
                for (int i = 0; i < indexes.Length; i++)
                {
                    deferArgs[i + 1] = indexes[i];
                }
                deferArgs[0] = target;
                deferArgs[indexes.Length + 1] = value;
                return Defer(deferArgs);
            }
            // Find our own binding.
            Expression valueExpr = value.Expression;
            BindingRestrictions restrictions =
                 RuntimeHelpers.GetTargetArgsRestrictions(target, indexes, false);
            if (target.LimitType.IsArray)
            {
                valueExpr = RuntimeHelpers.EnsureType(value, target.LimitType.GetElementType());
                restrictions = restrictions.Merge(BindingRestrictions.GetTypeRestriction(value.Expression, value.LimitType));
            }

            Expression setIndexExpr;
            
            Expression indexingExpr = RuntimeHelpers.GetIndexingExpression(
                                                        target, indexes);
            // Assign returns the stored value, so we're good for Sympl.
            setIndexExpr = Expression.Assign(indexingExpr, valueExpr);
            
            return new DynamicMetaObject(
                RuntimeHelpers.EnsureObjectResult(setIndexExpr),
                restrictions);

        }
        
    }

    public class BTInvokeBinder : InvokeBinder
    {
        public BTInvokeBinder(CallInfo callInfo) : base(callInfo)
        {
        }

        public override T BindDelegate<T>(System.Runtime.CompilerServices.CallSite<T> site, object[] args)
        {
            return base.BindDelegate(site, args);
        }
        public override DynamicMetaObject FallbackInvoke(DynamicMetaObject target, DynamicMetaObject[] args, DynamicMetaObject errorSuggestion)
        {
            

            if (!target.HasValue || args.Any((a) => !a.HasValue))
            {
                var deferArgs = new DynamicMetaObject[args.Length + 1];
                for (int i = 0; i < args.Length; i++)
                {
                    deferArgs[i + 1] = args[i];
                }
                deferArgs[0] = target;
                return Defer(deferArgs);
            }
            // Find our own binding.
            if (target.LimitType.IsSubclassOf(typeof(Delegate)))
            {
                var parms = target.LimitType.GetMethod("Invoke").GetParameters();
                    // Don't need to check if argument types match parameters.
                    // If they don't, users get an argument conversion error.
                var callArgs = RuntimeHelpers.ConvertArguments(args, parms);
                var expression = Expression.Invoke(
                    Expression.Convert(target.Expression, target.LimitType),
                    callArgs);
                return new DynamicMetaObject(
                    RuntimeHelpers.EnsureObjectResult(expression),
                    BindingRestrictions.GetTypeRestriction(target.Expression,
                                                               target.LimitType));
            } else if (target.LimitType.IsSubclassOf(typeof(MethodInfo)))
            {
                MethodInfo method = target.Value as MethodInfo;

                Expression expression;
                if (method.IsStatic)
                {
                    var callArgs = RuntimeHelpers.ConvertArguments(args, method.GetParameters());
                    expression = Expression.Call(method, callArgs);
                }
                else
                {
                    var instance = RuntimeHelpers.EnsureType(args[0],method.DeclaringType);
                    var callArgs = args.Length == 1 ? Array.Empty<Expression>() : RuntimeHelpers.ConvertArguments(args[1..^0], method.GetParameters());
                    expression = Expression.Call(instance, method, callArgs);
                }
                    
                    return new DynamicMetaObject(
                            RuntimeHelpers.EnsureObjectResult(expression),
                            BindingRestrictions.GetTypeRestriction(target.Expression,
                                                                   target.LimitType));
            }
            return errorSuggestion ??
                RuntimeHelpers.CreateThrow(
                    target, args,
                    BindingRestrictions.GetTypeRestriction(target.Expression,
                                                           target.LimitType),
                    typeof(InvalidOperationException),
                    "Wrong number of arguments for function -- " +
                    target.LimitType.ToString() + " got " + args.ToString());

        }
    }
    


    public class BTUnaryOperationBinder : UnaryOperationBinder
    {
        public new ExpressionType Operation { get; }
        public BTUnaryOperationBinder(ExpressionType operation) : base(ExpressionType.Extension)
        {
            this.Operation = operation;
        }

        public override DynamicMetaObject FallbackUnaryOperation(DynamicMetaObject target, DynamicMetaObject errorSuggestion)
        {
            if (!target.HasValue)
            {
                return Defer(target);
            }

            var operation = this.Operation;
            Expression expr = null;
            Type type = target.LimitType;

            if (operation == ExpressionType.Extension) //!
            {
                operation = ExpressionType.Not;
                    
                if (target.LimitType != typeof(bool) &&target.LimitType.IsPrimitive)
                {
                    return new DynamicMetaObject(
                RuntimeHelpers.EnsureObjectResult(
                  Expression.MakeBinary(
                    ExpressionType.Equal,
                    Expression.Convert(target.Expression, target.LimitType),
                    Expression.Constant(0,target.LimitType))),
                target.Restrictions.Merge(
                    BindingRestrictions.GetTypeRestriction(
                        target.Expression, target.LimitType)));
                }

            } else if (operation.ToString().EndsWith("Assign") && target.LimitType.IsSubclassOf(typeof(BinaryTemplateVariable)))
            {
                target.Restrictions.Merge(BindingRestrictions.GetInstanceRestriction(target.Expression, target.Value));
                if (target.LimitType == typeof(LocalVariable))
                {
                    LocalVariable var = target.Value as LocalVariable;
                    expr = Expression.Field(Expression.Convert(target.Expression, typeof(LocalVariable)), "value");
                    type = var.Type.ClrType;
                }
                else
                {
                    return RuntimeHelpers.CreateThrow(target, new DynamicMetaObject[] { }, BindingRestrictions.Empty, typeof(InvalidOperationException), "Cannot assign non-local variable in template.");
                }
            }
            expr = RuntimeHelpers.EnsureType(target, target.LimitType);


            return new DynamicMetaObject(
                RuntimeHelpers.EnsureObjectResult(
                  Expression.MakeUnary(
                    operation,
                    expr, type)),
            target.Restrictions.Merge(
                    BindingRestrictions.GetTypeRestriction(
                        target.Expression, target.LimitType)));
        }
    }

    public class BTConvertBinder : ConvertBinder
    {
        public BTConvertBinder(Type type, bool @explicit) : base(type, @explicit)
        {
        }

        public override DynamicMetaObject FallbackConvert(DynamicMetaObject target, DynamicMetaObject errorSuggestion)
        {
            if (!target.HasValue)
                return Defer(target);

            return new DynamicMetaObject(
                RuntimeHelpers.EnsureType(target, this.Type),
                target.Restrictions.Merge(
                    BindingRestrictions.GetTypeRestriction(
                        target.Expression, target.LimitType)));

        }
    }
    

}
