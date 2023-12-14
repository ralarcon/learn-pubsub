#!/bin/bash
echo "Befor run this script you must ensure the secrets are available"
echo "by running secrets-creat-mqtt-passwords.sh and secrets-create-pull-serviceprincipal.sh scripts."
echo ""
echo "---"
echo ""
echo "Applying zonesimulator-pac-configmap.yaml"
kubectl apply -f ./zonesimulator-pac-configmap.yaml

echo "Applying zonesimulator-pac.yaml"
kubectl apply -f ./zonesimulator-pac.yaml