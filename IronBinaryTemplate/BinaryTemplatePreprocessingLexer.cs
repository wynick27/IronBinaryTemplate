using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IronBinaryTemplate
{
    public class BinaryTemplatePreprocessingLexer : BinaryTemplateLexer
    {
        Dictionary<string, List<IToken>> Macros = new Dictionary<string, List<IToken>>();
        HashSet<string> IncludeSet = new HashSet<string>();
        HashSet<string> MacroSet = new HashSet<string>();
        Stack<StreamInfo> Streams = new Stack<StreamInfo>();
        Stack<bool> Ifs = new Stack<bool>();
        bool isLineStart = true;
        class StreamInfo
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

            public StreamInfo(string includepath, ICharStream inputStream, Lexer lexer)
            {
                IsMacro = false;
                NameOrFile = includepath;
                TokenSource = lexer;
                InputStream = inputStream;
            }
            public IToken NextToken()
            {
                return TokenSource.NextToken();
            }
        }
        public BinaryTemplatePreprocessingLexer(ICharStream input) : base(input)
        {
        }

        StreamInfo CurrentStream => Streams.Peek();

        void PushStream(StreamInfo si)
        {
            if (si.IsMacro)
                MacroSet.Add(si.NameOrFile);
            else
                IncludeSet.Add(si.NameOrFile);
            Streams.Push(si);
        }

        void PopStream()
        {
            if (CurrentStream.IsMacro)
                MacroSet.Remove(CurrentStream.NameOrFile);
            else
                IncludeSet.Remove(CurrentStream.NameOrFile);
            Streams.Pop();
        }

        public override IToken NextToken()
        {
            IToken currentToken;
            while (true)
            {
                currentToken = CurrentStream.NextToken();
                if (currentToken.Type == PreProcessorDirective)
                {
                    currentToken = CurrentStream.NextToken();
                    var tokens = ReadPreprocessorLine();
                    HandleDirectives(currentToken.Text, tokens);
                    isLineStart = true;
                }
                else if (currentToken.Type == Eof)
                {
                    if (Ifs.Count != 0)
                    {
                        Error("ifdef not closed.");
                    }
                    Streams.Pop();
                    if (Streams.Count == 0)
                        return currentToken;
                }
                else if (Ifs.TryPeek(out bool discard) && discard)
                {
                    continue;
                }
                else if (currentToken.Type == Identifier && Macros.TryGetValue(currentToken.Text, out List<IToken> tokens))
                {
                    if (tokens.Count > 0)
                        PushStream(new StreamInfo(currentToken.Text, tokens));
                }
                isLineStart = currentToken.Type == Newline;
            }
            
            return currentToken; 
        }

        private void Error(string v)
        {
            throw new NotImplementedException();
        }

        private List<IToken> ReadPreprocessorLine()
        {
            List<IToken> tokens = new List<IToken>();
            IToken token = base.NextToken();
            while (token.Type != Newline && token.Type != Eof)
            {
                tokens.Add(token);
                token = base.NextToken();
            }
            return tokens;
        }

        private void HandleDirectives(string directive, List<IToken> tokens)
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
                            Ifs.Push(Macros.ContainsKey(nameToken.Text));
                        }
                        break;
                    }
                case "ifndef":
                    {
                        if (nameToken != null && nameToken.Type == Identifier)
                        {
                            Ifs.Push(!Macros.ContainsKey(nameToken.Text));
                        }
                        break;
                    }
                case "else":
                    {
                        if (Ifs.Count !=0)
                        {
                            Ifs.Push(!Ifs.Pop());
                        }
                        break;
                    }
                case "endif":
                    {
                        if (Ifs.Count != 0)
                        {
                            Ifs.Pop();
                        }
                        break;
                    }
                case "link":
                    {
                        if (nameToken != null && nameToken.Type == StringLiteral)
                        {
                            
                        }
                        break;
                    }
                case "endlink":
                    {
                        break;
                    }
                case "include":
                    {
                        if (nameToken != null && nameToken.Type == StringLiteral)
                        {
                        }
                        break;
                    }
            }
        }

    }
}
