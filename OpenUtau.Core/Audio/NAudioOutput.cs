using System;
using System.Collections.Generic;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using OpenUtau.Core.Util;

namespace OpenUtau.Audio {
#if !WINDOWS
    public class NAudioOutput : DummyAudioOutput { }
#else
    public class NAudioOutput : IAudioOutput {
        const int Channels = 2;

        private readonly object lockObj = new object();
        private WasapiOut wasapiOut;
        private string selectedDeviceID;

        public NAudioOutput() {
            SelectDevice(Preferences.Default.PlaybackDevice, Preferences.Default.PlaybackDeviceNumber);
        }

        public PlaybackState PlaybackState {
            get {
                lock (lockObj) {
                    return wasapiOut == null ? PlaybackState.Stopped : wasapiOut.PlaybackState;
                }
            }
        }

        private int deviceNumber;
        public int DeviceNumber => deviceNumber;

        public long GetPosition() {
            lock (lockObj) {
                return wasapiOut == null
                    ? 0
                    : wasapiOut.GetPosition() / Channels;
            }
        }

        public void Init(ISampleProvider sampleProvider) {
            lock (lockObj) {
                if (wasapiOut != null) {
                    wasapiOut.Stop();
                    wasapiOut.Dispose();
                }

                var enumerator = new MMDeviceEnumerator();

                MixingSampleProvider a;
                try {
                    wasapiOut = new WasapiOut(
                        enumerator.GetDevice(selectedDeviceID),
                        AudioClientShareMode.Shared,
                        true,
                        Math.Max((int)((Preferences.Default.AudioBufferSize / 44100.0f) * 1000), 30)
                    );
                    wasapiOut.Init(sampleProvider);

                } catch (System.Exception) {
                    // Handle the case where the saved device is not available
                    throw new Exception("Failed to access audio device");
                }
            }
        }

        public void Pause() {
            lock (lockObj) {
                if (wasapiOut != null) {
                    wasapiOut.Pause();
                }
            }
        }

        public void Play() {
            lock (lockObj) {
                if (wasapiOut != null) {
                    wasapiOut.Play();
                }
            }
        }

        public void Stop() {
            lock (lockObj) {
                if (wasapiOut != null) {
                    wasapiOut.Stop();
                    wasapiOut.Dispose();
                    wasapiOut = null;
                }
            }
        }

        public void SelectDevice(string id, int deviceNumber) {
            Preferences.Default.PlaybackDevice = id;
            Preferences.Default.PlaybackDeviceNumber = deviceNumber; // Device number is irrelevant with wasapi, still used for ordering
            Preferences.Save();

            this.deviceNumber = deviceNumber;
            selectedDeviceID = id;

            var enumerator = new MMDeviceEnumerator();

            try {
                enumerator.GetDevice(id);
            } catch (System.Exception) {
                // Handle the case where the saved device is not available
                selectedDeviceID = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console).ID;
            }
        }

        public List<AudioOutputDevice> GetOutputDevices() {
            var outDevices = new List<AudioOutputDevice>();
            var enumerator = new MMDeviceEnumerator();

            int i = 0;
            foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)) {
                outDevices.Add(new AudioOutputDevice {
                    api = "Wasapi",
                    name = device.FriendlyName,
                    deviceNumber = i, // Numerical device IDs are irrelevant with wasapi
                    wasapiEndpointID = device.ID,
                });
                i++;
            }

            return outDevices;
        }
    }
#endif
}
