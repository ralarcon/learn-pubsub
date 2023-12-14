#!/bin/bash
#update remote files in VMs
resourceGroup="iot-pocs-rg"
vmsPattern="iot-pocs-sim"
vmIps=$(az vm list-ip-addresses --resource-group $resourceGroup --query "[?contains(virtualMachine.name,'$vmsPattern')].virtualMachine.network.publicIpAddresses[0].ipAddress" --output tsv)

for vmIp in $vmIps
do
    vmIp=$(echo $vmIp | tr -cd '[:print:]')
    echo $vmIp

    echo 'ssh -i ~/.ssh/ralarcon_azure_rsa -p 12123 ralarcon-azure@'$vmIp' mkdir -p ~/deploy-apps/'
    ssh -i ~/.ssh/ralarcon_azure_rsa -p 12123 ralarcon-azure@$vmIp 'rm ~/deploy-apps/*.yaml ~/deploy-apps/*.sh'
    ssh -i ~/.ssh/ralarcon_azure_rsa -p 12123 ralarcon-azure@$vmIp 'mkdir -p ~/deploy-apps/'
    
    echo 'scp -i ~/.ssh/ralarcon_azure_rsa -P 12123 -r ./*.* ralarcon-azure@'$vmIp':~/deploy-apps/'
    scp -i ~/.ssh/ralarcon_azure_rsa -P 12123 -r ./*.* ralarcon-azure@$vmIp:~/deploy-apps/
done