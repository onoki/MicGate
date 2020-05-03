using NAudio.Wave;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace MicGate
{
    public class GatedSampleProvider : ISampleProvider
    {
        private readonly int WORST_CASE_QUEUE_TIME_SECONDS = 5;
        private int maxQueueSize = 0;

        private readonly ISampleProvider source;

        private float bufferIntegral = 0;
        public bool GateIsOpen { get; set; } = false;
        private int gateOpenedNSamplesAgo = 0;
        private int gateOpenForNSamples = 0;

        public float Volume { get; set; } = 1.0f;
        public Queue<float> PreGateBufferForCalculations { get; set; } = new Queue<float>();
        public Queue<float> PreGateBufferForDrawing { get; set; } = new Queue<float>();
        public Queue<float> PostGateBufferForDrawing { get; set; } = new Queue<float>();
        public Queue<float> PreGateIntegralForDrawing { get; set; } = new Queue<float>();

        public float MaxScaledSampleAmplitude { get; set; } = 0;


        /// <summary>
        /// Initializes a new instance of VolumeSampleProvider
        /// </summary>
        /// <param name="source">Source Sample Provider</param>
        public GatedSampleProvider(ISampleProvider sampleProvider)
        {
            source = sampleProvider;

            // any queue should not be longer than this number of seconds to prevent them growing indefinitely
            maxQueueSize = source.WaveFormat.SampleRate * WORST_CASE_QUEUE_TIME_SECONDS;
        }

        /// <summary>
        /// WaveFormat
        /// </summary>
        public WaveFormat WaveFormat => source.WaveFormat;

        /// <summary>
        /// Reads samples from this sample provider
        /// </summary>
        /// <param name="buffer">Sample buffer</param>
        /// <param name="offset">Offset into sample buffer</param>
        /// <param name="sampleCount">Number of samples desired</param>
        /// <returns>Number of samples read</returns>
        public int Read(float[] buffer, int offset, int sampleCount)
        {
            int samplesRead = source.Read(buffer, offset, sampleCount);

            var gateAttackDurationMs = Utility.StrToInt(Utility.ReadSetting("GateAttackDurationMs"));
            var gateHoldDurationMs = Utility.StrToInt(Utility.ReadSetting("GateHoldDurationMs"));
            var gateReleaseDurationMs = Utility.StrToInt(Utility.ReadSetting("GateReleaseDurationMs"));

            var gateAttackDurationSamples = Utility.TimeToSamples(source.WaveFormat.SampleRate, gateAttackDurationMs);
            var gateHoldDurationSamples = Utility.TimeToSamples(source.WaveFormat.SampleRate, gateHoldDurationMs);
            var gateReleaseDurationSamples = Utility.TimeToSamples(source.WaveFormat.SampleRate, gateReleaseDurationMs);

            var gateThreshold = Utility.StrToInt(Utility.ReadSetting("GateThreshold"));

            for (int n = 0; n < sampleCount; n++)
            {
                // collect everything to the unfiltered buffer
                // in order to have input and output plots scaled the same, apply volume scaling already when adding samples to pregatequeue
                // however when calculating buffer integral, it must be done without scaling
                var volumeModifiedSample = buffer[offset + n] * Volume;
                buffer[offset + n] = volumeModifiedSample;
                PreGateBufferForCalculations.Enqueue(volumeModifiedSample);
                lock (PreGateBufferForDrawing)
                {
                    PreGateBufferForDrawing.Enqueue(volumeModifiedSample);
                }
                bufferIntegral += Math.Abs(volumeModifiedSample / Volume);
                lock (PreGateIntegralForDrawing)
                {
                    PreGateIntegralForDrawing.Enqueue(bufferIntegral);
                }
                MaxScaledSampleAmplitude = volumeModifiedSample > MaxScaledSampleAmplitude ? volumeModifiedSample : MaxScaledSampleAmplitude;

                if (PreGateBufferForCalculations.Count > gateAttackDurationSamples)
                {
                    var dequeuedSample = PreGateBufferForCalculations.Dequeue();
                    bufferIntegral -= Math.Abs(dequeuedSample / Volume);
                }
                
                // check whether the latest sample opened or closed the gate
                gateOpenForNSamples--;
                gateOpenedNSamplesAgo++;
                if (bufferIntegral > gateThreshold)
                {
                    if (!GateIsOpen)
                    {
                        gateOpenedNSamplesAgo = 0;
                    }
                    GateIsOpen = true;
                    gateOpenForNSamples = gateAttackDurationSamples + gateHoldDurationSamples + gateReleaseDurationSamples;
                }
                else if (gateOpenForNSamples <= 0)
                {
                    GateIsOpen = false;
                    gateOpenForNSamples = 0;
                    gateOpenedNSamplesAgo = 0;
                }


                /*
                 * OUTPUT HANDLING
                 */

                var isAttack = gateOpenedNSamplesAgo < gateAttackDurationSamples;
                var isRelease = gateOpenForNSamples < gateReleaseDurationSamples;

                if (GateIsOpen)
                {
                    if (isAttack)
                    {
                        var progress = 1.0f * gateOpenedNSamplesAgo / gateAttackDurationSamples;
                        buffer[offset + n] = buffer[offset + n] * progress;
                    }
                    else if (isRelease)
                    {
                        var progress = 1.0f * (gateReleaseDurationSamples - gateOpenForNSamples) / gateReleaseDurationSamples;
                        buffer[offset + n] = buffer[offset + n] * progress;
                    }
                    else // isHold
                    {
                        buffer[offset + n] = buffer[offset + n];
                    }
                }
                else
                {
                    buffer[offset + n] = 0;
                }

                lock (PostGateBufferForDrawing)
                {
                    PostGateBufferForDrawing.Enqueue(buffer[offset + n]);
                }

                // none of the queues should grow indefinitely. the limit checked here is so large that any other 
                // dequeues above have happened already if they are going to happen 
                if (PreGateBufferForCalculations.Count > maxQueueSize) PreGateBufferForCalculations.Dequeue();
                if (PostGateBufferForDrawing.Count > maxQueueSize) PostGateBufferForDrawing.Dequeue();
                if (PreGateBufferForDrawing.Count > maxQueueSize) PreGateBufferForDrawing.Dequeue();
                if (PreGateIntegralForDrawing.Count > maxQueueSize) PreGateIntegralForDrawing.Dequeue();
            }

            return samplesRead;
        }
    }
}
