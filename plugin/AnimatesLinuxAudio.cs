// Animates Linux Audio Bridge 0.2.0 — experimental, reversible BepInEx plugin.
// Built against the user's local Unity/Animates assemblies; does not patch the originals.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace AnimatesLinuxAudio
{
    [BepInPlugin("linuxideas.animates.linuxaudio", "Animates Linux Audio Bridge", "0.2.0")]
    public sealed class LinuxAudioPlugin : BaseUnityPlugin
    {
        private const int Port = 47883;
        private const int SampleRate = 48000;
        private const float AudibleRms = 0.0015f;
        private const double HoldSeconds = 1.6;
        private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        private BridgeAudioCaptureSource _bridge;
        private Thread _reader;
        private volatile bool _stopping;
        private volatile bool _connected;
        private long _lastAudibleTimestamp;
        private bool _boundOnce;
        private string _lastError = "none";

        // Option B: suppress the dance flag at the animation decision point,
        // without interrupting music PCM, BPM analysis, microphone or TTS.
        private static LinuxAudioPlugin _active;
        private Harmony _speechHarmony;
        private ConfigEntry<bool> _suppressSpeech;
        private ConfigEntry<float> _speechCooldown;
        private readonly Dictionary<int, float> _resumeAfter = new Dictionary<int, float>();
        private readonly HashSet<int> _suppressedCharacters = new HashSet<int>();
        private bool _speechHookFailedLogged;


        private void Awake()
        {
            _active = this;
            _suppressSpeech = Config.Bind("Speech", "SuppressDanceWhileSpeaking", true,
                "Pause character dancing during Animates' own speech, while music capture continues.");
            _speechCooldown = Config.Bind("Speech", "ResumeDelaySeconds", 2.0f,
                "Wait after character speech ends before permitting a dance restart (0 to 10 seconds).");
            InstallSpeechHook();
            _bridge = new BridgeAudioCaptureSource();
            // Factories are used if the game constructs its controllers after this plugin starts.
            ActivityPlatform.AudioCaptureSourceFactory = delegate { return _bridge; };
            ActivityPlatform.DetectorFactory = delegate { return new LinuxDetector(new ActivityDetector(), this); };
            _reader = new Thread(ReadLoop);
            _reader.IsBackground = true;
            _reader.Name = "AnimatesLinuxPipeWireReader";
            _reader.Start();
            Logger.LogInfo("[LinuxAudio] Plugin loaded. TCP=127.0.0.1:" + Port +
                           " FeatureFlag=" + FeatureFlags.ActivityAnimationsEnabled +
                           " (unchanged). Waiting for Linux relay.");
        }

        private IEnumerator Start()
        {
            // Factories may be installed after a scene's Awake, so bind live controllers as well.
            int checks = 0;
            while (!_stopping)
            {
                try { BindLiveControllers(); }
                catch (Exception ex) { Logger.LogWarning("[LinuxAudio] Bind failed: " + ex); }
                if (++checks % 4 == 0)
                {
                    Logger.LogInfo("[LinuxAudio] State: connected=" + _connected +
                        " audible=" + IsAudible() +
                        " capturing=" + _bridge.IsCapturing +
                        " featureFlag=" + FeatureFlags.ActivityAnimationsEnabled);
                }
                yield return new WaitForSecondsRealtime(3f);
            }
        }

        private void InstallSpeechHook()
        {
            try
            {
                // UpdateData is the exact point where the game consumes musicPlaying.
                // A prefix avoids frame-order races between the two Unity controllers.
                MethodInfo target = typeof(CharacterDanceController).GetMethod(
                    "UpdateData", BindingFlags.Static | BindingFlags.Public,
                    null, new Type[] { typeof(CharacterData) }, null);
                MethodInfo prefix = typeof(LinuxAudioPlugin).GetMethod(
                    "DanceUpdatePrefix", BindingFlags.Static | BindingFlags.NonPublic);
                if (target == null || prefix == null)
                    throw new MissingMethodException("CharacterDanceController.UpdateData/patch prefix");
                _speechHarmony = new Harmony("linuxideas.animates.linuxaudio.speech");
                _speechHarmony.Patch(target, new HarmonyMethod(prefix));
                Logger.LogInfo("[LinuxAudio] Speech-aware dance suppression installed. Enabled=" +
                    _suppressSpeech.Value + " cooldown=" + _speechCooldown.Value + "s");
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[LinuxAudio] Speech hook unavailable; music bridge remains active: " + ex);
            }
        }

        // Called by Harmony on the main thread immediately before the game's dance decision.
        private static void DanceUpdatePrefix(CharacterData characterData)
        {
            LinuxAudioPlugin plugin = _active;
            if (plugin == null || plugin._stopping || !plugin._suppressSpeech.Value) return;
            try
            {
                plugin.ApplySpeechGate(characterData);
            }
            catch (Exception ex)
            {
                // Never allow our optional feature to interrupt animation updates.
                if (!plugin._speechHookFailedLogged)
                {
                    plugin._speechHookFailedLogged = true;
                    plugin.Logger.LogWarning("[LinuxAudio] Speech gate disabled after unexpected error: " + ex);
                }
                plugin._suppressSpeech.Value = false;
            }
        }

        private void ApplySpeechGate(CharacterData character)
        {
            if (character == null || character.danceAnimations == null) return;
            int id = character.GetInstanceID();
            float now = Time.realtimeSinceStartup;
            bool speaking = character.isSpeaking;
            float until;
            if (speaking)
            {
                float cooldown = Mathf.Clamp(_speechCooldown.Value, 0f, 10f);
                _resumeAfter[id] = now + cooldown;
            }
            bool suppressed = speaking || (_resumeAfter.TryGetValue(id, out until) && now < until);
            if (suppressed)
            {
                // Preserve the audio stream, capture PID and beat detector. Only
                // suppress the boolean consumed by CharacterDanceController.UpdateData.
                character.danceAnimations.musicPlaying = false;
                if (_suppressedCharacters.Add(id))
                    Logger.LogInfo("[LinuxAudio] Speech gate active: dance suspended (audio still capturing).");
            }
            else
            {
                _resumeAfter.Remove(id);
                if (_suppressedCharacters.Remove(id))
                    Logger.LogInfo("[LinuxAudio] Speech gate released: dance may resume if music is audible.");
            }
        }

        private bool IsAudible()
        {
            if (!_connected) return false;
            long last = Interlocked.Read(ref _lastAudibleTimestamp);
            if (last == 0) return false;
            double seconds = (Stopwatch.GetTimestamp() - last) / (double)Stopwatch.Frequency;
            return seconds >= 0 && seconds < HoldSeconds;
        }

        private void BindLiveControllers()
        {
            ActivityAnimationController[] all = Resources.FindObjectsOfTypeAll<ActivityAnimationController>();
            foreach (ActivityAnimationController controller in all)
            {
                if (controller == null || controller.gameObject == null ||
                    !controller.gameObject.scene.IsValid() || !controller.gameObject.scene.isLoaded ||
                    !controller.isActiveAndEnabled)
                    continue;

                FieldInfo detectorField = typeof(ActivityAnimationController).GetField("detector", PrivateInstance);
                FieldInfo managerField = typeof(ActivityAnimationController).GetField("musicCaptureManager", PrivateInstance);
                FieldInfo sourceField = typeof(MusicCaptureManager).GetField("_audioCapture", PrivateInstance);
                FieldInfo danceManagerField = typeof(SpotifyDanceController).GetField("captureManager", PrivateInstance);
                if (detectorField == null || managerField == null || sourceField == null || danceManagerField == null)
                    throw new MissingFieldException("Expected Animates activity fields are absent.");

                IActivityDetector originalDetector = (IActivityDetector)detectorField.GetValue(controller);
                MusicCaptureManager oldManager = (MusicCaptureManager)managerField.GetValue(controller);
                SpotifyDanceController dance = controller.danceController;
                if (originalDetector == null || oldManager == null || dance == null)
                    continue; // The application's Awake has not finished.

                if (!(originalDetector is LinuxDetector))
                {
                    detectorField.SetValue(controller, new LinuxDetector(originalDetector, this));
                    Logger.LogInfo("[LinuxAudio] Wrapped existing activity detector.");
                }

                if (!object.ReferenceEquals(sourceField.GetValue(oldManager), _bridge))
                {
                    MusicCaptureManager replacement = new MusicCaptureManager(_bridge);
                    managerField.SetValue(controller, replacement);
                    danceManagerField.SetValue(dance, replacement);
                    // Only dispose once the controller and dance component point to the replacement.
                    oldManager.Dispose();
                    Logger.LogInfo("[LinuxAudio] Replaced SpotifyAudioCapture with built-in BridgeAudioCaptureSource.");
                }

                if (!_boundOnce)
                {
                    _boundOnce = true;
                    Logger.LogInfo("[LinuxAudio] Bound live controller; featureFlag=" +
                                   FeatureFlags.ActivityAnimationsEnabled + ".");
                }
            }
        }

        private void ReadLoop()
        {
            while (!_stopping)
            {
                try
                {
                    using (TcpClient client = new TcpClient())
                    {
                        client.Connect(IPAddress.Loopback, Port);
                        client.NoDelay = true;
                        client.ReceiveTimeout = 4000;
                        _connected = true;
                        Logger.LogInfo("[LinuxAudio] Connected to PipeWire relay.");
                        using (NetworkStream stream = client.GetStream())
                        {
                            byte[] bytes = new byte[8192];
                            int pending = 0;
                            while (!_stopping)
                            {
                                int n = stream.Read(bytes, pending, bytes.Length - pending);
                                if (n <= 0) break;
                                int total = pending + n;
                                int complete = total - (total % 4); // float32 samples are 4 bytes
                                if (complete > 0)
                                {
                                    float[] samples = new float[complete / 4];
                                    Buffer.BlockCopy(bytes, 0, samples, 0, complete);
                                    double sum = 0;
                                    for (int i = 0; i < samples.Length; ++i)
                                        sum += (double)samples[i] * samples[i];
                                    double rms = Math.Sqrt(sum / samples.Length);
                                    if (rms > AudibleRms)
                                        Interlocked.Exchange(ref _lastAudibleTimestamp, Stopwatch.GetTimestamp());
                                    _bridge.SubmitChunk(samples, SampleRate);
                                }
                                pending = total - complete;
                                if (pending > 0)
                                    Buffer.BlockCopy(bytes, complete, bytes, 0, pending);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Expected when the relay is not yet running; avoid per-second log spam.
                    if (_lastError != ex.GetType().Name + ": " + ex.Message)
                    {
                        _lastError = ex.GetType().Name + ": " + ex.Message;
                        Logger.LogWarning("[LinuxAudio] Relay unavailable: " + _lastError);
                    }
                }
                finally
                {
                    if (_connected) Logger.LogInfo("[LinuxAudio] Relay disconnected.");
                    _connected = false;
                    Interlocked.Exchange(ref _lastAudibleTimestamp, 0);
                }
                if (!_stopping) Thread.Sleep(2000);
            }
        }

        private void OnDestroy()
        {
            _stopping = true;
            _connected = false;
            if (_speechHarmony != null)
            {
                try { _speechHarmony.UnpatchSelf(); }
                catch (Exception ex) { Logger.LogWarning("[LinuxAudio] Unpatch failed: " + ex.Message); }
            }
            if (object.ReferenceEquals(_active, this)) _active = null;
            // The worker is a background thread and exits on stream closure/retry.
            // Leave the game's own component teardown to its original lifecycle.
        }

        private sealed class LinuxDetector : IActivityDetector
        {
            private readonly IActivityDetector _original;
            private readonly LinuxAudioPlugin _owner;
            internal LinuxDetector(IActivityDetector original, LinuxAudioPlugin owner)
            {
                _original = original;
                _owner = owner;
            }
            public DetectionContext Detect()
            {
                DetectionContext context = _original.Detect();
                // Synthetic positive PID is only an activation token. Bridge ignores the PID.
                if (_owner.IsAudible()) context.musicAppPid = 1;
                return context;
            }
            public void Dispose() { _original.Dispose(); }
        }
    }
}