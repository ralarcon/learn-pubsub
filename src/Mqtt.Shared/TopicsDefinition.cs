using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Mqtt.Shared
{
    public class TopicsDefinition
    {
        public const string Root = "simulator"; // THIS MUST BE COORDINATED WITH THE CONFIG IN ItemsGenerator and ZoneSimulator
        private const string ItemsBaseTopic = $"{Root}/##_ZONE_##/items";
        private const string ItemsTerminatedTopic = $"{Root}/items/terminated";
        private const string ItemsLatenciesTopic = $"{Root}/items/latencies";
        private const string ItemStatusBaseTopic = $"{Root}/items/status/##_ITEM_ID_##";
        private const string ConveyorsBaseTopic = $"{Root}/##_ZONE_##/conveyors/##_CONVEYOR_ID_##/##_SENSOR_##"; 
        private const string ConveyorStatusBaseTopic = $"{Root}/##_ZONE_##/status/conveyors/##_CONVEYOR_ID_##";
        private const string PositionStatusBaseTopic = $"{Root}/##_ZONE_##/status/positions/##_POSITION_##";


        public static string Items(string zone) => ItemsBaseTopic.Replace("##_ZONE_##", zone);
        public static string ItemStatus(int id) => ItemStatusBaseTopic.Replace("##_ITEM_ID_##", id.ToString());
        public static string ItemsTerminated() => ItemsTerminatedTopic;
        public static string ItemsLatencies() => ItemsLatenciesTopic;
        public static string ConveyorSensor(string zone, string conveyor, string sensor) => ConveyorsBaseTopic.Replace("##_ZONE_##", zone).Replace("##_CONVEYOR_ID_##", conveyor).Replace("##_SENSOR_##", sensor);
        public static string ConveyorStatus(string zone, string conveyor) => ConveyorStatusBaseTopic.Replace("##_ZONE_##", zone).Replace("##_CONVEYOR_ID_##", conveyor);
        public static string PositionStatus(string zone, string position) =>PositionStatusBaseTopic.Replace("##_ZONE_##", zone).Replace("##_POSITION_##", position);

        public static string GetZone(string sourceTopic)
        {
            string result = string.Empty;
            if (!string.IsNullOrEmpty(sourceTopic))
            {
                var splitted = sourceTopic.Split("/");
                if(splitted.Length >= 2) 
                { 
                    result = splitted[1];
                }
            }
            return result;
        }
    }
}
