namespace IronBinaryTemplate
{
    public interface IBinaryTemplateScope
    {
        BinaryTemplateVariable GetVariable(string name);

        object this[string name]
        {
            get => GetVariable(name).Value;
            set => GetVariable(name).Value = value;
        }
    }
    public interface IBinaryTemplateArray
    {
        int Length { get; }
        BinaryTemplateVariable GetVariable(int index);
    }


}
