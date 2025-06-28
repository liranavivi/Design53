namespace Processor.File.Services;

/// <summary>
/// Service for recording processor file metrics.
/// </summary>
public interface IProcessorFileMetricsService : IDisposable
{
    /// <summary>
    /// Records Deserialized InputData metrics
    /// </summary>
    /// <param name="processorName">Processor name for labeling</param>
    /// <param name="processorVersion">Processor version for labeling</param>
    /// <param name="orchestratedFlowId">The orchestrated flow ID for filtering (optional)</param>
    void RecordDeserializedInputData(string processorName, string processorVersion, Guid? orchestratedFlowId = null);

}
