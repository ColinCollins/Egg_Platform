using System;
using System.Threading.Tasks;

namespace GF
{
    public partial class App
    {
        public static void ChangeProcedure<T>(params object[] args) where T : FsmState<App>, new()
        {
            Procedure.ChangeState<T>(args);
        }

        public static void ChangeProcedure(Type type, params object[] args)
        {
            Procedure.ChangeState(type, args);
        }
        
        public static FsmState<App> GetProcedure<T>() where T : FsmState<App>
        {
            return Procedure.GetState<T>();
        }

        public static T GetData<T>(string key)
        {
            return Procedure.GetData<T>(key);
        }

        public static void SetData<T>(string key, T value)
        {
            Procedure.SetData<T>(key, value);
        }
    }
}