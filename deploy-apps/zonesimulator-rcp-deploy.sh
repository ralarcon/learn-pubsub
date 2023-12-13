#!/bin/bash
echo "Befor run this script you must ensure the secrets are available"
echo "by running secrets-creat-mqtt-passwords.sh and secrets-create-pull-serviceprincipal.sh scripts."
echo ""
echo "---"
echo ""
echo "Applying zonesimulator-rcp-configmap.yaml"
kubectl apply -f ./zonesimulator-rcp-configmap.yaml

echo "Applying zonesimulator-rcp.yaml"
kubectl apply -f ./zonesimulator-rcp.yaml