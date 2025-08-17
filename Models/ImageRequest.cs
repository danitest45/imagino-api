namespace Imagino.Api.Models
{
    public class ImageRequest
    {
        public class ImageGenerationRequest
        {
            public string Prompt { get; set; } = string.Empty;
            public string NegativePrompt { get; set; } = string.Empty;
            public List<string> Styles { get; set; } = new() { "string" };
            public int Seed { get; set; } = -1;
            public int Subseed { get; set; } = -1;
            public float SubseedStrength { get; set; } = 0;
            public int SeedResizeFromH { get; set; } = -1;
            public int SeedResizeFromW { get; set; } = -1;
            public string SamplerName { get; set; } = "string";
            public string Scheduler { get; set; } = "string";
            public int BatchSize { get; set; } = 1;
            public int NIter { get; set; } = 1;
            public int Steps { get; set; } = 50;
            public float CfgScale { get; set; } = 7;
            public int Width { get; set; } = 512;
            public int Height { get; set; } = 512;
            public bool RestoreFaces { get; set; } = true;
            public bool Tiling { get; set; } = true;
            public bool DoNotSaveSamples { get; set; } = false;
            public bool DoNotSaveGrid { get; set; } = false;
            public float Eta { get; set; } = 0;
            public float DenoisingStrength { get; set; } = 0;
            public float SMinUncond { get; set; } = 0;
            public float SChurn { get; set; } = 0;
            public float STmax { get; set; } = 0;
            public float STmin { get; set; } = 0;
            public float SNoise { get; set; } = 0;
            public object OverrideSettings { get; set; } = new();
            public bool OverrideSettingsRestoreAfterwards { get; set; } = true;
            public string RefinerCheckpoint { get; set; } = "string";
            public float RefinerSwitchAt { get; set; } = 0;
            public bool DisableExtraNetworks { get; set; } = false;
            public string FirstpassImage { get; set; } = "string";
            public object Comments { get; set; } = new();
            public bool EnableHr { get; set; } = false;
            public int FirstphaseWidth { get; set; } = 0;
            public int FirstphaseHeight { get; set; } = 0;
            public float HrScale { get; set; } = 2;
            public string HrUpscaler { get; set; } = "string";
            public int HrSecondPassSteps { get; set; } = 0;
            public int HrResizeX { get; set; } = 0;
            public int HrResizeY { get; set; } = 0;
            public string HrCheckpointName { get; set; } = "string";
            public string HrSamplerName { get; set; } = "string";
            public string HrScheduler { get; set; } = "string";
            public string HrPrompt { get; set; } = "";
            public string HrNegativePrompt { get; set; } = "";
            public string ForceTaskId { get; set; } = "string";
            public string SamplerIndex { get; set; } = "Euler";
            public string ScriptName { get; set; } = "string";
            public List<object> ScriptArgs { get; set; } = new();
            public bool SendImages { get; set; } = true;
            public bool SaveImages { get; set; } = false;
            public object AlwaysonScripts { get; set; } = new();
            public string Infotext { get; set; } = "string";
        }

    }
}
