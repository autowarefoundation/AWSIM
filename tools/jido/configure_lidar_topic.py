"""Allow full urban VLP16 scans on the isolated SIL LiDAR topic."""
from confluent_kafka.admin import AdminClient, ConfigResource, ResourceType, ConfigEntry, AlterConfigOpType

admin = AdminClient({"bootstrap.servers": "127.0.0.1:9092"})
resource = ConfigResource(ResourceType.TOPIC, "sil.jidocab.lidar.pointcloud.full",
                          incremental_configs=[ConfigEntry("max.message.bytes", "4194304",
                          incremental_operation=AlterConfigOpType.SET)])
for future in admin.incremental_alter_configs([resource]).values():
    future.result(15)
print("SIL full LiDAR topic: max.message.bytes=4194304")
