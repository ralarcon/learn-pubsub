#!/bin/bash

#iot-pocs-sim-2 (Items Generator + Zone Pac) 4.233.192.238
ssh -i ~/.ssh/ralarcon_azure_rsa -p 12123 ralarcon-azure@4.233.192.238 'cd ~/deploy-apps/ && sudo ./generator-deploy.sh'
ssh -i ~/.ssh/ralarcon_azure_rsa -p 12123 ralarcon-azure@4.233.192.238 'cd ~/deploy-apps/ && sudo ./zonesimulator-pac-deploy.sh'

#iot-pocs-sim-3 (Zone Rcp + Zone Shp)
ssh -i ~/.ssh/ralarcon_azure_rsa -p 12123 ralarcon-azure@4.233.192.242 'cd ~/deploy-apps/ && sudo ./zonesimulator-rcp-deploy.sh'
ssh -i ~/.ssh/ralarcon_azure_rsa -p 12123 ralarcon-azure@4.233.192.242 'cd ~/deploy-apps/ && sudo ./zonesimulator-shp-deploy.sh'

#iot-pocs-sim-1 (MQTT) 4.233.192.237
# ALONE MQTT




