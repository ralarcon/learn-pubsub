#!/bin/bash
echo "Create the docker pull secret in kubernetes cluster"
if [ "$#" -ne 3 ]; then
  echo "Usage: $0 <CONTAINER_REGISTRY_NAME> <USER_NAME> <PASSWORD>"
  exit 1
fi


CONTAINER_REGISTRY=$1
USER_NAME=$2
PASSWORD=$3

kubectl create secret docker-registry ragcdevacr-pull-secret \
    --namespace default \
    --docker-server=$CONTAINER_REGISTRY.azurecr.io \
    --docker-username=$USER_NAME \
    --docker-password=$PASSWORD