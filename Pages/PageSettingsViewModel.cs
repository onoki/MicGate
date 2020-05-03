using MicGate.Processing;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace MicGate.Pages
{
    public class PageSettingsViewModel : INotifyPropertyChanged
    {
        private AudioCore audioCore;

        public ObservableCollection<string> DevicesInput { get; set; }

        public ObservableCollection<string> DevicesOutput { get; set; }

        private string _devicesInputSelection;
        public string DevicesInputSelection
        {
            get => _devicesInputSelection;
            set
            {
                _devicesInputSelection = value;
                if (UpdateSetting("DevicesInputSelection", value))
                {
                    audioCore.RestartRecordingAndPlayback();
                }
            }
        }

        private string _devicesOutputSelection;
        public string DevicesOutputSelection
        {
            get => _devicesOutputSelection;
            set
            {
                _devicesOutputSelection = value;
                if (UpdateSetting("DevicesOutputSelection", value))
                {
                    audioCore.RestartRecordingAndPlayback();
                }
            }
        }

        private bool _startWindowMinimized;
        public bool StartWindowMinimized
        {
            get => _startWindowMinimized;
            set
            {
                _startWindowMinimized = value;
                UpdateSetting("StartWindowMinimized", value.ToString());
            }
        }


        private float _volumeBoost;
        public float VolumeBoost
        {
            get => _volumeBoost;
            set
            {
                _volumeBoost = value;
                UpdateSetting("VolumeBoost", value);
            }
        }

        private int _gateThreshold;
        public int GateThreshold { 
            get => _gateThreshold;
            set 
            {
                _gateThreshold = value;
                UpdateSetting("GateThreshold", value.ToString());
            }
        }

        private int _gateAttackDurationMs;
        public int GateAttackDurationMs
        {
            get => _gateAttackDurationMs;
            set
            {
                _gateAttackDurationMs = value;
                UpdateSetting("GateAttackDurationMs", value.ToString());
            }
        }

        private int _gateHoldDurationMs;
        public int GateHoldDurationMs
        {
            get => _gateHoldDurationMs;
            set
            {
                _gateHoldDurationMs = value;
                UpdateSetting("GateHoldDurationMs", value.ToString());
            }
        }

        private int _gateReleaseDurationMs;
        public int GateReleaseDurationMs
        {
            get => _gateReleaseDurationMs;
            set
            {
                _gateReleaseDurationMs = value;
                UpdateSetting("GateReleaseDurationMs", value.ToString());
            }
        }

        public PageSettingsViewModel(AudioCore core)
        {
            audioCore = core;

            DevicesInput = new ObservableCollection<string>(core.GetAudioDevicesInput());
            DevicesOutput = new ObservableCollection<string>(core.GetAudioDevicesOutput());
            DevicesInputSelection = Utility.ReadSetting("DevicesInputSelection");
            DevicesOutputSelection = Utility.ReadSetting("DevicesOutputSelection");
            
            // give reasonable defaults to selections if this is the first time user starts the software (i.e. no previous value)
            if (DevicesInputSelection == null || DevicesInputSelection == "")
            {
                DevicesInputSelection = core.GetDefaultAudioDeviceInput();
            }
            if (DevicesOutputSelection == null || DevicesOutputSelection == "")
            {
                foreach (var device in DevicesOutput)
                {
                    if (device.Contains("CABLE Input"))
                    {
                        DevicesOutputSelection = device;
                        break;
                    }
                }
            }

            StartWindowMinimized = Utility.StrToBool(Utility.ReadSetting("StartWindowMinimized"));
            VolumeBoost = Utility.StrToFloat(Utility.ReadSetting("VolumeBoost"));
            GateThreshold = Utility.StrToInt(Utility.ReadSetting("GateThreshold"));
            GateAttackDurationMs = Utility.StrToInt(Utility.ReadSetting("GateAttackDurationMs"));
            GateHoldDurationMs = Utility.StrToInt(Utility.ReadSetting("GateHoldDurationMs"));
            GateReleaseDurationMs = Utility.StrToInt(Utility.ReadSetting("GateReleaseDurationMs"));
        }

        /// <summary>
        /// Saves a setting and reports the change to View
        /// </summary>
        /// <returns>True if a setting did actually change</returns>
        private bool UpdateSetting(string key, string value)
        {
            var actuallyChanged = Utility.ReadSetting(key) != value;
            if (actuallyChanged)
            {
                Utility.SaveSetting(key, value);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(key));
            }
            return actuallyChanged;
        }

        /// <summary>
        /// Same as UpdateSetting but converts possible comma decimal separators to dots.
        /// </summary>
        /// <returns>True if a setting did actually change</returns>
        private bool UpdateSetting(string key, float value)
        {
            return UpdateSetting(key, value.ToString().Replace(",", "."));
        }


        public event PropertyChangedEventHandler PropertyChanged;

    }
}
