namespace Client.App.Scenarios.Second
{
    public class Arguments: First.Arguments
    {
        public int MeshCount { get; set; }
        public int MeshOperationalParallelism { get; set; }
    }
}