using System;
using System.Collections.Generic;
using System.Linq;
using Common.Domain;
using Common.Providers;

namespace Demo.Services
{
    /// <summary>
    /// Manages a shared dataset that can be synchronized across multiple chart views.
    /// When a data point is deleted, all subscribers are notified to update their displays.
    /// </summary>
    public class SharedDataService
    {
        private readonly IDataSetProvider dataProvider;
        private ProcessedDataSet currentDataSet;
        private readonly object lockObject = new object();

        public SharedDataService()
        {
            dataProvider = new SphereDataSetProvider();
            currentDataSet = new ProcessedDataSet
            {
                DataPoints = Array.Empty<SphereDataPoint>(),
                GeneratedAt = DateTime.Now
            };
        }

        /// <summary>
        /// Gets the current dataset (read-only access)
        /// </summary>
        public ProcessedDataSet CurrentDataSet
        {
            get
            {
                lock (lockObject)
                {
                    return currentDataSet;
                }
            }
        }

        /// <summary>
        /// Event fired when the dataset changes (generation or deletion)
        /// </summary>
        public event EventHandler<DataSetChangedEventArgs>? DataSetChanged;

        /// <summary>
        /// Event fired when a specific data point is deleted
        /// </summary>
        public event EventHandler<DataPointDeletedEventArgs>? DataPointDeleted;

        /// <summary>
        /// Generates a new dataset with the specified count of data points
        /// </summary>
        /// <param name="count">Number of data points to generate</param>
        /// <param name="seed">Optional random seed for reproducibility</param>
        public void GenerateDataSet(int count, int? seed = null)
        {
            if (count < 0)
                throw new ArgumentException("Count must be non-negative", nameof(count));

            lock (lockObject)
            {
                currentDataSet = dataProvider.GenerateDataSet(count, seed);
            }

            OnDataSetChanged(new DataSetChangedEventArgs(currentDataSet, DataSetChangeType.Regenerated));
        }

        /// <summary>
        /// Deletes a data point at the specified index
        /// </summary>
        /// <param name="index">Index of the data point to delete</param>
        /// <returns>True if the point was deleted, false if index is invalid</returns>
        public bool DeleteDataPoint(int index)
        {
            lock (lockObject)
            {
                if (index < 0 || index >= currentDataSet.DataPoints.Count)
                    return false;

                var dataPointsList = currentDataSet.DataPoints.ToList();
                dataPointsList.RemoveAt(index);

                currentDataSet = new ProcessedDataSet
                {
                    DataPoints = dataPointsList,
                    GeneratedAt = currentDataSet.GeneratedAt
                };
            }

            OnDataPointDeleted(new DataPointDeletedEventArgs(index));
            OnDataSetChanged(new DataSetChangedEventArgs(currentDataSet, DataSetChangeType.PointDeleted));

            return true;
        }

        /// <summary>
        /// Gets the number of data points in the current dataset
        /// </summary>
        public int DataPointCount
        {
            get
            {
                lock (lockObject)
                {
                    return currentDataSet.DataPoints.Count;
                }
            }
        }

        protected virtual void OnDataSetChanged(DataSetChangedEventArgs e)
        {
            DataSetChanged?.Invoke(this, e);
        }

        protected virtual void OnDataPointDeleted(DataPointDeletedEventArgs e)
        {
            DataPointDeleted?.Invoke(this, e);
        }
    }

    /// <summary>
    /// Event arguments for dataset change events
    /// </summary>
    public class DataSetChangedEventArgs : EventArgs
    {
        public ProcessedDataSet DataSet { get; }
        public DataSetChangeType ChangeType { get; }

        public DataSetChangedEventArgs(ProcessedDataSet dataSet, DataSetChangeType changeType)
        {
            DataSet = dataSet;
            ChangeType = changeType;
        }
    }

    /// <summary>
    /// Event arguments for data point deletion events
    /// </summary>
    public class DataPointDeletedEventArgs : EventArgs
    {
        public int DeletedIndex { get; }

        public DataPointDeletedEventArgs(int deletedIndex)
        {
            DeletedIndex = deletedIndex;
        }
    }

    /// <summary>
    /// Type of dataset change
    /// </summary>
    public enum DataSetChangeType
    {
        Regenerated,
        PointDeleted
    }
}

