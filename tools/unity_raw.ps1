param(
    [string]$CommandFile,
    [string]$CommandJson,
    [int]$Port = 6400,
    [int]$TimeoutMs = 180000
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

function Read-Exact([System.Net.Sockets.NetworkStream]$ns, [int]$count, [int]$timeoutMs) {
    $buffer = New-Object byte[] $count
    $offset = 0
    $deadline = [DateTime]::UtcNow.AddMilliseconds($timeoutMs)
    while ($offset -lt $count) {
        $remaining = $count - $offset
        $left = [int]([DateTime]::UtcNow - $deadline).TotalMilliseconds
        $left = -$left
        if ($left -le 0) { throw [System.IO.IOException]::new("Read timed out after $timeoutMs ms") }
        $ns.ReadTimeout = $left
        $read = $ns.Read($buffer, $offset, $remaining)
        if ($read -le 0) { throw [System.IO.IOException]::new("Connection closed") }
        $offset += $read
    }
    return $buffer
}

function Write-Frame([System.Net.Sockets.NetworkStream]$ns, [byte[]]$payload) {
    $header = New-Object byte[] 8
    $len = [UInt64]$payload.Length
    for ($i = 7; $i -ge 0; $i--) {
        $header[$i] = [byte]($len -band 0xFF)
        $len = [UInt64]([math]::Floor($len / 256))
    }
    $ns.Write($header, 0, 8)
    $ns.Write($payload, 0, $payload.Length)
    $ns.Flush()
}

function Read-Frame([System.Net.Sockets.NetworkStream]$ns, [int]$timeoutMs) {
    $header = Read-Exact $ns 8 $timeoutMs
    $len = [UInt64]0
    for ($i = 0; $i -lt 8; $i++) { $len = ($len -shl 8) -bor [UInt64]$header[$i] }
    if ($len -gt [UInt64]100000000) { throw [System.IO.IOException]::new("Frame too large: $len") }
    $payload = Read-Exact $ns ([int]$len) $timeoutMs
    return [System.Text.Encoding]::UTF8.GetString($payload)
}

try {
    $client = New-Object System.Net.Sockets.TcpClient
    $client.NoDelay = $true
    $client.ReceiveTimeout = $TimeoutMs
    $client.SendTimeout = $TimeoutMs
    $client.Connect('127.0.0.1', $Port)
    $ns = $client.GetStream()

    # Welcome line
    $welcome = New-Object System.Text.StringBuilder
    while ($true) {
        $b = $ns.ReadByte()
        if ($b -le 0) { throw "Connection closed before welcome" }
        if ($b -eq 0x0A) { break }
        [void]$welcome.Append([char]$b)
    }
    $welcomeStr = $welcome.ToString()
    if (-not $welcomeStr.StartsWith('WELCOME UNITY-MCP')) {
        throw "Unexpected welcome: '$welcomeStr'"
    }

    $json = if ($CommandFile) { Get-Content $CommandFile -Raw -Encoding UTF8 } else { $CommandJson }
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
    Write-Frame $ns $bytes
    $response = Read-Frame $ns $TimeoutMs

    Write-Output $response

    try { $client.Close() } catch { }
    exit 0
} catch {
    Write-Output "ERROR: $($_.Exception.Message)"
    exit 1
}
