namespace Client.App.FirstScenario
{

    public class Arguments
    {
        public string Name { get; set; }
        public string ImageName { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string TenantId { get; set; }
        public string SubscriptionId { get; set; }
        public string ImageRegistryServer { get; set; }
        public string ImageRegistryUsername { get; set; }
        public string ImageRegistryPassword { get; set; }
        public string ResourceGroup { get; set; }
        public string AzurePipelinesUrl { get; set; }
        public string AzurePipelinesToken { get; set; }
    }
}