using System.Collections.ObjectModel;


namespace IronBinaryTemplate
{
    public class VariableCollection : KeyedCollection<string, BinaryTemplateVariable>
    {
        bool allowDuplicates;
        bool writeDictionary = true;

        public VariableCollection(bool allowDuplicates = true)
        {
            this.allowDuplicates = allowDuplicates;
        }
        protected override string GetKeyForItem(BinaryTemplateVariable item)
        {
            return writeDictionary ? item.Name : null;
        }

        protected override void InsertItem(int index, BinaryTemplateVariable item)
        {
            if (item.Name != null && allowDuplicates && Dictionary != null && Dictionary.ContainsKey(item.Name))
            {
                if (Dictionary[item.Name] is BinaryTemplateDuplicatedArray duparr)
                {
                    duparr.AddVariable(item);
                    writeDictionary = false;
                    base.InsertItem(index, item);
                    writeDictionary = true;
                }
                else
                {
                    duparr = new BinaryTemplateDuplicatedArray();
                    duparr.Name = item.Name;
                    var first = Dictionary[duparr.Name];
                    duparr.AddVariable(first);
                    duparr.AddVariable(item);
                    Dictionary.Remove(duparr.Name);
                    writeDictionary = false;
                    base.InsertItem(index, item);
                    writeDictionary = true;
                    base.Dictionary[duparr.Name] = duparr;
                }
                return;
            }
            base.InsertItem(index, item);
        }
    }



    public class DefinitionCollection : KeyedCollection<string, VariableDeclaration>
    {
        protected override string GetKeyForItem(VariableDeclaration item)
        {

            return item.Name;
        }
    }

    public class TypeDefinitionCollection : KeyedCollection<string, TypeDefinition>
    {
        protected override string GetKeyForItem(TypeDefinition item)
        {
            return item.Name;
        }
    }

    public class CustomAttributeCollection : KeyedCollection<string, CustomAttribute>
    {
        protected override string GetKeyForItem(CustomAttribute item)
        {
            return item.Name;
        }

    }
}
