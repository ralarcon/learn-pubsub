#!/bin/bash
# This script requires Azure CLI version 2.25.0 or later. Check version with `az --version`.

# Modify for your environment.
# ACR_NAME: The name of your Azure Container Registry
# SERVICE_PRINCIPAL_NAME: Must be unique within your AD tenant
ACR_NAME=$ContainerRegistry
SERVICE_PRINCIPAL_NAME=$ServicePrincipal

# Obtain the full registry ID
ACR_REGISTRY_ID=$(az acr show --name $ACR_NAME --query "id" --output tsv)
echo "Target Registry:" $ACR_REGISTRY_ID
echo "Target Service Principal Name:" $SERVICE_PRINCIPAL_NAME

# Create the service principal with rights scoped to the registry.
# Default permissions are for docker pull access. Modify the '--role'
# argument value as desired:
# acrpull:     pull only
# acrpush:     push and pull
# owner:       push, pull, and assign roles
PASSWORD=$(az ad sp create-for-rbac --name $SERVICE_PRINCIPAL_NAME --query "password" --output tsv  | tr -d '\r\n')
USER_NAME=$(az ad sp list --display-name $SERVICE_PRINCIPAL_NAME --query "[].appId" --output tsv  | tr -d '\r\n')


# Output the service principal's credentials; use these in your services and
# applications to authenticate to the container registry.
echo "Service principal ID: $USER_NAME"
echo "Service principal password: $PASSWORD"


# Assign the desired role to the service principal. Modify the '--role' argument
# value as desired:
# acrpull:     pull only
# acrpush:     push and pull
# owner:       push, pull, and assign roles

echo "Assigning role to SP for Acr Pull" 
az role assignment create --assignee $USER_NAME --scope $ACR_REGISTRY_ID --role acrpull


echo "Now create the secret in kubernetes cluster"

echo "kubectl create secret docker-registry ragcdevacr-pull-secret \\"
echo "    --namespace pubsub-tests \\"
echo "    --docker-server=$ContainerRegistry.azurecr.io \\"
echo "    --docker-username=$USER_NAME \\"
echo "    --docker-password=$PASSWORD"