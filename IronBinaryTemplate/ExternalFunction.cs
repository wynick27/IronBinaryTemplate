using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;

namespace IronBinaryTemplate
{

    public interface ICallableFunction
    {
        Expression GetCallExpression(ILexicalScope scope, List<Expression> arguments);
        Type ReturnType { get; }


        public static List<Expression> ConvertArguments(ILexicalScope scope, string name, IList<ParameterInfo> parameters, List<Expression> arguments)
        {
            var converted = new List<Expression>();

            var paramarray = new List<Expression>();

            bool isparamarray = parameters.Count == 0 ? false : parameters[^1].IsDefined(typeof(ParamArrayAttribute), false);


            if ((arguments == null ? 0 : arguments.Count) > parameters.Count && !isparamarray)
                throw new InvalidOperationException($"Function {name} called with too many arguments.");
            if (arguments != null)
            {
                int j = 0;


                for (int i = 0; i < arguments.Count; i++)
                {
                    if (j >= parameters.Count)
                        j = parameters.Count - 1;
                    var param = parameters[j];
                    if (param.ParameterType.IsAssignableFrom(typeof(BinaryTemplateContext)))
                    {
                        converted.Add(scope.Context);
                        j++;
                        continue;
                    }
                    else if (param.ParameterType == typeof(BinaryTemplateScope))
                    {
                        converted.Add(scope.Scope);
                        j++;
                        continue;
                    }
                    else if (param.ParameterType.IsAssignableFrom(typeof(BinaryTemplateVariable)))
                    {
                        if (arguments[i] is ILValue lvalue)
                        {
                            lvalue.AccessMode = ValueAccess.Wrapper;
                        }
                        else
                        {
                            throw new ArgumentException($"Arguments {param.Name} must be lvalue.");
                        }
                    }
                    else if (param.ParameterType.IsByRef)
                    {
                        if (arguments[i] is ILValue lvalue)
                        {
                            lvalue.AccessMode = ValueAccess.Reference;
                        }
                        else
                        {
                            throw new ArgumentException($"Arguments {param.Name} must be lvalue.");
                        }
                    }
                    converted.Add(arguments[i]);
                    j++;
                }
            }
            return converted;
        }

        public static List<Expression> ConvertArguments(ILexicalScope scope, string name, IList<ParameterExpression> parameters, List<Expression> arguments)
        {
            var converted = new List<Expression>();

            if ((arguments == null ? 0 : arguments.Count) != parameters.Count)
                throw new InvalidOperationException($"Function {name} called with wrong numbers of arguments.");
            if (arguments != null)
            {
                int j = 0;


                for (int i = 0; i < arguments.Count; i++,j++)
                {
                    var param = parameters[j];
                    if (param.IsByRef)
                    {
                        if (arguments[i] is ILValue lvalue)
                        {
                            lvalue.AccessMode = ValueAccess.Reference;
                        }
                        else
                        {
                            throw new ArgumentException($"Arguments {param.Name} must be lvalue.");
                        }
                    }
                    else if (!param.Type.IsAssignableFrom(arguments[i].Type))
                    {
                        converted.Add(RuntimeHelpers.DynamicConvert(arguments[i],param.Type));
                        continue;
                    }
                    converted.Add(arguments[i]);
                }
            }
            return converted;
        }
    }

    public abstract class ExternalFunctionBase : ICallableFunction
    {
        public abstract Expression GetCallExpression(ILexicalScope scope, List<Expression> arguments);

        public Type ReturnType => typeof(object);

        public List<Expression> ConvertArguments(MethodInfo method, ILexicalScope scope, List<Expression> arguments, bool requirethis)
        {
            var parameters = method.GetParameters();
            var converted = new List<Expression>();
            var thisExpr = scope.GetParameter("this");
            if (requirethis)
            {
                
                if (method.DeclaringType.IsAssignableFrom(typeof(BinaryTemplateContext)) && scope.Context != null)
                    converted.Add(scope.Context);
                else if (thisExpr !=null && method.DeclaringType.IsAssignableFrom(thisExpr.Type))
                    converted.Add(thisExpr);
                else
                    throw new InvalidOperationException($"Function requires an object instance of type {method.DeclaringType}.");
            }

            converted.AddRange(ICallableFunction.ConvertArguments(scope, method.Name, parameters, arguments));

            return converted;
        }

    }


    public class ExternalFunction : ExternalFunctionBase
    {
        public MethodInfo Method { get; protected set; }

        public ExternalFunction(MethodInfo method)
        {
            Method = method;
        }


        public override Expression GetCallExpression(ILexicalScope scope, List<Expression> arguments)
        {
            var converted = ConvertArguments(Method, scope, arguments, !Method.IsStatic);
            converted.Insert(0, Expression.Constant(Method));
            return Expression.Dynamic(new BTInvokeBinder(new CallInfo(converted.Count)), typeof(object), converted);
        }
    }
    public class ExternalDelegate : ExternalFunctionBase
    {
        public Delegate Delegate { get; set; }
        public ExternalDelegate(Delegate d)
        {
            Delegate = d;
        }

        public override Expression GetCallExpression(ILexicalScope scope, List<Expression> arguments)
        {
            var converted = ConvertArguments(Delegate.Method, scope, arguments, false);
            converted.Insert(0, Expression.Constant(Delegate));
            return Expression.Dynamic(new BTInvokeBinder(new CallInfo(converted.Count)), typeof(object), converted);
        }
    }

    public class ExistsFunction : ExternalFunction
    {
        public ExistsFunction() : base(typeof(LibraryFunctions).GetMethod("exists"))
        {
        }

        public override Expression GetCallExpression(ILexicalScope scope, List<Expression> arguments)
        {
            if (arguments.Count != 1)
            {
                throw new InvalidOperationException($"Function called with too many arguments.");
            }
            Expression arg = arguments[0];
            ParameterExpression param = scope.Scope as ParameterExpression;
            if (param == null || param.Type != typeof(BinaryTemplateScope))
                throw new InvalidOperationException("exists() requires a scope parameter.");
            if (arg is ILValue lvalue)
            {
                lvalue.AccessMode = ValueAccess.Wrapper;
                arguments = lvalue.GetPathExpressions();
                arguments.Insert(0, param);
            }
            else
                throw new InvalidOperationException("Arguments for exists() must be lvalue.");
            return base.GetCallExpression(scope, arguments);
        }
    }


}
