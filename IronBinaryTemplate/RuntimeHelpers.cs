using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using AST = System.Linq.Expressions;


namespace IronBinaryTemplate
{

    public static class RuntimeHelpers
    {
        public static System.Linq.Expressions.Expression EnsureObjectResult(System.Linq.Expressions.Expression expr)
        {
            if (!expr.Type.IsValueType)
                return expr;
            if (expr.Type == typeof(void))
                return System.Linq.Expressions.Expression.Block(
                           expr, System.Linq.Expressions.Expression.Default(typeof(object)));
            else
                return System.Linq.Expressions.Expression.Convert(expr, typeof(object));
        }


        public static Expression EnsureType(DynamicMetaObject obj, Type type)
        {
            Expression expr = obj.Expression;
            
            if (obj.LimitType != obj.Expression.Type)
                expr = Expression.Convert(expr, obj.LimitType);
            if (obj.LimitType != type)
            {
                if (type == typeof(bool) && obj.LimitType.IsPrimitive)
                    return Expression.MakeBinary(ExpressionType.NotEqual, expr, Expression.Constant(0));
                else if (type.IsPrimitive && obj.LimitType == typeof(bool))
                    expr = Expression.IfThenElse(expr, Expression.Constant(1,type), Expression.Constant(0,type));
                else if (type == typeof(string))
                {
                    if (obj.LimitType == typeof(sbyte[]) || obj.LimitType == typeof(byte[]))
                    {
                        expr = Expression.Convert(obj.Expression, typeof(byte[]));

                        return Expression.Convert(expr, typeof(string), typeof(RuntimeHelpers).GetMethod("ConvertBytesToString"));

                    }
                    else if (obj.LimitType == typeof(BinaryTemplateString))
                        return Expression.Call(expr, typeof(BinaryTemplateString).GetMethod("ToString"));

                }
                expr = Expression.Convert(expr, type);
            }
                
            return expr;
        }
        ///////////////////////////////////////
        // Utilities used by binders at runtime
        ///////////////////////////////////////

        // ParamsMatchArgs returns whether the args are assignable to the parameters.
        // We specially check for our TypeModel that wraps .NET's RuntimeType, and
        // elsewhere we detect the same situation to convert the TypeModel for calls.
        //
        // Consider checking p.IsByRef and returning false since that's not CLS.
        //
        // Could check for a.HasValue and a.Value is None and
        // ((paramtype is class or interface) or (paramtype is generic and
        // nullable<t>)) to support passing nil anywhere.
        //
        public static bool ParametersMatchArguments(ParameterInfo[] parameters,
                                                    DynamicMetaObject[] args)
        {
            // We only call this after filtering members by this constraint.
            Debug.Assert(args.Length == parameters.Length,
                         "Internal: args are not same len as params?!");
            for (int i = 0; i < args.Length; i++)
            {
                var paramType = parameters[i].ParameterType;
                if (!paramType.IsAssignableFrom(args[i].LimitType))
                {
                    return false;
                }
            }
            return true;
        }
        public static Expression[] ConvertArguments(
                                DynamicMetaObject[] args, ParameterInfo[] ps)
        {
            ParameterInfo paramArray = null;
            List<Expression> paramArgs = null;
            if (ps.Length > 0 && ps[^1].GetCustomAttribute<ParamArrayAttribute>() != null)
            {
                paramArray = ps[^1];
                paramArgs = new List<Expression>();
            }
            Debug.Assert(args.Length == ps.Length || (paramArray != null && args.Length >= ps.Length-1),
                         "Internal: args are not same len as params?!");
            List<Expression> callArgs = new List<Expression>();
            for (int i = 0; i < args.Length; i++)
            {
                if (paramArray != null && i >=  ps.Length-1)
                {
                    Expression argExpr = EnsureType(args[i], paramArray.ParameterType.GetElementType());
                    paramArgs.Add(argExpr);
                } else
                {
                    Expression argExpr = EnsureType(args[i], ps[i].ParameterType);
                    callArgs.Add(argExpr);
                }
                
            }
            if (paramArray != null)
                callArgs.Add(Expression.NewArrayInit(paramArray.ParameterType.GetElementType(), paramArgs));
            return callArgs.ToArray();
        }

        // GetTargetArgsRestrictions generates the restrictions needed for the
        // MO resulting from binding an operation.  This combines all existing
        // restrictions and adds some for arg conversions.  targetInst indicates
        // whether to restrict the target to an instance (for operations on type
        // objects) or to a type (for operations on an instance of that type).
        //
        // NOTE, this function should only be used when the caller is converting
        // arguments to the same types as these restrictions.
        //
        public static BindingRestrictions GetTargetArgsRestrictions(
                DynamicMetaObject target, DynamicMetaObject[] args,
                bool instanceRestrictionOnTarget)
        {
            // Important to add existing restriction first because the
            // DynamicMetaObjects (and possibly values) we're looking at depend
            // on the pre-existing restrictions holding true.
            var restrictions = target.Restrictions.Merge(BindingRestrictions
                                                            .Combine(args));
            if (instanceRestrictionOnTarget)
            {
                restrictions = restrictions.Merge(
                    BindingRestrictions.GetInstanceRestriction(
                        target.Expression,
                        target.Value
                    ));
            }
            else
            {
                restrictions = restrictions.Merge(
                    BindingRestrictions.GetTypeRestriction(
                        target.Expression,
                        target.LimitType
                    ));
            }
            for (int i = 0; i < args.Length; i++)
            {
                BindingRestrictions r;
                if (args[i].HasValue && args[i].Value == null)
                {
                    r = BindingRestrictions.GetInstanceRestriction(
                            args[i].Expression, null);
                }
                else
                {
                    r = BindingRestrictions.GetTypeRestriction(
                            args[i].Expression, args[i].LimitType);
                }
                restrictions = restrictions.Merge(r);
            }
            return restrictions;
        }


        // Return the expression for getting target[indexes]
        //
        // Note, callers must ensure consistent restrictions are added for
        // the conversions on args and target.
        //
        public static Expression GetIndexingExpression(
                                      DynamicMetaObject target,
                                      DynamicMetaObject[] indexes)
        {
            Debug.Assert(target.HasValue && target.LimitType != typeof(Array));

            var indexExpressions = indexes.Select(
                i => Expression.Convert(i.Expression, i.LimitType))
                .ToArray();
            if (target.LimitType.IsArray)
            {
                return Expression.ArrayAccess(
                    Expression.Convert(target.Expression,
                                       target.LimitType),
                    indexExpressions
                );
                // INDEXER
            }
            else
            {
                var props = target.LimitType.GetProperties();
                var indexers = props.
                    Where(p => p.GetIndexParameters().Length > 0).ToArray();
                indexers = indexers.
                    Where(idx => idx.GetIndexParameters().Length ==
                                 indexes.Length).ToArray();

                var res = new List<PropertyInfo>();
                foreach (var idxer in indexers)
                {
                    if (RuntimeHelpers.ParametersMatchArguments(
                                          idxer.GetIndexParameters(), indexes))
                    {
                        // all parameter types match
                        res.Add(idxer);
                    }
                }
                if (res.Count == 0)
                {
                    return Expression.Throw(
                        Expression.New(
                            typeof(MissingMemberException)
                                .GetConstructor(new Type[] { typeof(string) }),
                            Expression.Constant(
                               "Can't bind because there is no matching indexer.")
                        )
                    );
                }
                return Expression.MakeIndex(
                    Expression.Convert(target.Expression, target.LimitType),
                    res[0], indexExpressions);
            }
        }


        // CreateThrow is a convenience function for when binders cannot bind.
        // They need to return a DynamicMetaObject with appropriate restrictions
        // that throws.  Binders never just throw due to the protocol since
        // a binder or MO down the line may provide an implementation.
        //
        // It returns a DynamicMetaObject whose expr throws the exception, and 
        // ensures the expr's type is object to satisfy the CallSite return type
        // constraint.
        //
        // A couple of calls to CreateThrow already have the args and target
        // restrictions merged in, but BindingRestrictions.Merge doesn't add 
        // duplicates.
        //
        public static DynamicMetaObject CreateThrow
                (DynamicMetaObject target, DynamicMetaObject[] args,
                 BindingRestrictions moreTests,
                 Type exception, params object[] exceptionArgs)
        {
            Expression[] argExprs = null;
            Type[] argTypes = Type.EmptyTypes;
            int i;
            if (exceptionArgs != null)
            {
                i = exceptionArgs.Length;
                argExprs = new Expression[i];
                argTypes = new Type[i];
                i = 0;
                foreach (object o in exceptionArgs)
                {
                    Expression e = Expression.Constant(o);
                    argExprs[i] = e;
                    argTypes[i] = e.Type;
                    i += 1;
                }
            }
            ConstructorInfo constructor = exception.GetConstructor(argTypes);
            if (constructor == null)
            {
                throw new ArgumentException(
                    "Type doesn't have constructor with a given signature");
            }
            return new DynamicMetaObject(
                Expression.Throw(
                    Expression.New(constructor, argExprs),
                    // Force expression to be type object so that DLR CallSite
                    // code things only type object flows out of the CallSite.
                    typeof(object)),
                target.Restrictions.Merge(BindingRestrictions.Combine(args))
                                   .Merge(moreTests));
        }

        public static string ConvertBytesToString(byte[] bytes)
        {
            var nullPos = Array.IndexOf(bytes, (byte)0);
            return nullPos == -1 ?  Encoding.Default.GetString(bytes) : Encoding.Default.GetString(bytes,0,nullPos);
        }
        public static object ConvertIntLiteral(string text)
        {
            text = text.ToLower();
            bool unsigned = false;
            bool longint = false;
            int numbase = 10;
            if (text.Contains('u'))
            {
                text = text.Replace("u", "");
                unsigned = true;
            }
            else if (text.Contains('l'))
            {
                text = text.Replace("l", "");
                longint = true;
            }
            if (text.StartsWith("0x"))
            {
                text = text.Substring(2);
                numbase = 16;
            }
            else if (text.EndsWith('h'))
            {
                text = text.Substring(0, text.Length - 1);
                numbase = 16;
            }
            else if (text.StartsWith("0b"))
            {
                text = text.Substring(2);
                numbase = 2;
            }
            else if (text.StartsWith('0'))
            {
                numbase = 8;
            }
            if (longint)
            {
                return unsigned ? (object)Convert.ToUInt64(text, numbase) : (object)Convert.ToUInt32(text, numbase);
            }
            else
            {
                return unsigned ? (object)Convert.ToInt64(text, numbase) : (object)Convert.ToInt32(text, numbase);
            }
        }

        public static object ConvertFloatLiteral(string text)
        {
            text = text.ToLower();
            bool floatval = false;
            if (text.EndsWith('f'))
            {
                floatval = true;
                text = text.Substring(0, text.Length - 1);
            }
            return floatval ? float.Parse(text) : double.Parse(text);
        }

        static Dictionary<string, string> simpleescape =  new Dictionary<string, string>()
            {
                {"\'","\'" },
                {"\"","\"" },
                {"\\","\\" },
                {"a","\a" },
                {"b","\b" },
                {"f","\f" },
                {"n","\n" },
                {"r","\r" },
                {"t","\t" },
                {"v","\v" },
            };
        public static string ReplaceEscapeSequence(string text)
    {
            
            return Regex.Replace(text, "\\\\(?:([\'\"\\?abfnrtv])||([0-7]{1,3})|[xX]([0-9a-fA-F]{2,})|u([0-9a-fA-F]{4})|U([0-9a-fA-F]{8}))", (match) =>
            {
                if (match.Groups[1].Success)
                {
                    return simpleescape[match.Groups[1].Value];
                }
                else if (match.Groups[2].Success)
                {
                    return "" + (char)Convert.ToInt32(match.Groups[2].Value, 10);
                }
                else if (match.Groups[3].Success)
                {
                    return "" + (char)Convert.ToInt32(match.Groups[3].Value, 16);
                }
                else if (match.Groups[4].Success)
                {
                    return char.ConvertFromUtf32(Convert.ToInt32(match.Groups[4].Value, 16));
                }
                else if (match.Groups[5].Success)
                {
                    return char.ConvertFromUtf32(Convert.ToInt32(match.Groups[5].Value, 16));
                }
                return match.Value;

            });

        }

        public static string ConvertStringLiteral(string text, out StringLiteralPrefix prefix)
        {
            if (text.StartsWith("u8"))
            {
                text = text[2..];
                prefix = StringLiteralPrefix.UTF8;
            }
            else if (text[0] != '"')
            {
                text = text[1..];
                prefix = text[0] == 'L' ? StringLiteralPrefix.WideString : text[0] == 'u' ? StringLiteralPrefix.UTF16 : StringLiteralPrefix.UTF32;
            }
            else
                prefix = StringLiteralPrefix.NoPrefix;
            text = ReplaceEscapeSequence(text[1..^1]);
            return text;
        }

        public static object ConvertCharacterLiteral(string text)
        {
            bool isunicode = false;
            if (text[0] != '\'')
            {
                text = text[1..];
                isunicode = true;
            }
                
            text = ReplaceEscapeSequence(text[1..^1]);
            return isunicode ? (object)text[0] : (byte)text[0];
        }

        public static PropertyInfo GetIndexer(Type type, Type indexer)
        {
            foreach (PropertyInfo pi in type.GetProperties())
            {
                if (pi.GetIndexParameters().Length == 1)
                {
                    if (pi.GetIndexParameters()[0].ParameterType == indexer)
                        return pi;
                }
            }
            return null;
        }

        public static PropertyInfo GetIntIndexer(Type type) => GetIndexer(type, typeof(int));
        public static PropertyInfo GetStringIndexer(Type type) => GetIndexer(type, typeof(int));

        public static Type LiftType(Type type1, Type type2)
        {
            if (type1 == type2)
                return type1;
            if (type1 == typeof(bool) && type2.IsPrimitive)
                return type2;
            if (type2 == typeof(bool) && type1.IsPrimitive)
                return type1;
            if (type1.IsPrimitive && type2.IsPrimitive)
            {
                int rank1 = (int)(TypeDefinition.FromClrType(type1) as BasicType).Rank;
                int rank2 = (int)(TypeDefinition.FromClrType(type2) as BasicType).Rank;
                return BasicType.FromTypeRank((BasicTypeRank)Math.Max(rank1,rank2)).ClrType;
            }
            if ((type1 == typeof(byte[]) || type1 == typeof(sbyte[]) || type1 == typeof(BinaryTemplateString) || type1 == typeof(char[])) && type2 == typeof(string))
                return typeof(string);
            if ((type2 == typeof(byte[]) || type2 == typeof(sbyte[]) || type2 == typeof(BinaryTemplateString) || type2 == typeof(char[])) && type1 == typeof(string))
                return typeof(string);
            return null;
        }


        public static object ChangeType(object obj, Type type)
        {
            if (obj.GetType() == type)
                return obj;
            if (obj.GetType() == typeof(bool) && type.IsPrimitive)
                return Convert.ToBoolean(obj) ? 1 : 0 ;
            if (type == typeof(bool) && obj.GetType().IsPrimitive)
                return Convert.ToUInt64(obj) != 0;
            if (obj.GetType().IsPrimitive && type.IsPrimitive)
                return Convert.ChangeType(obj, type);
            if (type == typeof(string))
            {
                if (obj.GetType() == typeof(BinaryTemplateString))
                {
                    return (obj as BinaryTemplateString).ToString();
                }
            }
           // if ((type1 == typeof(byte[]) || type1 == typeof(sbyte[]) || type1 == typeof(BinaryTemplateString) || type1 == typeof(char[])) && type2 == typeof(string))
            //    return typeof(string);
            //if ((type2 == typeof(byte[]) || type2 == typeof(sbyte[]) || type2 == typeof(BinaryTemplateString) || type2 == typeof(char[])) && type1 == typeof(string))
            //    return typeof(string);
            return null;
        }

        public static T ChangeType<T>(object obj)
        {
            return (T)ChangeType(obj, typeof(T));
        }

        public static Expression DynamicConvert(Expression testExpr, Type supertype)
        {
            return Expression.Dynamic(new BTConvertBinder(supertype, false), supertype, testExpr);
        }
    }
}
