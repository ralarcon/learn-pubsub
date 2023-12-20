#!/bin/bash

#iot-pocs-sim-1 (MQTT + Zone Pac) 4.233.192.237

#iot-pocs-sim-2 (Items Generator + Zone Pac) 4.233.192.238
ssh -i ~/.ssh/ralarcon_azure_rsa -p 12123 ralarcon-azure@4.233.192.238 'sudo kubectl delete pods items-generator zone-simulator-pac'

#iot-pocs-sim-3 (Zone Rcp + Shp) 4.233.192.242
ssh -i ~/.ssh/ralarcon_azure_rsa -p 12123 ralarcon-azure@4.233.192.242 'sudo kubectl delete pods zone-simulator-rcp zone-simulator-shp'


