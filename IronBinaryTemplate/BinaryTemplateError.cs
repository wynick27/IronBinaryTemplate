using System;

namespace IronBinaryTemplate
{
    public class BinaryTemplateError : Exception
    {
        public SourceLocation Location { get; }
        public bool IsWarning { get; }
        public BinaryTemplateError(string message, SourceLocation location = default, bool iswarning = false)
            : base(message)
        {
            Location = location;
            IsWarning = iswarning;
        }
    }

    public class SyntaxError : BinaryTemplateError
    {
        public SyntaxError(string message, SourceLocation location = default) : base(message, location)
        {
        }
        public override string ToString()
        {
            return $"{Location.Line}:{Location.Column} Syntax Error: {Message}";
        }
    }

    public class PreprocessorError : BinaryTemplateError
    {
        public PreprocessorError(string message, SourceLocation location = default, bool iswarning = false) : base(message, location, iswarning)
        {
        }
        public override string ToString()
        {
            return $"{Location.Line}:{Location.Column} Preprocessor Error: {Message}";
        }
    }

    class SemanticError : BinaryTemplateError
    {
        public SemanticError(string message, SourceLocation location = default) : base(message, location)
        {
        }

        public override string ToString()
        {
            return $"{Location.Line}:{Location.Column} Semantic Error: {Message}";
        }
    }

    public class RuntimeError : BinaryTemplateError
    {
        private string message;

        public RuntimeError(string message)
            :base(message, default)
        {
            this.message = message;
        }

        public RuntimeError(string message, SourceLocation location = default) : base(message, location)
        {
        }
    }
}