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
        public const string Root = "simulator"; // THIS MUST BE COORDINATED WITH THE CONFIG IN ItemsGenerator and ConveyorSimulator
        private const string ItemsBase = $"{Root}/##_ZONE_##/items";
        private const string ItemStatusBase = $"{Root}/items/status/##_ITEM_ID_##";
        private const string ConveyorsBase = $"{Root}/##_ZONE_##/conveyors/##_CONVEYOR_ID_##/##_SENSOR_##"; 
        private const string ConveyorStatusBase = $"{Root}/##_ZONE_##/status/conveyors/##_CONVEYOR_ID_##";
        private const string JunctionStatusBase = $"{Root}/##_ZONE_##/status/junctions/##_JUNCTION_##";


        public static string Items(string zone) => ItemsBase.Replace("##_ZONE_##", zone);
        public static string ItemStatus(int id) => ItemStatusBase.Replace("##_ITEM_ID_##", id.ToString());
        public static string ConveyorSensor(string zone, string conveyor, string sensor) => ConveyorsBase.Replace("##_ZONE_##", zone).Replace("##_CONVEYOR_ID_##", conveyor).Replace("##_SENSOR_##", sensor);
        public static string ConveyorStatus(string zone, string conveyor) => ConveyorStatusBase.Replace("##_ZONE_##", zone).Replace("##_CONVEYOR_ID_##", conveyor);
        public static string JunctionStatus(string zone, string junction) =>JunctionStatusBase.Replace("##_ZONE_##", zone).Replace("##_JUNCTION_##", junction);

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
