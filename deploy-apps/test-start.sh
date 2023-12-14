#!/bin/bash

#iot-pocs-sim-2 (Items Generator + Zone Shp) 4.233.192.238
ssh -i ~/.ssh/ralarcon_azure_rsa -p 12123 ralarcon-azure@4.233.192.238 'cd ~/deploy-apps/ && sudo ./generator-deploy.sh'

#iot-pocs-sim-3 (Zone Rcp)
ssh -i ~/.ssh/ralarcon_azure_rsa -p 12123 ralarcon-azure@4.233.192.242 'cd ~/deploy-apps/ && sudo ./zonesimulator-rcp-deploy.sh'

#iot-pocs-sim-1 (MQTT + Zone Pac)
ssh -i ~/.ssh/ralarcon_azure_rsa -p 12123 ralarcon-azure@4.233.192.237 'cd ~/deploy-apps/ && sudo ./zonesimulator-pac-deploy.sh'

#iot-pocs-sim-2 (Items Generator + Zone Shp) 4.233.192.238
ssh -i ~/.ssh/ralarcon_azure_rsa -p 12123 ralarcon-azure@4.233.192.238 'cd ~/deploy-apps/ && sudo ./zonesimulator-shp-deploy.sh'



