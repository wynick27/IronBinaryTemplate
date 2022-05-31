# IronBinaryTemplate
A C# implementation of 010 Editer's Binary Template Language for the Dynamic Language Runtime (DLR).

Currently in early stage, many functions are not tested.

## Usage


```csharp
public class Program
{
	public static void Main(string[] args)
	{
		BinaryTemplate runtime = new BinaryTemplate();
		runtime.RegisterBuiltinFunctions();
		var scope = runtime.RunTemplateFile(@"ZIP.bt",
		   @"test.zip");
		foreach (var variable in scope.Variables)
		{
			Console.WriteLine($"{variable.Type} {variable.Name} {variable.Start} {variable.Size}");
		}
		dynamic dynamicscope = scope;
		Console.WriteLine($"{dynamicscope.record.frFileName}");
	}
}

```
## Language Reference

See [010 Editor Documentation Page](https://www.sweetscape.com/010editor/manual/IntroTempScripts.htm)

Difference with 010 Editor:
- Multi-dimensional Arrays are supported.
  This script is valid:
```
    float matrix[4][4];
```
- Enums constants are defined in file scope, but not resolved first and is not an array.
```
local int Zero = 1;
local int value = Zero;
local int value2 = Zero[1];

enum {
   Zero,
   One,
};
```
  Above code evaluates value to 0 in 010 editor but to 1 in IronBinaryTemplate.
  010 editor also evaluates value2 to 0, IronBinaryTemplate throws an error in this case.
## Implemented Features

- [x] Types
  - [x] Basic Types
  - [x] Typedefs
  - [x] Enums
  - [x] Structs
  - [x] Unions
  - [x] Structs
    - [x] Structs with Arguments
	- [x] Local Structs
  - [x] Arrays
  - [x] Duplicated Arrays
  - [x] Strings
  - [X] Wide Strings
  - [X] Bitfields
	- [X] Padded Bitfields
	- [X] Unpadded Bitfields
  - [ ] Disassembly
- [x] Special Attributes
  - [ ] On-Demand Structures
  - [ ] Inline Size Functions
  - [ ] Inline Read Functions
  - [ ] Inline Write Functions
- [x] Statements
  - [X] if Statements
  - [X] for Statements
  - [X] while Statements
  - [X] switch statements 
  - [X] break and continue
  - [X] return
- [x] Functions
  - [x] Custom Functions
  - [x] Buildin Functions
	- [ ] Interface Functions
	- [x] I/O Functions
	- [ ] String Functions
	- [x] Math Functions
	- [ ] Tool Functions
  - [x] External CLR Functions
  - [ ] External Native Functions

- [x] Preprocessor
  - [x] Defines
  - [x] Conditional Compilation
  - [x] External Functions
  - [x] Includes