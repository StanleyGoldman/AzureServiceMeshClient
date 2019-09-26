namespace Client.App.Model
{
    public enum ApplicationStatusEnum
    {
        Unknown,
        Failed,
        Creating,
        Ready,
        Deleting,
        NotFound
    }

    public enum ServiceStatusEnum
    {
        Unknown,
        Ready,
        NotFound,
        Failed
    }

    public enum AgentStatusEnum
    {
        Unknown,
        NotReady,
        Starting,
        Ready,
        NotFound,
        Failed
    }
}