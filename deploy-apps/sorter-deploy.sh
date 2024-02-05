#!/bin/bash
echo "Before running this script you must ensure the secrets are available."
echo "Exec secrets-creat-mqtt-passwords.sh and secrets-create-pull-serviceprincipal.sh scripts."
echo ""
echo "---"
echo ""
echo "Applying sortersimulator-configmap.yaml"
kubectl apply -f ./sortersimulator-configmap.yaml

echo "Applying sortersimulator.yaml"
kubectl apply -f ./sortersimulator.yaml