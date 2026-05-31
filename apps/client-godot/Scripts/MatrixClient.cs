using Godot;
using System;
using System.Text;

/// <summary>
/// MatrixClient
/// Connects the 2.5D Client (Layer 1/Layer 2) to the Cognitive Matrix Backend (10.0.0.102).
/// Transmits ConstraintField serialized state periodically.
/// </summary>
public partial class MatrixClient : Node
{
    [Signal]
    public delegate void DialogueReceivedEventHandler(string npcName, string text);

    private HttpRequest _httpRequest;
    private HttpRequest _dialogueRequest;
    private float _tickTimer = 0.0f;
    private const float TransmitInterval = 5.0f; // Send state every 5 seconds
    
    // We bind to local simulating 10.0.0.102 as per lightning_worker.py output
    private const string BackendUrl = "http://127.0.0.1:8080/ingest_state";

    public override void _Ready()
    {
        _httpRequest = new HttpRequest();
        AddChild(_httpRequest);
        _httpRequest.RequestCompleted += OnRequestCompleted;

        _dialogueRequest = new HttpRequest();
        AddChild(_dialogueRequest);
        _dialogueRequest.RequestCompleted += OnDialogueRequestCompleted;
        
        GD.Print("MatrixClient initialized. Target: " + BackendUrl);
    }

    public override void _Process(double delta)
    {
        _tickTimer += (float)delta;
        
        if (_tickTimer >= TransmitInterval)
        {
            TransmitState();
            _tickTimer = 0.0f;
        }
    }

    private void TransmitState()
    {
        string jsonState = ConstraintField.SerializeState();
        string[] headers = new string[] { "Content-Type: application/json" };
        
        Error err = _httpRequest.Request(BackendUrl, headers, HttpClient.Method.Post, jsonState);
        if (err != Error.Ok)
        {
            GD.PrintErr("MatrixClient: Failed to send request to Cognitive Matrix. Error: " + err);
        }
    }

    private void OnRequestCompleted(long result, long responseCode, string[] headers, byte[] body)
    {
        if (responseCode == 200 && body != null && body.Length > 0)
        {
            string response = Encoding.UTF8.GetString(body);
            // Parse response for any Substrate-level overrides (e.g. forced breaches)
            var json = new Json();
            var err = json.Parse(response);
            if (err == Error.Ok)
            {
                var dict = json.Data.AsGodotDictionary();
                if (dict.TryGetValue("corridor_classification", out var classification))
                {
                    // Evaluate classification: STABLE, NEUTRAL, BREACH
                    if (classification.AsString() == "BREACH")
                    {
                        // The Substrate has determined a hot-corridor breach.
                        // We escalate Layer 1 mechanics.
                        ConstraintField.EmitNoise(5.0f); // Force horde threat
                    }
                }
            }
        }
        else if (responseCode != 200 && responseCode != 0) // 0 happens if connection refused
        {
            GD.PrintErr($"MatrixClient: Non-200 response from Matrix ({responseCode})");
        }
    }

    public void RequestNPCDialogue(string npcName, string context)
    {
        var data = new Godot.Collections.Dictionary
        {
            {"NpcName", npcName},
            {"Context", context}
        };
        string json = Godot.Json.Stringify(data);
        string[] headers = new string[] { "Content-Type: application/json" };
        
        // Use 127.0.0.1:8080 which proxies to the 10.0.0.102 Ollama instance
        Error err = _dialogueRequest.Request("http://127.0.0.1:8080/generate_dialogue", headers, HttpClient.Method.Post, json);
        if (err != Error.Ok)
        {
            GD.PrintErr("MatrixClient: Failed to send dialogue request to Cognitive Matrix.");
        }
    }

    private void OnDialogueRequestCompleted(long result, long responseCode, string[] headers, byte[] body)
    {
        if (responseCode == 200 && body != null && body.Length > 0)
        {
            string response = Encoding.UTF8.GetString(body);
            var json = new Json();
            var err = json.Parse(response);
            if (err == Error.Ok)
            {
                var dict = json.Data.AsGodotDictionary();
                if (dict.TryGetValue("dialogue", out var dialogue))
                {
                    EmitSignal(SignalName.DialogueReceived, "NPC", dialogue.AsString());
                }
            }
        }
        else
        {
            EmitSignal(SignalName.DialogueReceived, "NPC", "The matrix is silent...");
        }
    }
}
