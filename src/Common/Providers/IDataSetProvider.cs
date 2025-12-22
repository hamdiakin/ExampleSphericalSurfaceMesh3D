using Common.Domain;

namespace Common.Providers
{
    public interface IDataSetProvider
    {
        ProcessedDataSet GenerateDataSet(int count, int? seed = null);
    }
}


