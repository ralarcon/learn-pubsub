#!/bin/bash
az acr build --image sim/itemsgenerator:{{.Run.ID}} -t sim/itemsgenerator:latest -r ragcdevacr -f ../src/Mqtt.ItemGenerator/Dockerfile ../src
az acr build --image sim/zonesimulator:{{.Run.ID}} -t sim/zonesimulator:latest -r ragcdevacr -f ../src/Mqtt.ZoneSimulator/Dockerfile ../src

source ./upload-assets-to-vms.sh
