namespace Veritas.Infrastructure.Analysis;

public sealed class AnalysisOptions
{
    public bool RunBackgroundWorker { get; set; } = true;
    public int PollSeconds { get; set; } = 5;
}

public sealed class ForensicsOptions
{
    public string PythonExecutable { get; set; } = "python";
    public string WorkerDirectory { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 300;
}
