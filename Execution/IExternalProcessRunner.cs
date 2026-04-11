namespace zavod.Execution;

public interface IExternalProcessRunner
{
    ExternalProcessResult Run(ExternalProcessRequest request);
}
