# Azure Resource Drift Detector

> **Know when reality diverges from your IaC**

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

A CLI tool that detects configuration drift in Azure resources by comparing live deployed state against your Bicep/ARM templates. Perfect for catching portal clicks, emergency fixes, or misconfigured pipelines before they cause deployment failures.

## Features

- **Bicep & ARM Support** - Parse both `.bicep` and ARM JSON templates
- **Azure Resource Graph** - Fast queries across subscriptions using Azure Resource Graph
- **Smart Diff Engine** - Configurable ignore rules for system-managed properties
- **Multiple Output Formats** - Console (colored), JSON, or Markdown reports
- **Azure DevOps Integration** - Post drift reports as PR comments automatically
- **Slack & Teams Alerts** - Send notifications when drift is detected
- **CI/CD Ready** - Exit codes and `--fail-on-drift` for pipeline integration

## Installation

### As a .NET Global Tool

```bash
dotnet tool install --global azure-drift-detector
```

### From Source

```bash
git clone https://github.com/your-org/azure-drift-detector.git
cd azure-drift-detector
dotnet build
dotnet pack src/AzureDriftDetector.Cli -c Release
dotnet tool install --global --add-source ./src/AzureDriftDetector.Cli/bin/Release azure-drift-detector
```

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Azure CLI (`az login`) or other Azure credentials
- [Bicep CLI](https://learn.microsoft.com/azure/azure-resource-manager/bicep/install) (for `.bicep` files)

### Basic Usage

```bash
# Detect drift and show results in console
azure-drift-detector detect \
  --template ./infra/main.bicep \
  --subscription your-subscription-id \
  --resource-group your-resource-group

# Output as JSON
azure-drift-detector detect \
  --template ./infra/main.bicep \
  --subscription your-subscription-id \
  --resource-group your-resource-group \
  --output json

# Save report to file
azure-drift-detector detect \
  --template ./infra/main.bicep \
  --subscription your-subscription-id \
  --resource-group your-resource-group \
  --output markdown \
  --output-file drift-report.md

# Fail CI pipeline if drift detected
azure-drift-detector detect \
  --template ./infra/main.bicep \
  --subscription your-subscription-id \
  --resource-group your-resource-group \
  --fail-on-drift
```

## Commands

### `detect`

Core drift detection command.

```bash
azure-drift-detector detect [options]

Options:
  -t, --template <path>        Path to Bicep or ARM template file (required)
  -s, --subscription <id>      Azure subscription ID (required)
  -g, --resource-group <name>  Azure resource group name (required)
  -o, --output <format>        Output format: console, json, markdown [default: console]
  --output-file <path>         Write output to file instead of stdout
  --fail-on-drift              Exit with code 1 if drift is detected
  -p, --parameter <key=value>  Template parameters (can specify multiple)
  -v, --verbose                Enable verbose output
```

### `devops`

Post drift report as an Azure DevOps pull request comment.

```bash
azure-drift-detector devops [options]

Options:
  -t, --template <path>        Path to Bicep or ARM template file (required)
  -s, --subscription <id>      Azure subscription ID (required)
  -g, --resource-group <name>  Azure resource group name (required)
  --org-url <url>              Azure DevOps organization URL (required)
  --project <name>             Azure DevOps project name (required)
  --pr-id <id>                 Pull request ID (required)
  --token <pat>                Azure DevOps PAT (or set AZURE_DEVOPS_PAT env var)
  --comment-id <id>            Identifier for upsert behavior
```

### `notify`

Send drift notifications to Slack and/or Microsoft Teams.

```bash
azure-drift-detector notify [options]

Options:
  -t, --template <path>        Path to Bicep or ARM template file (required)
  -s, --subscription <id>      Azure subscription ID (required)
  -g, --resource-group <name>  Azure resource group name (required)
  --slack-webhook <url>        Slack webhook URL (or set SLACK_WEBHOOK_URL env var)
  --teams-webhook <url>        Teams webhook URL (or set TEAMS_WEBHOOK_URL env var)
  --only-on-drift              Only send notification if drift is detected
```

### `init`

Create a sample configuration file.

```bash
azure-drift-detector init [--directory <path>]
```

## Configuration

Create a `.driftdetector.json` file to customize drift detection behavior:

```json
{
  "ignoreRules": [
    {
      "pattern": "properties.provisioningState",
      "reason": "Azure-managed provisioning state"
    },
    {
      "pattern": "properties.createdTime",
      "reason": "Timestamp managed by Azure"
    },
    {
      "pattern": "properties.**.*Id",
      "reason": "Azure-generated IDs"
    },
    {
      "pattern": "tags.LastDeployment",
      "resourceType": "Microsoft.Web/sites",
      "reason": "Deployment timestamp tag on App Services"
    }
  ],
  "reportAddedProperties": true,
  "maxPropertyDepth": 10,
  "treatNullAsMissing": true
}
```

### Ignore Rule Patterns

| Pattern | Description |
|---------|-------------|
| `properties.name` | Exact match |
| `properties.*.id` | Single-level wildcard (matches `properties.network.id`) |
| `properties.**` | Multi-level wildcard (matches any depth) |

## CI/CD Integration

### Azure DevOps Pipeline

```yaml
trigger:
  - main

pool:
  vmImage: 'ubuntu-latest'

steps:
  - task: UseDotNet@2
    inputs:
      version: '10.x'

  - script: dotnet tool install --global azure-drift-detector
    displayName: 'Install Drift Detector'

  - task: AzureCLI@2
    inputs:
      azureSubscription: 'your-service-connection'
      scriptType: 'bash'
      scriptLocation: 'inlineScript'
      inlineScript: |
        azure-drift-detector detect \
          --template ./infra/main.bicep \
          --subscription $(subscriptionId) \
          --resource-group $(resourceGroup) \
          --fail-on-drift
    displayName: 'Check for Drift'

  # Post results to PR (optional)
  - task: AzureCLI@2
    condition: eq(variables['Build.Reason'], 'PullRequest')
    inputs:
      azureSubscription: 'your-service-connection'
      scriptType: 'bash'
      scriptLocation: 'inlineScript'
      inlineScript: |
        azure-drift-detector devops \
          --template ./infra/main.bicep \
          --subscription $(subscriptionId) \
          --resource-group $(resourceGroup) \
          --org-url $(System.CollectionUri) \
          --project $(System.TeamProject) \
          --pr-id $(System.PullRequest.PullRequestId) \
          --token $(System.AccessToken) \
          --comment-id "drift-detector"
    displayName: 'Post Drift Report to PR'
```

### GitHub Actions

```yaml
name: Drift Detection

on:
  schedule:
    - cron: '0 6 * * *'  # Daily at 6 AM
  workflow_dispatch:

jobs:
  detect-drift:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.x'

      - name: Install Drift Detector
        run: dotnet tool install --global azure-drift-detector

      - name: Azure Login
        uses: azure/login@v2
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}

      - name: Check for Drift
        run: |
          azure-drift-detector detect \
            --template ./infra/main.bicep \
            --subscription ${{ secrets.AZURE_SUBSCRIPTION_ID }} \
            --resource-group my-resource-group \
            --output json \
            --output-file drift-report.json

      - name: Notify on Drift
        if: failure()
        run: |
          azure-drift-detector notify \
            --template ./infra/main.bicep \
            --subscription ${{ secrets.AZURE_SUBSCRIPTION_ID }} \
            --resource-group my-resource-group \
            --slack-webhook ${{ secrets.SLACK_WEBHOOK_URL }} \
            --only-on-drift
```

## Authentication

The tool uses `DefaultAzureCredential` from the Azure SDK, which automatically tries these methods in order:

1. Environment variables (`AZURE_CLIENT_ID`, `AZURE_CLIENT_SECRET`, `AZURE_TENANT_ID`)
2. Workload Identity (for Kubernetes)
3. Managed Identity (for Azure VMs, App Service, etc.)
4. Azure CLI (`az login`)
5. Azure PowerShell
6. Visual Studio / VS Code credentials

For CI/CD, we recommend using a Service Principal or Managed Identity with the following permissions:
- `Reader` role on the target subscription/resource group
- `Microsoft.ResourceGraph/resources/read` permission

## Sample Output

### Console Output

The CLI uses [Spectre.Console](https://spectreconsole.net/) for rich, colorful terminal output:

```
╔══════════════════════════════════════╗
║   Azure Resource Drift Report        ║
╚══════════════════════════════════════╝

Template:        ./infra/main.bicep
Subscription:    12345678-1234-1234-1234-123456789abc
Resource Group:  my-production-rg
Generated:       2024-01-15 10:30:00 UTC

                    Summary
╭──────────────────┬───────╮
│      Status      │ Count │
├──────────────────┼───────┤
│ ✓ In Sync        │   3   │
│ ✗ Drifted        │   1   │
│ ⚠ Missing        │   1   │
│ ? Unmanaged      │   0   │
╰──────────────────┴───────╯

─── Drift Details ─────────────────────────────────

╭─ ✗ DRIFTED ──────────────────────────────────────╮
│ my-storage-account                               │
│ Microsoft.Storage/storageAccounts                │
╰──────────────────────────────────────────────────╯
 Type     │ Property                      │ Expected │ Actual
──────────┼───────────────────────────────┼──────────┼────────
 ~ Modified│ properties.supportsHttpsOnly │ true     │ false

╭─ ⚠ MISSING ──────────────────────────────────────╮
│ my-app-insights                                  │
│ Microsoft.Insights/components                    │
╰──────────────────────────────────────────────────╯
```

> **Note**: Colors are displayed in terminals that support ANSI colors. Green for in-sync/expected values, red for drifted/actual values, yellow for missing resources.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Development

```bash
# Clone the repo
git clone https://github.com/your-org/azure-drift-detector.git
cd azure-drift-detector

# Build
dotnet build

# Run tests
dotnet test

# Run the CLI locally
dotnet run --project src/AzureDriftDetector.Cli -- detect --help
```

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Built with [Azure SDK for .NET](https://github.com/Azure/azure-sdk-for-net)
- CLI powered by [System.CommandLine](https://github.com/dotnet/command-line-api)
- Beautiful console output by [Spectre.Console](https://github.com/spectreconsole/spectre.console)
