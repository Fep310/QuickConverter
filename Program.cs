using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using FFMpegCore;
using FFMpegCore.Arguments;
using FFMpegCore.Enums;
using ImGuiNET;
using NativeFileDialogNET;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

namespace QuickConverter;

class Program
{
    private static Sdl2Window _window;
    private static GraphicsDevice _gd;
    private static CommandList _cl;
    private static ImGuiController _controller;
    
    private static Vector3 _clearColor = new(0, 0, 0);

    static void Main(string[] args)
    {
        // Create window, GraphicsDevice, and all resources necessary for the demo.
        VeldridStartup.CreateWindowAndGraphicsDevice(
            new WindowCreateInfo(1920/3, 1080/3, 900, 600, WindowState.Normal, "QuickConverter"),
            new GraphicsDeviceOptions(true, null, true, ResourceBindingModel.Improved, true, true),
            out _window,
            out _gd);
        _window.Resized += () =>
        {
            _gd.MainSwapchain.Resize((uint)_window.Width, (uint)_window.Height);
            _controller.WindowResized(_window.Width, _window.Height);
        };
        _cl = _gd.ResourceFactory.CreateCommandList();
        _controller = new ImGuiController(_gd, _gd.MainSwapchain.Framebuffer.OutputDescription, _window.Width, _window.Height);
        // _memoryEditor = new MemoryEditor();
        // Random random = new Random();
        // _memoryEditorData = Enumerable.Range(0, 1024).Select(i => (byte)random.Next(255)).ToArray();

        var stopwatch = Stopwatch.StartNew();
        float deltaTime = 0f;
        // Main application loop
        while (_window.Exists)
        {
            deltaTime = stopwatch.ElapsedTicks / (float)Stopwatch.Frequency;
            stopwatch.Restart();
            InputSnapshot snapshot = _window.PumpEvents();
            if (!_window.Exists) { break; }
            _controller.Update(deltaTime, snapshot); // Feed the input events to our ImGui controller, which passes them through to ImGui.

            SubmitUI();

            _cl.Begin();
            _cl.SetFramebuffer(_gd.MainSwapchain.Framebuffer);
            _cl.ClearColorTarget(0, new RgbaFloat(_clearColor.X, _clearColor.Y, _clearColor.Z, 1f));
            _controller.Render(_gd, _cl);
            _cl.End();
            _gd.SubmitCommands(_cl);
            _gd.SwapBuffers(_gd.MainSwapchain);
        }

        // Clean up Veldrid resources
        _gd.WaitForIdle();
        _controller.Dispose();
        _cl.Dispose();
        _gd.Dispose();
    }
    
    static bool _processing = false;
    static double _progress;
    
    static string _statusText = "Hi!";
    static string _outputFileURL = "";
    
    static readonly List<ConversionReport> reports = [];
    
    static readonly Vector2 _convertButtonSize = new(120, 19);
    
    static int _oggVBRBitrate = 192;
    
    static bool _mp3VBR = true;
    
    static int _mp3VBROptionIndex = 0;
    static readonly string[] _mp3VBROptions = [
        "220-260 kbit/s",
        "190-250 kbit/s",
        "170-210 kbit/s",
        "150-195 kbit/s",
        "140-185 kbit/s",
        "120-150 kbit/s",
        "100-130 kbit/s",
        "80-120 kbit/s",
        "70-105 kbit/s",
        "45-85 kbit/s",
    ];
    
    static int _mp3CBROptionIndex = 1;
    static readonly string[] _mp3CBROptions = [
        "320 kbit/s",
        "256 kbit/s",
        "224 kbit/s",
        "192 kbit/s",
        "160 kbit/s",
        "128 kbit/s",
        "112 kbit/s",
        "96 kbit/s",
        "80 kbit/s",
        "64 kbit/s",
        "48 kbit/s",
        "40 kbit/s",
        "32 kbit/s",
        "24 kbit/s",
        "16 kbit/s",
        "8 kbit/s",
    ];
    static readonly int[] _mp3CBRIndexToBitrate = [
        320,
        256,
        224,
        192,
        160,
        128,
        112,
        96,
        80,
        64,
        48,
        40,
        32,
        24,
        16,
        8,
    ];
    
    static bool _aacVBR = true;
    
    static int _aacVBROptionIndex = 1;
    static readonly string[] _aacVBROptions = [
        "Highest (0.1)",
        "Excellent (1.0)",
        "Very good (2.0)",
        "Good (3.0)",
        "Fair (4.0)",
    ];
    static readonly double[] _aacVBRQualityValues = [
        0.1,
        1.0,
        2.0,
        3.0,
        4.0,
    ];
    
    static int _aacCBROptionIndex = 2;
    static readonly string[] _aacCBROptions = [
        "320 kbit/s",
        "256 kbit/s",
        "192 kbit/s",
        "160 kbit/s",
        "128 kbit/s",
        "96 kbit/s",
        "64 kbit/s",
        "48 kbit/s",
        "32 kbit/s",
    ];
    static readonly int[] _aacCBRIndexToBitrate = [
        320,
        256,
        192,
        160,
        128,
        96,
        64,
        48,
        32,
    ];
    
    static bool SelectFile(out string filePath, out string newFilePath, string newFileSuffix)
    {
        using var selectFileDialog = new NativeFileDialog().SelectFile();
                
        var result = selectFileDialog.Open(out filePath,
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        
        if (result == DialogResult.Okay)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            newFilePath = Path.Combine(Path.GetDirectoryName(filePath), fileName + $"{newFileSuffix}");
            
            return true;
        }

        newFilePath = null;

        return false;
    }
    
    private static void SubmitUI()
    {
        const ImGuiWindowFlags windowFlags =
            ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoDecoration;
        
        var viewport = ImGui.GetMainViewport();
        
        // --- LEFT PANEL ---
        
        var leftWidth = viewport.WorkSize.X * 2f / 3f;
        var rightWidth = viewport.WorkSize.X * 1f / 3f;
        
        ImGui.SetNextWindowPos(viewport.WorkPos);
        ImGui.SetNextWindowSize(new Vector2(leftWidth, viewport.WorkSize.Y));
        
        ImGui.Begin("Left Panel", windowFlags);
        
        var style = ImGui.GetStyle();
        style.ItemSpacing = new(style.ItemSpacing.X, 6);
        
        if (ImGui.BeginTabBar("MainTabs"))
        {
            if (ImGui.BeginTabItem("Audio"))
            {
                AudioUI();
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("Video"))
            {
                VideoUI();
                ImGui.EndTabItem();
            }
            
            ImGui.EndTabBar();
        }
        
        ImGui.End();
        
        // --- RIGHT PANEL ---
        
        ImGui.SameLine();
        
        ImGui.SetNextWindowPos(viewport.WorkPos + new Vector2(leftWidth, 0));
        ImGui.SetNextWindowSize(new Vector2(rightWidth, viewport.WorkSize.Y));
        
        ImGui.Begin("Right Panel", windowFlags);

        if (reports.Count == 0)
        {
            ImGui.TextColored(new Vector4(1, 1, 1, 0.5f), "No conversion reports...");
        }

        for (int i = reports.Count - 1; i >= 0; i--)
        {
            reports[i].SubmitUI();
        }
        
        ImGui.End();
    }
    
    static string _selectedContainerStr = "mp4";
    static string _selectedContAndCodecStr = "mp4-h264";
    
    unsafe private static void AudioUI()
    {
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + 400);
        ImGui.TextWrapped("Recommendations:\n" +
            "- Editing: any uncompressed;\n" +
            "- Listening: OGG Vorbis 320 kbit/s;\n" +
            "- Compatibility: MP3.");
        ImGui.PopTextWrapPos();
        
        if (ImGui.CollapsingHeader("To uncompressed audio file formats", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (ImGui.TreeNode("Info##uncompressed"))
            {
                ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + 400);
                ImGui.TextWrapped("These formats store uncompressed PCM audio, making them very large in size. " +
                    "They are typically used in production environments (recording, editing, mixing) or in " +
                    "cases where maximum quality and fast decoding are required.");
                
                ImGui.TextLinkOpenURL("WAV", "https://en.wikipedia.org/wiki/WAV");
                ImGui.SameLine();
                ImGui.TextWrapped("- Windows standard.");
                
                ImGui.TextLinkOpenURL("AIFF", "https://en.wikipedia.org/wiki/Audio_Interchange_File_Format");
                ImGui.SameLine();
                ImGui.TextWrapped("- Apple standard.");
                
                ImGui.TextLinkOpenURL("PCM", "https://en.wikipedia.org/wiki/Pulse-code_modulation");
                ImGui.SameLine();
                ImGui.TextWrapped("- Digital sample encoding.");
                
                ImGui.TextLinkOpenURL("RAW", "https://en.wikipedia.org/wiki/Raw_audio_format");
                ImGui.SameLine();
                ImGui.TextWrapped("- Un-containerized.");
                
                ImGui.PopTextWrapPos();
                ImGui.TreePop();
            }
            
            if (ImGui.Button("To WAV", _convertButtonSize))
            {
                if (SelectFile(out var filePath, out var newFilePath, "_wav-output.wav"))
                {
                    var inputFileInfo = FFProbe.Analyse(filePath);
                    var inputContainer = Path.GetExtension(filePath).TrimStart('.');
                    var inputAudioCodec = inputFileInfo.PrimaryAudioStream?.CodecName ?? "unknown";
                    
                    var argumentProcessor = FFMpegArguments
                        .FromFileInput(filePath)
                        .OutputToFile(newFilePath, false);
                    
                    var report = new ConversionReport(
                        inputContainer, 
                        inputAudioCodec, 
                        "wav", 
                        "pcm_s16le", 
                        filePath, 
                        newFilePath, 
                        argumentProcessor,
                        report => reports.Remove(report));
                    
                    reports.Add(report);
                }
            }
            
            ImGui.SameLine();
            
            if (ImGui.Button("To AIFF", _convertButtonSize))
            {
                if (SelectFile(out var filePath, out var newFilePath, "_aiff-output.aiff"))
                {
                    var inputFileInfo = FFProbe.Analyse(filePath);
                    var inputContainer = Path.GetExtension(filePath).TrimStart('.');
                    var inputAudioCodec = inputFileInfo.PrimaryAudioStream?.CodecName ?? "unknown";

                    var argumentProcessor = FFMpegArguments
                        .FromFileInput(filePath)
                        .OutputToFile(newFilePath, false);
                    
                    var report = new ConversionReport(
                        inputContainer, 
                        inputAudioCodec, 
                        "aiff", 
                        "pcm_s16be", 
                        filePath, 
                        newFilePath, 
                        argumentProcessor,
                        report => reports.Remove(report));
                    
                    reports.Add(report);
                }
            }

            if (ImGui.Button("To PCM", _convertButtonSize))
            {
                if (SelectFile(out var filePath, out var newFilePath, "_pcm-output.pcm"))
                {
                    var inputFileInfo = FFProbe.Analyse(filePath);
                    var inputContainer = Path.GetExtension(filePath).TrimStart('.');
                    var inputAudioCodec = inputFileInfo.PrimaryAudioStream?.CodecName ?? "unknown";

                    var argumentProcessor = FFMpegArguments
                        .FromFileInput(filePath)
                        .OutputToFile(newFilePath, false);
                    
                    var report = new ConversionReport(
                        inputContainer, 
                        inputAudioCodec, 
                        "pcm", 
                        "pcm_s16le", 
                        filePath, 
                        newFilePath, 
                        argumentProcessor,
                        report => reports.Remove(report));
                    
                    reports.Add(report);
                }
            }

            ImGui.SameLine();

            if (ImGui.Button("To RAW", _convertButtonSize))
            {
                if (SelectFile(out var filePath, out var newFilePath, "_raw-output.raw"))
                {
                    var inputFileInfo = FFProbe.Analyse(filePath);
                    var inputContainer = Path.GetExtension(filePath).TrimStart('.');
                    var inputAudioCodec = inputFileInfo.PrimaryAudioStream?.CodecName ?? "unknown";

                    var argumentProcessor = FFMpegArguments
                        .FromFileInput(filePath)
                        .OutputToFile(newFilePath, false);
                    
                    var report = new ConversionReport(
                        inputContainer, 
                        inputAudioCodec, 
                        "raw", 
                        "pcm_s16le", 
                        filePath, 
                        newFilePath, 
                        argumentProcessor,
                        report => reports.Remove(report));
                    
                    reports.Add(report);
                }
            }
            
            ImGui.Spacing();
        }
        
        if (ImGui.CollapsingHeader("To compressed lossless audio file formats", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (ImGui.TreeNode("Info##lossless"))
            {
                ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + 400);
                ImGui.TextWrapped("These formats use lossless compression, reducing file size while preserving original quality. " +
                    "They are common for music archiving and high-fidelity playback.");

                ImGui.TextLinkOpenURL("FLAC", "https://en.wikipedia.org/wiki/FLAC");
                ImGui.SameLine();
                ImGui.TextWrapped("- Open standard.");
                
                ImGui.TextLinkOpenURL("ALAC", "https://en.wikipedia.org/wiki/Apple_Lossless_Audio_Codec");
                ImGui.SameLine();
                ImGui.TextWrapped("- Apple standard.");
                
                ImGui.PopTextWrapPos();
                ImGui.TreePop();
            }
            
            if (ImGui.Button("To FLAC", _convertButtonSize))
            {
                if (SelectFile(out var filePath, out var newFilePath, "_flac-output.flac"))
                {
                    var inputFileInfo = FFProbe.Analyse(filePath);
                    var inputContainer = Path.GetExtension(filePath).TrimStart('.');
                    var inputAudioCodec = inputFileInfo.PrimaryAudioStream?.CodecName ?? "unknown";
                    
                    var argumentProcessor = FFMpegArguments
                        .FromFileInput(filePath)
                        .OutputToFile(newFilePath, false);
                    
                    var report = new ConversionReport(
                        inputContainer, 
                        inputAudioCodec, 
                        "flac", 
                        "flac", 
                        filePath, 
                        newFilePath, 
                        argumentProcessor,
                        report => reports.Remove(report));
                    
                    reports.Add(report);
                }
            }
            
            ImGui.SameLine();
            
            if (ImGui.Button("To ALAC", _convertButtonSize))
            {
                if (SelectFile(out var filePath, out var newFilePath, "_alac-output.m4a"))
                {
                    var inputFileInfo = FFProbe.Analyse(filePath);
                    var inputContainer = Path.GetExtension(filePath).TrimStart('.');
                    var inputAudioCodec = inputFileInfo.PrimaryAudioStream?.CodecName ?? "unknown";

                    var argumentProcessor = FFMpegArguments
                        .FromFileInput(filePath)
                        .OutputToFile(newFilePath, false, options => options
                            .WithCustomArgument("-c:a alac -movflags +faststart"));
                    
                    var report = new ConversionReport(
                        inputContainer, 
                        inputAudioCodec, 
                        "m4a", 
                        "alac", 
                        filePath, 
                        newFilePath, 
                        argumentProcessor,
                        report => reports.Remove(report));
                    
                    reports.Add(report);
                }
            }
            
            ImGui.Spacing();
        }
        
        if (ImGui.CollapsingHeader("To compressed lossy audio file formats", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (ImGui.TreeNode("Info##lossy"))
            {
                ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + 400);
                ImGui.TextWrapped("These formats use lossy compression, greatly reducing file size while " +
                    "sacrificing some quality. They are common for streaming and high-fidelity playback.");

                ImGui.TextLinkOpenURL("MP3", "https://en.wikipedia.org/wiki/MP3");
                ImGui.SameLine();
                ImGui.TextWrapped("- Old but very common, will run on anything.");

                ImGui.TextLinkOpenURL("M4A (AAC)", "https://en.wikipedia.org/wiki/MP4_file_format");
                ImGui.SameLine();
                ImGui.TextWrapped("- Container for AAC.");

                ImGui.TextLinkOpenURL("AAC", "https://en.wikipedia.org/wiki/Advanced_Audio_Coding");
                ImGui.SameLine();
                ImGui.TextWrapped("- Successor to MP3, patent-encumbered, very common.");

                ImGui.TextLinkOpenURL("OGG Opus", "https://en.wikipedia.org/wiki/Opus_(audio_format)");
                ImGui.SameLine();
                ImGui.TextWrapped("- Opus in OGG container, the best lossy codec + it's open-source.");
                
                ImGui.PopTextWrapPos();
                ImGui.TreePop();
            }
            
            if (ImGui.Button("To MP3", _convertButtonSize))
            {
                if (SelectFile(out var filePath, out var newFilePath, "_mp3-output.mp3"))
                {
                    var inputFileInfo = FFProbe.Analyse(filePath);
                    var inputContainer = Path.GetExtension(filePath).TrimStart('.');
                    var inputAudioCodec = inputFileInfo.PrimaryAudioStream?.CodecName ?? "unknown";

                    if (_mp3VBR)
                    {
                        var argumentProcessor = FFMpegArguments
                            .FromFileInput(filePath)
                            .OutputToFile(newFilePath, false, options => options
                                .WithAudioCodec(AudioCodec.LibMp3Lame)
                                .WithCustomArgument($"-qscale:a {_mp3VBROptionIndex}"));
                        
                        var report = new ConversionReport(
                            inputContainer, 
                            inputAudioCodec, 
                            "mp3", 
                            "mp3", 
                            filePath, 
                            newFilePath, 
                            argumentProcessor,
                        report => reports.Remove(report));
                        
                        reports.Add(report);
                    }
                    else
                    {
                        var argumentProcessor = FFMpegArguments
                            .FromFileInput(filePath)
                            .OutputToFile(newFilePath, false, options => options
                                .WithAudioCodec(AudioCodec.LibMp3Lame)
                                .WithCustomArgument($"-b:a {_mp3CBRIndexToBitrate[_mp3CBROptionIndex]}k"));
                        
                        var report = new ConversionReport(
                            inputContainer, 
                            inputAudioCodec, 
                            "mp3", 
                            "mp3", 
                            filePath, 
                            newFilePath, 
                            argumentProcessor,
                        report => reports.Remove(report));
                        
                        reports.Add(report);
                    }
                }
            }
            
            ImGui.SameLine();
            ImGui.Checkbox("VBR##mp3", ref _mp3VBR);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(150);
            
            if (_mp3VBR)
                ImGui.Combo("Bitrate##mp3", ref _mp3VBROptionIndex, _mp3VBROptions, _mp3VBROptions.Length);
            else
                ImGui.Combo("Bitrate##mp3", ref _mp3CBROptionIndex, _mp3CBROptions, _mp3CBROptions.Length);
            
            if (ImGui.Button("To M4A (AAC)", _convertButtonSize))
            {
                if (SelectFile(out var filePath, out var newFilePath, "_m4a-output.m4a"))
                {
                    var inputFileInfo = FFProbe.Analyse(filePath);
                    var inputContainer = Path.GetExtension(filePath).TrimStart('.');
                    var inputAudioCodec = inputFileInfo.PrimaryAudioStream?.CodecName ?? "unknown";

                    if (_aacVBR)
                    {
                        var argumentProcessor = FFMpegArguments
                            .FromFileInput(filePath)
                            .OutputToFile(newFilePath, false, options => options
                                .WithAudioCodec(AudioCodec.Aac)
                                .WithCustomArgument($"-q:a {_aacVBRQualityValues[_aacVBROptionIndex]}"));
                        
                        var report = new ConversionReport(
                            inputContainer, 
                            inputAudioCodec, 
                            "m4a", 
                            "aac", 
                            filePath, 
                            newFilePath, 
                            argumentProcessor,
                        report => reports.Remove(report));
                        
                        reports.Add(report);
                    }
                    else
                    {
                        var argumentProcessor = FFMpegArguments
                            .FromFileInput(filePath)
                            .OutputToFile(newFilePath, false, options => options
                                .WithAudioCodec(AudioCodec.Aac)
                                .WithCustomArgument($"-b:a {_aacCBRIndexToBitrate[_aacCBROptionIndex]}k"));
                        
                        var report = new ConversionReport(
                            inputContainer, 
                            inputAudioCodec, 
                            "m4a", 
                            "aac", 
                            filePath, 
                            newFilePath, 
                            argumentProcessor,
                        report => reports.Remove(report));
                        
                        reports.Add(report);
                    }
                }
            }
            
            ImGui.SameLine();
            
            if (ImGui.Button("To AAC", _convertButtonSize))
            {
                if (SelectFile(out var filePath, out var newFilePath, "_aac-output.aac"))
                {
                    var inputFileInfo = FFProbe.Analyse(filePath);
                    var inputContainer = Path.GetExtension(filePath).TrimStart('.');
                    var inputAudioCodec = inputFileInfo.PrimaryAudioStream?.CodecName ?? "unknown";

                    if (_aacVBR)
                    {
                        var argumentProcessor = FFMpegArguments
                            .FromFileInput(filePath)
                            .OutputToFile(newFilePath, false, options => options
                                .WithAudioCodec(AudioCodec.Aac)
                                .WithCustomArgument($"-q:a {_aacVBRQualityValues[_aacVBROptionIndex]}"));
                        
                        var report = new ConversionReport(
                            inputContainer, 
                            inputAudioCodec, 
                            "aac", 
                            "aac", 
                            filePath, 
                            newFilePath, 
                            argumentProcessor,
                        report => reports.Remove(report));
                        
                        reports.Add(report);
                    }
                    else
                    {
                        var argumentProcessor = FFMpegArguments
                            .FromFileInput(filePath)
                            .OutputToFile(newFilePath, false, options => options
                                .WithAudioCodec(AudioCodec.Aac)
                                .WithCustomArgument($"-b:a {_aacCBRIndexToBitrate[_aacCBROptionIndex]}k"));
                        
                        var report = new ConversionReport(
                            inputContainer, 
                            inputAudioCodec, 
                            "aac", 
                            "aac", 
                            filePath, 
                            newFilePath, 
                            argumentProcessor,
                        report => reports.Remove(report));
                        
                        reports.Add(report);
                    }
                }
            }
            
            ImGui.SameLine();
            ImGui.Checkbox("VBR##aac", ref _aacVBR);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(130);
            
            if (_aacVBR)
                ImGui.Combo("Quality##aac", ref _aacVBROptionIndex, _aacVBROptions, _aacVBROptions.Length);
            else
                ImGui.Combo("Bitrate##aac", ref _aacCBROptionIndex, _aacCBROptions, _aacCBROptions.Length);
            
            if (ImGui.Button("To OGG (Opus)", _convertButtonSize))
            {
                if (SelectFile(out var filePath, out var newFilePath, "_ogg-output.ogg"))
                {
                    var inputFileInfo = FFProbe.Analyse(filePath);
                    var inputContainer = Path.GetExtension(filePath).TrimStart('.');
                    var inputAudioCodec = inputFileInfo.PrimaryAudioStream?.CodecName ?? "unknown";

                    var argumentProcessor = FFMpegArguments
                        .FromFileInput(filePath)
                        .OutputToFile(newFilePath, false, options => options
                            .WithAudioCodec(FFMpeg.GetCodec("libopus"))
                            .WithCustomArgument("-vbr on")
                            .WithCustomArgument($"-b:a {_oggVBRBitrate}k"));
                    
                    var report = new ConversionReport(
                        inputContainer, 
                        inputAudioCodec, 
                        "ogg", 
                        "opus", 
                        filePath, 
                        newFilePath, 
                        argumentProcessor,
                        report => reports.Remove(report));
                    
                    reports.Add(report);
                }
            }
            
            ImGui.SameLine();
            ImGui.SetNextItemWidth(100);
            
            ImGui.InputInt("Bitrate (kbit/s)##ogg", ref _oggVBRBitrate, 32);
            _oggVBRBitrate = Math.Clamp(_oggVBRBitrate, 6, 510);
        }
    }
    
    static int _vidContComboIndex = 0;
    static string[] _vidContOptions = [
        "MP4", "MKV",
        // "MOV", "AVI", "WEBM"
    ];
    
    static int _mp4VidEncComboIndex = 0;
    static string[] _mp4VidCodecComboOptions = [
        "H.264 / AVC - libx264", "H.265 / HEVC - libx265", "AV1 - libaom-av1 (slow, best)", "AV1 - svt-av1 (fast, great)"
    ];
    static string[] _mp4VidCodecs = [
        "libx264", "libx265", "libaom-av1", "libsvtav1"
    ];
    
    static int _h264RCComboIndex = 0;
    static string[] _h264RCComboOptions = [
        "CRF", "CBR"
    ];
    
    static int _h264CRF = 23;
    static Vector2 _h264CRFRange = new(0, 51);
    static uint _h264Bitrate = 5000;
    
    static int _h264PresetComboIndex = 4;
    static string[] _h264PresetOptions = [
        "ultrafast",
        "superfast",
        "veryfast",
        "faster",
        "fast",
        "medium",
        "slow",
        "slower",
        "veryslow",
    ];
    
    static int _h265PresetComboIndex = 4;
    static string[] _h265PresetOptions = [
        "ultrafast",
        "superfast",
        "veryfast",
        "faster",
        "fast",
        "medium",
        "slow",
        "slower",
        "veryslow",
        "placebo",
    ];
    
    static int _av1CRF = 30;
    static Vector2 _av1CRFRange = new(0, 63);
    
    static int _av1libaomPreset = 1;
    static Vector2 _av1libaomPresetRange = new(0, 8);
    
    static int _av1svtPreset = 8;
    static Vector2 _av1svtPresetRange = new(0, 13);
    
    static Codec _selectedVideoCodec = VideoCodec.LibX264;
    
    static uint _videoWidth = 1920;
    static uint _videoHeight = 1080;
    static double _videoFrameRate = 30;
    
    static string[] _videoArguments = [
        "-c:v libx264", "-crf 23", "-preset medium"
    ];
    static bool _mantainResolution = true;
    static bool _mantainFrameRate = true;

    unsafe private static void VideoUI()
    {
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + 400);
        ImGui.TextWrapped("Recommendations:\n" +
            "- High or low quality: AV1 svt;\n" +
            "- High or low quality but PAINFULLY SLOW: AV1 libaom;\n" +
            "- Compatibility/fast: MP4 h264.");
        ImGui.PopTextWrapPos();
        
        ImGui.PushItemWidth(220);
        
        if (ImGui.Combo("Conainer", ref _vidContComboIndex, _vidContOptions, _vidContOptions.Length))
        {
            _selectedContainerStr = _vidContOptions[_vidContComboIndex].ToLower();
        }
        
        switch (_vidContComboIndex)
        {
            case 0: // MP4
            case 1: // MKV
            
            if (ImGui.Combo("Video Codec", ref _mp4VidEncComboIndex, _mp4VidCodecComboOptions, _mp4VidCodecComboOptions.Length))
            {
                _videoArguments[0] = "-c:v " + _mp4VidCodecs[_mp4VidEncComboIndex];
                
                switch (_mp4VidEncComboIndex)
                {
                    case 0: // H.264
                    switch (_h264RCComboIndex)
                    {
                        case 0: // CRF
                        _videoArguments[1] = "-crf " + _h264CRF.ToString();
                        break;
                        
                        case 1: // CBR
                        _videoArguments[1] = "-b:v " + _h264Bitrate.ToString() + "k";
                        break;
                    }
                    break;
                    
                    case 2: // AV1 - libaom
                    _videoArguments[1] = "-crf " + _av1CRF.ToString();
                    _videoArguments[2] = "-cpu-used " + _av1libaomPreset.ToString();
                    break;
                    
                    case 3: // AV1 - svt-av1
                    _videoArguments[1] = "-crf " + _av1CRF.ToString();
                    _videoArguments[2] = "-preset " + _av1svtPreset.ToString();
                    break;
                }
            }
            
            switch (_mp4VidEncComboIndex)
            {
                case 0: // H.264
                _selectedContAndCodecStr = "mp4-h264";
                
                if (ImGui.Combo("Rate control##h264", ref _h264RCComboIndex, _h264RCComboOptions, _h264RCComboOptions.Length))
                {
                    switch (_h264RCComboIndex)
                    {
                        case 0: // CRF
                        _videoArguments[1] = "-crf " + _h264CRF.ToString();
                        break;
                        
                        case 1: // CBR
                        _videoArguments[1] = "-b:v " + _h264Bitrate.ToString() + "k";
                        break;
                    }
                }
                
                switch (_h264RCComboIndex)
                {
                    case 0: // CRF
                    if (ImGui.SliderInt("Quality (lower = better)##h264", ref _h264CRF, (int)_h264CRFRange.X, (int)_h264CRFRange.Y))
                        _videoArguments[1] = "-crf " + _h264CRF.ToString();
                    break;
                    
                    case 1: // CBR
                    fixed (uint* valPtr = &_h264Bitrate)
                    {
                        if (ImGui.InputScalar("Bitrate (kbit/s)##h264", ImGuiDataType.U32, (IntPtr)valPtr))
                            _videoArguments[1] = "-b:v " + _h264Bitrate.ToString() + "k";
                    }
                    break;
                }
                
                if (ImGui.Combo("Preset##h264", ref _h264PresetComboIndex, _h264PresetOptions, _h264PresetOptions.Length))
                    _videoArguments[2] = "-preset " + _h264PresetOptions[_h264PresetComboIndex];
                break;
                
                case 1: // H.265
                _selectedContAndCodecStr = "mp4-h265";
                break;

                case 2: // AV1 - libaom
                _selectedContAndCodecStr = "mp4-av1";
                
                if (ImGui.SliderInt("Quality (lower = better)##av1", ref _av1CRF, (int)_av1CRFRange.X, (int)_av1CRFRange.Y))
                    _videoArguments[1] = "-crf " + _av1CRF.ToString();
                
                if (ImGui.SliderInt("Preset (higher = faster = worse)##av1", ref _av1libaomPreset, (int)_av1libaomPresetRange.X, (int)_av1libaomPresetRange.Y))
                    _videoArguments[2] = "-cpu-used " + _av1libaomPreset.ToString();
                break;
                
                case 3: // AV1 - svt-av1
                _selectedContAndCodecStr = "mp4-av1";
                
                if (ImGui.SliderInt("Quality (lower = better)##av1", ref _av1CRF, (int)_av1CRFRange.X, (int)_av1CRFRange.Y))
                    _videoArguments[1] = "-crf " + _av1CRF.ToString();
                
                if (ImGui.SliderInt("Preset (higher = faster = worse)##av1", ref _av1svtPreset, (int)_av1svtPresetRange.X, (int)_av1svtPresetRange.Y))
                    _videoArguments[2] = "-preset " + _av1svtPreset.ToString();
                break;
            }
            
            break;
        }
        
        ImGui.PopItemWidth();
        
        ImGui.Checkbox("Maintain Resolution", ref _mantainResolution);
        
        if (!_mantainResolution)
        {
            ImGui.PushItemWidth(60);
            
            fixed (uint* valPtr = &_videoWidth)
            {
                ImGui.InputScalar("##with", ImGuiDataType.U32, (IntPtr)valPtr);
            }
            
            fixed (uint* valPtr = &_videoHeight)
            {
                ImGui.SameLine();
                ImGui.InputScalar("Resolution", ImGuiDataType.U32, (IntPtr)valPtr);
            }
            
            ImGui.PopItemWidth();
        }
        
        ImGui.Checkbox("Maintain Frame Rate", ref _mantainFrameRate);
        
        if (!_mantainFrameRate)
        {
            fixed (double* valPtr = &_videoFrameRate)
            {
                ImGui.PushItemWidth(100);
                ImGui.InputScalar("Frame rate", ImGuiDataType.Double, (IntPtr)valPtr);
                ImGui.PopItemWidth();
            }
        }
        
        if (ImGui.Button("Convert", _convertButtonSize))
        {
            if (SelectFile(out var filePath, out var newFilePath, $"_{_selectedContAndCodecStr}-output.{_selectedContainerStr}"))
            {
                var inputFileInfo = FFProbe.Analyse(filePath);
                var inputContainer = Path.GetExtension(filePath).TrimStart('.');
                var inputVideoCodec = inputFileInfo.PrimaryVideoStream?.CodecName ?? "unknown";
                var inputAudioCodec = inputFileInfo.PrimaryAudioStream?.CodecName ?? "unknown";
                
                // Extract the video codec from the selected configuration
                var outputVideoCodec = _mp4VidCodecs[_mp4VidEncComboIndex];
                var outputAudioCodec = "aac"; // Default audio codec for most video containers

                var argumentProcessor = FFMpegArguments
                    .FromFileInput(filePath)
                    .OutputToFile(newFilePath, false, options =>
                    {
                        var args = string.Join(" ", _videoArguments);
                        
                        options.WithCustomArgument(args);
                        
                        if (!_mantainResolution)
                            options.Resize((int)_videoWidth, (int)_videoHeight);
                        
                        if (!_mantainFrameRate)
                            options.WithFramerate(_videoFrameRate);
                    });
                
                var report = new ConversionReport(
                    inputContainer, 
                    inputVideoCodec, 
                    inputAudioCodec, 
                    _selectedContainerStr, 
                    outputVideoCodec, 
                    outputAudioCodec, 
                    filePath, 
                    newFilePath, 
                    argumentProcessor,
                    report => reports.Remove(report));
                
                reports.Add(report);
            }
        }
    }
}
