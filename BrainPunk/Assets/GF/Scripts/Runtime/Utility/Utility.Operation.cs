using System;
using YooAsset;

namespace GF
{
    public partial class Utility
    {
        public static class Operation
        {
            public static void StartOperation(AsyncOperationBase operation)
            {
                OperationSystem.StartOperation(operation);
            }
        }
    }
}