using ExampleShared.Core.Domain;

namespace ExampleShared.Core.Pipeline
{
    public interface IDataProcessor
    {
        ProcessedDataSet Process(ProcessedDataSet raw);
    }
}


