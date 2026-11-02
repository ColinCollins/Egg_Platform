using UnityEngine;

namespace MelenitasDev.SoundsGood.Demo
{
    public partial class SG_DemoManager // Serialized Fields
    {
        [SerializeField] private GameObject menuLabel;
        [SerializeField] private GameObject[] demoLabels;
    }

    public partial class SG_DemoManager : MonoBehaviour
    {
        void Start ()
        {
            foreach (GameObject demoLabel in demoLabels)
            {
                demoLabel.SetActive(false);
            }
            menuLabel.SetActive(true);
        }
    }

    public partial class SG_DemoManager // Public Methods
    {
        public void StopAllAudios ()
        {
            SoundsGoodManager.StopAll();
        }
    }
}
