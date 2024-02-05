#!/bin/bash

if [ "$#" -ne 1 ]; then
  echo "Usage: $0 <password>"
  exit 1
fi

mqttPassword=$(echo -n "$1" | base64)

cat <<EOF > secrets.yaml
apiVersion: v1
kind: Secret
metadata:
  name: simulation-secrets
data:
  MqttConfig__Password: $mqttPassword
EOF

echo "Secrets file created successfully."

echo "Applying file to kubernetes"
kubectl apply -f secrets.yaml