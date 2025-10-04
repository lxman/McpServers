﻿using Azure.Core;

namespace AzureMcp.Authentication.models;

public class AzureDiscoveryResult
{
    public TokenCredential? AzureCredential { get; set; }
    public List<DevOpsEnvironmentInfo> DevOpsEnvironments { get; set; } = [];
    public AzureCliInfo AzureCliInfo { get; set; } = new();
}