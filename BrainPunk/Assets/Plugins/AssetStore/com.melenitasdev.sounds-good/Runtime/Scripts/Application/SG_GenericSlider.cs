using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

namespace MelenitasDev.SoundsGood
{
    public partial class SG_GenericSlider // Serialized Fields
    {
        [Header("References")]
        [SerializeField] private TextMeshProUGUI percentageLabel;
        
        [Header("Events")]
        [SerializeField] private UnityEvent<float> onValueChange;
    }
    
    public partial class SG_GenericSlider // Serialized Fields
    {
        private Slider slider;
    }

    [RequireComponent(typeof(Slider))]
    public partial class SG_GenericSlider : MonoBehaviour
    {
        void Awake ()
        {
            slider = GetComponent<Slider>();
            slider.onValueChanged.AddListener(ChangeValue);
        }

        void Start ()
        {
            slider.value = 0.5f;
            ChangeValue(0.5f);
        }
    }

    public partial class SG_GenericSlider // Private Methods
    {
        private void ChangeValue (float value)
        {
            onValueChange?.Invoke(value);
            percentageLabel.text = $"{(value * 100):F0}%";
        }
    }
}
