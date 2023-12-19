#!/bin/bash


#iot-pocs-sim-1 (MQTT + Zone Pac)
echo "Checking pods in iot-pocs-sim-1"
ssh -i ~/.ssh/ralarcon_azure_rsa -p 12123 ralarcon-azure@4.233.192.237 'sudo kubectl get pods'
echo

#iot-pocs-sim-2 (Items Generator + Zone Shp) 4.233.192.238
echo "Checking pods in iot-pocs-sim-2"
ssh -i ~/.ssh/ralarcon_azure_rsa -p 12123 ralarcon-azure@4.233.192.238 'sudo kubectl get pods'
echo

#iot-pocs-sim-3 (Zone Rcp)
echo "Checking pods in iot-pocs-sim-3"
ssh -i ~/.ssh/ralarcon_azure_rsa -p 12123 ralarcon-azure@4.233.192.242 'sudo kubectl get pods'
echo

echo "Checking clocks..."
#iot-pocs-vm1
ssh -i ~/.ssh/ralarcon_azure_rsa -p 12123 ralarcon-azure@4.233.145.224 'echo vm1 - $(date +"%Y-%m-%d %H:%M:%S,%3N")' &
clock4=$!
#iot-pocs-sim-1 (MQTT + Zone Pac)
ssh -i ~/.ssh/ralarcon_azure_rsa -p 12123 ralarcon-azure@4.233.192.237 'echo sim1 - $(date +"%Y-%m-%d %H:%M:%S,%3N")' &
clock3=$!
#iot-pocs-sim-2 (Items Generator + Zone Shp) 4.233.192.238
ssh -i ~/.ssh/ralarcon_azure_rsa -p 12123 ralarcon-azure@4.233.192.238 'echo sim2 - $(date +"%Y-%m-%d %H:%M:%S,%3N")' &
clock1=$!
#iot-pocs-sim-3 (Zone Rcp)
ssh -i ~/.ssh/ralarcon_azure_rsa -p 12123 ralarcon-azure@4.233.192.242 'echo sim3 - $(date +"%Y-%m-%d %H:%M:%S,%3N")' &
clock2=$!

wait $clock1 $clock2 $clock3 $clock4
echo
echo "Done!"


