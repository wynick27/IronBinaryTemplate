using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace IronBinaryTemplate
{
    public class BinaryTemplatePreprocessingLexer : BinaryTemplateLexer
    {
        Dictionary<string, List<IToken>> Macros = new Dictionary<string, List<IToken>>();
        HashSet<string> IncludeSet = new HashSet<string>();
        HashSet<string> MacroSet = new HashSet<string>();
        Stack<StreamInfo> Streams = new Stack<StreamInfo>();
        Stack<(bool condition, bool currentBlock)> IfStack = new ();
        bool isLineStart = true;

        public List<string> IncludeDirs { get; }

        protected class StreamInfo
        {
            public string NameOrFile;
            public bool IsMacro;
            public ITokenSource TokenSource;
            public ICharStream InputStream;

            public StreamInfo(string name, List<IToken> macroReplacement)
            {
                IsMacro = true;
                NameOrFile = name;
                TokenSource = new ListTokenSource(macroReplacement);
            }

            public StreamInfo(string includepath, ICharStream inputStream)
            {
                IsMacro = false;
                NameOrFile = includepath;
                InputStream = inputStream;
            }
            
        }
        public BinaryTemplatePreprocessingLexer(ICharStream input) : base(input)
        {
            IncludeDirs = new List<string>();
            PushStream(new StreamInfo(input.SourceName, input));
        }

        StreamInfo CurrentStream => Streams.Count==0 ? null : Streams.Peek();

        void PushStream(StreamInfo si)
        {
            if (si.IsMacro)
            {
                if (MacroSet.Contains(si.NameOrFile))
                    Error("Recursive macro expansion not supported.",null);
                else
                    MacroSet.Add(si.NameOrFile);
            }
            else
            {
                if (MacroSet.Contains(si.NameOrFile))
                    Error("Recursive inclusion not allowed.",null);
                IncludeSet.Add(si.NameOrFile);
                SetInputStream(si.InputStream);
            }
            Streams.Push(si);
        }

        private void Error(string msg,IToken token)
        {
            throw new PreprocessorError(msg, token == null ? SourceLocation.None : new SourceLocation(token.StartIndex,token.Line,token.Column));
        }

        private void Warning(string msg,IToken token)
        {
            
            if (ErrorListenerDispatch != null)
            {
                ErrorListenerDispatch.SyntaxError(this, token.StartIndex, token.Line, token.Column, msg, null);
            }
        }

        void PopStream()
        {
            if (CurrentStream.IsMacro)
                MacroSet.Remove(CurrentStream.NameOrFile);
            else
                IncludeSet.Remove(CurrentStream.NameOrFile);
            Streams.Pop();
            if (Streams.TryPeek(out StreamInfo current) && !current.IsMacro)
            {
                SetInputStream(current.InputStream);
            }
        }

        protected IToken NextToken(StreamInfo currentStream)
        {
            if (currentStream == null || !currentStream.IsMacro)
                return base.NextToken();
            else
                return currentStream.TokenSource.NextToken();
                
        }

        public override IToken NextToken()
        {
            IToken currentToken;
            while (true)
            {
                currentToken = NextToken(CurrentStream);
                string directive = null;
                switch (currentToken.Type)
                {
                    case IncludeDirective:
                        directive = "include";
                        goto case PreProcessorDirective;
                    case LinkDirective:
                        directive = "link";
                        goto case PreProcessorDirective;
                    case EndlinkDirective:
                        directive = "endlink";
                        goto case PreProcessorDirective;
                    case PreProcessorDirective:
                        {
                            if (!isLineStart)
                                Error("Preprocessor directives can only appear in line start.",currentToken);
                            isLineStart = true;
                            if (directive == null)
                            {
                                currentToken = NextToken(CurrentStream);
                                directive = currentToken.Text;
                            }
                                
                            var tokens = ReadPreprocessorLine(currentToken);
                            
                            var resultToken = HandleDirectives(directive, tokens);
                            if (resultToken == null)
                                continue;
                            else
                            {
                                currentToken = resultToken;
                                break;
                            }
                        }
                    case Eof:
                        if (IfStack.Count != 0)
                        {
                            Error("if directive not closed.", currentToken);
                        }
                        PopStream();
                        if (Streams.Count == 0)
                            return currentToken;
                        continue;

                    default:
                        if (IfStack.TryPeek(out (bool boolcondition, bool currentBlock) ifstate) && (ifstate.boolcondition ^ ifstate.currentBlock))
                        {
                            continue;
                        }
                        else if (currentToken.Type == Identifier && Macros.TryGetValue(currentToken.Text, out List<IToken> tokens))
                        {
                            if (tokens.Count > 0)
                                PushStream(new StreamInfo(currentToken.Text, tokens));
                            continue;
                        }
                        break;
                }
                isLineStart = currentToken.Type == Newline;
                break;
            }
            
            return currentToken; 
        }

        private List<IToken> ReadPreprocessorLine(IToken currentToken)
        {
            List<IToken> tokens = new List<IToken>();
            if (currentToken.Type == IncludeDirective || currentToken.Type == LinkDirective)
            {
                int index = currentToken.Text.IndexOfAny(new[] { '<', '"' });
                var path = currentToken.Text.Substring(index + 1, currentToken.Text.Length - index - 2);
                var newtoken = TokenFactory.Create(new Tuple<ITokenSource,ICharStream>(currentToken.TokenSource, currentToken.InputStream), currentToken.Type, path, currentToken.Channel, currentToken.StartIndex + index, currentToken.StopIndex, currentToken.Line, currentToken.Column + index);
                tokens.Add(newtoken);
            }
            IToken token = NextToken(CurrentStream);
            while (token.Type != Newline && token.Type != Eof)
            {
                tokens.Add(token);
                token = NextToken(CurrentStream);
            }
            return tokens;
        }

        private ICharStream SearchIncludes(string path)
        {
            if (File.Exists(path))
                new AntlrInputStream(File.OpenRead(path));
            foreach (var includedir in IncludeDirs)
            {
                var fullpath = Path.Combine(includedir, path);
                if (File.Exists(fullpath))
                    return new AntlrInputStream(File.OpenRead(fullpath));
            }
            return null;
        }

        private IToken HandleDirectives(string directive, List<IToken> tokens)
        {
            var nameToken = tokens.FirstOrDefault();
            switch (directive)
            {
                case "define":
                    { 
                    if (nameToken != null && nameToken.Type == Identifier)
                    {
                        tokens.Remove(nameToken);
                        Macros.Add(nameToken.Text,tokens);
                    }
                        
                    break;
                    }
                case "undefine":
                    {
                        if (nameToken != null && nameToken.Type == Identifier)
                        {
                            tokens.Remove(nameToken);
                            Macros.Remove(nameToken.Text);
                        }
                        break;
                    }
                case "ifdef":
                    {
                        if (nameToken != null && nameToken.Type == Identifier)
                        {
                            var tuple = (Macros.ContainsKey(nameToken.Text), true);
                            IfStack.Push(tuple);
                        }
                        break;
                    }
                case "ifndef":
                    {
                        if (nameToken != null && nameToken.Type == Identifier)
                        {
                            var tuple = (!Macros.ContainsKey(nameToken.Text), true);
                            IfStack.Push(tuple);
                        }
                        break;
                    }
                case "else":
                    {
                        if (IfStack.Count != 0)
                        {
                            var tuple = IfStack.Pop();
                            if (!tuple.currentBlock)
                                Error("Multiple else blocks.", nameToken);
                            tuple.currentBlock = false;

                            IfStack.Push(tuple);
                        }
                        else
                            Error("Unmatched else block.", nameToken);
                        break;
                    }
                case "endif":
                    {
                        if (IfStack.Count != 0)
                        {
                            IfStack.Pop();
                        }
                        else
                            Error("Unmatched endif block.", nameToken);
                        break;
                    }
                case "link":
                    {
                        if (nameToken != null && nameToken.Type == LinkDirective)
                        {
                            return nameToken;
                        }
                        else
                            Error("Link directive must start with a path.", nameToken);
                        break;
                    }
                case "endlink":
                    {
                        return nameToken;
                    }
                case "include":
                    {
                        if (nameToken != null && nameToken.Type == IncludeDirective)
                        {
                            
                            var charStream = SearchIncludes(nameToken.Text);
                            if (charStream != null)
                            {
                                PushStream(new StreamInfo(nameToken.Text, charStream));
                            }
                            else
                                Error($"Could not find include file {nameToken.Text}", nameToken);
                        }
                        else
                            Error("Include directive must start with a path.", nameToken);
                        break;
                    }
                    
            }
            return null;
        }

    }
}
