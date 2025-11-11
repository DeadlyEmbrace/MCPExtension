using Eto.Forms;
using Mendix.StudioPro.ExtensionsAPI.UI.WebView;
using Mendix.StudioPro.ExtensionsAPI.UI.DockablePane;
using Mendix.StudioPro.ExtensionsAPI.Model;
using Mendix.StudioPro.ExtensionsAPI.Model.Projects;
using System;
using System.Text.Json;
using System.Net.NetworkInformation;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace MCPExtension
{
    public class AIAPIEngineViewModel : WebViewDockablePaneViewModel
    {
        private readonly AIAPIEngine parentPanel;
        private IWebView? currentWebView;

        private bool IsPortInUse(int port)
        {
            try
            {
                IPGlobalProperties ipProperties = IPGlobalProperties.GetIPGlobalProperties();
                IPEndPoint[] ipEndPoints = ipProperties.GetActiveTcpListeners();
                return ipEndPoints.Any(endPoint => endPoint.Port == port);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private const string EMBEDDED_HTML = @"
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset=""UTF-8"">
            <title>MCP Server</title>
            <style>
                /* Light Theme (Default) */
                :root[data-theme=""light""] {
                    --bg-primary: #f5f7fa;
                    --bg-secondary: #ffffff;
                    --bg-tertiary: #e8edf2;
                    --text-primary: #1a202c;
                    --text-secondary: #4a5568;
                    --text-tertiary: #718096;
                    --border-color: #e2e8f0;
                    --shadow: 0 4px 6px rgba(0, 0, 0, 0.07);
                    --shadow-lg: 0 10px 25px rgba(0, 0, 0, 0.1);
                    --primary: #3b82f6;
                    --primary-hover: #2563eb;
                    --success: #10b981;
                    --success-glow: rgba(16, 185, 129, 0.2);
                    --danger: #ef4444;
                    --danger-hover: #dc2626;
                    --danger-glow: rgba(239, 68, 68, 0.2);
                    --warning: #f59e0b;
                    --status-bg: #f0f9ff;
                }

                /* Dark Theme */
                :root[data-theme=""dark""] {
                    --bg-primary: #0f172a;
                    --bg-secondary: #1e293b;
                    --bg-tertiary: #334155;
                    --text-primary: #f1f5f9;
                    --text-secondary: #cbd5e1;
                    --text-tertiary: #94a3b8;
                    --border-color: #334155;
                    --shadow: 0 4px 6px rgba(0, 0, 0, 0.3);
                    --shadow-lg: 0 10px 25px rgba(0, 0, 0, 0.5);
                    --primary: #60a5fa;
                    --primary-hover: #3b82f6;
                    --success: #34d399;
                    --success-glow: rgba(52, 211, 153, 0.3);
                    --danger: #f87171;
                    --danger-hover: #ef4444;
                    --danger-glow: rgba(248, 113, 113, 0.3);
                    --warning: #fbbf24;
                    --status-bg: #1e3a5f;
                }

                /* Auto-detect system preference */
                :root {
                    --bg-primary: #f5f7fa;
                    --bg-secondary: #ffffff;
                    --bg-tertiary: #e8edf2;
                    --text-primary: #1a202c;
                    --text-secondary: #4a5568;
                    --text-tertiary: #718096;
                    --border-color: #e2e8f0;
                    --shadow: 0 4px 6px rgba(0, 0, 0, 0.07);
                    --shadow-lg: 0 10px 25px rgba(0, 0, 0, 0.1);
                    --primary: #3b82f6;
                    --primary-hover: #2563eb;
                    --success: #10b981;
                    --success-glow: rgba(16, 185, 129, 0.2);
                    --danger: #ef4444;
                    --danger-hover: #dc2626;
                    --danger-glow: rgba(239, 68, 68, 0.2);
                    --warning: #f59e0b;
                    --status-bg: #f0f9ff;
                }

                @media (prefers-color-scheme: dark) {
                    :root:not([data-theme=""light""]) {
                        --bg-primary: #0f172a;
                        --bg-secondary: #1e293b;
                        --bg-tertiary: #334155;
                        --text-primary: #f1f5f9;
                        --text-secondary: #cbd5e1;
                        --text-tertiary: #94a3b8;
                        --border-color: #334155;
                        --shadow: 0 4px 6px rgba(0, 0, 0, 0.3);
                        --shadow-lg: 0 10px 25px rgba(0, 0, 0, 0.5);
                        --primary: #60a5fa;
                        --primary-hover: #3b82f6;
                        --success: #34d399;
                        --success-glow: rgba(52, 211, 153, 0.3);
                        --danger: #f87171;
                        --danger-hover: #ef4444;
                        --danger-glow: rgba(248, 113, 113, 0.3);
                        --warning: #fbbf24;
                        --status-bg: #1e3a5f;
                    }
                }

                * {
                    margin: 0;
                    padding: 0;
                    box-sizing: border-box;
                }

                body {
                    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', 'Roboto', 'Oxygen', 'Ubuntu', 'Cantarell', sans-serif;
                    background: var(--bg-primary);
                    color: var(--text-primary);
                    padding: 32px;
                    min-height: 100vh;
                    transition: background-color 0.3s ease, color 0.3s ease;
                }

                .container {
                    max-width: 800px;
                    margin: 0 auto;
                }

                .header {
                    display: flex;
                    align-items: center;
                    justify-content: space-between;
                    margin-bottom: 32px;
                }

                .title-section {
                    display: flex;
                    align-items: center;
                    gap: 16px;
                }

                h1 {
                    font-size: 28px;
                    font-weight: 700;
                    color: var(--text-primary);
                    letter-spacing: -0.5px;
                }

                .subtitle {
                    font-size: 14px;
                    color: var(--text-tertiary);
                    font-weight: 500;
                }

                .theme-toggle {
                    background: var(--bg-secondary);
                    border: 1px solid var(--border-color);
                    border-radius: 12px;
                    padding: 8px 12px;
                    cursor: pointer;
                    display: flex;
                    align-items: center;
                    gap: 8px;
                    color: var(--text-secondary);
                    transition: all 0.2s ease;
                    font-size: 14px;
                    font-weight: 500;
                }

                .theme-toggle:hover {
                    background: var(--bg-tertiary);
                    transform: translateY(-1px);
                }

                .status-card {
                    background: var(--bg-secondary);
                    border-radius: 16px;
                    padding: 32px;
                    box-shadow: var(--shadow);
                    border: 1px solid var(--border-color);
                    margin-bottom: 24px;
                    transition: all 0.3s ease;
                }

                .status-row {
                    display: flex;
                    align-items: center;
                    gap: 16px;
                    margin-bottom: 24px;
                    padding: 20px;
                    background: var(--status-bg);
                    border-radius: 12px;
                    border: 1px solid var(--border-color);
                }

                .status-indicator {
                    width: 16px;
                    height: 16px;
                    border-radius: 50%;
                    transition: all 0.3s ease;
                    position: relative;
                }

                .status-indicator::before {
                    content: '';
                    position: absolute;
                    inset: -4px;
                    border-radius: 50%;
                    opacity: 0;
                    transition: opacity 0.3s ease;
                }

                .status-running {
                    background: var(--success);
                    animation: pulse 2s ease-in-out infinite;
                }

                .status-running::before {
                    background: var(--success-glow);
                    opacity: 1;
                    animation: pulse-ring 2s ease-in-out infinite;
                }

                .status-stopped {
                    background: var(--danger);
                }

                .status-stopped::before {
                    background: var(--danger-glow);
                    opacity: 0.5;
                }

                @keyframes pulse {
                    0%, 100% { opacity: 1; }
                    50% { opacity: 0.8; }
                }

                @keyframes pulse-ring {
                    0% { transform: scale(1); opacity: 0.8; }
                    50% { transform: scale(1.5); opacity: 0.4; }
                    100% { transform: scale(1); opacity: 0.8; }
                }

                .status-label {
                    font-size: 18px;
                    font-weight: 600;
                    color: var(--text-primary);
                }

                .status-value {
                    font-size: 16px;
                    color: var(--text-secondary);
                    margin-left: auto;
                    font-weight: 500;
                }

                .button-group {
                    display: flex;
                    gap: 12px;
                    margin-bottom: 20px;
                }

                button {
                    flex: 1;
                    padding: 14px 24px;
                    border: none;
                    border-radius: 12px;
                    font-size: 16px;
                    font-weight: 600;
                    cursor: pointer;
                    transition: all 0.2s ease;
                    box-shadow: var(--shadow);
                    position: relative;
                    overflow: hidden;
                }

                button::before {
                    content: '';
                    position: absolute;
                    inset: 0;
                    background: linear-gradient(135deg, rgba(255,255,255,0.2) 0%, rgba(255,255,255,0) 100%);
                    opacity: 0;
                    transition: opacity 0.2s ease;
                }

                button:hover::before {
                    opacity: 1;
                }

                #startButton {
                    background: linear-gradient(135deg, var(--primary) 0%, var(--primary-hover) 100%);
                    color: white;
                }

                #startButton:hover:not(:disabled) {
                    transform: translateY(-2px);
                    box-shadow: var(--shadow-lg);
                }

                #stopButton {
                    background: linear-gradient(135deg, var(--danger) 0%, var(--danger-hover) 100%);
                    color: white;
                }

                #stopButton:hover:not(:disabled) {
                    transform: translateY(-2px);
                    box-shadow: var(--shadow-lg);
                }

                button:disabled {
                    opacity: 0.5;
                    cursor: not-allowed;
                    transform: none !important;
                }

                #status {
                    padding: 16px;
                    border-radius: 10px;
                    font-size: 14px;
                    font-weight: 500;
                    display: none;
                    margin-top: 16px;
                    animation: slideIn 0.3s ease;
                }

                @keyframes slideIn {
                    from {
                        opacity: 0;
                        transform: translateY(-10px);
                    }
                    to {
                        opacity: 1;
                        transform: translateY(0);
                    }
                }

                .success {
                    background: linear-gradient(135deg, rgba(16, 185, 129, 0.15) 0%, rgba(16, 185, 129, 0.05) 100%);
                    color: var(--success);
                    border: 1px solid var(--success);
                }

                .error {
                    background: linear-gradient(135deg, rgba(239, 68, 68, 0.15) 0%, rgba(239, 68, 68, 0.05) 100%);
                    color: var(--danger);
                    border: 1px solid var(--danger);
                }

                .info {
                    background: linear-gradient(135deg, rgba(59, 130, 246, 0.15) 0%, rgba(59, 130, 246, 0.05) 100%);
                    color: var(--primary);
                    border: 1px solid var(--primary);
                }

                .info-section {
                    background: var(--bg-tertiary);
                    border-radius: 12px;
                    padding: 20px;
                    margin-top: 24px;
                }

                .info-title {
                    font-size: 14px;
                    font-weight: 600;
                    color: var(--text-secondary);
                    margin-bottom: 12px;
                    text-transform: uppercase;
                    letter-spacing: 0.5px;
                }

                .info-item {
                    display: flex;
                    align-items: center;
                    gap: 8px;
                    font-size: 14px;
                    color: var(--text-secondary);
                    padding: 8px 0;
                    border-bottom: 1px solid var(--border-color);
                }

                .info-item:last-child {
                    border-bottom: none;
                }

                .info-item-label {
                    font-weight: 500;
                    min-width: 100px;
                }

                .info-item-value {
                    font-family: 'Courier New', monospace;
                    color: var(--primary);
                }

                /* Icon styles */
                .icon {
                    width: 20px;
                    height: 20px;
                    display: inline-block;
                }
            </style>

            <script>
                function handleMessageFromHost(event) {
                    console.log('=== handleMessageFromHost called ===');
                    const eventData = event.data;
                    console.log('Received event:', event);
                    console.log('Event data:', eventData);
                    console.log('Data type:', typeof eventData);
                    
                    // According to Mendix API docs event.data should have message and data properties
                    const message = eventData.message || eventData;
                    console.log('Extracted message:', message);
                    
                    if (message === 'Running') {
                        console.log('✓ Handling Running message');
                        const indicator = document.getElementById('statusIndicator');
                        const statusText = document.getElementById('statusValue');
                        const startButton = document.getElementById('startButton');
                        const stopButton = document.getElementById('stopButton');
            
                        indicator.className = 'status-indicator status-running';
                        statusText.textContent = 'Running';
                        startButton.disabled = true;
                        stopButton.disabled = false;
            
                        const statusDiv = document.getElementById('status');
                        statusDiv.textContent = '✓ MCP Server started successfully';
                        statusDiv.className = 'success';
                        statusDiv.style.display = 'block';
                        console.log('✓ UI updated for Running state');
                    } 
                    else if (message === 'NotRunning') {
                        console.log('✓ Handling NotRunning message');
                        const indicator = document.getElementById('statusIndicator');
                        const statusText = document.getElementById('statusValue');
                        const startButton = document.getElementById('startButton');
                        const stopButton = document.getElementById('stopButton');
            
                        indicator.className = 'status-indicator status-stopped';
                        statusText.textContent = 'Stopped';
                        startButton.disabled = false;
                        stopButton.disabled = true;
            
                        const statusDiv = document.getElementById('status');
                        statusDiv.textContent = '✓ MCP Server stopped';
                        statusDiv.className = 'success';
                        statusDiv.style.display = 'block';
                        console.log('✓ UI updated for NotRunning state');
                    }
                    else if (message === 'Error') {
                        console.log('✓ Handling Error message');
                        const indicator = document.getElementById('statusIndicator');
                        const statusText = document.getElementById('statusValue');
                        const startButton = document.getElementById('startButton');
                        const stopButton = document.getElementById('stopButton');
            
                        indicator.className = 'status-indicator status-stopped';
                        statusText.textContent = 'Stopped';
                        startButton.disabled = false;
                        stopButton.disabled = true;
            
                        const statusDiv = document.getElementById('status');
                        statusDiv.textContent = '⚠ Failed to start MCP Server. Port may be in use. Try stopping any existing server first.';
                        statusDiv.className = 'error';
                        statusDiv.style.display = 'block';
                        console.log('✓ UI updated for Error state');
                    }
                    else {
                        console.warn('⚠️ No matching message handler for:', message);
                        console.log('Full event data:', JSON.stringify(eventData));
                    }
                }

                function updateServerStatus(status) {
                    const indicator = document.getElementById('statusIndicator');
                    const statusText = document.getElementById('statusValue');
                    const startButton = document.getElementById('startButton');
                    const stopButton = document.getElementById('stopButton');
        
                    if (status === true || status === 'running') {
                        indicator.className = 'status-indicator status-running';
                        statusText.textContent = 'Running';
                        startButton.disabled = true;
                        stopButton.disabled = false;
                    } else if (status === 'starting') {
                        indicator.className = 'status-indicator';
                        indicator.style.backgroundColor = '#ffc107';
                        statusText.textContent = 'Starting...';
                        startButton.disabled = true;
                        stopButton.disabled = true;
                    } else if (status === 'stopping') {
                        indicator.className = 'status-indicator';
                        indicator.style.backgroundColor = '#ffc107';
                        statusText.textContent = 'Stopping...';
                        startButton.disabled = true;
                        stopButton.disabled = true;
                    } else {
                        indicator.className = 'status-indicator status-stopped';
                        statusText.textContent = 'Stopped';
                        startButton.disabled = false;
                        stopButton.disabled = true;
                    }
                }

                function toggleTheme() {
                    const root = document.documentElement;
                    const currentTheme = root.getAttribute('data-theme');
                    const newTheme = currentTheme === 'dark' ? 'light' : 'dark';
                    root.setAttribute('data-theme', newTheme);
                    
                    // Save preference (wrapped in try-catch for data: URLs)
                    try {
                        localStorage.setItem('theme', newTheme);
                    } catch (e) {
                        console.warn('localStorage not available:', e.message);
                    }
                    
                    // Update button text
                    const btn = document.getElementById('themeToggle');
                    btn.textContent = newTheme === 'dark' ? '☀️ Light Mode' : '🌙 Dark Mode';
                }

                function init() {
                    console.log('init() called');
                    
                    // Load saved theme preference or use system preference
                    try {
                        const savedTheme = localStorage.getItem('theme');
                        if (savedTheme) {
                            document.documentElement.setAttribute('data-theme', savedTheme);
                            const btn = document.getElementById('themeToggle');
                            btn.textContent = savedTheme === 'dark' ? '☀️ Light Mode' : '🌙 Dark Mode';
                        } else if (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches) {
                            document.documentElement.setAttribute('data-theme', 'dark');
                            const btn = document.getElementById('themeToggle');
                            btn.textContent = '☀️ Light Mode';
                        }
                    } catch (e) {
                        console.warn('localStorage not available, using default theme:', e.message);
                        // Use system preference as fallback
                        if (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches) {
                            document.documentElement.setAttribute('data-theme', 'dark');
                            const btn = document.getElementById('themeToggle');
                            btn.textContent = '☀️ Light Mode';
                        }
                    }
                    
                    console.log('About to register message listener');
                    window.chrome.webview.addEventListener('message', handleMessageFromHost);
                    console.log('Sending MessageListenerRegistered');
                    chrome.webview.postMessage({ message: 'MessageListenerRegistered' });
                    console.log('init() completed');
                }

                function startEngine() {
                    const statusDiv = document.getElementById('status');
                    statusDiv.textContent = 'Starting MCP Server...';
                    statusDiv.className = 'info';
                    statusDiv.style.display = 'block';
                    chrome.webview.postMessage({ message: 'startEngine' });
                }

                function stopEngine() {
                    const statusDiv = document.getElementById('status');
                    statusDiv.textContent = 'Stopping MCP Server...';
                    statusDiv.className = 'info';
                    statusDiv.style.display = 'block';
                    chrome.webview.postMessage({ message: 'stopEngine' });
                }
            </script>
        </head>
        <body onload=""init()"">
            <div class=""container"">
                <div class=""header"">
                    <div class=""title-section"">
                        <div>
                            <h1>MCP Server</h1>
                            <div class=""subtitle"">Model Context Protocol Server (HTTP/SSE)</div>
                        </div>
                    </div>
                    <button class=""theme-toggle"" id=""themeToggle"" onclick=""toggleTheme()"">
                        🌙 Dark Mode
                    </button>
                </div>

                <div class=""status-card"">
                    <div class=""status-row"">
                        <span id=""statusIndicator"" class=""status-indicator status-stopped""></span>
                        <span class=""status-label"">Server Status</span>
                        <span id=""statusValue"" class=""status-value"">Stopped</span>
                    </div>

                    <div class=""button-group"">
                        <button id=""startButton"" onclick=""startEngine()"">
                            ▶ Start Server
                        </button>
                        <button id=""stopButton"" onclick=""stopEngine()"" disabled>
                            ⏹ Stop Server
                        </button>
                    </div>

                    <div id=""status""></div>

                    <div class=""info-section"">
                        <div class=""info-title"">Server Information</div>
                        <div class=""info-item"">
                            <span class=""info-item-label"">Protocol</span>
                            <span class=""info-item-value"">HTTP/SSE</span>
                        </div>
                        <div class=""info-item"">
                            <span class=""info-item-label"">Port</span>
                            <span class=""info-item-value"">3001</span>
                        </div>
                        <div class=""info-item"">
                            <span class=""info-item-label"">Endpoint</span>
                            <span class=""info-item-value"">http://localhost:3001</span>
                        </div>
                    </div>
                </div>
            </div>
        </body>
        </html>";
        public AIAPIEngineViewModel(string title, AIAPIEngine panel) : base()
        {
            Title = title;
            parentPanel = panel;
        }

        private string GetLogFilePath()
        {
            try
            {
                // Use the Mendix project directory instead of the extension assembly location
                var project = parentPanel.CurrentAppModel.Root as IProject;
                if (project?.DirectoryPath == null)
                {
                    throw new InvalidOperationException("Could not determine Mendix project directory");
                }

                string resourcesDir = System.IO.Path.Combine(project.DirectoryPath, "resources");
                if (!System.IO.Directory.Exists(resourcesDir))
                {
                    System.IO.Directory.CreateDirectory(resourcesDir);
                }
                
                return System.IO.Path.Combine(resourcesDir, "mcp_debug.log");
            }
            catch (Exception ex)
            {
                // Fallback to current directory if we can't determine project directory
                System.Diagnostics.Debug.WriteLine($"Could not determine log file path: {ex.Message}");
                return System.IO.Path.Combine(Environment.CurrentDirectory, "mcp_debug.log");
            }
        }

        private void WebView_MessageReceived(object? sender, MessageReceivedEventArgs e)
        {
            try
            {
                // Log to a file we can check
                var logMessage = $"[{DateTime.Now:HH:mm:ss.fff}] WebView received message: {e.Message}";
                System.IO.File.AppendAllText(GetLogFilePath(), logMessage + Environment.NewLine);
                
                if (e.Message.Contains("MessageListenerRegistered"))
                {
                    System.IO.File.AppendAllText(GetLogFilePath(), "[MessageListenerRegistered] Checking server status..." + Environment.NewLine);
                    
                    // Check if MCP server is running by checking the actual server status
                    var isRunning = parentPanel.McpServer?.IsRunning ?? false;
                    System.IO.File.AppendAllText(GetLogFilePath(), $"[MessageListenerRegistered] MCP Server IsRunning: {isRunning}" + Environment.NewLine);
                    
                    var messageToSend = isRunning ? "Running" : "NotRunning";
                    System.IO.File.AppendAllText(GetLogFilePath(), $"[MessageListenerRegistered] Sending initial message: {messageToSend}" + Environment.NewLine);
                    currentWebView?.PostMessage(messageToSend, null);
                    return;
                }

                if (e.Message.Contains("startEngine"))
                {
                    System.IO.File.AppendAllText(GetLogFilePath(), "[startEngine] Command received" + Environment.NewLine);
                    
                    // Run on background thread to avoid blocking UI
                    Task.Run(async () =>
                    {
                        try
                        {
                            System.IO.File.AppendAllText(GetLogFilePath(), "[startEngine] Starting server on background thread..." + Environment.NewLine);
                            string result = await parentPanel.StartAPIEngineAsync();
                            System.IO.File.AppendAllText(GetLogFilePath(), $"[startEngine] StartAPIEngineAsync result: {result}" + Environment.NewLine);
                            
                            // Check actual server status after start
                            var isRunning = parentPanel.McpServer?.IsRunning ?? false;
                            var messageToSend = isRunning ? "Running" : "NotRunning";
                            System.IO.File.AppendAllText(GetLogFilePath(), $"[startEngine] Server IsRunning: {isRunning}" + Environment.NewLine);
                            System.IO.File.AppendAllText(GetLogFilePath(), $"[startEngine] Sending message after start: {messageToSend}" + Environment.NewLine);
                            
                            // Check if there was an error in the result (port in use, etc.)
                            if (result.Contains("Error") || result.Contains("address already in use"))
                            {
                                messageToSend = "Error";
                                System.IO.File.AppendAllText(GetLogFilePath(), $"[startEngine] Error detected in result, sending Error message" + Environment.NewLine);
                            }
                            
                            // Post message back to UI thread
                            Application.Instance.Invoke(() =>
                            {
                                System.IO.File.AppendAllText(GetLogFilePath(), $"[startEngine] About to call PostMessage with message: {messageToSend}" + Environment.NewLine);
                                
                                // PostMessage expects (message, data) parameters
                                // The JavaScript will receive event.data = { message: "Running", data: null }
                                currentWebView?.PostMessage(messageToSend, null);
                                
                                System.IO.File.AppendAllText(GetLogFilePath(), $"[startEngine] PostMessage completed" + Environment.NewLine);
                            });
                        }
                        catch (Exception ex)
                        {
                            System.IO.File.AppendAllText(GetLogFilePath(), $"[startEngine] Exception in background thread: {ex.Message}" + Environment.NewLine);
                            Application.Instance.Invoke(() =>
                            {
                                currentWebView?.PostMessage("Error", null);
                            });
                        }
                    });
                }
                else if (e.Message.Contains("stopEngine"))
                {
                    System.IO.File.AppendAllText(GetLogFilePath(), "[stopEngine] Command received" + Environment.NewLine);
                    
                    // Run on background thread to avoid blocking UI
                    Task.Run(async () =>
                    {
                        try
                        {
                            System.IO.File.AppendAllText(GetLogFilePath(), "[stopEngine] Stopping server on background thread..." + Environment.NewLine);
                            string result = await parentPanel.StopAPIEngineAsync();
                            System.IO.File.AppendAllText(GetLogFilePath(), $"[stopEngine] StopAPIEngineAsync result: {result}" + Environment.NewLine);
                            
                            // Check actual server status after stop
                            var isRunning = parentPanel.McpServer?.IsRunning ?? false;
                            var messageToSend = isRunning ? "Running" : "NotRunning";
                            System.IO.File.AppendAllText(GetLogFilePath(), $"[stopEngine] Sending message after stop: {messageToSend}" + Environment.NewLine);
                            
                            // Post message back to UI thread
                            Application.Instance.Invoke(() =>
                            {
                                currentWebView?.PostMessage(messageToSend, null);
                            });
                        }
                        catch (Exception ex)
                        {
                            System.IO.File.AppendAllText(GetLogFilePath(), $"[stopEngine] Exception in background thread: {ex.Message}" + Environment.NewLine);
                            Application.Instance.Invoke(() =>
                            {
                                currentWebView?.PostMessage("NotRunning", null);
                            });
                        }
                    });
                }
                else if (e.Message.Contains("showDevTools"))
                {
                    System.IO.File.AppendAllText(GetLogFilePath(), "[showDevTools] Opening developer tools..." + Environment.NewLine);
                    Application.Instance.Invoke(() =>
                    {
                        currentWebView?.ShowDevTools();
                    });
                }
            }
            catch (Exception ex)
            {
                var errorMessage = $"[{DateTime.Now:HH:mm:ss.fff}] Exception in WebView_MessageReceived: {ex.Message}";
                System.IO.File.AppendAllText(GetLogFilePath(), errorMessage + Environment.NewLine);
                MessageBox.Show($"Error handling message from WebView: {ex.Message}\nStack trace: {ex.StackTrace}");
                currentWebView?.PostMessage("NotRunning");
            }
        }

        private void UpdateStatus(string message)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"UpdateStatus called with: {message}");
                // Send status message to JavaScript for display
                currentWebView?.PostMessage($"Status|{message}");
                System.Diagnostics.Debug.WriteLine($"Sent status message: Status|{message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating status: {ex.Message}");
            }
        }

        public override void InitWebView(IWebView webView)
        {
            try
            {
                currentWebView = webView;
                webView.MessageReceived -= WebView_MessageReceived;
                webView.MessageReceived += WebView_MessageReceived;

                string htmlContent = Uri.EscapeDataString(EMBEDDED_HTML);
                webView.Address = new Uri($"data:text/html,{htmlContent}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing WebView: {ex.Message}\nStack trace: {ex.StackTrace}");
            }
        }

        // Methods to notify UI of server state changes from background tasks
        public void NotifyServerStarted(string connectionInfo)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"NotifyServerStarted called with: {connectionInfo}");
                UpdateStatus($"MCP Server started successfully");
                currentWebView?.PostMessage("Running", null);
                
                // Also log that we sent the Running message
                System.Diagnostics.Debug.WriteLine("Sent 'Running' message to WebView");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in NotifyServerStarted: {ex.Message}");
                UpdateStatus($"Error notifying server started: {ex.Message}");
            }
        }

        public void NotifyServerStartFailed(string errorMessage)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"NotifyServerStartFailed called with: {errorMessage}");
                UpdateStatus($"Failed to start MCP Server: {errorMessage}");
                currentWebView?.PostMessage("NotRunning", null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in NotifyServerStartFailed: {ex.Message}");
                UpdateStatus($"Error notifying server start failed: {ex.Message}");
            }
        }

        public void NotifyServerStopped(string message)
        {
            try
            {
                UpdateStatus("MCP Server stopped");
                currentWebView?.PostMessage("NotRunning", null);
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error notifying server stopped: {ex.Message}");
            }
        }

        public void NotifyServerStopFailed(string errorMessage)
        {
            try
            {
                UpdateStatus($"Failed to stop MCP Server: {errorMessage}");
                currentWebView?.PostMessage("Running");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error notifying server stop failed: {ex.Message}");
            }
        }
    }
}