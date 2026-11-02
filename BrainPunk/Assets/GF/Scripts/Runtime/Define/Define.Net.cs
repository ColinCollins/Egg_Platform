namespace GF
{
    public partial class Define
    {
        public class Net
        {
            //网络连接完成
            public const string NET_CONNECT_COMPLETED = "NET_CONNECT_COMPLETED";
            //收到网络消息
            public const string NET_RECEIVE = "NET_RECEIVE";
            //收到网络消息，以cmd为后缀
            public const string NET_RECEIVE_WITH_CMD = "NET_RECEIVE_WITH_CMD_";

            //订阅服连接完成
            public const string SUBNET_CONNECT_COMPLETED = "SUBNET_CONNECT_COMPLETED";
        }
    }
}