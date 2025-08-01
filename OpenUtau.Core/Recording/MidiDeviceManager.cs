using OpenUtau.Core.Util;
using Melanchall.DryWetMidi.Multimedia;
using Melanchall.DryWetMidi.Core;
using System.Collections.Generic;
using System.Linq;
using System;
using Serilog;

namespace OpenUtau.Core.Recording {
    public class MidiDeviceManager : SingletonBase<MidiDeviceManager> {
        // InputDevice instances used outside this class should never be stored since these instances are volatile and change when midi devices are reloaded
        private List<InputDevice> midiDevices = new List<InputDevice>();

        private int selectedDeviceIndex = -1;

        public event EventHandler<int> MidiNoteOnEvent;
        public event EventHandler<int> MidiNoteOffEvent;

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
            ClearMidiEventHandling();

            selectedDeviceIndex = deviceID;
            SetLastDeviceName(midiDevices[selectedDeviceIndex].Name);

            SetupMidiEventHandling();

            Log.Information("Set selected MIDI device to: " + Preferences.Default.LastSelectedMidiDevice + " (ID " + deviceID + ")");
        }

        public void RefreshMidiDevices() {
            ClearMidiEventHandling();
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

            
            var selectedDevice = GetSelectedDevice();
            if (selectedDevice != null) {
                SetLastDeviceName(selectedDevice.Name);
                Log.Information("Last MIDI device: [" + Preferences.Default.LastSelectedMidiDevice + "] was selected after reload.");
            }

            SetupMidiEventHandling();
        }

        private void SetupMidiEventHandling() {
            var selectedDevice = GetSelectedDevice();

            if (selectedDevice != null) {
                selectedDevice.EventReceived += HandleMidiEvent;
                selectedDevice.StartEventsListening();
                selectedDevice.SilentNoteOnPolicy = SilentNoteOnPolicy.NoteOff;

                //var outputDevice = OutputDevice.GetByName("Microsoft GS Wavetable Synth");
                //var devicesConnector = selectedDevice.Connect(outputDevice);
            }
        }

        private void ClearMidiEventHandling() {
            var selectedDevice = GetSelectedDevice();

            if (selectedDevice != null) {
                selectedDevice.EventReceived -= HandleMidiEvent;

                if (selectedDevice.IsEnabled) {
                    try {
                        selectedDevice.StopEventsListening();
                    } catch (Exception e) {
                        Log.Information("Error unsubscribing from MIDI device: " + e.Message);
                    }
                }

            }
        }

        private void HandleMidiEvent(object sender, MidiEventReceivedEventArgs args) {
            var eventType = args.Event.EventType;

            // Could expand later to handle more events like starting recording from MIDI keyboard
            switch (eventType) {
                case MidiEventType.NoteOn:
                    NoteOnEvent noteOnEvent = (NoteOnEvent)args.Event;
                    
                    Log.Information("NoteOn: " + args.Event.DeltaTime + ", note: " + noteOnEvent.NoteNumber);
                    MidiNoteOnEvent?.Invoke(this, noteOnEvent.NoteNumber);

                    break;
                case MidiEventType.NoteOff:
                    NoteOffEvent noteOffEvent = (NoteOffEvent)args.Event;

                    Log.Information("NoteOff: " + args.Event.DeltaTime + ", note: " + noteOffEvent.NoteNumber);
                    MidiNoteOffEvent?.Invoke(this, noteOffEvent.NoteNumber);
                    break;
            }
             
        }
    }
}
