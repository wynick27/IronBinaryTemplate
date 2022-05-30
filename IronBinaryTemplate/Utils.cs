using System;
using System.Globalization;

namespace IronBinaryTemplate
{

    /// <summary>
    /// Represents a location in source code.
    /// </summary>
    [Serializable]
    public readonly struct SourceLocation : IEquatable<SourceLocation>
    {
        /// <summary>
        /// Creates a new source location.
        /// </summary>
        /// <param name="index">The index in the source stream the location represents (0-based).</param>
        /// <param name="line">The line in the source stream the location represents (1-based).</param>
        /// <param name="column">The column in the source stream the location represents (1-based).</param>
        public SourceLocation(int index, int line, int column)
        {
            ValidateLocation(index, line, column);

            Index = index;
            Line = line;
            Column = column;
        }

        private static void ValidateLocation(int index, int line, int column)
        {
            if (index < 0)
            {
                throw ErrorOutOfRange("index", 0);
            }
            if (line < 1)
            {
                throw ErrorOutOfRange("line", 1);
            }
            if (column < 1)
            {
                throw ErrorOutOfRange("column", 1);
            }
        }

        private static Exception ErrorOutOfRange(object p0, object p1)
        {
            return new ArgumentOutOfRangeException($"{p0} must be greater than or equal to {p1}");
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters")]
        private SourceLocation(int index, int line, int column, bool noChecks)
        {
            Index = index;
            Line = line;
            Column = column;
        }

        /// <summary>
        /// The index in the source stream the location represents (0-based).
        /// </summary>
        public int Index { get; } // TODO: remove index

        /// <summary>
        /// The line in the source stream the location represents (1-based).
        /// </summary>
        public int Line { get; }

        /// <summary>
        /// The column in the source stream the location represents (1-based).
        /// </summary>
        public int Column { get; }

        /// <summary>
        /// Compares two specified location values to see if they are equal.
        /// </summary>
        /// <param name="left">One location to compare.</param>
        /// <param name="right">The other location to compare.</param>
        /// <returns>True if the locations are the same, False otherwise.</returns>
        public static bool operator ==(SourceLocation left, SourceLocation right) => left.Equals(right);

        /// <summary>
        /// Compares two specified location values to see if they are not equal.
        /// </summary>
        /// <param name="left">One location to compare.</param>
        /// <param name="right">The other location to compare.</param>
        /// <returns>True if the locations are not the same, False otherwise.</returns>
        public static bool operator !=(SourceLocation left, SourceLocation right) => !left.Equals(right);

        /// <summary>
        /// Compares two specified location values to see if one is before the other.
        /// </summary>
        /// <param name="left">One location to compare.</param>
        /// <param name="right">The other location to compare.</param>
        /// <returns>True if the first location is before the other location, False otherwise.</returns>
        public static bool operator <(SourceLocation left, SourceLocation right)
        {
            return left.Index < right.Index;
        }

        /// <summary>
        /// Compares two specified location values to see if one is after the other.
        /// </summary>
        /// <param name="left">One location to compare.</param>
        /// <param name="right">The other location to compare.</param>
        /// <returns>True if the first location is after the other location, False otherwise.</returns>
        public static bool operator >(SourceLocation left, SourceLocation right)
        {
            return left.Index > right.Index;
        }

        /// <summary>
        /// Compares two specified location values to see if one is before or the same as the other.
        /// </summary>
        /// <param name="left">One location to compare.</param>
        /// <param name="right">The other location to compare.</param>
        /// <returns>True if the first location is before or the same as the other location, False otherwise.</returns>
        public static bool operator <=(SourceLocation left, SourceLocation right)
        {
            return left.Index <= right.Index;
        }

        /// <summary>
        /// Compares two specified location values to see if one is after or the same as the other.
        /// </summary>
        /// <param name="left">One location to compare.</param>
        /// <param name="right">The other location to compare.</param>
        /// <returns>True if the first location is after or the same as the other location, False otherwise.</returns>
        public static bool operator >=(SourceLocation left, SourceLocation right)
        {
            return left.Index >= right.Index;
        }

        /// <summary>
        /// Compares two specified location values.
        /// </summary>
        /// <param name="left">One location to compare.</param>
        /// <param name="right">The other location to compare.</param>
        /// <returns>0 if the locations are equal, -1 if the left one is less than the right one, 1 otherwise.</returns>
        public static int Compare(SourceLocation left, SourceLocation right)
        {
            if (left < right) return -1;
            if (left > right) return 1;

            return 0;
        }

        /// <summary>
        /// A location that is valid but represents no location at all.
        /// </summary>
        public static readonly SourceLocation None = new SourceLocation(0, 0xfeefee, 0, true);

        /// <summary>
        /// An invalid location.
        /// </summary>
        public static readonly SourceLocation Invalid = new SourceLocation(0, 0, 0, true);

        /// <summary>
        /// A minimal valid location.
        /// </summary>
        public static readonly SourceLocation MinValue = new SourceLocation(0, 1, 1);

        /// <summary>
        /// Whether the location is a valid location.
        /// </summary>
        /// <returns>True if the location is valid, False otherwise.</returns>
        public bool IsValid => Line != 0 && Column != 0;

        public bool Equals(SourceLocation other) =>
            other.Index == Index && other.Line == Line && other.Column == Column;

        public override bool Equals(object obj) =>
            obj is SourceLocation other && Equals(other);

        public override int GetHashCode()
        {
            return (Line << 16) ^ Column;
        }

        public override string ToString()
        {
            return "(" + Line + "," + Column + ")";
        }

        internal string ToDebugString()
        {
            return String.Format(CultureInfo.CurrentCulture, "({0},{1},{2})", Index, Line, Column);
        }
    }

    /// <summary>
    /// Stores the location of a span of text in a source file.
    /// </summary>
    [Serializable]
    public readonly struct SourceSpan : IEquatable<SourceSpan>
    {
        /// <summary>
        /// Constructs a new span with a specific start and end location.
        /// </summary>
        /// <param name="start">The beginning of the span.</param>
        /// <param name="end">The end of the span.</param>
        public SourceSpan(SourceLocation start, SourceLocation end)
        {
            ValidateLocations(start, end);
            Start = start;
            End = end;
        }

        private static void ValidateLocations(in SourceLocation start, in SourceLocation end)
        {
            if (start.IsValid && end.IsValid)
            {
                if (start > end)
                {
                    throw new ArgumentException("Start and End must be well ordered");
                }
            }
            else
            {
                if (start.IsValid || end.IsValid)
                {
                    throw new ArgumentException("Start and End must both be valid or both invalid");
                }
            }
        }

        /// <summary>
        /// The start location of the span.
        /// </summary>
        public SourceLocation Start { get; }

        /// <summary>
        /// The end location of the span. Location of the first character behind the span.
        /// </summary>
        public SourceLocation End { get; }

        /// <summary>
        /// Length of the span (number of characters inside the span).
        /// </summary>
        public int Length => End.Index - Start.Index;

        /// <summary>
        /// A valid span that represents no location.
        /// </summary>
        public static readonly SourceSpan None = new SourceSpan(SourceLocation.None, SourceLocation.None);

        /// <summary>
        /// An invalid span.
        /// </summary>
        public static readonly SourceSpan Invalid = new SourceSpan(SourceLocation.Invalid, SourceLocation.Invalid);

        /// <summary>
        /// Whether the locations in the span are valid.
        /// </summary>
        public bool IsValid => Start.IsValid && End.IsValid;

        /// <summary>
        /// Compares two specified Span values to see if they are equal.
        /// </summary>
        /// <param name="left">One span to compare.</param>
        /// <param name="right">The other span to compare.</param>
        /// <returns>True if the spans are the same, False otherwise.</returns>
        public static bool operator ==(SourceSpan left, SourceSpan right) => left.Equals(right);

        /// <summary>
        /// Compares two specified Span values to see if they are not equal.
        /// </summary>
        /// <param name="left">One span to compare.</param>
        /// <param name="right">The other span to compare.</param>
        /// <returns>True if the spans are not the same, False otherwise.</returns>
        public static bool operator !=(SourceSpan left, SourceSpan right) => !left.Equals(right);

        public bool Equals(SourceSpan other) =>
            Start == other.Start && End == other.End;

        public override bool Equals(object obj) =>
            obj is SourceSpan other && Equals(other);

        public override string ToString()
        {
            return Start.ToString() + " - " + End.ToString();
        }

        public override int GetHashCode()
        {
            // 7 bits for each column (0-128), 9 bits for each row (0-512), xor helps if
            // we have a bigger file.
            return (Start.Column) ^ (End.Column << 7) ^ (Start.Line << 14) ^ (End.Line << 23);
        }

        internal string ToDebugString()
        {
            return String.Format(CultureInfo.CurrentCulture, "{0}-{1}", Start.ToDebugString(), End.ToDebugString());
        }
    }

}
