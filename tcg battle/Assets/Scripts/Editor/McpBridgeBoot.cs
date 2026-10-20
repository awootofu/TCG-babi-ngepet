#if UNITY_EDITOR
using UnityEditor;

namespace TcgBattle.EditorTools
{
    /// <summary>
    /// Boots the MCP-for-Unity stdio bridge so external tooling (the OpenCode MCP server
    /// acting as a client) can discover and drive this editor instance. This mirrors what
    /// the MCP setup window and CI pipeline do (McpCiBoot.StartStdioForCi); the plugin's own
    /// StdioBridgeReloadHandler keeps it alive across domain reloads afterwards.
    /// </summary>
    [InitializeOnLoad]
    internal static class McpBridgeBoot
    {
        static McpBridgeBoot()
        {
            try
            {
                MCPForUnity.Editor.McpCiBoot.StartStdioForCi();
                UnityEngine.Debug.Log("[McpBridgeBoot] Requested MCP-for-Unity stdio bridge auto-connect.");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("[McpBridgeBoot] Could not start MCP bridge: " + e.Message);
            }
        }
    }
}
#endif
