
# Running an Azure DevOps Build Agent in a Docker Container on Azure Service Fabric Mesh

A prototype/sample/example of how to do it...
- Using the preview ServiceFabricMesh preview apis generated from the Azure api documetation. 
- Modifying the Azure DevOps Docker example to include the downloaded agent directly in the docker image

## Usage
```
Client.App.exe [options]
```
### Running
```
Press [ENTER] to CREATE/DELETE the build agent mesh
Press [Q] to quit the simulation
```

### Help
```
An experiment to deploy an DevOps build agent to Azure Service Fabric Mesh

Usage: Docker DevOps Agent Azure Mesh Client [options]

Options:
  -?|-h|--help                  Show help information
  -n|--name                     Mesh Name
  -i|--imageName                Image Name
  -rg|--resourceGroup           Resource Group
  -ci|--clientId                Client ID
  -cs|--clientSecret            Client Secret (or env:'DDOAAMC_ClientSecret')
  -t|--tenantId                 Tenant ID (or env:'DDOAAMC_TenantId')
  -s|--subscriptionId           Subscription ID (or env:'DDOAAMC_SubscriptionId')
  -irs|--imageRegistryServer    Docker Image Registry Server
  -iru|--imageRegistryUsername  Docker Image Registry Username
  -irp|--imageRegistryPassword  Docker Image Registry Password (or env:'DDOAAMC_ImageRegistryPassword')
  -azpu|--azurePipelinesUrl     Azure Pipelines Url
  -azpt|--azurePipelinesToken   Azure Pipelines Token (or env:'DDOAAMC_ImageRegistryPassword')
```
  
## Resources

Azure DevOps Pipelines Documentation - [Running a self-hosted agent in Docker](https://docs.microsoft.com/en-us/azure/devops/pipelines/agents/docker?view=azure-devops)

Azure SDK - https://github.com/Azure/azure-sdk-for-net

ServiceFabricMesh Api v2018-09-01-preview - https://github.com/Azure/azure-rest-api-specs/tree/master/specification/servicefabricmesh/resource-manager
