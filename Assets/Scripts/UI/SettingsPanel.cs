using CityScape.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

namespace CityScape.UI
{
    /// <summary>
    /// Settings panel with volume, graphics quality, and auto-save controls.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        [Header("Panel Root")]
        [SerializeField] private GameObject panelRoot;

        [Header("Audio")]
        [SerializeField] private Slider          masterVolumeSlider;
        [SerializeField] private Slider          musicVolumeSlider;
        [SerializeField] private Slider          sfxVolumeSlider;
        [SerializeField] private AudioMixer      audioMixer;

        [Header("Graphics")]
        [SerializeField] private TMP_Dropdown    qualityDropdown;

        [Header("Auto-Save")]
        [SerializeField] private Toggle          autoSaveToggle;
        [SerializeField] private Slider          autoSaveIntervalSlider;
        [SerializeField] private TextMeshProUGUI autoSaveIntervalLabel;

        [Header("Close")]
        [SerializeField] private Button          closeButton;

        private void Start()
        {
            closeButton?.onClick.AddListener(Hide);

            masterVolumeSlider?.onValueChanged.AddListener(v =>
            {
                audioMixer?.SetFloat("MasterVolume", Mathf.Log10(Mathf.Max(v, 0.0001f)) * 20f);
            });
            musicVolumeSlider?.onValueChanged.AddListener(v =>
            {
                audioMixer?.SetFloat("MusicVolume", Mathf.Log10(Mathf.Max(v, 0.0001f)) * 20f);
            });
            sfxVolumeSlider?.onValueChanged.AddListener(v =>
            {
                audioMixer?.SetFloat("SFXVolume", Mathf.Log10(Mathf.Max(v, 0.0001f)) * 20f);
            });

            qualityDropdown?.onValueChanged.AddListener(v => QualitySettings.SetQualityLevel(v));

            autoSaveIntervalSlider?.onValueChanged.AddListener(v =>
            {
                if (autoSaveIntervalLabel != null)
                    autoSaveIntervalLabel.text = $"{v:F0} min";
            });

            Hide();
        }

        public void Show() { if (panelRoot != null) panelRoot.SetActive(true); }
        public void Hide() { if (panelRoot != null) panelRoot.SetActive(false); }
    }
}
