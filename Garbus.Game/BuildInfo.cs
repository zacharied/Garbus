using System.Linq;
using System.Reflection;
using osu.Framework;

namespace Garbus.Game
{
    /// <summary>
    /// Identifies the running build: the platform it was published for and the short git commit hash
    /// it was built from. The hash is stamped into this assembly at build time by the
    /// <c>EmbedGitCommit</c> target in <c>Directory.Build.props</c>; a source-only build with no git
    /// available reports <c>unknown</c>.
    /// </summary>
    public static class BuildInfo
    {
        /// <summary>The OS the build is running on, e.g. <c>Windows</c>.</summary>
        public static string Platform { get; } = RuntimeInfo.OS.ToString();

        /// <summary>The short git commit hash the build was compiled from, or <c>unknown</c>.</summary>
        public static string Commit { get; } = readCommit();

        /// <summary>A compact build code for display, e.g. <c>Windows @ 5531380</c>.</summary>
        public static string DisplayText { get; } = $"{Platform} @ {Commit}";

        private static string readCommit()
        {
            string? value = typeof(BuildInfo).Assembly
                                             .GetCustomAttributes<AssemblyMetadataAttribute>()
                                             .FirstOrDefault(a => a.Key == "GitCommit")?.Value;

            return string.IsNullOrEmpty(value) ? "unknown" : value;
        }
    }
}
