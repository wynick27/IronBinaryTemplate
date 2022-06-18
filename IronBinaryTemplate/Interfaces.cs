using System;

namespace IronBinaryTemplate
{
    public interface IBinaryTemplateScope
    {
        BinaryTemplateVariable GetVariable(string name);

        object this[string name]
        {
            get {
                BinaryTemplateVariable var = this.GetVariable(name);
                //        throw new MemberAccessException(name);
                   // Console.WriteLine($"{scope.Context.Position} IGetVariable {name} = {var.Value}");
                    if (var is IBinaryTemplateScope || var is IBinaryTemplateArray)
                        return var;
                    else
                        return var.Value;
            }
            set 
            {
                BinaryTemplateVariable var = this.GetVariable(name);
                //if (!scope.TryGetVariable(name, out BinaryTemplateVariable var))
                //        throw new MemberAccessException(name);
                //    Console.WriteLine($"{scope.Context.Position} TSetVariable {name} = {var.Value}");
                var.Value = value;

            }
        }
    }
    public interface IBinaryTemplateArray
    {
        int Length { get; }
        BinaryTemplateVariable GetVariable(int index);

        object this[int index]
        {
            get => GetVariable(index).Value;
            set => GetVariable(index).Value = value;
        }
    }


}
