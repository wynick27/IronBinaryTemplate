using System;

namespace IronBinaryTemplate
{
    public class BinaryTemplateError : Exception
    {
        public SourceLocation Location { get; }
        public BinaryTemplateError(string message, SourceLocation location = default)
            : base(message)
        {
            Location = Location;
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
        public RuntimeError(string message, SourceLocation location = default) : base(message, location)
        {
        }
    }
}