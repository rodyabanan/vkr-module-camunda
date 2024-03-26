namespace TaskManager.Domain.Integration.Camunda;

public sealed class CamundaOptions
{
    public const string Camunda = "Camunda";
    public Uri Url { get; set; }
    public bool IsEnabled { get; set; }
}
