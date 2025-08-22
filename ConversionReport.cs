using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using FFMpegCore;
using ImGuiNET;

namespace QuickConverter;

public class ConversionReport
{
    public enum Type
    {
        Audio, Video
    }
    
    static uint s_nextId;

    public ConversionReport(string inputContainer, string inputAudioCodec, string newContainer, string newAudioCodec,
        string inputFilePath, string outputFilePath, FFMpegArgumentProcessor argumentProcessor,
        Action<ConversionReport> closeAction)
    {
        Id = s_nextId++;
        ConversionType = Type.Audio;
        _submitUIAction = SubmitAudioUI;
        _closeAction = closeAction;

        _inputContainer = inputContainer;
        _inputAudioCodec = inputAudioCodec;
        _outputContainer = newContainer;
        _outputAudioCodec = newAudioCodec;
        _inputFilePath = inputFilePath;
        _inputFilePathDir = Path.GetDirectoryName(inputFilePath);
        _outputFilePath = outputFilePath;
        _outputFilePathDir = Path.GetDirectoryName(outputFilePath);

        var totalTime = FFProbe.Analyse(_inputFilePath).Duration;
        argumentProcessor.NotifyOnProgress(UpdateProgress, totalTime);
        
        argumentProcessor
            .ProcessAsynchronously()
            .ContinueWith(OnProcessCompleted);
    }

    public ConversionReport(string inputContainer, string inputVideoCodec, string inputAudioCodec, string newContainer,
        string newVideoCodec, string newAudioCodec, string inputFilePath, string outputFilePath,
        FFMpegArgumentProcessor argumentProcessor, Action<ConversionReport> closeAction)
    {
        Id = s_nextId++;
        ConversionType = Type.Video;
        _submitUIAction = SubmitVideoUI;
        _closeAction = closeAction;

        _inputContainer = inputContainer;
        _inputVideoCodec = inputVideoCodec;
        _inputAudioCodec = inputAudioCodec;
        _outputContainer = newContainer;
        _outputVideoCodec = newVideoCodec;
        _outputAudioCodec = newAudioCodec;
        _inputFilePath = inputFilePath;
        _inputFilePathDir = Path.GetDirectoryName(inputFilePath);
        _outputFilePath = outputFilePath;
        _outputFilePathDir = Path.GetDirectoryName(outputFilePath);

        var totalTime = FFProbe.Analyse(_inputFilePath).Duration;
        argumentProcessor.NotifyOnProgress(UpdateProgress, totalTime);
        
        argumentProcessor
            .ProcessAsynchronously()
            .ContinueWith(OnProcessCompleted);
    }

    Action _submitUIAction;
    readonly Action<ConversionReport> _closeAction;
    readonly string _inputContainer;
    readonly string _outputContainer;
    readonly string _inputVideoCodec;
    readonly string _outputVideoCodec;
    readonly string _inputAudioCodec;
    readonly string _outputAudioCodec;
    readonly string _inputFilePath;
    readonly string _inputFilePathDir;
    readonly string _outputFilePath;
    readonly string _outputFilePathDir;
    string _errorMessage;
    public readonly Type ConversionType;
    public readonly uint Id;
    double _progress;

    void UpdateProgress(double progress) => _progress = progress;

    void OnProcessCompleted(Task<bool> task)
    {
        if (task.IsCompletedSuccessfully)
        {
            _submitUIAction = ConversionType == Type.Audio ?
                SubmitSuccesfullAudioUI : SubmitSuccessfulVideoUI;
        }
        else
        {
            _errorMessage = task.Exception?.GetBaseException().Message;
            _submitUIAction = SubmitUnsuccessfulUI;
        }
    }

    public void SubmitUI() => _submitUIAction();

    void SubmitAudioUI()
    {
        ImGui.SetNextWindowSizeConstraints(
            new Vector2(0, ImGui.GetTextLineHeightWithSpacing()),
            new Vector2(float.MaxValue, float.MaxValue));
        
        ImGui.BeginChild("Conversion Report##" + Id, Vector2.Zero, ImGuiChildFlags.Borders | ImGuiChildFlags.AutoResizeY);
        // ImGui.Text("CONVERSION ID: " + Id);
        ImGui.Text($"Converting {_inputContainer}-{_inputAudioCodec} to {_outputContainer}-{_outputAudioCodec}");
        ImGui.TextLinkOpenURL("Open input file.", _inputFilePath);
        ImGui.TextLinkOpenURL("Open input file directory.", _inputFilePathDir);
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        ImGui.ProgressBar((float)(_progress * .01), Vector2.Zero, _progress.ToString("0") + "%");
        ImGui.EndChild();
    }

    void SubmitVideoUI()
    {
        ImGui.SetNextWindowSizeConstraints(
            new Vector2(0, ImGui.GetTextLineHeightWithSpacing()),
            new Vector2(float.MaxValue, float.MaxValue));

        ImGui.BeginChild("Conversion Report##" + Id, Vector2.Zero, ImGuiChildFlags.Borders | ImGuiChildFlags.AutoResizeY);
        // ImGui.Text("CONVERSION ID: " + Id);
        ImGui.Text($"Converting {_inputContainer}-{_inputVideoCodec}-{_inputAudioCodec} to {_outputContainer}-{_outputVideoCodec}-{_outputAudioCodec}");
        ImGui.TextLinkOpenURL("Open input file.", _inputFilePath);
        ImGui.TextLinkOpenURL("Open input file directory.", _inputFilePathDir);
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        ImGui.ProgressBar((float)(_progress * .01), Vector2.Zero, _progress.ToString("0") + "%");
        ImGui.EndChild();
    }

    void SubmitSuccesfullAudioUI()
    {
        ImGui.SetNextWindowSizeConstraints(
            new Vector2(0, ImGui.GetTextLineHeightWithSpacing()),
            new Vector2(float.MaxValue, float.MaxValue));
        
        ImGui.BeginChild("Conversion Report##" + Id, Vector2.Zero, ImGuiChildFlags.Borders | ImGuiChildFlags.AutoResizeY);
        // ImGui.Text("CONVERSION ID: " + Id);
        ImGui.Text($"Converted {_inputContainer}-{_inputAudioCodec} to {_outputContainer}-{_outputAudioCodec}!");
        ImGui.TextLinkOpenURL("Open input file.", _inputFilePath);
        ImGui.TextLinkOpenURL("Open input file directory.", _inputFilePathDir);
        ImGui.TextLinkOpenURL("Open output file.", _outputFilePath);
        ImGui.TextLinkOpenURL("Open output file directory.", _outputFilePathDir);
        if (ImGui.Button("Close##" + Id)) _closeAction(this);
        ImGui.EndChild();
    }

    void SubmitSuccessfulVideoUI()
    {
        ImGui.SetNextWindowSizeConstraints(
            new Vector2(0, ImGui.GetTextLineHeightWithSpacing()),
            new Vector2(float.MaxValue, float.MaxValue));

        ImGui.BeginChild("Conversion Report##" + Id, Vector2.Zero, ImGuiChildFlags.Borders | ImGuiChildFlags.AutoResizeY);
        // ImGui.Text("CONVERSION ID: " + Id);
        ImGui.Text($"Converted {_inputContainer}-{_inputVideoCodec}-{_inputAudioCodec} to {_outputContainer}-{_outputVideoCodec}-{_outputAudioCodec}!");
        ImGui.TextLinkOpenURL("Open input file.", _inputFilePath);
        ImGui.TextLinkOpenURL("Open input file directory.", _inputFilePathDir);
        ImGui.TextLinkOpenURL("Open output file.", _outputFilePath);
        ImGui.TextLinkOpenURL("Open output file directory.", _outputFilePathDir);
        if (ImGui.Button("Close##" + Id)) _closeAction(this);
        ImGui.EndChild();
    }

    void SubmitUnsuccessfulUI()
    {
        ImGui.SetNextWindowSizeConstraints(
            new Vector2(0, ImGui.GetTextLineHeightWithSpacing()),
            new Vector2(float.MaxValue, float.MaxValue));

        ImGui.BeginChild("Conversion Report##" + Id, Vector2.Zero, ImGuiChildFlags.Borders | ImGuiChildFlags.AutoResizeY);
        // // ImGui.Text("CONVERSION ID: " + Id);
        ImGui.Text($"Failed to convert {_inputContainer}-{_inputVideoCodec}-{_inputAudioCodec} to " +
            $"{_outputContainer}-{_outputVideoCodec}-{_outputAudioCodec}!");
        ImGui.TextLinkOpenURL("Open input file.", _inputFilePath);
        ImGui.TextLinkOpenURL("Open input file directory.", _inputFilePathDir);
        ImGui.Text("Error message:");
        ImGui.InputTextMultiline("Error", ref _errorMessage, uint.MaxValue, Vector2.Zero,
            ImGuiInputTextFlags.ReadOnly | ImGuiInputTextFlags.AutoSelectAll);
        if (ImGui.Button("Close##" + Id)) _closeAction(this);
        ImGui.EndChild();
    }
}
