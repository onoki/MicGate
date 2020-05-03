using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MicGate.Processing
{
    public class AudioCore
    {
        private WasapiCapture realMic;
        private BufferedWaveProvider bufferedWaveProvider;
        private GatedSampleProvider gatedSampleProvider;
        private WaveOut virtualMic;

        private AutoResetEvent audioResetIsAllowed = new AutoResetEvent(false);

        public AudioCore()
        {
            StartRecording();
        }

        #region " Recording main flow "

        private void StartRecording()
        {
            // set up recording from real mic
            var devicesInput = GetAudioDevicesInput();
            var selectedDeviceNameInput = Utility.ReadSetting("DevicesInputSelection");
            var selectedDeviceNumberInput = devicesInput.IndexOf(selectedDeviceNameInput); // Wasapi device indexing starts from 0
            if (selectedDeviceNumberInput < 0) selectedDeviceNumberInput = 0;
            var mmDevices = new MMDeviceEnumerator().EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
            var mmdMic = mmDevices.FirstOrDefault(x => x.FriendlyName == devicesInput[selectedDeviceNumberInput]);
            realMic = new WasapiCapture(mmdMic);
            realMic.ShareMode = AudioClientShareMode.Shared;
            realMic.WaveFormat = new WaveFormat();
            realMic.DataAvailable += RecorderOnDataAvailable;
            realMic.RecordingStopped += RecorderOnDataEnds;
            try
            {
                realMic.StartRecording();
            }
            catch (System.Runtime.InteropServices.COMException e)
            {
                // initializing some input devices fails because of e.g. improper sample rate.
                // if this becomes a problem, something could be done to fix it. low priority as of now.
                Console.WriteLine($"Real mic initialization failed: {e}");
            }

            // set up our signal chain
            bufferedWaveProvider = new BufferedWaveProvider(realMic.WaveFormat);
            bufferedWaveProvider.DiscardOnBufferOverflow = true;
            gatedSampleProvider = new GatedSampleProvider(bufferedWaveProvider.ToSampleProvider());
            gatedSampleProvider.Volume = Utility.StrToFloat(Utility.ReadSetting("VolumeBoost"));

            // set up playback to virtual mic
            var devicesOutput = GetAudioDevicesOutput();
            var selectedDeviceNameOutput = Utility.ReadSetting("DevicesOutputSelection");
            var selectedDeviceNumberOutput = devicesOutput.IndexOf(selectedDeviceNameOutput) - 1; // WaveOut device indexing starts from -1
            if (selectedDeviceNumberOutput < -1) selectedDeviceNumberOutput = -1;
            virtualMic = new WaveOut() { DeviceNumber = selectedDeviceNumberOutput };
            virtualMic.Init(gatedSampleProvider);
            try
            {
                virtualMic.Play();
            }
            catch (System.NullReferenceException e)
            {
                // if the config file is scuffed, sometimes the same audio is started twice and the second time will fail.
                // this is actually kind of a big problem because closing the software from systray actually leaves a thread running
                // however this happens rarely, fix it if it becomes a problem. low priority as of now.
                Console.WriteLine($"Virtual mic initialization failed: {e}");
            }

            // saving selections here is not really needed, but if it is not done, Settings window restarts audio twice the first time it is accessed (after installation)
            // i.e. just for user convenience, save the selected audio devices
            if (selectedDeviceNameInput == "") Utility.SaveSetting("DevicesInputSelection", devicesInput[selectedDeviceNumberInput]); // no +1 because of Wasapi device indexing
            if (selectedDeviceNameOutput == "") Utility.SaveSetting("DevicesOutputSelection", devicesOutput[selectedDeviceNumberOutput + 1]); // +1 because of WaveOut device indexing

            // when StartRecording has finished, the audio is allowed to be restarted
            audioResetIsAllowed.Set();
        }

        private void RecorderOnDataAvailable(object sender, WaveInEventArgs waveInEventArgs)
        {
            var ignoreSamplesWithAmplitude = Utility.StrToFloat(Utility.ReadSetting("IgnoreSamplesWithAmplitudeBelow"));
            var ignoreAllSamples = true;
            for (var i = 0; i < waveInEventArgs.BytesRecorded; i += 4)
            {
                var sampleAmplitude = Math.Abs(BitConverter.ToSingle(waveInEventArgs.Buffer, i));
                if (gatedSampleProvider.GateIsOpen || sampleAmplitude > ignoreSamplesWithAmplitude) ignoreAllSamples = false;
            }

            // if this buffer contains only ignorable samples, skip it altogether to prevent little-by-little increase of unprocessed buffer
            if (!ignoreAllSamples)
            {
                bufferedWaveProvider.AddSamples(waveInEventArgs.Buffer, 0, waveInEventArgs.BytesRecorded);
            }
        }

        private void RecorderOnDataEnds(object sender, StoppedEventArgs stoppedEventArgs)
        {
            if (realMic != null)
            {
                realMic.Dispose();
                realMic = null;
            }
        }

        public void Shutdown()
        {
            // stop recording
            if (realMic != null)
            {
                realMic.StopRecording();
            }

            // stop playback
            if (virtualMic != null)
            {
                virtualMic.Stop();
            }

            var realMicStopped = false;
            var virtualMicStopped = false;
            var stoppingStarted = DateTime.Now;
            while ((!realMicStopped || !virtualMicStopped) && DateTime.Now - stoppingStarted < TimeSpan.FromSeconds(10))
            {
                realMicStopped = realMic == null || realMic.CaptureState == CaptureState.Stopped;
                virtualMicStopped = virtualMic == null || virtualMic.PlaybackState == PlaybackState.Stopped;
            }
        }

        public void RestartRecordingAndPlayback()
        {
            // wait for permission to reset audio
            // this is released (set) in the end of StartRecording
            audioResetIsAllowed.WaitOne();
            
            Shutdown();

            // if recording is started immediately, sometimes it does not start.
            // a fixed delay does not guarantee it starts, but no idea how to find out the device is ready to start again
            var RESTART_DELAY_MS = 1000;
            Task.Delay(RESTART_DELAY_MS).ContinueWith(t => StartRecording());
        }

        #endregion

        #region " General getters, information, etc "

        public List<string> GetAudioDevicesInput()
        {
            var deviceEnumerator = new MMDeviceEnumerator();
            var fullDeviceNames = deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).Select(x => x.FriendlyName);
            return new List<string>(fullDeviceNames);
        }

        public List<string> GetAudioDevicesOutput()
        {
            var deviceEnumerator = new MMDeviceEnumerator();
            var fullDeviceNames = deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).Select(x => x.FriendlyName);
            
            var outputNames = new List<string>();
            for (int n = -1; n < WaveOut.DeviceCount; n++)
            {
                var limitedDeviceName = WaveOut.GetCapabilities(n).ProductName;
                var fullDeviceName = fullDeviceNames.FirstOrDefault(x => x.StartsWith(limitedDeviceName));
                outputNames.Add(fullDeviceName ?? limitedDeviceName);
            }
            return outputNames;
        }

        public string GetDefaultAudioDeviceInput()
        {
            var deviceEnumerator = new MMDeviceEnumerator();
            var defaultDeviceInput = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
            return defaultDeviceInput.FriendlyName;
        }

        public string GetDefaultAudioDeviceOutput()
        {
            var deviceEnumerator = new MMDeviceEnumerator();
            var defaultDeviceInput = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Communications);
            return defaultDeviceInput.FriendlyName;
        }

        public Queue<float> GetPreGateBufferForDrawing() => gatedSampleProvider.PreGateBufferForDrawing;
        public Queue<float> GetPostGateBufferForDrawing() => gatedSampleProvider.PostGateBufferForDrawing;
        public Queue<float> GetPreGateIntegralForDrawing() => gatedSampleProvider.PreGateIntegralForDrawing;


        public int SampleRate { get => bufferedWaveProvider.WaveFormat.SampleRate; private set => _ = value; }

        #endregion

    }
}
