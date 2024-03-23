namespace Entities
{
    public class CommandDataEntity
    {
        public int id { get; set; }
        public string username { get; set; }
        public ulong steam_id { get; set; }
        public bool must_be_online { get; set; }
        public List<string> commands { get; set; }

        public CommandDataEntity(int id, string username, ulong steam_id, bool must_be_online, List<string> commands)
        {
            this.id = id;
            this.username = username;
            this.steam_id = steam_id;
            this.must_be_online = must_be_online;
            this.commands = commands;
        }
    }

}
namespace Entities
{
    public class CommandsResponseEntity
    {
        public int status { get; set; }
        public List<CommandDataEntity> data { get; set; }

        public CommandsResponseEntity(int status, List<CommandDataEntity> data)
        {
            this.status = status;
            this.data = data;
        }
    }
}
namespace Entities
{
    public class ServerResponseEntity
    {

        public int id { get; set; }

        public ServerResponseEntity(int id)
        {
            this.id = id;
        }

    }
}