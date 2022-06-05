using IronBinaryTemplate;
using System;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.CSharp.RuntimeBinder;

namespace IronBinaryTemplate
{


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
            Expression expr = target.Expression;
            Type targettype = target.LimitType;
            var restriction = target.Restrictions.Merge(
                    BindingRestrictions.GetTypeRestriction(
                        target.Expression, target.LimitType));
            if (target.LimitType.IsAssignableTo(typeof(BinaryTemplateVariable)) && (target.Value as BinaryTemplateVariable).Value != target.Value)
            {
                expr = Expression.Property(Expression.Convert(target.Expression, typeof(BinaryTemplateVariable)), "Value");
                targettype = (target.Value as BinaryTemplateVariable).Value.GetType();
                restriction = target.Restrictions.Merge(BindingRestrictions.GetTypeRestriction(
                        expr, targettype));
            }
            if (operation == ExpressionType.Extension) //!
            {
                operation = ExpressionType.Not;

                if (targettype != typeof(bool) && targettype.IsPrimitive)
                {
                    return new DynamicMetaObject(
                RuntimeHelpers.EnsureObjectResult(
                  Expression.MakeBinary(
                    ExpressionType.Equal,
                    Expression.Convert(expr, targettype),
                    Expression.Convert(Expression.Constant(0), targettype))),
                     restriction);
                }

            }

            expr = RuntimeHelpers.EnsureType(expr, targettype, targettype);


            return new DynamicMetaObject(
                RuntimeHelpers.EnsureObjectResult(
                  Expression.MakeUnary(
                    operation,
                    expr, targettype)), restriction);
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

            var targetexpr = target.Expression;
            var targettype = target.LimitType;
            BindingRestrictions restrictions = target.Restrictions;
            if (target.LimitType.IsAssignableTo(typeof(BinaryTemplateVariable)) && !this.Type.IsAssignableFrom(typeof(BinaryTemplateVariable)) && (target.Value as BinaryTemplateVariable).Value != target.Value)
            {
                targetexpr = Expression.Property(Expression.Convert(target.Expression, typeof(BinaryTemplateVariable)), "Value");
                targettype = (target.Value as BinaryTemplateVariable).Value.GetType();
                restrictions = restrictions.Merge(BindingRestrictions.GetTypeRestriction(targetexpr, targettype));
                try
                {
                    return new DynamicMetaObject(
                RuntimeHelpers.EnsureType(targetexpr, targettype, this.Type),
                restrictions);
                }
                catch (Exception)
                {

                    targetexpr = target.Expression;
                    targettype = target.LimitType;
                    restrictions = restrictions.Merge(BindingRestrictions.GetInstanceRestriction(targetexpr, target.Value));
                }
            }
            else
            {
                restrictions = restrictions.Merge(BindingRestrictions.GetTypeRestriction(
                        target.Expression, target.LimitType));
            }

            return new DynamicMetaObject(
                RuntimeHelpers.EnsureType(targetexpr, targettype, this.Type),
                restrictions);

        }
    }

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
            Type targettype = target.LimitType;
            Type argtype = arg.LimitType;
            var restrictions = target.Restrictions.Merge(arg.Restrictions)
                .Merge(BindingRestrictions.GetTypeRestriction(
                    target.Expression, target.LimitType))
                .Merge(BindingRestrictions.GetTypeRestriction(
                    arg.Expression, arg.LimitType));
            
            if (target.LimitType.IsAssignableTo(typeof(BinaryTemplateVariable)) && (target.Value as BinaryTemplateVariable).Value != target.Value)
            {
                expr1 = Expression.Property(Expression.Convert(target.Expression, typeof(BinaryTemplateVariable)), "Value");
                targettype = (target.Value as BinaryTemplateVariable).Value.GetType();
                restrictions = restrictions.Merge(BindingRestrictions.GetTypeRestriction(expr1, targettype));

            }
            if (arg.LimitType.IsAssignableTo(typeof(BinaryTemplateVariable)) && (arg.Value as BinaryTemplateVariable).Value != arg.Value)
            {
                expr2 = Expression.Property(Expression.Convert(arg.Expression, typeof(BinaryTemplateVariable)), "Value");
                argtype = (arg.Value as BinaryTemplateVariable).Value.GetType();
                restrictions = restrictions.Merge(BindingRestrictions.GetTypeRestriction(expr2, argtype));
            }
            if (isAssignment)
            {
                expr2 = RuntimeHelpers.EnsureType(arg, targettype);
            }
            else if (operation == ExpressionType.LeftShift || operation == ExpressionType.RightShift)
            {
                expr1 = RuntimeHelpers.EnsureType(expr1, targettype, targettype);
                expr2 = RuntimeHelpers.EnsureType(expr2, argtype, typeof(int));
            }
            else if (targettype.IsPrimitive && argtype.IsPrimitive)
            {
                Type resulttype = RuntimeHelpers.LiftType(targettype, argtype);
                expr1 = RuntimeHelpers.EnsureType(expr1, targettype, resulttype);
                expr2 = RuntimeHelpers.EnsureType(expr2, argtype, resulttype);
            }
            else
            {
                expr1 = RuntimeHelpers.EnsureType(expr1, targettype, targettype);
                expr2 = RuntimeHelpers.EnsureType(expr2, argtype, argtype);
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


            BindingRestrictions restrictions = target.Restrictions;
            Expression indexingExpr = target.Expression;

            if (target.LimitType.IsPrimitive && indexes.Length == 1)
            {
                
                restrictions = target.Restrictions.Merge(BindingRestrictions.GetInstanceRestriction(
                                                  target.Expression, target.Value));
                if (indexes[0].LimitType.IsPrimitive && Convert.ToUInt64(indexes[0].Value) == 0)
                {
                    indexingExpr = target.Expression;

                    restrictions = target.Restrictions.Merge(BindingRestrictions.GetInstanceRestriction(
                                                  indexes[0].Expression, indexes[0].Value));
                }
                else if (indexes[0].LimitType.IsAssignableTo(typeof(BinaryTemplateVariable)) &&
                    Convert.ToUInt64((indexes[0].Value as BinaryTemplateVariable).Value) == 0)
                {

                    
                    restrictions = restrictions.Merge(BindingRestrictions.GetTypeRestriction(
                                                  indexes[0].Expression, indexes[0].LimitType));
                    
                    var restrictionExpr = Expression.Property(Expression.Convert(indexes[0].Expression, typeof(BinaryTemplateVariable)), "Value");
                    var currentValue = (indexes[0].Value as BinaryTemplateVariable).Value;

                    restrictions = restrictions.Merge(BindingRestrictions.GetInstanceRestriction(restrictionExpr, currentValue));
                    
                    indexingExpr = target.Expression;
                }
                else
                indexingExpr = Expression.Throw(
                        Expression.New(
                            typeof(MissingMemberException)
                                .GetConstructor(new Type[] { typeof(string) }),
                            Expression.Constant(
                               "Index out of bound.")
                        )
                    );

            }
            else if (indexes.Length == 1 && indexes[0].LimitType.IsAssignableTo(typeof(BinaryTemplateVariable)) && (indexes[0].Value as BinaryTemplateVariable).Value != indexes[0].Value)
            {

                restrictions = target.Restrictions.Merge(BindingRestrictions.GetTypeRestriction(
                                                  target.Expression, target.LimitType));

                restrictions = restrictions.Merge(BindingRestrictions.GetTypeRestriction(
                                              indexes[0].Expression, indexes[0].LimitType));

                var restrictionExpr = Expression.Property(Expression.Convert(indexes[0].Expression, typeof(BinaryTemplateVariable)), "Value");
                var currentValue = (indexes[0].Value as BinaryTemplateVariable).Value;

                restrictions = restrictions.Merge(BindingRestrictions.GetTypeRestriction(restrictionExpr, currentValue.GetType()));
                var newmetaobject = new DynamicMetaObject(restrictionExpr, restrictions, currentValue);
                indexingExpr = RuntimeHelpers.EnsureObjectResult(
                                  RuntimeHelpers.GetIndexingExpression(target,
                                                                       new[] { newmetaobject }));
            }
            else
            {
                indexingExpr = RuntimeHelpers.EnsureObjectResult(
                                  RuntimeHelpers.GetIndexingExpression(target,
                                                                       indexes));
                restrictions = restrictions.Merge(RuntimeHelpers.GetTargetArgsRestrictions(
                                                      target, indexes, false, false));
            }

            
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
            if (target.LimitType.IsAssignableTo(typeof(BinaryTemplateDuplicatedArray)))
            {

                var restrictions = target.Restrictions;
                restrictions = restrictions.Merge(BindingRestrictions.GetTypeRestriction(
                                              target.Expression, target.LimitType));
                var restrictionExpr = Expression.Property(Expression.Convert(target.Expression, typeof(BinaryTemplateDuplicatedArray)), "Value");
                var currentValue = (target.Value as BinaryTemplateDuplicatedArray).Value;
                restrictions = restrictions.Merge(BindingRestrictions.GetTypeRestriction(restrictionExpr, currentValue.GetType()));
                if (currentValue is IDynamicMetaObjectProvider)
                    return (currentValue as IDynamicMetaObjectProvider).GetMetaObject(restrictionExpr).BindGetMember(this);
            }
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
            BindingRestrictions restrictions =
                 RuntimeHelpers.GetTargetArgsRestrictions(target, indexes, false, false);

            var indexObjects = indexes;

            if (indexes.Length == 1 && (indexes[0].Value as BinaryTemplateVariable).Value != indexes[0].Value)
            {
                var restrictionExpr = Expression.Property(Expression.Convert(indexes[0].Expression, typeof(BinaryTemplateVariable)), "Value");
                var currentValue = (indexes[0].Value as BinaryTemplateVariable).Value;
                restrictions = restrictions.Merge(BindingRestrictions.GetTypeRestriction(restrictionExpr, currentValue.GetType()));
                indexObjects = new[] { new DynamicMetaObject(restrictionExpr, restrictions, currentValue) };


            }
            // Find our own binding.
            Expression valueExpr = value.Expression;


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
            BindingRestrictions restrictions = BindingRestrictions.GetTypeRestriction(target.Expression,
                                                                   target.LimitType);
            // Find our own binding.
            if (target.LimitType.IsSubclassOf(typeof(Delegate)))
            {
                var parms = target.LimitType.GetMethod("Invoke").GetParameters();
                    // Don't need to check if argument types match parameters.
                    // If they don't, users get an argument conversion error.
                
                var callArgs = RuntimeHelpers.ConvertArguments(args, parms, ref restrictions);
                var expression = Expression.Invoke(
                    Expression.Convert(target.Expression, target.LimitType),
                    callArgs);
                return new DynamicMetaObject(
                    RuntimeHelpers.EnsureObjectResult(expression),
                    restrictions);
            } else if (target.LimitType.IsSubclassOf(typeof(MethodInfo)))
            {
                MethodInfo method = target.Value as MethodInfo;
                
                Expression expression;
                if (method.IsStatic)
                {
                    var callArgs = RuntimeHelpers.ConvertArguments(args, method.GetParameters(), ref restrictions);
                    expression = Expression.Call(method, callArgs);
                }
                else
                {
                    var instance = RuntimeHelpers.EnsureType(args[0],method.DeclaringType);
                    var callArgs = RuntimeHelpers.ConvertArguments(args[1..^0], method.GetParameters(), ref restrictions);
                    expression = Expression.Call(instance, method, callArgs);
                }
                    
                    return new DynamicMetaObject(
                            RuntimeHelpers.EnsureObjectResult(expression),
                            restrictions);
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
    



}
