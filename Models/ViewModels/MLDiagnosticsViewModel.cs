namespace Freelancing.Models.ViewModels
{
    public class MLDiagnosticsViewModel
    {
        public bool IsRandomForestAvailable { get; set; }
        public string CacheStatus { get; set; } = "";
        public string FlaskApiUrl { get; set; } = "";
        public bool IsRailwayEnvironment { get; set; }
        public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
    }
}
