
# Running an Azure DevOps Build Agent in a Docker Container on Azure Service Fabric Mesh

A prototype/sample/example of how to do it...
- Using the preview ServiceFabricMesh preview apis generated from the Azure api documetation. 
- Modifying the Azure DevOps Docker example to include the downloaded agent directly in the docker image

Polls the Application and Service.
Polls the Container for logs.

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

### Example Output
```
11:25:52 [DBUG] (01) InputManager Starting
Press [ENTER] to CREATE/DELETE the build agent mesh
Press [Q] to quit the simulation
11:26:00 [INFO] (05) MeshController Starting
11:26:03 [INFO] (08) MeshController Mesh Requested "stgoldmamesh177e3a8d60464b7a8e424dbd01c94b8c"
11:26:03 [INFO] (08) MeshController Started
11:26:04 [INFO] (07) MeshController Application ApplicationData {Services=null, ProvisioningState="Updating", HealthState="Ok", Status="Creating"}
11:26:04 [INFO] (04) MeshController Service ServiceData {CodePackages=[ServiceCodePackage {Name="AzureAgentContainer"}], ProvisioningState=null, HealthState="Ok", Status="Unknown"}
11:26:05 [INFO] (05) MeshController Application ApplicationData {Services=null, ProvisioningState="Updating", HealthState="Unknown", Status="Creating"}
11:26:15 [INFO] (04) MeshController Application ApplicationData {Services=null, ProvisioningState="Updating", HealthState="Ok", Status="Ready"}
11:26:25 [INFO] (04) MeshController Service ServiceData {CodePackages=[ServiceCodePackage {Name="AzureAgentContainer"}], ProvisioningState=null, HealthState="Ok", Status="Ready"}
11:26:25 [DBUG] (04) MeshController Starting Container Output Polling
11:27:13 [INFO] (05) MeshController Container: "2019-09-20T15:27:07.055084621+00:00 stdout P \u001B[?1h\u001B="
11:27:13 [INFO] (05) MeshController Container: "2019-09-20T15:27:07.448474230+00:00 stdout F "
11:27:13 [INFO] (05) MeshController Container: "2019-09-20T15:27:07.448474230+00:00 stdout F >> End User License Agreements:"
11:27:13 [INFO] (05) MeshController Container: "2019-09-20T15:27:07.448474230+00:00 stdout F "
11:27:13 [INFO] (05) MeshController Container: "2019-09-20T15:27:07.465457147+00:00 stdout F Building sources from a TFVC repository requires accepting the Team Explorer Everywhere End User License Agreement. This step is not required for building sources from Git repositories."
11:27:13 [INFO] (05) MeshController Container: "2019-09-20T15:27:07.465457147+00:00 stdout F "
11:27:13 [INFO] (05) MeshController Container: "2019-09-20T15:27:07.465457147+00:00 stdout F A copy of the Team Explorer Everywhere license agreement can be found at:"
11:27:13 [INFO] (05) MeshController Container: "2019-09-20T15:27:07.465457147+00:00 stdout F   /azp/agent/externals/tee/license.html"
11:27:13 [INFO] (05) MeshController Container: "2019-09-20T15:27:07.465457147+00:00 stdout F "
11:27:13 [INFO] (05) MeshController Container: "2019-09-20T15:27:07.479275262+00:00 stdout F "
11:27:13 [INFO] (05) MeshController Container: "2019-09-20T15:27:07.479275262+00:00 stdout F >> Connect:"
11:27:13 [INFO] (05) MeshController Container: "2019-09-20T15:27:07.479275262+00:00 stdout F "
11:27:13 [INFO] (05) MeshController Container: "2019-09-20T15:27:09.582381648+00:00 stdout F Connecting to server ..."
11:27:13 [INFO] (05) MeshController Container: "2019-09-20T15:27:10.438924140+00:00 stdout P \u001B[?1h\u001B="
11:27:13 [INFO] (05) MeshController Container: "2019-09-20T15:27:10.459140761+00:00 stdout F "
11:27:13 [INFO] (05) MeshController Container: "2019-09-20T15:27:10.459140761+00:00 stdout F >> Register Agent:"
11:27:13 [INFO] (05) MeshController Container: "2019-09-20T15:27:10.459140761+00:00 stdout F "
11:27:13 [INFO] (05) MeshController Container: "2019-09-20T15:27:10.725116838+00:00 stdout F Scanning for tool capabilities."
11:27:13 [INFO] (05) MeshController Container: "2019-09-20T15:27:10.787159702+00:00 stdout F Connecting to the server."
11:27:13 [INFO] (05) MeshController Container: "2019-09-20T15:27:11.546323593+00:00 stdout F Successfully added the agent"
11:27:13 [INFO] (05) MeshController Container: "2019-09-20T15:27:11.555714002+00:00 stdout F Testing agent connection."
11:27:13 [INFO] (05) MeshController Container: "2019-09-20T15:27:12.318545097+00:00 stdout F 2019-09-20 15:27:12Z: Settings Saved."
11:27:13 [INFO] (05) MeshController Container: "2019-09-20T15:27:12.351837732+00:00 stdout P \u001B[?1h\u001B="
11:27:13 [INFO] (05) MeshController Container: "2019-09-20T15:27:12.602478093+00:00 stdout F Starting Agent listener interactively"
11:27:13 [INFO] (05) MeshController Container: "2019-09-20T15:27:12.639881132+00:00 stdout F Started listener process"
11:27:13 [INFO] (05) MeshController Container: "2019-09-20T15:27:12.640300532+00:00 stdout F Started running service"
11:27:13 [INFO] (05) MeshController Container: "2019-09-20T15:27:13.186396901+00:00 stdout F Scanning for tool capabilities."
11:27:13 [INFO] (05) MeshController Container: "2019-09-20T15:27:13.283264702+00:00 stdout F Connecting to the server."
11:27:16 [INFO] (04) MeshController Container: "2019-09-20T15:27:15.639534760+00:00 stdout F 2019-09-20 15:27:15Z: Listening for Jobs"
11:27:17 [INFO] (04) MeshController Application ApplicationData {Services=null, ProvisioningState="Succeeded", HealthState="Ok", Status="Ready"}
11:27:29 [INFO] (04) MeshController Stop
11:27:29 [INFO] (04) MeshController Delete Mesh "stgoldmamesh177e3a8d60464b7a8e424dbd01c94b8c"
11:27:32 [INFO] (09) MeshController Stopped
11:27:35 [INFO] (04) MeshController Quitting
11:27:35 [DBUG] (04) MeshController Quit
```
  
## Resources

Azure DevOps Pipelines Documentation - [Running a self-hosted agent in Docker](https://docs.microsoft.com/en-us/azure/devops/pipelines/agents/docker?view=azure-devops)

Azure SDK - https://github.com/Azure/azure-sdk-for-net

ServiceFabricMesh Api v2018-09-01-preview - https://github.com/Azure/azure-rest-api-specs/tree/master/specification/servicefabricmesh/resource-manager
