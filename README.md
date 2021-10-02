# Azure Durable Functions

Exploring patterns on how to use and deploy durable functions. These steps makes it easy to verify function-app functionality in different Azure hosted services.

**NOTE:**

- All steps below are executed from the root folder (the same folder as this README file)

## Create shared Azure Resources

```Powershell

# ##################################################################
# Set your variables
# ##################################################################
# Set you Azure Subscription name and preferred resource location
# ------------------------------------------------------------------
$subscriptionName="<YOUR_AZURE_SUBSCRIPTION_NAME>"
$location="westeurope"
$myDemoNamePrefix="funcappdemo"
# ##################################################################

# Setting variables
$rand = Get-Random -Minimum 1000 -Maximum 9999
$resourceGroup="$myDemoNamePrefix-rg-$rand"
$storageAccountName = "${myDemoNamePrefix}st$rand"
$logWorkspaceName = "${myDemoNamePrefix}-log-$rand"
$appInsightsName = "${myDemoNamePrefix}-appi-$rand"
$functionCodeAppName = "${myDemoNamePrefix}-code-func-$rand"
$functionAciAppName = "${myDemoNamePrefix}-aci-func-$rand"
$functionAppPlanName = "${myDemoNamePrefix}-plan-$rand"
$containerRegistryName = "${myDemoNamePrefix}acr$rand"

# Run login first
az login

# Then set the subscription name explicitly.
az account set -s "$subscriptionName"
az account show

az group create -n $resourceGroup -l $location

az storage account create `
  -n $storageAccountName `
  -l $location `
  -g $resourceGroup `
  --sku Standard_LRS

# To access the preview Application Insights Azure CLI commands, you first need to run:
az extension add -n application-insights

# Create log workspace
az monitor log-analytics workspace create `
    --resource-group $resourceGroup `
    --workspace-name $logWorkspaceName `
    --location $location

$logWorkspaceId=$(az monitor log-analytics workspace list --query "[?contains(name, '$logWorkspaceName')].[id]" --output tsv)

# Now you can run the following to create your Application Insights resource:
az monitor app-insights component create `
    --app $appInsightsName `
    --location $location `
    --resource-group $resourceGroup `
    --application-type web `
    --kind web `
    --workspace $logWorkspaceId

az appservice plan create `
    --name $functionAppPlanName `
    --resource-group $resourceGroup `
    --is-linux `
    --location $location `
    --sku S1

```

## Deploy to App Service as code in Zip file

```Powershell

# Create the Azure App instance
az functionapp create `
  --name $functionCodeAppName `
  --plan $functionAppPlanName `
  --storage-account $storageAccountName `
  --app-insights $appInsightsName `
  --runtime dotnet `
  --os-type Linux  `
  --functions-version 3 `
  --resource-group $resourceGroup

# Publish the code to the created instance
dotnet publish ./Source/InProccessFuncApp/InProccessFuncApp.csproj -c Release
$publishFolder = "./Source/InProccessFuncApp/bin/Release/netcoreapp3.1/publish"

# Create the zip
$publishZip = "publish.zip"
if(Test-path $publishZip) {Remove-item $publishZip}
Add-Type -assembly "system.io.compression.filesystem"
[io.compression.zipfile]::CreateFromDirectory($publishFolder, $publishZip)

# Deploy the zipped package. This sometimes responds with a timeout. Retrying this command usually works.
az functionapp deployment source config-zip `
  -g $resourceGroup `
  -n $functionCodeAppName `
  --src $publishZip

```

## Deploy to App Service as prebuilt Docker Image

```Powershell

# Create the Azure App instance
az acr create `
    --name $containerRegistryName `
    --resource-group $resourceGroup `
    --sku Basic `
    --admin-enabled true `
    --location $location

# Fetch the Docker user, password and server for use later
# $dockerUserName = az acr credential show -n $containerRegistryName --query username
# $dockerUserPassword = az acr credential show -n $containerRegistryName --query passwords[0].value
$dockerServer=$(az acr list --query "[?contains(name, '$containerRegistryName')].[loginServer]" --output tsv)

# Do a docker login to make things easy for us. No need to use docker user/pwd when doing  'az acr login'
az acr login -n $containerRegistryName

$dockerImageName="$dockerServer/${functionAciAppName}:demo"

docker build ./Source -f ./Source/InProccessFuncApp/Dockerfile -t $dockerImageName

docker push $dockerImageName

```

Create the Function App Service with Docker image

```Powershell

az functionapp create `
  --name $functionAciAppName `
  --storage-account $storageAccountName `
  --resource-group $resourceGroup `
  --plan $functionAppPlanName `
  --os-type Linux  `
  --functions-version 3 `
  --deployment-container-image-name $dockerImageName

```
