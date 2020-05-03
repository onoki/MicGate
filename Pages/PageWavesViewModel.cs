using MicGate.Processing;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;

namespace MicGate.Pages
{
    public class PageWavesViewModel : INotifyPropertyChanged, IDisposable
    {
        private AudioCore audioCore;

        private bool disposed;
        private readonly Timer drawTimer;
        private readonly Stopwatch drawWatch = new Stopwatch();

        // AudioCore buffers are as short as possible to keep delays down, 
        // but the plotted ones should be longer so that user has time to see them
        private readonly int PLOT_BUFFER_LENGTH_MS = 10000;
        private Queue<float> plottableAudioIn = new Queue<float>();
        private Queue<float> plottableAudioOut = new Queue<float>();
        private Queue<float> plottableIntegral = new Queue<float>();

        public bool PauseUpdating { get; set; } = false;

        private PlotModel _waveInPlot;
        public PlotModel WaveInPlot
        {
            get => _waveInPlot;
            private set
            {
                _waveInPlot = value;
                RaisePropertyChanged("WaveInPlot");
            }
        }

        private PlotModel _waveOutPlot;
        public PlotModel WaveOutPlot
        { 
            get => _waveOutPlot; 
            private set
            {
                _waveOutPlot = value;
                RaisePropertyChanged("WaveOutPlot");
            }
        }

        private float _bufferIntegral;
        public float BufferIntegral
        {
            get => _bufferIntegral;
            private set
            {
                _bufferIntegral = value;
                RaisePropertyChanged("BufferIntegral");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void RaisePropertyChanged(string property)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }

        public PageWavesViewModel(AudioCore core)
        {
            audioCore = core;

            drawTimer = new Timer(OnTimerElapsed);
            SetupPlots();
        }

        public void SetupPlots()
        {
            drawTimer.Change(Timeout.Infinite, Timeout.Infinite);

            var inPlot = new PlotModel();
            inPlot.Axes.Add(new LinearAxis { Position = AxisPosition.Left, IsAxisVisible = false });
            inPlot.Axes.Add(new LinearAxis { Position = AxisPosition.Right, Title = "Integral and threshold", Key = "integral" });
            inPlot.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, IsAxisVisible = false });
            inPlot.Series.Add(new LineSeries { LineStyle = LineStyle.Solid, Title = "Volume" });
            inPlot.Series.Add(new LineSeries { LineStyle = LineStyle.Dash, Color = OxyColors.CornflowerBlue, YAxisKey = "integral", Title = "Integral" });
            inPlot.Series.Add(new LineSeries { LineStyle = LineStyle.Dash, Color = OxyColors.IndianRed, YAxisKey = "integral", Title = "Threshold" });
            WaveInPlot = inPlot;

            var outPlot = new PlotModel();
            outPlot.Axes.Add(new LinearAxis { Position = AxisPosition.Left, IsAxisVisible = false });
            outPlot.Axes.Add(new LinearAxis { Position = AxisPosition.Right, IsAxisVisible = true, Title = " " });
            outPlot.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, IsAxisVisible = true, Title = "Time [s]" });
            outPlot.Series.Add(new LineSeries { LineStyle = LineStyle.Solid, Title = "Volume" });
            WaveOutPlot = outPlot;

            drawWatch.Start();

            drawTimer.Change(TimeSpan.FromMilliseconds(1000), 
                             TimeSpan.FromMilliseconds(Utility.StrToInt(Utility.ReadSetting("PlotUpdateIntervalMs"))));
        }

        private void OnTimerElapsed(object state)
        {
            if (!PauseUpdating)
            {
                lock (WaveInPlot.SyncRoot) lock (WaveOutPlot.SyncRoot)
                {
                    UpdatePlots();
                }

                WaveInPlot.InvalidatePlot(true);
                WaveOutPlot.InvalidatePlot(true);
            }
        }

        private float maxYIntegralPrev = 0;
        private void UpdatePlots()
        {
            // PreGate plot
            var seriesIn = (LineSeries)WaveInPlot.Series[0];
            seriesIn.Points.Clear();
            var plottableBufferSampleCount = Utility.TimeToSamples(audioCore.SampleRate, PLOT_BUFFER_LENGTH_MS);
            var preGateBuffer = audioCore.GetPreGateBufferForDrawing();
            lock (preGateBuffer)
            {
                while (preGateBuffer.Count > 0)
                {
                    var sample = preGateBuffer.Dequeue();
                    plottableAudioIn.Enqueue(sample);
                    if (plottableAudioIn.Count > plottableBufferSampleCount) plottableAudioIn.Dequeue();
                }
            }
            var x = 0f;
            var maxYBuffer = 0.25f; // default value here determines max zoom
            foreach (var y in plottableAudioIn)
            {
                maxYBuffer = Math.Max(maxYBuffer, Math.Abs(y));
                seriesIn.Points.Add(new DataPoint(x / audioCore.SampleRate, y));
                x++;
            }


            // PostGate plot
            var seriesOut = (LineSeries)WaveOutPlot.Series[0];
            seriesOut.Points.Clear();
            var postGateBuffer = audioCore.GetPostGateBufferForDrawing();
            lock (postGateBuffer)
            {
                while (postGateBuffer.Count > 0)
                {
                    var sample = postGateBuffer.Dequeue();
                    plottableAudioOut.Enqueue(sample);
                    if (plottableAudioOut.Count > plottableBufferSampleCount) plottableAudioOut.Dequeue();
                }
            }
            x = 0;
            foreach (var y in plottableAudioOut)
            {
                maxYBuffer = Math.Max(maxYBuffer, Math.Abs(y));
                seriesOut.Points.Add(new DataPoint(x / audioCore.SampleRate, y));
                x++;
            }


            // Integral plots
            var seriesIntegral = (LineSeries)WaveInPlot.Series[1];
            seriesIntegral.Points.Clear();
            var preGateIntegralBuffer = audioCore.GetPreGateIntegralForDrawing();
            lock (preGateIntegralBuffer)
            {
                while (preGateIntegralBuffer.Count > 0)
                {
                    var sample = preGateIntegralBuffer.Dequeue();
                    plottableIntegral.Enqueue(sample);
                    if (plottableIntegral.Count > plottableBufferSampleCount) plottableIntegral.Dequeue();
                }
            }
            x = 0;
            var maxYIntegral = 0f;
            foreach (var y in plottableIntegral)
            {
                maxYIntegral = Math.Max(maxYIntegral, y);
                seriesIntegral.Points.Add(new DataPoint(x / audioCore.SampleRate, y));
                x++;
            }


            // Threshold plot
            var seriesThreshold = (LineSeries)WaveInPlot.Series[2];
            seriesThreshold.Points.Clear();
            var threshold = Utility.StrToInt(Utility.ReadSetting("GateThreshold"));
            maxYIntegral = Math.Max(maxYIntegral, threshold);
            seriesThreshold.Points.Add(new DataPoint(0, threshold));
            x--; // threshold line should end the same X as integral line
            seriesThreshold.Points.Add(new DataPoint(x / audioCore.SampleRate, threshold));

            // zooming in on the right Y axis should be done gradually so that especially the threshold will not jump around too much
            var MAX_INTEGRAL_ZOOM_IN_PER_UPDATE = 10;
            if (maxYIntegralPrev > maxYIntegral - MAX_INTEGRAL_ZOOM_IN_PER_UPDATE) maxYIntegral = maxYIntegralPrev - (maxYIntegralPrev - maxYIntegral) / 2;
            maxYIntegralPrev = maxYIntegral;

            // scale both plots the same way
            WaveInPlot.Axes[0].Maximum = maxYBuffer;
            WaveInPlot.Axes[0].Minimum = -maxYBuffer;
            WaveOutPlot.Axes[0].Maximum = maxYBuffer;
            WaveOutPlot.Axes[0].Minimum = -maxYBuffer;
            WaveInPlot.Axes[1].Maximum = maxYIntegral + 2; // max needs to be a bit higher than max value so threshold is always visible
            WaveInPlot.Axes[1].Minimum = -2; // min needs to be a bit lower than actual min so integral is always visible
            WaveOutPlot.Axes[1].Maximum = WaveInPlot.Axes[1].Maximum;
            WaveOutPlot.Axes[1].Minimum = WaveInPlot.Axes[1].Minimum;
        }

        //public void ResetPlotYAxes() => audioCore.MaxScaledAmplitude = 0;

        #region " Disposing "

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    drawTimer.Dispose();
                }
            }

            disposed = true;
        }

        #endregion
    }
}
