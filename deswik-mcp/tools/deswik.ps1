# Deswik MCP bridge client for PowerShell.
# Dot-source it, then call Invoke-Deswik:
#   . "G:\Claude_Projects\Deswik_MCP_Plugin\deswik-mcp\tools\deswik.ps1"
#   Invoke-Deswik get_schedule_info
#   Invoke-Deswik get_tasks @{ limit = 5 }
#   Invoke-Deswik get_task_fields @{ taskId = "815_183a681b4181"; fields = @("Name","Tonnes") }
#   Invoke-Deswik get_cad_layers | Where-Object entityCount -gt 0

function Invoke-Deswik {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position = 0)] [string] $Action,
        [Parameter(Position = 1)] [hashtable] $Params = @{},
        [string] $BridgeHost = "127.0.0.1",
        [int] $Port = 9595,
        [int] $TimeoutSec = 60
    )

    $client = [System.Net.Sockets.TcpClient]::new()
    try {
        $client.Connect($BridgeHost, $Port)
        $client.ReceiveTimeout = $TimeoutSec * 1000
        $stream = $client.GetStream()
        $writer = [System.IO.StreamWriter]::new($stream, [System.Text.UTF8Encoding]::new($false))
        $writer.NewLine = "`n"
        $reader = [System.IO.StreamReader]::new($stream, [System.Text.Encoding]::UTF8)

        $cmd = @{ id = [guid]::NewGuid().ToString(); action = $Action; params = $Params } |
            ConvertTo-Json -Depth 10 -Compress
        $writer.WriteLine($cmd)
        $writer.Flush()

        $line = $reader.ReadLine()
        if (-not $line) { throw "no response from bridge" }
        $resp = $line | ConvertFrom-Json
        if (-not $resp.success) {
            throw "$Action failed: $($resp.error) [$($resp.errorCode)]"
        }
        return $resp.data
    }
    finally {
        $client.Dispose()
    }
}

Set-Alias dsw Invoke-Deswik
