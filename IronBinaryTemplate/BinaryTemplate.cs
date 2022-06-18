using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
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
            BuiltinTypes.Add("time_t", BasicType.FromClrType(typeof(uint)));
            BuiltinTypes.Add("FILETIME", BasicType.FromClrType(typeof(ulong)));
            BuiltinTypes.Add("GUID", BasicType.FromClrType(typeof(byte)).GetArrayType(16));
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
            BuiltinConstants.Add(new ConstVariable("CHECKSUM_CRC32", BasicType.FromString("int"), 0));
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
                try
                {
                    func(context, scope);
                }
                catch (Exception ex)
                {
                    scope.Errors.Add(ex);
                   // throw;
                }
                
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
                script.Errors = errorListener.Errors;
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


}
