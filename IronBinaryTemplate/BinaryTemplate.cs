using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using AST = System.Linq.Expressions;


namespace IronBinaryTemplate
{


    public class BinaryTemplate
    {
        public Dictionary<string, ICallableFunction> BuiltinFunctions { get; }

        public VariableCollection BuiltinConstants { get; }

        public Dictionary<string, TypeDefinition> BuiltinTypes { get; }
        public List<string> IncludeDirs { get;  }

        public BinaryTemplate()
        {
            IncludeDirs = new List<string>();
            BuiltinFunctions = new Dictionary<string, ICallableFunction>();
            BuiltinTypes = new Dictionary<string, TypeDefinition>();
            BuiltinTypes.Add("DOSTIME", BasicType.FromClrType(typeof(ushort)));
            BuiltinTypes.Add("DOSDATE", BasicType.FromClrType(typeof(ushort)));
            BuiltinFunctions.Add("sizeof", new ExternalFunction(typeof(LibraryFunctions).GetMethod("sizeof")));
            BuiltinFunctions.Add("startof", new ExternalFunction(typeof(LibraryFunctions).GetMethod("startof")));
            BuiltinFunctions.Add("parentof", new ExternalFunction(typeof(LibraryFunctions).GetMethod("parentof")));
            BuiltinFunctions.Add("exists", new TranslateNameToPathFunction(typeof(LibraryFunctions).GetMethod("exists")));
            BuiltinFunctions.Add("function_exists", new TranslateNameToPathFunction(typeof(LibraryFunctions).GetMethod("function_exists")));
            BuiltinConstants = new VariableCollection(false);
            BuiltinConstants.Add(new ConstVariable("true", BasicType.FromString("int"), 1));
            BuiltinConstants.Add(new ConstVariable("false", BasicType.FromString("int"), 0));
            BuiltinConstants.Add(new ConstVariable("TRUE", BasicType.FromString("int"), 1));
            BuiltinConstants.Add(new ConstVariable("FALSE", BasicType.FromString("int"), 0));

        }

        public BinaryTemplateRootScope RunTemplateFile(string templateFile, string file)
        {
            return RunTemplateFile(templateFile, File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read));
        }

        public BinaryTemplateRootScope RunTemplateFile(string templateFile, Stream stream)
        {
            BinaryTemplateContext context = new BinaryTemplateContext(stream);
            return RunTemplateString(File.ReadAllText(templateFile), context, templateFile);
        }

        public BinaryTemplateRootScope RunTemplateString(string template, string file)
        {
            return RunTemplateString(template, File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read));
        }

        public BinaryTemplateRootScope RunTemplateString(string template, Stream stream)
        {
            
            BinaryTemplateContext context = new BinaryTemplateContext(stream);
            return RunTemplateString(template, context);
        }

        public BinaryTemplateRootScope RunTemplateString(string template, BinaryTemplateContext context, string filename = "")
        {
            BinaryTemplateRootDefinition script = ParseTemplateCode(template, filename);

            BinaryTemplateRootScope scope = new BinaryTemplateRootScope(context, script);

            if (script.Errors.Count == 0)
            {
                var func = script.Compile();
                func(context, scope);
            }
            
            return scope;
        }
        public class SyntaxErrorListener : IAntlrErrorListener<IToken>, IAntlrErrorListener<int>
        {
            public List<BinaryTemplateError> Errors = new List<BinaryTemplateError>();

            public void SyntaxError([NotNull] IRecognizer recognizer, [Nullable] IToken offendingSymbol, int line, int charPositionInLine, [NotNull] string msg, [Nullable] RecognitionException e)
            {
                Console.WriteLine($"line:{line} column:{charPositionInLine} {msg}");
                Errors.Add(new SyntaxError(msg, new SourceLocation(offendingSymbol.StartIndex, line, charPositionInLine)));
            }

            public void SyntaxError([NotNull] IRecognizer recognizer, [Nullable] int offendingSymbol, int line, int charPositionInLine, [NotNull] string msg, [Nullable] RecognitionException e)
            {
                Console.WriteLine($"line:{line} column:{charPositionInLine} {msg}");
                Errors.Add(new SyntaxError(msg, new SourceLocation(offendingSymbol, line, charPositionInLine)));
            }
        }

        public BinaryTemplateRootDefinition ParseTemplateCode(string templateCode, string filename = "<script>")
        {
            var textstream = new Antlr4.Runtime.AntlrInputStream(templateCode);

            BinaryTemplatePreprocessingLexer lexer = new BinaryTemplatePreprocessingLexer(textstream);
            var errorListener = new SyntaxErrorListener();
            try
            {
                if (filename != "<script>")
                    lexer.IncludeDirs.Add(Path.GetDirectoryName(filename));
                lexer.IncludeDirs.AddRange(this.IncludeDirs);
                lexer.AddErrorListener(errorListener);
                BinaryTemplateParser parser = new BinaryTemplateParser(new Antlr4.Runtime.CommonTokenStream(lexer));
                BinaryTemplateASTVisitor visitor = new BinaryTemplateASTVisitor(parser, this);
                parser.AddErrorListener(errorListener);
                var tree = parser.compilationUnit();
                
                var script = visitor.VisitCompilationUnit(tree);
                script.Name = filename;
                return script;
            }
            catch (PreprocessorError ex)
            {
                errorListener.Errors.Add(ex);
            }
            catch (SemanticError ex)
            {
                errorListener.Errors.Add(ex);
            }
            return new BinaryTemplateRootDefinition(this, errorListener.Errors);
        }

        public bool RegisterClrFunction(string name, Delegate d, bool overwrite = true)
        {
            ICallableFunction function;
            if (BuiltinFunctions.TryGetValue(name, out function))
            {
                if (overwrite)
                    BuiltinFunctions[name] = new ExternalDelegate(d);
                else
                    return false;
            }
            else
                BuiltinFunctions.Add(name, new ExternalDelegate(d));
            return true;
        }
        public bool RegisterClrFunction(string name, MethodInfo method, bool overwrite = true)
        {
            ICallableFunction function;
            if (BuiltinFunctions.TryGetValue(name, out function))
            {
                if (overwrite)
                    BuiltinFunctions[name] = new ExternalFunction(method);
                else
                    return false;
            } else
                BuiltinFunctions.Add(name, new ExternalFunction(method));
            return true;
        }

        public void RegisterClrFunctions(Type type, bool overwrite = true, string prefix="")
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (method.GetCustomAttribute<TemplateCallableAttribute>() != null)
                    RegisterClrFunction(prefix + method.Name, method, overwrite);
            }
        }


        public void RegisterMathFunctions()
        {
            var funcset = new HashSet<string>() { "Abs", "Ceiling", "Cos", "Exp", "Floor", "Log", "Log10", "Max", "Min", "Pow", "Sin", "Sqrt", "Tan" };
            foreach (var method in typeof(Math).GetMethods())
            {
                if (method.ReturnType == typeof(double) && funcset.Contains(method.Name))
                {
                    RegisterClrFunction(method.Name == "Ceiling" ? "Ceil" : method.Name, method);
                }
                    
            }
            RegisterClrFunction("Random", typeof(LibraryFunctions).GetMethod("Random"));
        }
        public void RegisterBuiltinFunctions()
        {
            RegisterClrFunctions(typeof(BinaryTemplateContext)); //IO functions
            RegisterMathFunctions();
            RegisterClrFunctions(typeof(LibraryFunctions));
        }
    }


    public class TemplateCallableAttribute : Attribute
    {

    }



    public class DefinitionCollection : KeyedCollection<string, VariableDeclaration>
    {
        protected override string GetKeyForItem(VariableDeclaration item)
        {

            return item.Name;
        }
    }


    public enum StringLiteralPrefix
    {
        NoPrefix,
        WideString,
        UTF8,
        UTF16,
        UTF32
    }

    public class BinaryTemplateASTVisitor : IronBinaryTemplate.BinaryTemplateBaseVisitor<AST.Expression>
    {

        Parser parser;

        public BinaryTemplateRootDefinition Global { get; }

        BinaryTemplate Runtime { get; }

        public BinaryTemplateASTVisitor(Parser p, BinaryTemplate runtime)
        {
            parser = p;
            Runtime = runtime;
            Global = new BinaryTemplateRootDefinition(runtime);
            CurrentScope = new AnalysisScope(null,AnalysisScope.ScopeType.Function, "$script", Global);
        }
        public override AST.Expression VisitConstant([NotNull] BinaryTemplateParser.ConstantContext context)
        {
            if (context.IntegerConstant()!=null)
            {
                return new ConstExpr(RuntimeHelpers.ConvertIntLiteral(context.IntegerConstant().GetText()));
            } else if (context.FloatingConstant() != null)
            {
                return new ConstExpr(RuntimeHelpers.ConvertFloatLiteral(context.FloatingConstant().GetText()));
            } else if (context.CharacterConstant() != null)
            {
                return new ConstExpr(RuntimeHelpers.ConvertCharacterLiteral(context.CharacterConstant().GetText()));
            }
            return null;
        }

        public override AST.Expression VisitPrimaryExpression([NotNull] BinaryTemplateParser.PrimaryExpressionContext context)
        {
          if (context.Identifier() != null)
                return new VariableAccessExpr(context.Identifier().GetText(), CurrentScope.LexicalScope);
            if (context.StringLiteral().Length != 0)
            {
                string result =null;
                StringLiteralPrefix prefix = StringLiteralPrefix.NoPrefix;
                foreach (var str in context.StringLiteral())
                {
                    
                    if (result == null)
                        result = RuntimeHelpers.ConvertStringLiteral(str.GetText(), out prefix);
                    else
                    {
                        result += RuntimeHelpers.ConvertStringLiteral(str.GetText(), out StringLiteralPrefix curprefix);
                        if (curprefix != prefix)
                        {
                            if (prefix == StringLiteralPrefix.NoPrefix)
                                prefix = curprefix;
                            else if (curprefix == StringLiteralPrefix.NoPrefix)
                                continue;
                            else
                                AddError("Different string literal prefix", str);
                        }
                    }
                }
                if (prefix == StringLiteralPrefix.NoPrefix)
                    return new ConstExpr(new BinaryTemplateString(result,Encoding.Default));
                else if (prefix == StringLiteralPrefix.UTF8)
                    return new ConstExpr(new BinaryTemplateString(result));
                return new ConstExpr(result);
            }
            if (context.expression() != null)
                return VisitExpression(context.expression());
            return base.VisitPrimaryExpression(context);
        }
        public override AST.Expression VisitPostfixExpression([NotNull] BinaryTemplateParser.PostfixExpressionContext context)
        {
            if (context.primaryExpression() != null)
                return VisitPrimaryExpression(context.primaryExpression());
            Expr expr = null;
            if (context.postfixExpression() != null)
                expr = VisitPostfixExpression(context.postfixExpression()) as Expr;
            if (context.expression() != null)
                return new ArrayAccessExpr(expr, VisitExpression(context.expression()) as Expr);
            string op = context.GetChild(1).GetText();
            if (op == "++")
                return new UnaryExpr("_++", expr);
            else if (op == "--")
                return new UnaryExpr("_--", expr);
            else if (op == "(")
            {
                string funcname = context.Identifier().GetText();
                List<AST.Expression> exprs = context.argumentExpressionList() == null ? null : VisitArgumentExpressionList(context.argumentExpressionList());
                var funccallexpr = new FunctionCallExpr(funcname, CurrentScope.LexicalScope, exprs);
                if (Global.TryGetFunctions(funcname, out ICallableFunction value))
                    funccallexpr.Function = value;
                else
                    Console.WriteLine($"Function {funcname} not found.");

                return funccallexpr;
            }
            else if (op == ".")
                return new MemberAccessExpr(expr, context.Identifier().GetText());

            return null;
        }

        public new List<AST.Expression> VisitArgumentExpressionList([NotNull] BinaryTemplateParser.ArgumentExpressionListContext context)
        {
            List<Expression> exprs = new List<Expression>();
            foreach (var expr in context.assignmentExpression())
            {
                exprs.Add(VisitAssignmentExpression(expr));
            }
            return exprs;
        }

        public override AST.Expression VisitUnaryExpression([NotNull] BinaryTemplateParser.UnaryExpressionContext context)
        {
            if (context.postfixExpression() != null)
                return VisitPostfixExpression(context.postfixExpression());
            Expression expr = null;
            if (context.unaryExpression() != null)
                expr = VisitUnaryExpression(context.unaryExpression());
            if (context.castExpression() != null)
                expr = VisitCastExpression(context.castExpression());
            string op = context.GetChild(0).GetText();
            
            
            if (op == "sizeof")
            {
                if (context.typeName() != null)
                {
                    var typename = VisitTypeName(context.typeName()) as VariableDeclaration;
                    var size = typename.TypeDefinition.Size;
                    if (!size.HasValue)
                        AddError("Can not calculate size of dynamic type.", context.typeName().Start);
                    return new ConstExpr(size.Value);
                }
                else if (expr is VariableAccessExpr varaccess)
                {
                    if (Global.TryGetType(varaccess.VariableName, out TypeDefinition typedef))
                    {
                        var size = typedef.Size;
                        if (!size.HasValue)
                            AddError("Can not calculate size of dynamic type.", context.typeName().Start);
                        return new ConstExpr(size.Value);
                    }
                }
                var funccallexpr = new FunctionCallExpr("sizeof", CurrentScope.LexicalScope, new() { expr});
                if (Runtime.BuiltinFunctions.TryGetValue("sizeof", out ICallableFunction value))
                    funccallexpr.Function = value;

                return funccallexpr;
            }

            UnaryExpr result = new UnaryExpr(op, expr as Expr);
            return result;
        }

        public override Expression VisitCastExpression([NotNull] BinaryTemplateParser.CastExpressionContext context)
        {
            if (context.castExpression() != null)
            {
                var typename = VisitTypeName(context.typeName()) as VariableDeclaration;

                return new CastExpr(typename.TypeDefinition, VisitCastExpression(context.castExpression()) as Expr);
                
            }
            
            return base.VisitCastExpression(context);
        }


        public override AST.Expression VisitAdditiveExpression([NotNull] BinaryTemplateParser.AdditiveExpressionContext context)
            => VisitBinaryExpression(context);
        public override AST.Expression VisitMultiplicativeExpression([NotNull] BinaryTemplateParser.MultiplicativeExpressionContext context)
            => VisitBinaryExpression(context);

        public override AST.Expression VisitShiftExpression([NotNull] BinaryTemplateParser.ShiftExpressionContext context)
            => VisitBinaryExpression(context);

        public override AST.Expression VisitRelationalExpression([NotNull] BinaryTemplateParser.RelationalExpressionContext context)
            => VisitBinaryExpression(context);

        public override AST.Expression VisitEqualityExpression([NotNull] BinaryTemplateParser.EqualityExpressionContext context)
            => VisitBinaryExpression(context);

        public override AST.Expression VisitAndExpression([NotNull] BinaryTemplateParser.AndExpressionContext context)
            => VisitBinaryExpression(context);
        public override AST.Expression VisitExclusiveOrExpression([NotNull] BinaryTemplateParser.ExclusiveOrExpressionContext context)
            => VisitBinaryExpression(context);

        public override AST.Expression VisitInclusiveOrExpression([NotNull] BinaryTemplateParser.InclusiveOrExpressionContext context)
            => VisitBinaryExpression(context);

        public override AST.Expression VisitLogicalAndExpression([NotNull] BinaryTemplateParser.LogicalAndExpressionContext context)
            => VisitBinaryExpression(context);

        public override AST.Expression VisitLogicalOrExpression([NotNull] BinaryTemplateParser.LogicalOrExpressionContext context)
            => VisitBinaryExpression(context);

        public override Expression VisitConditionalExpression([NotNull] BinaryTemplateParser.ConditionalExpressionContext context)
        {
            Expression logicalexpr = VisitLogicalOrExpression(context.logicalOrExpression());
            if (context.expression() != null)
            {
                return new ConditionalExpr(logicalexpr as Expr,
                    VisitExpression(context.expression()) as Expr,
                    VisitConditionalExpression(context.conditionalExpression()) as Expr);
            }
            return logicalexpr;
        }

        public override Expression VisitAssignmentExpression([NotNull] BinaryTemplateParser.AssignmentExpressionContext context)
        {
            if (context.unaryExpression() != null)
            {
                var lhs = VisitUnaryExpression(context.unaryExpression());
                var oper = context.assignmentOperator().GetText();
                var rhs = VisitAssignmentExpression(context.assignmentExpression());
                if (lhs is ILValue lvalue)
                {
                    return new BinaryExpr(oper, lhs as Expr, rhs as Expr);
                }
                else
                {
                    AddError("Assignment target must have lvalue.", context.start);
                }
            }
            return base.VisitAssignmentExpression(context);
        }


        TypeDefinition ChangeDefinitionType(TypeDefinition def,bool issigned)
        {
            if (def.TypeKind == TypeKind.Integer)
            {
                BasicType basictype = def as BasicType;
                if (basictype.IsSigned != issigned)
                    return BasicType.FromTypeRank((BasicTypeRank)(((int)basictype.Rank & ~3) | (issigned ? 3: 0)));
            }
            return null;
        }


        public override AST.Expression VisitDeclaration([NotNull] BinaryTemplateParser.DeclarationContext context)
        {
            VariableDeclaration typedef = VisitDeclarationSpecifiers(context.declarationSpecifiers()) as VariableDeclaration;
            if (typedef.TypeDefinition.IsStructOrUnion && typedef.TypeDefinition.Name != null)
                Global.Typedefs.Add(typedef.TypeDefinition);
            if (context.initDeclaratorList() == null)
            {
                return null;
            }
            
            List<VariableDeclaration> decls = VisitInitDeclaratorList(context.initDeclaratorList(), typedef);
            Debug.Assert(CurrentScope.LexicalScope is CompoundDefinition);
            foreach (var decl in decls)
            {
                decl.Parent = CurrentScope.LexicalScope as CompoundDefinition;
            }
            if (context.Typedef() != null)
            {
                if (typedef.IsArray && !typedef.IsConstArray)
                    AddError("Array in typedefs must be const.", context.start);
                foreach (var decl in decls)
                {
                    Global.Typedefs.Add(new TypeAliasDefinition(decl.Name, typedef.TypeDefinition));
                }
                return null;

            }
            else if (decls.Count == 1)
            {
                return decls[0];
            }
            else
            {
                
                return AST.Expression.Block(decls);
            }
                
        }


        public override AST.Expression VisitDeclarationSpecifiers([NotNull] BinaryTemplateParser.DeclarationSpecifiersContext context)
        {
            TypeDefinition typedef = VisitTypeSpecifier(context.typeSpecifier()) as TypeDefinition;
            VariableDeclaration vardef = new VariableDeclaration(typedef);
            foreach (var specifier in context.declarationSpecifier())
            {
                switch (specifier.GetText())
                {
                    case "local": vardef.IsLocal = true; break;
                    case "const": vardef.IsConst = true; break;
                    case "unsigned":
                    case "signed":
                        var newtype = ChangeDefinitionType(typedef, specifier.GetText() == "signed");
                        if (newtype == null)

                            AddError($"Type specifier {specifier.GetText()} cannot be used on type {typedef.Name}",context.start);
                        break;
                }
            }
            return vardef;
        }

        public List<VariableDeclaration> VisitInitDeclaratorList([NotNull] BinaryTemplateParser.InitDeclaratorListContext context, VariableDeclaration vardef)
        {
            List<VariableDeclaration> decls;
            if (context.initDeclaratorList() != null)
            {
                decls = VisitInitDeclaratorList(context.initDeclaratorList(), vardef);
            }
            else
            {
                decls = new List<VariableDeclaration>();
            }
            VariableDeclaration vardecl = VisitInitDeclarator(context.initDeclarator(), vardef.Clone()) as VariableDeclaration;
            if (context.customAttributeSpecifier() != null)
                vardecl.CustomAttributes = VisitCustomAttributeSpecifier(context.customAttributeSpecifier());
            decls.Add(vardecl);
            return decls;
        }


        public AST.Expression VisitInitDeclarator([NotNull] BinaryTemplateParser.InitDeclaratorContext context, VariableDeclaration vardef)
        {
            VisitDeclarator(context.declarator(), vardef);
            if (context.initializer() != null)
                vardef.Initializer = VisitInitializer(context.initializer());
            return vardef;
        }


        public new TypeDefinition VisitTypeSpecifier([NotNull] BinaryTemplateParser.TypeSpecifierContext context)
        {
            if (context.basicType() != null || context.GetText() == "void")
            {
                return TypeDefinition.FromString(context.GetText());
            }
            else if (context.structOrUnionSpecifier() != null)
            {
                return VisitStructOrUnionSpecifier(context.structOrUnionSpecifier());
            }
            else if (context.enumSpecifier() != null)
            {
                return VisitEnumSpecifier(context.enumSpecifier());
            }
            else if (context.Identifier() != null)
            {
                string typename = context.Identifier().GetText();
                TypeDefinition typedef;
                if (Global.TryGetType(typename, out typedef))
                    return typedef;
                else
                    throw new Exception($"Type {typename} not found.");

            }
            return null;
        }

        public new TypeDefinition VisitStructOrUnionSpecifier([NotNull] BinaryTemplateParser.StructOrUnionSpecifierContext context)
        {

            var deftype = context.structOrUnion().GetText() == "struct" ? TypeKind.Struct : TypeKind.Union;
            var text = context.Identifier()?.GetText();
            CompoundDefinition structdef;
            List<VariableDeclaration> paramlist = null;
            if (context.parameterTypeList() != null)
            {
                paramlist = VisitParameterTypeList(context.parameterTypeList());
            }
                
            if (context.compoundStatement() != null)
            {
                structdef = Global.FindOrDefineType(deftype, text, true) as CompoundDefinition;
                
                AnalysisScope scope = new AnalysisScope(this.CurrentScope, AnalysisScope.ScopeType.StructOrUnion, text, structdef);
                PushScope(scope);
                BlockExpression expr = VisitCompoundStatement(context.compoundStatement()) as BlockExpression;
                foreach (var subexpr in expr.Expressions)
                    structdef.AddStatement(subexpr);
                PopScope();
                structdef.IsComplete = true;

            }
            else
            {
                structdef = Global.FindOrDefineType(deftype, text, false) as CompoundDefinition;
            }
            if (context.parameterTypeList() != null)
            {
                //Todo: Check byref params
                foreach (var param in paramlist)
                {
                    ParameterExpression paramexpr;
                    paramexpr = Expression.Parameter(param.TypeDefinition.ClrType, param.Name);
                    structdef.Parameters.Add(paramexpr);
                }
            }
                
            return structdef;
        }

        public new TypeDefinition VisitEnumSpecifier([NotNull] BinaryTemplateParser.EnumSpecifierContext context)
        {
            string enumname = context.Identifier()?.GetText();
            TypeDefinition typedef = null;
            if (context.enumeratorList() == null)
            {
                Global.TryGetType(enumname, out typedef);
                if (typedef.IsEnum)
                    return typedef;
                else
                    AddError($"Inconsistency declaration for enum {enumname}", context.start);

            }

            if (context.enumTypeSpecifier() != null)
            {
                typedef = VisitEnumTypeSpecifier(context.enumTypeSpecifier());
            }
            EnumDefinition enumdef = new EnumDefinition(enumname, typedef?.ClrType);
            Global.Typedefs.Add(enumdef);
            AnalysisScope scope = new AnalysisScope(CurrentScope, enumdef);
            PushScope(scope);
            List<Tuple<string,Expr>> enumeratorList = VisitEnumeratorList(context.enumeratorList());
            PopScope();
            if (typedef == null)
                typedef = BasicType.FromString("int");
            foreach (var (key,expr) in enumeratorList)
            {
                object value = null;
                
                if (expr == null)
                {
                    if (enumdef.Values.Count == 0)
                        value = Activator.CreateInstance(enumdef.ClrType);
                    else
                        value = (enumdef.Values[enumdef.Values.Count - 1].Value as dynamic) + 1;
                } else if (expr is Expr btexpr)
                {
                    value = Convert.ChangeType(btexpr.Eval(Global), typedef.ClrType);
                } else
                {
                    throw new InvalidOperationException("Not supported expr type in enum.");
                }
                var constvar = new ConstVariable(key, typedef, value);

                if (Global.AddConst(constvar))
                    enumdef.Values.Add(constvar);
                else
                    AddError($"Enum const {key} already defined.",context.start);
            }
            return enumdef;
        }

        public new TypeDefinition VisitEnumTypeSpecifier([NotNull] BinaryTemplateParser.EnumTypeSpecifierContext context)
        {
            VariableDeclaration vardef = VisitDeclarationSpecifiers(context.declarationSpecifiers()) as VariableDeclaration;
            TypeDefinition typedef = vardef.TypeDefinition;
            if (typedef == null || typedef.TypeKind != TypeKind.Integer )
                AddError("Enum must be integral type.", context.declarationSpecifiers().Start);
            return typedef;
        }

        public List<Tuple<string, Expr>> VisitEnumeratorList([NotNull] BinaryTemplateParser.EnumeratorListContext context)
        {
            List<Tuple<string, Expr>> list;
            if (context.enumeratorList() != null)
            {
                list = VisitEnumeratorList(context.enumeratorList());
            }
            else
            {
                list = new List<Tuple<string, Expr>>();
            }
            list.Add(VisitEnumerator(context.enumerator()));
            return list;
        }

        public Tuple<string, Expr> VisitEnumerator([NotNull] BinaryTemplateParser.EnumeratorContext context)
        {
            var tuple = new Tuple<string, Expr>(context.Identifier().GetText(), context.constantExpression() == null ? null : VisitConstantExpression(context.constantExpression()) as Expr);
            return tuple;
        }

        public AST.Expression VisitDeclarator([NotNull] BinaryTemplateParser.DeclaratorContext context, VariableDeclaration vardef)
        {
            if (context.varDeclarator() != null)
                return VisitVarDeclarator(context.varDeclarator(), vardef);
            if (context.bitfieldDeclarator() != null)
                return VisitBitfieldDeclarator(context.bitfieldDeclarator(),  vardef);
            return null;
        }

        public Expression VisitBitfieldDeclarator([NotNull] BinaryTemplateParser.BitfieldDeclaratorContext context, VariableDeclaration vardef)
        {
            if (context.Identifier() != null)
                vardef.Name = context.Identifier().GetText();
            if (!vardef.TypeDefinition.IsBasicType)
                AddError("Bitfield can only be integral.", context.start);
            vardef.BitfieldExpression = VisitConstantExpression(context.constantExpression());
             return vardef;
        }
        public AST.Expression VisitVarDeclarator([NotNull] BinaryTemplateParser.VarDeclaratorContext context, VariableDeclaration vardef)
        {
            if (context.Identifier() !=null)
            {
                vardef.Name = context.Identifier().GetText();
                vardef.Arguments= context.argumentExpressionList()!=null ? VisitArgumentExpressionList(context.argumentExpressionList()) : null;
            } else
            {
                VisitVarDeclarator(context.varDeclarator(), vardef);
                vardef.ArrayDimensions.Add(context.assignmentExpression() == null ? null : VisitAssignmentExpression(context.assignmentExpression()));
            }
            return vardef;
        }

        public CustomAttributeCollection VisitCustomAttributeSpecifier([NotNull] BinaryTemplateParser.CustomAttributeSpecifierContext context)
        {
            return VisitCustomAttributeList(context.customAttributeList());
        }

        public CustomAttributeCollection VisitCustomAttributeList([NotNull] BinaryTemplateParser.CustomAttributeListContext context)
        {
            CustomAttributeCollection attributes = new ();
            foreach (var attrexpr in context.customAttribute())
            {
                attributes.Add(VisitCustomAttribute(attrexpr));
            }
            return attributes;
        }

        public CustomAttribute VisitCustomAttribute([NotNull] BinaryTemplateParser.CustomAttributeContext context)
        {
            var name = context.Identifier().GetText();
            var attribute = new CustomAttribute(name);
            var scope = new AnalysisScope(CurrentScope, attribute);
            PushScope(scope);
            var expr = VisitPrimaryExpression(context.primaryExpression());
            PopScope();
            attribute.Expr = expr as Expr;
            return attribute;
        }


        FunctionDefinition DeclareFunction(BinaryTemplateParser.FunctionDeclarationContext context)
        {
            string funcName = context.functionDeclarator().Identifier().GetText();

            List<VariableDeclaration> paramlist = null;
            if (context.functionDeclarator().parameterTypeList() != null)
                paramlist = VisitParameterTypeList(context.functionDeclarator().parameterTypeList());
            else
                paramlist =new List<VariableDeclaration>();
            var returntype = VisitDeclarationSpecifiers(context.declarationSpecifiers()) as VariableDeclaration; 

            FunctionDefinition func;
            if (Global.Functions.ContainsKey(funcName))
            {
                func = Global.Functions[funcName];

            } else
            {
                func = new FunctionDefinition(funcName, paramlist, returntype.TypeDefinition);
                Global.Functions.Add(funcName, func);
            }

            return func;
        }

        public override AST.Expression VisitFunctionDeclarator([NotNull] BinaryTemplateParser.FunctionDeclaratorContext context)
        {
            return base.VisitFunctionDeclarator(context);
        }

        public new List<VariableDeclaration> VisitParameterTypeList([NotNull] BinaryTemplateParser.ParameterTypeListContext context)
        {
            var decls = new List<VariableDeclaration>();
            foreach (var decl in context.parameterDeclaration())
            {
                decls.Add(VisitParameterDeclaration(decl));
            }
            return decls;
        }

        public new void VisitParamDeclarator([NotNull] BinaryTemplateParser.ParamDeclaratorContext context, VariableDeclaration vardef)
        {
            if (context.assignmentExpression() != null)
            {
                vardef.ArrayDimensions.Add(context.assignmentExpression() == null ? null : VisitAssignmentExpression(context.assignmentExpression()));
                if (context.paramDeclarator() != null)
                    VisitParamDeclarator(context.paramDeclarator(), vardef);
            }
            else
            {
                vardef.Name = context.Identifier().GetText();
                if (context.ChildCount == 2)
                    vardef.IsReference = true;
            }
        }

        public new VariableDeclaration VisitParameterDeclaration([NotNull] BinaryTemplateParser.ParameterDeclarationContext context)
        {
            VariableDeclaration vardef = VisitDeclarationSpecifiers(context.declarationSpecifiers()) as VariableDeclaration;
            if (context.abstractDeclarator() != null)
                VisitAbstractDeclarator(context.abstractDeclarator(),vardef);
            else
                VisitParamDeclarator(context.paramDeclarator(), vardef);
            
            return vardef;
        }


        public void VisitAbstractDeclarator([NotNull] BinaryTemplateParser.AbstractDeclaratorContext context, VariableDeclaration vardef)
        {
            VisitDirectAbstractDeclarator(context.directAbstractDeclarator(),vardef);
        }
        public void VisitDirectAbstractDeclarator([NotNull] BinaryTemplateParser.DirectAbstractDeclaratorContext context, VariableDeclaration vardef)
        {
            vardef.ArrayDimensions.Add(context.assignmentExpression()==null ? null : VisitAssignmentExpression(context.assignmentExpression()));
            if (context.directAbstractDeclarator() != null)
                VisitDirectAbstractDeclarator(context.directAbstractDeclarator(), vardef);
        }

        public new Initializer VisitInitializer([NotNull] BinaryTemplateParser.InitializerContext context)
        {
            if (context.assignmentExpression() != null)
                return new Initializer(VisitAssignmentExpression(context.assignmentExpression()));
            else
                return VisitInitializerList(context.initializerList());
        }

        public new Initializer VisitInitializerList([NotNull] BinaryTemplateParser.InitializerListContext context)
        {
            Initializer result;
            if (context.initializerList() == null)
            {
                result = new Initializer();
            } else
            {
                result = VisitInitializerList(context.initializerList());
            }
            result.Initializers.Add(VisitInitializer(context.initializer()));
            return result;
        }

        public override AST.Expression VisitStatement([NotNull] BinaryTemplateParser.StatementContext context)
        {
            AST.Expression stmt = base.VisitStatement(context);
            //CurrentScope.Statements.Add(stmt);
            return stmt;
        }

        public override AST.Expression VisitLabeledStatement([NotNull] BinaryTemplateParser.LabeledStatementContext context)
        {
            Expression stmt = VisitStatement(context.statement());
            if (!CurrentScope.IsSwitch)
                AddError("Case label can only appear in switch statement.", context.start);

            SwitchCaseStmt switchcase;
            switchcase = stmt as SwitchCaseStmt;
            if (switchcase == null)
            {
                switchcase = new SwitchCaseStmt();
                switchcase.Statements.Add(stmt);
            }

            if (context.constantExpression() != null)
            {
                switchcase.TestValues.Add(VisitConstantExpression(context.constantExpression()) as Expr);
            }
            else
                switchcase.IsDefault = true;
            return switchcase;
        }

        public override AST.Expression VisitCompoundStatement([NotNull] BinaryTemplateParser.CompoundStatementContext context)
        {
            List<AST.Expression> stmtlist = new List<AST.Expression>();
            foreach (var stmt in context.statement())
            {
                var expr = VisitStatement(stmt);
                if (expr == null)
                    continue;
                stmtlist.Add(expr);
            }
            return AST.Expression.Block(stmtlist.ToArray());
        }

        public override Expression VisitExpressionStatement([NotNull] BinaryTemplateParser.ExpressionStatementContext context)
        {
            if (context.expression() != null)
                return VisitExpression(context.expression());
            return null;
        }

        public override AST.Expression VisitSelectionStatement([NotNull] BinaryTemplateParser.SelectionStatementContext context)
        {

            if (context.If() != null)
            {

                var elsestmt = context.statement(1);
                return elsestmt == null ? AST.Expression.IfThen(Expression.Dynamic(new BTConvertBinder(typeof(bool),false),typeof(bool),VisitExpression(context.expression())), VisitStatement(context.statement(0))) :
                    AST.Expression.IfThenElse(Expression.Dynamic(new BTConvertBinder(typeof(bool), false), typeof(bool), VisitExpression(context.expression())), VisitStatement(context.statement(0)), VisitStatement(context.statement(1)));

            }
            else if (context.Switch() != null)
            {
                AnalysisScope scope = new AnalysisScope(this.CurrentScope, AnalysisScope.ScopeType.Switch);
                PushScope(scope);
                var expr = VisitExpression(context.expression());
                var block = VisitCompoundStatement(context.compoundStatement()) as BlockExpression;
                
                //scope.SwitchCases
                PopScope();
                return new SwitchStmt(expr as Expr, block, scope.LoopBreak);
            }
            return null;
        }

        public override AST.Expression VisitIterationStatement([NotNull] BinaryTemplateParser.IterationStatementContext context)
        {
            AnalysisScope scope = new AnalysisScope(this.CurrentScope, AnalysisScope.ScopeType.Loop);
            PushScope(scope);
            AST.Expression statement = VisitStatement(context.statement());
            
            if (context.Do() != null)
            {
                AST.Expression expression = VisitExpression(context.expression());
                PopScope();
                return new DoWhileStmt(expression,statement,scope.LoopBreak,scope.LoopContinue);
                
            }
            if (context.While() != null)
            {
                AST.Expression expression = VisitExpression(context.expression());
                PopScope();
                return new WhileStmt(expression, statement, scope.LoopBreak, scope.LoopContinue);
                
            }
            
            if (context.For() != null)
            {
                AST.Expression initexpr = null;
                if (context.forCondition().forInit != null)
                {
                    initexpr = VisitExpression(context.forCondition().forInit);
                }
                AST.Expression condtexpr = null;
                if (context.forCondition().forExpression != null)
                {
                    condtexpr = VisitExpression(context.forCondition().forExpression);
                }
                AST.Expression updateexpr = null;
                if (context.forCondition().forUpdate != null)
                {
                    updateexpr = VisitExpression(context.forCondition().forUpdate);
                }
                AST.Expression body = VisitStatement(context.statement());
                
                PopScope();
                return new ForStmt(initexpr,condtexpr,updateexpr,body,scope.LoopBreak,scope.LoopContinue);
                //return AST.Expression.Loop((context);
            }

            
            return base.VisitIterationStatement(context);
        }

        public override AST.Expression VisitJumpStatement([NotNull] BinaryTemplateParser.JumpStatementContext context)
        {
            AnalysisScope scope;
            if (context.Continue() != null)
            {
                scope = FindScope(s => s.IsLoop);
                if (scope == null)
                    AddError("Continue must inside a loop statement.", context.Start);
                else
                    return new ContinueStmt(Expression.Continue(scope.LoopContinue));
            }
            if (context.Break() != null)
            {
                scope = FindScope(s => s.CanBreak);
                if (scope == null)
                    AddError("Break must inside a loop or switch statement.", context.Start);
                else
                    return new BreakStmt(Expression.Break(scope.LoopBreak));
            }
            if (context.Return() != null)
            {
                scope = FindScope(s => s.IsFunction);
                if (context.expression() != null)
                {
                    var expr = VisitExpression(context.expression());
                    if (scope.LexicalScope == Global)
                    {
                        return new ReturnStmt(Expression.Throw(Expression.New(typeof(ExitException).GetConstructor(new[] { typeof(int) }), RuntimeHelpers.DynamicConvert(expr, typeof(int)))));
                    }
                    return new ReturnStmt(Expression.Return(scope.FuncReturn,RuntimeHelpers.DynamicConvert(expr,scope.FuncReturn.Type)));
                }
                return new ReturnStmt(Expression.Return(scope.FuncReturn));
            }
            return null;
        }


        public override AST.Expression VisitExternalDeclaration([NotNull] BinaryTemplateParser.ExternalDeclarationContext context)
        {
            if (context.functionDeclaration() != null)
            {
                DeclareFunction(context.functionDeclaration());
            }
            else if (context.functionDefinition() != null)
            {
                var func = DeclareFunction(context.functionDefinition().functionDeclaration());
                AnalysisScope scope = new AnalysisScope(CurrentScope, func);
                PushScope(scope);
                func.Body = VisitCompoundStatement(context.functionDefinition().compoundStatement());
                func.Body = Expression.Block(func.Body, Expression.Label(CurrentScope.FuncReturn));
                PopScope();
            }
            else if (context.statement() != null)
            {
                var expr = VisitStatement(context.statement());
                Global.AddStatement(expr);
            }
            return null;
        }

        public new BinaryTemplateRootDefinition VisitCompilationUnit([NotNull] BinaryTemplateParser.CompilationUnitContext context)
        {
            foreach (var decl in context.externalDeclaration())
            {
                if (decl.functionDeclaration() != null)
                {
                    DeclareFunction(decl.functionDeclaration());
                }
                else if (decl.functionDefinition() != null)
                {
                    var func = DeclareFunction(decl.functionDefinition().functionDeclaration());
                    AnalysisScope scope = new AnalysisScope(CurrentScope, func);
                    PushScope(scope);
                    func.Body = VisitCompoundStatement(decl.functionDefinition().compoundStatement());
                    func.Body = Expression.Block(func.Body, Expression.Label(CurrentScope.FuncReturn,Expression.Default(CurrentScope.FuncReturn.Type)));
                    PopScope();
                }else 
                if (decl.statement() != null)
                {
                    var expr = VisitStatement(decl.statement());
                    if (expr != null)
                        Global.AddStatement(expr);
                }
            }
            Global.Statements.Add(Expression.Label(CurrentScope.FuncReturn));
            return Global;
        }


        private void PopScope()
        {
            if (CurrentScope != null)
                this.CurrentScope = this.CurrentScope.Parent;
        }

        private void PushScope(AnalysisScope scope)
        {
            CurrentScope = scope;
        }

        private void AddError(string error, ITerminalNode start)
        {
            AddError(error, start.Symbol);
        }

        private void AddError(string error, IToken start)
        {
            AddError(error, start.StartIndex, start.Line, start.Column);
        }


        void AddError(string error,int index, int line,int column)
        {
            Global.Errors.Add(new SemanticError(error,new SourceLocation(index,line,column)));
        }


        private AnalysisScope FindScope(Func<AnalysisScope,bool> func)
        {
            var curscope = CurrentScope;
            while (curscope != null)
            {
                if (func(curscope))
                    return curscope;
                else
                    curscope = curscope.Parent;
            }
            return null;
        }

        private AnalysisScope CurrentScope;


        public AST.Expression VisitBinaryExpression(ParserRuleContext context)
        {
            if (context.ChildCount == 1)
                return Visit(context.GetChild(0));
            else
                return new BinaryExpr(context.GetChild(1).GetText(), Visit(context.GetChild(0)) as Expr, Visit(context.GetChild(2)) as Expr);
        }
    }

    internal class AnalysisScope
    {
        public enum ScopeType
        {
            If,
            Switch,
            Loop,
            Function,
            StructOrUnion,
            Enum,
            Attribute,
        }

        private ScopeType _scopetype;

        private AnalysisScope _parent;
        private string _name;
        // Need runtime for interning Symbol constants at code gen time.
       // private Sympl _runtime;
        private ParameterExpression _runtimeParam;
        private ParameterExpression _contextParam;
        // Need IsLambda when support return to find tightest closing fun.
        private LabelTarget _loopBreak = null;
        private LabelTarget _funcReturn = null;
        private LabelTarget _continueBreak = null;
        private Dictionary<string, AST.ParameterExpression> _names;

        public List<AST.Expression> Statements;

        private ILexicalScope lexicalScope;

        public ILexicalScope LexicalScope => lexicalScope ?? Parent.LexicalScope;

        public AnalysisScope(AnalysisScope parent, ScopeType type, string name, CompoundDefinition curdefinition)
            : this(parent, type, name) 
        {
            lexicalScope = curdefinition;
        }

        public AnalysisScope(AnalysisScope parent, CustomAttribute attribute)
            : this(parent, ScopeType.Attribute, attribute.Name)
        {
            lexicalScope = attribute;
        }

        public AnalysisScope(AnalysisScope parent, EnumDefinition enumdef)
            : this(parent, ScopeType.Enum, enumdef.Name)
        {
            lexicalScope = enumdef;
        }

        public AnalysisScope(AnalysisScope parent, FunctionDefinition funcdef)
            : this(parent, ScopeType.Function, funcdef.Name)
        {
            lexicalScope = funcdef;
            
            FuncReturn = AST.Expression.Label(funcdef.ReturnType.ClrType);
        }
        public AnalysisScope(AnalysisScope parent, ScopeType type, string name = "")
        {
            _parent = parent;
            _name = name;

            _names = new Dictionary<string, AST.ParameterExpression>();
            _scopetype = type;

            Statements = new List<AST.Expression>();
            switch (type)
            {
                case ScopeType.Loop:
                    LoopBreak = AST.Expression.Label("loop break");
                    LoopContinue = AST.Expression.Label("loop continue");
                    break;
                case ScopeType.Function:
                    FuncReturn = AST.Expression.Label();
                    break;
                case ScopeType.Switch:
                    LoopBreak = AST.Expression.Label("switch break");
                    break;
            }
        }

        public AnalysisScope Parent { get { return _parent; } }

        public AnalysisScope Root { 
            get { 
                return _parent == null ? this : _parent.Root; 
            } 
        }

        //public Sympl Runtime { get { return _runtime; } }

        public bool IsFunction => _scopetype == ScopeType.Function;

        public bool IsLoop => _scopetype == ScopeType.Loop;
        public bool IsSwitch => _scopetype == ScopeType.Switch;

        public bool CanBreak => IsLoop || IsSwitch;

        public AST.LabelTarget LoopBreak
        {
            get { return _loopBreak; }
            set { _loopBreak = value; }
        }

        public AST.LabelTarget LoopContinue
        {
            get { return _continueBreak; }
            set { _continueBreak = value; }
        }


        public AST.LabelTarget FuncReturn
        {
            get { return _funcReturn; }
            set { _funcReturn = value; }
        }

        public Dictionary<string, AST.ParameterExpression> Names
        {
            get { return _names; }
            set { _names = value; }
        }


    }

    public class Program
    {
        public static void Main(string[] args)
        {
            //foreach (var file in Directory.GetFiles(@"Tests","*.bt"))
            //{
            //    using var fs = File.OpenText(file);
            //    BinaryTemplatePreprocessingLexer pplex = new BinaryTemplatePreprocessingLexer(new AntlrInputStream(fs));
            //    pplex.IncludeDirs.Add(Path.GetDirectoryName(file));
            //    var tokens = pplex.GetAllTokens().ToList();
            //    StringBuilder sb = new StringBuilder();
            //    tokens.ForEach(token => sb.Append(token.Text));
            //    var text = sb.ToString();
            //}

            BinaryTemplate runtime = new BinaryTemplate();
            runtime.RegisterBuiltinFunctions();
            var scope = runtime.RunTemplateFile(@"ZIP.bt",
               @"test.zip");

        }
    }
}
