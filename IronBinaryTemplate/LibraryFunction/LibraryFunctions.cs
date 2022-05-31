using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq.Expressions;
using System.Linq;

namespace IronBinaryTemplate
{

    public class ExitException : Exception
    {
        public int ErrorCode { get; set; }
        public ExitException(int errorcode)
        {
            ErrorCode = errorcode;
        }
    }
    public static class LibraryFunctions
    {
        //double ConvertBytesToDouble(uchar byteArray[] )
        //float ConvertBytesToFloat(uchar byteArray[] )
        //hfloat ConvertBytesToHFloat(uchar byteArray[] )
        //    int ConvertDataToBytes(data_type value, uchar byteArray[] )

        //  int DirectoryExists(string dir)
        //   TFileList FindFiles(string dir, string filter)

        //   int MakeDir(string dir)
        #region "Builtin Functions"
        public static long startof(BinaryTemplateVariable var)
        {
            if (!var.Start.HasValue)
                throw new InvalidOperationException($"Variable {var.Name} have no start address");
            return var.Start.Value;
        }

        public static long @sizeof(BinaryTemplateVariable var)
        {
            if (!var.Size.HasValue)
                throw new InvalidOperationException($"Variable {var.Name} have no size");
            return var.Size.Value;
        }

        public static BinaryTemplateScope parentof(BinaryTemplateVariable var)
        {
            if (var.Parent == null)
                throw new InvalidOperationException($"Cannot compute parent of {var.Name}");
            return var.Parent;
        }

        public static bool exists(BinaryTemplateScope scope, params object[] path)
        {
            object result = scope;
            foreach (var pathpart in path)
            {
                if (result == null)
                    return false;
                if (pathpart is string)
                {
                    var indexer = RuntimeHelpers.GetStringIndexer(result.GetType());
                    if (indexer == null)
                        return false;
                    result = indexer.GetValue(result, new[] { pathpart });
                }
                else if (pathpart is int)
                {
                    var indexer = RuntimeHelpers.GetIntIndexer(result.GetType());
                    if (indexer == null)
                        return false;
                    result = indexer.GetValue(result, new[] { pathpart });
                }
                else
                    return false;
            }
            return true;
        }

        public static bool function_exists(BinaryTemplateScope scope, string name)
        {
            return scope.GetFunction(name) == null;
        }
        #endregion

        #region "Interface Functions"
        [TemplateCallable]
        static void Assert(bool test, string message)
        {
            if (!test)
                Console.WriteLine(message);
            throw new ExitException(0);
        }

        [TemplateCallable]
        static void Exit(int errorcode)
        {
            throw new ExitException(errorcode);
        }

        [TemplateCallable]
        public static int Printf(string format, params object[] arguments)
        {
            
            Console.WriteLine(Str(format,arguments));
            return 0;
        }
        [TemplateCallable]
        public static void Warning(string format, params object[] arguments)
        {
            Console.WriteLine(Str(format, arguments));
        }
        public static void SetBackColor(int color)
        {

        }
        public static void SetColor(int forecolor, int backcolor)
        {

        }
        public static void SetForeColor(int color)
        {

        }

        #endregion

        #region "Math Functions"
        static Random rand = new Random();

        public static int Random(int max)
        {
            return rand.Next(max);
        }

        #endregion

        #region "String Functions"
        [TemplateCallable]
        public static double Atof(string s)
        {
            if (double.TryParse(s, out double value))
                return value;
            return 0.0;
        }
        [TemplateCallable]
        public static int Atoi(string s)
        {
            if (int.TryParse(s, out int value))
                return value;
            return 0;
        }
        [TemplateCallable]
        public static long BinaryStrToInt(string s)
        {
            try
            {
                return Convert.ToInt64(s, 2);
            }
            catch (Exception ex)
            {

                return 0;
            }

        }

        //char[] ConvertString( const char src[], int srcCharSet, int destCharSet )
        //Given a string src that uses the character set encoding srcCharSet, the string is converted to use the character set encoding destCharSet and returned as a string. The following character set constants exist: 
        //CHARSET_ASCII
        //CHARSET_ANSI
        //CHARSET_OEM
        //CHARSET_EBCDIC
        //CHARSET_UNICODE
        //CHARSET_MAC
        //CHARSET_ARABIC
        //CHARSET_BALTIC
        //CHARSET_CHINESE_S
        //CHARSET_CHINESE_T
        //CHARSET_CYRILLIC
        //CHARSET_EASTEUROPE
        //CHARSET_GREEK
        //CHARSET_HEBREW
        //CHARSET_JAPANESE
        //CHARSET_KOREAN_J
        //CHARSET_KOREAN_W
        //CHARSET_THAI
        //CHARSET_TURKISH
        //CHARSET_VIETNAMESE
        //CHARSET_UTF8
        //CHARSET_ARABIC_ISO
        //CHARSET_BALTIC_ISO
        //CHARSET_CYRILLIC_KOI8R
        //CHARSET_CYRILLIC_KOI8U
        //CHARSET_CYRILLIC_ISO
        //CHARSET_EASTEUROPE_ISO
        //CHARSET_GREEK_ISO
        //CHARSET_HEBREW_ISO
        //CHARSET_JAPANESE_EUCJP
        //CHARSET_TURKISH_ISO
        //Custom character sets can also be specified using the ID Number specified in the Character Set Options dialog.This function should not be used with Unicode character sets(CHARSET_UNICODE). To perform conversions with Unicode strings see the StringToWString and WStringToString functions.
        //Requires 010 Editor v4.0 or higher. 
        //Requires 010 Editor v9.0 or higher for CHARSET_ARABIC_ISO or greater.


        //string DosDateToString(DOSDATE d, char format[] = "MM/dd/yyyy")
        //Converts the given DOSDATE into a string and returns the results.By default the date will be in the format 'MM/dd/yyyy' but other formats can be used as described in the GetCurrentDateTime function.Click here for more information on the DOSDATE type and see the FileTimeToString function for an example of using SScanf to parse the resulting string. 
        //Requires 010 Editor v4.0 or higher for the format parameter.

        //￼

        //string DosTimeToString(DOSTIME t, char format[] = "hh:mm:ss")
        //Converts the given DOSTIME into a string and returns the results.By default the time will be in the format 'hh:mm:ss' but other formats can be used as described in the GetCurrentDateTime function.Click here for more information on the DOSTIME type and see the FileTimeToString function for an example of using SScanf to parse the resulting string. 
        //Requires 010 Editor v4.0 or higher for the format parameter.

        //￼
        [TemplateCallable]
        public static BinaryTemplateString EnumToString(BinaryTemplateVariable e)
        {
            if (e.Type.TypeKind != TypeKind.Enum)
                throw new ArgumentException("Not a valid enum.");
            EnumDefinition enumdef = e.Type as EnumDefinition;

            return new BinaryTemplateString(enumdef.FindName(e.Value));
        }
        [TemplateCallable]
        public static BinaryTemplateString FileNameGetBase(string path, bool includeExtension = true)
        {
            return new BinaryTemplateString(FileNameGetBaseW(path, includeExtension));
        }
        [TemplateCallable]
        public static string FileNameGetBaseW(string path, bool includeExtension = true)
        {
            if (includeExtension)
                return Path.GetFileName(path);
            else
                return Path.GetFileNameWithoutExtension(path);
        }
        [TemplateCallable]
        public static BinaryTemplateString FileNameGetExtension(string path)
        {
            return new BinaryTemplateString(FileNameGetExtensionW(path));
        }
        [TemplateCallable]
        public static string FileNameGetExtensionW(string path)
        {
            return Path.GetExtension(path);
        }
        [TemplateCallable]
        public static BinaryTemplateString FileNameGetPath(string path, bool includeExtension = true)
        {
            return new BinaryTemplateString(FileNameGetPathW(path, includeExtension));
        }
        [TemplateCallable]
        public static string FileNameGetPathW(string path, bool includeExtension = true)
        {
            string result = Path.GetDirectoryName(path);
            if (includeExtension)
                return result + '\\';
            return result;
        }
        [TemplateCallable]
        public static BinaryTemplateString FileNameSetExtension(string path, string extension)
        {
            return new BinaryTemplateString(FileNameSetExtensionW(path, extension));
        }
        [TemplateCallable]
        public static string FileNameSetExtensionW(string path, string extension)
        {
            return Path.ChangeExtension(path, extension);
        }


        //string FileTimeToString(FILETIME ft, char format[] = "MM/dd/yyyy hh:mm:ss")
        //Converts the given FILETIME into a string and returns the results.By default the time will be in the format 'MM/dd/yyyy hh:mm:ss' but other formats can be used as described in the GetCurrentDateTime function.Click here for more information on the FILETIME type.The resulting string can be separated into parts using the SScanf function.For example: 

        // int hour, minute, second, day, month, year;
        //    string s = FileTimeToString(ft);
        //    SScanf(s, "%02d/%02d/%04d %02d:%02d:%02d",
        //        month, day, year, hour, minute, second );
        //    year++;
        //    SPrintf(s, "%02d/%02d/%04d %02d:%02d:%02d",
        //        month, day, year, hour, minute, second );
        //    Requires 010 Editor v4.0 or higher for the format parameter.

        //￼

        //string GUIDToString(GUID g)
        //Given a GUID g, the GUID is converted into a string in the format "{XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX}" and returned.Note that a GUID is the same as an array of 16 unsigned bytes (see Data Types, Typedefs, and Enums). See StringToGUID to convert from a string to a GUID.
        //Requires 010 Editor v11.0 or higher. 

        //￼

        [TemplateCallable]
        public static BinaryTemplateString IntToBinaryStr(long num, int numGroups = 0, bool includeSpaces = true)
        {
            throw new NotImplementedException();
            Convert.ToString(num, 2);
        }


        //int IsCharAlpha(char c )
        [TemplateCallable]
        public static bool IsCharAlphaW(char c)
        {
            return char.IsLetter(c);
        }

        //int IsCharNum(char c )
        [TemplateCallable]
        public static bool IsCharNumW(char c)
        {
            return char.IsNumber(c);
        }


        //int IsCharAlphaNum(char c )
        [TemplateCallable]
        public static bool IsCharAlphaNumW(char c)
        {
            return char.IsLetterOrDigit(c);
        }


        //int IsCharSymbol(char c )
        [TemplateCallable]
        public static bool IsCharSymbolW(char c)
        {
            return char.IsSymbol(c);
        }

        //int IsCharWhitespace(char c )
        [TemplateCallable]
        public static bool IsCharWhitespaceW(char c )
        {
            return char.IsWhiteSpace(c);
        }
        [TemplateCallable]
        public static int Memcmp(byte[] s1, byte[] s2, int n)
        {
            throw new NotImplementedException();
        }
        //Compares the first n bytes of s1 and s2.Returns a value less than zero if s1 is less than s2, zero if they are equal, or a value greater than zero if s1 is greater than s2.
        [TemplateCallable]
        public static void Memcpy(byte[] dest, byte[] src, int n, int destOffset = 0, int srcOffset = 0)
        {
            throw new NotImplementedException();
        }
        //Copies a block of n bytes from src to dest.If srcOffset is not zero, the bytes are copied starting from the srcOffset byte in src.If destOffset is not zero, the bytes are copied to dest starting at the byte destOffset. See the WMemcpy function for copying wchar_t data.
        //Requires 010 Editor v6.0 or higher for the destOffset and srcOffset parameters.
        [TemplateCallable]
        public static void Memset(byte[] s, int c, int n)
        {
            throw new NotImplementedException();
        }
        //Sets the first n bytes of s to the byte c.



        //string OleTimeToString(OLETIME ot, char format[] = "MM/dd/yyyy hh:mm:ss")
        //Converts the given OLETIME into a string and returns the results.By default the time will be in the format 'MM/dd/yyyy hh:mm:ss' but other formats can be used as described in the GetCurrentDateTime function.Click here for more information on the OLETIME type and see the FileTimeToString function for an example of using SScanf to parse the resulting string. 
        //Requires 010 Editor v4.0 or higher for the format parameter.

        //￼

        //int RegExMatch(string str, string regex);
        [TemplateCallable]
        public static int RegExMatchW(string str, string regex)
        {
            try
            {
                return Regex.IsMatch(str, regex) ? 1 : 0;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        //int RegExSearch( string str, string regex, int &matchSize, int startPos=0 );
        [TemplateCallable]
        public static int RegExSearchW(string str, string regex, ref int matchSize, int startPos = 0)
        {
            try
            {
                var match = Regex.Match(str, regex);
                matchSize = match.Length;
                return match.Index;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        //int SPrintf( char buffer[], const char format[] [, argument, ... ] )
        //Performs a Printf starting from format and places the result into buffer. See Printf for more information. This function is similar to the Str function. 

        //￼

        //int SScanf( char str[], char format[], ... )
        [TemplateCallable]
        public static int SScanf(string str, string format, params BinaryTemplateVariable[] arguments)
        {
            throw new NotImplementedException();
        }
        //This function parses the str parameter into a number of variables according to the format string. The format string uses the same specifiers as the Printf function. Following the format must be a list of arguments, one for each format specifier in the format string. Note that unlike the regular C function, do not use '&' for each argument. For example: 
        //    int a, b;
        //SScanf("34, 62", "%d, %d", a, b);
        //would read the value 34 and 62 into a and b. The return value will be the number of successfully read arguments (in this example the return value would be 2). 

        [TemplateCallable]
        public static string Str(string format, params object[] arguments)
        {
            int index = 0;
            Func<object?> NextArg = () =>
            {
                if (index >= arguments.Length)
                    return null;
                else
                    return arguments[index++];
            };
            var result = Regex.Replace(format, @"%(?<flags>[\#\-\+ 0])?(?<width>\d+|\*)?(?:\.(?<precision>\d+|\*))?([hlL])?(?<format>[dioxXucsfFeEgG%])", match =>
            {

                string formatStr = match.Groups["format"].Value;
                bool unsigned = false;
                switch (formatStr)
                {
                    case "%":
                        return "%";
                    case "i":
                        formatStr = "d"; break;
                    case "u":
                        formatStr = "d";
                        unsigned = true;
                        break;

                }

                string flags = match.Groups["flags"].Success ? match.Groups["flags"].Value : "";

                if (match.Groups["precision"].Success)
                {
                    string precision = match.Groups["precision"].Value;
                    if (precision == "*")
                        precision = RuntimeHelpers.ChangeType<int>(NextArg()).ToString();
                    formatStr += precision;
                }
                formatStr = ":" + formatStr;
                if (match.Groups["width"].Success)
                {
                    string width = match.Groups["width"].Value;
                    if (width == "*")
                        width = RuntimeHelpers.ChangeType<int>(NextArg()).ToString();
                    formatStr = $",{width}{formatStr}";
                    if (flags == "-")
                        formatStr = "-" + formatStr;
                }

                string result = string.Format("{0" + formatStr + "}", NextArg());


                return result;
            });
            return result;
        }

        //Requires 010 Editor v12.0 or higher. 

        //￼

       // void Strcat(byte[] dest,  byte[] src)
       // {

       // }
        //Appends the characters from src to the end of the string dest. The string may be resized if necessary. The += operator can also be used for a similar result. 

        //￼

        public static int Strchr(byte[] s, char c )
        {
            for (int i = 0;i < s.Length;i++)
            {
                if (s[i] == c)
                    return i;
                if (s[i] == '\0')
                    return -1;
            }
            return -1;
        }
        //Scans the string s for the first occurrence of the character c. Returns the index of the match, or -1 if no characters match. 

        //￼
        [TemplateCallable]
        public static int Strcmp(byte[] s1, byte[] s2 )
        {
            return 0;
        }
        //Compares the one string to another. Returns a value less than zero if s1 is less than s2, zero if they are equal, or a value greater than zero if s1 is greater than s2. 

        //￼

        //void Strcpy( char dest[], const char src[] )
        //Copies string src to string dest, stopping when the null-character has been copied. 

        //￼

        //char[] StrDel( const char str[], int start, int count)
        //Removes count characters from str starting at the index start and returns the resulting string. 

        //￼

        //int Stricmp( const char s1[], const char s2[] )
        //Identical to Strcmp except the strings are compared without case sensitivity. 

        //￼

        //int StringToDosDate( string s, DOSDATE &d, char format[] = "MM/dd/yyyy" )
        //Converts the given string into a DOSDATE and stores the results in d. The format of the date string is given with the format parameter and is by default 'MM/dd/yyyy' but other formats can be used as described in the GetCurrentDateTime function. This function returns 0 if it succeeds or a negative number on failure. More information on date types is available here. 
        //Requires 010 Editor v4.0 or higher for the format parameter. 

        //￼

        //int StringToDosTime( string s, DOSTIME &t, char format[] = "hh:mm:ss" )
        //Converts the given string into a DOSTIME and stores the results in t. The format of the time string is given with the format parameter and is by default 'hh:mm:ss' but other formats can be used as described in the GetCurrentDateTime function. This function returns 0 if it succeeds or a negative number on failure. More information on date types is available here. 
        //Requires 010 Editor v4.0 or higher for the format parameter. 

        //￼

        //int StringToFileTime( string s, FILETIME &ft, char format[] = "MM/dd/yyyy hh:mm:ss" )
        //Converts the given string into a FILETIME and stores the results in ft. The format of the time string is given with the format parameter and is by default 'MM/dd/yyyy hh:mm:ss' but other formats can be used as described in the GetCurrentDateTime function. This function returns 0 if it succeeds or a negative number on failure. More information on date types is available here. 
        //Requires 010 Editor v4.0 or higher for the format parameter. 

        //￼

        //int StringToGUID( string str, GUID g )
        //Given a string str in the format "{XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX}" where each X is 0..9 or A..F, the string is converted into a GUID and stored in the g parameter. Note that a GUID is the same as an array of 16 unsigned bytes (see Data Types, Typedefs, and Enums). A negative number is returned if the string could not be converted or zero is returned on success. See GUIDToString to convert from a GUID to a string. 
        //Requires 010 Editor v11.0 or higher. 

        //￼

        //int StringToOleTime( string s, OLETIME &ot, char format[] = "MM/dd/yyyy hh:mm:ss" )
        //Converts the given string into an OLETIME and stores the results in ot. The format of the string is given with the format parameter and is by default 'MM/dd/yyyy hh:mm:ss' but other formats can be used as described in the GetCurrentDateTime function. This function returns 0 if it succeeds or a negative number on failure. More information on date types is available here. 
        //Requires 010 Editor v4.0 or higher for the format parameter. 

        //￼

        //int StringToTimeT( string s, time_t &t, char format[] = "MM/dd/yyyy hh:mm:ss" )
        //Converts the given string into a time_t and stores the results in t. The format of the string is given with the format parameter and is by default 'MM/dd/yyyy hh:mm:ss' but other formats can be used as described in the GetCurrentDateTime function. This function returns 0 if it succeeds or a negative number on failure. More information on date types is available here. 
        //Requires 010 Editor v4.0 or higher for the format parameter. 

        //￼

        //int StringToTime64T( string s, time64_t &t, char format[] = "MM/dd/yyyy hh:mm:ss" )
        //Converts the given string into a time64_t and stores the results in t. The format of the string is given with the format parameter and is by default 'MM/dd/yyyy hh:mm:ss' but other formats can be used as described in the GetCurrentDateTime function. This function returns 0 if it succeeds or a negative number on failure. More information on date types is available here. 
        //Requires 010 Editor v9.0 or higher. 

        //￼

        //char[] StringToUTF8( const char src[], int srcCharSet = CHARSET_ANSI)
        //Takes as input a string src which uses the character set encoding srcCharSet. The string is converted to the UTF-8 character set and returned. The list of character set constants is available in the ConvertString function and this function is equivalent to 'ConvertString( src, srcCharSet, CHARSET_UTF8 );'. 
        //Requires 010 Editor v4.0 or higher. 

        //￼

        //wstring StringToWString( const char str[], int srcCharSet = CHARSET_ANSI )
        //Converts the given string str into a wide (unicode) string.str is assumed to be an ANSI string but other character sets can be specified using the srcCharSet parameter(see the ConvertString function for a list of character set constants).See Strings for information on wide strings and note that wstring and wchar_t[] are equivalent.
        //Requires 010 Editor v3.1 or higher.
        //Requires 010 Editor v4.0 or higher for the srcCharSet parameter.

        //￼

        [TemplateCallable]
        public static int Strlen(byte[] s)
        {
            for (int i=0;i<s.Length;i++)
            {
                if (s[i] == 0)
                    return i;
            }
            return s.Length;
        }


        //int Strncmp( const char s1[], const char s2[], int n )
        //Similar to Strcmp, except that no more than n characters are compared. 

        //￼

        //void Strncpy( char dest[], const char src[], int n )
        //Similar to Strcpy, except that at most n characters will be copied. 

        //￼

        //int Strnicmp( const char s1[], const char s2[], int n )
        //Similar to Strcmp except that at most n characters are compared and the characters are compared without case sensitivity. 

        //￼

        [TemplateCallable]
        public static int Strstr(byte[] s1, byte[] s2)
        {
            return 0;
        }
        //Scans the string s1 for the first occurrence of s2. Returns the index of the first matching character, or -1 if no match is found. 

        //￼

        //char[] SubStr( const char str[], int start, int count = -1)
        //Returns a string containing count characters from str starting at the index start. If count is -1, all the characters from the start index to the end of the string are returned.

        //￼

        //string TimeTToString( time_t t, char format[] = "MM/dd/yyyy hh:mm:ss" )
        //Converts the given time_t into a string and returns the results. By default the time will be in the format 'MM/dd/yyyy hh:mm:ss' but other formats can be used as described in the GetCurrentDateTime function. Click here for more information on the time_t type and see the FileTimeToString function for an example of using SScanf to parse the resulting string. 
        //Requires 010 Editor v4.0 or higher for the format parameter. 

        //￼

        //string Time64TToString( time64_t t, char format[] = "MM/dd/yyyy hh:mm:ss" )
        //Converts the given time64_t into a string and returns the results. By default the time will be in the format 'MM/dd/yyyy hh:mm:ss' but other formats can be used as described in the GetCurrentDateTime function. Click here for more information on the time64_t type and see the FileTimeToString function for an example of using SScanf to parse the resulting string. 
        //Requires 010 Editor v9.0 or higher. 

        //￼

        //char ToLower( char c )
        [TemplateCallable]
        public static char ToLowerW(char c)
        {
            return char.ToLower(c);
        }

        //char ToUpper( char c )
        [TemplateCallable]
        public static char ToUpper(char c)
        {
            return char.ToUpper(c);
        }


        //void WMemcmp( const wchar_t s1[], const wchar_t s2[], int n )
        //Compares the first n wchar_t items of the arrays s1 and s2. This function returns a value less than zero if s1 is less than s2, zero if they are equal, or a value greater than zero if s1 is greater than s2. 
        //Requires 010 Editor v3.1 or higher. 

        //￼

        //void WMemcpy( wchar_t dest[], const wchar_t src[], int n, int destOffset=0, int srcOffset=0 )
        //Copies n wchar_t items from the array src to the array dest. If srcOffset is not zero, the bytes are copied starting from the srcOffset index in src. If destOffset is not zero, the bytes are copied to dest starting at the index destOffset. See the Memcpy function for copying byte data. 
        //Requires 010 Editor v3.1 or higher. 
        //Requires 010 Editor v6.0 or higher for the destOffset and srcOffset parameters. 

        //￼

        //void WMemset( wchar_t s[], int c, int n )
        //Sets the first n wchar_t items of the array s to the value c. 
        //Requires 010 Editor v3.1 or higher. 

        //￼

        //void WStrcat( wchar_t dest[], const wchar_t src[] )
        //Appends all characters from the src string to the end of the dest string. Note that the string may be resized if required and the += operator can also be used for a similar result. 
        //Requires 010 Editor v3.1 or higher. 

        //￼

        //int WStrchr( const wchar_t s[], wchar_t c )
        //Searchs through the string s for the first occurrence of the character c. If the character is found, this function returns the index of the match, otherwise -1 is returned. 
        //Requires 010 Editor v3.1 or higher. 

        //￼

        //int WStrcmp(string s1, string s2)
        //Use this function to compare one wide string to another.Returns a value less than zero if s1 is less than s2, zero if they are equal, or a value greater than zero if s1 is greater than s2.
        //Requires 010 Editor v3.1 or higher. 

        //￼

        //void WStrcpy(wchar_t dest[], const wchar_t src[] )
        //Copies the string src to the string dest, stopping when the null-character has been copied. 
        //Requires 010 Editor v3.1 or higher. 


        //wchar_t []
        //WStrDel(string str, int start, int count )
        //Returns a string where count characters have been removed from the string str starting at the index start. Note that the str argument is not modified. 
        //Requires 010 Editor v3.1 or higher. 


        //int WStricmp(string s1, string s2)
        //Identical to WStrcmp except the strings are compared without case sensitivity. 
        //Requires 010 Editor v3.1 or higher. 

        //char[]
        //WStringToString(string str, int destCharSet=CHARSET_ANSI )
        //Converts the given wide string str by default into an ANSI string and returns it. The string can be converted to other character sets using the destCharSet parameter(see the ConvertString function for a list of character set constants).Note that not all characters can be successfully converted from wide characters to other character sets and any characters that cannot be converted will be replaced with the '?' character. See Strings for information on wide strings and note that wstring and wchar_t[]
        //are equivalent.
        //Requires 010 Editor v3.1 or higher. 
        //Requires 010 Editor v4.0 or higher for the destCharSet parameter.


        //char[] WStringToUTF8(string str)
        //Takes as input a Unicode string str which is then converted to the UTF-8 character set and returned as a string. 
        //Requires 010 Editor v4.0 or higher. 

        //int WStrlen(string s1)
        //Counts the number of characters in s before the null-character is encountered and returns the result. 
        //Requires 010 Editor v3.1 or higher. 

        //￼

        //int WStrncmp(string s1, string s2, int n )
        //Similar to WStrcmp, except that at most n characters are compared between the two strings. 
        //Requires 010 Editor v3.1 or higher. 

        //￼

        //void WStrncpy(string s1, string s2, int n )
        //Similar to WStrcpy, except that at most n characters will be copied. 
        //Requires 010 Editor v3.1 or higher. 

        //￼

        //int WStrnicmp(string s1, string s2, int n )
        //Similar to WStrcmp except that at most n characters are compared and the characters are compared without case sensitivity. 
        //Requires 010 Editor v3.1 or higher. 

        //￼

        //int WStrstr(string s1, string s2)
        //Searches through the wide string s1 for the first occurrence of the string s2. If the string is found, the index of the first matching character is returned, otherwise -1 is returned. 
        //Requires 010 Editor v3.1 or higher. 

        //￼

        //wchar_t[] WSubStr( const wchar_t str[], int start, int count = -1)
        //Returns a wide string containing count characters from str starting at the index start. If count is -1, all the characters from the start index to the end of the string are returned. 
        //Requires 010 Editor v3.1 or higher. 

        #endregion

        #region "Tool Functions"

        [TemplateCallable]
        public static long Checksum(int algorithm, long start = 0, long size = 0, long crcPolynomial = -1, long crcInitValue = -1)
        {
            throw new NotImplementedException();
        }
        //Runs a simple checksum on a file and returns the result as a int64.The algorithm can be one of the following constants: 
        //CHECKSUM_BYTE - Treats the file as a set of unsigned bytes
        //CHECKSUM_SHORT_LE - Treats the file as a set of unsigned little-endian shorts
        //CHECKSUM_SHORT_BE - Treats the file as a set of unsigned big-endian shorts
        //CHECKSUM_INT_LE - Treats the file as a set of unsigned little-endian ints
        //CHECKSUM_INT_BE - Treats the file as a set of unsigned big-endian ints
        //CHECKSUM_INT64_LE - Treats the file as a set of unsigned little-endian int64s
        //CHECKSUM_INT64_BE - Treats the file as a set of unsigned big-endian int64s
        //CHECKSUM_SUM8 - Same as CHECKSUM_BYTE except result output as 8-bits
        //CHECKSUM_SUM16 - Same as CHECKSUM_BYTE except result output as 16-bits
        //CHECKSUM_SUM32 - Same as CHECKSUM_BYTE except result output as 32-bits
        //CHECKSUM_SUM64 - Same as CHECKSUM_BYTE
        //CHECKSUM_CRC16
        //CHECKSUM_CRCCCITT
        //CHECKSUM_CRC32 = 0x200,
        //CHECKSUM_ADLER32 = 0x400,
        //If start and size are zero, the algorithm is run on the whole file.If they are not zero then the algorithm is run on size bytes starting at address start. See the ChecksumAlgBytes and ChecksumAlgStr functions to run more complex algorithms.crcPolynomial and crcInitValue can be used to set a custom polynomial and initial value for the CRC functions.A value of -1 for these parameters uses the default values as described in the Check Sum/Hash Algorithms topic.A negative number is returned on error.

        //￼

        //int ChecksumAlgArrayStr(
        //    int algorithm,
        //    char result[],
        //    uchar* buffer,
        //    int64 size,
        //    char ignore[]= "",
        //    int64 crcPolynomial = -1,
        //    int64 crcInitValue = -1)
        //Similar to the ChecksumAlgStr function except that the checksum is run on data stored in an array instead of in a file.The data for the checksum should be passed in the buffer array and the size parameter lists the number of bytes in the array. The result from the checksum will be stored in the result string and the number of characters in the string will be returned, or -1 if an error occurred.See the ChecksumAlgStr function for a list of available algorithms.
        //Requires 010 Editor v4.0 or higher. 

        //￼

        //int ChecksumAlgArrayBytes(
        //    int algorithm,
        //    uchar result[],
        //    uchar* buffer,
        //    int64 size,
        //    char ignore[]= "",
        //    int64 crcPolynomial = -1,
        //    int64 crcInitValue = -1)
        //Similar to the ChecksumAlgStr function except that the checksum is run on data in an array instead of in a file and the results are stored in an array of bytes instead of a string. The data for the checksum should be passed in the buffer array and the size parameter lists the number of bytes in the array.The result of the checksum operation will be stored as a set of hex bytes in the parameter result.The function will return the number of bytes placed in the result array or -1 if an error occurred.See the ChecksumAlgStr function for a list of available algorithms.
        //Requires 010 Editor v4.0 or higher. 

        //￼

        //int ChecksumAlgStr(
        //    int algorithm,
        //    char result[],
        //    int64 start = 0,
        //    int64 size = 0,
        //    char ignore[]= "",
        //    int64 crcPolynomial = -1,
        //    int64 crcInitValue = -1)
        //Similar to the Checksum algorithm except the following algorithm constants are supported: 
        //CHECKSUM_BYTE
        //CHECKSUM_SHORT_LE
        //CHECKSUM_SHORT_BE
        //CHECKSUM_INT_LE
        //CHECKSUM_INT_BE
        //CHECKSUM_INT64_LE
        //CHECKSUM_INT64_BE
        //CHECKSUM_SUM8
        //CHECKSUM_SUM16
        //CHECKSUM_SUM32
        //CHECKSUM_SUM64
        //CHECKSUM_CRC16
        //CHECKSUM_CRCCCITT
        //CHECKSUM_CRC32
        //CHECKSUM_ADLER32
        //CHECKSUM_MD2
        //CHECKSUM_MD4
        //CHECKSUM_MD5
        //CHECKSUM_RIPEMD160
        //CHECKSUM_SHA1
        //CHECKSUM_SHA256
        //CHECKSUM_SHA384
        //CHECKSUM_SHA512
        //CHECKSUM_TIGER
        //The result argument specifies a string which will hold the result of the checksum.The return value indicates the number of characters in the string, or is negative if an error occurred.Any ranges to ignore can be specified in string format with the ignore argument(see Check Sum/Hash Algorithms). The crcPolynomial and crcInitValue parameters are used to set a custom polynomial and initial value for the CRC algorithms.Specifying -1 for these parameters uses the default values as indicated in the Check Sum/Hash Algorithms help topic.See the Checksum function above for an explanation of the different checksum constants.
        //Requires 010 Editor v12.0 or higher for CHECKSUM_SHA384.

        //￼


        //int ChecksumAlgBytes(
        //int algorithm,
        //uchar result[],
        //int64 start = 0,
        //int64 size = 0,
        //char ignore[]= "",
        //int64 crcPolynomial = -1,
        //int64 crcInitValue = -1)
        //This function is identical to the ChecksumAlgStr function except that the checksum is returned as a byte array in the result argument.The return value is the number of bytes returned in the array.

        //￼

        //TCompareResults Compare(
        //   int type,
        //   int fileNumA,
        //   int fileNumB,
        //   int64 startA= 0,
        //   int64 sizeA = 0,
        //   int64 startB = 0,
        //   int64 sizeB = 0,
        //   int matchcase = true,
        //   int64 maxlookahead = 10000,
        //   int64 minmatchlength = 8,
        //   int64 quickmatch = 512)
        //Runs a comparison between two files or between two blocks of data.The type argument indicates the type of comparison that should be run and can be either: 
        //COMPARE_SYNCHRONIZE (a binary comparison)
        //COMPARE_SIMPLE(a byte-by-byte comparison)
        //fileNumA and fileNumB indicate the numbers of the file to compare(see GetFileNum). The file numbers may be the same to compare two blocks in the same file.The startA, sizeA, startB, and sizeB arguments indicate the size of the blocks to compare in the two files.If the start and size are both zero, the whole file is used.If matchcase is false, then letters of mixed upper and lower cases will match.See Comparing Files for details on the maxlookahead, minmatchlength and quickmatch arguments.The return value is TCompareResults structure with contains a count variable indicating the number of resulting ranges, and an array of record. Each record contains the variables type, startA, sizeA, startB, and sizeB to indicate the range.The type variable will be one of: 
        //COMPARE_MATCH=0 
        //COMPARE_DIFFERENCE=1 
        //COMPARE_ONLY_IN_A=2 
        //COMPARE_ONLY_IN_B=3 
        //For example: 
        //    int i, f1, f2;
        //        FileOpen( "C:\\temp\\test1" );
        //        f1 = GetFileNum();
        //        FileOpen( "C:\\temp\\test2" );
        //        f2 = GetFileNum();
        //        TCompareResults r = Compare(COMPARE_SYNCHRONIZE, f1, f2);
        //    for(i = 0; i<r.count; i++ )
        //    {
        //         Printf( "%d %Ld %Ld %Ld %Ld\n",
        //             r.record[i].type,
        //             r.record[i].startA,
        //             r.record[i].sizeA,
        //             r.record[i].startB,
        //             r.record[i].sizeB );
        //    } 

        //￼

        //char ConvertASCIIToEBCDIC(char ascii)
        //Converts the given ASCII character into an EBCDIC character and returns the result.

        //￼

        //void ConvertASCIIToUNICODE(
        //    int len,
        //    const char ascii[],
        //    ubyte unicode[],
        //    int bigendian = false)
        //Converts an ASCII string into an array of bytes and stores them in the unicode argument.len indicates the number of characters to convert and the unicode array must be of size at least 2* len.If bigendian is true, the bytes are stored in big-endian mode, otherwise the bytes are stored in little-endian mode. 

        //￼


        //void ConvertASCIIToUNICODEW(
        //   int len,
        //    const char ascii[],
        //   ushort unicode[] )
        //Converts an ASCII string into an array of words and stores the array in the unicode argument.The number of characters to convert is given by the len argument and the unicode argument must have size at least len.

        //￼

        //char ConvertEBCDICToASCII(char ebcdic)
        //Converts the given EBCDIC character into an ASCII character and returns the result.

        //￼

        //void ConvertUNICODEToASCII(
        //    int len,
        //    const ubyte unicode[],
        //    char ascii[],
        //    int bigendian = false)
        //Converts an array of UNICODE characters in the unicode argument into ASCII bytes and stores them in the ascii array.len indicates the number of characters to convert.unicode must be of size at least size 2*len and ascii must be of size at least len. If bigendian is true, the bytes are stored in big-endian mode, otherwise the bytes are stored in little-endian mode. 

        //￼


        //void ConvertUNICODEToASCIIW(
        //   int len,
        //    const ushort unicode[],
        //   char ascii[] )
        //Converts the array of words in the unicode argument to ASCII bytes and saves them to the ascii argument.The number of characters to convert is given by len.unicode and ascii must be of size at least size len.

        //￼

        //int ExportFile(
        //    int type,
        //    char filename[],
        //    int64 start = 0,
        //    int64 size = 0,
        //    int64 startaddress = 0,
        //    int bytesperrow = 16,
        //    int wordaddresses = 0)
        //Exports the currently open file to a file on disk given by filename using one of the following type formats: 
        //EXPORT_HEXTEXT
        //EXPORT_DECTEXT
        //EXPORT_BINARYTEXT
        //EXPORT_CCODE
        //EXPORT_JAVACODE
        //EXPORT_INTEL8
        //EXPORT_INTEL16
        //EXPORT_INTEL32
        //EXPORT_S19
        //EXPORT_S28
        //EXPORT_S37
        //EXPORT_TEXT_AREA
        //EXPORT_HTML
        //EXPORT_RTF
        //EXPORT_BASE64
        //EXPORT_UUENCODE
        //The start and size arguments indicate what portion of the file to export.If they are both zero then the whole file is exported.startaddress indicates the starting address that is written to the file for Intel Hex or Motorola formats.bytesperrow indicates the number of bytes written on each line of the output file.If wordaddresses is true and the export format is Intel Hex, the file will be written using word-based addresses.See Importing/Exporting Files for more information on exporting. 

        //￼


        //TFindResults FindAll(
        //    <datatype> data,
        //int matchcase = true,
        //int wholeword = false,
        //int method = 0,
        //double tolerance = 0.0,
        //int dir = 1,
        //int64 start = 0,
        //int64 size = 0,
        //int wildcardMatchLength = 24)
        //This function converts the argument data into a set of hex bytes and then searches the current file for all occurrences of those bytes.data may be any of the basic types or an array of one of the types.If data is an array of signed bytes, it is assumed to be a null-terminated string. To search for an array of hex bytes, create an unsigned char array and fill it with the target value.If the type being search for is a string, the matchcase and wholeworld arguments can be used to control the search (see Using Find for more information). method controls which search method is used from the following options: 
        //FINDMETHOD_NORMAL=0 - a normal search
        //FINDMETHOD_WILDCARDS = 1 - when searching for strings use wildcards '*' or '?' 
        //FINDMETHOD_REGEX=2 - when searching for strings use Regular Expressions
        //wildcardMatchLength indicates the maximum number of characters a '*' can match when searching using wildcards. If the target is a float or double, the tolerance argument indicates that values that are only off by the tolerance value still match.If dir is 1 the find direction is down and if dir is 0 the find direction is up.start and size can be used to limit the area of the file that is searched.start is the starting byte address in the file where the search will begin and size is the number of bytes after start that will be searched. If size is zero, the file will be searched from start to the end of the file.
        //The return value is a TFindResults structure.This structure contains a count variable indicating the number of matches, and a start array holding an array of starting positions, plus a size array which holds an array of target lengths.For example, use the following code to find all occurrences of the ASCII string "Test" in a file: 
        //    int i;
        //    TFindResults r = FindAll("Test");
        //    Printf( "%d\n", r.count );
        //    for(i = 0; i<r.count; i++ )
        //        Printf( "%Ld %Ld\n", r.start[i], r.size[i] );
        //    To search for the floating point value 4.25 with tolerance 0.01, use the following code: 
        //     float value = 4.25f;
        //    TFindResults r = FindAll(value, true, false, 0, 0.01);
        //    Type Specifiers can be used in a string as an alternate way to specify the type of data to find.For example, the string "15.5,lf" can be used to search for the double 15.5. Using type specifiers is currently the only way to search for hex bytes with wildcards.For example: 

        //        TFindResults r = FindAll("FF*6A,h", true, false,
        //           FINDMETHOD_WILDCARDS);
        //    When searching for regular expressions with FindAll, remember to use '\\' to denote a backslash character.For example, to find all numbers with 8 digits use: 
        //     TFindResults r = FindAll("\\b\\d{8}\\b", true, false,
        //        FINDMETHOD_REGEX);
        //    Requires 010 Editor v4.0 or higher for the wildcardMatchLength parameter.
        //    Requires 010 Editor v6.0 or higher for method=FINDMETHOD_REGEX.

        //￼

        class TFindResults
        {
            int count;
            long[] start;
            long[] length;
        }

        [TemplateCallable]
        public static long FindFirst( 
            BinaryTemplateVariable data,
            bool matchcase = true,
            bool wholeword = false,
            int method = 0,
            double tolerance = 0.0,
            int dir = 1,
            long start = 0,
            long size = 0,
            int wildcardMatchLength = 24)
        {
            return 0;
        }
        //This function is identical to the FindAll function except that the return value is the position of the first occurrence of the target found.A negative number is returned if the value could not be found. 
        //Requires 010 Editor v4.0 or higher for the wildcardMatchLength parameter.
        //Requires 010 Editor v6.0 or higher for method= FINDMETHOD_REGEX.

        //￼

        //TFindInFilesResults FindInFiles(
        //    <datatype> data,
        //    char dir[],
        //    char mask[],
        //    int subdirs = true,
        //    int openfiles = false,
        //    int matchcase = true,
        //    int wholeword = false,
        //    int method = 0,
        //    double tolerance = 0.0,
        //    int wildcardMatchLength = 24,
        //    int followSymbolicLinks = true)
        //Searches for a given set of data across multiple files.See the FindAll function for information on the data, matchcase, wholeword, method, wildcardMatchLength and tolerance arguments. The dir argument indicates the starting directory where the search will take place.mask indicates which file types to search and may contain the characters '*' and '?'. If subdirs is true, all subdirectories are recursively searched for the value as well.If openfiles is true, only the currently open files are searched.If followSymbolicLinks and subdirs are true then all subdirectories that are symbolic links are searched, and if followSymbolicLinks is false then directories that are symbolic links are ignored. The return value is the TFindInFilesResults structure which contains a count variable indicate the number of files found plus an array of file variables. Each file variable contains a count variable indicating the number of matches, plus an array of start and size variables indicating the match position. For example: 
        //    int i, j;
        //    TFindInFilesResults r = FindInFiles("PK",
        //        "C:\\temp", "*.zip");
        //    Printf( "%d\n", r.count );
        //    for(i = 0; i<r.count; i++ )
        //    {
        //        Printf( "   %s\n", r.file[i].filename );
        //    Printf( "   %d\n", r.file[i].count );
        //        for(j = 0; j<r.file[i].count; j++ )
        //            Printf( "       %Ld %Ld\n",
        //                r.file[i].start[j],
        //                r.file[i].size[j] );
        //}
        //See Using Find In Files for more information. 
        //Requires 010 Editor v4.0 or higher for the wildcardMatchLength parameter. 
        //Requires 010 Editor v6.0 or higher for method=FINDMETHOD_REGEX. 
        //Requires 010 Editor v11.0 or higher for followSymbolicLinks. 

        //￼

        //int64 FindNext( int dir=1 )
        //This function returns the position of the next occurrence of the target value specified with the FindFirst function. If dir is 1, the find direction is down. If dir is 0, the find direction is up. The return value is the address of the found data, or -1 if the target is not found. 

        //￼

        //TFindStringsResults FindStrings( 
        //    int minStringLength,
        //    int type,
        //    int matchingCharTypes,
        //    wstring customChars="",
        //    int64 start=0,
        //    int64 size=0,
        //    int requireNull=false )
        //Attempts to locate any strings within a binary file similar to the Find Strings dialog which is accessed by clicking 'Search > Find Strings' on the main menu. Specify the minimum length of each string in number of characters with the minStringLength parameter. The type option tells the algorithm to look for ASCII strings, UNICODE strings or both by using one of the following constants: 
        //FINDSTRING_ASCII
        //FINDSTRING_UNICODE 
        //FINDSTRING_BOTH 
        //To specify which characters are considered as part of a string, use an OR bitmask ('|') of one or more of the following constants: 
        //FINDSTRING_LETTERS - the letters A..Z and a..z 
        //FINDSTRING_LETTERS_ALL - all international numbers including FINDSTRING_LETTERS 
        //FINDSTRING_NUMBERS - the numbers 0..9 
        //FINDSTRING_NUMBERS_ALL - all international numbers including FINDSTRING_NUMBERS 
        //FINDSTRING_SYMBOLS - symbols such as '#', '@', '!', etc. except for '_' 
        //FINDSTRING_UNDERSCORE - the character '_' 
        //FINDSTRING_SPACES - spaces or whitespace 
        //FINDSTRING_LINEFEEDS - line feed characters 0x0a, 0x0d 
        //FINDSTRING_CUSTOM - include any custom characters in the customChars string 
        //Note if the FINDSTRING_CUSTOM constant is included, any characters from customChars are considered as part of the string otherwise the customChars string is ignored. The start and size parameters indicate the range of the file to search and if size is zero, the file is searched starting from start to the end of the file. If requireNull is true, the strings must have a null (0) character after each string. 
        //The return value is a TFindStringsResults structure which contains a count variable with the number of strings found, a start array holding the starting position of each string, a size array holding the size in bytes of each string, and a type array which indicates FINDSTRING_ASCII if the string is an ASCII string or FINDSTRING_UNICODE if the string is a Unicode string. For example, the following code finds all ASCII strings of length at least 5 containing the characters "A..Za..z$&": 
        //    TFindStringsResults r = FindStrings(5, FINDSTRING_ASCII,
        //        FINDSTRING_LETTERS | FINDSTRING_CUSTOM, "$&");
        //Printf("%d\n", r.count);
        //for (i = 0; i < r.count; i++)
        //    Printf("%Ld %Ld %d\n", r.start[i], r.size[i], r.type[i]);
        //Requires 010 Editor v6.0 or higher. 

        //￼

        //int GetSectorSize()
        //Returns the size in bytes of the sectors for this drive. If this file is not a drive, the current sector size is defined using the 'View > Division Lines > Set Sector Size' menu option.

        //￼

        //int HexOperation(
        //    int operation,
        //    int64 start,
        //    int64 size,
        //    operand,
        //    step= 0,
        //    int64 skip = 0)
        //Perform any of the operations on hex data as available in the Hex Operations dialog. The operation parameter chooses which operation to perform and these operations are described in the Hex Operations dialog documentation. start and size indicate which range of bytes to operate on and if size is 0, the whole file is used. The operand indicates what value to use during the operation and the result is different depending upon which operation is used (see the Hex Operations dialog). operand can be any of the basic numeric or floating point types and the type of this parameter tells the function how to interpret the data. For example, if a 'ushort' is passed as an operand, the block of data is considered as an array of 'ushort' using the current endian.If step is non-zero, the operand is incremented by step after each operation and if skip is non-zero, skip number of bytes are skipped after each operation. This function returns the number of bytes modified if successful, or a negative number on error. The following constants can be used for the operation parameter: 
        //HEXOP_ASSIGN
        //HEXOP_ADD 
        //HEXOP_SUBTRACT 
        //HEXOP_MULTIPLY 
        //HEXOP_DIVIDE 
        //HEXOP_NEGATE 
        //HEXOP_MODULUS 
        //HEXOP_SET_MINIMUM 
        //HEXOP_SET_MAXIMUM 
        //HEXOP_SWAP_BYTES 
        //HEXOP_BINARY_AND 
        //HEXOP_BINARY_OR 
        //HEXOP_BINARY_XOR 
        //HEXOP_BINARY_INVERT 
        //HEXOP_SHIFT_LEFT 
        //HEXOP_SHIFT_RIGHT 
        //HEXOP_SHIFT_BLOCK_LEFT 
        //HEXOP_SHIFT_BLOCK_RIGHT 
        //HEXOP_ROTATE_LEFT 
        //HEXOP_ROTATE_RIGHT 
        //For example, the following code would treat the bytes from address 16 to 48 as an array of floats and add the value 3.0 to each float in the array: 
        //     HexOperation(HEXOP_ADD, 16, 32, (float)3.0f);
        //Alternately, the following code would swap all groups of 2 bytes in a file: 
        //     HexOperation(HEXOP_SWAP_BYTES, 0, 0, (ushort)0);
        //Requires 010 Editor v4.0 or higher. 

        //￼

        //int64 Histogram( int64 start, int64 size, int64 result[256] )
        //Counts the number of bytes of each value in the file from 0 up to 255. The bytes are counting starting from address start and continuing for size bytes. The resulting counts are stored in the int64 array results. For example, result[0] would indicate the number of 0 bytes values found in the given range of data. The return value is the total number of bytes read. 

        //￼

        //int ImportFile( int type, char filename[], int wordaddresses = false, int defaultByteValue=-1 )
        //Attempts to import the file specified by filename in one of the supported import formats. The format is given by the type argument and may be: 
        //IMPORT_HEXTEXT
        //IMPORT_DECTEXT 
        //IMPORT_BINARYTEXT 
        //IMPORT_SOURCECODE 
        //IMPORT_INTEL 
        //IMPORT_MOTOROLA 
        //IMPORT_BASE64 
        //IMPORT_UUENCODE 
        //If successful, the file is opened as a new file in the editor.If the function fails, a negative number is returned.If wordaddresses is true and the file is an Intel Hex or Motorola file, the file is imported using word-based addressing.When importing some data formats (such as Intel Hex or S-Records) these formats may skip over certain bytes. The value to assign these bytes can be controlled with the defaultByteValue parameter and if the parameter is -1, the value from the Importing Options dialog is used. See Importing/Exporting Files for more information on importing. 

        //￼

        //int IsDrive()
        //Returns true if the current file is a physical or logical drive, or false otherwise(see Editing Drives).

        //￼

        //int IsLogicalDrive()
        //Returns true if the current file is a logical drive, or false otherwise(see Editing Drives).

        //￼

        //int IsPhysicalDrive()
        //Returns true if the current file is a physical drive, or false otherwise(see Editing Drives).

        //￼

        //int IsProcess()
        //Returns true if the current file is a process, or false otherwise(see Editing Processes).

        //￼

        //int OpenLogicalDrive(char driveletter)
        //Opens the drive with the given driveLetter as a new file in the editor.For example, 'OpenLogicalDrive('c');'. This function returns a negative number on failure. See Editing Drives for more information on drive editing.

        //￼

        //int OpenPhysicalDrive(int physicalID )
        //Opens the physical drive physicalID as a new file in the editor(see Editing Drives). For example, 'OpenPhysicalDrive(0);'. This function returns a negative number on failure. 

        //￼

        //int OpenProcessById( int processID, int openwriteable=true )
        //Opens a process identified by the processID number (see Editing Processes). If openwriteable is true, only bytes that can be modified are opened, otherwise all readable bytes are opened. A negative number if returned if this function fails. 

        //￼

        //int OpenProcessByName( char processname[], int openwriteable = true )
        //Attempts to open a process given by the name processname as a new file in the editor.For example: 'OpenProcessByName( "cmd.exe" );' If openwriteable is true, only bytes that can be modified are opened, otherwise all readable bytes are opened. A negative number if returned if this function fails. See Editing Processes for more information. 

        //￼

        //int ReplaceAll(
        //    <datatype> finddata,
        //    <datatype> replacedata,
        //    int matchcase = true,
        //    int wholeword = false,
        //    int method = 0,
        //    double tolerance = 0.0,
        //    int dir = 1,
        //    int64 start = 0,
        //    int64 size = 0,
        //    int padwithzeros = false,
        //    int wildcardMatchLength = 24)
        //This function converts the arguments finddata and replacedata into a set of bytes, and then finds all occurrences of the find bytes in the file and replaces them with the replace bytes. The arguments matchcase, wholeword, method, wildcardMatchLength, tolerance, dir, start, and size are all used when finding a value and are discussed in the FindAll function above. If padwithzeros is true, a set of zero bytes are added to the end of the replace data until it is the same length as the find data. The return value is the number of replacements made. 
        #endregion

    }
}
