using ExampleShared.Core.Domain;

namespace ExampleShared.Core.Providers
{
    public interface IDataSetProvider
    {
        ProcessedDataSet GenerateDataSet(int count, int? seed = null);
    }
}

