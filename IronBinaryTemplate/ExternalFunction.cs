using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;

namespace IronBinaryTemplate
{

    public class TemplateCallableAttribute : Attribute
    {
        public TemplateCallableAttribute(string name = "")
        {
            FunctionName = name;
        }
        public string FunctionName { get; }
    }

    public class TemplateVariableCreatingCallbackAttribute : Attribute
    {
        public TemplateVariableCreatingCallbackAttribute(string name = "")
        {
            AttributeName = name;
        }
        public string AttributeName { get; }
    }

    public class TemplateVariableCreatedCallbackAttribute : Attribute
    {
        public TemplateVariableCreatedCallbackAttribute(string name = "")
        {
            AttributeName = name;
        }
        public string AttributeName { get; }
    }

    public interface ICallableFunction
    {
        Expression GetCallExpression(ILexicalScope scope, List<Expression> arguments);
        Type ReturnType { get; }


        public static List<Expression> ConvertArguments(ILexicalScope scope, string name, IList<ParameterInfo> parameters, List<Expression> arguments)
        {
            var converted = new List<Expression>();

            var paramarray = new List<Expression>();

            bool isparamarray = parameters.Count == 0 ? false : parameters[^1].IsDefined(typeof(ParamArrayAttribute), false);


            //if ((arguments == null ? 0 : arguments.Count) > parameters.Count && !isparamarray)
            //    throw new InvalidOperationException($"Function {name} called with too many arguments.");
            if (arguments != null)
            {
                int j = 0;


                for (int i = 0; i < arguments.Count; i++)
                {
                    bool paramarrayparam = false;
                    if (j >= parameters.Count || j == parameters.Count - 1 && isparamarray)
                    {
                        j = parameters.Count - 1;
                        paramarrayparam = true;
                    }
                    var param = parameters[j];
                    if (param.ParameterType.IsAssignableFrom(typeof(BinaryTemplateContext)))
                    {
                        converted.Add(scope.Context);
                        j++;
                        continue;
                    }
                    else if (param.ParameterType.IsAssignableTo(typeof(IBinaryTemplateScope)))
                    {
                        converted.Add(scope.ScopeArg);
                        j++;
                        continue;
                    }
                    else if (param.ParameterType.IsAssignableFrom(typeof(BinaryTemplateVariable)))
                    {
                        if (arguments[i] is ILValue lvalue)
                        {
                            lvalue.AccessMode = ValueAccess.Wrapper;
                        }
                        else if (arguments[i] is FunctionCallExpr funccall && funccall.Name == "parentof")
                        {

                        }
                        else
                        {
                            throw new ArgumentException($"Arguments {param.Name} must be lvalue.");
                        }
                        converted.Add(RuntimeHelpers.DynamicConvert(arguments[i], param.ParameterType));
                        continue;
                    }
                    else if (param.ParameterType.IsByRef && param.ParameterType.IsValueType)
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
    }
    public abstract class ExternalFunctionBase : ICallableFunction
    {
        public abstract Expression GetCallExpression(ILexicalScope scope, List<Expression> arguments);

        public Type ReturnType => typeof(object);

        public List<Expression> ConvertArguments(MethodInfo method, ILexicalScope scope, List<Expression> arguments, bool requirethis)
        {
            var parameters = method.GetParameters();
            var converted = new List<Expression>();
            var scopeExpr = scope.ScopeArg;
            if (requirethis)
            {

                if (method.DeclaringType.IsAssignableFrom(typeof(BinaryTemplateContext)) && scope.Context != null)
                    converted.Add(scope.Context);
                else if (scopeExpr != null && method.DeclaringType.IsAssignableFrom(scopeExpr.Type))
                    converted.Add(scopeExpr);
                else if (scopeExpr != null)
                    converted.Add(Expression.Call(scope.Context, typeof(BinaryTemplateContext).GetMethod("GetService"), Expression.Constant(method.DeclaringType)));
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

    public class TranslateNameToPathFunction : ExternalFunction
    {
        public TranslateNameToPathFunction(MethodInfo method) : base(method)
        {
        }

        public override Expression GetCallExpression(ILexicalScope scope, List<Expression> arguments)
        {
            if (arguments.Count != 1)
            {
                throw new InvalidOperationException($"Function called with too many arguments.");
            }
            Expression arg = arguments[0];
            Expression scopeArg = scope.ScopeArg;
            if (scopeArg == null || !scopeArg.Type.IsAssignableTo(typeof(IBinaryTemplateScope)))
                throw new InvalidOperationException($"{Method.Name}() requires a scope argument.");
            if (arg is ILValue lvalue)
            {
                lvalue.AccessMode = ValueAccess.Wrapper;
                arguments = lvalue.GetPathExpressions();
                arguments.Insert(0, scopeArg);
            }
            else
                throw new InvalidOperationException($"Arguments for {Method.Name}() must be lvalue.");
            return base.GetCallExpression(scope, arguments);
        }
    }

    public class NativeLibrary
    {

    }

    public class NativeDelegate : ExternalDelegate
    {
        public NativeLibrary NativeLibrary { get; set; }
        public NativeDelegate(Delegate d)
            : base(d)
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

}
