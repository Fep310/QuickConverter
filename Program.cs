using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using FFMpegCore;
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
            new WindowCreateInfo(1920/3, 1080/3, 512+128, 512, WindowState.Normal, "QuickConverter"),
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
    
    static string _statusText = "Hi!";
    
    static readonly Vector2 _convertButtonSize = new(120, 19);
    
    static int _oggVBROptionIndex = 2;
    static readonly string[] _oggVBROptions = [
        "500-1000 kbit/s",
        "320-500 kbit/s",
        "256-320 kbit/s",
        "224-256 kbit/s",
        "192-224 kbit/s",
        "160-192 kbit/s",
        "128-160 kbit/s",
        "112-128 kbit/s",
        "96-112 kbit/s",
        "80-96 kbit/s",
        "64-80 kbit/s",
        "48-64 kbit/s",
        "32-64 kbit/s",
    ];
    static readonly int[] _VBROptionIndexToNumber = [
        10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0, -1, -2,
    ];
    
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
        "Highest quality (0.1)",
        "Excellent quality (1.0)",
        "Very good quality (2.0)",
        "Good quality (3.0)",
        "Fair quality (4.0)",
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
        const ImGuiWindowFlags fullscreenWindowFlags =
            ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings;
        
        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewport.WorkPos);
        ImGui.SetNextWindowSize(viewport.WorkSize);
        
        ImGui.Begin("Main Window", fullscreenWindowFlags);
        
        var style = ImGui.GetStyle();
        style.ItemSpacing = new(style.ItemSpacing.X, 6);
        
        ImGui.BeginChild("MainContent", ImGui.GetContentRegionAvail() - new Vector2(0, ImGui.GetFontSize() * 2));
        
        if (ImGui.BeginTabBar("MainTabs"))
        {
            if (ImGui.BeginTabItem("Audio"))
            {
                AudioUI();
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("Video"))
            {
                // todo
                ImGui.EndTabItem();
            }
            
            ImGui.EndTabBar();
        }
        
        ImGui.EndChild();
        
        ImGui.Separator();
        ImGui.Text(_statusText);
        
        ImGui.End();
    }
    
    private static void AudioUI()
    {
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
                    _statusText = "Converting to wav...";
                    
                    FFMpegArguments
                        .FromFileInput(filePath)
                        .OutputToFile(newFilePath, false)
                        .ProcessSynchronously();
                    
                    _statusText = "Converted to wav.";
                }
            }
            
            ImGui.SameLine();
            
            if (ImGui.Button("To AIFF", _convertButtonSize))
            {
                if (SelectFile(out var filePath, out var newFilePath, "_aiff-output.aiff"))
                {
                    _statusText = "Converting to aiff...";

                    FFMpegArguments
                        .FromFileInput(filePath)
                        .OutputToFile(newFilePath, false)
                        .ProcessSynchronously();

                    _statusText = "Converted to aiff.";
                }
            }

            if (ImGui.Button("To PCM", _convertButtonSize))
            {
                if (SelectFile(out var filePath, out var newFilePath, "_pcm-output.pcm"))
                {
                    _statusText = "Converting to pcm...";

                    FFMpegArguments
                        .FromFileInput(filePath)
                        .OutputToFile(newFilePath, false)
                        .ProcessSynchronously();

                    _statusText = "Converted to pcm.";
                }
            }

            ImGui.SameLine();

            if (ImGui.Button("To RAW", _convertButtonSize))
            {
                if (SelectFile(out var filePath, out var newFilePath, "_raw-output.raw"))
                {
                    _statusText = "Converting to raw...";

                    FFMpegArguments
                        .FromFileInput(filePath)
                        .OutputToFile(newFilePath, false)
                        .ProcessSynchronously();

                    _statusText = "Converted to raw.";
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
                    _statusText = "Converting to flac...";
                    
                    FFMpegArguments
                        .FromFileInput(filePath)
                        .OutputToFile(newFilePath, false)
                        .ProcessSynchronously();
                        
                    _statusText = "Converted to flac.";
                }
            }
            
            ImGui.SameLine();
            
            if (ImGui.Button("To ALAC", _convertButtonSize))
            {
                if (SelectFile(out var filePath, out var newFilePath, "_alac-output.m4a"))
                {
                    _statusText = "Converting to alac...";

                    FFMpegArguments
                        .FromFileInput(filePath)
                        .OutputToFile(newFilePath, false, options => options
                            .WithCustomArgument("-c:a alac -movflags +faststart"))
                        .ProcessSynchronously();

                    _statusText = "Converted to alac.";
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
                    "sacrificing some quality. They are common for streaming, distribution, and general playback.");

                ImGui.TextLinkOpenURL("MP3", "https://en.wikipedia.org/wiki/MP3");
                ImGui.SameLine();
                ImGui.TextWrapped("- Old but very common.");

                ImGui.TextLinkOpenURL("M4A (AAC)", "https://en.wikipedia.org/wiki/MP4_file_format");
                ImGui.SameLine();
                ImGui.TextWrapped("- Container for AAC.");

                ImGui.TextLinkOpenURL("AAC", "https://en.wikipedia.org/wiki/Advanced_Audio_Coding");
                ImGui.SameLine();
                ImGui.TextWrapped("- Successor to MP3, patent-encumbered.");

                ImGui.TextLinkOpenURL("OGG", "https://en.wikipedia.org/wiki/Vorbis");
                ImGui.SameLine();
                ImGui.TextWrapped("- Vorbis in OGG container, open-source.");
                
                ImGui.PopTextWrapPos();
                ImGui.TreePop();
            }
            
            if (ImGui.Button("To MP3", _convertButtonSize))
            {
                if (SelectFile(out var filePath, out var newFilePath, "_mp3-output.mp3"))
                {
                    _statusText = "Converting to mp3...";

                    if (_mp3VBR)
                    {
                        FFMpegArguments
                            .FromFileInput(filePath)
                            .OutputToFile(newFilePath, false, options => options
                                .WithAudioCodec(AudioCodec.LibMp3Lame)
                                .WithCustomArgument($"-qscale:a {_mp3VBROptionIndex}"))
                            .ProcessSynchronously();
                    }
                    else
                    {
                        FFMpegArguments
                            .FromFileInput(filePath)
                            .OutputToFile(newFilePath, false, options => options
                                .WithAudioCodec(AudioCodec.LibMp3Lame)
                                .WithCustomArgument($"-b:a {_mp3CBRIndexToBitrate[_mp3CBROptionIndex]}k"))
                            .ProcessSynchronously();
                    }

                    _statusText = "Converted to mp3.";
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
                    _statusText = "Converting to m4a...";

                    if (_aacVBR)
                    {
                        FFMpegArguments
                            .FromFileInput(filePath)
                            .OutputToFile(newFilePath, false, options => options
                                .WithAudioCodec(AudioCodec.Aac)
                                .WithCustomArgument($"-q:a {_aacVBRQualityValues[_aacVBROptionIndex]}"))
                            .ProcessSynchronously();
                    }
                    else
                    {
                        FFMpegArguments
                            .FromFileInput(filePath)
                            .OutputToFile(newFilePath, false, options => options
                                .WithAudioCodec(AudioCodec.Aac)
                                .WithCustomArgument($"-b:a {_aacCBRIndexToBitrate[_aacCBROptionIndex]}k"))
                            .ProcessSynchronously();
                    }

                    _statusText = "Converted to m4a.";
                }
            }
            
            ImGui.SameLine();
            
            if (ImGui.Button("To AAC", _convertButtonSize))
            {
                if (SelectFile(out var filePath, out var newFilePath, "_aac-output.aac"))
                {
                    _statusText = "Converting to aac...";

                    if (_aacVBR)
                    {
                        FFMpegArguments
                            .FromFileInput(filePath)
                            .OutputToFile(newFilePath, false, options => options
                                .WithAudioCodec(AudioCodec.Aac)
                                .WithCustomArgument($"-q:a {_aacVBRQualityValues[_aacVBROptionIndex]}"))
                            .ProcessSynchronously();
                    }
                    else
                    {
                        FFMpegArguments
                            .FromFileInput(filePath)
                            .OutputToFile(newFilePath, false, options => options
                                .WithAudioCodec(AudioCodec.Aac)
                                .WithCustomArgument($"-b:a {_aacCBRIndexToBitrate[_aacCBROptionIndex]}k"))
                            .ProcessSynchronously();
                    }

                    _statusText = "Converted to aac.";
                }
            }
            
            ImGui.SameLine();
            ImGui.Checkbox("VBR##aac", ref _aacVBR);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(150);
            
            if (_aacVBR)
                ImGui.Combo("Quality##aac", ref _aacVBROptionIndex, _aacVBROptions, _aacVBROptions.Length);
            else
                ImGui.Combo("Bitrate##aac", ref _aacCBROptionIndex, _aacCBROptions, _aacCBROptions.Length);
            
            if (ImGui.Button("To OGG Vorbis", _convertButtonSize))
            {
                if (SelectFile(out var filePath, out var newFilePath, "_ogg-output.ogg"))
                {
                    _statusText = "Converting to ogg...";

                    FFMpegArguments
                        .FromFileInput(filePath)
                        .OutputToFile(newFilePath, false, options => options
                            .WithAudioCodec(AudioCodec.LibVorbis)
                            .WithCustomArgument($"-qscale:a {_VBROptionIndexToNumber[_oggVBROptionIndex]}"))
                        .ProcessSynchronously();

                    _statusText = "Converted to ogg.";
                }
            }
            
            ImGui.SameLine();
            ImGui.SetNextItemWidth(150);
            ImGui.Combo("Bitrate##ogg", ref _oggVBROptionIndex, _oggVBROptions, _oggVBROptions.Length);
            
            if (_oggVBROptionIndex > 10)
                ImGui.Text($"WARNING - {_oggVBROptions[_oggVBROptionIndex]} is only supported on aoTuVb3 and newer.");
                
        }
    }
}
