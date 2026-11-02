using System.Collections.Generic;
using UnityEngine;

namespace Bear.SaveModule
{
    public partial class ExampleData : BaseSaveDataSO
    {
        public static StorageType StorageType = StorageType.PlayerPrefs;

        [SerializeField] private int number = 1;

        [SerializeField] private List<string> stringArray;
        [SerializeField] private Dictionary<string, object> dic;
    }
}
