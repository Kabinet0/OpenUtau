using OpenUtau.Core.Util;
using Melanchall.DryWetMidi.Multimedia;
using System.Collections.Generic;
using System.Linq;
using System;
using Serilog;

namespace OpenUtau.Core.Recording {
    public class MidiDeviceManager : SingletonBase<MidiDeviceManager> {
        // InputDevice instances used outside this class should never be stored since these instances are volatile and change when midi devices are reloaded
        private List<InputDevice> midiDevices = new List<InputDevice>();

        private int selectedDeviceIndex = -1;

        public void Initialize() {
            RefreshMidiDevices();
        }

        public InputDevice? GetSelectedDevice() {
            if (selectedDeviceIndex < 0 || selectedDeviceIndex >= midiDevices.Count) {
                return null;
            }
            return midiDevices[selectedDeviceIndex];
        }

        public int? GetSelectedDeviceIndex() {
            if (selectedDeviceIndex < 0 || selectedDeviceIndex >= midiDevices.Count) {
                return null;
            }
            return selectedDeviceIndex;
        }

        private void SetLastDeviceName(string name) {
            Preferences.Default.LastSelectedMidiDevice = name;
            Preferences.Save();
        }

        public List<string> GetDeviceNameList() {
            List<string> deviceList = new List<string>();
            foreach (var device in midiDevices) {
                deviceList.Add(device.Name);
            }
            return deviceList;
        }

        public void SetSelectedDeviceID(int deviceID) {
            if (deviceID < 0 || deviceID >= midiDevices.Count) {
                throw new IndexOutOfRangeException();
            }

            selectedDeviceIndex = deviceID;
            SetLastDeviceName(midiDevices[selectedDeviceIndex].Name);

            Log.Information("Set selected ID to " + deviceID);
            Log.Information("Device name is now " + Preferences.Default.LastSelectedMidiDevice);
        }

        public void RefreshMidiDevices() {
            foreach (var device in midiDevices) {
                // All instances must be manually disposed of manually before creating or using new instances
                // See: https://melanchall.github.io/drywetmidi/articles/devices/Input-device.html
                device.Dispose();
            }
            midiDevices = InputDevice.GetAll().ToList();

            // Resolve selected MIDI device

            // By default nothing is selected
            selectedDeviceIndex = -1;

            // Try looking for last used device
            for (int i = 0; i < midiDevices.Count; i++) {
                if (midiDevices[i].Name == Preferences.Default.LastSelectedMidiDevice) {
                    selectedDeviceIndex = i;
                    break;
                }
            }

            Log.Information("Within reload last device name is " + Preferences.Default.LastSelectedMidiDevice);

            var selectedDevice = GetSelectedDevice();
            if (selectedDevice != null) {
                SetLastDeviceName(selectedDevice.Name);
            }

            
        }
    }
}
