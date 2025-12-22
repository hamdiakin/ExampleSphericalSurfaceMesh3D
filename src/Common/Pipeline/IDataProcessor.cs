using Common.Domain;

namespace Common.Pipeline
{
    public interface IDataProcessor
    {
        ProcessedDataSet Process(ProcessedDataSet raw);
    }
}


