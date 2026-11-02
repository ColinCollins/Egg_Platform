namespace GF
{
    public interface ILogger
    {
        void Log(string msg);
        void Warning(string msg);
        void Error(string msg);
        void Exception(System.Exception exception);
    }
}