namespace GF
{
    public class BuiltinEventDefine
    {
        public class Gateway
        {
            //Token过期或失效事件
            public static readonly string TokenInvalid = "GATEWAY_TOKEN_INVALID";
        }
        
        public class Storage
        {
            //本地Storage更新成功
            public static readonly string UpdateComplete = "STORAGE_UPDATE_COMPLETE";
        }
    }
}