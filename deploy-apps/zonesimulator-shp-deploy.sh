#!/bin/bash
echo "Before run this script you must ensure the secrets are available"
echo "by running secrets-creat-mqtt-passwords.sh and secrets-create-pull-serviceprincipal.sh scripts."
echo ""
echo "---"
echo ""
echo "Applying zonesimulator-shp-configmap.yaml"
kubectl apply -f ./zonesimulator-shp-configmap.yaml

echo "Applying zonesimulator-shp.yaml"
kubectl apply -f ./zonesimulator-shp.yaml