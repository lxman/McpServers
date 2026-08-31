#requires -Version 7
<#
.SYNOPSIS
Publish then activate. The one command to upgrade a server without stopping any client.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $Server,
    [string] $Configuration = 'Release',
    [string] $GatewayUrl = 'http://127.0.0.1:7300'
)

$ErrorActionPreference = 'Stop'

$version = & (Join-Path $PSScriptRoot 'publish.ps1') -Server $Server -Configuration $Configuration
& (Join-Path $PSScriptRoot 'activate.ps1') -Server $Server -Version $version -GatewayUrl $GatewayUrl
