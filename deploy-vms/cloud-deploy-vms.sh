#!/bin/bash
# Variables
resourceGroup="iot-pocs-rg"
virtualNetwork="iot-pocs-vnet"
subnet="default"
vmSize="Standard_D2s_v3"
location="francecentral"
adminUsername="ralarcon-azure"
sshKeyName="ralarcon_azure_ssh_keypair"
evictionPolicy="Deallocate"
proximityPlacementGroup="iot-pocs-ppg"
nsgName="iot-pocs-vm1-nsg"
adminPass=$1
avZone=1
logAnalytics="iot-pocs-logs"

# # Crear el grupo de recursos
# # az group create --name $resourceGroup --location $location

# # Crear la red virtual
# # az network vnet create --resource-group $resourceGroup --name $virtualNetwork --address-prefixes 10.1.0.0/16 --subnet-name $subnet --subnet-prefix 10.1.0.0/24

# # Create log analytics
az monitor log-analytics workspace create --resource-group $resourceGroup --name $logAnalytics --location $location

# Crear las máquinas virtuales con todas las especificaciones
for i in {1..3}
do
    echo "Creating public-ip: iot-pocs-sim-pip-$i"
    az network public-ip create --resource-group $resourceGroup --name "iot-pocs-sim-$i-pip" --allocation-method Static --sku Standard --location $location --zone $avZone
    
    echo "Creating vm: iot-pocs-sim-$i"
    az vm create \
        --resource-group $resourceGroup \
        --name "iot-pocs-sim-$i" \
        --image Ubuntu2204 \
        --vnet-name $virtualNetwork \
        --subnet $subnet \
        --size $vmSize \
        --admin-username $adminUsername \
        --ssh-key-value "$(cat ~/.ssh/$sshKeyName.pub)" \
        --admin-password $adminPass \
        --custom-data cloud-init-script.txt \
        --public-ip-sku Standard \
        --public-ip-address "iot-pocs-sim-pip-$i" \
        --nsg $nsgName \
        --authentication-type all \
        --location $location \
        --zone $avZone \
        --priority Spot \
        --eviction-policy $evictionPolicy \
        --ppg $proximityPlacementGroup \
        --no-wait 
        

done

# Configurar el autoshutdown
for i in {1..3}
do
    echo "Configuring autoshutdown for vm iot-pocs-sim-$i"
    az vm auto-shutdown --resource-group $resourceGroup --name "iot-pocs-sim-$i" --time 0000 --location $location
done

# Configurar network acceleration
for i in {1..3}
do
    echo "Configuring network acceleration for vm iot-pocs-sim-$i"
    az network nic update --name "iot-pocs-sim-"$i"VMnic" --resource-group $resourceGroup --accelerated-networking true
done

# Configurar extensiones
for i in {1..3}
do
    echo "Configuring extensions for vm iot-pocs-sim-$i"
    sshPub="$(cat ~/.ssh/$sshKeyName.pub)"
    az vm extension set --resource-group $resourceGroup --vm-name "iot-pocs-sim-$i" \
        --publisher Microsoft.OSTCExtensions --version 1.4 \
        --name VMAccessForLinux \
        --protected-settings "{\"username\":\"$adminUsername\", \"ssh_key\":\"$sshPub\"}"
    
    az vm extension set --resource-group $resourceGroup --vm-name "iot-pocs-sim-$i" \
        --name NetworkWatcherAgentLinux --publisher Microsoft.Azure.NetworkWatcher --version 1.4

    logsId="$(az monitor log-analytics workspace show --resource-group "$resourceGroup" --name "$logAnalytics" --query customerId --output tsv | tr -cd "[:print:]\n")"
    logsKey="$(az monitor log-analytics workspace get-shared-keys --resource-group "$resourceGroup" --name "$logAnalytics" --query primarySharedKey -o tsv | tr -cd "[:print:]\n")"

    az vm extension set --resource-group $resourceGroup --vm-name "iot-pocs-sim-$i" \
            --name OmsAgentForLinux --publisher Microsoft.EnterpriseCloud.Monitoring \
            --version 1.17 \
            --protected-settings "{\"workspaceKey\":\"$logsKey\"}" \
            --settings "{\"workspaceId\":\"$logsId\",\"skipDockerProviderInstall\": false}"

done