# Quest IFA Sampling

Azure Functions app (.NET 8 Isolated) demonstrating Application Insights adaptive sampling configuration that can be toggled on/off via app settings **without redeployment** — only a restart is required.

## Quick Start

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-tools)
- An Application Insights resource (for Azure deployment)

### Setup
```bash
# Copy the example settings file
cp local.settings.example.json local.settings.json

# Add your Application Insights connection string to local.settings.json
# (optional for local-only testing — telemetry will go to console)

dotnet restore
func start
```

### Endpoints
| Endpoint | Description |
|---|---|
| `GET /api/SampleHttpTrigger` | Generates a single event + trace |
| `GET /api/GenerateTelemetry?count=50` | Generates bulk telemetry (up to 1000) |
| `GET /api/SamplingStatus` | Returns current sampling configuration |

## How Sampling Works

Sampling is configured **entirely via app settings**, not in `host.json`. The `host.json` sampling is disabled (`"isEnabled": false`) so that code-based configuration in `Program.cs` takes full control.

### App Settings

| Setting | Default | Description |
|---|---|---|
| `Sampling__Enabled` | `true` | Master toggle — set to `false` to disable all sampling |
| `Sampling__Percentage` | `100` | Sampling percentage (100 = keep everything) |
| `Sampling__ExcludedTypes` | `Request;Exception` | Semicolon-separated telemetry types to always keep |

### Toggling Sampling Without Redeployment

**In Azure:**
1. Go to your Function App in the Azure Portal
2. Navigate to **Configuration > Application Settings**
3. Change `Sampling__Enabled` to `false` (or adjust `Sampling__Percentage`)
4. Save and **Restart** the Function App
5. Sampling changes take effect immediately — no code redeploy needed

**Locally:**
1. Edit `local.settings.json`
2. Change the `Sampling__Enabled` or `Sampling__Percentage` values
3. Restart the function host (`func start`)

### Excluded Types
The `Sampling__ExcludedTypes` setting accepts a semicolon-separated list of telemetry types that should **never** be sampled:
- `Request` — HTTP request telemetry
- `Exception` — Exception telemetry
- `Dependency` — Dependency calls
- `Event` — Custom events
- `Trace` — Trace/log messages

## Project Structure

```
├── .github/
│   └── copilot-instructions.md   # Workspace instructions for Copilot
├── Functions/
│   └── SampleHttpTrigger.cs      # HTTP triggers that generate telemetry
├── TelemetryConfig/
│   └── SamplingSettings.cs       # POCO for sampling configuration
├── Program.cs                    # App Insights + sampling config from IConfiguration
├── host.json                     # Minimal config (sampling disabled here)
├── local.settings.json           # Local app settings with sampling toggles
└── QuestIFASampling.csproj       # Project file
```

## Key Design Decisions

1. **`host.json` sampling is disabled** — `"isEnabled": false` ensures the built-in host sampling doesn't interfere with our code-based configuration.
2. **`Program.cs` reads from `IConfiguration`** — All sampling settings come from app settings/environment variables, which are read at startup.
3. **Restart-only changes** — Because settings are read at startup, changing app settings and restarting applies new sampling behavior without redeploying code.

## Demo Walkthrough

Follow these steps to demonstrate sampling can be toggled without redeployment.

### Step 1 — Start the app and confirm sampling is ON
```bash
func start
```
Open a browser or use curl:
```bash
curl http://localhost:7071/api/SamplingStatus
```
Expected: `"samplingEnabled": true`

### Step 2 — Generate telemetry with sampling active
```bash
curl http://localhost:7071/api/GenerateTelemetry?count=50
```
In Application Insights (Transaction Search), you'll see that some events are sampled — not all 50 will appear.

### Step 3 — Toggle sampling OFF (no redeploy)
**Locally:** In `local.settings.json`, change:
```json
"Sampling__Enabled": "false"
```
Then restart the function host (Ctrl+C, then `func start`).

**In Azure:** Go to **Configuration > Application Settings**, set `Sampling__Enabled` = `false`, Save, then **Restart** the Function App.

### Step 4 — Confirm sampling is OFF
```bash
curl http://localhost:7071/api/SamplingStatus
```
Expected: `"samplingEnabled": false`

### Step 5 — Generate telemetry with sampling disabled
```bash
curl http://localhost:7071/api/GenerateTelemetry?count=50
```
Now all 50 events will appear in Application Insights — nothing is sampled.

### Step 6 — (Optional) Adjust sampling percentage
Set `Sampling__Enabled` back to `true` and change `Sampling__Percentage` to a value like `50`, restart, and repeat to show partial sampling.

---

> **Key takeaway:** At no point was the code redeployed. Sampling behavior changed solely through app settings + restart.
