/** C 2011 grammar built from the C11 Spec */
grammar BinaryTemplate;



constant
    :   IntegerConstant
    |   FloatingConstant
    //|   EnumerationConstant
    |   CharacterConstant
    ;

primaryExpression
    :   Identifier
    |   constant
    |   StringLiteral+
    |   '(' expression ')'
    ;


postfixExpression
    :   primaryExpression
    |   postfixExpression '[' expression ']'
    |   Identifier '(' argumentExpressionList? ')'
    |   postfixExpression '.' Identifier
    |   postfixExpression '++'
    |   postfixExpression '--'
  //  |   '(' typeName ')' '{' initializerList ','? '}'
    ;

argumentExpressionList
    :   assignmentExpression (',' assignmentExpression)*
    ;

unaryExpression
    :   postfixExpression
    |   '++' unaryExpression
    |   '--' unaryExpression
    |   unaryOperator castExpression
    |   'sizeof' unaryExpression
    |   'sizeof' '(' typeName ')'
    ;

unaryOperator
    :    '+' | '-' | '~' | '!' //'&' |
    ;

castExpression
    :   '(' typeName ')' castExpression
    |   unaryExpression
    ;

multiplicativeExpression
    :   castExpression
    |   multiplicativeExpression '*' castExpression
    |   multiplicativeExpression '/' castExpression
    |   multiplicativeExpression '%' castExpression
    ;

additiveExpression
    :   multiplicativeExpression
    |   additiveExpression '+' multiplicativeExpression
    |   additiveExpression '-' multiplicativeExpression
    ;

shiftExpression
    :   additiveExpression
    |   shiftExpression '<<' additiveExpression
    |   shiftExpression '>>' additiveExpression
    ;

relationalExpression
    :   shiftExpression
    |   relationalExpression '<' shiftExpression
    |   relationalExpression '>' shiftExpression
    |   relationalExpression '<=' shiftExpression
    |   relationalExpression '>=' shiftExpression
    ;

equalityExpression
    :   relationalExpression
    |   equalityExpression '==' relationalExpression
    |   equalityExpression '!=' relationalExpression
    ;

andExpression
    :   equalityExpression
    |   andExpression '&' equalityExpression
    ;

exclusiveOrExpression
    :   andExpression
    |   exclusiveOrExpression '^' andExpression
    ;

inclusiveOrExpression
    :   exclusiveOrExpression
    |   inclusiveOrExpression '|' exclusiveOrExpression
    ;

logicalAndExpression
    :   inclusiveOrExpression
    |   logicalAndExpression '&&' inclusiveOrExpression
    ;

logicalOrExpression
    :   logicalAndExpression
    |   logicalOrExpression '||' logicalAndExpression
    ;

conditionalExpression
    :   logicalOrExpression ('?' expression ':' conditionalExpression)?
    ;

assignmentExpression
    :   conditionalExpression
    |   unaryExpression assignmentOperator assignmentExpression
    ;

assignmentOperator
    :   '=' | '*=' | '/=' | '%=' | '+=' | '-=' | '<<=' | '>>=' | '&=' | '^=' | '|='
    ;

expression
    :   assignmentExpression
    |   expression ',' assignmentExpression
    ;

constantExpression
    :   conditionalExpression
    ;

declaration
    :   Typedef? declarationSpecifiers initDeclaratorList? ';'
    ;

declarationSpecifiers
    :   declarationSpecifier* typeSpecifier
    ;

declarationSpecifier
    :   
    'local'  //storageClassSpecifier
    |   'const' //typeQualifier
    |   'signed'  //typeSpecifier
    |   'unsigned' //typeSpecifier
    ;

initDeclaratorList
    :   initDeclarator customAttributeSpecifier?
    |   initDeclaratorList ',' initDeclarator customAttributeSpecifier?
    ;

initDeclarator
    :   declarator
    |   declarator '=' initializer
    ;


typeSpecifier
    :   'void'
    |   basicType
    |   structOrUnionSpecifier
    |   enumSpecifier
    |   Identifier
    ;

basicType
    :
    'char' |'byte' //| 'CHAR' |'BYTE'
    | 'uchar'| 'ubyte'//, UCHAR, UBYTE
    |'short' | 'int16'//, SHORT, INT16
    |'ushort' | 'uint16'//, USHORT, UINT16, WORD
    |'int'| 'int32' | 'long'// INT, INT32, LONG
    |'uint' | 'uint32' | 'ulong'//, UINT, UINT32, ULONG, DWORD
    |'int64' | 'quad'//, QUAD, INT64, __int64
    |'uint64' | 'uquad'//, UQUAD, UINT64, QWORD, __uint64
    
    |   'hfloat'
    |   'float'
    |   'double'
    ;
 

structOrUnionSpecifier
    :   structOrUnion Identifier? compoundStatement
    |   structOrUnion Identifier? '(' parameterTypeList? ')' compoundStatement
    |   structOrUnion Identifier
    ;

structOrUnion
    :   'struct'
    |   'union'
    ;

    /*
specifierQualifierList
    :   typeSpecifier specifierQualifierList?
    |   typeQualifier specifierQualifierList?
    ;
    */

enumSpecifier
    :   'enum' enumTypeSpecifier? Identifier?  '{' enumeratorList '}'
    |   'enum' enumTypeSpecifier? Identifier? '{' enumeratorList ',' '}'
    |   'enum' Identifier
    ;

enumTypeSpecifier
    :   '<' declarationSpecifiers '>'
    ;

enumeratorList
    :   enumerator
    |   enumeratorList ',' enumerator
    ;

enumerator
    :   Identifier 
    |   Identifier '=' constantExpression
    ;

//enumerationConstant
//    :   Identifier
//    ;

declarator
    :   varDeclarator
    |   bitfieldDeclarator
    ;

bitfieldDeclarator
    :
    Identifier? ':' constantExpression
    ;
varDeclarator
    :   Identifier ('(' argumentExpressionList? ')')?
    |   varDeclarator '[' assignmentExpression? ']'
    ;

customAttributeSpecifier
    :   '<' customAttributeList  '>'
    ;

customAttributeList
    :   customAttribute (',' customAttribute)*
    |   // empty
    ;

customAttribute
    :   Identifier '=' primaryExpression
    ;

    /*
typeQualifierList
    :   typeQualifier+
    ;
    */
functionDeclaration
    :
    declarationSpecifiers functionDeclarator 
    ;


functionDeclarator
    :
    Identifier '(' parameterTypeList? ')'
    ;


parameterTypeList
    :   parameterDeclaration (',' parameterDeclaration)*
    ;

paramDeclarator
    :   '&'? Identifier 
    |   paramDeclarator '[' assignmentExpression? ']'

    ;


parameterDeclaration
    :   declarationSpecifiers (paramDeclarator | abstractDeclarator?)
    ;

typeName
    :   declarationSpecifiers //abstractDeclarator?
    ;

abstractDeclarator
    :   directAbstractDeclarator 
    ;

directAbstractDeclarator
    :
    '['  assignmentExpression? ']'
    |   directAbstractDeclarator '['  assignmentExpression? ']'
    ;

initializer
    :   assignmentExpression
    |   '{' initializerList  ','? '}'
    ;

initializerList
    :   /*designation?*/ initializer
    |   initializerList ',' /*designation?*/ initializer
    ;
    /*
designation
    :   designatorList '='
    ;

designatorList
    :   designator
    |   designatorList designator
    ;

designator
    :   '[' constantExpression ']'
    |   '.' Identifier
    ;
*/

statement
    :   labeledStatement
    |   compoundStatement
    |   expressionStatement
    |   selectionStatement
    |   iterationStatement
    |   jumpStatement
    |   declaration
    ;

labeledStatement
    :   /*Identifier ':' statement
    |*/   'case' constantExpression ':' statement
    |   'default' ':' statement
    ;

compoundStatement
    :   '{' statement* '}'
    ;

expressionStatement
    :   expression? ';'
    ;

selectionStatement
    :   If '(' expression ')' statement ('else' statement)?
    |   Switch '(' expression ')' compoundStatement
    ;

iterationStatement
    :   While '(' expression ')' statement
    |   Do statement While '(' expression ')' ';'
    |   For '(' forCondition ')' statement
    ;

//    |   'for' '(' expression? ';' expression?  ';' forUpdate? ')' statement
//    |   For '(' declaration  expression? ';' expression? ')' statement

forCondition
	:   forDeclaration ';' forExpression=expression? ';' forUpdate=expression?
	|   forInit=expression? ';' forExpression=expression? ';' forUpdate=expression?
	;

forDeclaration
    :   declarationSpecifiers initDeclaratorList?
	//| 	declarationSpecifiers
    ;


jumpStatement
    :      Continue ';'
    |   Break ';'
    |   Return expression? ';'
    ;

compilationUnit
    :   externalDeclaration* EOF
    ;
 

externalDeclaration
    :   functionDefinition
    //|   declaration
    |   functionDeclaration ';'
    |   statement
    
    |   ';' // stray ;
    ;

functionDefinition
    :   functionDeclaration compoundStatement
    ;


//Auto : 'auto';
Break : 'break';
Case : 'case';
Char : 'char';
Const : 'const';
Continue : 'continue';
Default : 'default';
Do : 'do';
Double : 'double';
Else : 'else';
Enum : 'enum';
//Extern : 'extern';
Float : 'float';
For : 'for';
//Goto : 'goto';
If : 'if';
//Inline : 'inline';
Int : 'int';
Long : 'long';
//Register : 'register';
//Restrict : 'restrict';
Return : 'return';
Short : 'short';
Signed : 'signed';
Sizeof : 'sizeof';
Static : 'static';
Struct : 'struct';
Switch : 'switch';
Typedef : 'typedef';
Union : 'union';
Unsigned : 'unsigned';
Void : 'void';
//Volatile : 'volatile';
While : 'while';

LeftParen : '(';
RightParen : ')';
LeftBracket : '[';
RightBracket : ']';
LeftBrace : '{';
RightBrace : '}';

Less : '<';
LessEqual : '<=';
Greater : '>';
GreaterEqual : '>=';
LeftShift : '<<';
RightShift : '>>';

Plus : '+';
PlusPlus : '++';
Minus : '-';
MinusMinus : '--';
Star : '*';
Div : '/';
Mod : '%';

And : '&';
Or : '|';
AndAnd : '&&';
OrOr : '||';
Caret : '^';
Not : '!';
Tilde : '~';

Question : '?';
Colon : ':';
Semi : ';';
Comma : ',';

Assign : '=';
// '*=' | '/=' | '%=' | '+=' | '-=' | '<<=' | '>>=' | '&=' | '^=' | '|='
StarAssign : '*=';
DivAssign : '/=';
ModAssign : '%=';
PlusAssign : '+=';
MinusAssign : '-=';
LeftShiftAssign : '<<=';
RightShiftAssign : '>>=';
AndAssign : '&=';
XorAssign : '^=';
OrAssign : '|=';

Equal : '==';
NotEqual : '!=';

//Arrow : '->';
Dot : '.';
//Ellipsis : '...';

Identifier
    :   IdentifierNondigit
        (   IdentifierNondigit
        |   Digit
        )*
    ;

fragment
IdentifierNondigit
    :   Nondigit
    |   UniversalCharacterName
    //|   // other implementation-defined characters...
    ;

fragment
Nondigit
    :   [a-zA-Z_]
    ;

fragment
Digit
    :   [0-9]
    ;

fragment
UniversalCharacterName
    :   '\\u' HexQuad
    |   '\\U' HexQuad HexQuad
    ;

fragment
HexQuad
    :   HexadecimalDigit HexadecimalDigit HexadecimalDigit HexadecimalDigit
    ;



IntegerConstant
    :  ( DecimalConstant
    |   OctalConstant
    |   HexadecimalConstant
    |   HexadecimalDigit+ [hH]
    |	BinaryConstant) [uUlL]*
    ;

fragment
BinaryConstant
	:	'0' [bB] [0-1]+
	;

fragment
DecimalConstant
    :   NonzeroDigit Digit*
    ;

fragment
OctalConstant
    :   '0' OctalDigit*
    ;

fragment
HexadecimalConstant
    :   HexadecimalPrefix HexadecimalDigit+
    ;

fragment
HexadecimalPrefix
    :   '0' [xX]
    ;

fragment
NonzeroDigit
    :   [1-9]
    ;

fragment
OctalDigit
    :   [0-7]
    ;

fragment
HexadecimalDigit
    :   [0-9a-fA-F]
    ;

FloatingConstant
    :   DecimalFloatingConstant
//    |   HexadecimalFloatingConstant
    ;

fragment
DecimalFloatingConstant
    :   FractionalConstant ExponentPart? FloatingSuffix?
    |   DigitSequence ExponentPart FloatingSuffix?
    ;
/*
fragment
HexadecimalFloatingConstant
    :   HexadecimalPrefix HexadecimalFractionalConstant BinaryExponentPart FloatingSuffix?
    |   HexadecimalPrefix HexadecimalDigitSequence BinaryExponentPart FloatingSuffix?
    ;
    */
fragment
FractionalConstant
    :   DigitSequence? '.' DigitSequence
    |   DigitSequence '.'
    ;

fragment
ExponentPart
    :   'e' Sign? DigitSequence
    |   'E' Sign? DigitSequence
    ;

fragment
Sign
    :   '+' | '-'
    ;

fragment
DigitSequence
    :   Digit+
    ;
    /*
fragment
HexadecimalFractionalConstant
    :   HexadecimalDigitSequence? '.' HexadecimalDigitSequence
    |   HexadecimalDigitSequence '.'
    ;

fragment
BinaryExponentPart
    :   'p' Sign? DigitSequence
    |   'P' Sign? DigitSequence
    ;

fragment
HexadecimalDigitSequence
    :   HexadecimalDigit+
    ;
    */
fragment
FloatingSuffix
    :   [fF]
    ;


CharacterConstant
    :   '\'' CCharSequence '\''
    |   'L\'' CCharSequence '\''
    |   'u\'' CCharSequence '\''
    |   'U\'' CCharSequence '\''
    ;

fragment
CCharSequence
    :   CChar+
    ;

fragment
CChar
    :   ~['\\\r\n]
    |   EscapeSequence
    ;

fragment
EscapeSequence
    :   SimpleEscapeSequence
    |   OctalEscapeSequence
    |   HexadecimalEscapeSequence
    |   UniversalCharacterName
    ;

fragment
SimpleEscapeSequence
    :   '\\' ['"?abfnrtv\\]
    ;

fragment
OctalEscapeSequence
    :   '\\' OctalDigit
    |   '\\' OctalDigit OctalDigit
    |   '\\' OctalDigit OctalDigit OctalDigit
    ;

fragment
HexadecimalEscapeSequence
    :   '\\x' HexadecimalDigit+
    ;

StringLiteral
    :   EncodingPrefix? '"' SCharSequence? '"'
    ;

fragment
EncodingPrefix
    :   'u8'
    |   'u'
    |   'U'
    |   'L'
    ;

fragment
SCharSequence
    :   SChar+
    ;

fragment
SChar
    :   ~["\\\r\n]
    |   EscapeSequence
    |   '\\\n'   // Added line
    |   '\\\r\n' // Added line
    ;

ComplexDefine
    :   '#' Whitespace? 'define'  ~[#\r\n]*
        -> skip
    ;
         
IncludeDirective
    :   '#' Whitespace? 'include' Whitespace? (('"' ~[\r\n]* '"') | ('<' ~[\r\n]* '>' )) Whitespace? Newline
        -> skip
    ;

// ignore the lines generated by c preprocessor                                   
// sample line : '#line 1 "/home/dm/files/dk1.h" 1'                           
LineAfterPreprocessing
    :   '#line' Whitespace* ~[\r\n]*
        -> skip
    ;  

LineDirective
    :   '#' Whitespace? DecimalConstant Whitespace? StringLiteral ~[\r\n]*
        -> skip
    ;

PragmaDirective
    :   '#' Whitespace? 'pragma' Whitespace ~[\r\n]*
        -> skip
    ;

Whitespace
    :   [ \t]+
        -> skip
    ;

Newline
    :   (   '\r' '\n'?
        |   '\n'
        )
        -> skip
    ;

BlockComment
    :   '/*' .*? '*/'
        -> skip
    ;

LineComment
    :   '//' ~[\r\n]*
        -> skip
    ;
