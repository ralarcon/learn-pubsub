#!/bin/bash
echo "Before running this script you must ensure the secrets are available."
echo "Exec secrets-creat-mqtt-passwords.sh and secrets-create-pull-serviceprincipal.sh scripts."
echo ""
echo "---"
echo ""
echo "Applying generator-configmap.yaml"
kubectl apply -f ./generator-configmap.yaml

echo "Applying generator.yaml"
kubectl apply -f ./generator.yaml