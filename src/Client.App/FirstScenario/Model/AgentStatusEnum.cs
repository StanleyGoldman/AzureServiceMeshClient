namespace Client.App.FirstScenario.Model
{
    public enum ApplicationStatusEnum
    {
        Unknown,
        Creating,
        Ready,
        Deleting,
        NotFound
    }

    public enum ServiceStatusEnum
    {
        Unknown,
        Ready,
        NotFound
    }

    public enum AgentStatusEnum
    {
        Unknown,
        NotReady,
        Starting,
        Ready,
        NotFound
    }
}